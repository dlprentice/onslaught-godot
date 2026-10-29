// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The media beats retail plays before the interactive frontend appears, named
/// SEMANTICALLY rather than by file path.
///
/// The names are the released sequencer's own: the intro block at
/// <c>0x004efce3</c>-<c>0x004efee9</c> in pristine <c>BEA.exe</c>
/// (sha256 <c>74154bfa…</c>, image base <c>0x400000</c>) loads
/// <c>"splash.tga"</c> and then plays <c>"ltlogo"</c> and <c>"openingfmv"</c>
/// through the format string <c>"data\video\%s.vid"</c>.
/// </summary>
public enum RetailStartupCue
{
    /// <summary><c>data/video/LTLogo.vid</c> — the Lost Toys bear logo.</summary>
    LostToysLogo,

    /// <summary><c>data/video/OpeningFMV.vid</c> — the title card and gameplay montage.</summary>
    OpeningMontage,

    /// <summary><c>data/textures/splash.tga</c> — the static Lost Toys / Atari card.</summary>
    Splash,

    /// <summary>
    /// <c>data/video/cutscenes/01.vid</c> — the Level 100 campaign cutscene,
    /// 3,095 frames of 480x300 at 25 fps (123.80 s, 32,067,000 bytes).
    ///
    /// <para><b>This enum outgrew its name and is deliberately not renamed.</b>
    /// Its member names are the JSON keys in <c>startup-media.json</c>, so a
    /// rename is a silent cache-format break, and the index reads the file with
    /// <c>Enum.TryParse</c>.</para>
    ///
    /// <para><b>Why this cue exists.</b> It is NOT part of the cold-start chain.
    /// <c>CGame::RunIntroFMV</c> (<c>references/Onslaught/game.cpp:1122-1152</c>)
    /// formats <c>"cutscenes\\%02d"</c> and is called from
    /// <c>CGame::RestartLoopRunLevel</c> at <c>game.cpp:1336-1345</c>, i.e.
    /// AFTER the level has loaded and BEFORE the first gameplay frame. The clip
    /// id comes from the campaign FMV table, read from the pristine specimen
    /// a private pristine copy
    /// (sha256 <c>74154bfa…</c>) at file offset <c>0x23FD70</c>
    /// (VA <c>0x0063FD70</c>), records of
    /// <c>[i32 level][i32 intro][i32 outroA][i32 outroB]</c>. Row 0 reads
    /// <c>100, 1, 2, -1</c>, so Level 100's intro is clip <c>01</c> and its
    /// outro is clip <c>02</c>.</para>
    /// </summary>
    Level100IntroCutscene,
}

/// <summary>
/// How retail draws ONE frame of an FMV — measured from its own draw calls, not
/// inferred from ours.
///
/// <para><b>Provenance.</b> Passive D3D9 proxy capture
/// <c><maintainer evidence>\bea-d3d9-capture\d3d9-20260728-111552.log</c>, 900 presented frames /
/// 896 draws, <c>refusals total=0 warnings=0</c>, specimen verified unchanged
/// after the run. Target a private patched copy
/// (pristine plus the four-byte force-windowed patch at <c>0x12A644</c>), run
/// with NO arguments — the first proxy capture ever taken without
/// <c>-skipfmv</c>. Written up in
/// the private lab note FMV-PRESENTATION-2026-07-28.md.</para>
///
/// <para><b>The whole FMV is one draw per frame:</b>
/// <c>DrawPrimitiveUP</c>, <c>TRIFAN</c>, 2 primitives, 4 vertices,
/// <c>upstride=28</c>, <c>fvf=0x144</c> (XYZRHW + DIFFUSE + one 2-D texture
/// coordinate set); tex0 512x512 <c>D3DFMT_A8R8G8B8</c>, 1 level; alpha blend
/// on with <c>SRCALPHA</c>/<c>INVSRCALPHA</c>/<c>ADD</c>; depth test off, depth
/// write off, cull <c>NONE</c>, unlit, fog off; stage 0 colour op
/// <c>MODULATE</c>, alpha op <c>SELECTARG1</c>; viewport
/// <c>(0,0,640x480)</c>.</para>
///
/// <para>Everything here is MEASURED. The one inferred statement is marked as
/// such at <see cref="FullBrightnessChannel"/>.</para>
/// </summary>
public static class RetailFmvPresentation
{
    /// <summary>The viewport the capture logged: <c>vp = (0,0,640x480)</c>.</summary>
    public const float StageWidth = 640f;

