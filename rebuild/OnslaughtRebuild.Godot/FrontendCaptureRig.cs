// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Deterministic screenshot capture for the bounded released frontend path and
/// for the Level 100 opening gameplay timeline.
///
/// This is a PRESENTATION-LAYER rig. It exists so parity claims can cite pixels
/// instead of code paths. <c>OnslaughtRebuild.Core</c> must never reference it —
/// it touches the GPU (viewport readback), the filesystem (PNG writes), and the
/// engine frame clock, all of which Core is contractually free of.
///
/// Determinism contract: the engine must be launched with <c>--fixed-fps</c> so
/// that one <c>_Process</c> call is one logical frame. Shots are keyed to frame
/// ordinals, never to wall-clock time, so a given plan yields byte-identical
/// framing across runs on the same build.
///
/// Retail reference captures are 640x480 (see the Hypothesis 1 verdict — the
/// released frontend composes at 4:3). Launch with <c>--resolution 640x480</c>
/// for pixel-comparable output; the rig records the actual size it saw so a
/// mismatched run cannot be silently compared against a 640x480 reference.
///
/// <para><b>Plans</b></para>
/// <list type="bullet">
/// <item><c>startup</c> — 13 frontend shots at absolute frame ordinals.</item>
/// <item><c>gameplay</c> — the Level 100 in-level timeline, sampled at the same
/// level offsets as <c>local-lab/retail-reference-pristine/level100-gameplay/</c>
/// so retail and reconstruction frames pair by offset mechanically.</item>
/// </list>
/// </summary>
public sealed partial class FrontendCaptureRig : Node
{
    /// <summary>Frame ordinal, the shot label, and the screen expected at that point.</summary>
    private readonly record struct Shot(
        int Frame,
        string Label,
        RetailFrontendScreen? ExpectScreen,
        int? LevelOffsetMs,
        string? Phase);

    /// <summary>Frame ordinal and the flow action to apply before that frame draws.</summary>
    private readonly record struct Step(int Frame, string Action);

    private readonly List<Shot> _shots = [];
    private readonly List<Step> _steps = [];
    private readonly List<Dictionary<string, object?>> _written = [];

    /// <summary>
    /// Released composition size. `CD3DApplication__Init` seeds
    /// `m_dwCreationWidth = 0x280` (640) and `m_dwCreationHeight = 0x1e0` (480),
    /// and every retail reference capture is 640x480.
    /// </summary>
    private const int NativeWidth = 640;
    private const int NativeHeight = 480;

    /// <summary>
    /// The <c>--fixed-fps</c> value the capture scripts launch with. One
    /// <c>_Process</c> call is 1/60 s of logical time, so a level offset in
    /// milliseconds converts to an exact engine frame ordinal. See
    /// <see cref="FrameForOffsetMs"/> for the arithmetic and its exactness proof.
    /// </summary>
    private const int CaptureFramesPerSecond = 60;

    /// <summary>
    /// The reconstruction's scripted opening pan length,
    /// <c>SimulationConstants.Level100OpeningPanTicks</c> = 6 * 30 ticks. Used
    /// only to label a shot's phase in the manifest; it is not a schedule.
    /// Retail's pan end was measured from pixels at t0+6006..6256 ms.
    /// </summary>
    private const int OpeningPanMs = 6_000;

    /// <summary>
    /// If the frontend has not handed off to gameplay by this frame the run is
    /// abandoned and the manifest records the boundary. 60 fps * 20 s; the
    /// scripted traversal below reaches gameplay near frame 490.
    /// </summary>
    private const int GameplayArmDeadlineFrame = 1_200;

    private RetailFrontendFlow _frontend = null!;
    private string _outputDirectory = string.Empty;
    private string _planName = string.Empty;
    private int _captureWidth = NativeWidth;
    private int _captureHeight = NativeHeight;
    private int _frame = -1;
    private int _shotCursor;
    private int _stepCursor;
    private string? _pendingLabel;
    private bool _finished;
    private int _plannedShots;
    private int? _gameplayZeroFrame;
    private string? _boundary;
    private int[]? _requestedOffsetsMs;
    private readonly List<int> _droppedOffsetsMs = [];

    public event Action? Completed;

