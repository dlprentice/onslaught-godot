// SPDX-License-Identifier: GPL-3.0-only
// The particle descriptors: the particle system editor's building blocks, each read from the set file. Adapted from
// onslaught-decomp src/ParticleDescriptor.h, ParticleDescriptor.cpp (each class's Load, 004c05c0 onwards) and ParticleSet.cpp
// (the constructors). The behaviours are in ParticleBehaviours.cs.

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>EParticleDescriptorType.</summary>
public enum PdType
{
    None = 0,
    SimpleSprite,
    Emitter,
    Modifier,
    Selector,
    ColourRange,
    Timeline,
    Shape,
    Trail,
    Mover,
    Function,
    Mesh,
    FoR,
    PMesh,
}

/// <summary>CParticleTexture: a texture by its archive path and blend mode + 1, one of each (CDXParticleTexture::Get).</summary>
public sealed class ParticleTexture(string name, int mode)
{
    /// <summary>The path the archive names (from the editor's machine).</summary>
    public string Name { get; } = name;

    /// <summary>The descriptor's blend mode + 1.</summary>
    public int Mode { get; } = mode;

    /// <summary>The path the game finds it by: the name past its first 32 characters (path + 32).</summary>
    public string GamePath => Name.Length > 32 ? Name[32..] : "";
}

/// <summary>CParticleDescriptor: a named descriptor, on the set's name-ordered list and, while it has particles, the live list.</summary>
public abstract partial class ParticleDescriptor
{
    public string Name = "";

    /// <summary>mNext: the set's list, sorted by name.</summary>
    public ParticleDescriptor? Next;

    /// <summary>The live list's links (SParticleSetNode +0x3c, +0x40).</summary>
    public ParticleDescriptor? NodePrev;
    public ParticleDescriptor? NodeNext;

    /// <summary>mNextToUpdate.</summary>
    public ParticleDescriptor? NextToUpdate;

    public Particle? NewestParticle;
    public Particle? OldestParticle;
    public Particle? NewestAtUpdate;

    public abstract PdType Type { get; }

    /// <summary>Load: this descriptor's lines up to the separator.</summary>
    public abstract void Load(TokenArchive archive, ParticleSet set);

    /// <summary>A Load's loop: ReadToken into the Load's own locals (kept across lines) until the separator.</summary>
    protected static void ReadUntilSeparator(TokenArchive archive, Action<Pst, int, float, string> each)
    {
        Pst token;
        int ival = 0;
        float fval = 0;
        string sval = "";
        do
        {
            archive.ReadToken(out token, ref ival, ref fval, ref sval);
            each(token, ival, fval, sval);
        }
        while (token != Pst.Separator);
    }
}

public sealed partial class PdSimpleSprite : ParticleDescriptor
{
    public FloatRef Radius = new(0.5f);
    public float FinalRadius;
    public int Gravity;
    public ParticleTexture? Texture;
    public int Life = 100;
    public float LifePct = 1.0f;
    public int Bounce;
    public int FadeCol = 1;
    public ParticleDescriptor? Modifier;
    public ParticleDescriptor? ColourRange;
    public int BlendMode;
    public int TextureNumber;
    public int AxisAligned;
    public float VelocityDamp = 1.0f;
    public FloatRef Length = new(1.0f);
    public int TwoD;
    public int AnimType;
    public int EndFrame;
    public int TextureSize = 2;
    public float AnimSpeed = 1.0f;
    public int RandomStartFrame;
    public float U0, V0, U1 = 1.0f, V1 = 1.0f;

    public override PdType Type => PdType.SimpleSprite;

    public int GetNumFrames() => EndFrame - TextureNumber + 1;

