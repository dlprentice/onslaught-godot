// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// original CRound.Move004d92c7..004d983a and BE CanBeLocked0040e7d0; matched Round.cpp/BattleEngine.cpp.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class RoundSeekingTests
{
    private sealed class TestUnit(ThingContext context) : Unit(context);
    private static ThingContext Context()
    {
        var pool = new SPtrSetPool(); pool.Init(256);
        var events = new EventManager(); events.Init();
        var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        var map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("round-flat-map", raw)));
        map.InitQuickCollisionMap();
        return new(pool, events, new SeedUniformRandomNumberStream(123), map, new PhysicsRegistry());
    }
    private static BattleEngine Engine(ThingContext c)
    {
        var be = new BattleEngine(c) { Configuration = new BattleEngineData(), State = BattleEngineState.Walker };
        be.SetThingType(ThingType.BattleEngine | ThingType.Unit);
        be.WalkerPart = new BattleEngineWalkerPart(be); be.JetPart = new BattleEngineJetPart(be);
        return be;
    }
    private static Round Shot(ThingContext c, Thing target)
    {
        var r = new Round(c, new RoundData("test"u8.ToArray()) { Damage = 1, Seek = 2, TurnRate = .04f, SeekAngle = 1 })
            { Orientation = FMatrix.Identity, Velocity = new(0, 3, 0), StartTime = -1 };
        r.SetTarget(target, false); return r;
    }

    [Fact]
    public void LockQueryUsesStateAndStrictPositiveCountersNotDyingOrUnitData()
    {
        var be = Engine(Context()); be.Flags |= ThingFlags.Dying;
        be.UnitData = new UnitData("test"u8.ToArray()) { CanBeLocked = 0 };
        Assert.True(be.CanBeLocked()); be.Stealth = float.Epsilon; Assert.False(be.CanBeLocked());
        be.Stealth = float.NaN; Assert.True(be.CanBeLocked());
        be.WalkerPart!.DoingDashCount = 1; Assert.False(be.CanBeLocked());
        be.WalkerPart.DoingDashCount = -1; Assert.True(be.CanBeLocked());
        be.State = BattleEngineState.Jet; be.JetPart!.DoingLoop = true; Assert.False(be.CanBeLocked());
        be.JetPart.DoingLoop = false; be.JetPart.DoingBarrelCount = float.Epsilon; Assert.False(be.CanBeLocked());
        be.JetPart.DoingBarrelCount = -1; Assert.True(be.CanBeLocked());
        be.JetPart.DoingBarrelCount = float.NaN; Assert.True(be.CanBeLocked());
        be.State = BattleEngineState.MorphingIntoJet; be.JetPart.DoingLoop = true; Assert.True(be.CanBeLocked());
    }

    [Theory]
    [InlineData(15f, true)]
    [InlineData(14.999f, false)]
    [InlineData(float.NaN, false)]
    public void OnlyCloseBattleEngineLockLossPreventsReacquisition(float distance, bool retained)
    {
        var c = Context(); var be = Engine(c); be.Pos = new(distance, 0, 0); be.WalkerPart!.DoingDashCount = 1;
        var r = Shot(c, be); r.Data.TurnRate = 0;
        r.UpdateSeeking();
        Assert.Equal(retained, r.Target.ToRead is not null); Assert.Equal(retained ? 1 : 0, r.Unknown130);
        Assert.Equal(retained ? 1 : 0, c.World.Set110.Size); Assert.Equal(retained ? 1 : 0, be.DeletionCount);
        Assert.Equal(0, c.Random.Draws); Assert.Equal(0, c.Crt.Calls);
    }

    [Fact]
    public void OrdinaryTargetDeathOrDisabledLockReleasesWithoutChangingReacquisitionFlag()
    {
        var c = Context(); var target = new TestUnit(c) { Pos = new(0, 100, 0), Flags = ThingFlags.Dying };
        var r = Shot(c, target); r.UpdateSeeking(); Assert.Null(r.Target.ToRead); Assert.Equal(1, r.Unknown130);
        Assert.Equal(0, target.DeletionCount);
        target.Flags = 0; target.UnitData = new UnitData("test"u8.ToArray()) { CanBeLocked = 0 };
        r.SetTarget(target, false); r.UpdateSeeking(); Assert.Null(r.Target.ToRead); Assert.Equal(1, r.Unknown130);
        target.UnitData.CanBeLocked = 1; r.SetTarget(target, false); target.Destroy();
        r.UpdateSeeking(); Assert.Null(r.Target.ToRead); // monitor notification, no stale dereference
    }

    [Theory]
    [InlineData(1f, 1000f, false)]
    [InlineData(0f, 1f, false)]
    [InlineData(float.NaN, 1000f, true)]
    [InlineData(0f, float.NaN, false)]
    [InlineData(0f, 1000f, true)]
    public void SeekingHasStrictTimeBoundsAndNativeUnorderedDelay(float delay, float end, bool turns)
    {
        var c = Context(); var target = new TestUnit(c) { Pos = new(2, 100, 1) };
        var r = Shot(c, target); r.Data.SeekDelay = delay; r.Data.SeekTerminationTime = end;
        r.UpdateSeeking(); Assert.Equal(turns, r.Velocity.X != 0);
        Assert.Same(target, r.Target.ToRead);
    }

    [Fact]
    public void RepairAndUnorderedDamageSkipLockAndTurnBeforeReadingTargetPosition()
    {
        foreach (float damage in new[] {-1f, float.NaN})
        {
            var c = Context(); var be = Engine(c); be.Pos = new(1, 1, 0); be.Stealth = 1;
            var r = Shot(c, be); r.Data.Damage = damage; r.UpdateSeeking();
            Assert.Same(be, r.Target.ToRead); Assert.Equal(1, r.Unknown130); Assert.Equal(new FVector(0, 3, 0), r.Velocity);
        }
    }

    [Fact]
    public void ConeLossDoesNotDisableReacquisitionAndWeirdoSeeksOutsideInsteadOfInside()
    {
        var c = Context(); var target = new TestUnit(c) { Pos = new(100, 0, 0) };
        var r = Shot(c, target); r.UpdateSeeking(); Assert.Null(r.Target.ToRead); Assert.Equal(1, r.Unknown130);
        r.SetTarget(target, false); r.Data.WeirdoSeek = 1; r.UpdateSeeking();
        Assert.Same(target, r.Target.ToRead); Assert.NotEqual(0, r.Velocity.X);
        r.Orientation = FMatrix.Identity; r.Velocity = new(0, 3, 0); target.Pos = new(0, 100, 0);
        r.UpdateSeeking(); Assert.Equal(FMatrix.Identity, r.Orientation); Assert.Equal(new FVector(0, 3, 0), r.Velocity);
    }

    [Fact]
    public void TorpedoMustBeInWaterAndUsesWaterHeightAndDoesNotDropOutsideCone()
    {
        var c = Context(); var target = new TestUnit(c) { Pos = new(100, 0, -20) };
        var r = Shot(c, target); r.Data.Torpedo = 1; r.LastTimeInWater = -100; r.UpdateSeeking();
        Assert.Equal(new FVector(0, 3, 0), r.Velocity);
        r.LastTimeInWater = c.Events.Time; r.UpdateSeeking();
        Assert.Same(target, r.Target.ToRead); Assert.NotEqual(0, r.Velocity.X); Assert.Equal(0, r.Velocity.Z);
    }

    [Fact]
    public void RadarForgetsReleasedRoundAtItsNextScanWithoutPlayingOrSuppressingWarningSound()
    {
        var c = Context(); var be = Engine(c); var r = Shot(c, be); r.Pos = new(0, 5, 0); r.Data.TurnRate = 0;
        var radar = new RadarWarningReceiver(c, c.World.Set110, () => be.PlayIncomingMissileSound = true);
        radar.Init(new InitEquipment { AttachedTo = be }, 80, .03f); radar.Update();
        Assert.Equal(1, radar.Threats.Size); Assert.True(be.PlayIncomingMissileSound);
        be.WalkerPart!.DoingDashCount = 1; r.UpdateSeeking();
        Assert.Equal(0, c.World.Set110.Size); Assert.Equal(1, radar.Threats.Size); Assert.True(be.PlayIncomingMissileSound);
        radar.Update(); Assert.Equal(0, radar.Threats.Size); Assert.True(be.PlayIncomingMissileSound);
        radar.Delete(); Assert.Equal(1, be.DeletionCount); Assert.Equal(0, r.DeletionCount); // queued incoming-warning reader survives
    }

    [Fact]
    public void RealMoveTestsLockDistanceAfterMovementAndPreservesTheUnportedLauncherBoundary()
    {
        var c = Context(); var be = Engine(c); be.Pos = new(100, 116, -10); be.Stealth = 1;
        var r = Shot(c, be); r.Pos = new(100, 100, -10); r.Velocity = new(0, 2, 0);
        r.Move();
        Assert.Equal(new FVector(100, 100, -10), r.OldPos);
        Assert.Equal(new FVector(100, 102, -10), r.Pos);
        Assert.Null(r.Target.ToRead); Assert.Equal(0, r.Unknown130);
        r.SetTarget(be, false); r.Unknown130 = 1; r.Launcher.SetReader(Engine(c));
        Assert.Throws<NotSupportedException>(() => r.UpdateSeeking());
        Assert.Same(be, r.Target.ToRead); Assert.Equal(1, r.Unknown130); Assert.Equal(1, c.World.Set110.Size);
    }

    private sealed class VelocityObservedRound(ThingContext c, RoundData data) : Round(c, data)
    {
        public bool SawUpdatedOrientation;
        public override FVector GetVelocity()
        {
            SawUpdatedOrientation = Orientation.Row0.Y != 0;
            return new(0, 5, 0);
        }
    }

    [Fact]
    public void SeekingPublishesOrientationBeforeCallingTheVirtualVelocityAccessor()
    {
        var c = Context(); var target = new TestUnit(c) { Pos = new(2, 100, 1) };
        var r = new VelocityObservedRound(c, new RoundData("test"u8.ToArray())
            { Damage = 1, Seek = 2, TurnRate = .04f, SeekAngle = 1 })
            { Orientation = FMatrix.Identity, Velocity = new(0, 1, 0), StartTime = -1 };
        r.SetTarget(target, false); r.UpdateSeeking();
        Assert.True(r.SawUpdatedOrientation);
        Assert.InRange(r.Velocity.Magnitude(), 4.99999f, 5.00001f);
    }

    [GameDataFact]
    public void InstalledTrainerRoundUsesThisBranchWithoutAutomaticRetargetingOrMissileSubclass()
    {
        ThingContext c = LevelLoadTests.Level100Context(out _);
        UnitData trainer = c.Physics.GetUnitData("Air Trainer"u8.ToArray())!;
        UnitWeaponReference reference = Assert.Single(trainer.Weapons);
        Weapon weapon = Weapon.Spawn(c, reference.Index, reference.Unknown04)!;
        RoundData d = weapon.Mode!.Round!;
        Assert.Equal("Forseti Missile", d.NameText); Assert.Equal(2, d.Seek); Assert.Equal(0, d.Missile);
        Assert.Equal(0, d.Torpedo); Assert.Equal(0, d.WeirdoSeek); Assert.Equal(0, d.Gravity); Assert.True(d.TurnRate > 0);
    }

    [RetailOracleFact("round-seeking")]
    public void PostMoveReleaseSeekingAndLockPredicatesMatchOriginalCode()
    {
        // Original block executes all its arithmetic/branches, original BE lock query and native matrix/vector
        // dependencies. Target/velocity accessors supply authored fields; radar-remove/reader-clear are counter
        // stubs. This checks post-move state, not the Actor.Move, world lifetime, scheduler or retail play route.
        var c = Context(); var lines = new List<string>();
        foreach ((string name, uint[] w, string expected) in RetailOracle.Cases("round-seeking"))
        {
            Assert.Equal("Seek", name); Assert.Equal(36, w.Length);
            float F(int i) => RetailOracle.F(w[i]);
            var be = Engine(c); be.ThingTypeBits &= ~ThingType.GroundUnit; // oracle accessor returns authored Pos
            be.Stealth = F(28); be.State = (BattleEngineState)w[29];
            be.JetPart!.DoingLoop = w[30] != 0; be.JetPart.DoingBarrelCount = F(31); be.WalkerPart!.DoingDashCount = (int)w[32];
            Unit target = w[33] != 0 ? be : new TestUnit(c) { UnitData = new UnitData("test"u8.ToArray()) { CanBeLocked = (int)w[35] } };
            target.Pos = RetailOracle.V(w, 3); if (w[34] != 0) target.Flags |= ThingFlags.Dying;
            var r = Shot(c, target); r.Pos = RetailOracle.V(w, 0);
            r.Orientation = new(RetailOracle.V(w, 6), RetailOracle.V(w, 9), RetailOracle.V(w, 12)); r.Velocity = RetailOracle.V(w, 15);
            r.Data.Damage = F(18); r.Data.TurnRate = F(19); r.Data.SeekDelay = F(20); r.Data.SeekTerminationTime = F(21);
            r.Data.SeekAngle = F(22); Assert.Equal(c.Events.Time, F(23)); r.StartTime = F(24);
            r.Data.Torpedo = (int)w[25]; r.Data.WeirdoSeek = (int)w[26]; r.LastTimeInWater = w[27] != 0 ? c.Events.Time : c.Events.Time - 100;
            int before = c.World.Set110.Size; r.UpdateSeeking();
            float[] floats = [r.Orientation.Row0.X,r.Orientation.Row0.Y,r.Orientation.Row0.Z,
                r.Orientation.Row1.X,r.Orientation.Row1.Y,r.Orientation.Row1.Z,
                r.Orientation.Row2.X,r.Orientation.Row2.Y,r.Orientation.Row2.Z,r.Velocity.X,r.Velocity.Y,r.Velocity.Z];
            uint[] tail = [r.Target.ToRead is null ? 0u : 1u,(uint)r.Unknown130,(uint)(before-c.World.Set110.Size),
                r.Target.ToRead is null ? 1u : 0u,be.CanBeLocked() ? 1u : 0u];
            string line = "Seek " + string.Join(" ",floats.Select(RetailOracle.H).Concat(tail.Select(v => v.ToString("x8"))));
            lines.Add(line); Assert.True(line == expected, $"case {lines.Count-1}:\\nours {line}\\nretail {expected}");
            r.ReleaseTarget();
        }
        Assert.Equal(2320, lines.Count);
        RetailOracle.WriteOurs("round-seeking", lines.ToArray());
    }
}