    /// <summary>
    /// Frames are chosen to land on settled state, not on transition edges.
    /// FEBack128 runs at 15 fps over 286 frames; at --fixed-fps 60 one video
    /// frame is four engine frames, so multiples of 4 sample a stable video frame.
    /// </summary>
    /// <summary>
    /// First frame on which FEP_MAIN is drawn: the confirm at frame 128 is applied
    /// by this rig's `_Process`, which runs after `RetailFrontendFlow`'s, so the
    /// flow first sees the main menu on the following frame.
    /// </summary>
    private const int MainMenuEntryFrame = 129;

    /// <summary>Every third engine frame — 20 Hz against the underlay's 30 fps.</summary>
    private const int MainMenuSweepStride = 3;

    /// <summary>
    /// 129 + 8 s at 60 fps. The retail reference burst runs to 7,027 ms after the
    /// same origin, so this covers it with a second of margin either side of any
    /// phase lag the sweep might have to reveal.
    /// </summary>
    private const int MainMenuSweepLastFrame = 129 + 480;

    private static readonly (int Frame, string Label, RetailFrontendScreen? Screen)[] StartupPlan =
    [
        (12, "01-click-early", RetailFrontendScreen.ClickToStart),
        (120, "02-click-settled", RetailFrontendScreen.ClickToStart),
        (132, "03-main-menu-entry", RetailFrontendScreen.MainMenu),
        (240, "04-main-menu-settled", RetailFrontendScreen.MainMenu),
        (252, "05-dev-select-entry", RetailFrontendScreen.DevSelect),
        (300, "06-dev-select-settled", RetailFrontendScreen.DevSelect),
        (312, "07-level-select", RetailFrontendScreen.LevelSelect),
        (360, "08-level-select-settled", RetailFrontendScreen.LevelSelect),
        // Retail's SELECT LEVEL leads to MISSION BRIEFING and then to SELECT
        // CONFIGURATION before loading; each has a pristine 640x480 reference
        // frame captured 2026-07-25.
        (372, "09-mission-briefing-entry", RetailFrontendScreen.MissionBriefing),
        (420, "10-mission-briefing-settled", RetailFrontendScreen.MissionBriefing),
        (432, "11-select-configuration-entry", RetailFrontendScreen.SelectConfiguration),
        (480, "12-select-configuration-settled", RetailFrontendScreen.SelectConfiguration),
        // The loading screen is short-lived: at +4 frames the flow has already
        // reached Gameplay. Sample the frame immediately after the confirm.
        (489, "13-loading-handoff", RetailFrontendScreen.Loading),
    ];

    /// <summary>
    /// FEP_OPTIONS and its three subpages, one settled shot each.
    ///
    /// These exist so the options pages can be COMPARED to the retail frames in
    /// local-lab/retail-captures-options-pause-2026-07-27/ rather than eyeballed.
    /// Frames sit well clear of the main-menu entry transition (50 frames from
    /// 129) and of each page change.
    /// </summary>
    private static readonly (int Frame, string Label, RetailFrontendScreen? Screen)[] OptionsPlan =
    [
        (220, "fep-options-root", RetailFrontendScreen.Options),
        (280, "fep-options-controller", RetailFrontendScreen.Options),
        (400, "fep-options-sound", RetailFrontendScreen.Options),
        (520, "fep-options-video", RetailFrontendScreen.Options),
        // Navigation OUT, asserted rather than assumed: two Backs from the Video
        // subpage must land on FEP_MAIN, and the rig fails the run if the screen
        // does not match.
        (600, "fep-options-exit-to-main", RetailFrontendScreen.MainMenu),
    ];

    private static readonly (int Frame, string Action)[] OptionsSteps =
    [
        (128, "confirm"),           // click-to-start -> main menu
        (200, "main-select=5"),     // Options is main-menu row 5
        (204, "confirm"),           // -> FEP_OPTIONS root
        (240, "options-select=0"),  // Controller Options
        (244, "options-confirm"),
        (320, "options-back"),
        (360, "options-select=1"),  // Sound Options
        (364, "options-confirm"),
        (440, "options-back"),
        (480, "options-select=2"),  // Video Options
        (484, "options-confirm"),
        (560, "options-back"),      // Video -> Options root
        (580, "options-back"),      // Options root -> FEP_MAIN
    ];

    private static readonly (int Frame, string Action)[] StartupSteps =
    [
        (128, "confirm"), // click-to-start -> main menu
        (248, "confirm"), // main menu New Game -> FEP_DEVSELECT
        (308, "confirm"), // CHOOSE GAME NAME -> level select
        (368, "confirm"), // level select Level 100 -> mission briefing
        (428, "confirm"), // mission briefing -> select configuration
        (488, "confirm"), // select configuration -> loading
    ];

