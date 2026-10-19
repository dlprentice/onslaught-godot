// SPDX-License-Identifier: GPL-3.0-only
// DeclareOnGround0040c750..0040c983, pristine PC SHA-256
// 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
// The private full-body oracle intercepts terrain and effect callbacks, leaving the retail arithmetic and base
// ground/object methods unchanged. The optional cosine group selects only rows that actually evaluated it.
using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineGroundTests
{
    private static BattleEngine Engine(BattleEngineState state = BattleEngineState.Jet)
    {
        var pool = new SPtrSetPool(); pool.Init(128);
        var events = new EventManager(); events.Init();
        var bytes = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(16 + 0x102c), 1);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(16 + 0x1034), 100);
        HeightField map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("ground-test-flat-map", bytes)));
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(12345), map, new PhysicsRegistry());
        var engine = new BattleEngine(context)
        {
            Configuration = new BattleEngineData { Life = 20, Energy = 12, ShieldEfficiency = 50 },
            State = state, Pos = new FVector(4, 5, 0), Life = 20, Energy = 7, Shields = 10,
            Vulnerable = true, LastTimeOnGround = -100, LastTimeOnObject = -100,
        };
        engine.Player = new Player(1, context);
        engine.WalkerPart = new BattleEngineWalkerPart(engine);
        return engine;
    }

    [RetailOracleFact("be-ground-cosine")]
    public void SurfaceCosineMatchesEveryEvaluatedOriginalPc24Case()
    {
        // Projection of the original 1,028 full-body cases: 13 skipped the hard branch, so their default scratch
        // values are not evidence. These 1,015 rows retain their original case names, including zero/NaN normals.
        // The separate PC53 negative controls expose the retained velocity-Z and threshold-spill boundaries;
        // this core intentionally implements the retail runtime's PC24 mode.
        var lines = new List<string>();
        var identities = new HashSet<string>();
        foreach ((string identity, uint[] words, string expected) in RetailOracle.Cases("be-ground-cosine"))
        {
            Assert.Equal(15, words.Length);
            Assert.True(identities.Add(identity), $"duplicate original oracle row: {identity}");
            float cosine = BattleEngine.HardLandingSquaredCosine(RetailOracle.V(words, 4), RetailOracle.V(words, 7));
            string actual = $"{identity} {RetailOracle.H(cosine)}";
            Assert.True(actual == expected, $"original row {identity}: {actual}\nretail: {expected}");
            lines.Add(actual);
        }
        Assert.Equal(1015, lines.Count);
        Assert.Contains("zero_normal", identities);
        Assert.Contains("nan_normal", identities);
        Assert.Contains("oblique_0999", identities);
        RetailOracle.WriteOurs("be-ground-cosine", lines.ToArray());
    }

    [Theory]
    [InlineData(BattleEngineState.Jet, 0x3e4cccccu, 0x3e3851eau, 0x41a00000u, false)]
    [InlineData(BattleEngineState.Jet, 0x3e4ccccdu, 0x3e3851ebu, 0x41a00000u, false)]
    [InlineData(BattleEngineState.Jet, 0x3e4cccceu, 0x00000000u, 0x41866666u, true)]
    [InlineData(BattleEngineState.Walker, 0x3eccccccu, 0x3eccccccu, 0x41a00000u, false)]
    [InlineData(BattleEngineState.Walker, 0x3ecccccdu, 0x3ecccccdu, 0x41a00000u, false)]
    [InlineData(BattleEngineState.Walker, 0x3eccccceu, 0x00000000u, 0x41599999u, true)]
    [InlineData(BattleEngineState.MorphingIntoJet, 0x3e4ccccdu, 0x3e4ccccdu, 0x41a00000u, false)]
    public void FlatGroundUsesStrictStateSpecificCapAndOnlySoftJetDamping(
        BattleEngineState state, uint speed, uint expectedSpeed, uint expectedLife, bool hard)
    {
        BattleEngine be = Engine(state);
        Assert.Equal(new FVector(0, 0, -1), be.Context.Map.Normal(be.Pos));
        be.Velocity = new FVector(0, 0, RetailOracle.F(speed));
        be.DeclareOnGround();
        Assert.Equal(expectedSpeed, BitConverter.SingleToUInt32Bits(be.Velocity.Z));
        Assert.Equal(expectedLife, BitConverter.SingleToUInt32Bits(be.Life));
        Assert.Equal(0, be.Velocity.X); Assert.Equal(0, be.Velocity.Y);
        Assert.Equal(10, be.Shields);
        Assert.Equal(hard && state == BattleEngineState.Walker ? 10 : 7, be.Energy);
        Assert.Equal(hard ? be.Context.Events.Time : -1, be.LastDamageTime);
        Assert.Equal(be.Context.Events.Time, be.LastTimeOnGround);
    }

    [Theory]
    [InlineData(BattleEngineState.Jet, 7f)]
    [InlineData(BattleEngineState.Walker, 10f)]
    public void HardLandingBypassesShieldsAndRunsDamageCommonTail(BattleEngineState state, float energy)
    {
        BattleEngine be = Engine(state);
        be.Velocity = new FVector(0, 0, .5f);
        be.DeclareOnGround();
        // Original full-body parallel-impact control: amount 0x41000000 (8), shields flag false, part -1.
        Assert.Equal(12, be.Life); Assert.Equal(10, be.Shields); Assert.Equal(energy, be.Energy);
        Assert.Equal(new FVector(0, 0, 0), be.Velocity);
        Assert.Equal(2048, Assert.IsType<Player>(be.Player).Stats[(int)PlayerStat.DamageTaken]);
        Assert.Equal(be.Context.Events.Time, be.LastDamageTime);
    }

    [Theory]
    [InlineData(BattleEngineState.Walker, 10f)]
    [InlineData(BattleEngineState.Jet, 7f)]
    public void TangentialHardContactStillCallsZeroDamageAndDoesNotApplySoftJetFriction(
        BattleEngineState state, float expectedEnergy)
    {
        BattleEngine be = Engine(state);
        be.Velocity = new FVector(.5f, 0, 0); be.AugValue = 12;
        be.DeclareOnGround();
        Assert.Equal(new FVector(.5f, 0, 0), be.Velocity);
        Assert.Equal(20, be.Life); Assert.Equal(10, be.Shields);
        Assert.Equal(expectedEnergy, be.Energy); Assert.Equal(10, be.AugValue); // Damage(0)'s common tail executed.
        Assert.Equal(-1, be.LastDamageTime);
        Assert.Equal(0, Assert.IsType<Player>(be.Player).Stats[(int)PlayerStat.DamageTaken]);
    }

    [Theory]
    [InlineData(0x3e199999u, false)]
    [InlineData(0x3e19999au, true)]
    [InlineData(0x3e19999bu, true)]
    public void RecentObjectContactSuppressesDamageOnlyStrictlyBeforePointFifteen(uint sinceObject, bool damages)
    {
        BattleEngine be = Engine();
        Assert.Equal(0, be.Context.Events.Time);
        be.LastTimeOnObject = -RetailOracle.F(sinceObject);
        be.Velocity = new FVector(0, 0, .5f);
        be.DeclareOnGround();
        Assert.Equal(damages ? 12 : 20, be.Life);
        Assert.Equal(damages ? 0 : .5f, be.Velocity.Z);
        Assert.Equal(be.Context.Events.Time, be.LastTimeOnGround); // The base call precedes the guard.
    }

    [Theory]
    [InlineData(BattleEngineState.Walker, false)]
    [InlineData(BattleEngineState.Jet, true)]
    public void DashCounterSuppressesOnlyPureWalkerLanding(BattleEngineState state, bool damages)
    {
        BattleEngine be = Engine(state);
        be.WalkerPart!.DoingDashCount = 1;
        be.Velocity = new FVector(0, 0, .5f);
        be.DeclareOnGround();
        Assert.Equal(damages ? 12 : 20, be.Life);
        Assert.Equal(damages ? 0 : .5f, be.Velocity.Z);
        Assert.Equal(state == BattleEngineState.Walker, be.RenderThingIsWalker);
        Assert.Equal(be.Context.Events.Time, be.LastTimeOnGround);
    }
}
