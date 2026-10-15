// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/MeshPart.cpp (CMeshPart::Deserialize 004b27a0, ReadBoundingBox 004b3180, GetTransforms
// 004b0d00, GetWorldTransforms 004b0fb0, ResolveFrameNumber 004b24d0, GetMeshDataPart 004b0cd0) and src/mesh.h (the
// CMeshPart, CBoundingBox, CMeshDVertex layouts). MeshPart.cpp is in Stuart's release.

using System.Buffers.Binary;
using System.Text;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>CBoundingBox: origin, half-size axes, a valid flag and the radius.</summary>
public sealed record BoundingBox(FVector Origin, FVector Axes, int Valid, float Radius);

/// <summary>
/// CMeshPart as the level resources store it: the 0x13c-byte record (kept whole; its pointer fields are presence
/// markers in the file) and the arrays and links Deserialize reads after it. Links to other parts are resolved against
/// the owning mesh's parts.
/// </summary>
public sealed class MeshPart
{
    /// <summary>sizeof(CMeshPart).</summary>
    public const int RecordSize = 0x13c;

    internal MeshPart(MeshData mesh, int index)
    {
        Mesh = mesh;
        Index = index;
    }

    /// <summary>The mesh this part was read into (mBaseMesh).</summary>
    public MeshData Mesh { get; }

    /// <summary>The part's position in the mesh's part array.</summary>
    public int Index { get; }

    /// <summary>The record as stored (its size is the chunk's).</summary>
    public byte[] Record { get; private set; } = [];

    public FMatrix Orientation => Matrix(0x00);
    public FMatrix BaseOrientation => Matrix(0x30);
    public FVector OffsetPos => Vector(0x60);
    public FVector BasePosition => Vector(0x70);
    public int PartNumber => Int(0x88);
    public int PartType => Int(0x8c);
    public int NumChildren => Int(0x90);
    public int NumChainedParts => Int(0xa0);
    public int IsChained => Int(0xa4);
    public int NumDVertices => Int(0xa8);
    public int NumPVertices => Int(0xac);
    public int NumTriangles => Int(0xb0);
    public int NumAFrames => Int(0xb4);
    public int NumVFrames => Int(0xb8);
    public int NumHFrames => Int(0xbc);
    public int NumBones => Int(0xc0);
    public int NumCachedFrames => Int(0x118);
    public int PositionStatic => Int(0x11c);
    public int OrientationStatic => Int(0x120);
    public float Radius => Single(0x12c);
    public float CueFactor => Single(0x130);

    /// <summary>mName: the 32-byte name up to its terminator.</summary>
    public string Name
    {
        get
        {
            ReadOnlySpan<byte> n = Record.AsSpan(0xdc, 32);
            int end = n.IndexOf((byte)0);
            return Encoding.ASCII.GetString(end < 0 ? n : n[..end]);
        }
    }

    public MeshPart?[] Children { get; private set; } = [];
    public MeshPart? Parent { get; private set; }
    public MeshPart? NextMeshPartInChain { get; private set; }
    public MeshPart? ReferenceTo { get; private set; }
    public BoundingBox? BoundingBox { get; private set; }

    /// <summary>mDVertices: the 0x60-byte vertex records, raw.</summary>
    public byte[] DVertices { get; private set; } = [];

    /// <summary>The stored size of one DVertex record.</summary>
    public int DVertexSize { get; private set; }

    /// <summary>mTriangles: three DVertex indices each.</summary>
    public int[] Triangles { get; private set; } = [];

    /// <summary>mPVertices: mNumAFrames sets of mNumPVertices positions.</summary>
    public FVector[][] PVertices { get; private set; } = [];

    public byte[] VHFrameMap { get; private set; } = [];
    public FVector[]? HFramePositions { get; private set; }
    public FMatrix[]? HFrameOrientations { get; private set; }
    public float[]? HFrameFovs { get; private set; }
    public MeshPart?[] Bones { get; private set; } = [];
    public float[][]? BoneWeights { get; private set; }
    public int[][]? BoneSlots { get; private set; }
    public MeshPolyBucket? PolyBucket { get; private set; }
    public FVector[]? CachedPosition { get; private set; }
    public FMatrix[]? CachedOrientation { get; private set; }
    public MeshVertexBuffer VertexBuffer { get; private set; } = default!;