    public static bool TryCreate(
        IReadOnlyList<string> arguments,
        RetailFrontendFlow frontend,
        out FrontendCaptureRig? rig)
    {
        rig = null;
        string? directory = null;
        string plan = "startup";
        int width = NativeWidth;
        int height = NativeHeight;
        int[]? offsets = null;

        foreach (string argument in arguments)
        {
            if (argument.StartsWith("--capture-dir=", System.StringComparison.Ordinal))
            {
                directory = argument["--capture-dir=".Length..];
            }
            else if (argument.StartsWith("--capture-plan=", System.StringComparison.Ordinal))
            {
                plan = argument["--capture-plan=".Length..];
            }
            else if (argument.StartsWith("--capture-offsets-ms=", System.StringComparison.Ordinal))
            {
                // Lets the caller sample the reconstruction at retail's REALISED
                // offsets instead of the nominal grid, driving the pairing error
                // to zero. See GameplayOffsetsMs for why that matters.
                string[] tokens = argument["--capture-offsets-ms=".Length..]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries);
                var parsed = new List<int>(tokens.Length);
                foreach (string token in tokens)
                {
                    if (!int.TryParse(token, out int offsetMs) || offsetMs < 0)
                    {
                        throw new ArgumentException(
                            $"Malformed --capture-offsets-ms entry '{token}'. " +
                            "Expected non-negative whole milliseconds.");
                    }

                    parsed.Add(offsetMs);
                }

                parsed.Sort();
                offsets = [.. parsed.Distinct()];
                if (offsets.Length == 0)
                {
                    throw new ArgumentException("--capture-offsets-ms listed no offsets.");
                }
            }
            else if (argument.StartsWith("--capture-size=", System.StringComparison.Ordinal))
            {
                string[] parts = argument["--capture-size=".Length..].Split('x');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out width) ||
                    !int.TryParse(parts[1], out height) ||
                    width <= 0 || height <= 0)
                {
                    throw new ArgumentException(
                        $"Malformed --capture-size in '{argument}'. Expected WIDTHxHEIGHT, e.g. 640x480.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        if (!Path.IsPathFullyQualified(directory))
        {
            throw new ArgumentException("Capture mode requires an absolute --capture-dir path.");
        }

        if (plan is not ("startup" or "gameplay" or "mainmenu" or "options"))
        {
            throw new ArgumentException(
                $"Unknown capture plan '{plan}'. Known plans: startup, gameplay, mainmenu, options.");
        }

        rig = new FrontendCaptureRig
        {
            Name = "FrontendCaptureRig",
            _frontend = frontend,
            _outputDirectory = directory,
            _planName = plan,
            _captureWidth = width,
            _captureHeight = height,
            _requestedOffsetsMs = offsets,
        };
        return true;
    }

    /// <summary>
    /// The level offsets the gameplay plan samples, in milliseconds from t0.
    ///
    /// These are the NOMINAL offsets of the retail reference sets:
    /// `opening-pan-run1/` is 0..16000 ms at 250 ms (65 frames) and
    /// `hud-timeline-run1/` is 16000..42000 ms at 1000 ms (27 frames). The
    /// 16000 ms sample is shared, so this list has 65 + 26 = 91 entries.
    ///
    /// Retail's realised offsets jitter by up to ~13 ms around these nominals
    /// (the scheduler drift documented in RETAIL-LEVEL100-GAMEPLAY-CAPTURE),
    /// which is why the pairing tool matches nearest-offset rather than exact.
    /// The reconstruction has no such jitter: every nominal offset below is an
    /// exact engine frame ordinal.
    ///
    /// Pass <c>--capture-offsets-ms=</c> with retail's realised offsets to drive
    /// the pairing error to zero instead; the cost is that an arbitrary
    /// millisecond is rounded to the nearest engine frame, i.e. at most 8.34 ms
    /// (half of 1/60 s) — still three times inside retail's stated +-25 ms.
    /// </summary>
    private IEnumerable<int> GameplayOffsetsMs()
    {
        if (_requestedOffsetsMs is { Length: > 0 } requested)
        {
            return requested;
        }

        return NominalGameplayOffsetsMs();
    }

    private static IEnumerable<int> NominalGameplayOffsetsMs()
    {
        for (int offset = 0; offset <= 16_000; offset += 250)
        {
            yield return offset;
        }

        for (int offset = 17_000; offset <= 42_000; offset += 1_000)
        {
            yield return offset;
        }
    }

    /// <summary>
    /// Converts a retail level offset in milliseconds to an engine frame ordinal
    /// relative to t0.
    ///
    /// <para><b>The arithmetic, and which rate each step converts through.</b></para>
    ///
    /// The engine is launched with <c>--fixed-fps 60</c>, so Godot forces
    /// <c>process_step = 1/60 s</c> and one <c>_Process</c> call is exactly
    /// 1/60 s of logical time regardless of how fast the host actually renders.
    /// Therefore
    ///
    /// <code>frame = round(offsetMs * 60 / 1000) = round(offsetMs * 3 / 50)</code>
    ///
    /// Every offset in the DEFAULT plan is a multiple of 250 ms, and
    /// 250 * 3 / 50 = 15 exactly, so the division is exact for all 91 samples —
    /// no rounding, no accumulated drift, no wall-clock term anywhere in the
    /// schedule. An explicit <c>--capture-offsets-ms</c> list may name arbitrary
    /// milliseconds; those round to the nearest engine frame, bounded by 8.34 ms.
    ///
    /// Downstream of the engine frame there are two further rates, and they are
    /// deliberately NOT the rate the schedule is expressed in:
    ///
    /// <list type="bullet">
    /// <item><b>Simulation:</b> <c>SimulationConstants.TicksPerSecond = 20</c>, so
    /// 3 engine frames per simulation tick, and
    /// <c>simTicks = offsetMs * 20 / 1000 = offsetMs * 2 / 100</c>. A 500 ms
    /// sample is 10 whole ticks and a 250 ms sample is 5 whole ticks. Since the
    /// 20 Hz migration EVERY offset in the default plan lands on a tick
    /// boundary, so no default sample is rendered mid-tick. At 30 Hz a 250 ms
    /// sample was 7.5 ticks and rendered at interpolation alpha 0.5.</item>
    /// <item><b>Retail base update:</b> 20 Hz
    /// (<c>Level100ActorMechanics.RetailBaseTicksPerSecond</c>), which is now
    /// the simulation tick itself, where Core once drove it from the 30 Hz tick
    /// by accumulating 20 thirtieths per tick — 2
    /// retail base updates per 3 simulation ticks. In offset terms
    /// <c>retailUpdates = offsetMs / 50</c>: 5 per 250 ms sample, 20 per 1 s
    /// sample. Both are whole numbers, so no gameplay sample straddles a retail
    /// base update boundary.</item>
    /// </list>
    ///
    /// <para><b>The one wall-clock residue, and its size.</b></para>
    /// <c>FirstFlightGame</c> converts the engine delta to <see cref="TimeSpan"/>
    /// ticks for <c>InteractiveSession</c>:
    /// <c>round(1/60 * 10_000_000) = 166_667</c> against an exact 166_666.667,
    /// i.e. +0.333 TimeSpan ticks (33.3 ns) per engine frame, or +2 us per second
    /// of captured time. Across the full 42 s timeline the simulation therefore
    /// leads the nominal offset by about 84 us. Retail's own matched-offset
    /// pairing is valid to +-25 ms, so this residue is ~300x below the noise of
    /// the reference it is being compared against.
    /// </summary>
    private static int FrameForOffsetMs(int offsetMs) =>
        checked((int)(((long)offsetMs * CaptureFramesPerSecond + 500L) / 1_000L));

    /// <summary>
    /// Retail filename convention, reproduced exactly:
    /// <c>level100-t&lt;offset&gt;ms.png</c> with the offset zero-padded to six
    /// digits. Pairing a reconstruction frame to its retail counterpart is then
    /// a filename operation, not a manual judgement.
    /// </summary>
    private static string GameplayLabel(int offsetMs) => $"level100-t{offsetMs:D6}ms";

    private static string GameplayPhase(int offsetMs) =>
        offsetMs < OpeningPanMs ? "opening-pan" : "settled-playing-camera";

    public override void _Ready()
    {
        _ = Directory.CreateDirectory(_outputDirectory);
        ApplyNativeCaptureViewport();

        if (_planName == "gameplay")
        {
            // Shots cannot be scheduled yet: their frame ordinals are relative to
            // t0, and t0 is the first drawn gameplay frame, which is observed
            // rather than predicted. Only the frontend traversal is scheduled now.
            _plannedShots = GameplayOffsetsMs().Count();
        }
        else if (_planName == "mainmenu")
        {
            // A DENSE SWEEP of FEP_MAIN, and the density is the point.
            //
            // The main-menu underlay is a 30 fps video, so scoring it against a
            // retail frame is only meaningful if the two are at the same video
            // frame. A plan that emits one shot per retail offset cannot tell a
            // wrong drawing from a right drawing at the wrong phase — it reports
            // one number that confounds both. Sweeping every third engine frame
            // across the whole retail burst window lets the phase be MEASURED
            // (best-matching sweep frame per retail frame) instead of assumed, and
            // a constant best-match lag is then a phase error while a scattered
            // one is not a phase error at all.
            //
            // Labels are milliseconds after the main menu is first drawn, which is
            // the same origin the retail bursts are marked from.
            for (int frame = MainMenuEntryFrame; frame <= MainMenuSweepLastFrame; frame += MainMenuSweepStride)
            {
                int offsetMs = (int)Math.Round((frame - MainMenuEntryFrame) * 1000.0 / 60.0);
                _shots.Add(new Shot(
                    frame,
                    $"mainmenu-t{offsetMs:D6}ms",
                    RetailFrontendScreen.MainMenu,
                    null,
                    null));
            }

            _plannedShots = _shots.Count;
        }
        else if (_planName == "options")
        {
            // The retail cursor is excluded from each Options ink band at its
            // measured retail location. Keep our custom cursor at a fixed point
            // outside every scored band so the operator's live mouse cannot
            // turn the comparison into a nondeterministic input.
            _frontend.SetMouseCursorDesignPositionForCapture(Vector2.Zero);
            foreach ((int frame, string label, RetailFrontendScreen? screen) in OptionsPlan)
            {
                _shots.Add(new Shot(frame, label, screen, null, null));
            }

            _plannedShots = _shots.Count;
        }
        else
        {
            foreach ((int frame, string label, RetailFrontendScreen? screen) in StartupPlan)
            {
                _shots.Add(new Shot(frame, label, screen, null, null));
            }

            _plannedShots = _shots.Count;
        }

        // The gameplay plan reuses the startup traversal verbatim: it is the
        // proven path from cold start to Level 100, and reusing it means the
        // in-level capture cannot disagree with the frontend capture about how
        // the level was entered. The mainmenu plan takes only the first confirm,
        // because it must STAY on FEP_MAIN for the whole sweep.
        foreach ((int frame, string action) in
            _planName == "options" ? OptionsSteps : StartupSteps)
        {
            if (_planName == "mainmenu" && frame > StartupSteps[0].Frame)
            {
                break;
            }

            _steps.Add(new Step(frame, action));
        }

        _shots.Sort(static (left, right) => left.Frame.CompareTo(right.Frame));
        _steps.Sort(static (left, right) => left.Frame.CompareTo(right.Frame));

        // FramePostDraw fires after the frame is rasterized, which is the only
        // point at which viewport readback returns the frame _Process just staged.
        RenderingServer.Singleton.Connect(
            RenderingServer.SignalName.FramePostDraw,
            Callable.From(OnFramePostDraw));
    }

    /// <summary>
    /// Forces a 1:1 native composition for the duration of the capture run.
    ///
    /// This is CAPTURE-ONLY and deliberately does not change the shipped window
    /// contract in `project.godot` (1280x720) — that is a product decision, not a
    /// parity concern. `RetailFrontendFlow` letterboxes its 640x480 design stage
    /// with `scale = min(Size.X / 640, Size.Y / 480)`, so at exactly 640x480 the
    /// scale is 1.0 and the offset is zero: every drawn pixel maps to one output
    /// pixel, which is what makes a direct diff against the retail reference
    /// meaningful. At any other size the frame is resampled and sub-pixel layout
    /// error — the kind we are hunting — gets smeared away before it can be seen.
    ///
    /// Content scaling is disabled rather than resized so Godot does not
    /// reintroduce its own stretch transform on top of the flow's.
    /// </summary>
    private void ApplyNativeCaptureViewport()
    {
        Window window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(_captureWidth, _captureHeight);
    }

    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _frame++;

        while (_stepCursor < _steps.Count && _steps[_stepCursor].Frame == _frame)
        {
            ApplyAction(_steps[_stepCursor].Action);
            _stepCursor++;
        }

        if (_planName == "gameplay" && _gameplayZeroFrame is null)
        {
            ArmGameplayTimeline();
            if (_finished)
            {
                return;
            }
        }

        if (_shotCursor < _shots.Count && _shots[_shotCursor].Frame == _frame)
        {
            _pendingLabel = _shots[_shotCursor].Label;
        }
    }