    public override void Load(TokenArchive archive, ParticleSet set)
    {
        ReadUntilSeparator(archive, (token, ival, fval, sval) =>
        {
            switch (token)
            {
                case Pst.Radius: archive.SetFloatRef(fval, ival, Radius); break;
                case Pst.FinalRadius: FinalRadius = fval; break;
                case Pst.Life: Life = ival; break;
                case Pst.LifePct: LifePct = fval; break;
                case Pst.Gravity: Gravity = ival; break;
                case Pst.Bounce: Bounce = ival; break;
                case Pst.FadeCol: FadeCol = ival; break;
                case Pst.Texture: Texture = set.GetTexture(sval, BlendMode + 1); break;
                case Pst.Modifier: archive.SetFixup(ival, d => Modifier = d); break;
                case Pst.ColourRange: archive.SetFixup(ival, d => ColourRange = d); break;
                case Pst.BlendMode: BlendMode = ival; break;
                case Pst.TextureNumber: TextureNumber = ival; break;
                case Pst.TextureSize: TextureSize = ival; break;
                case Pst.AxisAligned: AxisAligned = ival; break;
                case Pst.VelocityDamp: VelocityDamp = fval; break;
                case Pst.Length: archive.SetFloatRef(fval, ival, Length); break;
                case Pst.TwoD: TwoD = ival; break;
                case Pst.AnimType: AnimType = ival; break;
                case Pst.EndFrame: EndFrame = ival; break;
                case Pst.AnimSpeed: AnimSpeed = fval; break;
                case Pst.RandomStartFrame: RandomStartFrame = ival; break;
            }
        });
        SetUV(TextureNumber, TextureSize);
    }

    /// <summary>SetUV (004c0940): the tile's UVs in a texture of (16 >> size) tiles a side; the whole texture with none.</summary>
    public void SetUV(int number, int size)
    {
        (int count, int shift) = size switch
        {
            0 => (16, 4),
            1 => (8, 3),
            2 => (4, 2),
            3 => (2, 1),
            4 => (1, 0),
            _ => throw new NotSupportedException($"particle texture size {size} (retail reads an unset shift)"),
        };
        if (Texture is not null && TextureNumber != -1)
        {
            float tiles = count;
            float u = (number & (count - 1)) / tiles;
            float v = (number >> shift) / tiles;
            U0 = u;
            V0 = v;
            float step = 1.0f / tiles;
            U1 = step + u;
            V1 = step + v;
        }
        else
        {
            U0 = V0 = 0;
            U1 = V1 = 1.0f;
        }
    }
}

public sealed partial class PdEmitter : ParticleDescriptor
{
    public FloatRef EmitPerTurn = new(10.0f);
    public int Life = 10;
    public ParticleDescriptor? ParticleDescriptor;
    public FloatRef InitialVelocityX = new(0);
    public FloatRef InitialVelocityY = new(0);
    public FloatRef InitialVelocityZ = new(-0.1f);
    public FloatRef VelocityRandomness = new(0.1f);
    public int TransmitLife;
    public int TransmitFoR;
    public FloatRef TransmitVelocity = new(0);
    public int InterpolatedEmission;
    public ParticleDescriptor? Shape;
    public int PassNumParticles;
    public FloatRef OutwardVelocity = new(0);
    public ParticleDescriptor? Mover;

    public override PdType Type => PdType.Emitter;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, fval, _) =>
        {
            switch (token)
            {
                case Pst.EmitPerTurn: archive.SetFloatRef(fval, ival, EmitPerTurn); break;
                case Pst.Life: Life = ival; break;
                case Pst.ParticleDescriptor: archive.SetFixup(ival, d => ParticleDescriptor = d); break;
                case Pst.InitialVelocityX: archive.SetFloatRef(fval, ival, InitialVelocityX); break;
                case Pst.InitialVelocityY: archive.SetFloatRef(fval, ival, InitialVelocityY); break;
                case Pst.InitialVelocityZ: archive.SetFloatRef(fval, ival, InitialVelocityZ); break;
                case Pst.VelocityRandomness: archive.SetFloatRef(fval, ival, VelocityRandomness); break;
                case Pst.TransmitLife: TransmitLife = ival; break;
                case Pst.TransmitFoR: TransmitFoR = ival; break;
                case Pst.TransmitVelocity: archive.SetFloatRef(fval, ival, TransmitVelocity); break;
                case Pst.InterpolatedEmission: InterpolatedEmission = ival; break;
                case Pst.Shape: archive.SetFixup(ival, d => Shape = d); break;
                case Pst.PassNumParticles: PassNumParticles = ival; break;
                case Pst.OutwardVelocity: archive.SetFloatRef(fval, ival, OutwardVelocity); break;
                case Pst.Mover: archive.SetFixup(ival, d => Mover = d); break;
            }
        });
}

