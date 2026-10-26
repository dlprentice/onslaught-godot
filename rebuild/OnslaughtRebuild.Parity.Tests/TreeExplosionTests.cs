// SPDX-License-Identifier: GPL-3.0-only
// CTree::Hit004f6aa0, unchanged218-byte original caller (SHA256634a08074497c320228ac11e4abe0e678c2337e8681ff293ad5a9eb17f4ee161).
// Pristine74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750; CreateFallingTree was a controlled sink.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class TreeExplosionTests
{
    private sealed class TreeDestroyer(ThingContext context) : Thing(context, Oid.Thing);

    private static Tree Standing()
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init(); events.Log = [];
        var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        var map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("tree-hit-flat-map", raw)));
        map.InitQuickCollisionMap();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), map, new PhysicsRegistry());
        return new Tree(context) { Pos = new(100, 100, -1), TreeLife = 1.5f };
    }

    private static Explosion Blast(Tree tree, float currentRadius)
    {
        var blast = new Explosion(tree.Context) { Pos = new(97, 96, -1), Radius = 50, CurrentRadius = currentRadius };
        blast.SetThingType(ThingType.Explosion | ThingType.CanDestroyTrees);
        return blast;
    }

    [Theory]
    [InlineData(5f, true)]
    [InlineData(5.0000005f, true)]
    [InlineData(4.9999995f, false)]
    [InlineData(0f, false)]
    [InlineData(-1f, false)]
    [InlineData(float.PositiveInfinity, true)]
    [InlineData(float.NaN, true)]
    public void OriginalRadiusComparisonUsesCurrentRadiusAndAdmitsEqualityOrUnordered(float currentRadius, bool falls)
    {
        Tree tree = Standing(); Explosion blast = Blast(tree, currentRadius);
        long random = tree.Context.Crt.Calls;
        tree.Hit(blast, null);
        Assert.Equal(falls, tree.Falling is not null);
        Assert.Equal(random, tree.Context.Crt.Calls);
        Assert.Equal(1.5f, tree.TreeLife); // Hit is the falling trigger, not an invented Damage call.
        if (!falls) { Assert.Equal(0, tree.Context.Events.TotalEvents); return; }
        Assert.Equal(0x3f19999au, BitConverter.SingleToUInt32Bits(tree.Falling!.Dir.X));
        Assert.Equal(0x3f4ccccdu, BitConverter.SingleToUInt32Bits(tree.Falling.Dir.Y));
        Assert.Equal(0u, BitConverter.SingleToUInt32Bits(tree.Falling.Dir.Z));
        Assert.Equal(2, tree.Context.Events.TotalEvents);
        Assert.Single(tree.Context.Events.Log!, e => e.EventNum == Tree.CollideEvent);
        Assert.Single(tree.Context.Events.Log!, e => e.EventNum == Tree.FallEvent);
        FallingTree first = tree.Falling;
        tree.Hit(blast, null);
        Assert.Same(first, tree.Falling); Assert.Equal(2, tree.Context.Events.TotalEvents);
    }

    [Fact]
    public void ABlastMustCarryTheTreeDestructionTypeAndAZeroDirectionStillFalls()
    {
        Tree tree = Standing(); Explosion blast = Blast(tree, 5);
        blast.SetThingType(ThingType.Explosion);
        tree.Hit(blast, null);
        Assert.Null(tree.Falling); Assert.Equal(0, tree.Context.Events.TotalEvents);
        blast.SetThingType(ThingType.Explosion | ThingType.CanDestroyTrees);
        blast.Pos = tree.Pos; blast.CurrentRadius = 0;
        tree.Hit(blast, null);
        Assert.NotNull(tree.Falling); Assert.Equal(new FVector(0, 0, 0), tree.Falling.Dir);
        Assert.Equal(2, tree.Context.Events.TotalEvents);
    }

    // Authored positions run through the original caller. These distinguish retained exponent range,
    // (Y²+Z²)+X², and the unspilled reciprocal from the generic FVector.Normalise implementation.
    [Theory]
    [InlineData(0xf149f2cau, 0xf1c9f2cau, 0x72177617u, false, true, 0x3e88d678u, 0x3f08d678u, 0xbf4d41b3u)]
    [InlineData(0xe248a494u, 0x62105ae8u, 0xe1eb9cd8u, false, false, 0x3f3b96ccu, 0xbf06f69fu, 0x3edc48a1u)]
    [InlineData(0x02a5c0e7u, 0x823fc59du, 0x8304b045u, true, true, 0xbf01aa24u, 0x3e9604a4u, 0x3f4f98f7u)]
    [InlineData(0x43afa7abu, 0x4283dd43u, 0xc2d029d1u, false, true, 0xbf6b0231u, 0x3dfedd39u, 0x3ec0ca5bu)]
    [InlineData(0xf0834500u, 0xf000e87cu, 0xf11f4162u, false, true, 0x3ebfc434u, 0x3e3c5118u, 0x3f68a65eu)]
    [InlineData(0xccbba309u, 0x4e26a697u, 0x4c44df17u, false, true, 0x3e0e5528u, 0xbf7cd3a8u, 0xbd955661u)]
    [InlineData(0x4b751144u, 0x4d9f620au, 0xce0f96adu, false, false, 0xbcbef64du, 0xbef86421u, 0x3f5fc6bfu)]
    public void OriginalDirectionKeepsTheHitCallersNormAndReciprocal(
        uint x, uint y, uint z, bool originZero, bool explosion, uint expectedX, uint expectedY, uint expectedZ)
    {
        Tree tree = Standing();
        if (originZero) tree.Pos = new FVector(0, 0, 0);
        Thing other = explosion ? Blast(tree, 1e38f) : new TreeDestroyer(tree.Context);
        other.Pos = new FVector(BitConverter.UInt32BitsToSingle(x), BitConverter.UInt32BitsToSingle(y), BitConverter.UInt32BitsToSingle(z));
        other.SetThingType(ThingType.CanDestroyTrees | (explosion ? ThingType.Explosion : 0));
        tree.Hit(other, null);
        Assert.NotNull(tree.Falling);
        Assert.Equal(expectedX, BitConverter.SingleToUInt32Bits(tree.Falling.Dir.X));
        Assert.Equal(expectedY, BitConverter.SingleToUInt32Bits(tree.Falling.Dir.Y));
        Assert.Equal(expectedZ, BitConverter.SingleToUInt32Bits(tree.Falling.Dir.Z));
    }

    [Fact]
    public void OriginalUnorderedNormSkipsNormalisationRatherThanContaminatingTheOtherComponents()
    {
        Tree tree = Standing(); Explosion blast = Blast(tree, 1);
        blast.Pos = new FVector(float.NaN, 98, -4);
        tree.Hit(blast, null);
        Assert.NotNull(tree.Falling);
        Assert.True(float.IsNaN(tree.Falling.Dir.X));
        Assert.Equal(2, tree.Falling.Dir.Y); Assert.Equal(3, tree.Falling.Dir.Z);
    }

    [GameDataFact]
    public void LoadedLevel100WeaponExplosionCanTriggerTheLoadedTreesExistingFallOwner()
    {
        var session = OnslaughtRebuild.Parity.Game.LevelSession.Load(100, GameData.Read,
            OnslaughtRebuild.Parity.Game.LevelSession.VirtualVoiceFor(GameData.Read));
        var be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        // A destructive blast is a charged/secondary mode, not the initial primary's small effect.
        IEnumerable<Weapon> equipped = be.WalkerPart!.Weapons.Concat(be.JetPart!.Weapons)
            .Concat(new[] { be.WalkerPart.PrimaryWeapon, be.WalkerPart.AugWeapon }.OfType<Weapon>());
        byte[]? name = equipped.SelectMany(w => w.Data.ChargeModes.Select(w.GetWeaponMode))
            .Where(m => m?.Round?.ExplosionName is not null)
            .Select(m => m!.Round!.ExplosionName!)
            .FirstOrDefault(n => session.Context.Physics.GetExplosionIndex(n) is int i && i >= 0 &&
                session.Context.Physics.Explosions[i].Damage > 3 && session.Context.Physics.Explosions[i].Radius > 0);
        Assert.NotNull(name);
        int index = session.Context.Physics.GetExplosionIndex(name);
        Assert.True(index >= 0);
        ExplosionData definition = session.Context.Physics.Explosions[index];
        Assert.True(definition.Damage > 3); Assert.True(definition.Radius > 0);
        Tree tree = session.Context.World.Things.ToList().OfType<Tree>().First(t =>
            t.Falling is null && t.Pos.Z < session.Context.WaterLevel + 1.5f);
        Explosion blast = Assert.IsType<Explosion>(Explosion.Spawn(session.Context, index));
        blast.Init(new InitThing { Pos = tree.Pos, Behaviour = definition });
        Assert.True(blast.IsA(ThingType.CanDestroyTrees));
        Assert.True(blast.CurrentRadius >= 0);
        tree.Hit(blast, null);
        Assert.NotNull(tree.Falling);
        FallingTree first = tree.Falling;
        int events = session.Context.Events.TotalEvents;
        tree.Hit(blast, null);
        Assert.Same(first, tree.Falling); Assert.Equal(events, session.Context.Events.TotalEvents);
    }
}
