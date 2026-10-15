// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/InitThing.h (CInitThing::Load 0040e280 and the derived Loads, CMEMBUFFER versions),
// src/InitThing.cpp (SpawnInitThing 0048c650) and src/oids.h.

using System.Text;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.World;

/// <summary>The class ids the world file stores before each thing (oids.h).</summary>
public static class Oid
{
    public const int Thing = 0;
    public const int Actor = 1;
    public const int BattleEngine = 3;
    public const int Round = 4;
    public const int Tree = 7;
    public const int Unit = 8;
    public const int SpawnerThing = 19;
    public const int Explosion = 22;
    public const int Building = 25;
    public const int Cutscene = 26;
    public const int LevelScriptThing = 27;
    public const int Squad = 28;
    public const int Wall = 33;
    public const int Feature = 35;
    public const int SphereTrigger = 36;
    public const int SafeSide = 37;
    public const int Hazard = 39;
    public const int ComplexThing = 40;
    public const int SpawnPoint = 41;
    public const int Start = 15;
}

/// <summary>
/// A world file's placement record for one thing: CInitThing's stored fields and whatever the class SpawnInitThing
/// chooses for the id adds. Only world-file versions 43 to 50 are read (CWorld::LoadWorld refuses the rest).
/// </summary>
public sealed class InitThingRecord
{
    public FVector Position;
    public float Yaw;
    public float Pitch;
    public float Roll;
    public int MeshNo;
    public int Allegiance;
    public int Target;
    public byte[] Script = [];
    public byte[] Name = [];
    public byte[] SpawnScript = [];
    public int Active = 1;
    public int AttachScriptsToUnits;

    /// <summary>The class SpawnInitThing makes for the id, which decides the extra fields.</summary>
    public InitKind Kind;

    // CSpawnerInitThing
    public int Amount = -1;
    public float Delay;
    public float SquadDelay;
    public float InitialDelay;
    public int SquadSize = 1;
    public byte[] SpawnUnit = [];
    public byte[] SpawnerSpawnScript = [];

    // CSquadInitThing (Amount is shared with the spawner's field name)
    public int Mode;

    // CTreeInitThing
    public byte[] TreeType = Encoding.ASCII.GetBytes("DefaultTree0");

    // CWallInitThing
    public float Length;
    public float Life = 50.0f;
    public byte[] WallType = Encoding.ASCII.GetBytes("wall1");

    // CCutsceneInitThing
    public byte[] CutsceneFile = [];
    public byte[] LinkTo = [];

    // CStartInitThing
    public int PlaneMode;
    public int PlayerNumber = 1;

    // CSphereTriggerInitThing
    public float Radius;

    public enum InitKind
    {
        Plain,
        Unit,
        Squad,
        Spawner,
        Cutscene,
        Start,
        Tree,
        Wall,
        Feature,
        Hazard,
        SphereTrigger,
    }

    /// <summary>SpawnInitThing: the init class for a class id.</summary>
    public static InitKind KindFor(int oid) => oid switch
    {
        Oid.Unit or Oid.Building => InitKind.Unit,
        Oid.Squad => InitKind.Squad,
        Oid.SpawnerThing => InitKind.Spawner,
        Oid.Cutscene => InitKind.Cutscene,
        Oid.Start or Oid.SpawnPoint => InitKind.Start,
        Oid.Tree => InitKind.Tree,
        Oid.Wall => InitKind.Wall,
        Oid.Feature => InitKind.Feature,
        Oid.Hazard => InitKind.Hazard,
        Oid.SphereTrigger => InitKind.SphereTrigger,
        _ => InitKind.Plain,
    };

    /// <summary>The init's virtual Load for <paramref name="kind"/>.</summary>
    public static InitThingRecord Load(InitKind kind, int version, MemBuffer file)
    {
        if (version < 43 || version > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "world files 43 to 50 only");
        }

        var r = new InitThingRecord { Kind = kind };
        r.LoadBase(version, file);
        switch (kind)
        {
            case InitKind.Tree:
                r.TreeType = file.ReadZeroTerminated();
                break;
            case InitKind.Spawner:
                r.Amount = file.ReadInt32();
                r.Delay = file.ReadSingle();
                r.SquadDelay = file.ReadSingle();
                r.InitialDelay = file.ReadSingle();
                r.SquadSize = file.ReadInt32();
                r.SpawnUnit = file.ReadZeroTerminated();
                if (version > 43)
                {
                    r.SpawnerSpawnScript = file.ReadZeroTerminated();
                }

                break;
            case InitKind.Squad:
                r.Amount = file.ReadInt32();
                r.Mode = file.ReadInt32();
                break;
            case InitKind.Wall:
                r.Length = file.ReadSingle();
                r.Life = file.ReadSingle();
                r.WallType = file.ReadZeroTerminated();
                break;
            case InitKind.Cutscene:
                r.CutsceneFile = file.ReadZeroTerminated();
                r.LinkTo = file.ReadZeroTerminated();
                break;
            case InitKind.Start:
                r.PlaneMode = file.ReadInt32();
                r.PlayerNumber = file.ReadInt32();
                break;
            case InitKind.SphereTrigger:
                r.Radius = file.ReadSingle();
                break;
        }

        return r;
    }

    private void LoadBase(int version, MemBuffer file)
    {
        Position = new FVector(file.ReadSingle(), file.ReadSingle(), file.ReadSingle());
        Yaw = file.ReadSingle();
        Pitch = file.ReadSingle();
        Roll = file.ReadSingle();
        MeshNo = file.ReadInt32();
        Allegiance = file.ReadInt32();
        Target = file.ReadInt32();
        Script = file.ReadZeroTerminated();
        Name = file.ReadZeroTerminated();
        SpawnScript = file.ReadZeroTerminated();
        Active = file.ReadInt32();
        if (version > 45)
        {
            AttachScriptsToUnits = file.ReadInt32();
        }
    }

    public override string ToString() => $"{Kind} '{Encoding.ASCII.GetString(Name)}' at {Position}";
}

/// <summary>UBattleEngineConfigurations (BattleEngineConfigurations.cpp Load 0040f180 / Skip 0040f260).</summary>
public static class BattleEngineConfigurations
{
    /// <summary>The configuration names: a 32-bit count, then counted strings.</summary>
    public static IReadOnlyList<byte[]> Read(MemBuffer file)
    {
        int count = file.ReadInt32();
        var names = new List<byte[]>(Math.Max(count, 0));
        for (int n = 0; n < count; n++)
        {
            names.Add(file.ReadCountedString());
        }

        return names;
    }
}
