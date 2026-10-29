// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Client.Tests;

public sealed class Level100HudPresentationTests
{
    private const long OneCoreStepTicks = 500_000;
    private const uint Seed = 0x4F4E534Cu;

    /// <summary>
    /// A mission restart CLEARS the presentation-side delivery log.
    ///
    /// <para><c>Reset</c> restarts the released mission and its tick returns to
    /// the opening, but this state kept accumulating across it. Every message
    /// and help prompt from the previous run stayed in the log, so after a reset
    /// <c>ActiveAt</c> resolved against deliveries that had not happened yet in
    /// the run now on screen - the player saw a message from their last
    /// attempt.</para>
    ///
    /// <para>The signal is the mission tick going BACKWARDS, derived from
    /// simulation state rather than a reset notification, so it cannot be missed
    /// and Core needs no change. The assertion that the tick actually went
    /// backwards is load-bearing: without it this test would pass even if the
    /// reset never happened.</para>
    /// </summary>
    [Fact]
    public void MissionRestart_ClearsTheDeliveryLog()
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        var presentation = new Level100HudPresentationState();
        var playback = new Level100MessagePlaybackState(
            null, null, 0d, 0d, false, false);

        for (int tick = 0; tick < 400; tick++)
        {
            presentation.Consume(
                session.AdvanceFrameTicks(OneCoreStepTicks).Level100MissionEvents);
        }

        Level100HudSnapshot before = presentation.Project(
            session.CurrentSnapshot, playback);
        Assert.NotEmpty(before.DeliveredMessages);
        int tickBeforeReset = session.CurrentSnapshot.Level100Mission.Tick;
        Assert.True(tickBeforeReset > 0);

        session.ObserveInput(new InteractiveInput(0, 0, false, false, true));
        presentation.Consume(
            session.AdvanceFrameTicks(OneCoreStepTicks).Level100MissionEvents);
        session.ObserveInput(InteractiveInput.Idle);

        Assert.True(
            session.CurrentSnapshot.Level100Mission.Tick < tickBeforeReset,
            "the reset did not restart the mission, so this test proves nothing");