    /// <summary>GetMeshDataPart: the part itself for types 1 and 3, its reference for type 6, else none.</summary>
    public MeshPart? GetMeshDataPart() => PartType is 1 or 3 ? this : PartType == 6 ? ReferenceTo : null;

    public FVector GetKeyPosition(int k) => HFramePositions![k];

    public FMatrix GetHFrameOrientation(int k) => HFrameOrientations![k];

    /// <summary>CMeshPart::Deserialize: the record, then the arrays its counts and markers call for, in file order.</summary>
    internal void Deserialize(ChunkReader c)
    {
        c.GetNext();
        c.GetNext();
        Record = new byte[c.Size];
        c.Read(Record);

        if (NumHFrames > 0 && Ptr(0xc8) != 0)
        {
            HFramePositions = new FVector[NumHFrames];
        }

        if (NumHFrames > 0 && Ptr(0xcc) != 0)
        {
            HFrameFovs = new float[NumHFrames];
        }

        bool boneWeights = NumBones > 0 && NumPVertices > 0 && Ptr(0xd4) != 0;
        bool boneSlots = NumBones > 0 && NumPVertices > 0 && Ptr(0xd8) != 0;

        if (NumDVertices > 0)
        {
            c.GetNext();
            DVertexSize = c.ReadInt32();
            DVertices = new byte[DVertexSize * NumDVertices];
            for (int i = 0; i < NumDVertices; i++)
            {
                c.Read(DVertices.AsSpan(i * DVertexSize, DVertexSize));
            }
        }

        if (NumTriangles > 0)
        {
            c.GetNext();
            Triangles = new int[NumTriangles * 3];
            for (int i = 0; i < Triangles.Length; i++)
            {
                Triangles[i] = c.ReadInt32();
            }
        }

        PVertices = new FVector[Math.Max(NumAFrames, 0)][];
        if (NumPVertices > 0)
        {
            c.GetNext();
            for (int i = 0; i < NumAFrames; i++)
            {
                PVertices[i] = ReadVectors(c, NumPVertices);
            }
        }

        if (NumChildren > 0)
        {
            c.GetNext();
            Children = new MeshPart?[NumChildren];
            for (int i = 0; i < NumChildren; i++)
            {
                Children[i] = Mesh.GetPart(c.ReadInt32());
            }
        }

        if (Ptr(0x98) != 0)
        {
            c.GetNext();
            Parent = Mesh.GetPart(c.ReadInt32());
        }

        if (Ptr(0x9c) != 0)
        {
            c.GetNext();
            NextMeshPartInChain = Mesh.GetPart(c.ReadInt32());
        }

        c.GetNext();
        BoundingBox = ReadBoundingBox(c);

        if (NumVFrames > 0)
        {
            c.GetNext();
            VHFrameMap = new byte[NumVFrames];
            c.Read(VHFrameMap);
        }

        if (NumHFrames > 0)
        {
            if (Ptr(0x10c) != 0 && c.GetNext() == ChunkReader.MakeId("HORI"))
            {
                HFrameOrientations = ReadMatrices(c, NumHFrames);
            }

            if (HFramePositions is not null)
            {
                c.GetNext();
                HFramePositions = ReadVectors(c, NumHFrames);
            }

            if (HFrameFovs is not null)
            {
                c.GetNext();
                for (int i = 0; i < NumHFrames; i++)
                {
                    HFrameFovs[i] = c.ReadSingle();
                }
            }
        }

        if (NumBones > 0)
        {
            c.GetNext();
            Bones = new MeshPart?[NumBones];
            for (int i = 0; i < NumBones; i++)
            {
                Bones[i] = Mesh.GetPart(c.ReadInt32());
            }

            if (Ptr(0xd4) != 0)
            {
                c.GetNext();
                BoneWeights = new float[boneWeights ? NumPVertices : 0][];
                for (int i = 0; i < BoneWeights.Length; i++)
                {
                    BoneWeights[i] = new float[NumBones];
                    for (int b = 0; b < NumBones; b++)
                    {
                        BoneWeights[i][b] = c.ReadSingle();
                    }
                }
            }

            if (Ptr(0xd8) != 0)
            {
                c.GetNext();
                BoneSlots = new int[boneSlots ? NumPVertices : 0][];
                for (int i = 0; i < BoneSlots.Length; i++)
                {
                    BoneSlots[i] = [c.ReadInt32(), c.ReadInt32(), c.ReadInt32()];
                }
            }
        }

        if (Ptr(0x100) != 0)
        {
            PolyBucket = MeshPolyBucket.Load(c);
        }

        if (Ptr(0x104) != 0)
        {
            c.GetNext();
            CachedPosition = ReadVectors(c, NumCachedFrames);
        }

        if (Ptr(0x108) != 0 && c.GetNext() == ChunkReader.MakeId("CORI"))
        {
            CachedOrientation = ReadMatrices(c, NumCachedFrames);
        }

        if (Ptr(0x124) != 0)
        {
            c.GetNext();
            ReferenceTo = Mesh.GetPart(c.ReadInt32());
        }

        VertexBuffer = MeshVertexBuffer.Load(c);
    }

