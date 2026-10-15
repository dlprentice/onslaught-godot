// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>One stage boundary of a cold start.</summary>
internal sealed record ColdStartStage(
    string Name,
    int Frame,
    double Seconds,
    int? Tick)
{
    public override string ToString() =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0,-28} frame {1,6}  t+{2,8:F3}s  {3}",
            Name,
            Frame,
            Seconds,
            Tick is null ? "-" : "tick " + Tick.Value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// The whole released experience as ONE run: the cold-start media sequence, the
/// interactive frontend, the entry into Level 100, and the tutorial played to
/// its outcome — with nothing but player input in between.
///
/// <para><b>What "player input only" means here, exactly.</b></para>
/// <list type="bullet">
///   <item><description>The frontend is navigated by <b>keystrokes and left
///   mouse clicks at 640x480 design-stage coordinates</b>, routed through
///   <see cref="RetailFrontendHarness"/>. Level 100 is entered by CLICKING THE
///   LEVEL NODE on the SELECT LEVEL page. Neither this class nor the harness
///   calls <c>ConsumeLevel100LaunchRequest</c>, <c>CompleteLevel100Load</c> or
///   any other lifecycle method out of order; those run where
///   <c>RetailFrontendFlow._Process</c> runs them.</description></item>
///   <item><description>The level is played through
///   <see cref="InteractiveSession"/> — <c>ObserveInput</c>,
///   <c>QueueToggleMode</c>, <c>QueuePointerMotionMilliPixels</c>,
///   <c>AdvanceFrameTicks</c> — which is the same surface
///   <c>FirstFlightGame._Process</c> and <c>_Input</c> use. See
///   <see cref="Level100InteractiveChainHost"/>.</description></item>
///   <item><description><b>No mission event is posted.</b> The driver is
///   <see cref="Level100ChainAutopilot"/> unchanged; every named progression
///   event still has to be produced by the world.</description></item>
/// </list>
///
/// <para><b>The two places this run is NOT the shipping path, stated up
/// front.</b> First, the binding layer is transcribed rather than executed —
/// see the remarks on <see cref="RetailFrontendHarness"/>. Second, the
/// cold-start media beats are replayed as a SCHEDULE
/// (<see cref="RetailStartupSchedule"/>, the product's own class) using the
/// shipped clips' evidenced frame counts; no video is decoded, because the
/// decode cache is a machine-local materialization of the user's retail
/// install and a gate cannot depend on it.</para>
/// </summary>
internal sealed class Level100ColdStartRun : IDisposable
{
    /// <summary>
    /// Frontend frame time. <c>rebuild/tools/first_flight.py smoke</c> launches
    /// the client with <c>--fixed-fps 60</c>, so this is the client's own
    /// frontend cadence.
    /// </summary>
    private const double FrontendFrameSeconds = 1d / 60d;

    /// <summary>
    /// <c>LTLogo.vid</c>: 229 frames at 25 fps. Stated in
    /// <c>RetailStartupSchedule</c>'s own remarks as what <c>ffprobe</c> reports
    /// for the shipped Bink file, and again in
    /// <c>RetailStartupMediaIndex</c>'s decode note.
    /// </summary>
    private static readonly RetailStartupClip LostToysLogoClip =
        new(229, 25, 1, 640, 480);

    /// <summary><c>OpeningFMV.vid</c>: 2054 frames at 25 fps, same source.</summary>
    private static readonly RetailStartupClip OpeningMontageClip =
        new(2_054, 25, 1, 640, 480);

    private readonly List<ColdStartStage> _stages = [];
    private readonly RetailFrontendHarness _frontend = new();

    private InteractiveSession? _session;
    private Level100InteractiveChainHost? _host;

    /// <summary>
    /// The session's command tape, recorded from its first tick exactly as
    /// <c>FirstFlightGame</c>'s <c>--record-tape</c> records it. Recording is
    /// in-process only and changes nothing the session computes.
    /// </summary>
    internal CommandTapeRecorder Recorder { get; } = new();
    private int _frame;
    private double _seconds;
    private int _loadRequestFrame = -1;
    private bool _transitionRejectedAClick;