    /// <summary>
    /// Establishes t0 for the gameplay timeline.
    ///
    /// Retail's t0 is "the first client frame whose mean has LEFT the loading
    /// signature 107,115,125" — i.e. the first frame on which the loading screen
    /// is no longer what is being drawn. The reconstruction's exact structural
    /// equivalent is the first frame on which `RetailFrontendFlow` is no longer
    /// on `Loading`: the flow sets `Visible = false` and hands off to gameplay in
    /// the same `_Process` in which it leaves `Loading`, and this rig's
    /// `_Process` runs after the flow's (it is added to the tree later), so the
    /// frame on which this method first sees `Gameplay` is the first frame that
    /// rasterizes the level instead of the loading screen.
    ///
    /// This is an observed edge, not a predicted frame ordinal, for the same
    /// reason retail's is: it is the only crisp, reproducible boundary available,
    /// and hard-coding it would silently mis-zero the whole timeline the moment
    /// the load path changed by one frame.
    /// </summary>
    private void ArmGameplayTimeline()
    {
        if (_frontend.CurrentScreen != RetailFrontendScreen.Gameplay)
        {
            if (_frame >= GameplayArmDeadlineFrame)
            {
                FinishWithBoundary(
                    $"The frontend never reached Gameplay. It was on " +
                    $"{_frontend.CurrentScreen} at frame {_frame} (deadline " +
                    $"{GameplayArmDeadlineFrame}). No in-level frame was captured.");
            }

            return;
        }

        _gameplayZeroFrame = _frame;
        int lastFrame = int.MinValue;
        foreach (int offsetMs in GameplayOffsetsMs())
        {
            int frame = _frame + FrameForOffsetMs(offsetMs);
            if (frame == lastFrame)
            {
                // Two requested offsets closer together than one engine frame
                // (16.67 ms) are the SAME frame. Emitting the file twice would
                // present one rendered frame as two independent samples, so the
                // duplicate is dropped and reported instead.
                _droppedOffsetsMs.Add(offsetMs);
                continue;
            }

            lastFrame = frame;
            _shots.Add(new Shot(
                frame,
                GameplayLabel(offsetMs),
                RetailFrontendScreen.Gameplay,
                offsetMs,
                GameplayPhase(offsetMs)));
        }

        _shots.Sort(static (left, right) => left.Frame.CompareTo(right.Frame));
        _plannedShots = _shots.Count;
    }