        Level100HudSnapshot after = presentation.Project(
            session.CurrentSnapshot, playback);
        Assert.Empty(after.DeliveredMessages);
        Assert.Empty(after.DeliveredHelp);
    }

    [Fact]
    public void ProductMissionPathProjectsOrderedMessagesAndCanonicalObjectives()
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        var presentation = new Level100HudPresentationState();
        var requestedMessages = new List<Level100MessageRequested>();

        Consume(session.AdvanceFrameTicks(0));
        for (int tick = 0;
             tick < 1_400 &&
             !string.Equals(
                 session.CurrentSnapshot.Level100Mission.NavigationObjective,
                 "Target Zone 1",
                 StringComparison.Ordinal);
             tick++)
        {
            Consume(session.AdvanceFrameTicks(OneCoreStepTicks));
        }

        Assert.Equal(
            "Target Zone 1",
            session.CurrentSnapshot.Level100Mission.NavigationObjective);

        // The objective is set on the tick TUTORIAL_13_MOD clears, and the
        // released message box does not start TUTORIAL_01 until
        // Level100MissionTiming.MessageAdvanceDelayTicks later. Step over that
        // gap so there is a message on screen to assert about.
        for (int tick = 0; tick <= Level100MissionTiming.MessageAdvanceDelayTicks; tick++)
        {
            Consume(session.AdvanceFrameTicks(OneCoreStepTicks));
        }

        Level100MessageRequested playing = requestedMessages[^1];
        Level100HudSnapshot hud = presentation.Project(
            session.CurrentSnapshot,
            new Level100MessagePlaybackState(
                playing.SpeakerId,
                playing.MessageId,
                PositionSeconds: 0.25d,
                LengthSeconds: 1d,
                Playing: true,
                Paused: false));

        Assert.Equal(
            requestedMessages.Select(message =>
                (message.Tick, message.SpeakerId, message.MessageId)),
            hud.DeliveredMessages.Select(message =>
                (message.Tick, (int)message.Speaker, message.MessageId)));

        // The active message is the one the CORE TICK schedule puts on screen,
        // not the one the mixer happens to be playing and not simply the most
        // recently requested. Retail queues: a message requested while another
        // is still speaking waits its turn, so at this tick the on-screen
        // message is an EARLIER member of the delivered list than
        // requestedMessages[^1]. Pinning it to the last request would have
        // pinned a behaviour the released game does not have.
        int missionTick = session.CurrentSnapshot.Level100Mission.Tick;
        Level100MessageScheduleEntry? scheduled =
            Level100MessageSchedule.ActiveAt(hud.DeliveredMessages, missionTick);
        Assert.NotNull(scheduled);
        Level100MessageScheduleEntry expected = scheduled!.Value;
        Assert.Equal(expected.Delivery.MessageId, hud.ActiveMessage?.MessageId);
        Assert.Contains(
            hud.DeliveredMessages,
            delivery => delivery.MessageId == hud.ActiveMessage?.MessageId);
        Assert.True(expected.StartTick <= missionTick);
        Assert.True(missionTick < expected.StartTick + expected.DurationTicks);

        Level100ActorSnapshot[] canonicalObjectives =
            session.CurrentSnapshot.Level100Actors.Actors
                .Where(actor =>
                    actor.Active &&
                    actor.IsObjective &&
                    actor.Lifecycle != Level100ActorLifecycle.Destroyed)
                .ToArray();
        Assert.NotEmpty(canonicalObjectives);
        Assert.Equal(
            canonicalObjectives.Select(actor => actor.ActorId),
            hud.Objectives.Select(objective => objective.ActorId));
        Assert.Equal(
            canonicalObjectives.Select(actor => actor.Pose.PositionMillimeters),
            hud.Objectives.Select(objective => objective.PositionMillimeters));
        Assert.Equal(
            canonicalObjectives.Select(actor => actor.Name),
            hud.Objectives.Select(objective => objective.ThingName));

        // Contacts are now projected from the live registry. The player's own
        // Battle Engine and the trigger volumes are excluded because neither is
        // a member of retail's unit list at 0x008550d0; every remaining
        // active, undestroyed actor is a contact.
        Assert.NotEmpty(hud.Contacts);
        Assert.DoesNotContain(
            hud.Contacts,
            contact => contact.Id == session.CurrentSnapshot.Level100Actors.Actors
                .Single(actor => StringComparer.Ordinal.Equals(actor.Name, "Player 1"))
                .ActorId.Value);
        Assert.Equal(
            session.CurrentSnapshot.Level100Actors.Actors
                .Where(actor =>
                    actor.Active &&
                    actor.Lifecycle != Level100ActorLifecycle.Destroyed &&
                    !actor.Trigger.HasValue &&
                    !StringComparer.Ordinal.Equals(actor.Name, "Player 1"))
                .Select(actor => actor.ActorId.Value),
            hud.Contacts.Select(contact => contact.Id));

        Assert.Empty(hud.Threats);
        Assert.Empty(hud.DamageFlashes);
        Assert.Null(hud.Target);
        Assert.Equal(Level100HudWeapon.PulseCannon, hud.Weapon.SelectedWeapon);
        Assert.Null(hud.Weapon.PulseHeatPermille);
        Assert.Null(hud.Weapon.VulcanAmmo);
        Assert.True(hud.BattleLine.HasInfluenceValues);
        Assert.Equal(Level100HudInfluenceMapState.Populated, hud.BattleLine.InfluenceMap);
        Assert.Equal(Level100HudInfluenceMap.Nodes.Count, hud.BattleLine.InfluencePermille.Count);

        Level100ActorId removedObjective = canonicalObjectives[0].ActorId;
        FrameAdvanceResult destruction = session.AdvanceFrameTicks(
            OneCoreStepTicks,
            [new Level100ActorDiedFact(removedObjective)]);
        Consume(destruction);
        Level100HudSnapshot afterDestruction = presentation.Project(
            destruction.CurrentSnapshot,
            default);
        Assert.DoesNotContain(
            afterDestruction.Objectives,
            objective => objective.ActorId == removedObjective);

        void Consume(FrameAdvanceResult frame)
        {
            presentation.Consume(frame.Level100MissionEvents);
            requestedMessages.AddRange(
                frame.Level100MissionEvents.OfType<Level100MessageRequested>());
        }
    }

    [Fact]
    public void ProjectionPublishesSignedNodeInfluenceAndSelectsTheUnobscuredOverlay()
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        WorldSnapshot baseline = session.CurrentSnapshot;
        Level100ActorSnapshot[] contributors = baseline.Level100Actors.Actors
            .Where(actor =>
                actor.Active &&
                actor.Lifecycle != Level100ActorLifecycle.Destroyed &&
                !actor.Trigger.HasValue)
            .DistinctBy(actor => actor.DefinitionIdentity)
            .Take(2)
            .ToArray();
        Assert.Equal(2, contributors.Length);

        Level100HudInfluenceNode node = Level100HudInfluenceMap.Nodes[0];
        Level100ActorSnapshot friendly = contributors[0] with
        {
            Pose = contributors[0].Pose with
            {
                PositionMillimeters = new SimVector3(node.Position.X, 0, node.Position.Z),
            },
        };
        Level100ActorSnapshot enemyFarAway = contributors[1] with
        {
            Pose = contributors[1].Pose with
            {
                PositionMillimeters = new SimVector3(1_000_000, 0, 1_000_000),
            },
        };
        var allegiance = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [friendly.DefinitionIdentity] = (int)Level100HudAllegiance.Friendly,
            [enemyFarAway.DefinitionIdentity] = (int)Level100HudAllegiance.Enemy,
        };
        var presentation = new Level100HudPresentationState(allegiance);
        WorldSnapshot friendlyOnly = baseline with
        {
            Level100Actors = baseline.Level100Actors with
            {
                Actors = Array.AsReadOnly(new[] { friendly, enemyFarAway }),
            },
        };

        Level100HudSnapshot first = presentation.Project(friendlyOnly, default);
        Assert.True(first.BattleLine.HasInfluenceValues);
        Assert.Equal(Level100HudInfluenceMapState.Populated, first.BattleLine.InfluenceMap);
        Assert.Equal(Level100HudInfluenceMap.Nodes.Count, first.BattleLine.InfluencePermille.Count);
        Assert.Equal(1_000, first.BattleLine.InfluencePermille[0]);
        Assert.Equal(
            Level100HudLowerRightSocket.InfluenceOverlay,
            Level100HudLowerRightSocketLaw.Select(
                messageBoxHoldsActiveMessage: false,
                first.BattleLine.InfluenceMap));

        Level100ActorSnapshot enemyAtNode = enemyFarAway with
        {
            Pose = enemyFarAway.Pose with
            {
                PositionMillimeters = new SimVector3(node.Position.X, 0, node.Position.Z),
            },
        };
        WorldSnapshot contested = friendlyOnly with
        {
            Level100Actors = friendlyOnly.Level100Actors with
            {
                Actors = Array.AsReadOnly(new[] { friendly, enemyAtNode }),
            },
        };
        Level100HudSnapshot second = presentation.Project(contested, default);
        Assert.Equal(0, second.BattleLine.InfluencePermille[0]);
        Assert.NotEqual(
            first.BattleLine.InfluencePermille[0],
            second.BattleLine.InfluencePermille[0]);

        Level100ActorSnapshot friendlyFarAway = friendly with
        {
            Pose = friendly.Pose with
            {
                PositionMillimeters = new SimVector3(1_000_000, 0, 1_000_000),
            },
        };
        var unrelatedCommandState = new Level100ActorCommandIntentSnapshot(
            enemyAtNode.ActorId,
            AiState: 2,
            Allegiance: (int)Level100HudAllegiance.Friendly,
            HasAllegianceOverride: false,
            Level100ActorCommandIntent.Stopped,
            TargetActorId: null,
            WaypointPath: null,
            WaypointNodeIndex: null,
            WaypointCommandScalar: 0,
            WaitForWaypointCompletion: false,
            GroundFullGuideBaseTickPhase: 0);
        WorldSnapshot authoredEnemy = contested with
        {
            Level100Actors = contested.Level100Actors with
            {
                Actors = Array.AsReadOnly(new[] { friendlyFarAway, enemyAtNode }),
            },
            Level100ActorMechanics = contested.Level100ActorMechanics with
            {
                Actors = Array.AsReadOnly(new[] { unrelatedCommandState }),
            },
        };
        Level100HudSnapshot third = presentation.Project(authoredEnemy, default);
        Assert.Equal(-1_000, third.BattleLine.InfluencePermille[0]);
        Assert.Equal(
            -1_000,
            third.BattleLine.InfluencePermille[
                Level100HudInfluenceMap.Nodes.Single(item => item.Id == 9).Id]);

        WorldSnapshot explicitFriendly = authoredEnemy with
        {
            Level100ActorMechanics = authoredEnemy.Level100ActorMechanics with
            {
                Actors = Array.AsReadOnly(new[]
                {
                    unrelatedCommandState with { HasAllegianceOverride = true },
                }),
            },
        };
        Level100HudSnapshot fourth = presentation.Project(explicitFriendly, default);
        Assert.Equal(1_000, fourth.BattleLine.InfluencePermille[0]);
    }

    [Fact]
    public void ProjectionPublishesOnlyPositiveIntensityDamageFlashesInListOrder()
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        WorldSnapshot snapshot = session.CurrentSnapshot with
        {
            Tick = 100,
            Level100DamageFlashes = Array.AsReadOnly(new[]
            {
                new Level100DamageFlashSnapshot(111_000, 100),
                new Level100DamageFlashSnapshot(-222_000, 61),
                new Level100DamageFlashSnapshot(333_000, 60),
            }),
        };

        Level100HudSnapshot hud =
            new Level100HudPresentationState().Project(snapshot, default);

        Assert.Equal(2, hud.DamageFlashes.Count);
        Assert.Equal(
            new Level100HudDamageFlashSnapshot(
                111_000,
                SimulationConstants.Level100DamageFlashLifetimeTicks),
            hud.DamageFlashes[0]);
        Assert.Equal(
            new Level100HudDamageFlashSnapshot(-222_000, 1),
            hud.DamageFlashes[1]);
    }

    [Theory]
    [InlineData(
        VehicleMode.Walker,
        Level100MissionWeapon.PulseCannonPod,
        Level100HudWeapon.PulseCannon)]
    [InlineData(
        VehicleMode.Walker,
        Level100MissionWeapon.MechTwinVulcanCannon,
        Level100HudWeapon.VulcanCannon)]
    [InlineData(
        VehicleMode.Jet,
        Level100MissionWeapon.MechVulcanCannon,
        Level100HudWeapon.VulcanCannon)]
    [InlineData(VehicleMode.Jet, Level100MissionWeapon.MissilePod, null)]
    public void ProjectionUsesTheCurrentModeSelectedWeaponForRetainedHudIcons(
        VehicleMode mode,
        Level100MissionWeapon selectedMissionWeapon,
        Level100HudWeapon? expectedHudWeapon)
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        WorldSnapshot baseline = session.CurrentSnapshot;
        WorldSnapshot snapshot = baseline with
        {
            Mode = mode,
            Level100WalkerSelectedWeapon = mode == VehicleMode.Walker
                ? selectedMissionWeapon
                : baseline.Level100WalkerSelectedWeapon,
            Level100JetSelectedWeapon = mode == VehicleMode.Jet
                ? selectedMissionWeapon
                : baseline.Level100JetSelectedWeapon,
        };

        Level100HudSnapshot hud = new Level100HudPresentationState().Project(
            snapshot,
            default);

        Assert.Equal(expectedHudWeapon, hud.Weapon.SelectedWeapon);
    }

    [Theory]
    [InlineData(
        Level100MissionOutcome.Running,
        Level100MissionTerminalState.None,
        Level100MissionFailureReason.None,
        0,
        0,
        false)]
    [InlineData(
        Level100MissionOutcome.Won,
        Level100MissionTerminalState.SuccessCountdown,
        Level100MissionFailureReason.None,
        100,
        100,
        true)]
    [InlineData(
        Level100MissionOutcome.Won,
        Level100MissionTerminalState.FrontEndHandoffReady,
        Level100MissionFailureReason.None,
        0,
        0,
        false)]
    [InlineData(
        Level100MissionOutcome.Lost,
        Level100MissionTerminalState.FailureCountdown,
        Level100MissionFailureReason.TutorialBroken,
        40,
        40,
        true)]
    [InlineData(
        Level100MissionOutcome.Lost,
        Level100MissionTerminalState.FailureMenuReady,
        Level100MissionFailureReason.PlayerDeath,
        290,
        30,
        true)]
    [InlineData(
        Level100MissionOutcome.Lost,
        Level100MissionTerminalState.FailureCountdown,
        Level100MissionFailureReason.WaterLoss,
        261,
        1,
        true)]
    [InlineData(
        Level100MissionOutcome.Lost,
        Level100MissionTerminalState.FailureMenuReady,
        Level100MissionFailureReason.WaterLoss,
        260,
        0,
        false)]
    [InlineData(
        Level100MissionOutcome.Lost,
        Level100MissionTerminalState.FailureCountdownElapsed,
        Level100MissionFailureReason.WaterLoss,
        0,
        0,
        false)]
    public void ProjectionExposesRetailTerminalOverlayOnlyWhileCountdownRemains(
        Level100MissionOutcome outcome,
        Level100MissionTerminalState terminalState,
        Level100MissionFailureReason failureReason,
        int ticksRemaining,
        int expectedOverlayTicksRemaining,
        bool expectedVisible)
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        WorldSnapshot baseline = session.CurrentSnapshot;
        WorldSnapshot snapshot = baseline with
        {
            Level100Mission = baseline.Level100Mission with
            {
                Outcome = outcome,
                TerminalState = terminalState,
                FailureReason = failureReason,
                TerminalTicksRemaining = ticksRemaining,
            },
        };

        Level100HudTerminalSnapshot terminal =
            new Level100HudPresentationState().Project(snapshot, default).Terminal;

        Assert.Equal(expectedVisible, terminal.Visible);
        Assert.Equal(outcome, terminal.Outcome);
        Assert.Equal(failureReason, terminal.FailureReason);
        Assert.Equal(expectedOverlayTicksRemaining, terminal.TicksRemaining);
    }

    [Fact]
    public void ProjectionPreservesRepeatedHelpAndIgnoresPlaybackIdentity()
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        var presentation = new Level100HudPresentationState();
        FrameAdvanceResult initial = session.AdvanceFrameTicks(0);
        presentation.Consume(initial.Level100MissionEvents);
        // The released message box holds every message until
        // Level100MissionTiming.MessageBoxAllowedTick, so the greeting arrives
        // after the opening pan rather than on tick 0.
        Level100MessageRequested? first = null;
        FrameAdvanceResult onScreen = initial;
        while (first is null)
        {
            onScreen = session.AdvanceFrameTicks(OneCoreStepTicks);
            presentation.Consume(onScreen.Level100MissionEvents);
            first = onScreen.Level100MissionEvents
                .OfType<Level100MessageRequested>()
                .FirstOrDefault();
        }

        Level100MessageRequested message = first;
        presentation.Consume(
        [
            new Level100HelpRequested(
                initial.CurrentSnapshot.Tick,
                (int)Level100HudHelpPrompt.Fire),
            new Level100HelpRequested(
                initial.CurrentSnapshot.Tick,
                (int)Level100HudHelpPrompt.Transform),
            new Level100HelpRequested(
                initial.CurrentSnapshot.Tick,
                (int)Level100HudHelpPrompt.Fire),
        ]);

        // Every one of these is a state the audio mixer could be in at this
        // tick, including one naming the WRONG speaker for this message and one
        // naming no message at all. None of them may change what the HUD shows:
        // the active message is a function of the Core tick alone. The previous
        // contract - "the active message is whatever playback names, matched by
        // speaker identity" - is what made the message panel and the
        // portrait/compass region disagree by 21-25 % material between two
        // captures of the SAME commit, because playback.PositionSeconds is the
        // audio mixer's wall clock and a capture's frames are keyed on Core
        // ticks. See Level100MessageSchedule.
        Level100MessagePlaybackState[] mixerStates =
        [
            default,
            new(
                ActiveSpeakerId: (int)Level100HudSpeaker.Kramer,
                ActiveMessageId: message.MessageId,
                PositionSeconds: 0d,
                LengthSeconds: 1d,
                Playing: true,
                Paused: false),
            new(
                ActiveSpeakerId: message.SpeakerId,
                ActiveMessageId: message.MessageId,
                PositionSeconds: 0.75d,
                LengthSeconds: 1d,
                Playing: true,
                Paused: true),
        ];

        foreach (Level100MessagePlaybackState mixer in mixerStates)
        {
            Level100HudSnapshot hud = presentation.Project(
                onScreen.CurrentSnapshot,
                mixer);

            Assert.Equal(
                [
                    Level100HudHelpPrompt.Fire,
                    Level100HudHelpPrompt.Transform,
                    Level100HudHelpPrompt.Fire,
                ],
                hud.DeliveredHelp);
            // This is the message's own first tick, so it is on screen
            // regardless of what the mixer says.
            Assert.Equal(message.MessageId, hud.ActiveMessage?.MessageId);
            Assert.Equal(message.SpeakerId, (int?)hud.ActiveMessage?.Speaker);
        }
    }

    // The First Flight smoke used to pin level100PlayingMessageId, a value read
    // straight off the Godot audio mixer, and it failed on a loaded host while
    // the Core stateHash stayed byte-identical. The smoke now pins
    // DeliveredMessages instead, so the property that makes that legitimate --
    // that the delivered sequence is a function of the Core event stream alone
    // and never of playback -- is pinned here rather than assumed.
    [Fact]
    public void DeliveredMessagesAreIndependentOfPlaybackState()
    {
        var session = new InteractiveSession(
            Seed,
            Level100TestActorDefinitions.Create());
        var presentation = new Level100HudPresentationState();
        var requestedMessages = new List<Level100MessageRequested>();

        FrameAdvanceResult frame = session.AdvanceFrameTicks(0);
        presentation.Consume(frame.Level100MissionEvents);
        requestedMessages.AddRange(
            frame.Level100MissionEvents.OfType<Level100MessageRequested>());
        for (int step = 0; step < 400; step++)
        {
            frame = session.AdvanceFrameTicks(OneCoreStepTicks);
            presentation.Consume(frame.Level100MissionEvents);
            requestedMessages.AddRange(
                frame.Level100MissionEvents.OfType<Level100MessageRequested>());
        }

        Assert.True(requestedMessages.Count > 1);
        int[] expected = requestedMessages
            .Select(message => message.MessageId)
            .ToArray();

        // Every playback state a mixer can be in at this one tick: silent, on
        // the first message, on the last, and paused mid-stream. None of them
        // may change the delivered sequence.
        Level100MessagePlaybackState[] playbackStates =
        [
            default,
            new(
                requestedMessages[0].SpeakerId,
                requestedMessages[0].MessageId,
                PositionSeconds: 0d,
                LengthSeconds: 2d,
                Playing: true,
                Paused: false),
            new(
                requestedMessages[^1].SpeakerId,
                requestedMessages[^1].MessageId,
                PositionSeconds: 1.75d,
                LengthSeconds: 2d,
                Playing: true,
                Paused: false),
            new(
                requestedMessages[^1].SpeakerId,
                requestedMessages[^1].MessageId,
                PositionSeconds: 0.5d,
                LengthSeconds: 2d,
                Playing: true,
                Paused: true),
        ];

        foreach (Level100MessagePlaybackState playback in playbackStates)
        {
            Level100HudSnapshot hud = presentation.Project(
                session.CurrentSnapshot,
                playback);
            Assert.Equal(
                expected,
                hud.DeliveredMessages.Select(delivery => delivery.MessageId));
        }
    }

    // ---------------------------------------------------------------------
    // Message panel: retail behaviour measured off the 640x480 gameplay
    // captures in a private retail reference capture.
    // Every expectation below is a transcription of a specific frame, not a
    // restatement of the code under test. The working is written up in
    // the private lab note HUD-MESSAGE-PANEL-2026-07-26.md.
    // ---------------------------------------------------------------------

    private const string Hud02 =
        "This is the threat circle. That notch indicates North. As for its " +
        "other functions, I'll demonstrate them later.";

    private const string Hud06 =
        "The circle to the left is your scanner. Enemy units show up in red, " +
        "friendly units in blue.";

    private const string MessageLog =
        "If you ever need to review these messages, check out Aquila's " +
        "message log in the Pause Menu.";

    private const string Tutorial13 =
        "You have two primary controls.  One determines the direction of " +
        "travel, and the other changes which way Aquila faces.";

    private const string Tutorial01 =
        "Okay, Hawk? I want you to manoeuvre the Battle Engine to the area " +
        "marked on your HUD.";

    [Theory]
    // opening-pan-run1/level100-t013269ms.png + .../t016011ms.png
    [InlineData(
        Hud02,
        new[]
        {
            "This is the threat",
            "circle. That notch",
            "indicates North. As for",
            "its other functions, I'll",
            "demonstrate them later.",
        })]
    // hud-timeline-run1/level100-t020080ms.png + .../t022080ms.png - the
    // message the reported defect overflowed the panel with.
    [InlineData(
        Hud06,
        new[]
        {
            "The circle to the left is",
            "your scanner. Enemy units",
            "show up in red, friendly",
            "units in blue.",
        })]
    // hud-timeline-run1/level100-t026073ms.png + .../t028057ms.png
    [InlineData(
        MessageLog,
        new[]
        {
            "If you ever need to",
            "review these messages,",
            "check out Aquila's",
            "message log in the Pause",
            "Menu.",
        })]
    // hud-timeline-run1/level100-t035064ms.png + .../t037063ms.png. Note the
    // double space the released text carries inside "controls.  One": retail
    // keeps it, so the wrap must not collapse whitespace.
    [InlineData(
        Tutorial13,
        new[]
        {
            "You have two primary",
            "controls.  One determines",
            "the direction of travel,",
            "and the other changes",
            "which way Aquila faces.",
        })]
    // hud-timeline-run1/level100-t042062ms.png
    [InlineData(
        Tutorial01,
        new[]
        {
            "Okay, Hawk? I want you to",
            "manoeuvre the Battle",
            "Engine to the area marked",
            "on your HUD.",
        })]
    // hud-timeline-run1/level100-t032071ms.png
    [InlineData("All systems nominal.", new[] { "All systems nominal." })]
    public void MessageWrapReproducesEveryCapturedRetailLineBreak(
        string text,
        string[] expected)
    {
        Assert.Equal(
            expected,
            Level100MessagePanel.Wrap(text).Select(line => line.Text));
    }

    [Fact]
    public void MessageWrapWalksTheSourceStringExactly()
    {
        foreach (string text in new[] { Hud02, Hud06, MessageLog, Tutorial13, Tutorial01 })
        {
            IReadOnlyList<Level100MessageLine> lines = Level100MessagePanel.Wrap(text);
            Assert.Equal(text.Length, Level100MessagePanel.SourceLength(lines));
        }
    }

    [Fact]
    public void RetailWrapsMessagesByColumnAndNotByPixelWidth()
    {
        // This is the measurement that decides the whole layout. Retail renders
        // "your scanner. Enemy units" (25 columns) unbroken in
        // hud-timeline-run1/level100-t022080ms.png at an ink span of 220px, and
        // breaks "This is the threat circle." (26 columns, 214px advance) in
        // opening-pan-run1/level100-t013011ms.png. A pixel-width wrap would have
        // to satisfy W >= 224 and W < 214 at once, so no pixel width explains
        // the captures and the wrap is by column.
        Assert.Equal(25, Level100MessagePanel.WrapColumns);
        Assert.All(
            new[] { Hud02, Hud06, MessageLog, Tutorial13, Tutorial01 },
            text => Assert.All(
                Level100MessagePanel.Wrap(text),
                line => Assert.True(
                    line.Text.Length <= Level100MessagePanel.WrapColumns,
                    $"'{line.Text}' is {line.Text.Length} columns.")));

        // ... and the limit is exactly 25, not merely at least 25: four
        // captured lines are 25 columns wide.
        Assert.Contains(
            Level100MessagePanel.Wrap(Hud06),
            line => line.Text.Length == Level100MessagePanel.WrapColumns);
    }

    [Theory]
    // Frame -> (characters of the source visible in that frame, the three lines
    // the frame shows). Both columns are transcribed from the capture; the
    // character count is just the length of the visible prefix.
    //
    // opening-pan-run1, HUD_02:
    [InlineData(Hud02, 42, new[] { "This is the threat", "circle. That notch", "indi" })]
    [InlineData(Hud02, 54, new[] { "This is the threat", "circle. That notch", "indicates North." })]
    // t013761: the window has scrolled up exactly one line - line 1 is gone and
    // line 3 is now the TOP line. That is what rules out paging: a pager would
    // have dropped line 3 along with lines 1 and 2.
    [InlineData(Hud02, 71, new[] { "circle. That notch", "indicates North. As for", "its other" })]
    // t014260: scrolled one more line, again by exactly one.
    [InlineData(Hud02, 92, new[] { "indicates North. As for", "its other functions, I'll", "demo" })]
    // t014762 through t018060: fully typed, and it rests on these three lines.
    [InlineData(
        Hud02,
        111,
        new[] { "indicates North. As for", "its other functions, I'll", "demonstrate them later." })]
    // hud-timeline-run1, HUD_06 - the message from the defect report. The panel
    // is empty at t019074, mid-word at t020080 and t021073, settled at t022080.
    [InlineData(Hud06, 38, new[] { "The circle to the left is", "your scanner" })]
    [InlineData(
        Hud06,
        78,
        new[] { "your scanner. Enemy units", "show up in red, friendly", "u" })]
    [InlineData(
        Hud06,
        91,
        new[] { "your scanner. Enemy units", "show up in red, friendly", "units in blue." })]
    public void MessageWindowMatchesEveryCapturedTypeOnFrame(
        string text,
        int revealedCharacters,
        string[] expected)
    {
        Assert.Equal(
            expected,
            Level100MessagePanel.Window(
                Level100MessagePanel.Wrap(text),
                revealedCharacters));
    }

    [Theory]
    // The same frames again, this time pinning the CLOCK rather than the
    // window. The reveal t0 of each message is the least squares intercept of
    // its own samples (HUD_02 11.947s, HUD_06 19.137s on the capture's level
    // clock), and the frames are sampled ~250ms apart, so a 40 char/s reveal
    // has to land within a few characters of the transcribed count - not on it.
    [InlineData(13.011d - 11.947d, 42)]
    [InlineData(13.269d - 11.947d, 54)]
    [InlineData(13.761d - 11.947d, 71)]
    [InlineData(14.260d - 11.947d, 92)]
    [InlineData(20.080d - 19.137d, 38)]
    [InlineData(21.073d - 19.137d, 78)]
    public void TypeOnClockReproducesEveryCapturedRevealWithinSamplingError(
        double elapsedSeconds,
        int capturedCharacters)
    {
        int revealed = Level100MessagePanel.RevealedCharacters(elapsedSeconds);
        Assert.InRange(revealed, capturedCharacters - 3, capturedCharacters + 3);
    }

    [Theory]
    // Both messages are fully typed by their last mid-reveal frame's successor.
    [InlineData(Hud02, 14.762d - 11.947d)]
    [InlineData(Hud02, 18.060d - 11.947d)]
    [InlineData(Hud06, 22.080d - 19.137d)]
    public void MessagesAreFullyRevealedByTheirSettledFrame(
        string text,
        double elapsedSeconds)
    {
        IReadOnlyList<Level100MessageLine> lines = Level100MessagePanel.Wrap(text);
        Assert.True(
            Level100MessagePanel.RevealedCharacters(elapsedSeconds) >=
                Level100MessagePanel.SourceLength(lines));
    }

    [Fact]
    public void MessageWindowNeverExceedsTheThreeCapturedLines()
    {
        // No captured frame ever shows a fourth line. The reported defect was a
        // four-line render spilling out of the panel art, so this is the
        // regression guard: sweep the whole reveal of every message in the
        // released text table.
        foreach (string text in new[] { Hud02, Hud06, MessageLog, Tutorial13, Tutorial01 })
        {
            IReadOnlyList<Level100MessageLine> lines = Level100MessagePanel.Wrap(text);
            for (int revealed = 0;
                 revealed <= Level100MessagePanel.SourceLength(lines) + 40;
                 revealed++)
            {
                IReadOnlyList<string> window = Level100MessagePanel.Window(lines, revealed);
                Assert.InRange(window.Count, 1, Level100MessagePanel.VisibleLines);
                Assert.All(
                    window,
                    line => Assert.True(
                        line.Length <= Level100MessagePanel.WrapColumns,
                        $"'{line}' is {line.Length} columns."));
            }
        }
    }

    [Fact]
    public void MessageWindowScrollsOneLineAtATimeAndNeverPages()
    {
        // Paging would make the top line jump by VisibleLines; retail's does
        // not. Walk the whole reveal and assert the top line only ever advances
        // to the next wrapped line.
        IReadOnlyList<Level100MessageLine> lines = Level100MessagePanel.Wrap(MessageLog);
        int total = Level100MessagePanel.SourceLength(lines);
        int expectedTop = 0;
        for (int revealed = 0; revealed <= total; revealed++)
        {
            IReadOnlyList<string> window = Level100MessagePanel.Window(lines, revealed);
            int top = window.Count < Level100MessagePanel.VisibleLines
                ? 0
                : IndexOfWindowTop(lines, window);
            Assert.InRange(top, expectedTop, expectedTop + 1);
            expectedTop = top;
        }
        Assert.Equal(lines.Count - Level100MessagePanel.VisibleLines, expectedTop);

        static int IndexOfWindowTop(
            IReadOnlyList<Level100MessageLine> lines,
            IReadOnlyList<string> window)
        {
            for (int index = 0; index + window.Count <= lines.Count; index++)
            {
                if (string.Equals(lines[index].Text, window[0], StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }
    }

    [Fact]
    public void TypeOnRunsAtTheMeasuredFortyCharactersPerSecond()
    {
        // Least squares over the seven HUD_02 samples gives 39.67 char/s and
        // the two HUD_06 samples give 40.28; both round to 40.
        Assert.Equal(40d, Level100MessagePanel.CharactersPerSecond);
        Assert.Equal(0, Level100MessagePanel.RevealedCharacters(0d));
        Assert.Equal(0, Level100MessagePanel.RevealedCharacters(-5d));
        Assert.Equal(40, Level100MessagePanel.RevealedCharacters(1d));
        Assert.Equal(100, Level100MessagePanel.RevealedCharacters(2.5d));
    }

    [Fact]
    public void ThreeMessageLinesAreCentredInTheMeasuredRetailPanelBody()
    {
        // Measured off opening-pan-run1/level100-t016011ms.png: the three white
        // glyph cells start on rows 412, 427 and 442, and the shadow sits one
        // pixel down-right of the white glyph, so the pen tops are 413/428/443.
        //
        // HELD against the d3d9 draw log's (203.5, 411.5) on 2026-07-27, which
        // is the vertex retail issues rather than the row it rasterises to.
        // Capturing this client at 411.5 puts its glyph ink two rows above
        // retail's; at 413 the rows coincide exactly. See
        // Level100MessagePanel.TextPenLeft.
        Assert.Equal(413f, Level100MessagePanel.FirstLinePenTop);
        Assert.Equal(15f, Level100MessagePanel.LineHeightPixels);
        Assert.Equal(206f, Level100MessagePanel.TextPenLeft);

        const float glyphCell = 16f;
        float whiteTop = Level100MessagePanel.FirstLinePenTop - 1f;
        float whiteBottom = whiteTop +
            ((Level100MessagePanel.VisibleLines - 1) *
                Level100MessagePanel.LineHeightPixels) +
            glyphCell;
        Assert.Equal(412f, whiteTop);
        Assert.Equal(458f, whiteBottom);

        // The block is centred in the panel body this HUD already pins.
        Assert.Equal(
            (Level100MessagePanel.PanelBodyTop + Level100MessagePanel.PanelBodyBottom) * 0.5f,
            (whiteTop + whiteBottom) * 0.5f,
            0.5f);

        // ... and it fits inside it, which the reported defect did not: the old
        // constants started the first line at y 387, 18px above the panel top.
        Assert.True(whiteTop >= Level100MessagePanel.PanelBodyTop);
        Assert.True(whiteBottom <= Level100MessagePanel.PanelBodyBottom);
    }
}
