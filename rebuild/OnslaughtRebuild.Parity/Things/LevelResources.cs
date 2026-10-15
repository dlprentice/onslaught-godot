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

    /// <param name="fileName">The resource file's game path (its extension decides packing).</param>
    /// <param name="bytes">The file as stored.</param>
    /// <param name="meshFile">Reads data\resources\meshes\m_&lt;name&gt;.aya by game path.</param>
    /// <param name="doWeWantMesh">GAME.DoWeWantMesh for optional meshes (null: none is wanted).</param>
    public static LevelResources Read(string fileName, byte[] bytes, Func<string, (string Name, byte[] Bytes)> meshFile,
        Func<string, bool>? doWeWantMesh = null)
    {
        var c = new ChunkReader(MemBuffer.FromFile(fileName, bytes));
        var meshes = new Dictionary<string, MeshData>(StringComparer.OrdinalIgnoreCase);
        HeightField? heightField = null;
        (int, int, uint, uint)? worlds = null;
        uint tag;
        while ((tag = c.GetNext()) != 0)
        {
            if (tag == ChunkReader.MakeId("MESH"))
            {
                MeshData? mesh = MeshData.Deserialize(c, meshFile, doWeWantMesh);
                // CMesh::GetMesh searches the loaded meshes by their own name (mName), ignoring case; the first wins.
                if (mesh is not null && mesh.Name.Length > 0)
                {
                    meshes.TryAdd(mesh.Name, mesh);
                }
            }
            else if (tag == ChunkReader.MakeId("ERES"))
            {
                // The chunk reader is flat: the engine's sub-chunks follow, so the rest of ERES (the mixer map and
                // textures) is skipped to the end the ERES header gave.
                uint end = c.WhereAmI + c.Size;
                heightField = EngineResources.Read(c).HeightField;
                c.File.Skip((int)(end - c.WhereAmI));
            }
            else if (tag == ChunkReader.MakeId("WRES"))
            {
                worlds = WorldFile.ReadResourcePositions(c);
            }
            else
            {
                c.Skip();
            }
        }

        return new LevelResources
        {
            FileName = fileName,
            FileBytes = bytes,
            Meshes = meshes,
            HeightField = heightField ?? throw new InvalidDataException("no ERES chunk"),
            Worlds = worlds ?? throw new InvalidDataException("no WRES chunk"),
        };
    }

    /// <summary>A reader positioned at a world's data (CWorld::Load reopens the resource file and skips to it).</summary>
    public MemBuffer OpenAt(uint position)
    {
        MemBuffer file = MemBuffer.FromFile(FileName, FileBytes);
        file.Skip((int)position);
        return file;
    }
}
