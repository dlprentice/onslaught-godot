// SPDX-License-Identifier: GPL-3.0-only
// Damage0040a890 and shake00407940/004247a0, pristine PC SHA-256
// 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750; matched onslaught-decomp BattleEngine.cpp.
// The private oracle executes unchanged original bodies on authored objects. Death and CRT TLS are intercepted;
// the main damage fixture has null attackers; the flash fixture intercepts position/allocation/list boundaries.
// These are isolated arithmetic/state checks, not gameplay acceptance.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineDamageTests
{
    [GameDataFact]
    public void ScheduledMovementExpiresDamageFlashesWithoutManualLifetimeUpdates()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        while (session.PresentCount < 3000) session.AdvanceToNextPresent();
        BattleEngine be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        var attacker = new BattleEngine(session.Context) { Pos = be.Pos + new FVector(5, 0, 0) };
        be.Damage(.25f, attacker);
        float started = Assert.Single(be.DamageFlashes).StartTime;
        while (session.Context.Events.Time <= started + 2.0f) session.AdvanceToNextPresent();
        // The first Present can precede Move's next tick; let the actual scheduler deliver it.
        for (int i = 0; i < 4 && be.DamageFlashes.Count != 0; i++) session.AdvanceToNextPresent();
        Assert.Empty(be.DamageFlashes);
        Assert.True(be.Life > 0);
    }

    private static BattleEngine Engine(int seed = 12345, int frames = 840)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager();
        for (int n = 0; n < frames; n++) events.AdvanceTime();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(seed), null!, new PhysicsRegistry());
        var be = new BattleEngine(context)
        {
            Configuration = new BattleEngineData { Life = 20, Energy = 12, ShieldEfficiency = 50 },
            State = BattleEngineState.MorphingIntoWalker,
            Life = 10,
            Energy = 5,
            Shields = 4,
            LastDamageTime = 3,
            AugValue = 2,
            Vibration = 1,
        };
        var player = new Player(1, context); player.Stats[(int)PlayerStat.DamageTaken] = 17; be.Player = player;
        be.WalkerPart = new BattleEngineWalkerPart(be);
        be.WalkerPart.AugWeapon = new Weapon(context, new WeaponData("fixture-aug"u8.ToArray()), 0);
        return be;
    }

    [RetailOracleFact("be-damage")]
    public void DamageAndBothShakeStreamsMatchOriginalCodeAtRetailPrecision()
    {
        var lines = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("be-damage"))
        {
            Assert.Equal("Damage", function); Assert.Equal(25, w.Length);
            float F(int i) => RetailOracle.F(w[i]);
            BattleEngine be = Engine(unchecked((int)w[15]));
            be.Life = F(1); be.Energy = F(2); be.Shields = F(3); be.AugValue = F(4); be.Vibration = F(5); be.LastDamageTime = F(6);
            be.State = (BattleEngineState)w[7]; be.Vulnerable = w[8] != 0; be.AugActive = w[9] != 0;
            if (w[10] == 0) be.WalkerPart!.AugWeapon = null;
            be.Configuration!.Life = F(11); be.Configuration.Energy = F(12); be.Configuration.ShieldEfficiency = F(13);
            var player = Assert.IsType<Player>(be.Player); player.Stats[(int)PlayerStat.DamageTaken] = unchecked((int)w[14]);
            Assert.Equal(F(16), be.Context.Events.Time); be.Context.Crt.Srand(w[20]);
            be.YawShake = F(21); be.PitchShake = F(22); be.RollShake = F(23); be.ShakeR = F(24);
            if (w[19] != 0)
            {
                be.Cockpit = new Cockpit(be.Context, be, null);
                for (int n = 0; n < 4; n++) be.Cockpit.Shake[n] = 10 + n;
                be.Cockpit.ShakeTime = 14;
            }
            be.Damage(F(0), null, w[17] != 0, unchecked((int)w[18]));
            string line = "Damage " + string.Join(" ", new[]{be.Life,be.Energy,be.Shields,be.LastDamageTime,be.AugValue,
                be.YawShake,be.PitchShake,be.RollShake,be.ShakeR,be.Vibration}.Select(RetailOracle.H))
                + $" {unchecked((uint)player.Stats[(int)PlayerStat.DamageTaken]):x8} {unchecked((uint)be.Context.Random.Value):x8}"
                + $" {be.Context.Crt.HoldRand:x8} {be.Context.Crt.Calls:x8} "
                + string.Join(" ", (be.Cockpit?.Shake ?? [10f, 11f, 12f, 13f]).Append(be.Cockpit?.ShakeTime ?? 14f).Select(RetailOracle.H));
            lines.Add(line);
            Assert.True(line == expected, $"case {lines.Count - 1}:\nours {line}\nretail {expected}");
        }
        Assert.Equal(1035, lines.Count);
        RetailOracle.WriteOurs("be-damage", lines.ToArray());
    }

    [RetailOracleFact("be-damage-flash")]
    public void DamageFlashAngleCapAndAdmissionMatchOriginalCode()
    {
        // Native Damage and FPATAN execute unchanged. GetOldPos supplies the authored position;
        // allocation supplies one 8-byte object, and list Add validates/increments count. This
        // proves the angle/admission arithmetic, not allocator or list lifetime equivalence.
        var lines = new List<string>();
        foreach ((string function, uint[] words, string expected) in RetailOracle.Cases("be-damage-flash"))
        {
            Assert.Equal("Flash", function);
            Assert.Equal(9, words.Length);
            float F(int index) => RetailOracle.F(words[index]);
            BattleEngine be = Engine();
            be.OldPos = new FVector(F(0), F(1), 0);
            be.CurrentOrientation.Yaw = F(4);
            be.Vulnerable = words[8] != 0;
            for (uint i = 0; i < words[5]; i++)
                be.AddDamageFlash(new FVector(F(0), F(1) + 1, 0));
            int initialCount = be.DamageFlashes.Count;
            Thing? attacker = words[7] == 0 ? null
                : new BattleEngine(be.Context) { Pos = new FVector(F(2), F(3), 0) };
            be.Damage(F(6), attacker);
            bool added = be.DamageFlashes.Count != initialCount;
            BattleEngine.DamageFlash? flash = added ? be.DamageFlashes[0] : null;
            float yaw = flash?.Yaw ?? 0, time = flash?.StartTime ?? 0;
            string line = $"Flash {be.DamageFlashes.Count:x8} {(added ? 1 : 0):x8} "
                + string.Join(" ", new[] { yaw, time, be.LastDamageTime, be.Life, be.Shields, be.Vibration }
                    .Select(RetailOracle.H));
            lines.Add(line);
            Assert.True(line == expected, $"case {lines.Count - 1}:\nours {line}\nretail {expected}");
        }
        Assert.Equal(1006, lines.Count);
        RetailOracle.WriteOurs("be-damage-flash", lines.ToArray());
    }

    [Theory]
    [InlineData(false, 10f, 4f, 5f)]
    [InlineData(true, 8f, 0f, 0f)]
    public void InvulnerabilityRestoresOnlyHealthAfterSharedSideEffects(bool vulnerable, float life, float shields, float energy)
    {
        BattleEngine be = Engine(); be.State = BattleEngineState.Walker; be.Vulnerable = vulnerable;
        be.Cockpit = new Cockpit(be.Context, be, null);
        be.Damage(6, null);
        Assert.Equal(life, be.Life); Assert.Equal(shields, be.Shields); Assert.Equal(energy, be.Energy);
        Assert.Equal(6, be.AugValue); Assert.Equal(42, be.LastDamageTime); Assert.Equal(3.5f, be.Vibration);
        Assert.Equal(1553, Assert.IsType<Player>(be.Player).Stats[(int)PlayerStat.DamageTaken]);
        Assert.Equal(3, be.Context.Random.Draws); Assert.Equal(4, be.Context.Crt.Calls);
    }

    [Theory]
    [InlineData(0f, 10f, 4f, 1f)]
    [InlineData(-3f, 13f, 4f, -8.375f)]
    [InlineData(-12f, 20f, 6f, -30.25f)]
    public void CommonTailMirrorsWalkerEnergyBeforeRepair(float amount, float life, float energy, float vibration)
    {
        BattleEngine be = Engine(); be.State = BattleEngineState.Walker; be.AugValue = 12;
        be.Damage(amount, null);
        Assert.Equal(life, be.Life); Assert.Equal(energy, be.Energy); Assert.Equal(vibration, be.Vibration);
        Assert.Equal(10, be.AugValue); Assert.Equal(4, be.Shields); Assert.Equal(3, be.LastDamageTime);
        Assert.Equal(0, be.Context.Random.Draws); Assert.Equal(0, be.Context.Crt.Calls);
    }

    [Fact]
    public void ShieldEfficiencyAndExhaustionHaveDifferentAugWeaponGuards()
    {
        BattleEngine be = Engine(); be.WalkerPart!.AugWeapon = null; be.Damage(2, null);
        Assert.Equal(2, be.AugValue); Assert.Equal(9, be.Life); Assert.Equal(3, be.Shields);
        be = Engine(); be.WalkerPart!.AugWeapon = null; be.Damage(6, null);
        Assert.Equal(6, be.AugValue); Assert.Equal(8, be.Life); Assert.Equal(0, be.Shields);
    }

    [Fact]
    public void LethalDamageWithoutGameRefusesBeforeTimestampOrCommonTail()
    {
        BattleEngine be = Engine(); be.State = BattleEngineState.Walker; be.AugValue = 9;
        Assert.Contains("requires its controlling player and game", Assert.Throws<NotSupportedException>(() => be.Damage(30, null)).Message);
        Assert.Equal(-16, be.Life); Assert.Equal(0, be.Shields); Assert.Equal(5, be.Energy);
        Assert.Equal(13, be.AugValue); Assert.Equal(3, be.LastDamageTime); Assert.Equal(1, be.Vibration);
        Assert.Equal(0, be.Context.Random.Draws);
    }

    [Fact]
    public void DamageFlashUsesOldPositionAndDoesNotVanishForAnInvulnerablePlayer()
    {
        BattleEngine be = Engine(); be.Vulnerable = false; be.OldPos = new FVector(1, 2, 3); be.Pos = new FVector(90, 80, 70);
        be.CurrentOrientation.Yaw = .5f;
        var attacker = new BattleEngine(be.Context) { Pos = new FVector(1, 3, 900) };
        be.Damage(2, attacker);
        BattleEngine.DamageFlash flash = Assert.Single(be.DamageFlashes);
        Assert.Equal(.5f, flash.Yaw); Assert.Equal(42, flash.StartTime); Assert.Equal(10, be.Life);
        be.Damage(0, attacker); be.Damage(-1, attacker); Assert.Single(be.DamageFlashes);
    }

    [Fact]
    public void FlashCapAndStrictExpiryRemoveOnlyOneExpiredEntryPerMovement()
    {
        BattleEngine be = Engine(frames: 0);
        for (int i = 0; i < 16; i++) { be.CurrentOrientation.Yaw = i; be.AddDamageFlash(new FVector(0, 1, 0)); }
        Assert.Equal(15, be.DamageFlashes.Count); Assert.Equal(14, be.DamageFlashes[0].Yaw);
        for (int i = 0; i < 40; i++) be.Context.Events.AdvanceTime();
        be.ProcessDamageFlashes(); Assert.Equal(15, be.DamageFlashes.Count);
        be.Context.Events.AdvanceTime(); be.ProcessDamageFlashes(); Assert.Equal(14, be.DamageFlashes.Count);
        Assert.Equal(13, be.DamageFlashes[0].Yaw);
        be.ProcessDamageFlashes(); Assert.Equal(13, be.DamageFlashes.Count);
    }
}
