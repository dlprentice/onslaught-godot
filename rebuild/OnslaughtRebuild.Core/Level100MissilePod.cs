// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One of the Missile Pod's two charge-selected weapon modes, as the shipped
/// <c>WeaponMode</c> records carry it.
/// </summary>
internal sealed record Level100MissilePodMode(
    int Level,
    float ReloadTime,
    int BurstSize,
    float BurstDelay,
    float LockTime,
    float LockDeflection,
    uint LockUnitMask,
    float LockRange,
    int MaxLocks,
    float Power);

/// <summary>
/// The lock parameters a weapon mode supplies to <c>HandleLocks</c>. Modes
/// without lock nodes keep the record defaults (ctor <c>0x0042fa80</c>): five
/// locks, lock time and deflection 0, range 40 and lock unit 0, so with the
/// Pulse or a Vulcan selected every lock is pruned (cos 0 is 1) and none can be
/// acquired (<c>CanLock</c> ANDs the target type with 0).
/// </summary>
internal readonly record struct Level100LockParameters(
    int MaxLocks,
    float LockTime,
    float LockDeflection,
    float LockRange,
    uint LockUnitMask)
{
    internal static Level100LockParameters RecordDefaults => new(5, 0.0f, 0.0f, 40.0f, 0u);
}

/// <summary>
/// The jet's Missile Pod: the <c>Weapon</c> record, its two modes and the
/// <c>Micro Missile</c> round, all read from <c>data/default physics.dat</c>
/// (SHA-256 <c>e1fb3ded…ada14</c>) with the value-id map in
/// <c>reverse-engineering/binary-analysis/physics-round-value-ids-2026-07-25.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>Weapon "Missile Pod"</c> @<c>0x17746</c>: <c>CWeaponChargeRate</c>
/// <c>0x41000000</c> (8.0), <c>CWeaponAmmoStore</c> 3, <c>CWeaponConsumption</c>
/// 1.0, charge level 0 <c>Mech Micro Missile Launcher</c> and level 1
/// <c>Mech Micro Missile Launcher Salvo</c>. No <c>CWeaponSmart</c> or
/// <c>CWeaponAdjustAim</c> node, so it is not Smart and adjusts its aim
/// (profile default 1, <c>0x0042f6a9</c>).
/// </para>
/// <para>
/// <c>WeaponMode "Mech Micro Missile Launcher"</c> @<c>0x13e49</c>: reload
/// <c>0x3f4ccccd</c>, burst 5 x <c>0x3dcccccd</c>, lock time
/// <c>0x3e4ccccd</c>, lock deflection <c>0xbeb2b8c2</c>, lock mode 0 (direct),
/// lock unit <c>0x000e8400</c>, lock range 100, 5 locks, power
/// <c>0x3c23d70a</c>, inaccuracy 0, and five launch-sequence and five
/// launch-angle entries. <c>... Salvo</c> @<c>0x14093</c>: burst 10 x
/// <c>0x3d4ccccd</c>, lock deflection <c>0xbf32b8c2</c>, 10 locks; otherwise
/// the same.
/// </para>
/// <para>
/// <c>Round "Micro Missile"</c> @<c>0x8e74</c>: velocity 15.0, life 8.0,
/// damage 1.5, turn rate <c>0x3d0efa35</c>, seek 2, seek delay
/// <c>0x3d4ccccd</c>, seek angle <c>0x3f490fdb</c>, wiggle <c>0x3cd67750</c>,
/// explode 1, explosion <c>Micro Missile Hit</c> (based on <c>Small Explosion
/// Base</c>: radius 1.0, damage 0.5), no <c>CRoundRadius</c> (0).
/// </para>
/// </remarks>
internal static class Level100MissilePod
{
    internal const float ChargeRate = 8.0f;

    internal static readonly Level100MissilePodMode Launcher = new(
        Level: 0,
        ReloadTime: BitConverter.UInt32BitsToSingle(0x3f4ccccdu),
        BurstSize: 5,
        BurstDelay: BitConverter.UInt32BitsToSingle(0x3dcccccdu),
        LockTime: BitConverter.UInt32BitsToSingle(0x3e4ccccdu),
        LockDeflection: BitConverter.UInt32BitsToSingle(0xbeb2b8c2u),
        LockUnitMask: 0x000e8400u,
        LockRange: 100.0f,
        MaxLocks: 5,
        Power: BitConverter.UInt32BitsToSingle(0x3c23d70au));

    internal static readonly Level100MissilePodMode Salvo = Launcher with
    {
        Level = 1,
        BurstSize = 10,
        BurstDelay = BitConverter.UInt32BitsToSingle(0x3d4ccccdu),
        LockDeflection = BitConverter.UInt32BitsToSingle(0xbf32b8c2u),
        MaxLocks = 10,
    };

    /// <summary>
    /// The launch-angle entries, (yaw, pitch) by slot, from the pairs
    /// (1: 0, <c>0xbe32b8c2</c>), (2: <c>0xbe0efa35</c>, <c>0xbdb2b8c2</c>),
    /// (3: <c>0x3e0efa35</c>, <c>0xbdb2b8c2</c>), (4: <c>0xbe567750</c>, 0),
    /// (5: <c>0x3e567750</c>, 0) in both modes. Mode <c>+0x74</c> starts at -1
    /// and advances before each round, wrapping at five
    /// (<c>ProjectileBurst__SpawnFromCurrentPreset</c>, the RE lane's Q15).
    /// </summary>
    internal static ReadOnlySpan<uint> LaunchAngleYawBits =>
        [0x00000000u, 0xbe0efa35u, 0x3e0efa35u, 0xbe567750u, 0x3e567750u];

    internal static ReadOnlySpan<uint> LaunchAnglePitchBits =>
        [0xbe32b8c2u, 0xbdb2b8c2u, 0xbdb2b8c2u, 0x00000000u, 0x00000000u];

    /// <summary>The launch-sequence emitters by slot: Gun 4, 3, 5, 2, 6.</summary>
    internal static ReadOnlySpan<int> LaunchSequenceEmitters => [4, 3, 5, 2, 6];

    internal const int LaunchSlots = 5;

    // Micro Missile. Speed is CRoundVelocity x 0.05 per frame.
    internal const int SpeedMillimetersPerTick = 750;
    internal const int LifetimeTicks = 8 * SimulationConstants.TicksPerSecond;
    internal const float SeekDelay = 0.05f;
    internal const int TurnRateMicroRadians = 34_907;
    internal const int SeekAngleMicroRadians = 785_398;
    internal const int WiggleMicroRadians = 26_180;
    // CRoundDamage 1.5 on the struck part plus Micro Missile Hit's 0.5 at the
    // explosion's centre: the round-plus-explosion sum the other player rounds
    // use for their direct hit.
    internal const uint DamageBits = 0x40000000u;

    internal static Level100MissilePodMode Mode(int level) => level switch
    {
        0 => Launcher,
        1 => Salvo,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>A rest-state pod: rate 8.0, levels 0 and 1 present, charge +0.0f.</summary>
    internal static RetailWeaponChargeTable CreateCharge()
    {
        RetailWeaponChargeTable pod = new()
        {
            ChargeRate = ChargeRate,
            Charge = BitConverter.UInt32BitsToSingle(0u),
            ReadyAtTime = BitConverter.UInt32BitsToSingle(0u),
            ReadyToChargeGateActive = true,
        };
        pod.Levels[0] = 0;
        pod.Levels[1] = 1;
        return pod;
    }
}