    /// <inheritdoc cref="StageWidth"/>
    public const float StageHeight = 480f;

    /// <summary>
    /// Edge of the drawn quad, from the four logged <c>xyzrhw</c> positions:
    /// <c>(0,40)</c>, <c>(640,40)</c>, <c>(640,440)</c>, <c>(0,440)</c>.
    /// The video is LETTERBOXED, not full-screen — 40-pixel bars top and bottom.
    ///
    /// <para>Independently corroborated from pixels: across the eight
    /// 2026-07-25 intro reference frames the per-pixel maximum over all frames
    /// is zero above y=40 and below y=440, and non-zero across the full width.
    /// Two instruments, one rectangle.</para>
    /// </summary>
    public const float QuadLeft = 0f;

    /// <inheritdoc cref="QuadLeft"/>
    public const float QuadTop = 40f;

    /// <inheritdoc cref="QuadLeft"/>
    public const float QuadRight = 640f;

    /// <inheritdoc cref="QuadLeft"/>
    public const float QuadBottom = 440f;

    public const float QuadWidth = QuadRight - QuadLeft;

    public const float QuadHeight = QuadBottom - QuadTop;

    /// <summary>
    /// Logged texture dimension: <c>tex0 = 512x512 fmt21</c>
    /// (<c>D3DFMT_A8R8G8B8</c>), 1 level.
    ///
    /// <para><b>STATED DIVERGENCE.</b> This reconstruction's decoded frames are
    /// <see cref="SourceWidth"/> x <see cref="SourceHeight"/> textures drawn
    /// whole, not 512x512 textures sampled through
    /// <see cref="MaxU"/>/<see cref="MaxV"/>. The two address exactly the same
    /// texels: retail's UVs select the top-left 480x300 region and nothing else.
    /// The power-of-two allocation is a 2003 D3D9 constraint that Godot does not
    /// have, so reproducing it would cost 40 % more texture memory per buffer to
    /// hold padding that is never sampled.</para>
    ///
    /// <para>The one place the two are not identical is the outermost texel row
    /// and column, where retail's bilinear filter can reach one texel past the
    /// video into the padding and ours clamps to the edge. That is a sub-pixel
    /// difference on the frame border, and it is recorded rather than
    /// asserted away.</para>
    /// </summary>
    public const int TextureSize = 512;

    /// <summary>
    /// The logged texture coordinates are <c>(0,0)</c>, <c>(0.9375,0)</c>,
    /// <c>(0.9375,0.5859)</c>, <c>(0,0.5859)</c>.
    ///
    /// That is what fixes the DECODE SIZE: the video occupies the top-left
    /// <c>0.9375 x 512 = 480</c> by <c>0.5859 x 512 = 300</c> texels of a
    /// power-of-two texture. <c>300/512 = 0.5859375</c>, which the log's four
    /// decimal places print as <c>0.5859</c>.
    /// </summary>
    public const float MaxU = SourceWidth / (float)TextureSize;

    /// <inheritdoc cref="MaxU"/>
    public const float MaxV = SourceHeight / (float)TextureSize;

    /// <summary>Decoded video size implied by <see cref="MaxU"/>/<see cref="MaxV"/>.</summary>
    public const int SourceWidth = 480;

    /// <inheritdoc cref="SourceWidth"/>
    public const int SourceHeight = 300;

    /// <summary>
    /// Every vertex in the capture carries <c>diff=0xFFFEFEFE</c> — alpha
    /// <c>0xFF</c>, and <c>0xFE</c> (254/255 = 0.99608) on each colour channel,
    /// NOT white.
    ///
    /// <para>With stage 0 set to <c>MODULATE</c> the sampled video is multiplied
    /// by that diffuse, so this is the value "full brightness" actually takes.
    /// It is reproduced rather than rounded up to <c>0xFF</c> because rounding it
    /// would discard the only direct evidence of the fade mechanism.</para>
    ///
    /// <para><b>INFERRED, NOT MEASURED:</b> that retail's fade to black sweeps
    /// this diffuse toward zero. The mechanism follows from MODULATE against a
    /// non-unit diffuse and from alpha staying <c>0xFF</c> throughout (so the
    /// blend mode is not what fades it), but these 900 frames hold ONE
    /// brightness level and contain no transition. No fade curve is implemented
    /// here, because none has been measured; a capture across a transition would
    /// settle it and has not been taken.</para>
    /// </summary>
    public const int FullBrightnessChannel = 0xFE;

