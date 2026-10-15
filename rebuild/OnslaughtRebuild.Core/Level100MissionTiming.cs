// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

public static class Level100MissionTiming
{
    // Consumed evidence only: supported 100_res_PC.aya SHA-256
    // ed6350c0e214d00ab1bf6a7bd137fba3e77d0afe19a6dc4c0607f56ac037496a;
    // exact LevelScript object SHA-256
    // 73eb349b9c4b5c5d7294b2183cd4d4aebe024c5d3c8cda9be685bd1463ed6fb1;
    // readable LevelScript.msl SHA-256
    // d51f8864564b5bde872092ec822df5af49daac16563f500719135f1a8c6c04a4.
    // Steam Ghidra establishes the object loader,
    // opcode dispatch (0x0052d3d0), saved-state restore (0x00533840), Pause
    // (0x00537c70), message-wait path (0x005375f0), and terminal functions
    // (0x0046f2f0/0x0046f430). Voice ticks are shipped Ogg granules plus the
    // evidenced 18-tick wait post-roll. No retail executable is a play dependency.

    public const int AuthoredTriggerRadiusMillimeters = 5_000;
    public const int HealthPollCadenceTicks = SimulationConstants.TicksPerSecond;
    // CGame::DeclareLevelWon @ 0x0046F338 stores 0x40A00000 = 5.0f at +0x48
    // for every world that is not 741/742. Level 100 takes that arm.
    public const int SuccessCountdownTicks = RetailGameEndCountdown.WonTicks;
    // CGame::DeclareLevelLost @ 0x0046F4A8 stores 0x40000000 = 2.0f at +0x48.
    // The pinned GPL source still says 5.0f, so this is an evidenced
    // retail/source divergence rather than a source-port choice.
    public const int FailureCountdownTicks = RetailGameEndCountdown.LostTicks;
    public const int FailureMenuDelayTicks = SimulationConstants.TicksPerSecond / 2;
    // CGame::DeclareLevelLost schedules PAUSE_GAME 15 seconds after a
    // player-death loss and starts CONTINUE_FADE_OUT_GAME_SOUNDS on the next
    // released frame. The static evidence proves the duration, not whether the
    // strict event-time comparison lands on nominal update 300 or 301; Core
    // uses the established 20 Hz nominal projection and keeps that edge open.
    public const int DeathPauseDelayTicks = 15 * SimulationConstants.TicksPerSecond;
    public const float DeathGameplayFadeStep = 0.003f;

    public static int FailureTerminalTicks(Level100MissionFailureReason reason) => reason switch
    {
        Level100MissionFailureReason.TutorialBroken => FailureCountdownTicks,
        Level100MissionFailureReason.PlayerDeath or
            Level100MissionFailureReason.WaterLoss => DeathPauseDelayTicks,
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static int FailureOverlayTicksRemaining(
        Level100MissionFailureReason reason,
        int terminalTicksRemaining)
    {
        if (terminalTicksRemaining < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(terminalTicksRemaining));
        }

        int terminalTicks = FailureTerminalTicks(reason);
        return Math.Clamp(
            terminalTicksRemaining - (terminalTicks - FailureCountdownTicks),
            0,
            FailureCountdownTicks);
    }

    public static bool GameplayPaused(
        Level100MissionOutcome outcome,
        Level100MissionFailureReason reason,
        int terminalTicksRemaining) =>
        outcome == Level100MissionOutcome.Lost &&
        (reason == Level100MissionFailureReason.TutorialBroken ||
         (reason is (Level100MissionFailureReason.PlayerDeath or
             Level100MissionFailureReason.WaterLoss) &&
          terminalTicksRemaining == 0));

    internal static bool GameplayPausesOnNextTick(
        Level100MissionOutcome outcome,
        Level100MissionFailureReason reason,
        int terminalTicksRemaining) =>
        GameplayPaused(outcome, reason, terminalTicksRemaining) ||
        (outcome == Level100MissionOutcome.Lost &&
         reason is (Level100MissionFailureReason.PlayerDeath or
             Level100MissionFailureReason.WaterLoss) &&
         terminalTicksRemaining == 1);

