// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/WorldPhysicsManager.h (CExplosionData: the fields, the physics script's constructor, the
// setters and CopyFrom). Not in Stuart's release.

using System.Text;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>
/// CExplosionData: an explosion definition (the explosion statement). The effect and sound names are resolved against
/// the particle set and the sound bank by Resolve (00511d20), which is not ported here; this class keeps the names.
/// </summary>
public sealed class ExplosionData
{
    public ExplosionData(byte[] name)
    {
        Name = name;
    }

    public byte[] Name { get; }

    public string NameText => Encoding.ASCII.GetString(Name);

    public byte[]? AirEffectName;           // mParticleAName
    public byte[]? WaterEffectName;         // mParticleBName
    public byte[]? GroundEffectName;        // mParticleCName
    public byte[]? UnitEffectName;          // mParticleDName
    public byte[]? SoundName;               // mSoundAName
    public byte[]? WaterSoundName;          // mSoundBName

    public float Radius;
    public float Damage;
    public int Volumetric = 1;
    public int Time = 10;
    public int Smart;
    public int Oriented;
    public int Light = -1;

    public void SetAirEffect(byte[]? name) => AirEffectName = name ?? AirEffectName;

    public void SetWaterEffect(byte[]? name) => WaterEffectName = name ?? WaterEffectName;

    public void SetGroundEffect(byte[]? name) => GroundEffectName = name ?? GroundEffectName;

    public void SetUnitEffect(byte[]? name) => UnitEffectName = name ?? UnitEffectName;

    public void SetSound(byte[]? name) => SoundName = name ?? SoundName;

    public void SetWaterSound(byte[]? name) => WaterSoundName = name ?? WaterSoundName;

    /// <summary>CopyFrom: a BasedOn copy, in retail's order. It does not copy Time; a null source copies nothing.</summary>
    public void CopyFrom(ExplosionData? other)
    {
        if (other is null)
        {
            return;
        }

        SetAirEffect(other.AirEffectName);
        SetGroundEffect(other.GroundEffectName);
        SetWaterEffect(other.WaterEffectName);
        SetUnitEffect(other.UnitEffectName);
        Radius = other.Radius;
        Damage = other.Damage;
        Volumetric = other.Volumetric;
        SetSound(other.SoundName);
        SetWaterSound(other.WaterSoundName);
        Smart = other.Smart;
        Light = other.Light;
        Oriented = other.Oriented;
    }

    public override string ToString() => NameText;
}
