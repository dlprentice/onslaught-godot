// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/WorldPhysicsManager.h (CRoundData: the fields, the physics script's constructor, the
// setters and CopyFrom). Not in Stuart's release.

using System.Text;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>
/// CRoundData: a round definition (the round statement). The effect names are resolved against the particle set by
/// Resolve, which is not ported here; this class keeps the names.
/// </summary>
public sealed class RoundData
{
    public RoundData(byte[] name)
    {
        Name = name;
        StreamLaser = name.AsSpan().SequenceEqual("Stream Laser"u8) ? 1 : 0;
        GillMBreath = name.AsSpan().SequenceEqual("Gill-M Breath"u8) ? 1 : 0;
    }

    public byte[] Name { get; }

    public string NameText => Encoding.ASCII.GetString(Name);

    public byte[]? ExplosionName;           // mUnknown08
    public byte[]? MeshName;                // mUnknown0c
    public byte[]? EffectName;              // mParticleAName
    public byte[]? WaterEffectName;         // mParticleBName

    public float Damage;                    // mUnknown1C
    public float Rearm;                     // Rearm / 100
    public float LifeSpan;
    public float TurnRate;                  // mUnknown28
    public float Speed;
    public float BounceFactor;
    public float SeekDelay;
    public float Wiggle;
    public float Gravity;                   // Gravity / 100
    public float SeekAngle = 0.785f;
    public float SeekTerminationTime = 1000.0f;
    public int Seek;                        // mUnknown48: 0 nothing, 1 target, 2 target only
    public int Flak;
    public int Beam;
    public int WeirdoSeek;
    public int GridOfFear;                  // mUnknown58
    public int CanGoUnderWater;
    public int Fire;                        // mUnknown60
    public int PassiveCollision;
    public int GroundHugging;
    public int Torpedo;                     // mUnknown6C
    public int Missile;                     // mUnknown70
    public int Explode;
    public int Smart;

    /// <summary>
    /// mFlakInaccuracy (FlakInaccuracy / 100). The constructor does not set it; 0 here until its value in a round
    /// without the keyword is measured (open question: what the MT_ROUND_DATA allocation leaves there).
    /// </summary>
    public float FlakInaccuracy;

    public int SoundMaterial;
    public int Jumps;
    public float Proximity;
    public float Radius;
    public float JumpRange = 10.0f;         // mUnknown90
    public float JumpDelay;
    public float Length;
    public int StreamLaser;
    public int GillMBreath;
    public int TreeCollision;               // 0 on, 1 onf, 2 off

    public void SetMesh(byte[]? name) => MeshName = name ?? MeshName;

    public void SetEffect(byte[]? name) => EffectName = name ?? EffectName;

    public void SetWaterEffect(byte[]? name) => WaterEffectName = name ?? WaterEffectName;

    public void SetExplosion(byte[]? name) => ExplosionName = name ?? ExplosionName;

    /// <summary>CopyFrom: a BasedOn copy, in retail's order. It leaves out FlakInaccuracy and the two name flags; a null
    /// source copies nothing.</summary>
    public void CopyFrom(RoundData? other)
    {
        if (other is null)
        {
            return;
        }

        Damage = other.Damage;
        Rearm = other.Rearm;
        LifeSpan = other.LifeSpan;
        TurnRate = other.TurnRate;
        Speed = other.Speed;
        BounceFactor = other.BounceFactor;
        SeekDelay = other.SeekDelay;
        SeekAngle = other.SeekAngle;
        Wiggle = other.Wiggle;
        Radius = other.Radius;
        SeekTerminationTime = other.SeekTerminationTime;
        CanGoUnderWater = other.CanGoUnderWater;
        GroundHugging = other.GroundHugging;
        Torpedo = other.Torpedo;
        Missile = other.Missile;
        PassiveCollision = other.PassiveCollision;
        Length = other.Length;
        Gravity = other.Gravity;
        GridOfFear = other.GridOfFear;
        Seek = other.Seek;
        Smart = other.Smart;
        SetEffect(other.EffectName);
        SetWaterEffect(other.WaterEffectName);
        SetExplosion(other.ExplosionName);
        SetMesh(other.MeshName);
        Flak = other.Flak;
        Beam = other.Beam;
        WeirdoSeek = other.WeirdoSeek;
        SoundMaterial = other.SoundMaterial;
        Proximity = other.Proximity;
        Jumps = other.Jumps;
        JumpRange = other.JumpRange;
        JumpDelay = other.JumpDelay;
        Explode = other.Explode;
        Fire = other.Fire;
        TreeCollision = other.TreeCollision;
    }

    public override string ToString() => NameText;
}