    public static float GameplayMix(
        Level100MissionOutcome outcome,
        Level100MissionFailureReason reason,
        int terminalTicksRemaining)
    {
        if (outcome != Level100MissionOutcome.Lost ||
            reason is not (Level100MissionFailureReason.PlayerDeath or
                Level100MissionFailureReason.WaterLoss))
        {
            return 1f;
        }

        int elapsedTicks = Math.Clamp(
            DeathPauseDelayTicks - terminalTicksRemaining,
            0,
            DeathPauseDelayTicks);
        float mix = 1f;
        for (int tick = 0; tick < elapsedTicks; tick++)
        {
            // Preserve the released repeated float32 subtraction. Computing
            // 1 - elapsed*step produces different bits at the pause boundary.
            mix -= DeathGameplayFadeStep;
        }
        return mix;
    }

    public static SimVector2 TriggerPosition(Level100MissionTrigger trigger) => trigger switch
    {
        Level100MissionTrigger.TargetZone1 => SimulationConstants.Level100TargetZone1Position,
        Level100MissionTrigger.FiringRange => SimulationConstants.Level100FiringRangePosition,
        Level100MissionTrigger.TargetZone2 => new(-56_688, -62_250),
        Level100MissionTrigger.TargetZone3 => new(-57_938, 2_625),
        Level100MissionTrigger.TargetZone4 => new(0, -31),
        _ => throw new ArgumentOutOfRangeException(nameof(trigger)),
    };

    public static bool RequiresNotInJetMode(Level100MissionTrigger trigger) => trigger is
        Level100MissionTrigger.TargetZone2 or
        Level100MissionTrigger.TargetZone3 or
        Level100MissionTrigger.TargetZone4;

    /// <summary>
    /// The released <c>InJetMode()</c> script builtin, read out of the pristine
    /// executable rather than inferred.
    /// </summary>
    /// <remarks>
    /// The registered handler at <c>0x005380f0</c> is nine instructions:
    /// <code>
    ///   [retail instruction listing removed]
    ///   [retail instruction listing removed]
    ///   [retail instruction listing removed]
    ///   74 0E             JZ   done                        ; anything else is never in jet mode
    ///   [retail instruction listing removed]
    ///   [retail instruction listing removed]
    ///   [retail instruction listing removed]
    /// </code>
    /// and <c>0x00408120</c> returns true when
    /// <c>*(int*)(this+0x260) == 2 &amp;&amp; 0x00672fd0 - *(float*)(this+0xcc) &lt; _DAT_005d85ec</c>.
    /// <para>
    /// So <c>InJetMode()</c> is the <em>negation</em> of "is a walker that
    /// touched the ground recently". <c>+0x260 == 2</c> is the walker state —
    /// <c>CPlayer::ReceiveButtonAction</c> (<c>0x004D3110</c>) routes
    /// button <c>0x15</c> to <c>ActivateLandingJets</c> only inside that same
    /// <c>== 2</c> branch, which is <c>BATTLE_ENGINE_STATE_WALKER</c> in
    /// <c>references/Onslaught/Player.cpp</c>. The threshold at
    /// <c>0x005D85EC</c> reads <c>00 00 00 3F</c> in the pristine binary
    /// (sha256 74154bfa…7750), i.e. <b>0.5 s</b> — note that the GPL source's
    /// only other use of this predicate,
    /// <c>CBattleEngineWalkerPart::Move</c>'s
    /// <c>GetTime() - mLastTimeOnGround &lt; 0.3f</c>, says 0.3. The shipped
    /// bytes win; the reference does not describe this executable here.
    /// </para>
    /// <para>
    /// This matters because <c>TargetZone2/3/4.msl</c> gate on
    /// <c>InJetMode() == FALSE</c>. Reading that as "not in jet mode" accepted
    /// an airborne walker, so beats 6, 8 and 10 could be completed by morphing
    /// mid-air over the volume and never landing at all.
    /// </para>
    /// </remarks>
    public static Level100MissionJetModeState JetModeState(
        VehicleMode mode,
        VehicleTransition transition,
        int ticksSinceGroundContact) =>
        mode == VehicleMode.Walker &&
        transition == VehicleTransition.None &&
        ticksSinceGroundContact < GroundContactRecencyTicks
            ? Level100MissionJetModeState.NotInJetMode
            : Level100MissionJetModeState.InJetMode;

