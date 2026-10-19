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
    private static BattleEngine Engine(OnslaughtRebuild.Parity.World.HeightField? map = null)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        for (int n = 0; n < 40; n++) events.AdvanceTime();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), map!, new PhysicsRegistry());
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

    [RetailOracleFact("flight-yaw")]
    public void StrafeAndBarrelControlsMatchOriginalCodeIncludingAsymmetricLoopBreak()
    {
        BattleEngine be = Engine();
        var actual = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("flight-yaw"))
        {
            float F(int n) => RetailOracle.F(w[n]);
            be.Energy = F(1); be.Velocity = RetailOracle.V(w, 2);
            be.Orientation = new FMatrix(RetailOracle.V(w, 5), RetailOracle.V(w, 8), RetailOracle.V(w, 11));
            be.CurrentOrientation = new EulerAngles(F(14), 0, 0); be.LowEnergyStartTime = F(15);
            var jet = new BattleEngineJetPart(be) { LastMoveXVal=F(16), LastStartHardLeftTime=F(17),
                LastStartHardRightTime=F(18), DoingBarrelCount=F(19), DoingBarrelLeft=w[20]!=0,
                DoingLoop=w[21]!=0, LoopBroken=w[22]!=0 };
            if (function == "YawLeft") jet.YawLeft(F(0));
            else if (function == "YawRight") jet.YawRight(F(0));
            else Assert.Fail($"Unknown yaw case {function}");
            string line = $"{function} {RetailOracle.H(be.Velocity.X)} {RetailOracle.H(be.Velocity.Y)} {RetailOracle.H(be.Velocity.Z)} "
                + $"{RetailOracle.H(be.LowEnergyStartTime)} {RetailOracle.H(jet.LastMoveXVal)} {RetailOracle.H(jet.LastStartHardLeftTime)} "
                + $"{RetailOracle.H(jet.LastStartHardRightTime)} {RetailOracle.H(jet.DoingBarrelCount)} {(jet.DoingBarrelLeft?1:0):x8} "
                + $"{RetailOracle.H(jet.StrafingStartTime)} {(jet.LoopBroken?1:0):x8}";
            Assert.True(line == expected, $"case {actual.Count}: {line}\nretail: {expected}");
            actual.Add(line);
        }
        RetailOracle.WriteOurs("flight-yaw", actual.ToArray());
        Assert.Equal(5000, actual.Count);
    }

    private static OnslaughtRebuild.Parity.World.HeightField FlatMap()
    {
        var raw = new byte[24 + OnslaughtRebuild.Parity.World.HeightField.RecordSize
            + OnslaughtRebuild.Parity.World.HeightField.SampleCount * 2];
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x102c), 1.0f);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x1034), 100.0f);
        return OnslaughtRebuild.Parity.World.HeightField.Load(new OnslaughtRebuild.Parity.Data.ChunkReader(
            OnslaughtRebuild.Parity.Data.MemBuffer.FromFile("flat-map", raw)));
    }

    [RetailOracleFact("flight-move")]
    public void PoweredMoveGroundEffectAndFrictionMatchOriginalCodeOnFlatTerrain()
    {
        var map = FlatMap();
        BattleEngine be = Engine(map);
        var actual = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("flight-move"))
        {
            float F(int n) => RetailOracle.F(w[n]);
            be.Energy=F(0);be.InfinateEnergy=w[1]!=0;be.TransformStartTime=F(3);be.Stalling=w[4]!=0;be.StallTime=F(5);
            be.LastTimeOnGround=w[6]!=0?2:-100;be.Pos=RetailOracle.V(w,7);be.Velocity=RetailOracle.V(w,10);
            be.Orientation=new FMatrix(RetailOracle.V(w,13),RetailOracle.V(w,16),RetailOracle.V(w,19));
            be.CurrentOrientation=new EulerAngles(F(22),F(23),F(24));be.Pitchvel=F(25);be.Rollvel=F(26);be.Yawvel=F(27);
            be.EngineState=0;be.State=BattleEngineState.MorphingIntoJet; // Any requested Morph returns at its transition guard.
            be.Configuration!.MinAirEnergyCost=F(35);be.Configuration.MaxAirEnergyCost=F(36);
            be.Configuration.MinAirVelocity=F(37);be.Configuration.MaxAirVelocity=F(38);
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(map.Record.AsSpan(0x1034),F(39));
            var jet=new BattleEngineJetPart(be){ThrusterValue=F(2),OnGround=F(28),DoingLoop=w[29]!=0,LoopHalfway=w[30]!=0,
                LoopBroken=w[31]!=0,DoingBarrelCount=F(32),DoingBarrelLeft=w[33]!=0,StrafingStartTime=F(34)};
            be.JetPart=jet;
            uint result=0;
            if(function=="Move")jet.Move();
            else if(function=="Ground")jet.HandleGroundEffect();
            else if(function=="Friction")result=BitConverter.SingleToUInt32Bits(jet.GetFriction());
            else Assert.Fail($"Unknown movement case {function}");
            float[] floats=[be.Energy,0,be.Velocity.X,be.Velocity.Y,be.Velocity.Z,be.Yawvel,be.Rollvel,be.Pitchvel,0,
                be.StallTime,jet.OnGround,jet.ThrusterValue,0,0,0,jet.DoingBarrelCount,0,
                be.CurrentOrientation.Yaw,be.CurrentOrientation.Pitch,be.CurrentOrientation.Roll,
                be.Orientation.Row0.X,be.Orientation.Row0.Y,be.Orientation.Row0.Z,
                be.Orientation.Row1.X,be.Orientation.Row1.Y,be.Orientation.Row1.Z,
                be.Orientation.Row2.X,be.Orientation.Row2.Y,be.Orientation.Row2.Z,0];
            uint[] bits=floats.Select(BitConverter.SingleToUInt32Bits).ToArray();
            bits[1]=(uint)be.EngineState;bits[8]=be.Stalling?1u:0u;bits[12]=jet.DoingLoop?1u:0u;
            bits[13]=jet.LoopHalfway?1u:0u;bits[14]=jet.LoopBroken?1u:0u;bits[16]=jet.DoingBarrelLeft?1u:0u;bits[29]=result;
            string line=function+" "+string.Join(" ",bits.Select(x=>x.ToString("x8")));
            Assert.True(line==expected,$"case {actual.Count}: {line}\nretail: {expected}");
            actual.Add(line);
        }
        RetailOracle.WriteOurs("flight-move",actual.ToArray());
        Assert.Equal(7000,actual.Count);
    }

    [GameDataFact]
    public void ResetJetConfigurationInvalidatesPendingWeaponEventsBeforeReplacingTheList()
    {
        LevelSession session=LevelSession.Load(100,GameData.Read,LevelSession.VirtualVoiceFor(GameData.Read));
        BattleEngine be=Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        Weapon old=Assert.IsType<Weapon>(be.JetPart!.GetCurrentWeapon());
        var pending=new Event(Weapon.BurstEvent,old);
        old.Target.SetReader(be);
        int readers=be.DeletionCount;
        session.Context.Events.AddEvent(Weapon.BurstEvent,old,session.Context.Events.Time+1.0f);
        Assert.True(old.DeletionCount>=2);
        be.JetPart.ResetConfiguration();
        Assert.Null(pending.ToCall);
        Assert.Equal(0,old.DeletionCount);
        Assert.Null(old.Target.ToRead);
        Assert.True(be.DeletionCount<readers);
        Assert.DoesNotContain(old,be.JetPart.Weapons);
        Assert.NotSame(old,be.JetPart.GetCurrentWeapon());
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

    [Fact]
    public void RepeatingOnceStillDrawsBeforeDuplicateAdmissionAndLoopingEffectOverridesOnce()
    {
        var sounds = OnslaughtRebuild.Parity.Sound.SoundEffects.Load("100\n0\n"u8.ToArray());
        var effect = new OnslaughtRebuild.Parity.Sound.SoundEffect("effect", 0, "same-sample");
        object owner = new();
        var rng = new OnslaughtRebuild.Parity.Crt.CrtRandStream();
        sounds.PlayRepeatingEffect(effect, owner, rng, fade: .2f, once: true);
        var first = Assert.Single(sounds.RepeatingEffects);
        sounds.PlayRepeatingEffect(effect, owner, rng, once: true);
        Assert.Same(first, Assert.Single(sounds.RepeatingEffects));
        Assert.Equal(.2f, first.Fade); // a once refusal does not replace/reset the existing fade
        Assert.Equal(2, rng.Calls);
        var looping = new OnslaughtRebuild.Parity.Sound.SoundEffect("loop", 0, "same-sample", looping: true);
        sounds.PlayRepeatingEffect(looping, owner, rng, once: true);
        Assert.Equal(2, sounds.RepeatingEffects.Count);
        Assert.Equal(3, rng.Calls);
    }

    [GameDataFact]
    public void WarningsShareTheirRealLoopOwnerAndRecoveryFadesWithoutRedrawing()
    {
        LevelSession session=LevelSession.Load(100,GameData.Read,LevelSession.VirtualVoiceFor(GameData.Read));
        BattleEngine be=Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        var sounds=session.Context.SoundEffects!;
        var low=sounds.Find("BE Energy Low");var critical=sounds.Find("BE Energy Critical");
        Assert.NotNull(low);Assert.NotNull(critical);
        be.Life=100;be.Energy=1;
        long calls=session.Context.Crt.Calls;
        be.HandleSounds();
        Assert.True(sounds.IsRepeatingEffectPlaying(low,be));
        Assert.Equal(calls+1,session.Context.Crt.Calls);
        be.HandleSounds();Assert.Equal(calls+1,session.Context.Crt.Calls);
        be.Life=6;be.HandleSounds();
        Assert.False(sounds.IsRepeatingEffectPlaying(low,be));
        Assert.True(sounds.IsRepeatingEffectPlaying(critical,be));
        Assert.Equal(calls+2,session.Context.Crt.Calls);
        be.Life=100;be.Energy=10;be.HandleSounds();
        Assert.Equal(-.02f,sounds.FindRepeatingSample(critical!.SampleName,be)!.Fade);
        Assert.Equal(calls+2,session.Context.Crt.Calls);
        for(int i=0;i<52;i++)sounds.AdvanceLoopStatus(false);
        Assert.False(sounds.IsRepeatingEffectPlaying(critical,be));
    }

    [GameDataFact]
    public void OrdinarySpaceUsesMorphPermissionThenScheduledJetAndLandingTransitions()
    {
        LevelSession session=LevelSession.Load(100,GameData.Read,LevelSession.VirtualVoiceFor(GameData.Read));
        BattleEngine be=Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        while(session.PresentCount<3000)session.AdvanceToNextPresent();
        Assert.Equal(GameState.Playing,session.Game.State);Assert.True(be.PoweredUp);
        var observed=new List<BattleEngineState>();
        void Space()
        {
            observed.Clear();
            session.Input.Input.PostTapeKey(0x20,0x39,true);
            for(int i=0;i<8;i++){session.AdvanceToNextPresent();observed.Add(be.State);}
            session.Input.Input.PostTapeKey(0x20,0x39,false);
            for(int i=0;i<8;i++){session.AdvanceToNextPresent();observed.Add(be.State);}
        }
        be.DisableFlightMode();Space();Assert.Equal(BattleEngineState.Walker,be.State);
        // A reconstruction fixture enables flight directly; this does not claim the tutorial's scripted permission route.
        be.EnableFlightMode();be.Energy=be.Configuration!.Energy;
        Space();Assert.Equal(BattleEngineState.MorphingIntoJet,be.State);
        long deadline=session.PresentCount+400;
        while(be.State!=BattleEngineState.Jet&&session.PresentCount<deadline)session.AdvanceToNextPresent();
        Assert.Equal(BattleEngineState.Jet,be.State);Assert.False(be.RenderThingIsWalker);
        float energy=be.Energy;
        for(int i=0;i<20;i++)session.AdvanceToNextPresent();
        Assert.True(be.Energy<energy);Assert.NotNull(be.GetCurrentWeapon());
        Assert.Equal(BattleEngineState.Jet,be.State);
        Space();Assert.Contains(BattleEngineState.MorphingIntoWalker,observed);
        deadline=session.PresentCount+200;
        while(be.State!=BattleEngineState.Walker&&session.PresentCount<deadline)session.AdvanceToNextPresent();
        Assert.Equal(BattleEngineState.Walker,be.State);
        deadline=session.PresentCount+300;
        while(!be.IsOnGround()&&session.PresentCount<deadline)session.AdvanceToNextPresent();
        Assert.True(be.IsOnGround());energy=be.Energy;
        for(int i=0;i<20;i++)session.AdvanceToNextPresent();
        Assert.True(be.Energy>energy);Assert.True(be.RenderThingIsWalker);
    }

    private sealed class LaunchProbe(ThingContext context) : Unit(context)
    {
        public readonly List<(int Index,bool NeedOrientation)> Calls=[];
        public override void GetLaunchPosition(Weapon weapon,int index,out FVector pos,out FMatrix orientation,bool needOrientation)
        {
            Calls.Add((index,needOrientation));pos=new FVector(index,index+1,index+2);orientation=FMatrix.Identity;
        }
    }

    [GameDataFact]
    public void MuzzleEffectsUseRecordedEmitterButEffectSlotAngleAndRespectPositionOnly()
    {
        LevelSession session=LevelSession.Load(100,GameData.Read,LevelSession.VirtualVoiceFor(GameData.Read));
        var probe=new LaunchProbe(session.Context);
        var mode=new WeaponModeData("test"u8.ToArray());
        mode.SetLaunchAngle(1,0,0); // only slot 0: slot 1 must retain the 1/4096-turn default
        var weapon=new Weapon(session.Context,new WeaponData("test"u8.ToArray()),0){AttachedTo=probe,Mode=mode};
        var first=new OnslaughtRebuild.Parity.Particles.FoR();
        var second=new OnslaughtRebuild.Parity.Particles.FoR();
        var manager=session.Context.Particles!.Manager;
        weapon.Effects[0]=new OnslaughtRebuild.Parity.Particles.ParticleEffectLink(manager){Effect=first};
        weapon.Effects[1]=new OnslaughtRebuild.Parity.Particles.ParticleEffectLink(manager){Effect=second};
        weapon.Emitters[0]=7;weapon.Emitters[1]=3;
        weapon.MoveEmitter(true);
        Assert.Equal([(7,false),(3,false)],probe.Calls);
        Assert.Equal(new FVector(7,8,9),first.Pos);Assert.Equal(new FVector(3,4,5),second.Pos);
        Assert.Equal(FMatrix.Identity.Row0,first.Orientation.Row0);
        Assert.Equal(FMatrix.Identity.Row1,first.Orientation.Row1);
        Assert.Equal(FMatrix.Identity.Row2,first.Orientation.Row2);
        Assert.Equal(FMatrix.MakeRotation(0,1,0).Row1,second.Orientation.Row1);
        Assert.True(first.HasOrientation);Assert.True(second.HasOrientation);
        FMatrix retained=second.Orientation;
        mode.SetLaunchAngle(2,.2f,.3f);weapon.Emitters[1]=9;
        weapon.MoveEmitter(false);
        Assert.Equal(new FVector(9,10,11),second.Pos);
        Assert.Equal(retained.Row1,second.Orientation.Row1);
        weapon.Effects[0]!.Reset();probe.Calls.Clear();
        weapon.MoveEmitter(false);Assert.Equal([(9,false)],probe.Calls);
        weapon.Mode=null;probe.Calls.Clear();weapon.MoveEmitter(true);Assert.Empty(probe.Calls);
        weapon.Release();
    }

}
