// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The released opening-pan skip, asserted as a law rather than as the presence
/// of a flag.
/// </summary>
/// <remarks>
/// <para>
/// <c>CPlayer::ReceiveButtonAction</c>
/// (<c>references/Onslaught/Player.cpp:283-319</c>, retail
/// <c>CPlayer__ReceiveButtonAction</c> at <c>0x004D3110</c>) puts the skip
/// ABOVE the movement gate, and that placement is the whole mechanism:
/// </para>
/// <code>
///   if (button == BUTTON_SKIP_PANNING &amp;&amp; mCurrentViewMode == PLAYER_PAN_VIEW
///       &amp;&amp; GAME.GetGameState() == GAME_STATE_PANNING)          // Player.cpp:311
///   {
///       GotoControlView() ;                                     // :313, client
///       GAME.StartPlayingState() ;                              // :314, Core
///   }
///   if (GAME.GetGameState() &lt; GAME_STATE_PLAYING) return ;       // :319
/// </code>
/// <para>
/// A wrong implementation gets one of three things wrong: it honours the skip
/// outside the pan, it lets other input through on the pan, or it leaves the
/// message schedule where an unskipped pan would have put it. Each has its own
/// test here.
/// </para>
/// <para>
/// <b>The "other input does nothing" half has to be asserted on a RETURNING
/// career, and the reason is in the shipped script.</b> On a cold first career
/// <c>init()</c> takes the <c>GetSlot(SLOT_TUTORIAL_1) == FALSE</c> branch and
/// calls <c>player.Deactivate()</c> (<c>LevelScript.msl:51-52</c>), so the
/// player is inactive for the whole pan and for another 800 ticks after it —
/// which means a cold-career test of "input during the pan does nothing" is
/// satisfied by the ACTIVATION gate and can never detect a broken PAN gate.
/// That is not hypothetical: an adversarial review on 2026-07-27 deleted
/// <c>_level100OpeningTicksRemaining == 0</c> from <c>Simulation.Step</c>
/// outright and every test in this file still passed. On a returning career
/// (<c>Level100TutorialProgress.Introduction</c>) <c>init()</c> takes the other
/// branch, never Deactivates, and the player is active from tick 0 — so the pan
/// gate is the only thing holding them still, and deleting it fails
/// <see cref="NoInputDoesAnythingDuringThePan_OnAReturningCareerWherePlayerIsActive"/>
/// immediately.
/// </para>
/// </remarks>
public sealed class Level100SkipPanningTests
{
    private const int SkipTick = 5;

    private static Simulation Create() =>
        new(1, Level100TestActorDefinitions.Create());

    /// <summary>
    /// The returning career: all four <c>SLOT_TUTORIAL_*</c> saved, which is
    /// what <c>Level100FullChainTests.ChainAutopilot_ReachesWonByInputAlone</c>
    /// runs. <c>init()</c> skips its whole introduction block, so the player is
    /// never deactivated and the opening pan is the only thing gating control.
    /// </summary>
    private static Simulation CreateReturningCareer() => new(
        1,
        Level100TestActorDefinitions.Create(),
        new Level100TutorialProgress(true, true, true, true));

    private static SimInput Skip => new(0, 0, SimActions.SkipPanning);

    /// <summary>
    /// Everything a player could physically be holding while the pan runs.
    /// Every one of these is dispatched BEFORE <c>BUTTON_SKIP_PANNING</c> in
    /// the shipped table (rows 0-15 against rows 22-25), so retail rejects all
    /// of it at <c>Player.cpp:319</c> even on the frame the pan is skipped.
    /// </summary>
    private static SimInput Everything(SimActions extra = SimActions.None) => new(
        1,
        1,
        SimActions.Fire | SimActions.ToggleMode | SimActions.LandingJets | extra,
        LookX: 1,
        LookY: 1,
        LookXAnalogPermille: 1_000,
        LookYAnalogPermille: 1_000);