    /// <summary>
    /// 0.5 s, the shipped <c>_DAT_005d85ec</c> threshold. Written in seconds so
    /// a Core tick-rate change leaves it meaning the same thing.
    /// </summary>
    public const int GroundContactRecencyTicks = SimulationConstants.TicksPerSecond / 2;

    /// <summary>Converts an authored MissionScript <c>Pause</c> duration to Core ticks.</summary>
    /// <remarks>
    /// Shipped registry row 4 binds <c>Pause</c> to <c>0x00537C70</c>. The
    /// pristine body reads argument zero through the float getter, clones the
    /// current script execution, appends that continuation to
    /// <c>IScript+0x28</c>, and schedules event <c>0x7D1</c> for
    /// <c>currentTime + seconds</c>. A trace-hashed copied-runtime query observed
    /// the handler once with equal symbolic/numeric <c>TTD.Calls</c> counts and
    /// observed its continuation-list/global-flag side effects. The durable
    /// evidence summary is in
    /// <c>reverse-engineering/binary-analysis/functions/IScript.cpp.md</c>.
    /// This is script suspension, not the player-facing game-pause menu.
    /// </remarks>
    internal static int PauseTicks(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0)
        {
            throw new InvalidOperationException("The released LevelScript requested an invalid pause.");
        }

