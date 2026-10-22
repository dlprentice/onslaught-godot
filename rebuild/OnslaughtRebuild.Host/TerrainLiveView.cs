// SPDX-License-Identifier: GPL-3.0-only
// Retained Godot presentation of TerrainView's terrain. This changes resource ownership and upload granularity,
// not the landscape texture calculations or the parity core's heightfield.

using Godot;
using OnslaughtRebuild.Host.Landscape;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Host;

/// <summary>
/// Live landscape with 64 retained chunks and five shared cache/material owners. Each chunk keeps its sampled
/// vertices and UVs; a texture-level transition only regroups that chunk's triangle indices. A finer cache is
/// rebuilt only when its tile membership changes, in the same ascending tile order as TerrainView.Build. Retail's
/// geometry LOD and static shadows remain outside this host slice.
/// </summary>
public sealed class TerrainLiveView : IDisposable
{
    private const int ChunkTiles = 8;
    private const int ChunkCells = ChunkTiles * 8;
    private const int ChunkSide = ChunkCells + 1;
    private const int ChunksAcross = 64 / ChunkTiles;

    private readonly TerrainTexture _terrain;
    private readonly float[] _midHeights;
    private readonly int[] _tileLevels = new int[4096];
    private readonly Shader _shader;
    private readonly ShaderMaterial?[] _materials = new ShaderMaterial?[5];
    private readonly ImageTexture?[] _caches = new ImageTexture?[5];
    private readonly Chunk?[] _chunks = new Chunk?[ChunksAcross * ChunksAcross];
    private Vector2 _cloudDrift;
    private bool _disposed;

    private sealed record Chunk(ArrayMesh Mesh, Vector3[] Vertices, Vector2[] Uvs);

    public Node3D Root { get; }

    /// <summary>Diagnostic counts for resource/upload checks, not parity claims.</summary>
    public int CacheUploads { get; private set; }

    public int ChunkRebuilds { get; private set; }

