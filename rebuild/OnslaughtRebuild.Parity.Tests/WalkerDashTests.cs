// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// Forward 00412d80, Backward 00412f70, StrafeLeft 00413160, StrafeRight 00413360, Move 00413760.
// Matched source: onslaught-decomp src/BattleEngineWalkerPart.cpp. The optional oracle executes those directional
// routines and native IsOnGround/AddVelocity/GetCurrentWeapon/FMatrix; logging and sound playback are intercepted.
// Synthetic unit fixtures are not tutorial acceptance. The final four tests use ordinary keys in a real session.

using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class WalkerDashTests
{
    private static BattleEngine NewEngine(int frame = 320)
    {
        var pool = new SPtrSetPool();
        pool.Init(64);
        var events = new EventManager();
        for (int i = 0; i < frame; i++) events.AdvanceTime();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, null!)
        {
            SoundEffects = SoundEffects.Load(Encoding.Latin1.GetBytes(
                "103\n1\nBE Strafe L/R\nsample.wav\n0\n0\n0\n0\n0\n0\n0\n")),
        };
        var engine = new BattleEngine(context)
        {
            Configuration = new BattleEngineData { GroundVelocity = 4.37f },
            State = BattleEngineState.Walker,
            LastTimeOnGround = events.Time,
            ZoomOutTime = 9.3f,
            Rollvel = 0.123f,
            Velocity = new FVector(0.13f, -0.24f, 0.017f),
        };
        engine.CurrentOrientation.Yaw = 0.731f;
        engine.WalkerPart = new BattleEngineWalkerPart(engine);
        engine.WalkerPart.PrimaryWeapon = new Weapon(context, new WeaponData("fixture-primary"u8.ToArray()), 0) { ChargeValue = 0.8f };
        engine.WalkerPart.AugWeapon = new Weapon(context, new WeaponData("fixture-aug"u8.ToArray()), 0) { ChargeValue = 0.8f };
        return engine;
    }

    private static float Sign(int direction) => direction is 0 or 2 ? -1.0f : 1.0f;

    private static void OppositeTime(BattleEngineWalkerPart w, int direction, float time)
    {
        switch (direction)
        {
            case 0: w.LastStartHardBackwardTime = time; break;
            case 1: w.LastStartHardForwardTime = time; break;
            case 2: w.LastStartHardRightTime = time; break;
            case 3: w.LastStartHardLeftTime = time; break;
        }
    }

    private static float OwnTime(BattleEngineWalkerPart w, int direction) => direction switch
    {
        0 => w.LastStartHardForwardTime, 1 => w.LastStartHardBackwardTime,
        2 => w.LastStartHardLeftTime, _ => w.LastStartHardRightTime,
    };

    private static float OppositeTime(BattleEngineWalkerPart w, int direction) => OwnTime(w, direction ^ 1);

    private static void Invoke(BattleEngineWalkerPart w, int direction, float value)
    {
        switch (direction)
        {
            case 0: w.Forward(value); break;
            case 1: w.Backward(value); break;
            case 2: w.StrafeLeft(value); break;
            case 3: w.StrafeRight(value); break;
        }
    }

    [RetailOracleFact("walker-dash")]
    public void FourCompleteDirectionalCallsAgreeWithOriginalCode()
    {
        var ours = new List<string>();
        foreach ((string name, uint[] w, string expected) in RetailOracle.Cases("walker-dash"))
        {
            Assert.Equal("Dash", name);
            Assert.Equal(17, w.Length);
            int direction = checked((int)w[0]);
            BattleEngine be = NewEngine((int)Math.Round(RetailOracle.F(w[7]) / 0.05f));
            Assert.Equal(w[7], BitConverter.SingleToUInt32Bits(be.Context.Events.Time));
            BattleEngineWalkerPart walker = be.WalkerPart!;
            be.CurrentOrientation.Yaw = RetailOracle.F(w[2]);
            be.Configuration!.GroundVelocity = RetailOracle.F(w[3]);
            be.Velocity = RetailOracle.V(w, 4);
            be.LastTimeOnGround = RetailOracle.F(w[8]);
            if (direction < 2) walker.LastMoveYVal = RetailOracle.F(w[9]);
            else walker.LastMoveXVal = RetailOracle.F(w[9]);
            OppositeTime(walker, direction, RetailOracle.F(w[10]));
            walker.DoingDashCount = unchecked((int)w[11]);
            be.Rollvel = RetailOracle.F(w[12]); be.ZoomOutTime = RetailOracle.F(w[13]);
            Weapon primary = walker.PrimaryWeapon!, aug = walker.AugWeapon!;
            primary.ChargeValue = aug.ChargeValue = RetailOracle.F(w[14]);
            be.SlowMovement = w[15] != 0;
            walker.PrimaryWeapon = w[16] != 0 ? primary : null;
            walker.AugWeapon = w[16] == 2 ? aug : null;
            be.AugActive = w[16] == 2;
            Invoke(walker, direction, RetailOracle.F(w[1]));
            string state = $"Dash {RetailOracle.Hv(be.Velocity)} {RetailOracle.H(be.Rollvel)} {RetailOracle.H(be.ZoomOutTime)} "
                + $"{RetailOracle.H(primary.ChargeValue)} {RetailOracle.H(aug.ChargeValue)} {unchecked((uint)walker.DoingDashCount):x8} "
                + $"{RetailOracle.H(direction < 2 ? walker.LastMoveYVal : walker.LastMoveXVal)} {RetailOracle.H(OwnTime(walker, direction))} "
                + RetailOracle.H(OppositeTime(walker, direction));
            string[] fields = expected.Split(' ');
            Assert.Equal(14, fields.Length);
            Assert.Equal(string.Join(' ', fields.Take(12)), state);
            // Native hook validates the real effect's arguments and call ordering; this authored single-effect
            // owner must consume one CRT draw for each call. Native debug logging is not modelled or compared.
            Assert.Equal(Convert.ToInt64(fields[13], 16), be.Context.Crt.Calls);
            ours.Add($"{state} {be.Context.Crt.Calls:x8}");
        }
        Assert.True(ours.Count >= 1800);
        RetailOracle.WriteOurs("walker-dash", ours.ToArray());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void StrictTimingAndInputEdgesDoNotBecomeInclusive(int direction)
    {
        foreach ((float age, bool admitted) in new[] { (0.1f, false), (0.15f, true), (0.2f, false) })
        {
            BattleEngine be = NewEngine();
            OppositeTime(be.WalkerPart!, direction, be.Context.Events.Time - age);
            Invoke(be.WalkerPart!, direction, Sign(direction));
            Assert.Equal(admitted ? 15 : 0, be.WalkerPart!.DoingDashCount);
            Assert.Equal(admitted ? 1 : 0, be.Context.Crt.Calls);
        }

        foreach (bool held in new[] { false, true })
        {
            BattleEngine be = NewEngine();
            BattleEngineWalkerPart w = be.WalkerPart!;
            OppositeTime(w, direction, be.Context.Events.Time - 0.15f);
            if (direction < 2) w.LastMoveYVal = held ? Sign(direction) : 0;
            else w.LastMoveXVal = held ? Sign(direction) : 0;
            Invoke(w, direction, Sign(direction) * (held ? 1 : 0.8f));
            Assert.Equal(0, w.DoingDashCount);
            Assert.Equal(0, be.Context.Crt.Calls);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void DashClearsSelectedChargeAndZoomOnceAndPreservesDirectionalRoll(int direction)
    {
        BattleEngine be = NewEngine();
        BattleEngineWalkerPart w = be.WalkerPart!;
        be.AugActive = true;
        OppositeTime(w, direction, be.Context.Events.Time - 0.15f);
        Invoke(w, direction, Sign(direction));
        Assert.Equal(15, w.DoingDashCount);
        Assert.Equal(0, w.AugWeapon!.ChargeValue);
        Assert.Equal(0.8f, w.PrimaryWeapon!.ChargeValue);
        Assert.Equal(0, be.ZoomOutTime);
        Assert.Equal(direction switch { 2 => 0.08f, 3 => 0.123f - 0.08f, _ => 0.123f }, be.Rollvel);
        Assert.Equal(1, be.Context.Crt.Calls);
        FVector after = be.Velocity;
        w.AugWeapon.ChargeValue = 0.6f;
        Invoke(w, direction, Sign(direction));
        Assert.Equal(after, be.Velocity);
        Assert.Equal(0.6f, w.AugWeapon.ChargeValue);
        Assert.Equal(15, w.DoingDashCount);
        Assert.Equal(1, be.Context.Crt.Calls);
    }

    [Theory]
    [InlineData(15, 14, false)]
    [InlineData(5, 4, false)]
    [InlineData(4, 3, true)]
    [InlineData(1, 0, true)]
    public void MovementAppliesFrictionAndTestsTheCapBeforeDecrementingTheDash(int count, int after, bool capped)
    {
        // Authored airborne unit fixture: the existing Move permits dash friction off the ground. No map queries,
        // stepping, water or slide branches are reached. Native 00413760 applies friction, tests count < 5, then
        // decrements; moving that decrement earlier incorrectly caps the count-5 boundary.
        BattleEngine be = NewEngine();
        be.LastTimeOnGround = be.Context.Events.Time - 1.0f;
        be.Configuration!.WalkFriction = 0.5f;
        be.Configuration.MaxWalkVelocity = 0.25f;
        be.Velocity = new FVector(3, 4, 2);
        be.WalkerPart!.DoingDashCount = count;
        be.WalkerPart.Move();
        Assert.Equal(after, be.WalkerPart.DoingDashCount);
        Assert.Equal(capped ? new FVector(0.15f, 0.2f, 1) : new FVector(1.5f, 2, 1), be.Velocity);

        be.WalkerPart.DoingDashCount = 0;
        FVector before = be.Velocity;
        be.WalkerPart.Move();
        Assert.Equal(before, be.Velocity); // The completed dash cannot keep applying airborne walking friction.
    }

    [GameDataFact] public void OrdinaryBackwardThenForwardKeysDodge() => OrdinaryKeys(0, 'S', 0x1f, 'W', 0x11);
    [GameDataFact] public void OrdinaryForwardThenBackwardKeysDodge() => OrdinaryKeys(1, 'W', 0x11, 'S', 0x1f);
    [GameDataFact] public void OrdinaryRightThenLeftKeysDodge() => OrdinaryKeys(2, 'D', 0x20, 'A', 0x1e);
    [GameDataFact] public void OrdinaryLeftThenRightKeysDodge() => OrdinaryKeys(3, 'A', 0x1e, 'D', 0x20);

    private static void OrdinaryKeys(int direction, int firstVk, int firstScan, int secondVk, int secondScan)
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        while (session.PresentCount < 3300) session.AdvanceToNextPresent();
        BattleEngine be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        BattleEngineWalkerPart w = be.WalkerPart!;
        Assert.Equal(GameState.Playing, session.Game.State);
        Assert.True(be.IsOnGround());
        Assert.Equal(0, w.DoingDashCount);
        session.Input.Input.PostTapeKey(firstVk, firstScan, true);
        long deadline = session.PresentCount + 20;
        while (OppositeTime(w, direction) < 0 && session.PresentCount < deadline) session.AdvanceToNextPresent();
        float start = OppositeTime(w, direction);
        Assert.True(start >= 0);
        session.Input.Input.PostTapeKey(firstVk, firstScan, false);
        // Wait two simulation ticks, then the opposite key is consumed on the next update (the 0.1..0.2s window).
        int firstFrame = session.Context.Events.FrameCount;
        while (session.Context.Events.FrameCount < firstFrame + 2) session.AdvanceToNextPresent();
        session.Input.Input.PostTapeKey(secondVk, secondScan, true);
        while (w.DoingDashCount == 0 && session.PresentCount < deadline) session.AdvanceToNextPresent();
        Assert.InRange(w.DoingDashCount, 14, 15);
        Assert.Equal(0, be.ZoomOutTime);
        int count = w.DoingDashCount;
        int frame = session.Context.Events.FrameCount;
        while (w.DoingDashCount > 0 && session.PresentCount < deadline + 100)
        {
            session.AdvanceToNextPresent();
            if (session.Context.Events.FrameCount == frame) continue;
            Assert.Equal(count - 1, w.DoingDashCount);
            count = w.DoingDashCount;
            frame = session.Context.Events.FrameCount;
        }
        Assert.Equal(0, w.DoingDashCount);
        int endFrame = session.Context.Events.FrameCount;
        while (session.Context.Events.FrameCount < endFrame + 3)
        {
            session.AdvanceToNextPresent();
            Assert.Equal(0, w.DoingDashCount); // A held key cannot re-use the old opposite-direction edge.
        }
        session.Input.Input.PostTapeKey(secondVk, secondScan, false);
    }
}