        return checked((int)MathF.Round(
            seconds * SimulationConstants.TicksPerSecond,
            MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// One released 20 Hz event-manager frame, expressed in whole Core ticks.
    /// Core now runs at the released rate, so this is <b>1</b>; it was 2 at
    /// 30 Hz, where 30/20 = 1.5 and a released <c>NEXT_FRAME</c> event cannot
    /// fire before the frame boundary, so it rounded up.
    /// </summary>
    /// <remarks>
    /// This is the highest-leverage single consequence of the 20 Hz migration.
    /// It feeds <see cref="MessageBoxAllowedTick"/>, which drops from 182 to
    /// 121, and with it the whole opening message timeline. Retail floors every
    /// scheduled delay onto a whole 20 Hz boundary
    /// (<c>references/Onslaught/eventmanager.cpp:210-212</c>), which a 30 Hz
    /// Core could not land at all; the residual against the measured retail
    /// boundaries is expected to SHRINK, and a failure to improve is evidence
    /// the migration was done wrong.
    /// </remarks>
    public const int ReleasedEventFrameTicks =
        (SimulationConstants.TicksPerSecond +
            Level100ActorMechanics.RetailBaseTicksPerSecond - 1) /
        Level100ActorMechanics.RetailBaseTicksPerSecond;

    /// <summary>
    /// The first Core tick on which a character message may become active
    /// <b>when the player lets the opening pan run to its full length</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The released level script runs from level start — it deactivates the
    /// player, disables both weapons and initialises the four primary
    /// objectives before the opening pan is over — but the message box is
    /// <em>not permitted to play anything</em> until the game leaves
    /// <c>GAME_STATE_PANNING</c>. <c>CGame::StartPlayingState</c>
    /// (<c>references/Onslaught/game.cpp:3026-3031</c>) is the whole law:
    /// </para>
    /// <code>
    ///   mGameState = GAME_STATE_PLAYING ;
    ///   SCRIPT_EVENT_NB.PostEvent("game playing");
    ///   EVENT_MANAGER.AddEvent(ALLOWED_TO_PLAY_MESSAGES, mMessageBox, NEXT_FRAME) ;
    /// </code>
    /// <para>
    /// So the greeting is held, not skipped, and every later message inherits
    /// the same offset. <c>FINISHED_PANNING</c> arrives at the end of the
    /// six-second pan (<c>SimulationConstants.Level100OpeningPanTicks</c> =
    /// 120 at the current 20 Hz, and <c>CPanCamera::GetShowHUD</c> is false for all of it), and
    /// <c>ALLOWED_TO_PLAY_MESSAGES</c> is one released event frame later.
    /// At 20 Hz that is 120 + 1 = 121 ticks after the pan starts. At 30 Hz it
    /// was 180 + 2 = 182, which is exactly the tick two fresh uninterrupted
    /// app-owned Steam runs measured for the first message boundary
    /// (<c>rebuild/PROVENANCE.md</c>, "HUD introduction 182..351") - those
    /// measurements are recorded in 30 Hz Core ticks from the pan's start at
    /// event time 3.0, and 121 x 1.5 = 181.5, so 121 is the same instant to
    /// within the sampler. The mission clock starts at the load, before the
    /// 3.0 s pre-run, so the gate is <b>181</b> mission ticks.
    /// </para>
    /// <para>
    /// This is why the reconstruction showed no greeting at all: the script
    /// initialiser ran at tick 0, so HUD_01 was delivered and had already
    /// finished before the HUD became visible at tick ~179, and the first
    /// thing the player ever saw was HUD_02 mid-reveal.
    /// </para>
    /// <para>
    /// <b>This is the unskipped value, not a fixed one.</b> The gate is
    /// <c>StartPlayingState + NEXT_FRAME</c>, and
    /// <c>CPlayer::ReceiveButtonAction</c> lets
    /// <c>BUTTON_SKIP_PANNING</c> call <c>StartPlayingState</c> at any tick of
    /// the pan (<c>Player.cpp:311-315</c>). A player who skips at tick
    /// <c>T</c> therefore moves the whole opening message chain — and with it
    /// <c>player.Activate()</c>, which the script reaches when
    /// <c>TUTORIAL_TECHNICIAN_01</c> clears — to <c>T + 2</c>. That makes the
    /// released tutorial's message schedule a
    /// <b>player-controllable timing input</b>, not a fixed table.
    /// <see cref="Level100Mission"/> owns the live value; this constant is
    /// only the value it starts at, and is what the two measured no-input
    /// Steam runs saw.
    /// </para>
    /// </remarks>
    public const int MessageBoxAllowedTick =
        SimulationConstants.Level100PreRunTicks + SimulationConstants.Level100OpeningPanTicks +
        ReleasedEventFrameTicks;

    /// <summary>
    /// <see cref="MessageBoxAllowedTick"/> for a career world's own pan: the
    /// mission clock counts from the load, so the 3.0 s pre-run comes first
    /// (<see cref="SimulationConstants.Level100PreRunTicks"/>), then the pan,
    /// then one released event frame. Level 100 gives 181; World 110, whose
    /// pan is 2.0 s, gives 101.
    /// </summary>
    public static int MessageBoxAllowedTickFor(int worldNumber) =>
        SimulationConstants.Level100PreRunTicks + SimulationConstants.OpeningPanTicks(worldNumber) +
        ReleasedEventFrameTicks;

    /// <summary>
    /// The gap between one character message clearing and the next queued one
    /// becoming active, in Core ticks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 0.2 s at the released 0.05 s event clock, from
    /// <c>CMessageBox__TryAdvanceQueuedMessage</c> (<c>0x004b7b80</c>), which
    /// waits 0.2 s before starting the voice/reveal path of the message it
    /// dequeues.
    /// </para>
    /// <para>
    /// Independently measured: the eight retail opening boundaries in
    /// <c>rebuild/PROVENANCE.md</c> are 182..351, 357..567, 573..756,
    /// 762..926, 932..998, 1004..1220, 1226..1387, 1393..1530. Every single
    /// end-to-next-start gap is exactly six ticks, and every span equals this
    /// file's <see cref="MessagePlaybackTicks"/> entry to within the ±1 tick
    /// the 50 ms sampler can resolve.
    /// </para>
    /// <para>
    /// That measurement also settles what the 18-tick offset in
    /// <see cref="MessagePlaybackTicks"/> is <em>not</em>: it is not a
    /// post-roll to subtract for display. Retail activates the message before
    /// the voice starts and retains it through the completion hold, so the
    /// table value <b>is</b> the on-screen duration.
    /// </para>
    /// </remarks>
    public const int MessageAdvanceDelayTicks = SimulationConstants.TicksPerSecond / 5;

    /// <summary>
    /// The on-screen duration of each catalogued Level 100 voice message, in
    /// Core ticks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Recomputed for the 20 Hz migration from the shipped Ogg granules, not
    /// rescaled from the 30 Hz integers.</b> Each entry is
    /// <c>round(oggGranuleSeconds * TicksPerSecond + offset)</c>, where the
    /// offset is the message-box overhead either side of the voice: 18.03 ticks
    /// at 30 Hz, and 18.03 * 20/30 = <b>12.02</b> here. The granule for every
    /// one of the 43 messages was read from the end-of-stream page of its
    /// shipped Ogg in
    /// <c>Assets/Level100/TutorialAudio/</c> (all 44100 Hz mono, each file
    /// SHA-256-pinned by <c>rebuild/tools/materialize_retail_assets.py</c>),
    /// and the id-to-file mapping was cross-checked between
    /// <c>Level100AudioCatalog</c> and the shipped
    /// <c>level100-hud-events.json</c> manifest with zero conflicts.
    /// </para>
    /// <para>
    /// <b>The measurement is self-checking:</b> the same granules and the same
    /// formula at 30 Hz reproduce all 43 of the previous integers exactly, so
    /// the table below is a re-derivation of a verified quantity rather than a
    /// new fit.
    /// </para>
    /// <para>
    /// <b>Rescaling would have been wrong on five entries.</b>
    /// <c>round(T30 * 2/3)</c> double-rounds, and disagrees with the granule
    /// on HUD_03, HUD_05, TUTORIAL_TECHNICIAN_01, TUTORIAL_01 and
    /// TUTORIAL_STRAFE.
    /// </para>
    /// <para>
    /// <b>The offset survives the rate change as evidence, not as arithmetic.</b>
    /// Expressed in seconds the per-message offset is mean 0.6011 s, min
    /// 0.5849, max 0.6141. Expressed in 20 Hz ticks it is mean 12.0217, min
    /// 11.6984, max 12.2812 - <em>all 43 round to 12</em>, the exact analogue
    /// of "all 43 round to 18" at 30 Hz. Two entries are decided by the third
    /// decimal and are marked below; 12.02 is used because it is the same
    /// number the 30 Hz table used, not a fresh fit. Note that 0.6011 s is
    /// about 0.1 s more than the two cited constants explain
    /// (<see cref="MessageAdvanceDelayTicks"/> 0.2 s plus the 0.3 s completion
    /// hold), and that residual is still unidentified.
    /// </para>
    /// <para>
    /// World 110's messages take the same retained 20 Hz equation from their
    /// shipped English Ogg sample counts: <c>110_protect.ogg</c> (SHA-256
    /// <c>03f1fc8e…35d3</c>) has 172,496 samples at 44.1 kHz and gives 90
    /// ticks, and the table below lists the rest. None sits within Level 100's
    /// measured offset range (11.70-12.28 ticks) of a rounding boundary. The
    /// shared HEALTH_LOW messages are not admitted.
    /// </para>
    /// </remarks>
    internal static int MessagePlaybackTicks(int messageId) => messageId switch
    {
        8444036 => 90,       // _110_PROTECT
        // World 110's other messages, by the same retained equation from
        // their shipped English Ogg sample counts at 44.1 kHz (ids from
        // data/MissionScripts/text/text.stf).
        453985879 => 81,     // _110_ENEMY_ENGAGED  152,115 samples
        33759812 => 142,     // _110_RED_ALERT      285,841
        261104546 => 104,    // _110_ENGAGE_TANKS   202,392
        29930330 => 117,     // _110_HANG_BACK      231,545
        30271398 => 83,      // _110_NO_ESCAPE      157,386
        9170604 => 63,       // _110_VICTORY        111,619
        954166190 => 98,     // _110_KRAMER_ANNOYED 190,396
        509975791 => 67,     // _110_BUILDING_DIES  122,195
        1353519 => 88,       // _110_LOST           167,610
        124264647 => 110,    // _110_SECOND_WAVE    216,042
        264341189 => 72,     // _110_RESEARCH_HIT   133,028
        292562 => 113,       // HUD_01
        293386 => 140,       // HUD_02
        294210 => 176,       // HUD_03
        295034 => 176,       // HUD_04
        295858 => 174,       // HUD_05  (knife-edge: 12.00 would give 173)
        296682 => 122,       // HUD_06
        297506 => 157,       // HUD_07
        -1575499396 => 109,  // TUTORIAL_MESSAGE_LOG
        -257967449 => 44,    // TUTORIAL_TECHNICIAN_01 (knife-edge: 12.00 -> 43)
        82987417 => 143,     // TUTORIAL_13_MOD
        4422830 => 106,      // TUTORIAL_01
        175347826 => 92,     // TUTORIAL_SCANNER
        4458134 => 120,      // TUTORIAL_02
        4493438 => 65,       // TUTORIAL_03
        1339691000 => 147,   // TUTORIAL_PULSE_CANNON
        669198996 => 75,     // TUTORIAL_OPEN_FIRE
        -1715818922 => 162,  // TUTORIAL_PULSE_CANNON_2
        -1616775312 => 159,  // TUTORIAL_VULCAN_CANNON
        -1860407443 => 81,   // TUTORIAL_OPEN_FIRE_2
        864965454 => 121,    // TUTORIAL_VULCAN_CANNON_2
        4564046 => 187,      // TUTORIAL_05
        22775962 => 127,     // TUTORIAL_ZOOM
        667656903 => 134,    // TUTORIAL_DODGE_MOD
        150647733 => 110,    // TUTORIAL_DODGE_2
        151778876 => 163,    // TUTORIAL_DODGE_3
        623538785 => 91,     // TUTORIAL_DODGE_BAD
        1326027769 => 86,    // TUTORIAL_DODGE_GOOD
        4528742 => 175,      // TUTORIAL_04
        165861931 => 152,    // TUTORIAL_LANDING
        4599350 => 151,      // TUTORIAL_06
        1062059777 => 87,    // TUTORIAL_THROTTLE_MOD
        4475837 => 89,       // TUTORIAL_12
        4705262 => 142,      // TUTORIAL_09
        4634654 => 112,      // TUTORIAL_07
        80260569 => 132,     // TUTORIAL_STRAFE
        4669958 => 151,      // TUTORIAL_08
        4440532 => 150,      // TUTORIAL_11
        162342028 => 112,    // TUTORIAL_ABORTED
        150940633 => 73,     // TUTORIAL_BROKE_1
        152071864 => 81,     // TUTORIAL_BROKE_2
        153203095 => 85,     // TUTORIAL_BROKE_3
        -1455850811 => 76,   // TUTORIAL_HELP_PLAYER
        4405227 => 133,      // TUTORIAL_10
        _ => throw new InvalidOperationException(
            $"Released Level 100 message id {messageId} has no evidenced wait duration."),
    };
}
