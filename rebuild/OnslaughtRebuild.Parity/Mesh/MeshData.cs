// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/mesh.cpp (CMesh::Deserialize 004aab90, GetNumEmitters 004aa4e0, GetEmitterNameByID
// 004aa500, GetEmitterSubpartByID 004aa5a0, GetMovieCameraSubpart 004aa5e0, GetAnimModeByName 004aa630,
// GetAnimModePtr 004aa680, GetFrameIncrement 004aa7e0, GetEmitterSubpart 004aa820, GetMeshSubpart 004aa8a0) and
// src/mesh.h (the CMesh, CMeshTexture, CAnimMode, CCutsceneSequence and CEmitter layouts). mesh.cpp is in Stuart's
// release.

using System.Buffers.Binary;
using System.Text;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>CAnimMode: a named animation, its mode number, frame range, frame scaler and rate (36 bytes).</summary>
public sealed record AnimMode(string ModeName, int Mode, int Start, int Last, int FrameScaler, float Rate)
{
    /// <summary>EAnimMode's AM_INVALID.</summary>
    public const int Invalid = -1;
}

/// <summary>CEmitter (0x150 bytes): a named, numbered point on a part.</summary>
public sealed record Emitter(FVector Pos, FMatrix Orientation, MeshPart? MeshPart, int TrackingVertex, int SubpartEmitter,
    string Name, int Number, byte[] Record);

/// <summary>CCutsceneSequence: a frame range drawn from one camera part.</summary>
public sealed record CutsceneSequence(MeshPart? Camera, int Start, int End);

/// <summary>CMeshTexture as stored: the 0x24-byte record, its frames' five arrays and the texture name.</summary>
public sealed record MeshTexture(byte[] Record, float[] Blend, float[] U, float[] V, float[] UScale, float[] VScale,
    string TextureName);

/// <summary>
/// A mesh as CMesh::Deserialize builds it from a level resource file. A resource file's MESH chunk holds the mesh's
/// header (name, runtime id, optional flag, keep-buckets flag); the body comes from the mesh's own packed file
/// (data\resources\meshes\m_&lt;name&gt;.aya) when the name is set, else from the chunk itself. A mesh that names the
/// next level of detail reads that one's header from the resource chunk and its body from the same source.
/// </summary>
public sealed class MeshData
{
    /// <summary>sizeof(CMesh).</summary>
    public const int RecordSize = 0x174;

    private MeshPart[] _parts = [];

    private MeshData()
    {
    }

    /// <summary>The header's name (the mesh file is m_&lt;name&gt;.aya).</summary>
    public string HeaderName { get; private set; } = "";

    /// <summary>mResourceID: the header's runtime id.</summary>
    public int ResourceId { get; private set; }

    public int Optional { get; private set; }

    public bool KeepBuckets { get; private set; }

    /// <summary>For a mesh read from its own file: the file's bytes left unread after it and its chain (-1 otherwise).</summary>
    public int MeshFileUnread { get; private set; } = -1;

    /// <summary>The CMesh record as read (pointer fields are presence markers).</summary>
    public byte[] Record { get; private set; } = [];

    public int NumTextures => Int(0x04);
    public int NumSequences => Int(0x0c);
    public int NumAnimModes => Int(0x14);
    public int NumEmitters => Int(0x1c);
    public int NumParts => Int(0x15c);
    public float Radius => BinaryPrimitives.ReadSingleLittleEndian(Record.AsSpan(0x164));
    public float CueFactor => BinaryPrimitives.ReadSingleLittleEndian(Record.AsSpan(0x168));

    /// <summary>mName (+0x24): the record's own 300-byte name.</summary>
    public string Name => CString(Record.AsSpan(0x24, 300));

    public IReadOnlyList<MeshTexture> Textures { get; private set; } = [];
    public IReadOnlyList<MeshPart> Parts => _parts;
    public IReadOnlyList<CutsceneSequence> Sequences { get; private set; } = [];
    public IReadOnlyList<AnimMode> AnimModes { get; private set; } = [];
    public IReadOnlyList<Emitter> Emitters { get; private set; } = [];
    public BoundingBox? BoundingBox { get; private set; }

    /// <summary>mNextInChain: the next level of detail, whose emitters count after this one's.</summary>
    public MeshData? NextInChain { get; private set; }

    /// <summary>GetPart: mParts[n] (null outside the array, where retail would read past it).</summary>
    public MeshPart? GetPart(int n) => n >= 0 && n < _parts.Length ? _parts[n] : null;