    /// <summary>KeepBuckets FALSE: CMesh::Deserialize deletes the part's bucket after reading it.</summary>
    internal void DropPolyBucket() => PolyBucket = null;

    /// <summary>ReadBoundingBox: one more chunk header, then origin and axes (16 bytes each), valid and radius.</summary>
    internal static BoundingBox ReadBoundingBox(ChunkReader c)
    {
        c.GetNext();
        FVector origin = ReadVector(c);
        FVector axes = ReadVector(c);
        int valid = c.ReadInt32();
        float radius = c.ReadSingle();
        return new BoundingBox(origin, axes, valid, radius);
    }

    /// <summary>GetTransforms: the keys of two frames interpolated by <paramref name="t"/>.</summary>
    public void GetTransforms(int f0, int f1, float t, out FMatrix ori, out FVector pos)
    {
        if (f0 >= NumVFrames)
        {
            f0 = NumVFrames - 1;
        }

        if (f1 >= NumVFrames)
        {
            f1 = NumVFrames - 1;
        }

        byte k0 = VHFrameMap[f0];
        byte k1 = VHFrameMap[f1];
        pos = GetKeyPosition(k0) + ((GetKeyPosition(k1) - GetKeyPosition(k0)) * t);
        ori = MatrixOps.Add(GetHFrameOrientation(k0), MatrixOps.Scale(MatrixOps.Sub(GetHFrameOrientation(k1), GetHFrameOrientation(k0)), t));
    }

    /// <summary>
    /// ResolveFrameNumber with no motion controller: the animation's frame for <paramref name="time"/>, wrapped to the
    /// part's frames, and the fraction past it.
    /// </summary>
    public int ResolveFrameNumber(float time, int anim, out float fraction)
    {
        int f;
        float part;
        if (Mesh.NumAnimModes != 0 && NumVFrames > 1 && anim >= 0)
        {
            AnimMode a = Mesh.AnimModes[anim];
            float frame = time;
            frame *= a.FrameScaler;
            frame += a.Start;
            f = X87.Fist(Math.Floor(frame)) % NumVFrames;
            part = frame - MathF.Floor(frame);
        }
        else
        {
            float frame = 0;
            f = X87.Fist(Math.Floor(frame));
            part = frame - MathF.Floor(frame);
        }

        fraction = part;
        return f;
    }