    internal TerrainLiveView(HeightField map, TerrainTexture terrain, TextureLibrary textures,
        int[] tileLevels, Vector2 cloudDrift)
    {
        ValidateLevels(tileLevels);
        string detailName = $@"mixers\detail{terrain.DetailTexture:00}.tga";
        ImageTexture detail = textures.Get(detailName) ?? throw new InvalidOperationException($"no texture {detailName}");
        ImageTexture cloud = textures.Find(@"clouds\shadow.tga", 4)
            ?? throw new InvalidOperationException(@"no texture clouds\shadow.tga");
        _terrain = terrain;
        _midHeights = TerrainView.TileMidHeights(map);
        _cloudDrift = cloudDrift;
        Array.Fill(_tileLevels, -1);
        Root = new Node3D { Name = "Landscape" };
        _shader = new Shader { Code = TerrainView.Shader };
        try
        {
            static Vector3 Bytes(uint c, float scale) => new Vector3((c >> 16) & 255, (c >> 8) & 255, c & 255) * scale;
            Vector3 diffuse = ((Bytes(terrain.Sun, 1 / 256f) + Bytes(terrain.AntiSun, 1 / 256f)) * 0.8f)
                .Clamp(Vector3.Zero, Vector3.One);
            for (int level = 0; level < 5; level++)
            {
                // The wrapping finer caches start blank just as BuildCacheLevel does. Their first populated images
                // are uploaded by Update. Level zero is the immutable whole map, even if no tile currently uses it.
                using Image image = TerrainView.CacheImage(level == 0 ? terrain.BuildMap(0) : new ushort[512 * 512], 512);
                var cache = ImageTexture.CreateFromImage(image);
                _caches[level] = cache;
                CacheUploads++;
                var material = new ShaderMaterial { Shader = _shader };
                _materials[level] = material;
                material.SetShaderParameter("cache", cache);
                material.SetShaderParameter("cache_scale", (1 << level) / 512.0f);
                material.SetShaderParameter("detail", detail);
                material.SetShaderParameter("cloud", cloud);
                material.SetShaderParameter("diffuse", diffuse);
                material.SetShaderParameter("fog_colour", Bytes(terrain.Fog, 1 / 255f));
                material.SetShaderParameter("fog_density", terrain.FogDensity);
                material.SetShaderParameter("cloud_scroll", cloudDrift);
            }

            for (int cy = 0; cy < ChunksAcross; cy++)
            {
                for (int cx = 0; cx < ChunksAcross; cx++)
                {
                    var vertices = new Vector3[ChunkSide * ChunkSide];
                    var uvs = new Vector2[vertices.Length];
                    for (int y = 0; y < ChunkSide; y++)
                    {
                        for (int x = 0; x < ChunkSide; x++)
                        {
                            int wx = (cx * ChunkCells) + x;
                            int wy = (cy * ChunkCells) + y;
                            int at = (y * ChunkSide) + x;
                            vertices[at] = Host.ToGodot(new FVector(wx, wy, map.HeightScale * map.GetHeightSample(wx, wy)));
                            uvs[at] = new Vector2(wx, wy);
                        }
                    }

                    var mesh = new ArrayMesh();
                    _chunks[(cy * ChunksAcross) + cx] = new Chunk(mesh, vertices, uvs);
                    Root.AddChild(new MeshInstance3D { Mesh = mesh, Name = $"Chunk{cx}_{cy}" });
                }
            }

            Update(tileLevels, cloudDrift);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>The snapshot selector with immutable tile-height bounds cached at construction.</summary>
    public int[] ChooseLevels(TerrainView.LandscapeView view, int maxLevels = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TerrainView.ChooseLevels(_midHeights, view, maxLevels);
    }

    public void Update(int[] tileLevels, Vector2 cloudDrift)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateLevels(tileLevels);
        Span<bool> changedLevels = stackalloc bool[5];
        Span<bool> changedChunks = stackalloc bool[ChunksAcross * ChunksAcross];
        changedLevels.Clear();
        changedChunks.Clear();
        for (int tile = 0; tile < _tileLevels.Length; tile++)
        {
            int previous = _tileLevels[tile];
            int current = tileLevels[tile];
            if (previous == current)
            {
                continue;
            }

            if (previous >= 0)
            {
                changedLevels[previous] = true;
            }

            changedLevels[current] = true;
            changedChunks[((tile >> 6) / ChunkTiles * ChunksAcross) + ((tile & 63) / ChunkTiles)] = true;
            _tileLevels[tile] = current;
        }

        // Clearing removed membership matters: wrapping tiles share cache locations, and mipmapped edge samples
        // depend on the surrounding texels. Rebuild the entire affected cache rather than patching only new tiles.
        for (int level = 1; level < 5; level++)
        {
            if (!changedLevels[level])
            {
                continue;
            }

            using Image image = TerrainView.CacheImage(_terrain.BuildCacheLevel(level, TilesAtLevel(_tileLevels, level)), 512);
            _caches[level]!.Update(image);
            CacheUploads++;
        }

        for (int chunk = 0; chunk < _chunks.Length; chunk++)
        {
            if (changedChunks[chunk])
            {
                RebuildChunk(chunk);
            }
        }

        if (cloudDrift != _cloudDrift)
        {
            foreach (ShaderMaterial? material in _materials)
            {
                material!.SetShaderParameter("cloud_scroll", cloudDrift);
            }

            _cloudDrift = cloudDrift;
        }
    }

    // Keep the snapshot's ascending order: later tiles overwrite earlier wrapping cache locations.
    internal static List<int> TilesAtLevel(int[] tileLevels, int level)
    {
        List<int> tiles = [];
        for (int tile = 0; tile < tileLevels.Length; tile++)
        {
            if (tileLevels[tile] == level)
            {
                tiles.Add(tile);
            }
        }

        return tiles;
    }

    // The returned triangle indices retain Build's tile/cell order and winding. This pure grouping operation is
    // separate from the GPU upload so geometry admission can compare expanded triangles without launching Godot.
    internal static int[] ChunkIndices(int[] tileLevels, int index, int level)
    {
        int tx0 = (index % ChunksAcross) * ChunkTiles;
        int ty0 = (index / ChunksAcross) * ChunkTiles;
        List<int> indices = [];
        for (int ty = 0; ty < ChunkTiles; ty++)
        {
            for (int tx = 0; tx < ChunkTiles; tx++)
            {
                if (tileLevels[((ty0 + ty) * 64) + tx0 + tx] != level)
                {
                    continue;
                }

                for (int y = ty * 8; y < (ty + 1) * 8; y++)
                {
                    for (int x = tx * 8; x < (tx + 1) * 8; x++)
                    {
                        int a = (y * ChunkSide) + x;
                        int b = a + ChunkSide;
                        indices.Add(a);
                        indices.Add(b);
                        indices.Add(a + 1);
                        indices.Add(a + 1);
                        indices.Add(b);
                        indices.Add(b + 1);
                    }
                }
            }
        }

        return indices.ToArray();
    }

    private void RebuildChunk(int index)
    {
        Chunk chunk = _chunks[index]!;
        chunk.Mesh.ClearSurfaces();
        for (int level = 0; level < 5; level++)
        {
            int[] indices = ChunkIndices(_tileLevels, index, level);
            if (indices.Length == 0)
            {
                continue;
            }

            using var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = chunk.Vertices;
            arrays[(int)Mesh.ArrayType.TexUV] = chunk.Uvs;
            arrays[(int)Mesh.ArrayType.Index] = indices;
            chunk.Mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            chunk.Mesh.SurfaceSetMaterial(chunk.Mesh.GetSurfaceCount() - 1, _materials[level]!);
        }

        ChunkRebuilds++;
    }

    private static void ValidateLevels(int[] tileLevels)
    {
        ArgumentNullException.ThrowIfNull(tileLevels);
        if (tileLevels.Length != 4096)
        {
            throw new ArgumentException("Terrain needs exactly 4096 tile levels.", nameof(tileLevels));
        }

        foreach (int level in tileLevels)
        {
            if ((uint)level > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(tileLevels), "Terrain texture levels must be between 0 and 4.");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (GodotObject.IsInstanceValid(Root))
        {
            Root.GetParent()?.RemoveChild(Root);
            Root.Free();
        }

        foreach (Chunk? chunk in _chunks)
        {
            chunk?.Mesh.Dispose();
        }

        foreach (ShaderMaterial? material in _materials)
        {
            material?.Dispose();
        }

        foreach (ImageTexture? cache in _caches)
        {
            cache?.Dispose();
        }

        _shader.Dispose();
        // Detail and cloud belong to TextureLibrary and are deliberately not disposed here.
    }
}