    internal IReadOnlyList<ColdStartStage> Stages => _stages;

    internal Level100ChainAutopilot? Driver { get; private set; }

    internal Level100MissionOutcome Outcome { get; private set; } =
        Level100MissionOutcome.Running;

    internal Level100InteractiveChainHost Host =>
        _host ?? throw new InvalidOperationException("The level was never entered.");

    internal WorldSnapshot Final =>
        _session?.CurrentSnapshot ??
        throw new InvalidOperationException("The level was never entered.");

    /// <summary>
    /// True when the 50-frame main-menu reveal was proven to swallow input: a
    /// click sent during it changed nothing.
    /// </summary>
    internal bool MainMenuRevealSwallowedAClick => _transitionRejectedAClick;

    internal int LoadRequestFrame => _loadRequestFrame;

    /// <summary>
    /// Runs the whole sequence.
    ///
    /// <para>The level is always entered as a <b>cold first career</b> — all
    /// four <c>SLOT_TUTORIAL_*</c> unsaved — because that is the only thing the
    /// client's New Game path can produce and the only thing
    /// <see cref="InteractiveSession"/> can be constructed with. It is a
    /// stricter run than the returning-player one this driver was measured on:
    /// nothing is skipped, and the HUD lectures with their
    /// <c>player.Deactivate()</c> / <c>player.Activate()</c> gates all
    /// play.</para>
    /// </summary>
    internal Level100MissionOutcome Run(int maximumLevelTicks = 1_200 * SimulationConstants.TicksPerSecond)
    {
        RunStartupMedia();
        RunFrontendToGameplay();
        Driver = Level100ChainAutopilot.CreateOnClientSession(_host!);
        Mark("level:opening-pan-done");
        Outcome = Driver.Run(maximumLevelTicks);
        Mark("level:" + Outcome.ToString().ToLowerInvariant());
        return Outcome;
    }

    // ------------------------------------------------------------------
    // Cold start
    // ------------------------------------------------------------------

    private void RunStartupMedia()
    {
        var schedule = new RetailStartupSchedule(
            new Dictionary<RetailStartupCue, RetailStartupClip>
            {
                [RetailStartupCue.LostToysLogo] = LostToysLogoClip,
                [RetailStartupCue.OpeningMontage] = OpeningMontageClip,
            },
            splashPresent: true);

        string last = string.Empty;
        // The player watches it through. RetailStartupSequence._Input aborts the
        // whole chain on ANY key or button, so pressing nothing is the only
        // input that reaches the end of the montage.
        while (true)
        {
            RetailStartupFrame sample = schedule.Sample(_seconds);
            string current = sample.Kind == RetailStartupFrameKind.Video
                ? "startup:" + sample.Cue
                : "startup:" + sample.Kind;
            if (!string.Equals(current, last, StringComparison.Ordinal))
            {
                Mark(current);
                last = current;
            }

            if (sample.Kind == RetailStartupFrameKind.Finished)
            {
                return;
            }

            Advance();
        }
    }

    // ------------------------------------------------------------------
    // Frontend
    // ------------------------------------------------------------------

