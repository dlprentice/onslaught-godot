// SPDX-License-Identifier: GPL-3.0-only
// 005088b0/00507ab0, pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class WeaponReachTests
{
    private sealed class TestUnit(ThingContext context) : Unit(context)
    {
        public override float GetBoundingRadius() => 1;
    }

    private static (ThingContext Context, TestUnit Owner, TestUnit Target, Weapon Weapon) Fixture(bool seeker = true)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init(); events.Log = [];
        var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        var map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("weapon-reach-flat-map", raw)));
        map.InitQuickCollisionMap();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123), map, new PhysicsRegistry());
        var owner = new TestUnit(context) { Pos = new(100, 100, -2), Orientation = FMatrix.Identity, Allegiance = 0 };
        var target = new TestUnit(context) { Pos = new(100, 110, -2), Allegiance = 1 };
        owner.SetThingType(ThingType.Unit); target.SetThingType(ThingType.Unit);
        var mode = new WeaponModeData("test"u8.ToArray())
        {
            Round = new RoundData("test"u8.ToArray()) { Speed = 20, Seek = seeker ? 1 : 0 },
            MinDeflection = 1, MaxDeflection = -1, YawTolerance = 1,
        };
        var weapon = new Weapon(context, new WeaponData("test"u8.ToArray()), 0) { Mode = mode };
        weapon.Init(new InitEquipment { AttachedTo = owner, Number = -1 });
        return (context, owner, target, weapon);
    }

    private static void Register(Thing thing)
    {
        thing.MapWhoEntry.Init(thing.Context.MapWho, thing.Pos, 1);
        thing.Seeker = new CollisionSeekingThing(thing, new InitCollision
        {
            StartCollideOnNextFrame = false, Shape = new SphereVolume(1), CollisionResponse = 1,
        });
    }

    [Theory]
    [InlineData(0f, 0f, true, false)]
    [InlineData(1f, -1f, true, true)]
    [InlineData(-0.00001f, -1f, false, false)]
    [InlineData(1f, 0.00001f, false, false)]
    public void ReachIncludesPitchBoundsButFireExcludesThem(float min, float max, bool reach, bool fire)
    {
        var (_, _, target, weapon) = Fixture(); weapon.Mode!.MinDeflection = min; weapon.Mode.MaxDeflection = max;
        Assert.Equal(reach, weapon.CanReach(target)); Assert.Equal(fire, weapon.CanFireAt(target, false));
    }

    [Fact]
    public void YawIsStrictAndAnArticulatedBarrelAcceptsTheEqualPitchTolerance()
    {
        var (_, owner, target, weapon) = Fixture();
        target.Pos = new(110, 100, -2);
        weapon.Mode!.YawTolerance = (float)(Math.PI / 2);
        Assert.False(weapon.CanFireAt(target, false));
        weapon.Mode.YawTolerance = float.BitIncrement(weapon.Mode.YawTolerance);
        Assert.True(weapon.CanFireAt(target, false));
        target.Pos = new(100, 110, -2); weapon.Unknown98 = 1;
        owner.UnknownE8 = 0.5f; weapon.Mode.YawTolerance = 0.5f;
        Assert.True(weapon.CanFireAt(target, false));
        owner.UnknownE8 = float.BitIncrement(0.5f);
        Assert.False(weapon.CanFireAt(target, false));
    }

    [Fact]
    public void WaterRejectionPrecedesModeAndFearAndTorpedoPrecedesPitchAndLos()
    {
        var (context, owner, target, weapon) = Fixture();
        owner.Pos.Z = 1; weapon.Mode = null;
        Assert.False(weapon.CanReach(target)); Assert.False(weapon.CanFireAt(target, true));
        var (_, _, _, configured) = Fixture(); weapon.Mode = configured.Mode;
        owner.Pos.Z = -2; weapon.Mode!.Round!.Torpedo = 1; weapon.Mode.MinDeflection = -2; weapon.Mode.MaxDeflection = 2;
        Assert.False(weapon.CanReach(target)); // water equals ground: strict comparison fails
        Assert.False(weapon.CanFireAt(target, true));
        BinaryPrimitives.WriteSingleLittleEndian(context.Map.Record.AsSpan(0x1034), -1);
        Assert.True(weapon.CanReach(target)); Assert.True(weapon.CanFireAt(target, true));
        Assert.Equal(0, context.Random.Draws); Assert.Equal(0, context.Crt.Calls);
    }

    [Fact]
    public void RealLineQueryRequiresAUnitOfTheTargetsSideAndIgnoresAmmunition()
    {
        var (context, _, target, weapon) = Fixture(seeker: false);
        Assert.False(weapon.CanReach(target)); // genuinely empty line is not proof of a reachable hit
        Register(target);
        Assert.True(weapon.CanReach(target)); Assert.True(weapon.CanFireAt(target, true));
        var blocker = new TestUnit(context) { Pos = new(100, 105, -2), Allegiance = 0 };
        blocker.SetThingType(ThingType.Unit); Register(blocker);
        Assert.False(weapon.CanReach(target)); Assert.False(weapon.CanFireAt(target, true));
        Assert.True(weapon.CanFireAt(target, false)); // caller explicitly declines LOS
        blocker.Allegiance = target.Allegiance;
        Assert.True(weapon.CanReach(target)); // first hit can be target's ally
        blocker.SetThingType(ThingType.Unit | ThingType.Ammunition); blocker.Allegiance = 0;
        Assert.True(weapon.CanReach(target)); Assert.True(weapon.CanFireAt(target, true));
        blocker.SetThingType(ThingType.Stationary);
        Assert.False(weapon.CanReach(target)); // a nonunit is not an allied target
    }

    [Fact]
    public void FireCanIncludeItsOwnHullButReachAlwaysExcludesItAndSeekingOnlyBypassesReachLos()
    {
        var (_, owner, target, weapon) = Fixture(seeker: false);
        Register(owner); Register(target);
        Assert.True(weapon.CanReach(target)); Assert.True(weapon.CanFireAt(target, true));
        owner.Unknown24C = 0;
        Assert.True(weapon.CanReach(target)); Assert.False(weapon.CanFireAt(target, true));
        weapon.Mode!.Round!.Seek = 1;
        Assert.True(weapon.CanReach(target)); Assert.False(weapon.CanFireAt(target, true));
    }

    [Fact]
    public void GroundAndWaterLineObstructionsAreNotTreatedAsClear()
    {
        var (_, _, target, weapon) = Fixture(seeker: false); target.Pos.Z = 2; Register(target);
        Assert.False(weapon.CanReach(target)); Assert.False(weapon.CanFireAt(target, true));
    }

    [Fact]
    public void FearUsesDifferentNonstandardAllegianceRoutingAndPrecedesBallisticAdmission()
    {
        var (context, owner, target, weapon) = Fixture();
        var game = new ParityGame(new PlatformClock(), context.Events); game.InitRestartLoop(); context.Game = game;
        game.ForsetiFearGrid!.Map = context.Map; game.MuspellFearGrid!.Map = context.Map;
        game.ForsetiFearGrid.World = context.World; game.MuspellFearGrid.World = context.World;
        context.World.UnitNB.Add(target); game.ForsetiFearGrid.Rebuild(); game.MuspellFearGrid.Rebuild();
        Assert.True(game.ForsetiFearGrid.Unknown44c780(target.Pos)); Assert.False(game.MuspellFearGrid.Unknown44c780(target.Pos));
        weapon.Mode!.Round!.GridOfFear = 1; owner.Allegiance = 7;
        Assert.False(weapon.CanReach(target)); Assert.True(weapon.CanFireAt(target, false));
        owner.Allegiance = 1; weapon.Mode.Round.Gravity = 1;
        Assert.False(weapon.CanReach(target)); Assert.False(weapon.CanFireAt(target, false));
        owner.Allegiance = 0;
        Assert.Throws<NotSupportedException>(() => weapon.CanReach(target));
        Assert.Throws<NotSupportedException>(() => weapon.CanFireAt(target, false));
    }

    [Fact]
    public void NativeUnorderedPredicatesAreNotReplacedWithSourceLevelComplements()
    {
        Assert.False(Weapon.ReachPitchInBand(0, float.NaN, -1));
        Assert.True(Weapon.ReachPitchInBand(0, 1, float.NaN));
        Assert.True(Weapon.FirePitchInBand(0, 0, float.NaN, -1, 1, false));
        Assert.False(Weapon.FirePitchInBand(0, 0, 1, float.NaN, 1, false));
        Assert.True(Weapon.FirePitchInBand(float.NaN, 0, 1, -1, 1, true));
    }

    [GameDataFact]
    public void InstalledAirTrainerUsesTheAdmittedSeekingNonballisticBranch()
    {
        ThingContext context = LevelLoadTests.Level100Context(out _);
        UnitData data = context.Physics.GetUnitData("Air Trainer"u8.ToArray())!;
        UnitWeaponReference reference = Assert.Single(data.Weapons);
        Weapon weapon = Weapon.Spawn(context, reference.Index, reference.Unknown04)!;
        Assert.Equal("Forseti Missile Trainer Launcher", weapon.Data.NameText);
        Assert.Equal("Forseti Missile", weapon.Mode!.Round!.NameText);
        Assert.False(WeaponMaths.IsBallistic(weapon.Mode.Round));
        Assert.NotEqual(0, weapon.Mode.Round.Seek);
        Assert.Equal(0, weapon.Mode.Predictive);
        var owner = new TestUnit(context) { Pos = new(100, 100, -100), Orientation = FMatrix.Identity };
        var target = new TestUnit(context) { Pos = new(100, 110, -100) };
        weapon.Init(new InitEquipment { AttachedTo = owner, Number = -1 });
        Assert.True(weapon.CanReach(target)); Assert.True(weapon.CanFireAt(target, false));
    }

    [RetailOracleFact("weapon-reach")]
    public void OriginalArithmeticAndBoundaryBlocksMatchIncludingWideExponentControls()
    {
        var lines = new List<string>();
        foreach ((string name, uint[] w, string expected) in RetailOracle.Cases("weapon-reach"))
        {
            Assert.Equal("Reach", name);
            Assert.Equal(18, w.Length);
            FVector delta = RetailOracle.V(w, 0), velocity = RetailOracle.V(w, 3), forward = RetailOracle.V(w, 6), normal = RetailOracle.V(w, 9);
            float pitch = RetailOracle.F(w[12]), min = RetailOracle.F(w[13]), max = RetailOracle.F(w[14]), tolerance = RetailOracle.F(w[15]);
            float target = RetailOracle.F(w[16]), current = RetailOracle.F(w[17]);
            uint B(bool b) => b ? 1u : 0;
            uint H(float f) => BitConverter.SingleToUInt32Bits(f);
            uint[] actual = [H((float)Weapon.QueryElevation(delta)), H(Weapon.QueryRelativePitch(delta, velocity)),
                H((float)Weapon.QueryElevation(velocity)), H(-(float)X87.Atan2(delta.X, delta.Y)), H((float)Weapon.QueryElevation(delta)),
                H(Weapon.QuerySlopePitch(pitch, forward, normal)), B(Weapon.ReachPitchInBand(pitch, min, max)),
                B(Weapon.FirePitchInBand(target, current, min, max, tolerance, true)),
                B(Weapon.FirePitchInBand(target, current, min, max, tolerance, false)),
                B(!(Math.Abs(Weapon.QueryAngleDifference(target, current)) >= tolerance))];
            lines.Add(name + " " + string.Join(" ", actual.Select(v => v.ToString("x8"))));
        }
        Assert.Equal(2073, lines.Count); // two absent/empty result streams are never a successful oracle run
        RetailOracle.WriteOurs("weapon-reach", lines.ToArray());
        Assert.Equal(File.ReadAllLines(Path.Combine(RetailOracle.Dir("weapon-reach")!, "retail.txt")), lines);
    }
}
