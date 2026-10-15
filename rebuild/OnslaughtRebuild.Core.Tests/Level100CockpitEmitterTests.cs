// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The cockpit Gun emitters and each weapon's launch sequence, against the RE
/// lane's aiming contract
/// (<c>reverse-engineering/game-mechanics/battle-engine-aiming.md</c>, "Gun
/// emitters": <c>cockpit2.msh</c>, SHA-256 prefix <c>008b9292</c>, model x
/// right, y forward, z down) and the weapon modes' <c>CWeaponLaunchSequence</c>
/// pairs in <c>data/default physics.dat</c> (<c>e1fb3ded…</c>).
/// </summary>
public sealed class Level100CockpitEmitterTests
{
    [Theory]
    [InlineData(1, true, 9.170048e-05, 0.0842639282, -0.25803259)]
    [InlineData(4, false, 0.000359148398, 0.0829063728, -0.247448772)]
    [InlineData(9, true, -0.23461841, 0.274871588, 0.00570840016)]
    [InlineData(9, false, -0.234618425, 0.0414116606, 0.00570841506)]
    [InlineData(12, true, 0.223871753, 0.275149643, 0.0558485575)]
    [InlineData(13, true, -0.0824229494, -0.0204110984, 0.185880587)]
    [InlineData(14, false, 0.0788260475, -0.0204110984, 0.185880199)]
    public void GunTable_MatchesTheComposedMeshPoses(int gun, bool walk, double x, double y, double z)
    {
        Level100CockpitEmitters.Emitter emitter = Level100CockpitEmitters.Gun(gun, walk);
        Assert.InRange(emitter.XMicrometres - (x * 1e6), -1.0, 1.0);
        Assert.InRange(emitter.YMicrometres - (y * 1e6), -1.0, 1.0);
        Assert.InRange(emitter.ZMicrometres - (z * 1e6), -1.0, 1.0);
    }

    [Fact]
    public void LaunchSequences_NameEachRoundsEmitter()
    {
        Assert.Equal(1, Simulation.LaunchGun(Level100MissionWeapon.PulseCannonPod, 0));
        Assert.Equal([9, 10, 11, 12, 9],
            Enumerable.Range(0, 5).Select(round => Simulation.LaunchGun(Level100MissionWeapon.MechTwinVulcanCannon, round)));
        Assert.Equal([13, 14],
            Enumerable.Range(0, 2).Select(round => Simulation.LaunchGun(Level100MissionWeapon.MechVulcanCannon, round)));
        Assert.Equal([4, 3, 5, 2, 6], Level100MissilePod.LaunchSequenceEmitters.ToArray());
    }

    [Fact]
    public void GunsWithoutAnEmitterAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Level100CockpitEmitters.Gun(7, walkPose: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Level100CockpitEmitters.Gun(8, walkPose: false));
    }
}
