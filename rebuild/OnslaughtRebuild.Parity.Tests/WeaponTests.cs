// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class WeaponTests(ITestOutputHelper output)
{
    /// <summary>A context with an empty physics registry holding invented weapon modes; no map (these cases never ask it).</summary>
    private static (ThingContext Context, PhysicsRegistry Physics) Synthetic()
    {
        var physics = new PhysicsRegistry();
        var events = new EventManager();
        events.Init();
        var pool = new SPtrSetPool();
        pool.Init(64);
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123456), null!, physics);
        return (context, physics);
    }

    private static WeaponModeData Mode(PhysicsRegistry physics, string name, RoundData? round)
    {
        var mode = new WeaponModeData(Encoding.ASCII.GetBytes(name)) { Round = round };
        physics.WeaponModes.Add(mode);
        return mode;
    }

    [Fact]
    public void TheCurrentModeIsTheHighestChargeLevelWithAModeAndTheQuotientRoundsToNearest()
    {
        (ThingContext context, PhysicsRegistry physics) = Synthetic();
        Mode(physics, "level0", null);
        Mode(physics, "level3", null);
        var data = new WeaponData("w"u8.ToArray());
        data.ChargeModes[0] = 0;
        data.ChargeModes[3] = 1;
        var weapon = new Weapon(context, data, 0);
        weapon.InitialiseMode();

        Assert.Equal("level0", weapon.Mode!.NameText);
        weapon.ChargeValue = 299.4f;   // (SINT) rounds to 299 under /QIfist: level 2, which walks down to 0
        Assert.Equal("level0", weapon.GetCurrentMode()!.NameText);
        weapon.ChargeValue = 299.6f;   // rounds to 300 (truncation would give 299): level 3
        Assert.Equal("level3", weapon.GetCurrentMode()!.NameText);
        Assert.True(weapon.CanCharge());
        Assert.Equal(300, weapon.GetMaxCharge());
        Assert.False(weapon.FullyCharged()); // 299.6 < 300
        weapon.ChargeValue = 300.0f;
        Assert.True(weapon.FullyCharged());
    }

    [Fact]
    public void ChargingStopsAtFourHundredAndReadinessFollowsTheNextFireTime()
    {
        (ThingContext context, PhysicsRegistry physics) = Synthetic();
        Mode(physics, "m", null);
        var data = new WeaponData("w"u8.ToArray()) { ChargeRate = 150.0f };
        data.ChargeModes[0] = 0;
        data.ChargeModes[1] = 0;
        var weapon = new Weapon(context, data, 0);
        weapon.InitialiseMode();

        for (int i = 0; i < 5; i++)
        {
            weapon.Charge();
        }

        Assert.Equal(450.0f, weapon.ChargeValue); // 0, 150, 300 (< 400), 450, then no more
        Assert.True(weapon.ReadyToFire());         // time 0 > -200
        weapon.NextFireTime = 5.0f;
        Assert.False(weapon.ReadyToFire());
        Assert.False(weapon.ReadyToCharge());
    }

    [Fact]
    public void FiringWithoutARoundFailsAndABurstIsInProgressOnlyMidway()
    {
        (ThingContext context, PhysicsRegistry physics) = Synthetic();
        WeaponModeData mode = Mode(physics, "m", null);
        mode.BurstSize = 3;
        var data = new WeaponData("w"u8.ToArray());
        data.ChargeModes[0] = 0;
        var weapon = new Weapon(context, data, 0);
        weapon.InitialiseMode();

        Assert.False(weapon.Fire()); // no round: retail returns FALSE before any bookkeeping
        Assert.False(weapon.IsFiring());
        weapon.BurstCount = 2;
        Assert.True(weapon.IsFiring());
        weapon.BurstCount = 3;
        Assert.False(weapon.IsFiring());
    }

    [Fact]
    public void TheActualMaximumRangeOfAStraightRoundIsItsSpeedTimesItsLife()
    {
        (ThingContext context, PhysicsRegistry physics) = Synthetic();
        var round = new RoundData("r"u8.ToArray()) { Speed = 14.0f, LifeSpan = 9.5f };
        WeaponModeData mode = Mode(physics, "m", round);
        mode.MaxRange = 130.0f;
        mode.LockRange = 77.0f;
        var data = new WeaponData("w"u8.ToArray());
        data.ChargeModes[0] = 0;
        var weapon = new Weapon(context, data, 0);
        weapon.InitialiseMode();

        Assert.Equal(14.0f * 9.5f, weapon.GetActualMaxRange(new FVector(0, 0, 0)));
        round.TurnRate = 1.0f;
        round.Seek = 1;
        Assert.Equal(77.0f, weapon.GetActualMaxRange(new FVector(0, 0, 0)));
        round.Beam = 1;
        Assert.Equal(130.0f, weapon.GetActualMaxRange(new FVector(0, 0, 0)));
        Assert.Equal(130.0f, weapon.GetMaxRange(new FVector(1, 2, 3)));
    }

    [GameDataFact]
    public void TheSatTurretSpawnsItsLauncherWithItsModeAndRanges()
    {
        ThingContext context = LevelLoadTests.Level100Context(out _);
        UnitData sat = context.Physics.GetUnitData("SAT Turret"u8.ToArray())!;
        UnitWeaponReference uw = sat.Weapons[0];
        Weapon weapon = Weapon.Spawn(context, uw.Index, uw.Unknown04)!;

        Assert.Equal("SAT Launcher", weapon.Data.NameText);
        WeaponModeData mode = weapon.Mode!;
        Assert.Equal(8, mode.BurstSize);
        Assert.Equal(130.0f, mode.MaxRange);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], mode.LaunchSequence);
        Assert.Equal("SAT 1", mode.Round!.NameText);
        Assert.False(WeaponMaths.IsBallistic(mode.Round));
        Assert.Equal(mode.MinRange, weapon.GetMinRange(new FVector(0, 0, 0)));
        Assert.Equal(130.0f, weapon.GetMaxRange(new FVector(0, 0, 0)));
        Assert.Equal(uw.Unknown04, (int)weapon.TargetTypes);
    }

    [GameDataFact]
    public void ListTheLevel100WeaponsAndWhetherTheirRoundsAreBallistic()
    {
        ThingContext context = LevelLoadTests.Level100Context(out _);
        string[] units = ["SAT Turret", "Blaster Turret", "Pulse Turret", "Target Tank", "U-17 Highside Transporter", "Air Trainer",
            "Control Tower", "Forseti Pulse Tank Factory", "Forseti Repair Pad"];
        foreach (string name in units)
        {
            UnitData unit = context.Physics.GetUnitData(Encoding.ASCII.GetBytes(name))!;
            foreach (UnitWeaponReference uw in unit.Weapons)
            {
                Weapon? weapon = Weapon.Spawn(context, uw.Index, uw.Unknown04);
                RoundData? round = weapon?.Mode?.Round;
                output.WriteLine($"{name}: weapon {weapon?.Data.NameText} mount {uw.Mount} types 0x{uw.Unknown04:x} mode {weapon?.Mode?.NameText} round {round?.NameText} ballistic {(round is null ? "-" : WeaponMaths.IsBallistic(round))}");
            }
        }
    }
}