public sealed partial class PdModifier : ParticleDescriptor
{
    public override PdType Type => PdType.Modifier;

    public override void Load(TokenArchive archive, ParticleSet set) => ReadUntilSeparator(archive, (_, _, _, _) => { });
}

public sealed partial class PdSelector : ParticleDescriptor
{
    public ParticleDescriptor?[] Descriptors = new ParticleDescriptor?[4];
    public int[] Probability = new int[4];

    public override PdType Type => PdType.Selector;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, _, _) =>
        {
            switch (token)
            {
                case >= Pst.ParticleDescriptor0 and <= Pst.ParticleDescriptor3:
                {
                    int i = token - Pst.ParticleDescriptor0;
                    archive.SetFixup(ival, d => Descriptors[i] = d);
                    break;
                }

                case >= Pst.Probability0 and <= Pst.Probability3:
                    Probability[token - Pst.Probability0] = ival;
                    break;
            }
        });
}

public sealed partial class PdColourRange : ParticleDescriptor
{
    public FloatRef StartRed = new(1.0f), StartGreen = new(1.0f), StartBlue = new(1.0f);
    public FloatRef EndRed = new(0), EndGreen = new(0), EndBlue = new(0);
    public FloatRef TransitionRed = new(1.0f), TransitionGreen = new(0), TransitionBlue = new(0);
    public int UseEnd = 1;
    public int UseTransition;
    public float TransitionPoint = 0.5f;

    public override PdType Type => PdType.ColourRange;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, fval, _) =>
        {
            switch (token)
            {
                case Pst.StartRed: archive.SetFloatRef(fval, ival, StartRed); break;
                case Pst.StartGreen: archive.SetFloatRef(fval, ival, StartGreen); break;
                case Pst.StartBlue: archive.SetFloatRef(fval, ival, StartBlue); break;
                case Pst.EndRed: archive.SetFloatRef(fval, ival, EndRed); break;
                case Pst.EndGreen: archive.SetFloatRef(fval, ival, EndGreen); break;
                case Pst.EndBlue: archive.SetFloatRef(fval, ival, EndBlue); break;
                case Pst.TransitionRed: archive.SetFloatRef(fval, ival, TransitionRed); break;
                case Pst.TransitionGreen: archive.SetFloatRef(fval, ival, TransitionGreen); break;
                case Pst.TransitionBlue: archive.SetFloatRef(fval, ival, TransitionBlue); break;
                case Pst.UseEnd: UseEnd = ival; break;
                case Pst.UseTransition: UseTransition = ival; break;
                case Pst.TransitionPoint: TransitionPoint = fval; break;
            }
        });
}

/// <summary>CPDTimelineEntry.</summary>
public sealed class TimelineEntry
{
    public ParticleDescriptor? Descriptor;
    public int Time = -1;
    public int TransmitFoR = 1;
}

public sealed partial class PdTimeline : ParticleDescriptor
{
    public int NumEntries;
    public TimelineEntry[] Entries = Enumerable.Range(0, 10).Select(_ => new TimelineEntry()).ToArray();

    public override PdType Type => PdType.Timeline;