    /// <summary>
    /// The decoder is DOUBLE-BUFFERED. Two textures alternate strictly, with no
    /// exception anywhere in the captured range:
    /// <c>frame 2 -> 0x0F220EE0, frame 3 -> 0x0F2214C0, frame 4 -> 0x0F220EE0</c>
    /// and so on. One texture is presented while the next frame decodes into the
    /// other.
    /// </summary>
    public const int BufferCount = 2;

    /// <summary>
    /// Which of the <see cref="BufferCount"/> textures a decoded source frame
    /// lands in. Retail alternates per PRESENTED frame; this reconstruction
    /// alternates per DECODED source frame, which is the same law wherever the
    /// engine frame rate differs from the clip's 25 fps and each source frame is
    /// still decoded exactly once.
    /// </summary>
    public static int BufferIndexForFrame(int frameIndex) =>
        ((frameIndex % BufferCount) + BufferCount) % BufferCount;
}

/// <summary>What the startup sequence is drawing at a sampled instant.</summary>
public enum RetailStartupFrameKind
{
    /// <summary>Nothing is drawn. The client is black.</summary>
    Black,

    /// <summary>A decoded video frame is drawn, letterboxed.</summary>
    Video,

    /// <summary>The static splash card is drawn full-frame at <see cref="RetailStartupFrame.Alpha"/>.</summary>
    Splash,

    /// <summary>The sequence is over; the interactive frontend owns the screen.</summary>
    Finished,
}

/// <summary>
/// Timing and geometry of one decoded clip. Populated from the media index the
/// materialize step writes, never guessed: a clip that has not been decoded is
/// ABSENT rather than assumed, so a missing decode can never silently
/// contribute a duration to the schedule.
/// </summary>
public readonly record struct RetailStartupClip(
    int FrameCount,
    int FramesPerSecondNumerator,
    int FramesPerSecondDenominator,
    int Width,
    int Height)
{
    public double FramesPerSecond =>
        FramesPerSecondNumerator / (double)FramesPerSecondDenominator;

    /// <summary>
    /// Duration in seconds, taken from the clip's own frame count and rate.
    /// For <c>LTLogo.vid</c> this is 229 / 25 = 9.16 s and for
    /// <c>OpeningFMV.vid</c> 2054 / 25 = 82.16 s, which is exactly what
    /// <c>ffprobe</c> reports for the shipped Bink files.
    /// </summary>
    public double DurationSeconds =>
        FrameCount * FramesPerSecondDenominator / (double)FramesPerSecondNumerator;
}