    [Fact]
    public void SkipDuringPanning_EndsThePanAndStartsPlayingState()
    {
        Simulation simulation = Create();

        for (int tick = 1; tick < SkipTick; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        Assert.Equal(
            SimulationConstants.Level100OpeningPanTicks - (SkipTick - 1),
            simulation.Snapshot.Level100OpeningTicksRemaining);

        WorldSnapshot skipped = simulation.Step(Skip);

        Assert.Equal(SkipTick, skipped.Tick);
        Assert.Equal(0, skipped.Level100OpeningTicksRemaining);
        // CGame::StartPlayingState posts ALLOWED_TO_PLAY_MESSAGES NEXT_FRAME
        // (game.cpp:3030), so the gate is the skip tick plus one released
        // 20 Hz event frame, on the mission clock that the pre-run started.
        Assert.Equal(
            SimulationConstants.Level100PreRunTicks + SkipTick + Level100MissionTiming.ReleasedEventFrameTicks,
            skipped.Level100Mission.MessageBoxAllowedTick);
    }

    /// <summary>
    /// The pan gate, isolated. This is the test that fails when
    /// <c>_level100OpeningTicksRemaining == 0</c> is deleted from
    /// <c>Simulation.Step</c>; the cold-career version below does not, and
    /// cannot.
    /// </summary>
    [Fact]
    public void NoInputDoesAnythingDuringThePan_OnAReturningCareerWherePlayerIsActive()
    {
        Simulation control = CreateReturningCareer();
        Simulation loud = CreateReturningCareer();

        // ANTI-VACUITY GUARD. If the player is not active here then the
        // activation gate is closed too, this test is being satisfied by the
        // wrong term, and the pan gate is once again untested. Fail loudly
        // rather than pass quietly.
        Assert.True(
            control.Snapshot.Level100PlayerActive,
            "A returning career must start with the player ACTIVE - the shipped " +
            "init() only Deactivates inside the GetSlot(SLOT_TUTORIAL_1) == FALSE " +
            "branch. Without that, this test cannot detect a broken pan gate.");
        Assert.False(control.Snapshot.Level100PlayerControlEnabled);

        // Every action and axis a player could hold, for the whole pan except
        // its last tick. No skip: this isolates the pan gate from the skip-tick
        // rule that the next test covers.
        for (int tick = 1; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            control.Step(SimInput.Idle);
            loud.Step(Everything());
        }

        Assert.True(control.Snapshot.Level100PlayerActive);
        Assert.Equal(1, control.Snapshot.Level100OpeningTicksRemaining);
        AssertNothingButThePanMoved(control.Snapshot, loud.Snapshot);
    }

    /// <summary>
    /// The skip tick specifically. This is the test that fails when
    /// <c>Simulation.Step</c> stops reading the pan state from the START of the
    /// tick - which is what it did until 2026-07-27, because
    /// <c>AdvanceOpeningCamera</c> zeroes the field before the gate reads it.
    /// </summary>
    [Fact]
    public void TheSkipTickIsStillAPanningTick_OnAReturningCareer()
    {
        Simulation control = CreateReturningCareer();
        Simulation loud = CreateReturningCareer();

        Assert.True(
            control.Snapshot.Level100PlayerActive,
            "A returning career must start with the player ACTIVE.");

        // Idle up to the skip on BOTH runs, so the only difference between them
        // is what is held on the skip tick itself.
        for (int tick = 1; tick < SkipTick; tick++)
        {
            control.Step(SimInput.Idle);
            loud.Step(SimInput.Idle);
        }

        WorldSnapshot quiet = control.Step(Skip);
        WorldSnapshot noisy = loud.Step(Everything(SimActions.SkipPanning));

        // Retail dispatches rows 0-15 before rows 22-25, so on this frame
        // movement, morph, fire and the rest were dispatched while the state
        // was still GAME_STATE_PANNING and were rejected at Player.cpp:319.
        // They take effect from the NEXT frame.
        Assert.Equal(0, quiet.Level100OpeningTicksRemaining);
        Assert.Equal(0, noisy.Level100OpeningTicksRemaining);
        AssertNothingButThePanMoved(quiet, noisy);

        // ...and from the next tick they do work, which is what makes the
        // assertion above a one-tick delay rather than a permanent block.
        WorldSnapshot after = loud.Step(Everything());
        Assert.NotEqual(noisy.PlayerPosition, after.PlayerPosition);
    }

    [Fact]
    public void NoInputDoesAnythingWhilePanning_IncludingOnTheSkipTickItself()
    {
        // NOTE: on a COLD career this is satisfied by the ACTIVATION gate, not
        // by the pan gate - the shipped init() deactivates the player until
        // ~t996. Kept because the cold career is the only career the shipping
        // client can start and the observable behaviour still has to be right;
        // the two returning-career tests above are what actually pin the pan
        // gate and the skip-tick rule.
        Simulation control = Create();
        Simulation loud = Create();

        Assert.False(control.Snapshot.Level100PlayerActive);

        // Hold every action and axis for the whole pan, then skip on the last
        // of those ticks while still holding them.
        for (int tick = 1; tick < SkipTick; tick++)
        {
            control.Step(SimInput.Idle);
            loud.Step(Everything());
        }

        WorldSnapshot quiet = control.Step(SimInput.Idle);
        WorldSnapshot noisy = loud.Step(Everything(SimActions.SkipPanning));

        // The skip took effect...
        Assert.Equal(SimulationConstants.Level100OpeningPanTicks - SkipTick, quiet.Level100OpeningTicksRemaining);
        Assert.Equal(0, noisy.Level100OpeningTicksRemaining);

        // ...and nothing else did. Retail dispatches every player-action row
        // (indices 0-15) before the four BUTTON_SKIP_PANNING rows (22-25), so
        // they are all still rejected by the Player.cpp:319 gate on this frame.
        Assert.Equal(quiet.PlayerPosition, noisy.PlayerPosition);
        Assert.Equal(quiet.PlayerVelocity, noisy.PlayerVelocity);
        Assert.Equal(quiet.PlayerElevationMillimeters, noisy.PlayerElevationMillimeters);
        Assert.Equal(quiet.FacingYawMicroRad, noisy.FacingYawMicroRad);
        Assert.Equal(quiet.FacingPitchMicroRad, noisy.FacingPitchMicroRad);
        Assert.Equal(quiet.WalkerYawVelocityMicroRadPerTick, noisy.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(quiet.Mode, noisy.Mode);
        Assert.Equal(quiet.Transition, noisy.Transition);
        Assert.Equal(quiet.Energy, noisy.Energy);
        Assert.Equal(quiet.LandingJetsActive, noisy.LandingJetsActive);
        Assert.Empty(noisy.Projectiles);
        Assert.Empty(noisy.Level100WeaponFireEvents);
    }

    [Fact]
    public void SkipAfterPlayingStateHasStarted_DoesNothingAtAll()
    {
        Simulation control = Create();
        Simulation pressing = Create();

        // Run both past the natural end of the pan.
        for (int tick = 1; tick <= SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            control.Step(SimInput.Idle);
            pressing.Step(SimInput.Idle);
        }

        Assert.Equal(0, control.Snapshot.Level100OpeningTicksRemaining);
        Assert.Equal(
            Level100MissionTiming.MessageBoxAllowedTick,
            control.Snapshot.Level100Mission.MessageBoxAllowedTick);

        // CGame::StartPlayingState early-returns when the state is already
        // GAME_STATE_PLAYING (game.cpp:3027), so hammering the key changes
        // nothing - not the gate, not the state, not one hashed byte.
        for (int tick = 0; tick < 200; tick++)
        {
            control.Step(SimInput.Idle);
            pressing.Step(Skip);
        }

        Assert.Equal(
            StateHasher.ComputeHex(control.Snapshot),
            StateHasher.ComputeHex(pressing.Snapshot));
    }

    [Fact]
    public void SkippingThePan_MovesTheWholeTutorialMessageSchedule()
    {
        const int horizon = 1_400;
        List<int> unskipped = MessageStartTicks(Create(), horizon, skipAtTick: null);
        List<int> skipped = MessageStartTicks(Create(), horizon, skipAtTick: SkipTick);

        // The unskipped chain is the measured retail one: the two fresh
        // uninterrupted Steam runs in rebuild/PROVENANCE.md put the first
        // message boundary at 182 30 Hz ticks after the pan began.
        Assert.Equal(Level100MissionTiming.MessageBoxAllowedTick, unskipped[0]);
        Assert.Equal(
            SimulationConstants.Level100PreRunTicks + SkipTick + Level100MissionTiming.ReleasedEventFrameTicks,
            skipped[0]);

        // Every message in the chain moves by the same amount, because the
        // whole chain is anchored on the gate and then paced by the message
        // box. This is the finding: the released tutorial's message schedule
        // is a player-controllable timing input, not a fixed table.
        int shift = unskipped[0] - skipped[0];
        Assert.Equal(SimulationConstants.Level100OpeningPanTicks - SkipTick, shift);
        Assert.True(skipped.Count >= unskipped.Count);
        for (int index = 0; index < unskipped.Count; index++)
        {
            Assert.Equal(unskipped[index] - shift, skipped[index]);
        }
    }

    [Fact]
    public void SkippingThePan_MovesPlayerActivationEarlierByTheSameAmount()
    {
        int unskipped = FirstPlayerActivationTick(Create(), 1_400, skipAtTick: null);
        int skipped = FirstPlayerActivationTick(Create(), 1_400, skipAtTick: SkipTick);

        // The released script reaches player.Activate() when
        // TUTORIAL_TECHNICIAN_01 clears, so control arrives earlier by exactly
        // the length of pan the player refused to watch.
        Assert.Equal(
            SimulationConstants.Level100OpeningPanTicks - SkipTick,
            unskipped - skipped);
    }

    /// <summary>
    /// The mission keeps its own clock and <c>SimActions.Reset</c> gives it a
    /// fresh one, while <c>Simulation</c>'s tick keeps running. A gate derived
    /// from the simulation tick would put every post-reset skip after the
    /// initial 182 and discard it silently.
    /// </summary>
    [Fact]
    public void SkipStillWorksAfterAReset_BecauseTheGateIsMissionRelative()
    {
        Simulation simulation = Create();

        // Burn well past the point where mission time and simulation time
        // would agree, then restart the level.
        for (int tick = 1; tick <= SimulationConstants.Level100OpeningPanTicks + 100; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        // The fresh mission clock has run the new level's pre-run.
        WorldSnapshot reset = simulation.Step(new SimInput(0, 0, SimActions.Reset));
        Assert.Equal(SimulationConstants.Level100PreRunTicks, reset.Level100Mission.Tick);
        Assert.NotEqual(reset.Level100Mission.Tick, reset.Tick);
        Assert.Equal(
            SimulationConstants.Level100OpeningPanTicks,
            reset.Level100OpeningTicksRemaining);
        Assert.Equal(
            Level100MissionTiming.MessageBoxAllowedTick,
            reset.Level100Mission.MessageBoxAllowedTick);

        for (int tick = 1; tick < SkipTick; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        WorldSnapshot skipped = simulation.Step(Skip);

        Assert.Equal(0, skipped.Level100OpeningTicksRemaining);
        Assert.Equal(SimulationConstants.Level100PreRunTicks + SkipTick, skipped.Level100Mission.Tick);
        Assert.Equal(
            SimulationConstants.Level100PreRunTicks + SkipTick + Level100MissionTiming.ReleasedEventFrameTicks,
            skipped.Level100Mission.MessageBoxAllowedTick);
    }

    [Fact]
    public void SkipIsAnEdgeAndReplaysDeterministically()
    {
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "skip-panning",
            1,
            400,
            null,
            null,
            [new CommandSpan(SkipTick, 1, 0, 0, SkipPanning: true)]);

        CommandTape roundTripped = CommandTapeCodec.Deserialize(CommandTapeCodec.Serialize(tape));
        Assert.True(roundTripped.Spans[0].SkipPanning);

        ReplayResult first = ReplayRunner.Run(roundTripped, Level100TestActorDefinitions.Create());
        ReplayResult second = ReplayRunner.Run(roundTripped, Level100TestActorDefinitions.Create());
        Assert.Equal(first.FinalStateHash, second.FinalStateHash);
        Assert.Equal(first.TraceHash, second.TraceHash);

        var idle = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "no-skip",
            1,
            400,
            null,
            null,
            []);
        ReplayResult unskipped = ReplayRunner.Run(idle, Level100TestActorDefinitions.Create());
        Assert.NotEqual(unskipped.FinalStateHash, first.FinalStateHash);
    }

    [Fact]
    public void SkipPanningIsAnEdgeActionInTheTape()
    {
        var held = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "held-skip",
            1,
            4,
            null,
            null,
            [new CommandSpan(0, 2, 0, 0, SkipPanning: true)]);

        Assert.Throws<InvalidDataException>(held.Validate);
    }

    /// <summary>
    /// <c>CommandSpan</c> gained the load-bearing <c>ChangeWeapon</c> edge in v4,
    /// then held <c>ChargeWeapon</c> and the zoom edges in v5. The current reader
    /// explicitly migrates v4, but still rejects v3 rather than treating its
    /// older field set as the current wire contract.
    /// </summary>
    /// <remarks>
    /// Unknown-member rejection already makes a v3 reader fail closed on a v4
    /// writer's <c>changeWeapon</c> property. This assertion covers the opposite
    /// direction after the v5 migration: the v5 reader permits only the explicit
    /// v4 upgrade and refuses a valid v3 field set by its schema identity.
    /// </remarks>
    [Fact]
    public void ATapeWrittenUnderTheOldSchemaIsRejectedRatherThanMisparsed()
    {
        Assert.Equal(
            "onslaught-rebuild-command-tape.v5",
            CommandTape.CurrentSchemaVersion);

        const string previousSchema = """
            {
              "schemaVersion": "onslaught-rebuild-command-tape.v3",
              "name": "old-schema",
              "seed": 1,
              "durationTicks": 400,
              "expectedFinalStateHash": null,
              "expectedTraceHash": null,
              "spans": [
                {
                  "startTick": 5,
                  "durationTicks": 1,
                  "moveX": 0,
                  "moveZ": 0,
                  "skipPanning": true
                }
              ]
            }
            """;

        InvalidDataException rejected = Assert.Throws<InvalidDataException>(
            () => CommandTapeCodec.Deserialize(previousSchema));
        Assert.Contains(
            "onslaught-rebuild-command-tape.v3",
            rejected.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The enum declares the whole released action set so its width and the
    /// command-tape format are decided once. The bits Core does not act on must
    /// therefore be rejected, not accepted and dropped.
    /// </summary>
    [Fact]
    public void DeclaredButUnimplementedActionsAreRejectedRatherThanIgnored()
    {
        foreach (SimActions action in new[]
                 {
                     SimActions.Cloak,
                 })
        {
            Assert.True((SimInput.DeclaredActions & action) != 0);
            Assert.True((SimInput.ImplementedActions & action) == 0);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SimInput(0, 0, action).Validate());
        }

        var changeWeapon = new SimInput(0, 0, SimActions.ChangeWeapon);
        changeWeapon.Validate();
        Assert.True((SimInput.ImplementedActions & SimActions.ChangeWeapon) != 0);

        var chargeWeapon = new SimInput(0, 0, SimActions.ChargeWeapon);
        chargeWeapon.Validate();
        Assert.True((SimInput.ImplementedActions & SimActions.ChargeWeapon) != 0);

        // And an unassigned bit is still an unknown bit.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimInput(0, 0, (SimActions)(1 << 15)).Validate());
    }

    /// <summary>
    /// Guards the one-change decision: the released set must fit without the
    /// storage width moving again.
    /// </summary>
    [Fact]
    public void ActionSetFitsTheChosenWidthWithRoomToSpare()
    {
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(SimActions)));
        int declaredBits = System.Numerics.BitOperations.PopCount((uint)SimInput.DeclaredActions);
        Assert.Equal(10, declaredBits);
        Assert.Equal(6, 16 - declaredBits);
    }

