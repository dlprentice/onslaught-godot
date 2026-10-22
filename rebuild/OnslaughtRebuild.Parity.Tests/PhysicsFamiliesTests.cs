// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using OnslaughtRebuild.Parity.Physics;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>The physics script's weapon, weapon mode, round, spawner, explosion and component families.</summary>
public sealed class PhysicsFamiliesTests
{
    private static byte[] B(string s) => Encoding.ASCII.GetBytes(s);

    private static string S(byte[]? b) => b is null ? "(null)" : Encoding.ASCII.GetString(b);

    [GameDataFact]
    public void EveryValueOfTheDefaultScriptApplies()
    {
        PhysicsRegistry r = PhysicsRegistry.Build(PhysicsScriptTests.Load());

        Assert.Equal(0, r.SkippedValues);
        Assert.Equal(139, r.Weapons.Count);
        Assert.Equal(145, r.WeaponModes.Count);
        Assert.Equal(91, r.Rounds.Count);
        Assert.Equal(38, r.SpawnerDefinitions.Count);
        Assert.Equal(118, r.Explosions.Count);
        Assert.Equal(39, r.Components.Count);
        Assert.DoesNotContain(r.Units.Concat(r.Components).SelectMany(u => u.Weapons), w => w.Index < 0);
    }

    [GameDataFact]
    public void TheSatTurretFiresEightSatRoundsFromGunA()
    {
        PhysicsRegistry r = PhysicsRegistry.Build(PhysicsScriptTests.Load());
        UnitWeaponReference gun = Assert.Single(r.GetUnitData(B("SAT Turret"))!.Weapons);
        Assert.Equal(88, gun.Index);
        Assert.Equal(1, gun.Mount); // GunA

        WeaponData weapon = r.GetWeaponData(gun.Index)!;
        Assert.Equal("SAT Launcher", weapon.NameText);
        Assert.Equal(new[] { 50, -1, -1, -1, -1 }, weapon.ChargeModes);
        Assert.Equal(WeaponData.DefaultLanguageName, weapon.LanguageName);

        WeaponModeData mode = r.WeaponModes[50];
        Assert.Equal("SAT Launcher", mode.NameText);
        Assert.Equal(10.0f, mode.ReloadTime);
        Assert.Equal(8, mode.BurstSize);
        Assert.Equal(130.0f, mode.MaxRange);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, mode.LaunchSequence);
        Assert.Equal("Flash Tiny", S(mode.MuzzleEffectName));
        Assert.Equal("Bolt", S(mode.LaunchSoundName));

        RoundData round = mode.Round!;
        Assert.Same(r.GetRoundData(B("SAT 1")), round);
        Assert.Equal(14.0f, round.Speed);
        Assert.Equal(0.5f, round.Damage);
        Assert.Equal(10.0f, round.LifeSpan);
        Assert.Equal(1, round.Seek); // seek target
        Assert.Equal("SAT Hit", S(round.ExplosionName));
        Assert.Equal("SAT Effect", S(round.EffectName));
    }

    [GameDataFact]
    public void TheAirTrainersMissileSeeksOnlyItsTarget()
    {
        PhysicsRegistry r = PhysicsRegistry.Build(PhysicsScriptTests.Load());
        UnitWeaponReference gun = Assert.Single(r.GetUnitData(B("Air Trainer"))!.Weapons);
        Assert.Equal(2, gun.Mount); // GunB
        WeaponModeData mode = r.WeaponModes[r.GetWeaponData(gun.Index)!.ChargeModes[0]];
        Assert.Equal(20.0f, mode.MinRange);
        Assert.Equal(60.0f, mode.MaxRange);
        Assert.Equal("Forseti Missile", mode.Round!.NameText);
        Assert.Equal(2, mode.Round.Seek); // seek target only
        Assert.Equal(15.0f, mode.Round.Speed);
    }

    [GameDataFact]
    public void TheTargetTankExplodesMedium()
    {
        PhysicsRegistry r = PhysicsRegistry.Build(PhysicsScriptTests.Load());
        ExplosionData e = r.GetExplosionData(r.GetUnitData(B("Target Tank"))!.Explosion)!;
        Assert.Equal("Tank Explosion Medium", e.NameText);
        Assert.Equal(2.0f, e.Radius);
        Assert.Equal(1.0f, e.Damage);
        Assert.Equal(10, e.Time);
        Assert.Equal("Explosion Medium", S(e.SoundName));
    }

    [GameDataFact]
    public void ARepeatedNameAppendsADefinitionThatKeepsItsDefaults()
    {
        // Two statements each for "Bullet Hit" and "Boat Sabre Spawner": the second appends a definition, but its values
        // apply to the first definition of that name, so the second stays as constructed.
        PhysicsRegistry r = PhysicsRegistry.Build(PhysicsScriptTests.Load());
        ExplosionData[] hits = r.Explosions.Where(e => e.NameText == "Bullet Hit").ToArray();
        Assert.Equal(2, hits.Length);
        Assert.Same(hits[0], r.GetExplosion(B("Bullet Hit")));
        Assert.Null(hits[1].AirEffectName);
        Assert.Equal(0.0f, hits[1].Radius);

        SpawnerData[] boats = r.SpawnerDefinitions.Where(s => s.NameText == "Boat Sabre Spawner").ToArray();
        Assert.Equal(2, boats.Length);
        Assert.Null(boats[1].ThingName);
        Assert.Equal(0.0f, boats[1].Delay);
        Assert.NotNull(boats[0].ThingName);
    }

    [Fact]
    public void ALaunchSequenceGrowsToTheOneBasedPositionItSets()
    {
        var mode = new WeaponModeData(B("m"));
        mode.SetLaunchSequence(3, 7);
        Assert.Equal(new[] { 0, 0, 7 }, mode.LaunchSequence);
        mode.SetLaunchSequence(0, 9); // position 0 sets nothing
        Assert.Equal(new[] { 0, 0, 7 }, mode.LaunchSequence);
        mode.SetLaunchAngle(2, 0.5f, 1.5f);
        Assert.Equal(2, mode.LaunchAngles.Count);
        Assert.Equal((0.0f, 0.0f), (mode.LaunchAngles[0].A, mode.LaunchAngles[0].B));
        Assert.Equal((0.5f, 1.5f), (mode.LaunchAngles[1].A, mode.LaunchAngles[1].B));
    }

    [Fact]
    public void ASpawnerCopiesItsBaseOnlyWhenItsOwnDelayIsSet()
    {
        var source = new SpawnerData(B("base")) { Amount = 5, Delay = 2.0f };
        var plain = new SpawnerData(B("plain"));
        plain.CopyFrom(source);
        Assert.Equal(0, plain.Amount);

        var delayed = new SpawnerData(B("delayed")) { Delay = 1.0f };
        delayed.CopyFrom(source);
        Assert.Equal(5, delayed.Amount);
        Assert.Equal(2.0f, delayed.Delay);
        Assert.Throws<InvalidOperationException>(() => delayed.CopyFrom(null));
    }
}