/// <summary>
/// One decoded Bink audio track belonging to a clip.
///
/// <para><b>Which track, and why there is no selector.</b></para>
/// <c>cutscenes/01.vid</c> carries FIVE <c>binkaudio_rdft</c> 44.1 kHz stereo
/// tracks, one per shipped language, and <b>English is track 0</b>. That is read
/// out of the pristine specimen
/// a private pristine copy (sha256
/// <c>74154bfa…</c>), file offset = VA − <c>0x400000</c>: the five arms of
/// <c>CText::Init</c>'s jump table at <c>0x004f2498</c> load, by case,
/// <c>0x00632D74</c> "english", <c>0x00632D7C</c> "french", <c>0x00632D94</c>
/// "german", <c>0x00632D84</c> "spanish", <c>0x00632D8C</c> "italian" — the arms
/// load OUT of address order, so reading the string table in address order gives
/// the wrong enum. <c>g_LanguageIndex</c> is <c>g_Text + 0x1c</c> =
/// <c>0x0083d97c</c>; <c>CFMV::PlayFullscreenWithLoadingGate</c>
/// (<c>0x00465640</c>) forwards <c>localise ? g_LanguageIndex : 0</c> into vtable
/// slot <c>+0x2c</c>; and five hops later <c>CBinkOpenThread::VFunc_0</c>
/// (<c>0x00541140</c>) passes it VERBATIM to <c>BINKW32::BinkSetSoundTrack</c> at
/// <c>0x0054116d</c> — no <c>+1</c>, no remap. <c>localise = FALSE</c> forces 0,
/// so 0 is also the fallback, and <c>CLIParams.cpp:64</c> defaults
/// <c>mLanguage = LANG_ENGLISH</c>.
///
/// <para>Tracks 1-4 are identified and deliberately NOT exposed. Nothing in this
/// reconstruction selects a language, so a selector would be a surface with no
/// consumer and no measured wiring behind it.</para>
/// </summary>
public readonly record struct RetailStartupClipAudio(
    int Track,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    long SampleFrameCount)
{
    /// <summary>
    /// Length of the decoded track in seconds.
    ///
    /// <para>This is expected to be slightly LONGER than the clip's video, never
    /// shorter: binkaudio emits whole 2,048-sample frames at 44.1 kHz, so the
    /// last one overruns. For <c>01.vid</c> the measured overhang is 900 sample
    /// frames — 123.820408 s of audio against 123.80 s of video, 0.0204 s, half
    /// of one 25 fps video frame.</para>
    /// </summary>
    public double DurationSeconds => SampleFrameCount / (double)SampleRate;
}

/// <summary>A sample of the startup sequence at one instant.</summary>
/// <param name="BeatSeconds">
/// Elapsed time WITHIN the current beat. The video frame index is quantised to
/// the clip's 25 fps, so it cannot serve as an audio start offset without
/// snapping the sound to a 40 ms grid; this is the unquantised value.
/// </param>
public readonly record struct RetailStartupFrame(
    RetailStartupFrameKind Kind,
    RetailStartupCue? Cue,
    int FrameIndex,
    float Alpha,
    double BeatSeconds);

/// <summary>
/// The released cold-start media sequence, as a pure deterministic function of
/// elapsed seconds. No Godot types, no filesystem, no clock — it is handed the
/// elapsed time by whichever clock the host injected, so the same schedule
/// serves a fixed capture tick and a wall clock without a second code path.
///
/// <para><b>Order, and where each beat comes from.</b></para>
/// The order is the disassembled sequencer's, not a guess. On the branch the PC
/// build takes (<c>[0x662f28] != 0</c>) it is
/// <c>Play("ltlogo")</c> → <c>Play("openingfmv")</c>, with the splash texture
/// loaded before the chain and released after it.
///
/// <para><b>The nVidia screen, and a claim that had to be withdrawn.</b></para>
/// The gated <c>Play("TWIMTBP_GefFX_640x480_Audio")</c> between them is not
/// reproduced. The first reason offered for that was WRONG, and is recorded
/// here so it is not re-derived: an exact scan of <c>.text</c> for the
/// little-endian dword <c>0x0083d404</c> finds three hits, all reads, which was
/// read as "no writer exists, therefore the screen is dead code". It is not a
/// plain BSS dword. <c>0x004efae0</c> runs
/// a call to 0x528aa0 on the object at 0x83d3f8 with the string "TWIMTBP" (at 0x632b28) and a zero argument,
/// constructing a console variable named <c>TWIMTBP</c> at <c>0x83d3f8</c>, and
/// <c>CVar::Init</c> stores its value with a store of ecx to field +0xc of eax —
/// and <c>0x83d3f8 + 0xc</c> IS <c>0x83d404</c>. The store is object-relative,
/// so an absolute-address scan cannot see it. The shipped <c>cardid.txt</c>
/// then carries <c>Tweak:TWIMTBP 1</c> under exactly one device:
/// <c>Vendor:10DE nVidia / Device:0330 NVIDIA GeForce FX 5900 Ultra</c>.
///
/// So the screen is reachable BY DESIGN, on one 2003 adapter. It is omitted
/// because no modern adapter matches that tweak entry and it demonstrably did
/// not play on the measured hardware — not because it is unreachable. See
/// the private lab note SPLASH-AND-INTRO-FMV-2026-07-26.md.
///
/// <para><b>The two constants that are NOT derived from a shipped byte.</b></para>
/// <see cref="InterClipBlackSeconds"/> and the splash fade/hold are measured
/// from captures of the released build and are labelled as such at each field.
/// They are observations of what a player sees, not recovered constants, and
/// they are the only numbers here that a better measurement should overwrite.
/// </summary>
public sealed class RetailStartupSchedule
{
    /// <summary>
    /// The black interval between LTLogo ending and OpeningFMV appearing.
    ///
    /// ZERO, AND THAT IS A CORRECTION. Two capture runs on 2026-07-26 showed
    /// last LTLogo content at t=9.2 s and first OpeningFMV content at t=11.4 s
    /// with pure black between, which read as a ~1.9 s authored gap. It is not
    /// one.
    ///
    /// Matching each of the eight 2026-07-25 intro reference frames against the
    /// decoded clips locates the exact source frame for every one of them
    /// (whole-frame mean |delta| 1.22-3.02). The implied clip start times are
    /// LTLogo at t=0.987 s (sd 0.023 over 3 frames) and OpeningFMV at
    /// t=10.200 s (sd 0.089 over 5 frames), while LTLogo's own 9.16 s duration
    /// puts its end at 10.147 s. The gap in THAT run was therefore
    /// <b>+0.053 s</b> — the two clips are contiguous.
    ///
    /// So the black is Bink close/open latency on a cold file cache, not a
    /// beat: it is present in one run and absent in another of the same build.
    /// Reproducing 1.9 s of it would have been fabricating an authored pause
    /// out of one machine's disk behaviour.
    /// </summary>
    public const double InterClipBlackSeconds = 0.00;