    /// <summary>
    /// CMesh::Deserialize for one MESH chunk of a resource file, read through <paramref name="c"/> after its tag.
    /// <paramref name="meshFile"/> returns the bytes of a game-relative path (data\resources\meshes\m_&lt;name&gt;.aya);
    /// <paramref name="doWeWantMesh"/> is CGame::DoWeWantMesh for optional meshes (the cockpit and wingman meshes the
    /// game's settings name); without it every optional mesh is skipped. Returns null for a skipped mesh.
    /// </summary>
    public static MeshData? Deserialize(ChunkReader c, Func<string, (string Name, byte[] Bytes)> meshFile,
        Func<string, bool>? doWeWantMesh = null) =>
        Deserialize(c, null, meshFile, doWeWantMesh, readingMeshFile: false);

    private static MeshData? Deserialize(ChunkReader c, ChunkReader? data, Func<string, (string Name, byte[] Bytes)> meshFile,
        Func<string, bool>? doWeWantMesh, bool readingMeshFile)
    {
        data ??= c;
        var mesh = new MeshData();

        c.GetNext();
        data.GetNext();
        var name = new byte[300];
        c.Read(name);
        mesh.HeaderName = CString(name);
        mesh.ResourceId = c.ReadInt32();
        mesh.Optional = c.ReadInt32();
        if (mesh.Optional == 1 && !(doWeWantMesh?.Invoke(mesh.HeaderName) ?? false))
        {
            data.Skip();
            return null;
        }

        Span<byte> keep = stackalloc byte[1];
        c.Read(keep);
        mesh.KeepBuckets = keep[0] != 0;

        bool ownsFile = false;
        if (!readingMeshFile && mesh.HeaderName.Length > 0)
        {
            (string fileName, byte[] bytes) = meshFile($@"data\resources\meshes\m_{mesh.HeaderName}.aya");
            data = new ChunkReader(MemBuffer.FromFile(fileName, bytes));
            ownsFile = true;
            readingMeshFile = true;
        }

        data.GetNext();
        mesh.Record = new byte[RecordSize];
        data.Read(mesh.Record);

        // the textures: their records, then each one's frames and name
        data.GetNext();
        var textureRecords = new byte[0x24 * Math.Max(mesh.NumTextures, 0)];
        data.Read(textureRecords);
        var textures = new List<MeshTexture>();
        var text = new byte[128];
        for (int i = 0; i < mesh.NumTextures; i++)
        {
            byte[] record = textureRecords.AsSpan(i * 0x24, 0x24).ToArray();
            int frames = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x08));
            data.GetNext();
            data.GetNext();
            float[] blend = ReadFloats(data, frames);
            float[] u = ReadFloats(data, frames);
            float[] v = ReadFloats(data, frames);
            float[] uScale = ReadFloats(data, frames);
            float[] vScale = ReadFloats(data, frames);
            data.Read(text);
            textures.Add(new MeshTexture(record, blend, u, v, uScale, vScale, CString(text)));
        }

        mesh.Textures = textures;

        mesh._parts = new MeshPart[Math.Max(mesh.NumParts, 0)];
        for (int i = 0; i < mesh._parts.Length; i++)
        {
            mesh._parts[i] = new MeshPart(mesh, i);
        }

        foreach (MeshPart part in mesh._parts)
        {
            part.Deserialize(data);
            if (!mesh.KeepBuckets)
            {
                part.DropPolyBucket();
            }
        }

        var sequences = new List<CutsceneSequence>();
        if (mesh.NumSequences > 0)
        {
            data.GetNext();
            var records = new byte[12 * mesh.NumSequences];
            data.Read(records);
            for (int i = 0; i < mesh.NumSequences; i++)
            {
                MeshPart? camera = null;
                if (BinaryPrimitives.ReadUInt32LittleEndian(records.AsSpan(i * 12)) != 0)
                {
                    data.GetNext();
                    camera = mesh.GetPart(data.ReadInt32());
                }

                sequences.Add(new CutsceneSequence(camera, BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan((i * 12) + 4)),
                    BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan((i * 12) + 8))));
            }
        }

        mesh.Sequences = sequences;

        var anims = new List<AnimMode>();
        if (mesh.NumAnimModes > 0)
        {
            data.GetNext();
            var records = new byte[36 * mesh.NumAnimModes];
            data.Read(records);
            for (int i = 0; i < mesh.NumAnimModes; i++)
            {
                ReadOnlySpan<byte> r = records.AsSpan(i * 36, 36);
                anims.Add(new AnimMode(CString(r[..16]), BinaryPrimitives.ReadInt32LittleEndian(r[16..]),
                    BinaryPrimitives.ReadInt32LittleEndian(r[20..]), BinaryPrimitives.ReadInt32LittleEndian(r[24..]),
                    BinaryPrimitives.ReadInt32LittleEndian(r[28..]), BinaryPrimitives.ReadSingleLittleEndian(r[32..])));
            }
        }

        mesh.AnimModes = anims;

        data.GetNext();
        mesh.BoundingBox = MeshPart.ReadBoundingBox(data);

        var emitters = new List<Emitter>();
        if (mesh.NumEmitters > 0)
        {
            data.GetNext();
            int size = data.ReadInt32();
            for (int i = 0; i < mesh.NumEmitters; i++)
            {
                var record = new byte[size];
                data.Read(record);
                MeshPart? part = null;
                if (BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0x40)) != 0)
                {
                    part = mesh.GetPart(data.ReadInt32());
                }

                emitters.Add(new Emitter(MeshBytes.Vector(record, 0), MeshBytes.Matrix(record, 0x10), part,
                    BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x44)),
                    BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x48)), CString(record.AsSpan(0x4c, 256)),
                    BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x14c)), record));
            }
        }

        mesh.Emitters = emitters;

        if (BinaryPrimitives.ReadUInt32LittleEndian(mesh.Record.AsSpan(0x08)) != 0)
        {
            mesh.NextInChain = Deserialize(c, data, meshFile, doWeWantMesh, readingMeshFile);
        }

        // Retail closes the mesh file in the innermost call; nothing is read from it after that, so the bytes left are
        // measured here where the file was opened.
        if (ownsFile)
        {
            mesh.MeshFileUnread = data.File.Length - (int)data.File.Position;
        }

        return mesh;
    }

    /// <summary>GetNumEmitters: this mesh's emitters and its chain's.</summary>
    public int GetNumEmitters() => NextInChain is null ? NumEmitters : NextInChain.GetNumEmitters() + NumEmitters;

    /// <summary>GetEmitterNameByID: the emitter's name and number by its position along the chain ("" and -1 past it).</summary>
    public (string Name, int Id) GetEmitterNameByID(int n) =>
        n < NumEmitters ? (Emitters[n].Name, Emitters[n].Number)
        : NextInChain is not null ? NextInChain.GetEmitterNameByID(n - NumEmitters) : ("", -1);

    /// <summary>GetEmitterSubpartByID: the part of the emitter at that position along the chain.</summary>
    public MeshPart? GetEmitterSubpartByID(int n) =>
        n < NumEmitters ? Emitters[n].MeshPart : NextInChain?.GetEmitterSubpartByID(n - NumEmitters);

    /// <summary>GetMovieCameraSubpart: the camera part of the first sequence covering the frame, along the chain.</summary>
    public MeshPart? GetMovieCameraSubpart(int frame)
    {
        foreach (CutsceneSequence s in Sequences)
        {
            if (s.Start <= frame && s.End >= frame)
            {
                return s.Camera;
            }
        }

        return NextInChain?.GetMovieCameraSubpart(frame);
    }

    /// <summary>GetAnimModeByName: the mode number of the animation with that name (any case), else AM_INVALID.</summary>
    public int GetAnimModeByName(string name)
    {
        foreach (AnimMode a in AnimModes)
        {
            if (string.Equals(name, a.ModeName, StringComparison.OrdinalIgnoreCase))
            {
                return a.Mode;
            }
        }

        return AnimMode.Invalid;
    }

    /// <summary>GetAnimModePtr: the first animation with that mode number.</summary>
    public AnimMode? GetAnimModePtr(int mode) => AnimModes.FirstOrDefault(a => a.Mode == mode);

    /// <summary>GetFrameIncrement: the rate of the first animation with that mode, and its position (-1 and 0 if none).</summary>
    public float GetFrameIncrement(int mode, out int realIndex)
    {
        for (int i = 0; i < AnimModes.Count; i++)
        {
            if (AnimModes[i].Mode == mode)
            {
                realIndex = i;
                return AnimModes[i].Rate;
            }
        }

        realIndex = -1;
        return 0;
    }

    /// <summary>GetEmitterSubpart: the part of the emitter with that name (any case) and number, along the chain.</summary>
    public MeshPart? GetEmitterSubpart(string name, int id)
    {
        foreach (Emitter e in Emitters)
        {
            if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) && e.Number == id)
            {
                return e.MeshPart;
            }
        }

        return NextInChain?.GetEmitterSubpart(name, id);
    }

    /// <summary>GetMeshSubpart: this mesh's part with that name (any case); the chain is not searched.</summary>
    public MeshPart? GetMeshSubpart(string name) =>
        _parts.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => $"mesh '{HeaderName}' ({NumParts} parts, {NumEmitters} emitters)";

    private int Int(int at) => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(at));

    private static float[] ReadFloats(ChunkReader c, int count)
    {
        var f = new float[Math.Max(count, 0)];
        for (int i = 0; i < f.Length; i++)
        {
            f[i] = c.ReadSingle();
        }

        return f;
    }

    internal static string CString(ReadOnlySpan<byte> b)
    {
        int end = b.IndexOf((byte)0);
        return Encoding.Latin1.GetString(end < 0 ? b : b[..end]);
    }
}
