// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineFlightTests
{
    private static BattleEngine Engine()
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        for (int n = 0; n < 40; n++) events.AdvanceTime();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        return new BattleEngine(context) { Configuration = new BattleEngineData(), LastTimeOnGround = -100 };
    }

    [RetailOracleFact("flight-controls")]
    public void ControlsMatchOriginalCodeForThresholdsModesAndFloatOrders()
    {
        int count = 0;
        var actual = new List<string>();
        BattleEngine be = Engine();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("flight-controls"))
        {
            float F(int i) => RetailOracle.F(w[i]);
            be.Energy = F(1); be.Zoom = F(2); be.Configuration!.AirTurnRate = F(3); be.TransformStartTime = F(4);
            be.SlowMovement = w[5] != 0; be.Velocity = RetailOracle.V(w,6); be.LastTimeOnGround = w[9] != 0 ? 2.0f : -100.0f;
            be.Yawvel=F(10);be.Rollvel=F(11);be.Pitchvel=F(12);
            var jet = new BattleEngineJetPart(be) { ThrusterValue=F(13), LastMoveYVal=F(14), LastStartHardForwardTime=F(15),
                DoingBarrelCount=F(16),DoingLoop=w[17]!=0,LoopHalfway=w[18]!=0,LoopBroken=w[19]!=0 };
            uint result = 0;
            switch(function)
            {
                case "Thrust": jet.Thrust(F(0)); break;
                case "Turn": jet.Turn(F(0)); break;
                case "Pitch": jet.Pitch(F(0)); break;
                case "AutoLevel": result=jet.AutoLevel()?1u:0u;break;
                case "Gravity": result=BitConverter.SingleToUInt32Bits(jet.Gravity());break;
                default: Assert.Fail($"Unknown original-code case {function}");break;
            }
            string line=$"{function} {RetailOracle.H(be.Yawvel)} {RetailOracle.H(be.Rollvel)} {RetailOracle.H(be.Pitchvel)} "
                +$"{RetailOracle.H(jet.ThrusterValue)} {RetailOracle.H(jet.LastMoveYVal)} {RetailOracle.H(jet.LastStartHardForwardTime)} "
                +$"{(jet.DoingLoop?1:0):x8} {(jet.LoopHalfway?1:0):x8} {(jet.LoopBroken?1:0):x8} {result:x8}";
            actual.Add(line);
            Assert.True(line==expected,$"case {count}: {line}\nretail: {expected}");
            count++;
        }
        RetailOracle.WriteOurs("flight-controls", actual.ToArray());
        Assert.Equal(10000,count);
    }

    [Fact]
    public void LoopStartsOnlyInsideTheStrictDoubleTapWindowAndBarrelSuppressesLastInput()
    {
        BattleEngine be = Engine();be.Energy=8;be.Velocity=new FVector(.4f,0,0);
        var jet=new BattleEngineJetPart(be);
        jet.LastStartHardForwardTime=be.Context.Events.Time-.2f;
        jet.Thrust(1);Assert.False(jet.DoingLoop);
        jet.LastStartHardForwardTime=be.Context.Events.Time-.1f;
        jet.Thrust(1);Assert.False(jet.DoingLoop);
        jet.LastStartHardForwardTime=1.85f;jet.Thrust(1);
        Assert.True(jet.DoingLoop);Assert.Equal(-.015f,be.Pitchvel);
        jet.DoingBarrelCount=13;jet.LastMoveYVal=.25f;jet.Thrust(-1);
        Assert.Equal(.25f,jet.LastMoveYVal);
    }
    [GameDataFact]
    public void MorphPermissionEnergyAndSpecialMoveGuardsPreserveTheirOrdering()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        BattleEngine be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        be.FlightModeActive = false;
        be.SlowMovement = true;
        be.DesiredZoom = .4f;
        be.Morph();
        Assert.True(be.SlowMovement); // permission guard precedes the clear
        Assert.Equal(.4f, be.DesiredZoom);
        Assert.Equal(BattleEngineState.Walker, be.State);
        be.EnableFlightMode();
        be.WalkerPart!.DoingDashCount = 1;
        be.Morph();
        Assert.False(be.SlowMovement);
        Assert.Equal(.4f, be.DesiredZoom); // special move guard precedes charge/zoom
        be.WalkerPart.DoingDashCount = 0;
        be.Energy = be.Configuration!.MinTransformEnergy - .01f;
        be.Morph();
        Assert.Equal(1.0f, be.DesiredZoom); // failed energy test still clears the zoom
        Assert.Equal(BattleEngineState.Walker, be.State);
    }

    [GameDataFact]
    public void MorphRetainsMeshesAndControllerAcrossBothStateTransitions()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        BattleEngine be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        var walkerMesh = be.RenderThing;
        var jetMesh = be.OtherMeshRenderThing;
        MCBattleEngine controller = be.MotionController!;
        Assert.NotNull(controller);
        be.EnableFlightMode();
        be.Energy = be.Configuration!.Energy;
        be.LastTimeOnGround = session.Context.Events.Time;
        uint before = session.Context.Crt.HoldRand;
        be.Morph();
        Assert.Equal(BattleEngineState.MorphingIntoJet, be.State);
        Assert.Same(jetMesh, be.RenderThing);
        Assert.Null(be.MotionController);
        Assert.False(controller.IsAttached);
        int processed = controller.ProcessCount;
        controller.ProcessMovement(true);
        Assert.Equal(processed, controller.ProcessCount);
        Assert.Equal(2, be.Cockpit!.MorphState);
        Assert.NotEqual(before, session.Context.Crt.HoldRand); // takeoff and repeating-engine selection are real draws
        Assert.Single(session.Context.SoundEffects!.RepeatingEffects);
        be.HandleEvent(new Event(BattleEngineEvent.BecomeJet));
        Assert.Equal(BattleEngineState.Jet, be.State);
        Assert.Equal(session.Context.Events.Time, be.TransformStartTime);
        session.Context.Events.AdvanceTime();
        be.HandleEvent(new Event(BattleEngineEvent.BecomeJet));
        Assert.NotEqual(session.Context.Events.Time, be.TransformStartTime); // duplicate event is guarded
        Assert.NotNull(be.GetCurrentWeapon()); // scheduled crosshair/aim events can resolve the real jet weapon
        be.Morph();
        Assert.Equal(BattleEngineState.MorphingIntoWalker, be.State);
        Assert.Same(jetMesh, be.RenderThing);
        Assert.Equal(1, be.Cockpit.MorphState);
        be.HandleEvent(new Event(BattleEngineEvent.BecomeWalker));
        Assert.Equal(BattleEngineState.Walker, be.State);
        be.PutUsInRightMesh();
        Assert.Same(walkerMesh, be.RenderThing);
        Assert.Same(controller, be.MotionController);
        Assert.True(controller.IsAttached);
        Assert.True(be.RenderThingIsWalker);
    }

    [Fact]
    public void RepeatingSoundFadeNeedsStrictOvershootAndDoesNotAdvanceWhenPaused()
    {
        var sounds = OnslaughtRebuild.Parity.Sound.SoundEffects.Load("100\n0\n"u8.ToArray());
        var effect = new OnslaughtRebuild.Parity.Sound.SoundEffect("effect", 0, "same-sample");
        var alias = new OnslaughtRebuild.Parity.Sound.SoundEffect("alias", 0, "SAME-SAMPLE");
        object owner = new();
        var rng = new OnslaughtRebuild.Parity.Crt.CrtRandStream();
        sounds.PlayRepeatingEffect(effect, owner, rng);
        Assert.True(sounds.IsRepeatingEffectPlaying(alias, owner));
        Assert.False(sounds.IsRepeatingEffectPlaying(alias, new object()));
        sounds.FadeRepeatingSample(effect, owner, 0, .5f);
        sounds.AdvanceLoopStatus(false);
        Assert.Equal(.5f, sounds.RepeatingEffects[0].SubVolume);
        sounds.AdvanceLoopStatus(true);
        Assert.Equal(.5f, sounds.RepeatingEffects[0].SubVolume);
        sounds.AdvanceLoopStatus(false);
        Assert.Equal(0.0f, sounds.RepeatingEffects[0].SubVolume);
        Assert.True(sounds.IsRepeatingEffectPlaying(effect, owner));
        sounds.AdvanceLoopStatus(false);
        Assert.False(sounds.IsRepeatingEffectPlaying(effect, owner));
    }

}