    /// <summary>
    /// The splash card's ramp from black to full.
    ///
    /// MEASURED, NOT RECOVERED. Least-squares alpha of the captured frame
    /// against the settled splash frame, over the 72,005 pixels brighter than
    /// 32: 0.165 at t=88.0 s, 0.521 at 88.5, 0.852 at 89.0, 1.000 at 89.5 —
    /// a straight ramp of about 0.67 per second, i.e. ~1.5 s end to end.
    /// </summary>
    public const double SplashFadeInSeconds = 1.50;

    /// <summary>
    /// How long the splash card holds at full before the client goes black.
    ///
    /// MEASURED, NOT RECOVERED. Alpha is exactly 1.000 at t=89.5, 90.0, 90.5,
    /// 91.0, 91.5 and 92.0 s and exactly 0 at 92.5 s, and the 89.6-92.1 s
    /// frames are byte-identical to a frame captured at t=90 s in a SEPARATE
    /// run. There is no fade out; the cut to black is abrupt.
    /// </summary>
    public const double SplashHoldSeconds = 3.00;

    private readonly List<Beat> _beats = [];
    private readonly List<RetailStartupCue> _missing = [];

    private readonly record struct Beat(
        RetailStartupFrameKind Kind,
        RetailStartupCue? Cue,
        double StartSeconds,
        double DurationSeconds,
        bool FadeIn,
        double FramesPerSecond,
        int FrameCount);

    /// <summary>
    /// Builds the schedule from whatever media is actually present.
    /// </summary>
    /// <param name="clips">Decoded video clips, keyed by cue. A cue absent from
    /// this map contributes NO time and is reported in <see cref="MissingCues"/>.</param>
    /// <param name="splashPresent">Whether the splash still was materialized.</param>
    public RetailStartupSchedule(
        IReadOnlyDictionary<RetailStartupCue, RetailStartupClip> clips,
        bool splashPresent)
    {
        ArgumentNullException.ThrowIfNull(clips);

        double cursor = 0d;

        cursor = AppendVideo(RetailStartupCue.LostToysLogo, clips, cursor);

        // The gap only exists BETWEEN two clips. If either side is absent there
        // is nothing to sit between, and holding black anyway would invent a
        // beat retail never shows on that path.
        if (clips.ContainsKey(RetailStartupCue.LostToysLogo) &&
            clips.ContainsKey(RetailStartupCue.OpeningMontage))
        {
            _beats.Add(new Beat(
                RetailStartupFrameKind.Black, null, cursor, InterClipBlackSeconds,
                false, 0d, 0));
            cursor += InterClipBlackSeconds;
        }

        cursor = AppendVideo(RetailStartupCue.OpeningMontage, clips, cursor);

        if (splashPresent)
        {
            _beats.Add(new Beat(
                RetailStartupFrameKind.Splash, RetailStartupCue.Splash, cursor,
                SplashFadeInSeconds, true, 0d, 0));
            cursor += SplashFadeInSeconds;
            _beats.Add(new Beat(
                RetailStartupFrameKind.Splash, RetailStartupCue.Splash, cursor,
                SplashHoldSeconds, false, 0d, 0));
            cursor += SplashHoldSeconds;
        }
        else
        {
            _missing.Add(RetailStartupCue.Splash);
        }

        TotalSeconds = cursor;
    }

