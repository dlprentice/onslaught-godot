// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/WorldPhysicsManager.h (CSpawnerData: the fields, the physics script's constructor,
// SetThingName and CopyFrom). Not in Stuart's release.

using System.Text;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>CSpawnerData: a spawner definition (the spawner statement).</summary>
public sealed class SpawnerData
{
    public SpawnerData(byte[] name)
    {
        Name = name;
    }

    public byte[] Name { get; }

    public string NameText => Encoding.ASCII.GetString(Name);

    /// <summary>mThingName: the unit definition it spawns (Unit).</summary>
    public byte[]? ThingName;

    public int Amount;
    public int SquadSize = 1;
    public int Conditions = 7;
    public float Delay;
    public float SeekDelay;
    public float SquadDelay;
    public int Infinite;
    public int Recall;
    public float MinRange;
    public float MaxRange = 100.0f;
    public float PreSpawnDelay;
    public float PostSpawnDelay;

    public void SetThingName(byte[]? name) => ThingName = name ?? ThingName;

    /// <summary>
    /// CopyFrom as retail has it: it copies only when this spawner's own Delay is already nonzero, and then reads the
    /// source unchecked (a missing source faults in retail; here it throws).
    /// </summary>
    public void CopyFrom(SpawnerData? other)
    {
        if (Delay == 0.0f)
        {
            return;
        }

        if (other is null)
        {
            throw new InvalidOperationException(
                $"spawner '{NameText}' is based on a spawner that does not exist; retail reads a NULL definition here");
        }

        Delay = other.Delay;
        SeekDelay = other.SeekDelay;
        Amount = other.Amount;
        SquadSize = other.SquadSize;
        SquadDelay = other.SquadDelay;
        Recall = other.Recall;
        Infinite = other.Infinite;
        Conditions = other.Conditions;
        MinRange = other.MinRange;
        MaxRange = other.MaxRange;
        PreSpawnDelay = other.PreSpawnDelay;
        PostSpawnDelay = other.PostSpawnDelay;
        SetThingName(other.ThingName);
    }

    public override string ToString() => NameText;
}