    private void ApplyAction(string action)
    {
        switch (action)
        {
            case "confirm":
                _frontend.ConfirmForSmoke();
                break;
            case "options-confirm":
                _frontend.ConfirmOptionsForCapture();
                break;
            case "options-back":
                _frontend.BackFromOptionsForCapture();
                break;
            default:
                if (action.StartsWith("main-select=", System.StringComparison.Ordinal))
                {
                    _frontend.SelectMainIndexForCapture(
                        int.Parse(action["main-select=".Length..]));
                    break;
                }
                if (action.StartsWith("options-select=", System.StringComparison.Ordinal))
                {
                    _frontend.SelectOptionsRowForCapture(
                        int.Parse(action["options-select=".Length..]));
                    break;
                }
                throw new ArgumentException($"Unknown capture action '{action}'.");
        }
    }

    private void OnFramePostDraw()
    {
        if (_pendingLabel is not { } label)
        {
            return;
        }

        _pendingLabel = null;
        Shot shot = _shots[_shotCursor];
        _shotCursor++;

        Image image = GetViewport().GetTexture().GetImage();
        string path = Path.Combine(_outputDirectory, $"{label}.png");
        Error error = image.SavePng(path);

        RetailFrontendScreen actual = _frontend.CurrentScreen;
        var record = new Dictionary<string, object?>
        {
            ["label"] = label,
            ["frame"] = shot.Frame,
            ["path"] = path,
            ["width"] = image.GetWidth(),
            ["height"] = image.GetHeight(),
            ["expectedScreen"] = shot.ExpectScreen?.ToString(),
            ["actualScreen"] = actual.ToString(),
            // A shot taken on the wrong screen is not evidence. Record the
            // disagreement rather than letting a mislabeled PNG be compared.
            ["screenMatched"] = shot.ExpectScreen is null || shot.ExpectScreen == actual,
            ["saveError"] = error == Error.Ok ? null : error.ToString(),
        };

        if (shot.LevelOffsetMs is int offsetMs)
        {
            // Field names and shapes mirror
            // local-lab/retail-reference-pristine/level100-gameplay/manifest.json
            // so a pairing tool reads both manifests with one code path.
            record["file"] = $"{label}.png";
            record["levelOffsetMs"] = offsetMs;
            record["phase"] = shot.Phase;
            record["widthxheight"] = $"{image.GetWidth()}x{image.GetHeight()}";
            record["simulationTick"] = offsetMs * 2 / 100d;
            record["engineFrameFromZero"] = FrameForOffsetMs(offsetMs);
            record["meanRGB"] = MeanRgb(image, error);
            record["sha256"] = error == Error.Ok ? Sha256Hex(path) : null;
        }

        _written.Add(record);

        if (_shotCursor >= _shots.Count)
        {
            _finished = true;
            WriteManifest();
            Completed?.Invoke();
        }
    }