    /// <summary>
    /// Whether any FMV should play at all on this run.
    ///
    /// <para><c>--skipfmv</c> is RETAIL'S own gate, reproduced by name.
    /// <c>CGame::GetIntroFMV</c>
    /// (<c>references/Onslaught/game.cpp:1103-1119</c>) opens with
    /// <c>if (CLIPARAMS.mSkipFMV) return -1;</c>, and every FMV route consults
    /// it, so the one flag suppresses the cold-start chain and the level
    /// cutscene alike.</para>
    ///
    /// <para><c>--smoke</c> and the capture arguments are OURS, not retail's:
    /// the smoke route and the frontend capture plan are frame-counted, and a
    /// two-minute movie inserted into either would make them time out rather
    /// than fail. <c>--intro</c> is also ours and overrides all of it, so a
    /// human or a future rig can always force the movie on — without it the
    /// intro would have no observed path at all, which is exactly how
    /// <c>_feBackFrames</c> came to be loaded and never drawn.</para>
    ///
    /// <para><c>FirstFlightGame.StartRetailStartupMedia</c> and the Level 100
    /// intro cutscene both call this method. Attract restart still splits
    /// smoke/capture from <c>-skipfmv</c> because that loop is not
    /// <c>GetIntroFMV</c>.</para>
    /// </summary>
    public static bool IsSuppressedByArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        bool forced = false;
        bool suppressed = false;
        foreach (string argument in arguments)
        {
            if (argument == "--intro")
            {
                forced = true;
            }
            else if (argument == "--skipfmv" || argument == "--smoke" ||
                     argument.StartsWith("--capture-dir=", StringComparison.Ordinal) ||
                     argument.StartsWith("--capture-plan=", StringComparison.Ordinal) ||
                     argument.StartsWith("--capture-size=", StringComparison.Ordinal) ||
                     argument.StartsWith("--capture-offsets-ms=", StringComparison.Ordinal))
            {
                suppressed = true;
            }
        }

