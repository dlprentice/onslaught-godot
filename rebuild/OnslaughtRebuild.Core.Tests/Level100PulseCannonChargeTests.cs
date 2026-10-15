// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The Level 100 Pulse Cannon Pod's authored charge table, joined to
/// <see cref="RetailWeaponCharge.Charge"/> so a player holding
/// <c>BUTTON_MECH_CHARGE_GUN_POD</c> advances that table. After Fire,
/// <see cref="RetailWeaponCharge.ReadyToCharge"/> keeps Charge blocked
/// until <c>CWeaponReloadTime</c> 0.1 s has strictly elapsed. Fire at
/// FullyCharged selects Charged 2 / <c>Mech Pulse Bolt Large</c>; tap-fire
/// at charge 0 stays Medium. Store spend remains the next ChargeWeapon arm.
/// </summary>
/// <remarks>
/// Every scalar is a dword out of
/// <c>local-lab/safe-copy-bea-pristine/data/default physics.dat</c>
/// (175,603 bytes, SHA-256
/// <c>e1fb3dedbeb29b4b4151da2c8cbbdc940b716b1a2321e1d6a9ba1542c74ada14</c>),
/// statement <c>Pulse Cannon Pod</c> at file offset <c>0x17463</c>. The
/// increment body is <c>CWeapon__AdvanceChargeProgressIfAnySlotAssigned</c>
/// at <c>0x005068F0</c> in the pristine <c>74154bfa…</c> image.
/// </remarks>
public sealed class Level100PulseCannonChargeTests
{
    /// <summary>
    /// Charge level 0 and 1 are present (modes
    /// <c>Mech Pulse Cannon Charged</c> / <c>Mech Pulse Cannon Charged 2</c>),
    /// so <see cref="RetailWeaponCharge.MaxCharge"/> is 100 and
    /// <see cref="RetailWeaponCharge.CanCharge"/> is true. A table that only
    /// kept level 0 would report a zero scale and refuse to increment.
    /// </summary>
    [Fact]
    public void PulseCannonPod_HasTwoPresentLevelsAndATenRate()
    {
        RetailWeaponChargeTable pod = PulseCannonPod();

        Assert.Equal(0x41200000u, Level100PulseCannonCharge.ChargeRateBits);
        Assert.Equal(
            Level100PulseCannonCharge.ChargeRate,
            BitConverter.UInt32BitsToSingle(Level100PulseCannonCharge.ChargeRateBits));
        Assert.Equal(100, RetailWeaponCharge.MaxCharge(pod));
        Assert.True(RetailWeaponCharge.CanCharge(pod));
        Assert.False(RetailWeaponCharge.FullyCharged(pod));
        Assert.Equal(
            0x00000000u,
            BitConverter.SingleToUInt32Bits(RetailWeaponCharge.GetCharge(pod)));
        Assert.True(RetailWeaponCharge.ReadyToCharge(pod, 1.0f));
        Assert.Equal(0x3DCCCCCDu, Level100PulseCannonCharge.ReloadTimeBits);
        Assert.Equal(
            Level100PulseCannonCharge.ReloadTime,
            BitConverter.UInt32BitsToSingle(Level100PulseCannonCharge.ReloadTimeBits));
    }

    /// <summary>
    /// Ten held 20 Hz samples of the charge button, at the authored rate of
    /// 10.0, fill the Pulse Cannon Pod exactly and no further: the
    /// <c>ChargeWeapon</c> wrapper at <c>0x00413CF0</c> stops calling
    /// <c>0x005068F0</c> once <see cref="RetailWeaponCharge.FullyCharged"/>
    /// is true. A rebuild that used the increment body's 400.0 cap as the
    /// *player* cap would keep adding; one that refused the second present
    /// level would never start.
    /// </summary>
    [Fact]
    public void PulseCannonPod_ReachesFullChargeInTenIncrementsOfTen()
    {
        RetailWeaponChargeTable pod = PulseCannonPod();

        for (int sample = 0; sample < 10; sample++)
        {
            Assert.False(RetailWeaponCharge.FullyCharged(pod));
            RetailWeaponCharge.Charge(pod);
        }

        Assert.Equal(
            0x42C80000u,
            BitConverter.SingleToUInt32Bits(pod.Charge));
        Assert.Equal(
            0x3F800000u,
            BitConverter.SingleToUInt32Bits(RetailWeaponCharge.GetCharge(pod)));
        Assert.True(RetailWeaponCharge.FullyCharged(pod));
        // Charge() itself still adds below 400. ChargeWeapon at 0x00413CF0
        // is what stops calling it, via FullyCharged. The increment body's
        // 400.0 cap is pinned in RetailWeaponChargeTests.
    }

    /// <summary>
    /// <c>Pulse Cannon Pod</c> @<c>0x17463</c> charge-level 1 names
    /// <c>Mech Pulse Cannon Charged 2</c> @<c>0x135b3</c>, whose
    /// <c>CWeaponRound</c> is <c>Mech Pulse Bolt Large</c> @<c>0xacda</c>
    /// of the same <c>e1fb3ded…ada14</c> physics.dat. FullyCharged is
    /// charge &gt;= MaxCharge 100, so ten rate-10 increments select Large.
    /// Charge 0 is still level 0 / Medium. A rebuild that always launched
    /// Medium, or that used Small, dies here.
    /// </summary>
    [Fact]
    public void FireAtFullyCharged_SelectsCharged2LargeBolt()
    {
        RetailWeaponChargeTable tap = PulseCannonPod();
        Assert.Equal(
            Level100ProjectileKind.MechPulseBoltMedium,
            Level100PulseCannonCharge.SelectFireRound(tap));

        for (int sample = 0; sample < 10; sample++)
        {
            RetailWeaponCharge.Charge(tap);
        }

        Assert.True(RetailWeaponCharge.FullyCharged(tap));
        Assert.Equal(
            Level100ProjectileKind.MechPulseBoltLarge,
            Level100PulseCannonCharge.SelectFireRound(tap));
    }

    private static RetailWeaponChargeTable PulseCannonPod() =>
        Level100PulseCannonCharge.CreatePod();
}