    public override void Load(TokenArchive archive, ParticleSet set)
    {
        foreach (TimelineEntry e in Entries)
        {
            e.Descriptor = null;
            e.Time = 0;
            e.TransmitFoR = 1;
        }

        int descriptors = 0, times = 0, transmits = 0;
        ReadUntilSeparator(archive, (token, ival, _, _) =>
        {
            switch (token)
            {
                case Pst.NumEntries: NumEntries = ival; break;
                case Pst.ParticleDescriptor:
                {
                    TimelineEntry e = Entries[descriptors++];
                    archive.SetFixup(ival, d => e.Descriptor = d);
                    break;
                }

                case Pst.Time: Entries[times++].Time = ival; break;
                case Pst.TransmitFoR: Entries[transmits++].TransmitFoR = ival; break;
            }
        });
    }
}

public sealed partial class PdShape : ParticleDescriptor
{
    public int ShapeType;
    public int RingAxis = 2;
    public int Hemisphere;
    public int NumParticles = 10;
    public FloatRef Radius = new(1.0f);
    public int Hollow = 1;
    public float RandomSX = 1.0f, RandomSY = 1.0f, RandomSZ = 1.0f;

    public override PdType Type => PdType.Shape;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, fval, _) =>
        {
            switch (token)
            {
                case Pst.Type: ShapeType = ival; break;
                case Pst.RingAxis: RingAxis = ival; break;
                case Pst.Hemisphere: Hemisphere = ival; break;
                case Pst.NumParticles: NumParticles = ival; break;
                case Pst.Radius: archive.SetFloatRef(fval, ival, Radius); break;
                case Pst.Hollow: Hollow = ival; break;
                case Pst.RandomSX: RandomSX = fval; break;
                case Pst.RandomSY: RandomSY = fval; break;
                case Pst.RandomSZ: RandomSZ = fval; break;
            }
        });
}

public sealed partial class PdTrail : ParticleDescriptor
{
    public ParticleTexture? Texture;
    public int TextureNumber;
    public int BlendMode;
    public int NumPoints = 10;
    public float Width = 1.0f;
    public float StartWidth;
    public ParticleDescriptor? ColourRange;
    public float WiggleFactor;
    public float DisperseRate;
    public int Gravity;
    public int Life = -2;
    public int TaperStart;
    public int WidthWithLife;
    public int TrailType;
    public ParticleDescriptor? ImpactSpawnee;
    public int TransmitFoR = 1;
    public int FadePoint;
    public int UseSegmentLength;
    public float SegmentLength = 10.0f;
    public int ManualWiggleEnabled;
    public FloatRef WiggleLength = new(0);
    public FloatRef YawLength = new(0);
    public int Flat;
    public float U0, V0, U1 = 1.0f, V1 = 1.0f;

    public override PdType Type => PdType.Trail;

    public override void Load(TokenArchive archive, ParticleSet set)
    {
        ReadUntilSeparator(archive, (token, ival, fval, sval) =>
        {
            switch (token)
            {
                case Pst.Texture: Texture = set.GetTexture(sval, BlendMode + 1); break;
                case Pst.BlendMode: BlendMode = ival; break;
                case Pst.TextureNumber: TextureNumber = ival; break;
                case Pst.Width: Width = fval; break;
                case Pst.StartWidth: StartWidth = fval; break;
                case Pst.NumPoints: NumPoints = ival; break;
                case Pst.ColourRange: archive.SetFixup(ival, d => ColourRange = d); break;
                case Pst.WiggleFactor: WiggleFactor = fval; break;
                case Pst.DisperseRate: DisperseRate = fval; break;
                case Pst.Gravity: Gravity = ival; break;
                case Pst.Life: Life = ival; break;
                case Pst.TaperStart: TaperStart = ival; break;
                case Pst.WidthWithLife: WidthWithLife = ival; break;
                case Pst.Type: TrailType = ival; break;
                case Pst.ImpactSpawnee: archive.SetFixup(ival, d => ImpactSpawnee = d); break;
                case Pst.TransmitFoR: TransmitFoR = ival; break;
                case Pst.FadePoint: FadePoint = ival; break;
                case Pst.UseSegmentLength: UseSegmentLength = ival; break;
                case Pst.SegmentLength: SegmentLength = fval; break;
                case Pst.ManualWiggleEnabled: ManualWiggleEnabled = ival; break;
                case Pst.WiggleLength: archive.SetFloatRef(fval, ival, WiggleLength); break;
                case Pst.YawLength: archive.SetFloatRef(fval, ival, YawLength); break;
                case Pst.Flat: Flat = ival; break;
            }
        });
        if (Texture is not null && TextureNumber != -1)
        {
            float u = (TextureNumber % 4) * 0.25f;
            float v = (TextureNumber >> 2) * 0.25f;
            U0 = u;
            V0 = v;
            U1 = u + 0.25f;
            V1 = v + 0.25f;
        }
        else
        {
            U0 = V0 = 0;
            U1 = V1 = 1.0f;
        }
    }
}

