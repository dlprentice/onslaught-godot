// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/WorldPhysicsManager.h (CWeaponData, CLaunchAngle, CWeaponModeData: the fields, the physics
// script's constructors and setters) and src/WorldPhysicsManager.cpp (CWeaponModeData::SetRound 005113f0). Not in
// Stuart's release.

using System.Text;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>CWeaponData: a weapon definition (the weapon statement). Strings are the bytes the script stored.</summary>
public sealed class WeaponData
{
    /// <summary>The language-name text id a weapon has until its LanguageName value says otherwise.</summary>
    public const int DefaultLanguageName = 0xfe0572;

    /// <summary>mChargeModes: the weapon mode each charge level fires, by registry position, -1 for none.</summary>
    public readonly int[] ChargeModes = [-1, -1, -1, -1, -1];

    /// <summary>mRating: the configuration page's ratings versus infantry, tanks and air.</summary>
    public readonly int[] Rating = new int[3];

    public WeaponData(byte[] name)
    {
        Name = name;
    }

    public byte[] Name { get; }

    public string NameText => Encoding.ASCII.GetString(Name);

    public byte[]? IconName;
    public float ChargeRate = 2.0f;
    public float Consumption = 1.0f;
    public int AmmoStore;
    public int AdjustAim = 1;
    public int AllowMovement = 1;
    public int Smart;
    public int ZoomMode;
    public int Placement;
    public int LanguageName = DefaultLanguageName;

    /// <summary>SetIconName: a null name leaves it as it was.</summary>
    public void SetIconName(byte[]? name) => IconName = name ?? IconName;

    public override string ToString() => NameText;
}

/// <summary>CLaunchAngle: one of a weapon mode's launch angle pairs.</summary>
public sealed class LaunchAngle
{
    public float A;
    public float B;
}

/// <summary>
/// CWeaponModeData: a weapon mode (the weaponmode statement). The particle and sound names are resolved against the
/// particle set and the sound bank by Resolve, which is not ported here; this class keeps the names.
/// </summary>
public sealed class WeaponModeData
{
    /// <summary>mSet4c: the launch sequence.</summary>
    public readonly List<int> LaunchSequence = [];

    /// <summary>mSet5c: the launch angles.</summary>
    public readonly List<LaunchAngle> LaunchAngles = [];

    public WeaponModeData(byte[] name)
    {
        Name = name;
    }

    public byte[] Name { get; }

    public string NameText => Encoding.ASCII.GetString(Name);

    /// <summary>mRound: the round at the position the Round value named when it applied, or null.</summary>
    public RoundData? Round;

    public byte[]? ClipName;                // mClipName
    public byte[]? MuzzleEffectName;        // mParticleAName
    public byte[]? PreFireEffectName;       // mParticleBName
    public byte[]? LaunchSoundName;         // mSoundAName
    public byte[]? PreFireSoundName;        // mSoundBName
    public byte[]? PostFireSoundName;       // mSoundCName

    public float Inaccuracy;
    public float ReloadTime;
    public float BurstDelay;
    public float Power;
    public int BurstSize = 1;
    public int VolleySize = 1;
    public float MinTargetHeight = -10.0f;  // mUnknown6C
    public float MaxTargetHeight = 10000.0f; // mUnknown70
    public float MinRange;
    public float MaxRange = 100.0f;
    public float MinDeflection;
    public float MaxDeflection;
    public float YawTolerance = 0.5f;
    public float PreFireDelay;              // mUnknown88
    public float PostFireDelay;             // mUnknown8C
    public int MaxLocks = 5;
    public float LockTime;
    public float LockDeflection;
    public float LockRange = 40.0f;
    public float LockRadius = 10.0f;
    public int LockUnit;
    public int LockMode;
    public int Track;
    public int Predictive;
    public int SoundPerBurst = 1;
    public int MuzzleLight = -1;
    public float MuzzleLightRadius;

    public void SetMuzzleEffect(byte[]? name) => MuzzleEffectName = name ?? MuzzleEffectName;

    public void SetPreFireEffect(byte[]? name) => PreFireEffectName = name ?? PreFireEffectName;

    public void SetClip(byte[]? name) => ClipName = name ?? ClipName;

    public void SetLaunchSound(byte[]? name) => LaunchSoundName = name ?? LaunchSoundName;

    public void SetPreFireSound(byte[]? name) => PreFireSoundName = name ?? PreFireSoundName;

    public void SetPostFireSound(byte[]? name) => PostFireSoundName = name ?? PostFireSoundName;

    /// <summary>
    /// SetLaunchSequence: sets the entry at a 1-based position, first growing the sequence with zeros to hold it. A
    /// position below 1 grows nothing and sets nothing.
    /// </summary>
    public void SetLaunchSequence(int index, int value)
    {
        while (index > LaunchSequence.Count)
        {
            LaunchSequence.Add(0);
        }

        if (index >= 1)
        {
            LaunchSequence[index - 1] = value;
        }
    }

    /// <summary>SetLaunchAngle: the same for a launch angle pair (new pairs are 0, 0).</summary>
    public void SetLaunchAngle(int index, float a, float b)
    {
        while (index > LaunchAngles.Count)
        {
            LaunchAngles.Add(new LaunchAngle());
        }

        if (index >= 1)
        {
            LaunchAngles[index - 1].A = a;
            LaunchAngles[index - 1].B = b;
        }
    }

    public override string ToString() => NameText;
}