    /// <summary>
    /// GetWorldTransforms with no motion controller (the thing has none, or <paramref name="controller"/> is false): the
    /// part's cached frames when it has them and <paramref name="uncached"/> is false, otherwise its parent's transform
    /// with this part's keys applied. <paramref name="subject"/> places the result in the world when not local.
    /// Retail also keeps the last local and world results in statics and returns them for the same part, thing, frame
    /// and render frame; with no controller those equal a fresh evaluation, so they are not kept here.
    /// </summary>
    public void GetWorldTransforms(int frame, int nextFrame, float fraction, IRenderSubject? subject, out FMatrix ori,
        out FVector pos, bool local, bool uncached)
    {
        if (!uncached && NumCachedFrames > 0)
        {
            MeshPart pp = this;
            MeshPart op = this;
            while (pp.PositionStatic != 0)
            {
                pp = pp.Parent!;
            }

            while (op.OrientationStatic != 0)
            {
                op = op.Parent!;
            }

            int p0 = frame, p1 = nextFrame, o0 = frame, o1 = nextFrame;
            if (frame >= pp.NumCachedFrames)
            {
                p0 = pp.NumCachedFrames - 1;
            }

            if (nextFrame >= pp.NumCachedFrames)
            {
                p1 = pp.NumCachedFrames - 1;
            }

            if (frame >= op.NumCachedFrames)
            {
                o0 = op.NumCachedFrames - 1;
            }

            if (nextFrame >= op.NumCachedFrames)
            {
                o1 = op.NumCachedFrames - 1;
            }

            FVector b = pp.CachedPosition![p1];
            FVector dp = b - pp.CachedPosition[p0];
            pos = pp.CachedPosition[p0] + (dp * fraction);
            FMatrix c0 = op.CachedOrientation![o0];
            ori = MatrixOps.Add(c0, MatrixOps.Scale(MatrixOps.Sub(op.CachedOrientation[o1], c0), fraction));
            if (!local && subject is not null)
            {
                FMatrix m = subject.RenderOrientation;
                ori = m.Multiply(ori);
                pos = m.Transform(pos) + subject.RenderPos;
            }

            return;
        }

        FMatrix parentOri;
        FVector parentPos;
        if (Parent is null)
        {
            parentOri = FMatrix.Identity;
            parentPos = new FVector(0, 0, 0);
        }
        else
        {
            Parent.GetWorldTransforms(frame, nextFrame, fraction, subject, out parentOri, out parentPos, local, uncached);
        }

        GetTransforms(frame, nextFrame, fraction, out ori, out pos);
        if (subject is not null && !local && PartNumber == 0)
        {
            parentPos += subject.RenderPos;
            parentOri = parentOri.Multiply(subject.RenderOrientation);
        }

        parentPos += parentOri.Transform(pos);
        parentOri = parentOri.Multiply(ori);
        pos = parentPos;
        ori = parentOri;
    }

    private int Int(int at) => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(at));

    private uint Ptr(int at) => BinaryPrimitives.ReadUInt32LittleEndian(Record.AsSpan(at));

    private float Single(int at) => BinaryPrimitives.ReadSingleLittleEndian(Record.AsSpan(at));

    private FVector Vector(int at) => MeshBytes.Vector(Record, at);

    private FMatrix Matrix(int at) => MeshBytes.Matrix(Record, at);

    internal static FVector ReadVector(ChunkReader c)
    {
        Span<byte> b = stackalloc byte[16];
        c.Read(b);
        return MeshBytes.Vector(b, 0);
    }

    internal static FVector[] ReadVectors(ChunkReader c, int count)
    {
        var v = new FVector[Math.Max(count, 0)];
        for (int i = 0; i < v.Length; i++)
        {
            v[i] = ReadVector(c);
        }

        return v;
    }

    internal static FMatrix[] ReadMatrices(ChunkReader c, int count)
    {
        var m = new FMatrix[Math.Max(count, 0)];
        Span<byte> b = stackalloc byte[48];
        for (int i = 0; i < m.Length; i++)
        {
            c.Read(b);
            m[i] = MeshBytes.Matrix(b, 0);
        }

        return m;
    }

    public override string ToString() => $"part {Index} '{Name}'";
}
