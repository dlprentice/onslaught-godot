// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// HandleSkimming00411500, Damage0040a890, AddShockShake00407940 and HostileEnvironment0040dce0.
// Original-code oracle uses authored objects and a flat Map.Collide hook, null cockpit/attacker,
// and an unavailable sound effect with intercepted formatting/logging. No game or device acceptance.
using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineSkimmingTests
{
    private static HeightField FlatMap()
    {
        var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x102c), 1.0f);
        return HeightField.Load(new ChunkReader(MemBuffer.FromFile("skim-flat-map", raw)));
    }

    private static BattleEngine Engine(int seed, float water, HeightField? sharedMap = null)
    {
        HeightField map = sharedMap ?? FlatMap();
        BinaryPrimitives.WriteSingleLittleEndian(map.Record.AsSpan(0x1034), water);
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        for (int i = 0; i < 40; i++) events.AdvanceTime();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(seed), map, new PhysicsRegistry());
        var be = new BattleEngine(context)
        {
            Configuration = new BattleEngineData { Life = 100, Energy = 20, ShieldEfficiency = 50 },
            Life = 100, Energy = 15, State = BattleEngineState.Jet,
        };
        be.Player = new Player(1, context);
        be.WalkerPart = new BattleEngineWalkerPart(be);
        be.JetPart = new BattleEngineJetPart(be);
        return be;
    }

    [RetailOracleFact("flight-skim")]
    public void WaterGuardsVelocityPitchDamageAndHostileTimeMatchOriginalCode()
    {
        var actual = new List<string>();
        HeightField map = FlatMap();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("flight-skim"))
        {
            Assert.Equal("Skim", function); Assert.Equal(11, w.Length);
            float F(int n) => RetailOracle.F(w[n]);
            BattleEngine be = Engine(unchecked((int)w[9]), F(0), map);
            be.Pos = new FVector(0, 0, F(1));
            be.Velocity = RetailOracle.V(w, 2); be.Pitchvel = F(5);
            be.LastTimeInHostileEnvironment = F(6); be.Shields = F(7);
            be.Vulnerable = w[8] != 0; be.LastDamageTime = F(10);
            be.JetPart!.HandleSkimming();
            string line = "Skim " + string.Join(" ", new[]
            {
                be.Velocity.X, be.Velocity.Y, be.Velocity.Z, be.Pitchvel,
                be.Life, be.Energy, be.Shields, be.LastDamageTime, be.LastTimeInHostileEnvironment,
                be.AugValue, be.Vibration, be.YawShake, be.PitchShake, be.RollShake, be.ShakeR,
            }.Select(RetailOracle.H)) + $" {unchecked((uint)Assert.IsType<Player>(be.Player).Stats[(int)PlayerStat.DamageTaken]):x8}"
                + $" {unchecked((uint)be.Context.Random.Value):x8}";
            Assert.True(line == expected, $"case {actual.Count}:\nours: {line}\nretail: {expected}");
            actual.Add(line);
        }
        Assert.Equal(4151, actual.Count);
        RetailOracle.WriteOurs("flight-skim", actual.ToArray());
    }

    [Fact]
    public void SkimmingBypassesShieldsAndHostileWarningUsesStrictFiveSecondQuietPeriod()
    {
        BattleEngine be = Engine(12345, -1);
        be.Pos = new FVector(0, 0, -1);
        be.Shields = 200;
        be.LastTimeInHostileEnvironment = -4;
        be.Context.SoundEffects = SoundEffects.Load(
            "103\n1\nhud\\hud_hostile_environment\nfixture-sample\nfixture-lowpass\n100\n0\n0\n0\n0\n0\n"u8.ToArray());
        void Skim()
        {
            be.Velocity = new FVector(.6f, .8f, .25f);
            be.JetPart!.HandleSkimming();
        }
        Skim();
        Assert.Equal(90, be.Life); Assert.Equal(200, be.Shields); Assert.Equal(15, be.Energy);
        Assert.Equal(2560, Assert.IsType<Player>(be.Player).Stats[(int)PlayerStat.DamageTaken]);
        Assert.Equal(2, be.LastDamageTime); Assert.Equal(2, be.LastTimeInHostileEnvironment);
        Assert.Equal(1, be.Context.Crt.Calls);
        Skim(); Assert.Equal(80, be.Life); Assert.Equal(1, be.Context.Crt.Calls);
        for (int i = 0; i < 100; i++) be.Context.Events.AdvanceTime();
        Assert.Equal(7, be.Context.Events.Time);
        Skim(); Assert.Equal(1, be.Context.Crt.Calls); // exactly five seconds is not enough
        for (int i = 0; i < 101; i++) be.Context.Events.AdvanceTime();
        Skim(); Assert.Equal(2, be.Context.Crt.Calls);
        Assert.Equal(be.Context.Events.Time, be.LastTimeInHostileEnvironment);
    }
}
