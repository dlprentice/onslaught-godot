// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/ResourceAccumulator.cpp (CResourceAccumulator::ReadResources 004d7200: the chunk loop and
// its MESH, ERES and WRES cases), in Stuart's release.

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// What a level's resource file gives the simulation: its meshes (by their header names), the height field, and where
/// the base and real worlds start in the decompressed stream. Textures, imposters, shadows and the other rendering
/// chunks are skipped, as their data never reaches the probes.
/// </summary>
public sealed class LevelResources
{
    public required string FileName { get; init; }

    public required byte[] FileBytes { get; init; }

    public required Dictionary<string, MeshData> Meshes { get; init; }

    public required HeightField HeightField { get; init; }

    public required (int BaseLevel, int WorldLevel, uint BasePosition, uint WorldPosition) Worlds { get; init; }

    /// <summary>Where the ERES chunk's contents start in the decompressed stream, for a renderer to read them whole.</summary>
    public uint EngineResourcesPosition { get; init; }

    /// <param name="fileName">The resource file's game path (its extension decides packing).</param>
    /// <param name="bytes">The file as stored.</param>
    /// <param name="meshFile">Reads data\resources\meshes\m_&lt;name&gt;.aya by game path.</param>
    /// <param name="doWeWantMesh">GAME.DoWeWantMesh for optional meshes (null: none is wanted).</param>
    /// <param name="otherChunk">
    /// A renderer's reader for the chunks the simulation skips (TEXT and the rest), given the reader just past the tag:
    /// it returns true when it read the whole chunk, false to have it skipped. Null skips them all.
    /// </param>
    public static LevelResources Read(string fileName, byte[] bytes, Func<string, (string Name, byte[] Bytes)> meshFile,
        Func<string, bool>? doWeWantMesh = null, Func<uint, ChunkReader, bool>? otherChunk = null)
    {
        var c = new ChunkReader(MemBuffer.FromFile(fileName, bytes));
        HeightField? heightField = null;
        (int, int, uint, uint)? worlds = null;
        uint engineResources = 0;
        Dictionary<string, MeshData> meshes = ReadMeshChunks(c, meshFile, doWeWantMesh, (tag, chunk) =>
        {
            if (tag == ChunkReader.MakeId("ERES"))
            {
                uint end = chunk.WhereAmI + chunk.Size;
                engineResources = chunk.WhereAmI;
                heightField = EngineResources.Read(chunk).HeightField;
                chunk.File.Skip((int)(end - chunk.WhereAmI));
                return true;
            }
            if (tag == ChunkReader.MakeId("WRES"))
            {
                worlds = WorldFile.ReadResourcePositions(chunk);
                return true;
            }
            return otherChunk?.Invoke(tag, chunk) == true;
        });

        return new LevelResources
        {
            FileName = fileName,
            FileBytes = bytes,
            Meshes = meshes,
            HeightField = heightField ?? throw new InvalidDataException("no ERES chunk"),
            Worlds = worlds ?? throw new InvalidDataException("no WRES chunk"),
            EngineResourcesPosition = engineResources,
        };
    }

    /// <summary>ReadResources(-2)'s meshes: front-end resources have no ERES or WRES world chunks.</summary>
    public static Dictionary<string, MeshData> ReadMeshes(string fileName, byte[] bytes,
        Func<string, (string Name, byte[] Bytes)> meshFile) =>
        ReadMeshChunks(new ChunkReader(MemBuffer.FromFile(fileName, bytes)), meshFile, null, null);

    private static Dictionary<string, MeshData> ReadMeshChunks(ChunkReader c,
        Func<string, (string Name, byte[] Bytes)> meshFile, Func<string, bool>? doWeWantMesh,
        Func<uint, ChunkReader, bool>? otherChunk)
    {
        var meshes = new Dictionary<string, MeshData>(StringComparer.OrdinalIgnoreCase);
        uint tag;
        while ((tag = c.GetNext()) != 0)
        {
            if (tag == ChunkReader.MakeId("MESH"))
            {
                MeshData? mesh = MeshData.Deserialize(c, meshFile, doWeWantMesh);
                // CMesh::GetMesh searches the loaded meshes by mName, ignoring case; the first wins.
                if (mesh is not null && mesh.Name.Length > 0) meshes.TryAdd(mesh.Name, mesh);
            }
            else if (otherChunk?.Invoke(tag, c) != true) c.Skip();
        }
        return meshes;
    }

    /// <summary>A reader positioned at a world's data (CWorld::Load reopens the resource file and skips to it).</summary>
    public MemBuffer OpenAt(uint position)
    {
        MemBuffer file = MemBuffer.FromFile(FileName, FileBytes);
        file.Skip((int)position);
        return file;
    }
}