    /// <summary>
    /// Whole-frame mean RGB, recorded so a reader can tell at a glance whether a
    /// captured frame is the level or something else entirely, and so the
    /// reconstruction's per-offset mean can be laid beside the retail manifest's.
    /// Computed AFTER the PNG is written, on a converted copy, so the conversion
    /// cannot influence the bytes on disk.
    /// </summary>
    private static double[]? MeanRgb(Image image, Error saveError)
    {
        if (saveError != Error.Ok)
        {
            return null;
        }

        image.Convert(Image.Format.Rgb8);
        byte[] data = image.GetData();
        int pixels = data.Length / 3;
        if (pixels == 0)
        {
            return null;
        }

        long r = 0;
        long g = 0;
        long b = 0;
        for (int index = 0; index + 2 < data.Length; index += 3)
        {
            r += data[index];
            g += data[index + 1];
            b += data[index + 2];
        }

        return
        [
            Math.Round((double)r / pixels, 1),
            Math.Round((double)g / pixels, 1),
            Math.Round((double)b / pixels, 1),
        ];
    }

    private static string Sha256Hex(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// Ends the run early and records WHY, so a short manifest is a reported
    /// boundary rather than a silent one. A missing frame is never invented.
    /// </summary>
    private void FinishWithBoundary(string reason)
    {
        _boundary = reason;
        _finished = true;
        _pendingLabel = null;
        WriteManifest();
        Completed?.Invoke();
    }

    private void WriteManifest()
    {
        Godot.Collections.Dictionary versionInfo = Engine.GetVersionInfo();
        var manifest = new Dictionary<string, object?>
        {
            ["schema"] = "onslaught-frontend-capture.v2",
            ["plan"] = _planName,
            ["engineVersion"] = versionInfo["string"].AsString(),
            ["viewportWidth"] = GetViewport().GetVisibleRect().Size.X,
            ["viewportHeight"] = GetViewport().GetVisibleRect().Size.Y,
            ["retailReferenceSize"] = "640x480",
            ["plannedShots"] = _plannedShots,
            ["capturedShots"] = _written.Count,
            ["boundary"] = _boundary,
            ["shots"] = _written,
        };

        if (_planName == "gameplay")
        {
            manifest["captureFramesPerSecond"] = CaptureFramesPerSecond;
            manifest["simulationTicksPerSecond"] = SimulationConstants.TicksPerSecond;
            manifest["retailBaseTicksPerSecond"] =
                Level100ActorMechanics.RetailBaseTicksPerSecond;
            manifest["openingPanMs"] = OpeningPanMs;
            manifest["gameplayZeroFrame"] = _gameplayZeroFrame;
            manifest["offsetSource"] =
                _requestedOffsetsMs is null ? "nominal-grid" : "--capture-offsets-ms";
            manifest["droppedOffsetsMs"] = _droppedOffsetsMs;
            manifest["t0Definition"] =
                "first drawn frame on which RetailFrontendFlow has left Loading " +
                "(structural equivalent of retail's 'first client frame whose mean " +
                "left the loading signature 107,115,125')";
            manifest["referenceSet"] =
                "local-lab/retail-reference-pristine/level100-gameplay/manifest.json";
        }

        File.WriteAllText(
            Path.Combine(_outputDirectory, "capture-manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
}
