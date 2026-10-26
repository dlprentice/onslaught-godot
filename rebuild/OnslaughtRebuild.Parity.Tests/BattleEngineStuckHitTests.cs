// SPDX-License-Identifier: GPL-3.0-only
// CBattleEngine::Hit00407350, original380B SHA2568034efee2c37c5e02579dc82d4405b758cedc96d62b27909f5c66a6cea43ae8a.
// Pristine74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750; native service callbacks are controlled.
// Authored stuck reports exercise the existing managed owners; no player-route/collision-delivery claim.
using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineStuckHitTests
{
    private sealed class Contact(ThingContext context) : Thing(context, Oid.Thing)
    {
        public FVector First = new(1, -2, 3), Second = new(4, -5, 6);
        public int Queries;
        public Action<int>? OnRead;
        public override FVector GetVelocity()
        {
            int n = ++Queries; OnRead?.Invoke(n);
            return n == 1 ? First : Second;
        }
    }

    private static (BattleEngine Engine, Contact Other) Fixture()
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        var be = new BattleEngine(context) { Pos = new(1, 2, -3), OldPos = new(7, 8, -9) };
        Array.Fill(be.SafePos, new FVector(10, 20, -30));
        var other = new Contact(context); other.SetThingType(ThingType.Unit);
        return (be, other);
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(17, 18)] [InlineData(18, 18)] [InlineData(19, 19)]
    public void StuckHitUsesBothVelocityReadsAndAdvancesTheNativeSafeTryIndex(int index, int next)
    {
        var (be, other) = Fixture(); be.CurrentSafeTry = index; be.SafePosTime[index] = -.1f;
        be.Hit(other, new CollisionReport { Stuck = true });
        Assert.Equal(new FVector(20, 6, -12), be.Pos); // safe + 2*first + (0 - -.1)*20*second
        Assert.Equal(new FVector(7, 8, -9), be.OldPos); // MoveTo, not Teleport
        Assert.False(be.InSafeCollisionPlace); Assert.Equal(next, be.CurrentSafeTry);
        Assert.Equal(2, other.Queries); Assert.Equal(1, be.Unknown248); // inherited Unit.Hit still ran
        Assert.Equal(new FVector(10, 20, -30), be.SafePos[index]);
        Assert.Equal(0, be.Context.Events.TotalEvents); Assert.Equal(0, be.Context.Crt.Calls);
    }

    [Fact]
    public void AContactWithoutStuckStillRunsTheBaseHitWithoutRecoveryOrVelocityQueries()
    {
        var (be, other) = Fixture();
        be.Hit(other, new CollisionReport());
        Assert.Equal(new FVector(1, 2, -3), be.Pos); Assert.True(be.InSafeCollisionPlace);
        Assert.Equal(0, be.CurrentSafeTry); Assert.Equal(0, other.Queries); Assert.Equal(1, be.Unknown248);
    }

    [Theory]
    [InlineData(0x7fc00000u)] [InlineData(0xffc00000u)]
    [InlineData(0x7fc12345u)] [InlineData(0xffc54321u)]
    public void OriginalFsubPreservesTheRecordedNanSignAndPayload(uint bits)
    {
        var (be, other) = Fixture(); be.SafePosTime[0] = BitConverter.UInt32BitsToSingle(bits);
        be.Hit(other, new CollisionReport { Stuck = true });
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(be.Pos.X));
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(be.Pos.Y));
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(be.Pos.Z));
        Assert.Equal(2, other.Queries); Assert.Equal(1, be.CurrentSafeTry);
    }

    [Theory]
    [InlineData(0x00000000u)] [InlineData(0x80000000u)]
    public void EqualTimeStillIncludesTheFirstDoubledVelocity(uint time)
    {
        var (be, other) = Fixture(); be.SafePosTime[0] = BitConverter.UInt32BitsToSingle(time);
        be.Hit(other, new CollisionReport { Stuck = true });
        Assert.Equal(new FVector(12, 16, -24), be.Pos); Assert.Equal(2, other.Queries);
    }

    [Fact]
    public void ZeroVelocityReturnsToTheSafePositionWithoutChangingTheEnginesVelocity()
    {
        var (be, other) = Fixture(); other.First = other.Second = new FVector(0, 0, 0);
        be.Velocity = new FVector(7, -8, 9); be.SafePosTime[0] = -10;
        be.Hit(other, new CollisionReport { Stuck = true });
        Assert.Equal(be.SafePos[0], be.Pos); Assert.Equal(new FVector(7, -8, 9), be.Velocity);
        Assert.Equal(2, other.Queries);
    }

    [Fact]
    public void BaseAndStandingDeclarationPrecedeReadsAndEachSavedValueKeepsItsNativeReadPoint()
    {
        var (be, other) = Fixture(); other.SetThingType(ThingType.Unit | ThingType.CanBeWalkedOn);
        be.SafePos[1] = new FVector(1000, 2000, -3000); be.SafePosTime[1] = -.2f;
        other.OnRead = n =>
        {
            Assert.Equal(1, be.Unknown248); Assert.Same(other, be.StandingOnThing.ToRead);
            if (n == 1) be.CurrentSafeTry = 1;
            be.Context.Events.AdvanceTime();
        };
        be.Hit(other, new CollisionReport { Stuck = true });
        Assert.Equal(new FVector(32, -9, 6), be.Pos); // old position, new index's time, clock before second read
        Assert.Equal(2, be.CurrentSafeTry); Assert.Equal(2, other.Queries);
        other.Destroy(); Assert.Null(be.StandingOnThing.ToRead);
    }

    // Authored fields executed through the original caller at event time0.150000006. These expose
    // the folded20 multiplier and the different X versus Y/Z spill points.
    [Theory]
    [InlineData(0x3cdf324cu, 0xc37b8d15u, 0x443cd2b4u, 0xc46b78f1u, 0xeff3e877u, 0xf0b211e4u, 0x6f48e1afu, 0x7045ec47u, 0x7114d6cbu, 0xf12ffa03u, 0x7072028fu, 0x713b57c8u, 0xf1cb76beu)]
    [InlineData(0x3b8cc6bau, 0xc479983au, 0xc3af1e61u, 0xc45ec36au, 0xf0f77a2eu, 0x7034edc6u, 0x706efdd3u, 0x70485855u, 0x70853c1fu, 0xf155c3a5u, 0xf0cb0b48u, 0x718e4bdeu, 0xf1fbb701u)]
    [InlineData(0xbeb87420u, 0x433b6b76u, 0x43885eaeu, 0xc3a80234u, 0xe2328305u, 0x6261d034u, 0xe209fc7du, 0x61ea954du, 0xe213f21eu, 0x6185111du, 0x6351fda2u, 0xe3844628u, 0x62c981ffu)]
    [InlineData(0xbfa205cdu, 0x442e16d0u, 0xc4196493u, 0xc2560ffcu, 0x4d9adbeau, 0xcd86c03bu, 0xcde885afu, 0x4d2a8fa2u, 0x4e15e978u, 0x4e71c414u, 0x4faa4867u, 0x5080714eu, 0x50ceaaa7u)]
    [InlineData(0xbf898b5au, 0xc3a15506u, 0x4458423au, 0xc4669c89u, 0x4e77efc6u, 0x4dfea1c7u, 0x4ddc4671u, 0x4e428012u, 0x4e534714u, 0x4e1dbeb0u, 0x50a45b81u, 0x50a9a8c6u, 0x507f3a70u)]
    [InlineData(0xbb844475u, 0xc3fd923fu, 0xc3f362cfu, 0xc3174121u, 0xe1ab1202u, 0xe250a63bu, 0x6186ea3bu, 0x61ebdb26u, 0xe23b013du, 0x625e9a32u, 0x62403becu, 0xe3785a3eu, 0x634d2c54u)]
    public void OriginalPositionKeepsTheClockProductAndComponentSpills(uint time, uint sx, uint sy, uint sz,
        uint ax, uint ay, uint az, uint bx, uint by, uint bz, uint expectedX, uint expectedY, uint expectedZ)
    {
        var (be, other) = Fixture();
        float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
        be.Context.Events.AdvanceTime(); be.Context.Events.AdvanceTime(); be.Context.Events.AdvanceTime();
        Assert.Equal(0x3e19999au, BitConverter.SingleToUInt32Bits(be.Context.Events.Time));
        be.SafePos[0] = new FVector(F(sx), F(sy), F(sz)); be.SafePosTime[0] = F(time);
        other.First = new FVector(F(ax), F(ay), F(az)); other.Second = new FVector(F(bx), F(by), F(bz));
        be.Hit(other, new CollisionReport { Stuck = true });
        Assert.Equal(expectedX, BitConverter.SingleToUInt32Bits(be.Pos.X));
        Assert.Equal(expectedY, BitConverter.SingleToUInt32Bits(be.Pos.Y));
        Assert.Equal(expectedZ, BitConverter.SingleToUInt32Bits(be.Pos.Z));
        Assert.Equal(2, other.Queries);
    }

    [GameDataFact]
    public void LoadedBattleEngineStuckContactUsesItsExistingSafeHistoryAndStandingReader()
    {
        var session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        var be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        Thing support = session.Context.World.Things.ToList().First(t => !ReferenceEquals(t, be) &&
            t.IsA(ThingType.CanBeWalkedOn) && t.GetVelocity().Equals(new FVector(0, 0, 0)));
        FVector safe = be.SafePos[0], old = be.OldPos; int events = session.Context.Events.TotalEvents;
        be.Hit(support, new CollisionReport { Stuck = true });
        Assert.Equal(safe, be.Pos); Assert.Equal(old, be.OldPos);
        Assert.Same(support, be.StandingOnThing.ToRead);
        Assert.Equal(session.Context.Events.Time, be.LastTimeOnObject);
        Assert.False(be.InSafeCollisionPlace); Assert.Equal(1, be.CurrentSafeTry);
        Assert.Equal(events, session.Context.Events.TotalEvents);
    }
}