    private void RunFrontendToGameplay()
    {
        _frontend.Level100LoadRequested += () =>
        {
            _loadRequestFrame = _frame;
            // FirstFlightGame.LoadLevel100FromFrontend (:671-689): construct the
            // session, build the world, then tell the frontend it is ready. The
            // seed is the client's own SimulationSeed.
            _session = CreateSession();
            _session.EnableRecording(Recorder);
            // The driver is held to what a hand on a mouse can issue. Retail's
            // CController::DoMappings maps an INTEGER pixel displacement, so
            // the reachable axis is {0, +-30, +-61, +-91, ...} permille - and
            // since 2026-07-30 that is the client's own law too, not a stricter
            // one applied in front of it, so every position this asks for is
            // delivered exactly. Without this the run is not a playthrough: it
            // asks for 3,243 positions finer than one mouse pixel.
            _host = new Level100InteractiveChainHost(
                _session,
                quantizeLookToIntegerMousePixels: true);
            _frontend.MarkLevel100Ready();
        };

        _frontend.Process();
        Mark("frontend:ClickToStart");

        // A click anywhere leaves the click page.
        RequireClick(320, 240, RetailFrontendScreen.MainMenu, "click-to-start");
        Mark("frontend:MainMenu");

        // The 50-frame reveal must swallow input. Prove it rather than assume
        // it: a click on the New Game row during the reveal has to do nothing.
        _frontend.Process();
        (double newGameX, double newGameY) =
            RetailFrontendHarness.MainMenuRowPoint(0);
        _transitionRejectedAClick =
            !_frontend.SendLeftClick(newGameX, newGameY) &&
            _frontend.Screen == RetailFrontendScreen.MainMenu;
        for (int frame = 0; frame < RetailFrontendHarness.MainMenuEntryTransitionFrames; frame++)
        {
            Advance();
            _frontend.Process();
        }

        Mark("frontend:MainMenuInteractive");

        // NEW GAME is row 0. Clicked, not indexed by hand: MainMenuRowPoint puts
        // the point on the row's own drawn position.
        RequireClick(newGameX, newGameY, RetailFrontendScreen.DevSelect, "New Game");
        Mark("frontend:DevSelect");

        // CHOOSE GAME NAME. The page pre-fills "BEA 1"; the forward chevron
        // accepts it.
        RequireClick(617, 454, RetailFrontendScreen.LevelSelect, "DevSelect forward");
        Mark("frontend:LevelSelect");

        // SELECT LEVEL. This is the selection: the click lands on the level node
        // ring the page hit-tests, and nothing else on the page is clickable
        // except the chevrons.
        (double levelX, double levelY) = RetailFrontendHarness.LevelNodePoint();
        RequireClick(levelX, levelY, RetailFrontendScreen.MissionBriefing, "Level 100 node");
        Mark("frontend:MissionBriefing");

        RequireClick(617, 454, RetailFrontendScreen.SelectConfiguration, "briefing forward");
        Mark("frontend:SelectConfiguration");

        RequireClick(617, 454, RetailFrontendScreen.Loading, "configuration forward");
        Mark("frontend:Loading");

        // The loading page consumes no input and completes on its own two-frame
        // gate, exactly as RetailFrontendFlow._Process does it.
        for (int frame = 0; frame < 8 && _frontend.Screen != RetailFrontendScreen.Gameplay; frame++)
        {
            Advance();
            _frontend.Process();
        }

        if (_frontend.Screen != RetailFrontendScreen.Gameplay)
        {
            throw new InvalidOperationException(
                $"Loading never handed over; the frontend is on {_frontend.Screen}.");
        }

        if (_session is null || _host is null)
        {
            throw new InvalidOperationException(
                "Gameplay was reached without a session having been constructed.");
        }

        Mark("frontend:Gameplay");
    }

    private static InteractiveSession CreateSession() =>
        // Same hash-checked decoder, actor data and cold career as
        // FirstFlightGame.CreateSession; the decoder has no Godot dependency.
        new InteractiveSession(SimulationSeed, Level100TestActorDefinitions.LoadMaterialized());

    /// <summary><c>FirstFlightGame.SimulationSeed</c> (:13), ASCII "ONSL".</summary>
    internal const uint SimulationSeed = 0x4F4E534Cu;

    public void Dispose() => Recorder.Dispose();

    private void RequireClick(
        double x,
        double y,
        RetailFrontendScreen expected,
        string what)
    {
        Advance();
        _frontend.Process();
        if (!_frontend.SendLeftClick(x, y))
        {
            throw new InvalidOperationException(
                $"The {what} click at ({x}, {y}) was not accepted on " +
                $"{_frontend.Screen}.");
        }

        if (_frontend.Screen != expected)
        {
            throw new InvalidOperationException(
                $"The {what} click left the frontend on {_frontend.Screen}, " +
                $"not {expected}.");
        }
    }

    private void Advance()
    {
        _frame++;
        _seconds += FrontendFrameSeconds;
    }

    private void Mark(string name) =>
        _stages.Add(new ColdStartStage(
            name,
            _frame,
            _seconds,
            _session?.CurrentSnapshot.Tick));
}
