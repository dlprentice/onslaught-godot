// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Core;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Client.Tests;

public sealed class InteractiveSessionTests
{
    // The first tick a first run has player control. The released
    // LevelScript reaches player.Activate() when TUTORIAL_TECHNICIAN_01
    // clears, and the released message box holds the opening five messages
    // to Level100MissionTiming.MessageBoxAllowedTick + the advance gaps:
    // 121 +113 +4+140 +4+122 +4+109 +4+44 = 665. Two fresh app-owned Steam
    // runs measured the Battle Engine power flag at +0x580 changing 0 -> 1
    // at 30 Hz Core tick 1000 (rebuild/PROVENANCE.md), i.e. 33,333 ms; 665
    // 20 Hz ticks is 33,250 ms, an 83 ms residual against the 50 ms sampler.
    //
    // THIS IS THE ACCEPTANCE SIGNAL THE 20 Hz MIGRATION WAS FOR, and it moved
    // the right way. At 30 Hz the sum was 182 +169 +6+210 +6+183 +6+163 +6+65
    // = 996 against the measured 1000, i.e. 33,200 ms against 33,333 ms - a
    // 133 ms residual. Retail floors every scheduled delay onto a whole 20 Hz
    // boundary (references/Onslaught/eventmanager.cpp:210-212), which a 30 Hz
    // Core cannot land at all, so the residual SHOULD shrink; it did, from
    // 133 ms to 83 ms. See Level100MissionTests
    // .ReleasedMessageBox_ReproducesTheRetailOpeningDeliverySchedule for the
    // same signal asserted across all eight measured boundaries.
    //
    // The value before either of those was 790, which is the same sum with the
    // gate and the gaps both absent.
    private const int FirstRunControlTick = 665;
    private static Level100ActorDefinitionSet ActorDefinitions =>
        Level100TestActorDefinitions.Create();
    private const long OneCoreStepTicks = 500_000;
    private const uint Seed = 0x4F4E534Cu;