    /// <summary>
    /// The reachability caveat has to stay written down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which of the four shipped scan codes actually reaches
    /// <c>BUTTON_SKIP_PANNING</c> in retail is UNPROVEN, because reading a
    /// KEY_ONCE flag consumes it (<c>references/Onslaught/ltshell.h:292</c>),
    /// <c>CController::DoMappings</c> walks the table in row order, and all four
    /// codes are bound to an earlier row. Retail's own <c>GetKeyOnce</c> body is
    /// not in the partial drop.
    /// </para>
    /// <para>
    /// That caveat is the only thing standing between "the client binds Space"
    /// and "retail skips on Space", and prose with nothing pinning it is prose
    /// that gets tidied away. This test is the pin. It is deliberately in Core,
    /// because Core's <c>SimActions</c> is where the four scan codes are
    /// asserted in the first place.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheUnprovenSkipKeyRoutingStaysRecordedOnTheAction()
    {
        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "core-source",
            "SimulationTypes.cs"));
        int start = source.IndexOf("BUTTON_SKIP_PANNING", StringComparison.Ordinal);
        Assert.True(start >= 0, "SimActions no longer documents BUTTON_SKIP_PANNING.");
        int end = source.IndexOf("SkipPanning = 1 << 4,", StringComparison.Ordinal);
        Assert.True(end > start, "The BUTTON_SKIP_PANNING documentation moved.");
        string documentation = source[start..end];

        foreach (string required in new[]
                 {
                     // The question itself.
                     "UNPROVEN",
                     "ltshell.h:292",
                     "GetKeyOnce",
                     // The three keys the client binds, named alongside the
                     // earlier rows that may eat their presses.
                     "BUTTON_MECH_MORPH",
                     "BUTTON_SKIP_CUTSCENE",
                     // And the corroboration from the drop: retail's own
                     // controller binds this action to a PAD button.
                     "PCController.cpp:76",
                 })
        {
            Assert.Contains(required, documentation, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Both runs saw the same pan and the same skip; the only difference was
    /// what the player was holding. Nothing may separate them.
    /// </summary>
    /// <remarks>
    /// The field-level assertions come first so a failure names what moved; the
    /// hash comparison is the one that cannot be fooled, and it is what makes
    /// this a law test rather than a checklist of the fields someone thought of.
    /// </remarks>
    private static void AssertNothingButThePanMoved(
        WorldSnapshot quiet,
        WorldSnapshot noisy)
    {
        Assert.Equal(quiet.PlayerPosition, noisy.PlayerPosition);
        Assert.Equal(quiet.PlayerVelocity, noisy.PlayerVelocity);
        Assert.Equal(quiet.PlayerElevationMillimeters, noisy.PlayerElevationMillimeters);
        Assert.Equal(quiet.FacingYawMicroRad, noisy.FacingYawMicroRad);
        Assert.Equal(quiet.FacingPitchMicroRad, noisy.FacingPitchMicroRad);
        Assert.Equal(quiet.WalkerYawVelocityMicroRadPerTick, noisy.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(quiet.Mode, noisy.Mode);
        Assert.Equal(quiet.Transition, noisy.Transition);
        Assert.Equal(quiet.Energy, noisy.Energy);
        Assert.Equal(quiet.LandingJetsActive, noisy.LandingJetsActive);
        Assert.Empty(noisy.Projectiles);
        Assert.Empty(noisy.Level100WeaponFireEvents);
        Assert.Equal(
            StateHasher.ComputeHex(quiet),
            StateHasher.ComputeHex(noisy));
    }

    private static List<int> MessageStartTicks(
        Simulation simulation,
        int horizon,
        int? skipAtTick)
    {
        var starts = new List<int>();
        for (int tick = 1; tick <= horizon; tick++)
        {
            WorldSnapshot state = simulation.Step(
                tick == skipAtTick ? Skip : SimInput.Idle);
            starts.AddRange(state.Level100MissionEvents
                .OfType<Level100MessageRequested>()
                .Select(message => message.Tick));
        }

        return starts;
    }

    private static int FirstPlayerActivationTick(
        Simulation simulation,
        int horizon,
        int? skipAtTick)
    {
        for (int tick = 1; tick <= horizon; tick++)
        {
            WorldSnapshot state = simulation.Step(
                tick == skipAtTick ? Skip : SimInput.Idle);
            if (state.Level100PlayerActive)
            {
                return tick;
            }
        }

        throw new InvalidOperationException(
            "The released script never activated the player within the horizon.");
    }
}