        return !forced && suppressed;
    }

    /// <summary>
    /// A schedule of exactly ONE clip and nothing else — no splash, no
    /// inter-clip black, no chain.
    ///
    /// This is what retail's level cutscenes are. <c>CGame::RunIntroFMV</c>
    /// (<c>references/Onslaught/game.cpp:1122-1152</c>) makes a single
    /// <c>FMV.PlayFullscreen("cutscenes\\NN", FALSE, localise)</c> call; there is
    /// no sequencer around it and nothing else is drawn. Reusing the cold-start
    /// chain's constructor and hoping the other beats stayed absent would make
    /// the difference implicit, so it is a separate entry point.
    ///
    /// A cue with no decoded clip yields an EMPTY schedule, reported through
    /// <see cref="MissingCues"/>. It is never padded with black.
    /// </summary>
    public static RetailStartupSchedule ForSingleClip(
        RetailStartupCue cue,
        IReadOnlyDictionary<RetailStartupCue, RetailStartupClip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        return new RetailStartupSchedule(cue, clips);
    }

    /// <summary>
    /// Attract restart: <c>Play("ltlogo")</c> then <c>Play("openingfmv")</c>.
    /// No splash beat, no publisher, no TWIMTBP. See
    /// <see cref="RetailAttractLoop"/>.
    /// </summary>
    public static RetailStartupSchedule ForAttractRestart(
        IReadOnlyDictionary<RetailStartupCue, RetailStartupClip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        return new RetailStartupSchedule(clips, new AttractRestartMark());
    }

    private RetailStartupSchedule(
        RetailStartupCue cue,
        IReadOnlyDictionary<RetailStartupCue, RetailStartupClip> clips)
    {
        TotalSeconds = AppendVideo(cue, clips, 0d);
    }

    private readonly struct AttractRestartMark
    {
    }

    private RetailStartupSchedule(
        IReadOnlyDictionary<RetailStartupCue, RetailStartupClip> clips,
        AttractRestartMark _)
    {
        ArgumentNullException.ThrowIfNull(clips);

        double cursor = 0d;
        cursor = AppendVideo(RetailStartupCue.LostToysLogo, clips, cursor);
        if (clips.ContainsKey(RetailStartupCue.LostToysLogo) &&
            clips.ContainsKey(RetailStartupCue.OpeningMontage))
        {
            _beats.Add(new Beat(
                RetailStartupFrameKind.Black, null, cursor, InterClipBlackSeconds,
                false, 0d, 0));
            cursor += InterClipBlackSeconds;
        }

        cursor = AppendVideo(RetailStartupCue.OpeningMontage, clips, cursor);
        TotalSeconds = cursor;
    }

    /// <summary>Total length of the sequence, in seconds of injected time.</summary>
    public double TotalSeconds { get; }

    /// <summary>
    /// Cues the media index did not supply. The sequence still runs; the beat is
    /// simply absent. Nothing is drawn in its place and nothing is imitated.
    /// </summary>
    public IReadOnlyList<RetailStartupCue> MissingCues => _missing;

    /// <summary>
    /// True when no beat has any content at all, i.e. the whole sequence would
    /// be a no-op. The host uses this to hand straight over to the frontend
    /// rather than holding a black screen for nothing.
    /// </summary>
    public bool IsEmpty => _beats.Count == 0;

    /// <summary>
    /// What to draw at <paramref name="elapsedSeconds"/>. A negative time is
    /// treated as zero; past the end the result is
    /// <see cref="RetailStartupFrameKind.Finished"/>.
    /// </summary>
    public RetailStartupFrame Sample(double elapsedSeconds)
    {
        double time = Math.Max(0d, elapsedSeconds);
        if (time >= TotalSeconds)
        {
            return new RetailStartupFrame(RetailStartupFrameKind.Finished, null, 0, 0f, 0d);
        }

        foreach (Beat beat in _beats)
        {
            if (time >= beat.StartSeconds + beat.DurationSeconds)
            {
                continue;
            }

            double local = time - beat.StartSeconds;
            switch (beat.Kind)
            {
                case RetailStartupFrameKind.Video:
                    // floor(local * fps): frame n covers [n/fps, (n+1)/fps).
                    // At 25 fps under a 60 Hz fixed tick this repeats each video
                    // frame for two or three engine frames in a fixed pattern,
                    // which is deterministic because `local` is.
                    int index = (int)Math.Floor(local * beat.FramesPerSecond);
                    index = Math.Clamp(index, 0, beat.FrameCount - 1);
                    return new RetailStartupFrame(
                        RetailStartupFrameKind.Video, beat.Cue, index, 1f, local);

                case RetailStartupFrameKind.Splash:
                    float alpha = beat.FadeIn
                        ? (float)Math.Clamp(local / beat.DurationSeconds, 0d, 1d)
                        : 1f;
                    return new RetailStartupFrame(
                        RetailStartupFrameKind.Splash, beat.Cue, 0, alpha, local);

                default:
                    return new RetailStartupFrame(
                        RetailStartupFrameKind.Black, null, 0, 0f, local);
            }
        }

        return new RetailStartupFrame(RetailStartupFrameKind.Finished, null, 0, 0f, 0d);
    }

    private double AppendVideo(
        RetailStartupCue cue,
        IReadOnlyDictionary<RetailStartupCue, RetailStartupClip> clips,
        double cursor)
    {
        if (!clips.TryGetValue(cue, out RetailStartupClip clip) || clip.FrameCount <= 0)
        {
            _missing.Add(cue);
            return cursor;
        }

        _beats.Add(new Beat(
            RetailStartupFrameKind.Video,
            cue,
            cursor,
            clip.DurationSeconds,
            false,
            clip.FramesPerSecond,
            clip.FrameCount));
        return cursor + clip.DurationSeconds;
    }
}