public sealed partial class PdMover : ParticleDescriptor
{
    public FloatRef GravityPC = new(0);
    public float Yaw, Pitch, Roll;
    public ParticleDescriptor? YawFunction, PitchFunction, RollFunction;

    public override PdType Type => PdType.Mover;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, fval, _) =>
        {
            switch (token)
            {
                case Pst.GravityPC: archive.SetFloatRef(fval, ival, GravityPC); break;
                case Pst.Yaw: Yaw = fval; break;
                case Pst.Pitch: Pitch = fval; break;
                case Pst.Roll: Roll = fval; break;
                case Pst.YawFunction: archive.SetFixup(ival, d => YawFunction = d); break;
                case Pst.PitchFunction: archive.SetFixup(ival, d => PitchFunction = d); break;
                case Pst.RollFunction: archive.SetFixup(ival, d => RollFunction = d); break;
            }
        });
}

public sealed partial class PdFunction : ParticleDescriptor
{
    public FloatRef ParamA = new(1.0f), ParamB = new(1.0f), ParamC = new(0), ParamD = new(0);
    public int ParamFunction = 1;
    public int Clip;
    public int ValueType;
    public FloatRef GameturnScale = new(20.0f);
    public int OffsetGameturn;

    public override PdType Type => PdType.Function;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, fval, _) =>
        {
            switch (token)
            {
                case Pst.ParamA: archive.SetFloatRef(fval, ival, ParamA); break;
                case Pst.ParamB: archive.SetFloatRef(fval, ival, ParamB); break;
                case Pst.ParamC: archive.SetFloatRef(fval, ival, ParamC); break;
                case Pst.ParamD: archive.SetFloatRef(fval, ival, ParamD); break;
                case Pst.ParamFunction: ParamFunction = ival; break;
                case Pst.Clip: Clip = ival; break;
                case Pst.ValueType: ValueType = ival; break;
                case Pst.GameturnScale: archive.SetFloatRef(fval, ival, GameturnScale); break;
                case Pst.OffsetGameturn: OffsetGameturn = ival; break;
            }
        });
}

public sealed partial class PdMesh : ParticleDescriptor
{
    /// <summary>The mesh's name (after the archive path's last backslash), looked up when a particle needs it.</summary>
    public string? MeshName;
    public int Gravity;
    public int Life = 100;
    public int Bounce;
    public int TransmitFoR;
    public float AngularMomentum;
    public int AutoCentre = 1;
    public ParticleDescriptor? ImpactSpawnee;

    public override PdType Type => PdType.Mesh;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, fval, sval) =>
        {
            switch (token)
            {
                case Pst.Life: Life = ival; break;
                case Pst.Gravity: Gravity = ival; break;
                case Pst.Bounce: Bounce = ival; break;
                case Pst.AngularMomentum: AngularMomentum = fval; break;
                case Pst.TransmitFoR: TransmitFoR = ival; break;
                case Pst.AutoCentre: AutoCentre = ival; break;
                case Pst.Mesh:
                {
                    int slash = sval.LastIndexOf('\\');
                    MeshName = slash >= 0 ? sval[(slash + 1)..] : sval;
                    break;
                }

                case Pst.ImpactSpawnee: archive.SetFixup(ival, d => ImpactSpawnee = d); break;
            }
        });
}

