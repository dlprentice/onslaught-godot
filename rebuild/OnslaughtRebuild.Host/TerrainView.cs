// SPDX-License-Identifier: GPL-3.0-or-later
// The landscape's texture stages from bea-decomp src/DXLandscape.cpp (CDXLandscape::RenderTerrain 00545590, method 2),
// its vertex format from src/LandscapeVB.cpp (XYZ and one texture coordinate) and the material from
// src/engine.cpp (CEngine::SetupLights: the ambient render state is the map's ambient).

using Godot;
using OnslaughtRebuild.Host.Landscape;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The heightfield as a mesh (one vertex per sample) drawn as retail's four-stage landscape: the lit cache texture
/// (world position / 512) times the diffuse (the terrain material's 0.8 ambient reflectance of both lights' ambient;
/// CDXLandscape::Render sets the ambient render state to 0 for the terrain), doubled; times the detail mixer at the world position; times the scrolling cloud shadow (/ 256), doubled; times
/// the detail mixer turned by one radian at a quarter scale, doubled. The cache is built whole at one mip shift; retail's
/// per-tile levels of detail and the landscape's own geometry levels come later.
/// </summary>
public static class TerrainView
{
    internal const string Shader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled;
        // Retail blends in gamma space, and this host's output is not converted from linear (the Compatibility
        // renderer here: a constant 0.5 put through the sRGB-to-linear curve read back as 54, i.e. 0.212), so ALBEDO
        // takes the gamma-space colour as it is.
        vec3 to_linear(vec3 c) { return c; }
        // D3D's EXP table fog (FOGTABLEMODE 1, the level's colour and density) blended in gamma space as retail does,
        // on the view depth; Godot's own fog would blend in linear space.
        uniform vec3 fog_colour;
        uniform float fog_density;
        vec3 fogged(vec3 c, float depth) { return mix(fog_colour, c, clamp(exp(-fog_density * depth), 0.0, 1.0)); }
        uniform sampler2D cache : filter_linear_mipmap_anisotropic, repeat_enable;
        // The level's texture matrix (T0): world * 2^level / 512, wrapping in the finer levels' caches.
        uniform float cache_scale;
        uniform sampler2D detail : filter_linear_mipmap, repeat_enable;
        uniform sampler2D cloud : filter_linear_mipmap, repeat_enable;
        uniform vec3 diffuse;
        uniform vec2 cloud_scroll;
        varying vec2 world;
        void vertex() { world = UV; }
        void fragment() {
            vec3 c = clamp(texture(cache, world * cache_scale).rgb * diffuse * 2.0, 0.0, 1.0);
            c = c * texture(detail, world).rgb;
            c = clamp(c * texture(cloud, world / 256.0 + cloud_scroll).rgb * 2.0, 0.0, 1.0);
            float cs = cos(1.0) * 0.25, sn = sin(1.0) * 0.25;
            vec2 r = vec2(world.x * cs - world.y * sn + 0.3, world.x * sn + world.y * cs + 0.3);
            c = clamp(c * texture(detail, r).rgb * 2.0, 0.0, 1.0);
            ALBEDO = to_linear(fogged(c, -VERTEX.z));
        }
        """;

    private const float CameraLag = 0.03f;

    /// <summary>
    /// CDXLandscape's view of the camera for choosing texture levels (CalcLODs): -position + (4, 4, 0) and the look
    /// direction (the orientation's Y column), each moving CAMERA_LAG (0.03) of the way to the camera's each render
    /// (SmoothLandscapeView), from the first render's values.
    /// </summary>
    public sealed class LandscapeView
    {
        private bool _started;

        public FVector LastPos { get; private set; }

        public FVector Look { get; private set; }

        public void Advance(FVector camPos, FMatrix camOri)
        {
            var p = new FVector(-camPos.X + 4.0f, -camPos.Y + 4.0f, -camPos.Z);
            var dir = new FVector(camOri.Row0.Y, camOri.Row1.Y, camOri.Row2.Y);
            if (!_started)
            {
                _started = true;
                LastPos = p;
                Look = dir;
                return;
            }

            LastPos = (p * CameraLag) + (LastPos * (1.0f - CameraLag));
            Look = (dir * CameraLag) + (Look * (1.0f - CameraLag));
        }
    }

    /// <summary>
    /// Each 8-metre tile's texture level, as CalcLODs and ChooseLandscapeTexture pick it from the smoothed view: within
    /// 128 of the camera (counting the tile's mid height less 8 vertically) a tile within 64 of a point 60 ahead takes
    /// level 2, within 32 of a point 28 ahead level 3, within 16 of a point 12 ahead level 4; otherwise within 128 it is
    /// level 1, and beyond, the base level 0. (Retail's geometry levels of detail are not modelled: every tile is drawn
    /// at full resolution.)
    /// </summary>
    public static int[] ChooseLevels(HeightField map, LandscapeView view, int maxLevels = 5)
        => ChooseLevels(TileMidHeights(map), view, maxLevels);

    // HeightField is immutable for this host slice. The live owner computes these 4096 bounds once, preserving
    // the same wrapped Collide samples and floating-point order as the snapshot selector.
    internal static float[] TileMidHeights(HeightField map)
    {
        var mid = new float[4096];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float lo = 10000.0f;
                float hi = -10000.0f;
                for (int j = 0; j <= 8; j++)
                {
                    for (int i = 0; i <= 8; i++)
                    {
                        float h = map.Collide(new FVector(((x * 8) + i) & 511, ((y * 8) + j) & 511, 0));
                        lo = Math.Min(lo, h);
                        hi = Math.Max(hi, h);
                    }
                }

                mid[(y * 64) + x] = (lo + hi) * 0.5f;
            }
        }

        return mid;
    }

    internal static int[] ChooseLevels(float[] midHeights, LandscapeView view, int maxLevels = 5)
    {
        var levels = new int[4096];
        FVector cam = view.LastPos;
        FVector look = view.Look;
        for (int y = 0; y < 64; y++)
        {
            float camY = cam.Y + (y * 8.0f);
            float dy2 = camY * camY;
            for (int x = 0; x < 64; x++)
            {
                float camX = cam.X + (x * 8.0f);
                float dz = cam.Z + midHeights[(y * 64) + x];
                float dz2 = dz * dz;
                float dist2 = (camX * camX) + dz2 + dy2;
                int tex = 0;
                if (maxLevels >= 3 && dist2 <= 16384.0f)
                {
                    dz2 = Math.Max(dz2 - 64.0f, 0.0f);
                    tex = 1;
                    float ax = camX - (look.X * 60.0f);
                    float ay = camY - (look.Y * 60.0f);
                    if ((ax * ax) + (ay * ay) + dz2 < 4096.0f)
                    {
                        tex = 2;
                        float bx = camX - (look.X * 28.0f);
                        float by = camY - (look.Y * 28.0f);
                        if (maxLevels > 3 && (bx * bx) + (by * by) + dz2 < 1024.0f)
                        {
                            tex = 3;
                            float cx = camX - (look.X * 12.0f);
                            float cy = camY - (look.Y * 12.0f);
                            if (maxLevels > 4 && (cx * cx) + (cy * cy) + dz2 < 256.0f)
                            {
                                tex = 4;
                            }
                        }
                    }
                }

                levels[(y * 64) + x] = tex;
            }
        }

        return levels;
    }

    private static ImageTexture CacheTexture(ushort[] texels, int side)
    {
        using Image cache = CacheImage(texels, side);
        return ImageTexture.CreateFromImage(cache);
    }

    internal static Image CacheImage(ushort[] texels, int side)
    {
        var bytes = new byte[texels.Length * 3];
        for (int i = 0; i < texels.Length; i++)
        {
            int v = texels[i];
            bytes[i * 3] = (byte)(((v >> 11) & 31) * 255 / 31);
            bytes[(i * 3) + 1] = (byte)(((v >> 5) & 63) * 255 / 63);
            bytes[(i * 3) + 2] = (byte)((v & 31) * 255 / 31);
        }

        Image cache = Image.CreateFromData(side, side, false, Image.Format.Rgb8, bytes);
        cache.GenerateMipmaps();
        return cache;
    }

    /// <summary>
    /// Retained terrain for the live host. Geometry is sampled once; texture cache membership and dirty chunks
    /// follow the same per-tile levels as Build. Dispose it before releasing the level's texture library.
    /// </summary>
    public static TerrainLiveView BuildLive(HeightField map, TerrainTexture terrain, TextureLibrary textures,
        int[] tileLevels, Vector2 cloudDrift) => new(map, terrain, textures, tileLevels, cloudDrift);

    /// <summary>The landscape: one surface per texture level in use, each with its cache and scale.</summary>
    public static Node3D Build(HeightField map, TerrainTexture terrain, TextureLibrary textures, int[] tileLevels, Vector2 cloudDrift)
    {
        const int Size = 512;
        var heights = new float[Size + 1, Size + 1];
        for (int y = 0; y <= Size; y++)
        {
            for (int x = 0; x <= Size; x++)
            {
                heights[x, y] = map.HeightScale * map.GetHeightSample(x, y);
            }
        }

        // D3D's lighting with the terrain material (ambient 0.8, no diffuse or emissive): 0.8 times the lights' ambient,
        // which ApplyLight(i, TRUE) sets to their diffuse colour (the sun and the anti-sun, each byte / 256); the ambient
        // render state is 0 while the terrain draws (CDXLandscape::Render).
        static Vector3 Bytes(uint c, float scale) => new Vector3((c >> 16) & 255, (c >> 8) & 255, c & 255) * scale;
        Vector3 diffuse = (Bytes(terrain.Sun, 1 / 256f) + Bytes(terrain.AntiSun, 1 / 256f)) * 0.8f;
        diffuse = diffuse.Clamp(Vector3.Zero, Vector3.One);
        var shader = new Shader { Code = Shader };
        string detailName = $@"mixers\detail{terrain.DetailTexture:00}.tga";
        ImageTexture detail = textures.Get(detailName) ?? throw new InvalidOperationException($"no texture {detailName}");
        ImageTexture cloud = textures.Find(@"clouds\shadow.tga", 4) ?? throw new InvalidOperationException(@"no texture clouds\shadow.tga");
        var root = new Node3D { Name = "Landscape" };
        for (int level = 0; level <= 4; level++)
        {
            List<int> tiles = [];
            for (int t = 0; t < 4096; t++)
            {
                if (tileLevels[t] == level)
                {
                    tiles.Add(t);
                }
            }

            if (tiles.Count == 0)
            {
                continue;
            }

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            void V(int x, int y)
            {
                st.SetUV(new Vector2(x, y));
                st.AddVertex(Host.ToGodot(new FVector(x, y, heights[x, y])));
            }

            foreach (int t in tiles)
            {
                int x0 = (t & 63) * 8;
                int y0 = (t >> 6) * 8;
                for (int y = y0; y < y0 + 8; y++)
                {
                    for (int x = x0; x < x0 + 8; x++)
                    {
                        V(x, y);
                        V(x, y + 1);
                        V(x + 1, y);
                        V(x + 1, y);
                        V(x, y + 1);
                        V(x + 1, y + 1);
                    }
                }
            }

            ImageTexture cache = level == 0 ? CacheTexture(terrain.BuildMap(0), 512) : CacheTexture(terrain.BuildCacheLevel(level, tiles), 512);
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("cache", cache);
            material.SetShaderParameter("cache_scale", (1 << level) / 512.0f);
            material.SetShaderParameter("detail", detail);
            material.SetShaderParameter("cloud", cloud);
            material.SetShaderParameter("diffuse", diffuse);
            material.SetShaderParameter("fog_colour", Bytes(terrain.Fog, 1 / 255f));
            material.SetShaderParameter("fog_density", terrain.FogDensity);
            material.SetShaderParameter("cloud_scroll", cloudDrift);
            root.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = material, Name = $"Level{level}" });
        }

        return root;
    }
}