    [Fact]
    public void RationalAccumulator_DoesNotTruncateTheCoreStep()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);

        FrameAdvanceResult beforeBoundary = session.AdvanceFrameTicks(499_999);
        FrameAdvanceResult afterBoundary = session.AdvanceFrameTicks(1);

        Assert.Equal(0, beforeBoundary.StepsAdvanced);
        Assert.Equal(1, afterBoundary.StepsAdvanced);
        Assert.Equal(1, afterBoundary.CurrentSnapshot.Tick);
        Assert.Equal(0, afterBoundary.InterpolationPhase);
        Assert.Equal(InteractiveSession.PhaseUnitsPerStep, afterBoundary.InterpolationPhaseScale);
    }

    [Fact]
    public void FourQuarterSecondFrames_AdvanceExactlyOneSecondOfTicks()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);

        FrameAdvanceResult result = default;
        for (int frame = 0; frame < 4; frame++)
        {
            result = session.AdvanceFrame(TimeSpan.FromMilliseconds(250));
            Assert.False(result.FrameTimeCapped);
        }

        Assert.Equal(SimulationConstants.TicksPerSecond, result.CurrentSnapshot.Tick);
        Assert.Equal(SimulationConstants.TicksPerSecond, session.Metrics.TotalSteps);
        Assert.Equal(0, session.Metrics.DroppedElapsedTicks);
    }

    [Fact]
    public void LongFrame_IsCappedAndReportedWithoutSkippingSimulationTicks()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);

        FrameAdvanceResult result = session.AdvanceFrame(TimeSpan.FromSeconds(1));

        Assert.True(result.FrameTimeCapped);
        // MaximumFrameElapsedTicks is 0.25 s of WALL CLOCK, and stays 0.25 s
        // across the 20 Hz migration: it bounds how far behind real time one
        // frame may catch up, which is a property of the host, not of the
        // simulation rate. Five 20 Hz steps fit in it where seven 30 Hz steps
        // did.
        Assert.Equal(5, result.StepsAdvanced);
        Assert.Equal(5, result.CurrentSnapshot.Tick);
        Assert.Equal(1, session.Metrics.CappedFrameCount);
        Assert.Equal(TimeSpan.FromMilliseconds(750).Ticks, session.Metrics.DroppedElapsedTicks);
    }

    [Fact]
    public void HeldToggleLevel_IsConsumedOnOneTickOnly()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(0, 0, false, true, false));

        FrameAdvanceResult result = session.AdvanceFrame(TimeSpan.FromMilliseconds(100));
        session.ObserveInput(new InteractiveInput(0, 0, false, true, false));
        session.AdvanceFrame(TimeSpan.FromMilliseconds(100));

        Assert.Equal(VehicleMode.Walker, result.CurrentSnapshot.Mode);
        Assert.Equal(VehicleTransition.None, result.CurrentSnapshot.Transition);
        Assert.Equal(1, session.Metrics.ToggleEdgesConsumed);

        for (int frame = 0; frame < 4; frame++)
        {
            session.AdvanceFrame(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(VehicleMode.Walker, session.CurrentSnapshot.Mode);
        Assert.Equal(VehicleTransition.None, session.CurrentSnapshot.Transition);
        Assert.Equal(1, session.Metrics.ToggleEdgesConsumed);
    }

    [Fact]
    public void PressAndReleaseBetweenTicks_LeavesOneLatchedEdge()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(0, 0, false, true, false));
        session.ObserveInput(InteractiveInput.Idle);

        FrameAdvanceResult result = session.AdvanceFrameTicks(500_000);

        Assert.Equal(VehicleMode.Walker, result.CurrentSnapshot.Mode);
        Assert.Equal(VehicleTransition.None, result.CurrentSnapshot.Transition);
        Assert.Equal(1, session.Metrics.ToggleEdgesConsumed);
    }

    [Fact]
    public void DuplicateEventAndLevelEdge_AreCoalescedBeforeTheTick()
    {
        InteractiveSession session = CreatePlayingSession();
        session.QueueToggleMode();
        session.ObserveInput(new InteractiveInput(0, 0, false, true, false));

        session.AdvanceFrame(TimeSpan.FromMilliseconds(100));

        Assert.Equal(1, session.Metrics.ToggleEdgesConsumed);
    }

    [Fact]
    public void ZoomWheelEdge_ReachesCoreOnceAndContinuesTheRetailEase()
    {
        InteractiveSession session = CreatePlayingSession();

        session.QueueZoomIn();
        Assert.True(session.HasHeldOrPendingInput);
        FrameAdvanceResult first = session.AdvanceFrameTicks(OneCoreStepTicks);

        Assert.Equal(900, first.CurrentSnapshot.ZoomPermille);
        Assert.Equal(
            SimulationConstants.ZoomInPermille,
            first.CurrentSnapshot.DesiredZoomPermille);
        Assert.False(session.HasHeldOrPendingInput);

        FrameAdvanceResult second = session.AdvanceFrameTicks(OneCoreStepTicks);
        Assert.Equal(800, second.CurrentSnapshot.ZoomPermille);
        Assert.Equal(
            SimulationConstants.ZoomInPermille,
            second.CurrentSnapshot.DesiredZoomPermille);
    }

    [Fact]
    public void ChangeWeaponEdge_IsRoutedAndConsumedOnce()
    {
        InteractiveSession session = CreatePlayingSession();
        Level100MissionWeapon before =
            session.CurrentSnapshot.Level100WalkerSelectedWeapon;

        session.QueueChangeWeapon();
        Assert.True(session.HasHeldOrPendingInput);
        FrameAdvanceResult first = session.AdvanceFrameTicks(OneCoreStepTicks);

        // Level 100 has only one active walker weapon at first control, so the
        // released CountActiveWeapons gate preserves the selected slot. The
        // edge counter is the observable that proves the Client emitted the
        // SimAction rather than merely clearing its pending latch.
        Assert.Equal(before, first.CurrentSnapshot.Level100WalkerSelectedWeapon);
        Assert.False(session.HasHeldOrPendingInput);
        Assert.Equal(1, session.Metrics.ChangeWeaponEdgesConsumed);
        Assert.Equal(
            first.CurrentSnapshot.Level100WalkerSelectedWeapon,
            session.AdvanceFrameTicks(OneCoreStepTicks)
                .CurrentSnapshot.Level100WalkerSelectedWeapon);
        Assert.Equal(1, session.Metrics.ChangeWeaponEdgesConsumed);
    }

    [Fact]
    public void GunReleaseEdge_IsLatchedOnceWhileHeldTicksRemainSeparate()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(0, 0, true, false, false));

        session.AdvanceFrame(TimeSpan.FromMilliseconds(200));
        session.ObserveInput(new InteractiveInput(0, 0, true, false, false));
        session.AdvanceFrameTicks(500_000);

        Assert.Equal(0, session.Metrics.FirePulseEdgesConsumed);
        Assert.Equal(5, session.Metrics.FireHeldTicksSampled);

        // Godot can observe the same physical release twice: once from its
        // key-up event and once from the next sampled level. The bool latch
        // must coalesce those sources into one retail release action.
        session.QueueFirePulse();
        session.ObserveInput(InteractiveInput.Idle);
        session.AdvanceFrameTicks(500_000);
        Assert.Equal(1, session.Metrics.FirePulseEdgesConsumed);
        session.AdvanceFrameTicks(500_000);

        Assert.Equal(1, session.Metrics.FirePulseEdgesConsumed);

        // A complete repress/release between fixed steps still contributes one
        // edge, matching CPCController's old/current button truth table.
        session.ObserveInput(new InteractiveInput(0, 0, true, false, false));
        session.ObserveInput(InteractiveInput.Idle);
        session.AdvanceFrameTicks(500_000);

        Assert.Equal(2, session.Metrics.FirePulseEdgesConsumed);
    }

    [Fact]
    public void HeldTwinVulcanButton_RepeatsVolleysThroughChargeActionWithoutReleaseEdges()
    {
        InteractiveSession session = CreateTwinVulcanSessionForWeaponChecks();
        long releaseEdges = session.Metrics.FirePulseEdgesConsumed;
        long heldTicks = session.Metrics.FireHeldTicksSampled;
        float expectedReady = -200f;
        int volleys = 0;
        session.ObserveInput(new InteractiveInput(0, 0, true, false, false));

        // One physical press, sampled through twenty Core updates. No queued
        // Fire pulses or injected actions stand in for the held button.
        for (int step = 0; step < 20; step++)
        {
            int previousNextId = session.CurrentSnapshot.NextProjectileId;
            FrameAdvanceResult frame = session.AdvanceFrameTicks(OneCoreStepTicks);
            Assert.True(session.LastConsumedInput!.Value.HasAction(SimActions.ChargeWeapon));
            Assert.False(session.LastConsumedInput!.Value.HasAction(SimActions.Fire));
            float now = (float)((double)frame.CurrentSnapshot.RetailEventFrameCount * (double)0.05f);
            if (now > expectedReady)
            {
                Level100WeaponFireEvent fired = Assert.Single(frame.Level100WeaponFireEvents);
                Assert.Equal(Level100PlayerWeapon.MechTwinVulcanCannon, fired.Weapon);
                Assert.Equal(4, fired.RoundCount);
                Assert.Equal(previousNextId + 4, frame.CurrentSnapshot.NextProjectileId);
                expectedReady = (float)((double)now + (double)0.05f);
                volleys++;
            }
            else
            {
                Assert.Empty(frame.Level100WeaponFireEvents);
                Assert.Equal(previousNextId, frame.CurrentSnapshot.NextProjectileId);
            }
            uint storedReady = frame.CurrentSnapshot.Level100PlayerWeaponState.TwinVulcanReadyAtTimeBits;
            Assert.Equal(BitConverter.SingleToUInt32Bits(expectedReady), storedReady);
        }

        Assert.InRange(volleys, 9, 20);
        Assert.Equal(releaseEdges, session.Metrics.FirePulseEdgesConsumed);
        Assert.Equal(heldTicks + 20, session.Metrics.FireHeldTicksSampled);
    }

    [Fact]
    public void PhysicalPulseHoldReleaseThenTap_ClearsChargeAndReturnsToMedium()
    {
        InteractiveSession session = CreateFiringRangeSessionForWeaponChecks();
        long releaseEdges = session.Metrics.FirePulseEdgesConsumed;
        session.ObserveInput(new InteractiveInput(0, 0, true, false, false));
        for (int step = 0; step < 10; step++)
            Assert.Empty(session.AdvanceFrameTicks(OneCoreStepTicks).Level100WeaponFireEvents);
        Assert.Equal(0x42c80000u, session.CurrentSnapshot.Level100PlayerWeaponState.PulseChargeBits);

        session.ObserveInput(InteractiveInput.Idle);
        FrameAdvanceResult release = session.AdvanceFrameTicks(OneCoreStepTicks);
        Assert.Single(release.Level100WeaponFireEvents);
        Assert.Equal(Level100ProjectileKind.MechPulseBoltLarge,
            release.CurrentSnapshot.Projectiles.MaxBy(projectile => projectile.Id)!.Kind);
        Assert.Equal(0u, release.CurrentSnapshot.Level100PlayerWeaponState.PulseChargeBits);
        float ready = BitConverter.UInt32BitsToSingle(
            release.CurrentSnapshot.Level100PlayerWeaponState.PulseReadyAtTimeBits);
        while ((float)((session.CurrentSnapshot.Tick + 1) / 20d) <= ready)
            session.AdvanceFrameTicks(OneCoreStepTicks);

        // A fresh physical tap between samples leaves a release edge and no
        // held charge tick, so it must use level zero after the earlier clear.
        session.ObserveInput(new InteractiveInput(0, 0, true, false, false));
        session.ObserveInput(InteractiveInput.Idle);
        FrameAdvanceResult tap = session.AdvanceFrameTicks(OneCoreStepTicks);
        Assert.Single(tap.Level100WeaponFireEvents);
        Assert.Equal(Level100ProjectileKind.MechPulseBoltMedium,
            tap.CurrentSnapshot.Projectiles.MaxBy(projectile => projectile.Id)!.Kind);
        Assert.Equal(0u, tap.CurrentSnapshot.Level100PlayerWeaponState.PulseChargeBits);
        Assert.Equal(releaseEdges + 2, session.Metrics.FirePulseEdgesConsumed);
    }

    [Fact]
    public void FlightDisabledSession_ConsumesTheTransformEdgeIntoCanonicalRejection()
    {
        InteractiveSession session = CreatePlayingSession();
        session.QueueToggleMode();

        FrameAdvanceResult result = session.AdvanceFrame(
            TimeSpan.FromMilliseconds(200));

        Assert.Equal(VehicleTransition.None, result.CurrentSnapshot.Transition);
        Assert.Empty(result.CurrentSnapshot.AquilaFlightEventLog);
        AquilaFlightEvent rejected = Assert.Single(result.AquilaFlightEvents);
        Assert.Equal(AquilaFlightEvents.TransformRejected, rejected.Kind);
        Assert.True(rejected.Tick < result.CurrentSnapshot.Tick);
        Assert.Equal(1, session.Metrics.ToggleEdgesConsumed);
    }

    [Fact]
    public void LandingJetsHeld_AreLevelSampledIntoCore()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(
            0,
            0,
            false,
            false,
            false,
            LandingJetsHeld: true));

        FrameAdvanceResult result = session.AdvanceFrameTicks(500_000);

        Assert.True(result.CurrentSnapshot.LandingJetsActive);
    }

    [Fact]
    public void ShortFirePulse_SurvivesUntilOneTickConsumesIt()
    {
        InteractiveSession session = CreatePlayingSession();

        session.QueueFirePulse();
        session.AdvanceFrameTicks(100_000);
        FrameAdvanceResult firingTick = session.AdvanceFrameTicks(233_334);
        FrameAdvanceResult followingTick = session.AdvanceFrameTicks(500_000);

        Assert.Empty(firingTick.CurrentSnapshot.Projectiles);
        Assert.Empty(followingTick.CurrentSnapshot.Projectiles);
        Assert.Equal(1, session.Metrics.FirePulseEdgesConsumed);
        Assert.Equal(0, session.Metrics.FireHeldTicksSampled);
    }

    [Fact]
    public void ShortMovementPulse_StartsAccelerationAndThenCoasts()
    {
        InteractiveSession session = CreatePlayingSession();

        session.QueueMovementPulse(0, 1);
        FrameAdvanceResult movementTick = session.AdvanceFrameTicks(500_000);
        FrameAdvanceResult idleTick = session.AdvanceFrameTicks(500_000);

        // MOVED by the 20 Hz migration. The walker's first accelerating tick
        // is WalkerAccelerationPerTick along the facing, and that constant is
        // 70 where it was 33: a per-tick increment into a store damped by
        // mWalkFriction, so it converts by (1-0.7)/(1-0.7884) * 1.5 = 2.126,
        // not by the velocity rule. |(-34, 61)| is 70; |(-16, 29)| was 33.
        // The walker's SPEED is unchanged - both rates cap at
        // mMaxWalkVelocity 0.15 = 3,000 mm/s.
        Assert.Equal(
            new SimVector2(-34, 61),
            movementTick.CurrentSnapshot.PlayerPosition);
        Assert.Equal(
            new SimVector2(-57, 103),
            idleTick.CurrentSnapshot.PlayerPosition);
        Assert.Equal(1, session.Metrics.MovementPulseEdgesConsumed);
    }

    [Fact]
    public void PulseAndHeldState_AreCoalescedIntoOneSimulationInput()
    {
        InteractiveSession session = CreatePlayingSession();
        session.QueueMovementPulse(0, 1);
        session.QueueFirePulse();
        session.ObserveInput(new InteractiveInput(0, 1, true, false, false));

        FrameAdvanceResult result = session.AdvanceFrameTicks(500_000);

        // MOVED by the 20 Hz migration. The walker's first accelerating tick
        // is WalkerAccelerationPerTick along the facing, and that constant is
        // 70 where it was 33: a per-tick increment into a store damped by
        // mWalkFriction, so it converts by (1-0.7)/(1-0.7884) * 1.5 = 2.126,
        // not by the velocity rule. |(-34, 61)| is 70; |(-16, 29)| was 33.
        // The walker's SPEED is unchanged - both rates cap at
        // mMaxWalkVelocity 0.15 = 3,000 mm/s.
        Assert.Equal(new SimVector2(-34, 61), result.CurrentSnapshot.PlayerPosition);
        Assert.Empty(result.CurrentSnapshot.Projectiles);
        Assert.Equal(1, session.Metrics.MovementPulseEdgesConsumed);
        Assert.Equal(1, session.Metrics.FirePulseEdgesConsumed);
        Assert.Equal(1, session.Metrics.FireHeldTicksSampled);
    }

    [Fact]
    public void ResetDominatesItsTick_AndRestartsTheOpeningInputGate()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(0, 0, true, false, true));

        FrameAdvanceResult resetTick = session.AdvanceFrameTicks(500_000);
        FrameAdvanceResult followingTick = session.AdvanceFrameTicks(500_000);

        Assert.Empty(resetTick.CurrentSnapshot.Projectiles);
        Assert.Empty(followingTick.CurrentSnapshot.Projectiles);
        Assert.False(followingTick.CurrentSnapshot.Level100PlayerControlEnabled);
        Assert.Equal(
            SimulationConstants.Level100OpeningPanTicks - 1,
            followingTick.CurrentSnapshot.Level100OpeningTicksRemaining);
        Assert.Equal(1, session.Metrics.ResetEdgesConsumed);
        Assert.Equal(1, session.Metrics.ResetGeneration);
        Assert.Equal(2, session.Metrics.FireHeldTicksSampled);
    }

    [Fact]
    public void SameIntegerFrameAndInputSequence_ProducesSameStateAndMetrics()
    {
        InteractiveSession first = RunInteractiveSequence();
        InteractiveSession second = RunInteractiveSequence();

        Assert.Equal(StateHasher.ComputeHex(first.CurrentSnapshot), StateHasher.ComputeHex(second.CurrentSnapshot));
        Assert.Equal(first.Metrics, second.Metrics);
        Assert.Equal(first.InterpolationPhase, second.InterpolationPhase);
    }

    [Fact]
    public void FramePartitioning_DoesNotChangeStateForTheSameHeldInput()
    {
        InteractiveSession coarse = CreatePlayingSession();
        InteractiveSession fine = CreatePlayingSession();
        var input = new InteractiveInput(1, 1, true, false, false);
        coarse.ObserveInput(input);
        fine.ObserveInput(input);

        for (int frame = 0; frame < 10; frame++)
        {
            coarse.AdvanceFrame(TimeSpan.FromMilliseconds(100));
        }

        for (int frame = 0; frame < 40; frame++)
        {
            fine.AdvanceFrame(TimeSpan.FromMilliseconds(25));
        }

        Assert.Equal(
            FirstRunControlTick + SimulationConstants.TicksPerSecond,
            coarse.CurrentSnapshot.Tick);
        Assert.Equal(
            FirstRunControlTick + SimulationConstants.TicksPerSecond,
            fine.CurrentSnapshot.Tick);
        Assert.Equal(StateHasher.ComputeHex(coarse.CurrentSnapshot), StateHasher.ComputeHex(fine.CurrentSnapshot));
    }

    [Fact]
    public void LookX_HeldRotatesFacingUsingWalkerYawInertia()
    {
        InteractiveSession session = CreatePlayingSession(1);
        // One Core step needs elapsedTicks such that elapsed * TPS >= PhaseUnitsPerStep.
        const long oneCoreStepTicks =
            (TimeSpan.TicksPerSecond / SimulationConstants.TicksPerSecond) + 1;
        session.ObserveInput(new InteractiveInput(0, 0, false, false, false, LookX: 1));
        Assert.True(session.HasHeldOrPendingInput);
        for (int i = 0; i < 20; i++)
        {
            session.AdvanceFrameTicks(oneCoreStepTicks);
        }

        Assert.Equal(1, session.CurrentSnapshot.FacingX);
        Assert.Equal(0, session.CurrentSnapshot.FacingZ);
    }

    [Fact]
    public void WalkerTravelThenSettleRequestsOneHydraulicsCue()
    {
        InteractiveSession session = CreatePlayingSession(1);
        session.ObserveInput(new InteractiveInput(0, 1, false, false, false));
        for (int tick = 0; tick < 80; tick++)
        {
            FrameAdvanceResult moving = session.AdvanceFrameTicks(OneCoreStepTicks);
            Assert.DoesNotContain(
                moving.AquilaFlightEvents,
                item => item.Kind ==
                    AquilaFlightEvents.WalkerHydraulicsRequested);
        }

        WorldSnapshot rolling = session.CurrentSnapshot;
        Assert.True(
            rolling.WalkerSoundRolloverCount >
                SimulationConstants.WalkerSoundMinimumRolloverCount);
        string rollingHash = StateHasher.ComputeHex(rolling);
        Assert.NotEqual(
            rollingHash,
            StateHasher.ComputeHex(rolling with
            {
                WalkerSoundTravelMillimeters =
                    rolling.WalkerSoundTravelMillimeters + 1,
            }));
        Assert.NotEqual(
            rollingHash,
            StateHasher.ComputeHex(rolling with
            {
                WalkerSoundRolloverCount = rolling.WalkerSoundRolloverCount + 1,
            }));

        session.ObserveInput(InteractiveInput.Idle);
        FrameAdvanceResult settled = default;
        bool requested = false;
        for (int tick = 0; tick < 20; tick++)
        {
            settled = session.AdvanceFrameTicks(OneCoreStepTicks);
            if (settled.AquilaFlightEvents.Any(item =>
                    item.Kind ==
                        AquilaFlightEvents.WalkerHydraulicsRequested))
            {
                requested = true;
                break;
            }
        }

        Assert.True(requested, "Walker never reached the strict settled-speed edge.");
        AquilaFlightEvent hydraulics = Assert.Single(
            settled.AquilaFlightEvents,
            item => item.Kind ==
                AquilaFlightEvents.WalkerHydraulicsRequested);
        Assert.Equal(VehicleMode.Walker, hydraulics.Mode);
        Assert.Equal(0, settled.CurrentSnapshot.WalkerSoundRolloverCount);
        Assert.DoesNotContain(
            session.AdvanceFrameTicks(OneCoreStepTicks).AquilaFlightEvents,
            item => item.Kind == AquilaFlightEvents.WalkerHydraulicsRequested);
    }

    [Fact]
    public void LookY_HeldPitchesTheBattleEngineThroughTheClientAdapter()
    {
        InteractiveSession session = CreatePlayingSession(1);
        const long oneCoreStepTicks =
            (TimeSpan.TicksPerSecond / SimulationConstants.TicksPerSecond) + 1;
        session.ObserveInput(new InteractiveInput(0, 0, false, false, false, LookY: -1));

        session.AdvanceFrameTicks(oneCoreStepTicks);

        // Retail's own 1/117 rad per update (BattleEngineWalkerPart.cpp:355),
        // verbatim since the 20 Hz migration. This read -3,938 at 30 Hz, the
        // time-equivalent of the same shipped divisor.
        Assert.Equal(-8_547, session.CurrentSnapshot.FacingPitchMicroRad);
        Assert.Equal(
            -8_547,
            session.CurrentSnapshot.WalkerPitchVelocityMicroRadPerTick);
    }

    [Fact]
    public void ShortLookPulse_SurvivesUntilOneTickConsumesIt()
    {
        InteractiveSession session = CreatePlayingSession(1);
        int startingYaw = session.CurrentSnapshot.FacingYawMicroRad;

        session.QueueLookPulse(1, -1);
        FrameAdvanceResult beforeTick = session.AdvanceFrameTicks(100_000);

        Assert.Equal(0, beforeTick.StepsAdvanced);
        Assert.True(session.HasHeldOrPendingInput);

        FrameAdvanceResult lookTick = session.AdvanceFrameTicks(400_001);

        Assert.Equal(1, lookTick.StepsAdvanced);
        Assert.False(session.HasHeldOrPendingInput);
        // Re-pinned 2026-08-14 after the shipped Aquila
        // mGroundTurnRate=1.0 correction reduced full walker yaw from the
        // provisional Core-only 22,667 to retail's 13,333 micro-radians/tick.
        Assert.Equal(startingYaw + 13_333, lookTick.CurrentSnapshot.FacingYawMicroRad);
        Assert.Equal(13_333, lookTick.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(-8_547, lookTick.CurrentSnapshot.FacingPitchMicroRad);
        Assert.Equal(
            -8_547,
            lookTick.CurrentSnapshot.WalkerPitchVelocityMicroRadPerTick);
    }

    /// <summary>
    /// The mouse-sensitivity slider's consumer.
    ///
    /// Steam <c>CController::DoMappings</c> scales a centred displacement by
    /// <c>g_MouseSensitivity * 0.004333333</c> (= 13/3000), and the slider's
    /// reachable values are <c>(index + 1) * 3</c>. Two things must hold together
    /// and neither is safe alone: passing the image default 7.0 must leave the
    /// axis EXACTLY where it was before the slider existed (91/3000), and a
    /// different sensitivity must actually move it. Asserting only the first
    /// would pass on a setter that does nothing.
    /// </summary>
    [Fact]
    public void PointerMotion_SensitivitySliderScalesTheAxisAndSevenIsTheDefault()
    {
        const long oneCoreStepTicks =
            (TimeSpan.TicksPerSecond / SimulationConstants.TicksPerSecond) + 1;

        InteractiveSession explicitDefault = CreatePlayingSession(1);
        explicitDefault.SetMouseSensitivity(7f);
        explicitDefault.QueuePointerMotionMilliPixels(15_000, -7_500);
        FrameAdvanceResult atSeven = explicitDefault.AdvanceFrameTicks(oneCoreStepTicks);

        // The goldens PointerMotion_PreservesMagnitudeAndRetailRecenteringCoast
        // pins for the untouched session, reproduced through the slider at 7.0.
        // MOVED 2026-07-30, from 1,640 and -299. See the accounting on that
        // test; both pins record the same 15 px / -7.5 px motion. The yaw pin
        // moved again on 2026-08-14 with the shipped 13,333-micro-rad/tick scale;
        // pitch is unchanged.
        Assert.Equal(3_147, atSeven.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(
            -863,
            atSeven.CurrentSnapshot.WalkerPitchVelocityMicroRadPerTick);

        // The lowest reachable stop is 3.0, well under the shipped 7.0, so the
        // same hand motion must turn the walker measurably less far.
        InteractiveSession slowest = CreatePlayingSession(1);
        slowest.SetMouseSensitivity(3f);
        slowest.QueuePointerMotionMilliPixels(15_000, -7_500);
        FrameAdvanceResult atThree = slowest.AdvanceFrameTicks(oneCoreStepTicks);

        Assert.True(
            atThree.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick <
                atSeven.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick,
            "the sensitivity slider did not reach the pointer axis");
        Assert.True(atThree.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick > 0);
    }

    /// <summary>
    /// One whole mouse pixel is worth exactly the stick position the released
    /// build gives it - the countable half of task #141.
    ///
    /// <para>Retail's look axis is
    /// <c>g_MouseSensitivity * (cursor - centre) * 13/3000</c>
    /// (<c>CController::DoMappings</c> 0x0042DB40, scalar at pristine
    /// VA 0x005D97C8), read out of an INTEGER cursor position. So one pixel is
    /// 91/3000 = <b>30 permille</b> at the image's untouched 7.0, and 39/3000 =
    /// <b>13 permille</b> at the slider's lowest reachable stop of 3.0.</para>
    ///
    /// <para><b>Neither number was reachable before 2026-07-30.</b> This client
    /// eased the offset BEFORE reading it, so one pixel was read as
    /// round(1 px × 0.702049) and arrived as 21 permille at 7.0 and 9 at 3.0 -
    /// 30 % short, and values the released build cannot produce at all. Retail
    /// sets its recentre flag on the way OUT of the read
    /// (<c>0x0066E94D = 1</c>) and eases afterwards in
    /// <c>Input__UpdateCursorCenterWithWindowScale</c> 0x0042DA00.</para>
    ///
    /// <para>The 13 is the interesting one: it is below the 15-permille floor
    /// the old half-pixel dead zone imposed, so it is a stop that was
    /// unreachable at ANY sensitivity and is now delivered exactly.</para>
    /// </summary>
    [Fact]
    public void PointerMotion_OneWholePixelIsWorthRetailsOwnStop()
    {
        foreach ((float sensitivity, short permille) in
            new (float, short)[] { (7f, 30), (3f, 13) })
        {
            InteractiveSession session = CreatePlayingSession(1);
            session.SetMouseSensitivity(sensitivity);
            session.QueuePointerMotionMilliPixels(1_000, 0);
            FrameAdvanceResult moved = session.AdvanceFrameTicks(OneCoreStepTicks);

            // Stated as an EQUIVALENCE rather than a golden: one pixel of mouse
            // has to be the same thing to Core as commanding that stick
            // position outright. A golden here would survive the axis moving.
            var direct = new Simulation(1, ActorDefinitions);
            for (int tick = 0; tick < FirstRunControlTick; tick++)
            {
                direct.Step(SimInput.Idle);
            }

            WorldSnapshot commanded = direct.Step(
                new SimInput(0, 0, SimActions.None, 0, 0, permille, 0));

            Assert.Equal(
                commanded.WalkerYawVelocityMicroRadPerTick,
                moved.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick);

            // The guard that stops the equivalence being trivially true if both
            // sides collapse to a motionless walker.
            Assert.True(
                moved.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick > 0,
                $"one pixel at sensitivity {sensitivity} moved nothing");
        }
    }

    /// <summary>
    /// The offset returns to rest, and it does so because of 0x0042DA00's
    /// one-pixel anti-stall rather than because a floor snapped it away.
    ///
    /// <para>Retail's ease is integer, so it stalls: round(1 px × 0.702049) is
    /// 1 px again. 0x0042DA00 carries
    /// <c>if ((centre != cursor) &amp;&amp; (step == 0)) step = ±1</c> for
    /// exactly that case. Without the rule a one-pixel offset would coast
    /// forever; with it, one pixel is spent in a single step.</para>
    /// </summary>
    [Fact]
    public void PointerMotion_OffsetWalksBackToRestOnePixelAtATime()
    {
        InteractiveSession session = CreatePlayingSession(1);
        int startingYaw = session.CurrentSnapshot.FacingYawMicroRad;
        session.QueuePointerMotionMilliPixels(1_000, 0);

        FrameAdvanceResult spent = session.AdvanceFrameTicks(OneCoreStepTicks);

        // It was READ before it was eased - the pixel reached the simulation.
        Assert.NotEqual(startingYaw, spent.CurrentSnapshot.FacingYawMicroRad);

        // And then it was gone, in one step, with no floor to snap it away.
        Assert.False(
            session.HasHeldOrPendingInput,
            "a one-pixel offset outlived its step - the anti-stall is missing");

        // Three pixels take exactly three steps: 3 -> 2 -> 1 -> 0. The last of
        // those only happens because of the ±1 forcing; round(1 × 0.702049) is
        // 1, so without it the offset coasts at one pixel for ever.
        InteractiveSession longer = CreatePlayingSession(1);
        longer.QueuePointerMotionMilliPixels(3_000, 0);
        for (int step = 1; step <= 2; step++)
        {
            longer.AdvanceFrameTicks(OneCoreStepTicks);
            Assert.True(
                longer.HasHeldOrPendingInput,
                $"a three-pixel offset was already at rest after {step} step(s)");
        }

        longer.AdvanceFrameTicks(OneCoreStepTicks);
        Assert.False(
            longer.HasHeldOrPendingInput,
            "a three-pixel offset never returned to rest");
    }

    [Fact]
    public void PointerMotion_PreservesMagnitudeAndRetailRecenteringCoast()
    {
        InteractiveSession session = CreatePlayingSession(1);
        int startingYaw = session.CurrentSnapshot.FacingYawMicroRad;
        const long oneCoreStepTicks =
            (TimeSpan.TicksPerSecond / SimulationConstants.TicksPerSecond) + 1;

        // 15 px, not the 80 px this test used before 2026-07-27. At retail's
        // sensitivity an 80 px flick SATURATES the axis, and both samples came
        // back at the full 10,444 - which would have made this test pass for
        // almost any sensitivity at all, blind to the constants it exists to
        // protect. 15 px lands mid-range, where the sensitivity scalar and the
        // Player.cpp:334-355 response curve both actually show up.
        session.QueuePointerMotionMilliPixels(15_000, -7_500);
        FrameAdvanceResult first = session.AdvanceFrameTicks(oneCoreStepTicks);

        // THESE FOUR GOLDENS MOVED ON 2026-07-30, and the old values are kept
        // here rather than replaced because the move is the whole point of the
        // change that caused it. The pins were:
        //
        //     step 1 yaw   1,640 -> 2,465        step 1 pitch  -299 -> -398
        //     step 2 yaw   2,531 -> 3,847        step 2 pitch  -466 -> -626
        //
        // THE ACCOUNTING. The motion is unchanged: 15 px right, 7.5 px up.
        //   WAS: the offset was eased BEFORE it was read, so 15,000 milli-px
        //        became round(15,000 x 0.702049) = 10,531 and the axis saw
        //        319 permille; -7,500 became -5,265 and the axis saw -160.
        //   NOW: retail's order - CController::DoMappings reads the cursor and
        //        only then raises the recentre flag - so the axis sees the
        //        whole-pixel cursor itself: 15 px = 455 permille and -7 px =
        //        -212. 455/319 is 1.426, which is 1/0.702049: exactly the ease
        //        that is no longer taken before the read.
        //   The Y axis moves by slightly more than that ratio because 7.5 px is
        //   not a whole cursor position. It reads as 7 px and the half pixel is
        //   CARRIED, which is what Windows does with sub-pixel mouse counts;
        //   retail's cursor globals (0x0089BDA8/0x0089BDA4) are ints and
        //   never hold a fraction.
        //
        // Neither the sensitivity scalar (13/3000), the Player.cpp:334-355
        // response curve, nor any Core constant moved: the whole change is
        // confined to InteractiveSession's pointer path. The cold-career
        // acceptance run is BIT-IDENTICAL across it - same Won, same terminal
        // tick 12463, same hull 12100, same state hash ffe391e7... - because
        // that run only ever asks for stick positions a whole pixel can produce,
        // and those are delivered the same either way. These four goldens moved
        // precisely because they are the one place that exercises a motion that
        // is NOT already on retail's lattice.
        // MOVED AGAIN 2026-07-31 BY THE 20 Hz MIGRATION, and this is a pure
        // R3 unit change with no behaviour in it. The pointer path, the
        // sensitivity scalar and the response curve are all untouched; what
        // moved is WalkerYawInputMicroRadPerTick 10,444 -> 22,667 and
        // WalkerPitchInputMicroRadPerTick 3,938 -> 8,547, because a per-tick
        // impulse into a store retained at retail's exact 0.8 converts by
        // (1-0.8)/(1-0.861774) * 1.5 = 2.170337. Every pin below scales by
        // that factor to within one micro-radian of integer rounding on the
        // FIRST step. The second step also carries the pointer recentring ease,
        // which is retail's own 10/17 per 20 Hz update and was itself running
        // as (10/17)^(2/3) = 702049/1000000 at 30 Hz - so the two-step pins
        // move by more than 2.170337. That ease is a THIRD verbatim retail
        // value the migration recovers, and it lives in InteractiveSession
        // rather than Core:
        //     step 1 yaw   2,465 -> 5,349        step 1 pitch  -398 -> -863
        //     step 2 yaw   3,847 -> 7,294        step 2 pitch  -626 -> -1,177
        // The walker's turn is the same angular rate per SECOND at either
        // Core rate; only the per-tick quantum changed.
        // MOVED AGAIN 2026-08-14 by the shipped Aquila mGroundTurnRate=1.0
        // correction. The prior Core-only scale was 22,667; retail is 13,333.
        // The same pointer samples now yield yaw 3,147 then 4,277 while pitch,
        // cursor recentring and the response-table evidence remain unchanged.
        Assert.Equal(3_147, first.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(-863, first.CurrentSnapshot.WalkerPitchVelocityMicroRadPerTick);
        Assert.Equal(startingYaw + 3_147, first.CurrentSnapshot.FacingYawMicroRad);
        Assert.True(session.HasHeldOrPendingInput);

        // The guard that makes the numbers above mean something. If a future
        // sensitivity change pushes this input to the clamp, these fail loudly
        // instead of the goldens quietly becoming a constant.
        Assert.True(
            first.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick < FullDeflectionYawPerTick,
            "pointer input saturated the yaw axis - pick a smaller motion");
        Assert.True(
            Math.Abs(first.CurrentSnapshot.WalkerPitchVelocityMicroRadPerTick) <
                FullDeflectionYawPerTick,
            "pointer input saturated the pitch axis - pick a smaller motion");

        FrameAdvanceResult second = session.AdvanceFrameTicks(oneCoreStepTicks);

        // STEP 2's YAW MOVED 7,294 -> 7,271 ON 2026-08-01, by the look-response
        // table going to one entry per representable input (task #161). The
        // move is EXACTLY ONE PERMILLE OF THE YAW SCALE and that is the whole
        // of it: WalkerYawInputMicroRadPerTick is 22,667, the difference is 23,
        // and 22,667 / 1,000 = 22.667. So a single table entry changed by one
        // permille at the magnitude the recentring ease leaves on the axis at
        // this step, and nothing else did - which is why STEP 1 above did not
        // move at all, its magnitude landing on an entry the new table agrees
        // with the old one about.
        //
        // The new value is the more faithful one. Retail computes
        // tan(1.2v)/tan(1.2) live per sample (Player.cpp:346,350) with no table
        // at all; ours exists only because .NET does not guarantee bit-identical
        // transcendentals. Every one of the 187 entries that moved is strictly
        // closer to that law - worst case 0.4997 permille against the old
        // table's 0.9436 - so this pin is nearer retail than the one it
        // replaces, not merely different.
        Assert.Equal(4_277, second.CurrentSnapshot.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(
            -1_177,
            second.CurrentSnapshot.WalkerPitchVelocityMicroRadPerTick);
        Assert.Equal(
            first.CurrentSnapshot.FacingYawMicroRad + 4_277,
            second.CurrentSnapshot.FacingYawMicroRad);
    }

    // Yaw rate at full look deflection, from
    // WalkerAnalogLook_FollowsTheReleasedCurveAndUsesTheSameRetailCoast.
    private const int FullDeflectionYawPerTick = 13_333;

    [Fact]
    public void InteractiveInputSequence_MatchesDirectCoreTicks()
    {
        InteractiveSession session = CreatePlayingSession();
        var direct = new Simulation(Seed, ActorDefinitions);
        for (int tick = 0; tick < FirstRunControlTick; tick++)
        {
            direct.Step(SimInput.Idle);
        }
        var held = new InteractiveInput(0, 1, true, false, false);
        session.ObserveInput(held);
        session.AdvanceFrameTicks(1_500_000);
        for (int tick = 0; tick < 3; tick++)
        {
            direct.Step(new SimInput(0, 1, SimActions.Fire));
        }

        session.ObserveInput(new InteractiveInput(0, 1, false, true, false));
        session.AdvanceFrameTicks(500_000);
        direct.Step(new SimInput(0, 1, SimActions.ToggleMode));

        session.ObserveInput(new InteractiveInput(0, 0, true, false, true));
        session.AdvanceFrameTicks(500_000);
        direct.Step(new SimInput(0, 0, SimActions.Fire | SimActions.Reset));

        Assert.Equal(StateHasher.ComputeHex(direct.Snapshot), StateHasher.ComputeHex(session.CurrentSnapshot));
    }

    [Fact]
    public void FocusLoss_ReleasesHeldInputAndDiscardsUnconsumedEdges()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(1, 1, true, true, true));
        session.QueueMovementPulse(-1, -1);
        session.QueueLookPulse(-1, -1);
        session.QueuePointerMotionMilliPixels(-10_000, 10_000);
        session.QueueFirePulse();

        session.ReleaseAllInput();
        session.AdvanceFrameTicks(500_000);

        Assert.Equal(SimVector2.Zero, session.CurrentSnapshot.PlayerPosition);
        Assert.Empty(session.CurrentSnapshot.Projectiles);
        Assert.Equal(VehicleMode.Walker, session.CurrentSnapshot.Mode);
        Assert.Equal(0, session.Metrics.ToggleEdgesConsumed);
        Assert.Equal(0, session.Metrics.ResetEdgesConsumed);
        Assert.Equal(0, session.Metrics.FireHeldTicksSampled);
    }

    [Fact]
    public void FocusLoss_SuppressesHeldAndPulseInputUntilANeutralSample()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(1, 1, true, true, true));
        session.QueueMovementPulse(-1, -1);
        session.QueueLookPulse(-1, -1);
        session.QueuePointerMotionMilliPixels(-10_000, 10_000);
        session.QueueFirePulse();

        session.SuspendInputUntilReleased();
        session.ObserveInput(new InteractiveInput(0, 1, true, false, false));
        session.QueueMovementPulse(1, 0);
        session.QueueLookPulse(1, 0);
        session.QueuePointerMotionMilliPixels(10_000, -10_000);
        session.QueueFirePulse();
        session.QueueToggleMode();
        session.QueueReset();
        session.AdvanceFrameTicks(500_000);

        Assert.True(session.InputSuspendedUntilReleased);
        Assert.False(session.HasHeldOrPendingInput);
        Assert.Equal(SimVector2.Zero, session.CurrentSnapshot.PlayerPosition);
        Assert.Empty(session.CurrentSnapshot.Projectiles);
        Assert.Equal(VehicleMode.Walker, session.CurrentSnapshot.Mode);
        Assert.Equal(0, session.Metrics.ToggleEdgesConsumed);
        Assert.Equal(0, session.Metrics.ResetEdgesConsumed);

        session.ObserveInput(InteractiveInput.Idle);
        Assert.False(session.InputSuspendedUntilReleased);
        session.AdvanceFrameTicks(500_000);
        Assert.Equal(0, session.Metrics.FirePulseEdgesConsumed);

        session.QueueMovementPulse(0, 1);
        session.QueueFirePulse();
        session.AdvanceFrameTicks(500_000);

        Assert.Equal(new SimVector2(-34, 61), session.CurrentSnapshot.PlayerPosition);
        Assert.Empty(session.CurrentSnapshot.Projectiles);
    }

    [Fact]
    public void AuthenticPauseFreezesOneSessionAndRequiresNeutralInputAfterResume()
    {
        InteractiveSession session = CreatePlayingSession();
        int startingTick = session.CurrentSnapshot.Tick;
        string startingHash = StateHasher.ComputeHex(session.CurrentSnapshot);
        InteractiveSessionMetrics startingMetrics = session.Metrics;
        long startingPhase = session.InterpolationPhase;
        session.ObserveInput(new InteractiveInput(1, 1, true, true, true));
        session.QueueMovementPulse(-1, -1);
        session.QueueLookPulse(1, -1);
        session.QueuePointerMotionMilliPixels(10_000, -10_000);
        session.QueueFirePulse();

        session.SetAuthenticMenuPaused(true);

        Assert.Equal(InteractivePauseReason.AuthenticMenu, session.PauseReasons);
        Assert.True(session.IsPaused);
        Assert.True(session.IsAuthenticMenuPaused);
        Assert.True(session.InputSuspendedUntilReleased);
        Assert.False(session.HasHeldOrPendingInput);

        session.ObserveInput(InteractiveInput.Idle);
        session.QueueMovementPulse(0, 1);
        FrameAdvanceResult pausedFrame = session.AdvanceFrame(TimeSpan.FromSeconds(1));

        Assert.Equal(0, pausedFrame.StepsAdvanced);
        Assert.False(pausedFrame.FrameTimeCapped);
        Assert.Empty(pausedFrame.Level100MissionEvents);
        Assert.Equal(startingTick, session.CurrentSnapshot.Tick);
        Assert.Equal(startingPhase, session.InterpolationPhase);
        Assert.Equal(startingHash, StateHasher.ComputeHex(session.CurrentSnapshot));
        Assert.Equal(startingMetrics, session.Metrics);

        session.SetAuthenticMenuPaused(false);

        Assert.False(session.IsPaused);
        Assert.True(session.InputSuspendedUntilReleased);
        session.ObserveInput(new InteractiveInput(0, 1, true, false, false));
        Assert.True(session.InputSuspendedUntilReleased);
        Assert.False(session.HasHeldOrPendingInput);
        session.ObserveInput(InteractiveInput.Idle);
        Assert.False(session.InputSuspendedUntilReleased);
        session.AdvanceFrameTicks(500_000);
        Assert.Equal(
            startingMetrics.FirePulseEdgesConsumed,
            session.Metrics.FirePulseEdgesConsumed);
    }

    [Fact]
    public void PauseResumePreservesTheSameCoreInputTapeTraceAndFinalHash()
    {
        InteractiveSession uninterrupted = CreatePlayingSession();
        InteractiveSession paused = CreatePlayingSession();
        InteractiveInput[] tape =
        [
            new InteractiveInput(0, 1, false, false, false),
            InteractiveInput.Idle,
            new InteractiveInput(0, 0, true, false, false),
            InteractiveInput.Idle,
            new InteractiveInput(0, 0, false, true, false),
            InteractiveInput.Idle,
            new InteractiveInput(1, 0, false, false, false),
        ];
        var uninterruptedTrace = new List<string>();
        var pausedTrace = new List<string>();

        for (int index = 0; index < tape.Length; index++)
        {
            if (index == 2)
            {
                paused.SetAuthenticMenuPaused(true);
                Assert.Equal(0, paused.AdvanceFrame(TimeSpan.FromSeconds(1)).StepsAdvanced);
                paused.ObserveInput(new InteractiveInput(1, 1, true, true, true));
                paused.QueuePointerMotionMilliPixels(100_000, -100_000);
                paused.SetAuthenticMenuPaused(false);
                paused.ObserveInput(InteractiveInput.Idle);
            }

            uninterrupted.ObserveInput(tape[index]);
            paused.ObserveInput(tape[index]);
            FrameAdvanceResult directFrame = uninterrupted.AdvanceFrameTicks(OneCoreStepTicks);
            FrameAdvanceResult resumedFrame = paused.AdvanceFrameTicks(OneCoreStepTicks);
            Assert.Equal(1, directFrame.StepsAdvanced);
            Assert.Equal(1, resumedFrame.StepsAdvanced);
            uninterruptedTrace.Add(StateHasher.ComputeHex(directFrame.CurrentSnapshot));
            pausedTrace.Add(StateHasher.ComputeHex(resumedFrame.CurrentSnapshot));
        }

        Assert.Equal(uninterruptedTrace, pausedTrace);
        Assert.Equal(
            StateHasher.ComputeHex(uninterrupted.CurrentSnapshot),
            StateHasher.ComputeHex(paused.CurrentSnapshot));
        Assert.Equal(uninterrupted.Metrics, paused.Metrics);
    }

    [Fact]
    public void SnapshotsExposePreviousAndCurrentSimulationStates()
    {
        InteractiveSession session = CreatePlayingSession();
        session.ObserveInput(new InteractiveInput(1, 0, false, false, false));

        int startingTick = session.CurrentSnapshot.Tick;

        FrameAdvanceResult result = session.AdvanceFrameTicks(500_000);

        Assert.Equal(startingTick, result.PreviousSnapshot.Tick);
        Assert.Equal(startingTick + 1, result.CurrentSnapshot.Tick);
        Assert.Equal(0, result.PreviousSnapshot.PlayerPosition.X);
        Assert.True(result.CurrentSnapshot.PlayerPosition.X > 0);
    }

    [Fact]
    public void InvalidInputAndElapsedTime_AreRejected()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.ObserveInput(new InteractiveInput(2, 0, false, false, false)));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceFrameTicks(-1));
        WorldSnapshot before = session.CurrentSnapshot;
        Assert.Throws<ArgumentException>(() => session.AdvanceFrameTicks(
            1,
            [new Level100PlayerDeathFact()]));
        Assert.Same(before, session.CurrentSnapshot);
    }

    [Fact]
    public void InitialMissionEvents_AreDeliveredOnceByFrameEnvelope()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);

        FrameAdvanceResult initial = session.AdvanceFrameTicks(0);
        FrameAdvanceResult next = session.AdvanceFrameTicks(0);

        Assert.Equal(0, initial.StepsAdvanced);
        // No character message is delivered on tick 0. The released message box
        // is not allowed to play anything until the opening pan is over
        // (Level100MissionTiming.MessageBoxAllowedTick, from
        // CGame::StartPlayingState in references/Onslaught/game.cpp:3026-3031),
        // and the HUD is not even drawn before then.
        Assert.Empty(initial.Level100MissionEvents.OfType<Level100MessageRequested>());
        Assert.Empty(next.Level100MissionEvents);

        var session2 = new InteractiveSession(Seed, ActorDefinitions);
        session2.AdvanceFrameTicks(0);
        Level100MessageRequested? greeting = null;
        for (int tick = 0;
             tick < Level100MissionTiming.MessageBoxAllowedTick && greeting is null;
             tick++)
        {
            greeting = session2.AdvanceFrameTicks(OneCoreStepTicks)
                .Level100MissionEvents
                .OfType<Level100MessageRequested>()
                .FirstOrDefault();
        }

        Assert.NotNull(greeting);
        Assert.Equal(292562, greeting!.MessageId);
        Assert.Equal(Level100MissionTiming.MessageBoxAllowedTick, greeting.Tick);
    }

    [Fact]
    public void FrameMissionEvents_AggregateEverySimulationStepInOrder()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);
        // HUD_02 becomes active at MessageBoxAllowedTick + HUD_01's 113 ticks +
        // the released 4-tick advance gap = 238. Step to 236 so the two-step
        // frame below straddles it.
        for (int tick = 0; tick < 237; tick++)
        {
            session.AdvanceFrameTicks(OneCoreStepTicks);
        }

        FrameAdvanceResult frame = session.AdvanceFrameTicks(1_000_000);

        Assert.Equal(2, frame.StepsAdvanced);
        Level100MessageRequested message = Assert.Single(
            frame.Level100MissionEvents.OfType<Level100MessageRequested>());
        Assert.Equal(293386, message.MessageId);
        Assert.Empty(frame.CurrentSnapshot.Level100MissionEvents);
    }

    [Fact]
    public void FrameDestructionEvents_AggregateTheReleaseTickInOrder()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);
        while (session.CurrentSnapshot.Tick < FirstFlightSmokeScenario.DurationTicks)
        {
            session.ObserveInput(
                FirstFlightSmokeScenario.GetInputForTick(session.CurrentSnapshot.Tick));
            session.AdvanceFrameTicks(OneCoreStepTicks);
        }

        // Use the released static Target Tank 2 so a multi-step client frame
        // exercises event aggregation without a waypoint mover changing the
        // contact pose during the release tick.
        TargetSnapshot target = session.CurrentSnapshot.Targets.Single(item => item.Id == 2);
        Level100ActorPoseSnapshot contactPose =
            PlaceTargetCenterAtPulseEmitter(session.CurrentSnapshot);
        session.QueueFirePulse();

        FrameAdvanceResult frame = session.AdvanceFrameTicks(
            InteractiveSession.MaximumFrameElapsedTicks,
            [
                new Level100ActorActivationFact(target.ActorId, true),
                new Level100ActorPoseFact(target.ActorId, contactPose),
            ]);

        Assert.Equal(5, frame.StepsAdvanced);
        Level100DestructionEvent[] targetEvents = frame.Level100DestructionEvents
            .Where(item => item.ActorId == target.ActorId.Value)
            .ToArray();
        Assert.Equal(
            new[]
            {
                Level100DestructionEventKind.PulseImpact,
                Level100DestructionEventKind.SegmentDamaged,
                Level100DestructionEventKind.SegmentDamaged,
            },
            targetEvents.Select(item => item.Kind));
        Assert.All(
            targetEvents.Where(item =>
                item.Kind == Level100DestructionEventKind.PulseImpact),
            item => Assert.Equal(
                Level100DestructionEffectKind.PulseImpact,
                item.EffectKind));
        float directDamage = BitConverter.UInt32BitsToSingle(
            Level100DestructionState.PulseDirectDamageBits);
        float explosionDamage = BitConverter.UInt32BitsToSingle(
            Level100DestructionState.PulseExplosionDamageBits);
        float afterDirectHit = 6f - directDamage;
        float afterExplosionHit = afterDirectHit - explosionDamage;
        Assert.Equal(
            new[]
            {
                BitConverter.SingleToUInt32Bits(afterDirectHit),
                BitConverter.SingleToUInt32Bits(afterExplosionHit),
            },
            targetEvents
                .Where(item => item.Kind == Level100DestructionEventKind.SegmentDamaged)
                .Select(item => item.RemainingHealthBits));
        Assert.Empty(frame.CurrentSnapshot.Level100DestructionEvents);
        Assert.Empty(session.AdvanceFrameTicks(0).Level100DestructionEvents);
    }

    [Fact]
    public void ClientLevel100FailureTape_FirstRunRepeatsLossTextAndHashes()
    {
        ClientMissionTape first = RunClientFailureTape();
        ClientMissionTape repeat = RunClientFailureTape();

        Assert.Equal(first.Hashes, repeat.Hashes);
        Assert.Equal(Level100MissionOutcome.Lost, first.Snapshot.Level100Mission.Outcome);
        Assert.Equal(1_110_345_999, first.Snapshot.Level100Mission.FailureTextId);
        Assert.Equal(
            Level100MissionTerminalState.FailureCountdownElapsed,
            first.Snapshot.Level100Mission.TerminalState);
    }

    [Fact]
    public void ClientAssembly_HasNoGodotOrForbiddenRuntimeDependencies()
    {
        string[] references = typeof(InteractiveSession).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Godot", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name == "System.Diagnostics.Process");
        Assert.DoesNotContain(references, name => name == "System.IO.FileSystem");
        Assert.DoesNotContain(references, name => name == "System.Net.Http");
    }

    [Fact]
    public void MaterializedLevel100ActorDefinitions_OwnCompleteWorldAndAuthoredSpawns()
    {
        Level100ActorDefinitionSet definitions = LoadMaterializedActorDefinitions();

        Assert.Equal(44, definitions.Actors.Count);
        Assert.Equal(33, definitions.Actors.Count(actor =>
            actor.DefinitionIdentity.StartsWith("wres:bswd:", StringComparison.Ordinal)));
        Assert.Equal(3, definitions.Actors.Count(actor =>
            actor.DefinitionIdentity.StartsWith("wres:rlwd:", StringComparison.Ordinal) &&
            actor.TargetGroup == Level100MissionTargetGroup.StaticTargets));
        Assert.Equal(5, definitions.Actors.Count(actor => actor.Trigger.HasValue));
        Assert.Single(definitions.Actors, actor => actor.ThingTypeMask ==
            Level100ReleasedThingTypeMasks.BattleEngine);
        Assert.Contains(definitions.Actors, actor => actor.Name == "Transporter");
        Assert.Contains(definitions.Actors, actor => actor.Name == "Air Trainer");
        Assert.Equal(10, definitions.Spawns.Count);
        Assert.Equal(5, definitions.MotionDefinitions.Count);
        Assert.Equal(
            [
                ("Target Tank", Level100ActorMotionClass.GroundVehicle,
                    3, 2, 0x005E297C, 2_000),
                ("Target Truck", Level100ActorMotionClass.GroundVehicle,
                    3, 2, 0x005E297C, 2_000),
                ("Air Trainer", Level100ActorMotionClass.Plane,
                    9, 8, 0x005E1930, 5_000),
                ("Target Drone", Level100ActorMotionClass.Plane,
                    9, 8, 0x005E1930, 5_000),
                ("U-17 Highside Transporter",
                    Level100ActorMotionClass.Dropship,
                    12, 12, 0x005E1DD8, 8_000),
            ],
            definitions.MotionDefinitions.Select(definition =>
                (
                    definition.DefinitionName,
                    definition.MotionClass,
                    definition.BehaviorSerializedType,
                    definition.BehaviorInternalId,
                    definition.SteamClassVtableAddress,
                    definition.ArrivalRadiusMillimeters)));
        Assert.All(
            definitions.MotionDefinitions.Where(definition =>
                definition.MotionClass ==
                Level100ActorMotionClass.GroundVehicle),
            definition =>
            {
                Assert.Equal(0x40600000,
                    definition.MaximumSpeedFloatBits);
                Assert.Equal(0x3D567750,
                    definition.MaximumTurnRadiansPerBaseTickFloatBits);
                Assert.Equal(4, definition.FullGuideBaseTicks);
                Assert.Equal(100,
                    definition.CoreGroundOriginOffsetMillimeters);
            });
        Assert.All(
            definitions.MotionDefinitions.Where(definition =>
                definition.MotionClass !=
                Level100ActorMotionClass.GroundVehicle),
            definition =>
            {
                Assert.Null(definition.MaximumSpeedFloatBits);
                Assert.Null(
                    definition.MaximumTurnRadiansPerBaseTickFloatBits);
                Assert.Null(definition.FullGuideBaseTicks);
                Assert.Null(
                    definition.CoreGroundOriginOffsetMillimeters);
            });
        Assert.Equal(
            [
                "Flyby Path",
                "Target Truck Path 3",
                "Target Truck Path 2",
                "Target Truck Path 1",
                "Transporter Path",
                "Target Tank Path 2",
                "Target Tank Path 1",
                "Drone Path 1",
            ],
            definitions.WaypointPaths.Select(path => path.Name));
        // Re-pinned 2026-07-27 by the waypoint coordinate correction. These
        // values used to come from the 121-entry navigation graph, which holds
        // 11 distinct XY tuples repeated 11 times, all at z = 10.0. Node 25 is
        // RLWD initial-actor ordinal 25, a thingType-18 marker authored at
        // retail (248.75, 275.0, -0.0).
        Level100WaypointPathDefinition truckPath =
            definitions.GetWaypointPath("Target Truck Path 1");
        // Retail's list is the file order reversed (waypoint-paths.md).
        Assert.Equal([28, 27, 26, 25], truckPath.Points.Select(point => point.NodeIndex));
        Assert.Equal(
            new SimVector3(-39_938, 0, 31_750),
            truckPath.Point(25).PositionMillimeters);
        Assert.Equal(
            new Level100FloatVector4Bits(
                BitConverter.SingleToInt32Bits(248.75f),
                BitConverter.SingleToInt32Bits(275.0f),
                BitConverter.SingleToInt32Bits(-0.0f),
                BitConverter.SingleToInt32Bits(0.0f)),
            truckPath.Point(25).RetailComponentsFloatBits);
        // No two of the 30 authored markers resolve to one position any more.
        // Before the correction these 30 points collapsed onto 11, and this
        // assertion is the regression guard for that specific failure.
        Assert.Equal(
            30,
            definitions.WaypointPaths
                .SelectMany(path => path.Points)
                .Select(point => point.PositionMillimeters)
                .Distinct()
                .Count());
        // `Flyby Path` and `Target Truck Path 2` used to share their first
        // three points exactly. They are the aliasing pair named in the
        // measurement, so they are the pair pinned here.
        Assert.Empty(
            definitions.GetWaypointPath("Flyby Path").Points
                .Select(point => point.PositionMillimeters)
                .Intersect(
                    definitions.GetWaypointPath("Target Truck Path 2").Points
                        .Select(point => point.PositionMillimeters)));
        // The two ambient aircraft routes carry their own authored altitude:
        // the Air Trainer's markers sit at retail z = -15 and the Transporter's
        // at z = -20. Every node used to be flattened to Y = +10000 mm.
        Assert.Equal(
            [-15_000, -15_000, 0],
            definitions.GetWaypointPath("Flyby Path").Points
                .Select(point => point.PositionMillimeters.Y));
        // The decoder turns the manifest's file order and target chain into
        // retail's list (the file order reversed) and each node's own target.
        Assert.Equal(
            [41, 42, 43],
            definitions.GetWaypointPath("Flyby Path").Points.Select(point => point.NodeIndex));
        Assert.Equal(
            [42, 43, null],
            definitions.GetWaypointPath("Flyby Path").Points.Select(point => point.TargetNodeIndex));
        Assert.All(
            definitions.GetWaypointPath("Drone Path 1").Points,
            point => Assert.NotNull(point.TargetNodeIndex));
        Level100SpawnDefinition[] trainingTrucks = definitions.Spawns
            .Where(spawn => spawn.ScriptName is
                "TargetTruck1" or "TargetTruck2" or "TargetTruck3")
            .ToArray();
        Assert.Equal(3, trainingTrucks.Length);
        Assert.All(trainingTrucks, spawn =>
            Assert.Equal(SimulationConstants.Level100TrainingTruckLife, spawn.InitialHealth));
        Level100WaypointPathDefinition transporterPath =
            definitions.GetWaypointPath("Transporter Path");
        // Retail's list is the file order [44, 22, 23] reversed.
        Assert.Equal([23, 22, 44], transporterPath.Points.Select(point => point.NodeIndex));
        // This pair used to assert the defect: nodes 44 and 22 resolved to the
        // SAME point, because the navigation graph they were read from repeats
        // its 11 positions. They are 92.2 m apart in the authored data, and
        // node 22 carries the Transporter's own -20 m cruise altitude.
        Assert.NotEqual(
            transporterPath.Point(44).PositionMillimeters,
            transporterPath.Point(22).PositionMillimeters);
        Assert.NotEqual(
            transporterPath.Point(44).RetailComponentsFloatBits,
            transporterPath.Point(22).RetailComponentsFloatBits);
        Assert.Equal(
            new SimVector3(68_313, 0, 28_750),
            transporterPath.Point(44).PositionMillimeters);
        Assert.Equal(
            new SimVector3(-47_688, -20_000, 36_500),
            transporterPath.Point(22).PositionMillimeters);
        Assert.Equal(
            new SimVector3(-20_188, -20_000, 23_250),
            transporterPath.Point(23).PositionMillimeters);
        Assert.Equal(64, definitions.IdentitySha256.Length);
        Assert.Null(definitions.Actors.Single(actor => actor.Name == "Airfield").ScriptName);
        Assert.Null(definitions.Actors.Single(actor => actor.Name == "Hangar").ScriptName);

        Level100SpawnDefinition trainer = definitions.Spawns.Single(spawn =>
            spawn.ScriptName == "AirTrainer");
        Assert.Equal("wres:bswd:0023", trainer.OwnerDefinitionIdentity);
        Assert.Equal(
            BitConverter.SingleToInt32Bits(-0.099276736f),
            trainer.AuthoredEmitterTransform.LocalPositionFloatBits.X);

        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId trainerId = Assert.Single(registry.SpawnThing(
            registry.GetThingRef("Airfield")!.Value,
            "Air Trainer",
            "SpawnerB",
            1,
            "AirTrainer"));
        // Compose the emitter against the seated Airfield, preserving its
        // height above the owner. The older precomputed manifest pose used an
        // unseated owner; clamping that child to terrain loses the emitter.
        // Raw construction also normalizes the matrix's Row1X zero sign.
        Assert.Equal(-3_867, trainer.InitialPose.PositionMillimeters.Y);
        int ownerY = registry.GetActor(registry.GetThingRef("Airfield")!.Value).Pose.PositionMillimeters.Y;
        int emitterHeight = checked((int)Math.Round(
            -BitConverter.Int32BitsToSingle(trainer.AuthoredEmitterTransform.LocalPositionFloatBits.Z) * 1_000.0,
            MidpointRounding.AwayFromZero));
        Level100ActorPoseSnapshot spawnedPose = registry.GetActor(trainerId).Pose;
        Assert.InRange(spawnedPose.PositionMillimeters.Y - ownerY, emitterHeight - 1, emitterHeight + 1);
        Assert.Equal(
            trainer.InitialPose with
            {
                PositionMillimeters =
                    trainer.InitialPose.PositionMillimeters with { Y = spawnedPose.PositionMillimeters.Y },
                BasisFloatBits = trainer.InitialPose.BasisFloatBits with { Row1X = 0 },
            },
            spawnedPose);
        Assert.DoesNotContain(registry.Snapshot.Actors, actor => actor.Pose is null);
    }

    [Fact]
    public void MaterializedActorCommands_DriveCanonicalMechanicsOwner()
    {
        Level100ActorDefinitionSet definitions = LoadMaterializedActorDefinitions();
        var simulation = new Simulation(Seed, definitions);
        WorldSnapshot snapshot = simulation.Snapshot;

        Level100ActorSnapshot factory = snapshot.Level100Actors.Actors.Single(
            actor => actor.Name == "Tank Factory");
        Assert.False(factory.Active);
        Level100ActorSnapshot target = snapshot.Level100Actors.Actors.Single(
            actor => actor.ScriptName == "TargetTank1");
        Assert.Equal(factory.ActorId, target.SpawnOwnerId);
        Assert.Equal("SpawnerA", target.SpawnerName);
        Assert.Equal(6_000, target.Health);

        Level100ActorCommandIntentSnapshot intent = Assert.Single(
            snapshot.Level100ActorMechanics.Actors,
            item => item.ActorId == target.ActorId);
        Assert.Equal(Level100ActorCommandIntent.FollowingWaypoint, intent.Intent);
        Assert.Equal("Target Tank Path 1", intent.WaypointPath);
        Assert.True(intent.WaitForWaypointCompletion);
        // The phase reaches 0 on the first full Move, which the tank's Actor
        // draw places one to three frames after its construction, and then
        // advances once a frame: the Tank Factory built it on frame 2 of the
        // pre-run, which ends on frame 60.
        Level100UnitCallbackSnapshot unit = Assert.Single(
            snapshot.Level100ActorMechanics.UnitCallbacks!,
            item => item.ActorId == target.ActorId);
        int lowFrequencyMoves = unit.FirstMoveFrame - (unit.ConstructionFrame + 1);
        Assert.InRange(lowFrequencyMoves, 0, 2);
        Assert.Equal(2, unit.ConstructionFrame);
        Assert.Equal(
            ((4 - lowFrequencyMoves) + (int)snapshot.RetailEventFrameCount - unit.ConstructionFrame) % 4,
            intent.GroundFullGuideBaseTickPhase);
        Assert.Contains(
            snapshot.Level100ActorScriptCommands,
            command =>
                command.ActorId == target.ActorId &&
                command.Kind ==
                    Level100ActorScriptCommandKind.FollowWaypointWait &&
                command.Argument == intent.WaypointPath);
        Assert.Equal(
            definitions.IdentitySha256,
            snapshot.Level100Actors.DefinitionSetIdentitySha256);
    }

    [Fact]
    public void MaterializedTargetTank1_FollowsReleasedRouteNaturally()
    {
        Level100ActorDefinitionSet definitions =
            LoadMaterializedActorDefinitions();
        var simulation = new Simulation(Seed, definitions);
        WorldSnapshot snapshot = simulation.Snapshot;
        Level100ActorId targetId =
            snapshot.Level100Actors.Actors.Single(actor =>
                actor.ScriptName == "TargetTank1").ActorId;
        var route = new List<int>();
        bool completed = false;

        for (int tick = 0; tick < 3_000; tick++)
        {
            Level100ActorCommandIntentSnapshot intent =
                snapshot.Level100ActorMechanics.Actors.Single(item =>
                    item.ActorId == targetId);
            if (intent.WaypointNodeIndex is { } node && (route.Count == 0 || route[^1] != node))
            {
                route.Add(node);
            }
            if (intent.Intent ==
                Level100ActorCommandIntent.Stopped)
            {
                completed = true;
                break;
            }
            snapshot = simulation.Step(SimInput.Idle);
        }

        // From the node nearest the tank's spawn along the targets 6 -> 7 -> 18.
        Assert.Equal([6, 7, 18], route);
        Assert.True(completed);
        Level100ActorSnapshot target =
            snapshot.Level100Actors.Actors.Single(actor =>
                actor.ActorId == targetId);
        // The end of the walk, node 18, whose target is none.
        Level100WaypointPointDefinition destination =
            definitions.GetWaypointPath("Target Tank Path 1").Point(18);
        long deltaX =
            (long)destination.PositionMillimeters.X -
            target.Pose.PositionMillimeters.X;
        long deltaZ =
            (long)destination.PositionMillimeters.Z -
            target.Pose.PositionMillimeters.Z;
        Assert.True(
            (deltaX * deltaX) + (deltaZ * deltaZ) <
            2_000L * 2_000L);
        Assert.Equal(
            Level100Terrain.Instance.SampleGroundElevationMillimeters(
                new SimVector2(
                    target.Pose.PositionMillimeters.X,
                    target.Pose.PositionMillimeters.Z)) +
                100,
            target.Pose.PositionMillimeters.Y);
        Assert.Equal(
            SimVector3.Zero,
            target.Pose.LinearVelocityMillimetersPerTick);
        Assert.Equal(
            SimVector3.Zero,
            target.Pose.AngularVelocityMicroRadiansPerTick);
        Level100ActorScriptInstanceSnapshot targetScript =
            snapshot.Level100ActorScripts.Instances.Single(item =>
                item.ActorId == targetId);
        Assert.DoesNotContain(
            targetScript.Continuations,
            continuation =>
                continuation.WaitKind ==
                Level100ActorScriptWaitKind.FollowWaypoint);
    }

    [Fact]
    public void TargetPresentation_ProjectsWarehouseAndNewTruckCanonicalBindings()
    {
        var actorId = new Level100ActorId(47);
        var identityBasis = new Level100FloatBasis3Bits(
            0x3F800000, 0, 0,
            0, 0x3F800000, 0,
            0, 0, 0x3F800000);
        var firstPose = new Level100ActorPoseSnapshot(
            new SimVector3(1_000, 2_000, 3_000),
            identityBasis,
            SimVector3.Zero,
            SimVector3.Zero);
        var target = new TargetSnapshot(
            actorId,
            5,
            "Target Truck",
            "m_f_truck_training.msh.aya",
            new SimVector2(1_000, 3_000),
            3_000,
            true,
            firstPose);

        Level100TargetVisualDescriptor first =
            Level100TargetPresentation.Project(target);
        var turnedPose = firstPose with
        {
            PositionMillimeters = new SimVector3(
                -4_000,
                5_000,
                -6_000),
            BasisFloatBits = new Level100FloatBasis3Bits(
                0, 0, unchecked((int)0xBF800000),
                0, 0x3F800000, 0,
                0x3F800000, 0, 0),
        };
        Level100TargetVisualDescriptor changed =
            Level100TargetPresentation.Project(
                target with
                {
                    Position = new SimVector2(-4_000, -6_000),
                    Pose = turnedPose,
                });

        Assert.Equal(actorId, first.ActorId);
        Assert.Equal("Target Truck", first.DefinitionName);
        Assert.Equal(
            "m_f_truck_training.msh.aya",
            first.MeshBinding);
        Assert.Equal(
            Level100TargetPresentation.TargetTruckBinding,
            first.Binding);
        Assert.True(first.Visible);
        Assert.Equal(1f, first.Position.X, 5);
        Assert.Equal(2f, first.Position.Y, 5);
        Assert.Equal(-3f, first.Position.Z, 5);
        Assert.Equal(
            new Level100RenderBasis3(
                new Level100RenderVector3(1f, 0f, 0f),
                new Level100RenderVector3(0f, 1f, 0f),
                new Level100RenderVector3(0f, 0f, 1f)),
            first.Basis);
        Assert.Equal(actorId, changed.ActorId);
        Assert.Equal(-4f, changed.Position.X, 5);
        Assert.Equal(5f, changed.Position.Y, 5);
        Assert.Equal(6f, changed.Position.Z, 5);
        Assert.Equal(
            new Level100RenderBasis3(
                new Level100RenderVector3(0f, 0f, -1f),
                new Level100RenderVector3(0f, 1f, 0f),
                new Level100RenderVector3(1f, 0f, 0f)),
            changed.Basis);
        Assert.NotEqual(first, changed);
        Assert.False(
            Level100TargetPresentation.Project(
                target with { IsActive = false }).Visible);

        var session = new InteractiveSession(
            Seed,
            LoadMaterializedActorDefinitions());
        TargetSnapshot warehouse =
            session.CurrentSnapshot.Targets.Single(item => item.Id == 4);
        Level100TargetVisualDescriptor warehouseDescriptor =
            Level100TargetPresentation.Project(warehouse);
        Assert.Equal(
            Level100TargetPresentation.WarehouseBinding,
            warehouseDescriptor.Binding);
        Assert.Equal("Warehouse", warehouseDescriptor.DefinitionName);
        Assert.Equal(
            "m_m_warehouse.msh.aya",
            warehouseDescriptor.MeshBinding);
    }

    [Fact]
    public void MaterializedTargetZone1_SpawnsThreeTrucksIntoCanonicalMover()
    {
        Level100ActorDefinitionSet definitions =
            LoadMaterializedActorDefinitions();
        var session =
            new InteractiveSession(Seed, definitions);
        Level100ActorSnapshot[] trucks = [];

        while (session.CurrentSnapshot.Tick <
            FirstFlightSmokeScenario.DurationTicks)
        {
            session.ObserveInput(
                FirstFlightSmokeScenario.GetInputForTick(
                    session.CurrentSnapshot.Tick));
            session.AdvanceFrameTicks(OneCoreStepTicks);
            trucks = session.CurrentSnapshot.Level100Actors.Actors
                .Where(actor =>
                    actor.TargetGroup ==
                    Level100MissionTargetGroup.TargetTrucks)
                .OrderBy(actor => actor.ScriptName)
                .ToArray();
            // Each truck's script starts on its INIT_SCRIPT, the frame after
            // it is built, and only then sends it down its path.
            if (trucks.Length == 3 &&
                trucks.All(actor =>
                {
                    Level100SpawnDefinition spawn =
                        definitions.Spawns.Single(definition =>
                            definition.ScriptName ==
                            actor.ScriptName);
                    return actor.Pose.PositionMillimeters !=
                        spawn.InitialPose.PositionMillimeters &&
                        session.CurrentSnapshot.Level100ActorMechanics.Actors
                            .Single(item => item.ActorId == actor.ActorId).Intent ==
                            Level100ActorCommandIntent.FollowingWaypoint;
                }))
            {
                break;
            }
        }

        Assert.Equal(3, trucks.Length);
        Assert.Equal(
            ["TargetTruck1", "TargetTruck2", "TargetTruck3"],
            trucks.Select(actor => actor.ScriptName));
        HashSet<Level100ActorId> truckActorIds =
            trucks.Select(actor => actor.ActorId).ToHashSet();
        TargetSnapshot[] truckTargets =
            session.CurrentSnapshot.Targets
                .Where(target => truckActorIds.Contains(target.ActorId))
                .OrderBy(target => target.Id)
                .ToArray();
        Assert.Equal(3, truckTargets.Length);
        Assert.Equal([5, 6, 7], truckTargets.Select(target => target.Id));
        foreach (TargetSnapshot truckTarget in truckTargets)
        {
            Level100TargetVisualDescriptor descriptor =
                Level100TargetPresentation.Project(truckTarget);
            Assert.Equal(truckTarget.ActorId, descriptor.ActorId);
            Assert.Equal("Target Truck", descriptor.DefinitionName);
            Assert.Equal(
                "m_f_truck_training.msh.aya",
                descriptor.MeshBinding);
            Assert.Equal(
                Level100TargetPresentation.TargetTruckBinding,
                descriptor.Binding);
            Assert.True(descriptor.Visible);
        }
        foreach (Level100ActorSnapshot truck in trucks)
        {
            Level100SpawnDefinition spawn =
                definitions.Spawns.Single(definition =>
                    definition.ScriptName ==
                    truck.ScriptName);
            Assert.Equal(
                spawn.OwnerDefinitionIdentity,
                session.CurrentSnapshot.Level100Actors.Actors
                    .Single(actor =>
                        actor.ActorId == truck.SpawnOwnerId)
                    .DefinitionIdentity);
            Assert.Equal(
                SimulationConstants.Level100TrainingTruckLife,
                truck.Health);
            Assert.NotEqual(
                spawn.InitialPose.PositionMillimeters,
                truck.Pose.PositionMillimeters);
            Assert.Equal(
                Level100Terrain.Instance
                    .SampleGroundElevationMillimeters(
                        new SimVector2(
                            truck.Pose.PositionMillimeters.X,
                            truck.Pose.PositionMillimeters.Z)) +
                    100,
                truck.Pose.PositionMillimeters.Y);
            Level100ActorCommandIntentSnapshot intent =
                session.CurrentSnapshot.Level100ActorMechanics.Actors
                    .Single(item =>
                        item.ActorId == truck.ActorId);
            Assert.Equal(
                Level100ActorCommandIntent.FollowingWaypoint,
                intent.Intent);
            Assert.Equal(
                $"Target Truck Path {truck.ScriptName![^1]}",
                intent.WaypointPath);
        }
    }

    [Fact]
    public void MaterializedLevel100ActorDefinitions_RepeatFailureHashes()
    {
        Level100ActorDefinitionSet definitions = LoadMaterializedActorDefinitions();

        ClientMissionTape failure = RunClientFailureTape(definitions);
        ClientMissionTape repeatedFailure = RunClientFailureTape(definitions);

        Assert.Equal(failure.Hashes, repeatedFailure.Hashes);
        Assert.Equal(Level100MissionOutcome.Lost, failure.Snapshot.Level100Mission.Outcome);
    }

    [Fact]
    public void FirstFlightSmokeScenario_ReachesFiringRangeAndCompletesWaypoint()
    {
        InteractiveInput pan = FirstFlightSmokeScenario.GetInputForTick(0);
        InteractiveInput strafe = FirstFlightSmokeScenario.GetInputForTick(
            FirstFlightSmokeScenario.TargetZoneInputStartTick);
        InteractiveInput forward = FirstFlightSmokeScenario.GetInputForTick(
            FirstFlightSmokeScenario.TargetZoneInputStartTick + 144);
        InteractiveInput closeout = FirstFlightSmokeScenario.GetInputForTick(
            FirstFlightSmokeScenario.DurationTicks - 1);
        InteractiveInput firingRangeTurn = FirstFlightSmokeScenario.GetInputForTick(1_330);
        InteractiveInput firingRangeApproach = FirstFlightSmokeScenario.GetInputForTick(1_360);
        InteractiveInput firingRangeAim = FirstFlightSmokeScenario.GetInputForTick(2_093);
        InteractiveInput pulseCannonProof = FirstFlightSmokeScenario.GetInputForTick(2_104);

        Assert.Equal(InteractiveInput.Idle, pan);
        Assert.Equal((sbyte)-1, strafe.MoveX);
        Assert.False(strafe.FireHeld);
        Assert.Equal((sbyte)1, forward.MoveZ);
        Assert.False(forward.ToggleModeHeld);
        Assert.Equal((sbyte)-1, firingRangeTurn.MoveX);
        Assert.Equal((sbyte)1, firingRangeApproach.MoveZ);
        Assert.Equal((sbyte)-1, firingRangeAim.LookX);
        Assert.True(pulseCannonProof.FireHeld);
        Assert.Equal(InteractiveInput.Idle, closeout);
        Assert.Equal(2_148, FirstFlightSmokeScenario.DurationTicks);
        Assert.Throws<ArgumentOutOfRangeException>(() => FirstFlightSmokeScenario.GetInputForTick(-1));

        Level100ActorDefinitionSet definitions = LoadMaterializedActorDefinitions();
        // The U-17 retreats inside the smoke, so the prior set keeps the safe sides.
        var priorDefinitions = new Level100ActorDefinitionSet(definitions.Actors,
            definitions.Spawns, definitions.WaypointPaths,
            definitions.MotionDefinitions.Select(definition => definition with { WeaponMounts = null }),
            baseWorldPineCount: definitions.BaseWorldPineCount, safeSides: definitions.SafeSides);
        var priorSession = new InteractiveSession(Seed, priorDefinitions);
        var session = new InteractiveSession(Seed, definitions);
        while (session.CurrentSnapshot.Tick < FirstFlightSmokeScenario.DurationTicks)
        {
            session.ObserveInput(
                FirstFlightSmokeScenario.GetInputForTick(session.CurrentSnapshot.Tick));
            FrameAdvanceResult result = session.AdvanceFrameTicks(500_000);
            Assert.Equal(1, result.StepsAdvanced);
            priorSession.ObserveInput(FirstFlightSmokeScenario.GetInputForTick(priorSession.CurrentSnapshot.Tick));
            Assert.Equal(1, priorSession.AdvanceFrameTicks(500_000).StepsAdvanced);
            Assert.Equal(StateHasher.ComputeHex(priorSession.CurrentSnapshot),
                StateHasher.ComputeHex(session.CurrentSnapshot with
                { Level100Actors = session.CurrentSnapshot.Level100Actors with
                    { DefinitionSetIdentitySha256 = priorDefinitions.IdentitySha256 } }));
        }

        TargetSnapshot firstTarget = session.CurrentSnapshot.Targets.Single(target => target.Id == 1);
        Assert.True(firstTarget.IsActive);
        Assert.Equal(SimulationConstants.Level100TargetTankLife, firstTarget.Hull);
        Assert.Equal(0, session.CurrentSnapshot.TargetsDestroyed);
        Level100ActorScriptInstanceSnapshot targetScript =
            session.CurrentSnapshot.Level100ActorScripts.Instances.Single(item =>
                item.ActorId == firstTarget.ActorId);
        Assert.DoesNotContain(
            targetScript.Continuations,
            continuation =>
                continuation.WaitKind ==
                Level100ActorScriptWaitKind.FollowWaypoint);
        Level100ActorCommandIntentSnapshot targetIntent =
            session.CurrentSnapshot.Level100ActorMechanics.Actors.Single(item =>
                item.ActorId == firstTarget.ActorId);
        Assert.Equal(
            Level100ActorCommandIntent.Stopped,
            targetIntent.Intent);
        Assert.Equal(4, session.Metrics.FireHeldTicksSampled);
        Assert.Equal(4, session.Metrics.FirePulseEdgesConsumed);
        // This fingerprint belongs to the 2,148-step in-process input tape.
        // September 26: the Battle Engine's 6002/6003 refresh draws and its
        // retained crosshair report joined the canonical state; then the
        // Aquila's weapon stores, recoil shake draws and every round's Actor
        // Init draw (the four Pulse releases each take six draws now); then
        // every round's launch basis took retail's default 2π/4096 pitch and
        // matrix composition, and the Pulse's level-0 mode lost the Small
        // bolt's scatter; then the load took the retail construction draws
        // and every unit's callbacks; then the Pulse left cockpit Gun 1
        // through the full body orientation; then every waypoint walk started
        // at the unit's nearest node and followed the nodes' own targets at
        // their load-time heights; then the walker's dash history became
        // float32 event times (no flick in this tape comes near the window).
        // Controller calls now precede callbacks and Move, so the four releases
        // use their retained emitter poses. Raw charge/readiness and shared RNG
        // state remain part of the canonical state. The semantic assertions
        // above and the independent identical-input repeat below guard this
        // expectation; no native Godot or Windows execution is claimed here.
        string finalStateHash = StateHasher.ComputeHex(session.CurrentSnapshot);
        Assert.Equal(finalStateHash, StateHasher.ComputeHex(
            CreateFiringRangeSessionForWeaponChecks(definitions).CurrentSnapshot));
        // The mount-only identity substitution above checks every step. Retain
        // both older definition formats' fingerprints without changing input.
        var legacyDefinitions = new Level100ActorDefinitionSet(definitions.Actors,
            definitions.Spawns.Select(spawn => spawn with { SpawnerExitWaypoints = null }),
            definitions.WaypointPaths, priorDefinitions.MotionDefinitions,
            baseWorldPineCount: definitions.BaseWorldPineCount);
        WorldSnapshot priorState = priorSession.CurrentSnapshot;
        WorldSnapshot priorIdentityOnly = session.CurrentSnapshot with
        {
            Level100Actors = session.CurrentSnapshot.Level100Actors with
            { DefinitionSetIdentitySha256 = priorDefinitions.IdentitySha256 },
        };
        Assert.Equal(StateHasher.ComputeHex(priorState), StateHasher.ComputeHex(priorIdentityOnly));
        Assert.Equal("9850e38fdd4b0d2f69daeb33302c5fa1282aa33c381b9e80a5ba80c4ce9ecd0e",
            StateHasher.ComputeHex(priorIdentityOnly));
        Assert.Equal("69781153748e692162b41b6ce001144fe8556acacc08d30b7c175a53d6323ce9",
            StateHasher.ComputeHex(session.CurrentSnapshot with
            { Level100Actors = session.CurrentSnapshot.Level100Actors with
                { DefinitionSetIdentitySha256 = legacyDefinitions.IdentitySha256 } }));
        Assert.True(
            finalStateHash == "79fc338c15183a50455f1e2ffdce549183e74cdf53b8a4a5f8f6bc7edc3e4503",
            $"First-flight final state hash: {finalStateHash}");
    }

    private static InteractiveSession RunInteractiveSequence()
    {
        InteractiveSession session = CreatePlayingSession();
        (long ElapsedTicks, InteractiveInput Input)[] frames =
        [
            (100_000, new InteractiveInput(0, 1, false, false, false)),
            (250_000, new InteractiveInput(0, 1, true, false, false)),
            (500_000, new InteractiveInput(0, 1, true, true, false)),
            (1_100_000, new InteractiveInput(1, 0, true, false, false)),
            (500_000, new InteractiveInput(0, 0, false, false, true)),
            (700_000, InteractiveInput.Idle),
        ];

        foreach ((long elapsedTicks, InteractiveInput input) in frames)
        {
            session.ObserveInput(input);
            session.AdvanceFrameTicks(elapsedTicks);
        }

        return session;
    }

    private static ClientMissionTape RunClientFailureTape(
        Level100ActorDefinitionSet? actorDefinitions = null)
    {
        var tape = new ClientMissionTape(actorDefinitions);
        tape.Step([new Level100MissionInputFact(Level100MissionInput.BrokeTutorial)]);
        tape.AdvanceUntil(
            state => state.Level100Mission.Outcome == Level100MissionOutcome.Lost,
            // "Broke Tutorial" is posted while HUD_01 still owns the message
            // box, so its two messages queue behind it at the released advance
            // gap and the terminal call lands at tick 594 rather than 291.
            800);
        tape.Advance(Level100MissionTiming.FailureCountdownTicks);
        return tape;
    }

    private sealed class ClientMissionTape
    {
        private readonly InteractiveSession _session;

        internal ClientMissionTape(Level100ActorDefinitionSet? actorDefinitions = null)
        {
            _session = new InteractiveSession(
                0x100u,
                actorDefinitions ?? Level100TestActorDefinitions.Create());
            Capture(_session.AdvanceFrameTicks(0).Level100MissionEvents);
        }

        internal WorldSnapshot Snapshot => _session.CurrentSnapshot;

        internal List<Level100MissionEvent> Events { get; } = [];

        internal List<string> Hashes { get; } = [];

        internal void Step(IReadOnlyList<Level100SimulationFact>? facts = null) =>
            Step(InteractiveInput.Idle, facts);

        internal void Step(
            InteractiveInput input,
            IReadOnlyList<Level100SimulationFact>? facts = null)
        {
            _session.ObserveInput(input);
            FrameAdvanceResult result = _session.AdvanceFrameTicks(OneCoreStepTicks, facts);
            Assert.Equal(1, result.StepsAdvanced);
            Assert.Empty(result.CurrentSnapshot.Level100Mission.PendingEvents);
            Capture(result.Level100MissionEvents);
        }

        internal void Advance(int ticks)
        {
            for (int tick = 0; tick < ticks; tick++)
            {
                Step();
            }
        }

        internal void AdvanceUntil(Func<WorldSnapshot, bool> predicate, int maximumTicks)
        {
            for (int tick = 0; tick < maximumTicks && !predicate(Snapshot); tick++)
            {
                Step();
            }

            Assert.True(predicate(Snapshot),
                $"Condition was not reached by client tick {Snapshot.Tick}.");
        }

        private void Capture(IReadOnlyList<Level100MissionEvent> events)
        {
            Events.AddRange(events);
            Hashes.Add(StateHasher.ComputeHex(Snapshot));
        }
    }

    private static InteractiveSession CreatePlayingSession(uint seed = Seed)
    {
        var session = new InteractiveSession(seed, ActorDefinitions);
        for (int tick = 0; tick < FirstRunControlTick; tick++)
        {
            session.AdvanceFrameTicks(OneCoreStepTicks);
        }

        Assert.True(session.CurrentSnapshot.Level100PlayerControlEnabled);
        return session;
    }

    private static InteractiveSession CreateFiringRangeSessionForWeaponChecks(
        Level100ActorDefinitionSet? definitions = null)
    {
        var session = new InteractiveSession(Seed, definitions ?? ActorDefinitions);
        while (session.CurrentSnapshot.Tick < FirstFlightSmokeScenario.DurationTicks)
        {
            session.ObserveInput(FirstFlightSmokeScenario.GetInputForTick(session.CurrentSnapshot.Tick));
            session.AdvanceFrameTicks(OneCoreStepTicks);
        }
        session.ObserveInput(InteractiveInput.Idle);
        Assert.True(session.CurrentSnapshot.Level100PulseCannonEnabled);
        return session;
    }

    private static InteractiveSession CreateTwinVulcanSessionForWeaponChecks()
    {
        InteractiveSession session = CreateFiringRangeSessionForWeaponChecks();
        // Focused input fixture: pose prior targets at the muzzle and shoot
        // their real contact meshes so their scripts select/enable the guns.
        // Pose facts make this preparation distinct from tutorial acceptance.
        CompleteGroup(Level100MissionTargetGroup.StaticTargets, 4);
        AdvanceUntil(state => state.Level100VulcanCannonEnabled &&
            state.Level100WalkerSelectedWeapon == Level100MissionWeapon.MechTwinVulcanCannon);
        return session;

        void CompleteGroup(Level100MissionTargetGroup group, int count)
        {
            AdvanceUntil(state => state.Level100Actors.Actors.Count(actor =>
                actor.TargetGroup == group && actor.Active && actor.IsObjective) == count);
            Level100ActorId[] targets = session.CurrentSnapshot.Level100Actors.Actors
                .Where(actor => actor.TargetGroup == group && actor.Active && actor.IsObjective)
                .Select(actor => actor.ActorId).ToArray();
            foreach (Level100ActorId targetId in targets)
            {
                int attempts = 0;
                while (session.CurrentSnapshot.Level100Actors.Actors.Single(actor =>
                    actor.ActorId == targetId).Lifecycle != Level100ActorLifecycle.Destroyed)
                {
                    WorldSnapshot state = session.CurrentSnapshot;
                    Assert.Equal(Level100MissionOutcome.Running, state.Level100Mission.Outcome);
                    Assert.True(attempts++ < 250, $"Contact fixture failed to destroy actor {targetId}.");
                    Level100DestructionSnapshot destruction = state.Level100Destruction.Actors.Single(
                        actor => actor.ActorId == targetId.Value);
                    Level100ContactDefinition definition = Level100ContactCatalog.Instance
                        .GetDefinition(destruction.DefinitionName);
                    Level100ContactPart part = definition.Parts.First(part =>
                        part.Collidable && destruction.PartActivity.Span[part.Index] != 0 &&
                        (definition.Kind != Level100DefinitionKind.Warehouse ||
                            destruction.CurrentHealthBits.Span[part.Index] != 0));
                    ReadOnlySpan<int> vertices = part.VerticesMillimeters.Span;
                    ReadOnlySpan<int> triangle = part.Triangles.Span;
                    int a = triangle[0] * 3, b = triangle[1] * 3, c = triangle[2] * 3;
                    int x = definition.Kind == Level100DefinitionKind.Warehouse
                        ? (vertices[a] + vertices[b] + vertices[c]) / 3 : part.Center.X;
                    int y = definition.Kind == Level100DefinitionKind.Warehouse
                        ? (vertices[a + 1] + vertices[b + 1] + vertices[c + 1]) / 3 : part.Center.Y;
                    int z = definition.Kind == Level100DefinitionKind.Warehouse
                        ? (vertices[a + 2] + vertices[b + 2] + vertices[c + 2]) / 3 : part.Center.Z;
                    Level100ActorPoseSnapshot tankAtMuzzle = PlaceTargetCenterAtPulseEmitter(state);
                    SimVector3 tankOrigin = tankAtMuzzle.PositionMillimeters;
                    var contactPose = tankAtMuzzle with
                    {
                        PositionMillimeters = new SimVector3(
                            tankOrigin.X + 43 - x,
                            tankOrigin.Y + 275 + z,
                            tankOrigin.Z - 228 - y),
                    };
                    session.ObserveInput(new InteractiveInput(0, 0, true, false, false));
                    session.ObserveInput(InteractiveInput.Idle);
                    session.AdvanceFrameTicks(OneCoreStepTicks,
                        [new Level100ActorPoseFact(targetId, contactPose)]);
                }
            }
        }

        void AdvanceUntil(Func<WorldSnapshot, bool> condition)
        {
            for (int step = 0; !condition(session.CurrentSnapshot) && step < 2_000; step++)
            {
                Assert.Equal(Level100MissionOutcome.Running, session.CurrentSnapshot.Level100Mission.Outcome);
                Assert.Equal(1, session.AdvanceFrameTicks(OneCoreStepTicks).StepsAdvanced);
            }
            Assert.True(condition(session.CurrentSnapshot),
                $"Weapon fixture setup stalled at tick {session.CurrentSnapshot.Tick}.");
        }
    }

    private static Level100ActorPoseSnapshot PlaceTargetCenterAtPulseEmitter(
        WorldSnapshot state)
    {
        // Gun 1 of cockpit2.msh (x right, y forward, z down, micrometres)
        // through yaw, nose-down pitch and roll.
        double yaw = state.FacingYawMicroRad / 1_000_000d;
        double pitch = state.FacingPitchMicroRad / 1_000_000d;
        double roll = state.BodyRollMicroRad / 1_000_000d;
        const double gunX = 0.092, gunY = 84.264, gunZ = -258.033;
        double forwardX = -Math.Sin(yaw) * Math.Cos(pitch), forwardY = -Math.Sin(pitch), forwardZ = Math.Cos(yaw) * Math.Cos(pitch);
        double baseUpX = -Math.Sin(pitch) * Math.Sin(yaw), baseUpY = Math.Cos(pitch), baseUpZ = Math.Sin(pitch) * Math.Cos(yaw);
        double rightX = (Math.Cos(yaw) * Math.Cos(roll)) + (baseUpX * Math.Sin(roll));
        double rightY = baseUpY * Math.Sin(roll);
        double rightZ = (Math.Sin(yaw) * Math.Cos(roll)) + (baseUpZ * Math.Sin(roll));
        double upX = (baseUpX * Math.Cos(roll)) - (Math.Cos(yaw) * Math.Sin(roll));
        double upY = baseUpY * Math.Cos(roll);
        double upZ = (baseUpZ * Math.Cos(roll)) - (Math.Sin(yaw) * Math.Sin(roll));
        int emitterOffsetX = (int)Math.Round((gunX * rightX) + (gunY * forwardX) - (gunZ * upX), MidpointRounding.AwayFromZero);
        int emitterOffsetZ = (int)Math.Round((gunX * rightZ) + (gunY * forwardZ) - (gunZ * upZ), MidpointRounding.AwayFromZero);
        int emitterVerticalOffset = (int)Math.Round((gunX * rightY) + (gunY * forwardY) - (gunZ * upY), MidpointRounding.AwayFromZero);
        var emitter = new SimVector3(
            state.PlayerPosition.X + emitterOffsetX,
            state.PlayerGroundElevationMillimeters +
                Level100Terrain.WalkerCenterOfGravityMillimeters +
                emitterVerticalOffset,
            state.PlayerPosition.Z + emitterOffsetZ);

        // The released Target Tank root broadphase center is contact-local
        // (43,-228,-275). Core is (retail X, up=-retail Z, retail Y).
        return new Level100ActorPoseSnapshot(
            new SimVector3(
                emitter.X - 43,
                emitter.Y - 275,
                emitter.Z + 228),
            new Level100FloatBasis3Bits(
                BitConverter.SingleToInt32Bits(1f), 0, 0,
                0, BitConverter.SingleToInt32Bits(1f), 0,
                0, 0, BitConverter.SingleToInt32Bits(1f)),
            SimVector3.Zero,
            SimVector3.Zero);
    }

    private static Level100ActorDefinitionSet LoadMaterializedActorDefinitions()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Level100",
            "StaticWorld",
            "level100-static-world.json");
        return Level100ActorDefinitionManifest.Decode(File.ReadAllBytes(path));
    }
}