public sealed partial class PdFoR : ParticleDescriptor
{
    public ParticleDescriptor? Initial, Death, Mover;

    public override PdType Type => PdType.FoR;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, _, _) =>
        {
            switch (token)
            {
                case Pst.Death: archive.SetFixup(ival, d => Death = d); break;
                case Pst.Initial: archive.SetFixup(ival, d => Initial = d); break;
                case Pst.Mover: archive.SetFixup(ival, d => Mover = d); break;
            }
        });
}

public sealed partial class PdPMesh : ParticleDescriptor
{
    public int MeshType;
    public int Life = 100;
    public ParticleTexture? Texture;
    public int BlendMode;
    public ParticleDescriptor? ColourRange, ColourRange2;
    public FloatRef TileU = new(1.0f), TileV = new(1.0f), ScrollU = new(0), ScrollV = new(0);
    public int CylinderNumPtsAxial = 2;
    public int CylinderNumPtsRadial = 10;
    public FloatRef CylinderRadius = new(0.5f), CylinderRadius2 = new(1.0f), CylinderLength = new(5.0f);
    public int SphereNumPtsAx = 10;
    public int SphereNumPtsRad = 10;
    public FloatRef SphereLatitudeStart = new(0), SphereLatitudeEnd = new(180.0f);
    public FloatRef SphereLongitudeStart = new(0), SphereLongitudeEnd = new(360.0f);
    public FloatRef SphereRadiusTime = new(1.0f);

    public override PdType Type => PdType.PMesh;

    public override void Load(TokenArchive archive, ParticleSet set) =>
        ReadUntilSeparator(archive, (token, ival, fval, sval) =>
        {
            switch (token)
            {
                case Pst.Type: MeshType = ival; break;
                case Pst.Life: Life = ival; break;
                case Pst.BlendMode: BlendMode = ival; break;
                case Pst.Texture: Texture = set.GetTexture(sval, BlendMode + 1); break;
                case Pst.ColourRange: archive.SetFixup(ival, d => ColourRange = d); break;
                case Pst.ColourRange2: archive.SetFixup(ival, d => ColourRange2 = d); break;
                case Pst.TileU: archive.SetFloatRef(fval, ival, TileU); break;
                case Pst.TileV: archive.SetFloatRef(fval, ival, TileV); break;
                case Pst.ScrollU: archive.SetFloatRef(fval, ival, ScrollU); break;
                case Pst.ScrollV: archive.SetFloatRef(fval, ival, ScrollV); break;
                case Pst.CylinderNumPtsAxial: CylinderNumPtsAxial = ival; break;
                case Pst.CylinderNumPtsRadial: CylinderNumPtsRadial = ival; break;
                case Pst.CylinderRadius: archive.SetFloatRef(fval, ival, CylinderRadius); break;
                case Pst.CylinderRadius2: archive.SetFloatRef(fval, ival, CylinderRadius2); break;
                case Pst.CylinderLength: archive.SetFloatRef(fval, ival, CylinderLength); break;
                case Pst.SphereNumPtsAx: SphereNumPtsAx = ival; break;
                case Pst.SphereNumPtsRad: SphereNumPtsRad = ival; break;
                case Pst.SphereRadiusTime: archive.SetFloatRef(fval, ival, SphereRadiusTime); break;
                case Pst.SphereLatitudeStart: archive.SetFloatRef(fval, ival, SphereLatitudeStart); break;
                case Pst.SphereLatitudeEnd: archive.SetFloatRef(fval, ival, SphereLatitudeEnd); break;
                case Pst.SphereLongitudeStart: archive.SetFloatRef(fval, ival, SphereLongitudeStart); break;
                case Pst.SphereLongitudeEnd: archive.SetFloatRef(fval, ival, SphereLongitudeEnd); break;
            }
        });
}
