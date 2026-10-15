// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Which clock drives the startup media.
/// </summary>
public enum RetailStartupClockMode
{
    /// <summary>
    /// One <c>_Process</c> call advances the sequence by exactly
    /// <c>1 / TicksPerSecond</c> seconds, regardless of the delta the engine
    /// reports. Used for capture and for any parity comparison: the frame
    /// chosen at tick N is then a pure function of N.
    /// </summary>
    FixedTick,

    /// <summary>
    /// The engine's own frame delta advances the sequence. Used for interactive
    /// play, where a dropped frame must cost time rather than slow the movie
    /// down. This is the released behaviour: retail's Bink path is clock-paced
    /// and logs "%i frames played, %i frames skipped".
    /// </summary>
    Wall,
}

/// <summary>
/// Retail's FMV player. It serves both the cold-start chain (the Lost Toys logo
/// movie, the opening montage, the static splash card) and a single level
/// cutscene played through <see cref="InitializeForClip"/> — which is what
/// retail does too, since both routes end in the same <c>FMV.PlayFullscreen</c>
/// and the D3D9 capture shows one presentation path.
///
/// <para><b>This is presentation only.</b></para>
/// It touches textures, the filesystem and a clock, so it can never be
/// referenced from <c>OnslaughtRebuild.Core</c>. The deterministic part — which
/// beat is on screen at time t, which video frame, and every measured value of
/// the draw itself — lives in <see cref="RetailStartupSchedule"/> and
/// <see cref="RetailFmvPresentation"/>, which have no Godot types and are unit
/// tested directly.
///
/// <para><b>Residency.</b></para>
/// Exactly <see cref="RetailFmvPresentation.BufferCount"/> decoded frames are
/// resident, matching retail's measured double buffering. Each frame is read
/// from the media cache as a PNG and pushed into one of two reused
/// <see cref="ImageTexture"/> handles. Building a texture per frame — the
/// pattern the FEBack strip loader uses for its 286 128² frames — would mean
/// 2,054 textures at 480×300 for the montage and 3,095 for the Level 100
/// cutscene, and is deliberately not done here.
///
/// <para><b>Audio.</b></para>
/// A clip that has a decoded Bink track plays it through one
/// <see cref="AudioStreamPlayer"/> child, started on the same engine frame the
/// beat's first video frame is selected and stopped in <see cref="Finish"/> and
/// <see cref="_ExitTree"/> — so the track cannot outlive the movie on any path,
/// including an abort or an early teardown. For the Level 100 cutscene that
/// track is <b>Bink track 0, English</b>; see
/// <see cref="RetailStartupClipAudio"/> for the byte evidence. A clip with no
/// decoded track plays silent, which is not a defect.
///
/// <para><b>Nothing is imitated.</b></para>
/// If a clip was not decoded, its beat does not exist and nothing is drawn in
/// its place. If NO media at all is available the sequence reports that and
/// hands straight over to the frontend; it never substitutes hand-made motion
/// for retail footage, and it never substitutes generated sound for retail
/// audio.
/// </summary>
public sealed partial class RetailStartupSequence : Control
{
    private const float DesignWidth = RetailFmvPresentation.StageWidth;
    private const float DesignHeight = RetailFmvPresentation.StageHeight;

    /// <summary>
    /// The capture tick. <c>FrontendCaptureRig</c> launches the engine with
    /// <c>--fixed-fps 60</c>, so this matches one <c>_Process</c> call to
    /// 1/60 s of sequence time exactly.
    /// </summary>
    private const double FixedTicksPerSecond = 60d;

    /// <summary>
    /// Retail's measured "full brightness" vertex diffuse, <c>0xFFFEFEFE</c>,
    /// applied here as Godot's canvas modulate against the same MODULATE
    /// semantics. See <see cref="RetailFmvPresentation.FullBrightnessChannel"/>
    /// for the evidence and for what about it is inferred.
    /// </summary>
    private static readonly Color FullBrightnessDiffuse = Color.Color8(
        (byte)RetailFmvPresentation.FullBrightnessChannel,
        (byte)RetailFmvPresentation.FullBrightnessChannel,
        (byte)RetailFmvPresentation.FullBrightnessChannel);

    private readonly ImageTexture?[] _videoBuffers =
        new ImageTexture?[RetailFmvPresentation.BufferCount];

    private RetailStartupMediaIndex _media = RetailStartupMediaIndex.Missing("not initialized");
    private RetailStartupSchedule _schedule = null!;
    private RetailStartupClockMode _clock = RetailStartupClockMode.Wall;
    private Texture2D? _splashTexture;
    private AudioStreamPlayer? _voice;
    private RetailStartupCue? _voiceCue;
    private (RetailStartupCue Cue, int Index)? _residentFrame;
    private int _presentedBuffer;
    private double _elapsedSeconds;
    private bool _aborted;
    private bool _completed;
    private bool _initialized;

    internal AudioPlaybackRetirement PlaybackRetirement { get; set; } = null!;

    /// <summary>Raised once, on the frame the sequence stops owning the screen.</summary>
    public event Action? Completed;

    /// <summary>The cues that had no decoded media. Empty when everything played.</summary>
    public IReadOnlyList<RetailStartupCue> MissingCues => _schedule.MissingCues;

    /// <summary>Why no media was available, or null when the cache was readable.</summary>
    public string? MediaUnavailableReason => _media.Unavailable;

    /// <summary>Total scheduled length in seconds, before any user skip.</summary>
    public double ScheduledSeconds => _schedule.TotalSeconds;

    /// <summary>
    /// Resolves the media cache root. It is deliberately never under
    /// <c>res://</c> — see <see cref="RetailStartupMediaIndex"/> for why.
    /// </summary>
    /// <param name="arguments">Engine user arguments; <c>--startup-media=DIR</c> wins.</param>
    public static string ResolveMediaRoot(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        foreach (string argument in arguments)
        {
            if (argument.StartsWith("--startup-media=", StringComparison.Ordinal))
            {
                return argument["--startup-media=".Length..];
            }
        }

        string? configured = System.Environment.GetEnvironmentVariable("ONSLAUGHT_STARTUP_MEDIA");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        string? local = System.Environment.GetEnvironmentVariable("LOCALAPPDATA");
        return string.IsNullOrWhiteSpace(local)
            ? string.Empty
            : Path.Combine(local, "OnslaughtToolkit", "startup-media");
    }

    /// <summary>The cold-start chain: Lost Toys logo, opening montage, splash.</summary>
    public void Initialize(string mediaRoot, RetailStartupClockMode clock)
    {
        BeginInitialize(clock);
        _media = RetailStartupMediaIndex.Load(mediaRoot, File.Exists);
        _schedule = new RetailStartupSchedule(_media.Clips, _media.HasSplash);

        if (_media.HasSplash && _media.SplashRelativePath is { } splash)
        {
            _splashTexture = LoadImageTexture(Path.Combine(_media.Root, splash));
            if (_splashTexture is null)
            {
                // The index said it was there and the read failed. Rebuild the
                // schedule without it rather than drawing a stand-in.
                _schedule = new RetailStartupSchedule(_media.Clips, false);
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// One clip, alone: retail's <c>FMV.PlayFullscreen</c> call for a level
    /// cutscene. No splash, no chain, no black padding.
    /// </summary>
    public void InitializeForClip(
        string mediaRoot, RetailStartupCue cue, RetailStartupClockMode clock)
    {
        BeginInitialize(clock);
        _media = RetailStartupMediaIndex.Load(mediaRoot, File.Exists);
        _schedule = RetailStartupSchedule.ForSingleClip(cue, _media.Clips);
        _initialized = true;
    }

    /// <summary>
    /// Attract restart: <c>ltlogo</c> then <c>openingfmv</c>, then the host
    /// re-enters click-to-start. No splash beat. See
    /// <see cref="RetailAttractLoop"/>.
    /// </summary>
    public void InitializeForAttract(string mediaRoot, RetailStartupClockMode clock)
    {
        BeginInitialize(clock);
        _media = RetailStartupMediaIndex.Load(mediaRoot, File.Exists);
        _schedule = RetailStartupSchedule.ForAttractRestart(_media.Clips);
        _initialized = true;
    }

    private void BeginInitialize(RetailStartupClockMode clock)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("The startup sequence is already initialized.");
        }

        _clock = clock;
    }

    public override void _Ready()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "Initialize the startup sequence before adding it to the tree.");
        }

        AnchorRight = 1f;
        AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Ignore;
        // Above the frontend flow's ZIndex of 100: while the sequence owns the
        // screen the frontend is not processing, but this makes the ordering
        // explicit rather than dependent on child order.
        ZIndex = 200;

        if (_schedule.IsEmpty)
        {
            if (_media.Unavailable is { } reason)
            {
                GD.PushWarning(
                    $"Startup media unavailable, so splash and intro FMV are absent: {reason}");
            }

            Finish();
            return;
        }

        CreateVoicePlayer();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_completed)
        {
            return;
        }

        _elapsedSeconds += _clock == RetailStartupClockMode.FixedTick
            ? 1d / FixedTicksPerSecond
            : Math.Max(0d, delta);

        if (_aborted || _elapsedSeconds >= _schedule.TotalSeconds)
        {
            Finish();
            return;
        }

        // Before the redraw, not after: the movie and its voice must begin on
        // the same engine frame, and a deferred _Draw would put the first
        // decoded frame ahead of the first sample.
        UpdateVoiceTrack(_schedule.Sample(_elapsedSeconds));
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        // A track that outlives its movie is worse than silence. Finish()
        // already stops it on completion and on abort; this covers the paths
        // that never reach Finish() at all — the flow freeing the node, the
        // scene tree being torn down, the game quitting mid-cutscene.
        StopVoiceTrack();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_completed)
        {
            return;
        }

        // CFMV::ReceiveButtonAction 0x004656E0 plus the three backend mouse
        // latches at 0x0053F2EB. Any other key or pad used to abort here; that
        // was not the specimen law. See RetailFmvSkip.
        // One abort still skips every remaining clip: the sequencer's
        // `test eax,eax / jne done` after each Play().
        bool abort = inputEvent switch
        {
            InputEventMouseButton button when button.Pressed =>
                RetailFrontendScenePath.AcceptsStartupSkip(
                    left: button.ButtonIndex == MouseButton.Left,
                    middle: button.ButtonIndex == MouseButton.Middle,
                    right: button.ButtonIndex == MouseButton.Right,
                    dik: 0),
            InputEventKey key when key.Pressed && !key.Echo =>
                RetailFrontendScenePath.AcceptsStartupSkip(
                    left: false,
                    middle: false,
                    right: false,
                    dik: ScanCodeFor(key.Keycode)),
            _ => false,
        };

        if (!abort)
        {
            return;
        }

        _aborted = true;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Skips the sequence from code. Used by the smoke harness.</summary>
    public void AbortForHarness() => _aborted = true;

    /// <summary>
    /// DIK scan codes for the four default skip-cutscene rows. Anything else
    /// returns 0, which is not in <see cref="RetailFmvSkip.DefaultSkipScanCodes"/>.
    /// </summary>
    private static int ScanCodeFor(Key key) => key switch
    {
        Key.Space => 0x39,
        Key.Enter => 0x1C,
        Key.Escape => 0x01,
        Key.KpEnter => 0x9C,
        _ => 0,
    };

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);

        RetailStartupFrame frame = _schedule.Sample(_elapsedSeconds);
        if (frame.Kind is RetailStartupFrameKind.Black or RetailStartupFrameKind.Finished)
        {
            return;
        }

        float scale = Mathf.Min(Size.X / DesignWidth, Size.Y / DesignHeight);
        var offset = new Vector2(
            (Size.X - (DesignWidth * scale)) * 0.5f,
            (Size.Y - (DesignHeight * scale)) * 0.5f);
        DrawSetTransform(offset, 0f, new Vector2(scale, scale));

        if (frame.Kind == RetailStartupFrameKind.Splash)
        {
            if (_splashTexture is { } splash)
            {
                DrawTextureRect(
                    splash,
                    new Rect2(0f, 0f, DesignWidth, DesignHeight),
                    false,
                    new Color(1f, 1f, 1f, frame.Alpha));
            }
        }
        else if (frame.Cue is { } cue && EnsureFrameResident(cue, frame.FrameIndex))
        {
            // The measured quad and the measured diffuse. Retail draws one
            // TRIFAN at (0,40)-(640,440) with stage 0 MODULATE against
            // 0xFFFEFEFE; Godot's canvas modulate multiplies the sampled texel
            // by the same value, and a CanvasItem's default Mix blend IS
            // SRCALPHA/INVSRCALPHA, which is what the capture logged
            // (ab=1 sb=5 db=6 bop=1). The remaining logged states — depth test
            // off, depth write off, cull NONE, unlit, fog off — are properties
            // of drawing on a 2D canvas at all, so they are satisfied by
            // construction rather than by a setting.
            DrawTextureRect(
                _videoBuffers[_presentedBuffer]!,
                new Rect2(
                    RetailFmvPresentation.QuadLeft,
                    RetailFmvPresentation.QuadTop,
                    RetailFmvPresentation.QuadWidth,
                    RetailFmvPresentation.QuadHeight),
                false,
                FullBrightnessDiffuse);
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    /// <summary>
    /// Brings one decoded frame into the buffer retail would have decoded it
    /// into, and presents that buffer. Returns false if the frame could not be
    /// read, in which case NOTHING is drawn for it — the letterbox stays black
    /// rather than repeating the previous frame, because a held frame would read
    /// as a stall in the footage that retail does not have.
    ///
    /// <para><b>Two textures, not one.</b> Retail's decoder is double-buffered
    /// and the capture shows the two textures alternating strictly, with no
    /// exception across 896 draws. The frame being presented is therefore never
    /// the frame being written, which is the property this reproduces via
    /// <see cref="RetailFmvPresentation.BufferIndexForFrame"/>. Only
    /// <see cref="RetailFmvPresentation.BufferCount"/> textures are ever
    /// resident: the 2,054-frame montage and the 3,095-frame Level 100 cutscene
    /// both rule out the FEBack strip loader's texture-per-frame pattern.</para>
    /// </summary>
    private bool EnsureFrameResident(RetailStartupCue cue, int frameIndex)
    {
        if (_residentFrame == (cue, frameIndex) &&
            _videoBuffers[_presentedBuffer] is not null)
        {
            return true;
        }

        string path;
        try
        {
            path = Path.Combine(_media.Root, _media.FrameRelativePath(cue, frameIndex));
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Startup media {cue} frame {frameIndex}: {exception.Message}");
            return false;
        }

        var image = new Image();
        if (image.Load(path) != Error.Ok)
        {
            GD.PushWarning($"Startup media {cue} frame {frameIndex} unreadable at {path}.");
            return false;
        }

        int target = RetailFmvPresentation.BufferIndexForFrame(frameIndex);
        ImageTexture? buffer = _videoBuffers[target];
        if (buffer is null ||
            buffer.GetWidth() != image.GetWidth() ||
            buffer.GetHeight() != image.GetHeight())
        {
            _videoBuffers[target] = ImageTexture.CreateFromImage(image);
        }
        else
        {
            buffer.Update(image);
        }

        _presentedBuffer = target;
        _residentFrame = (cue, frameIndex);
        return true;
    }

    /// <summary>
    /// Adds the one <see cref="AudioStreamPlayer"/> this sequence ever owns, if
    /// any beat in the schedule has a decoded track AND the clock is wall time.
    ///
    /// <para><b>Why the clock decides.</b> Audio playback is paced by the sound
    /// device; <see cref="RetailStartupClockMode.FixedTick"/> deliberately
    /// advances sequence time by <c>1/60 s</c> per <c>_Process</c> call
    /// regardless of how long that call took, precisely so a capture is a pure
    /// function of the tick index. Under that clock the two would desynchronise
    /// by construction, and a sound device is exactly the kind of
    /// nondeterminism a parity capture must not contain. So capture runs stay
    /// silent — which is also what the harness needs.</para>
    ///
    /// <para><b>Unity gain on the master bus, and that is a CHOICE.</b> Retail's
    /// Bink player owns its own audio and its level relative to
    /// <c>MUS_*</c>/SFX has not been measured. Routing this through
    /// <c>Level100Audio</c>'s mix would be inventing a relationship; playing it
    /// at the level it was decoded at asserts nothing.</para>
    /// </summary>
    private void CreateVoicePlayer()
    {
        if (_clock != RetailStartupClockMode.Wall || _media.ClipAudio.Count == 0)
        {
            return;
        }

        _voice = new AudioStreamPlayer
        {
            Name = "RetailFmvVoice",
            // The frontend suspends its own _Process while the movie owns the
            // screen, but nothing pauses the tree here; Always keeps the two
            // from diverging if that ever changes.
            ProcessMode = ProcessModeEnum.Always,
            VolumeDb = 0f,
        };
        AddChild(_voice);
    }

    /// <summary>
    /// Starts the current beat's decoded track once, at the beat-local offset,
    /// and stops it the moment the beat is no longer a clip that has one.
    ///
    /// <para>It is started ONCE and then left alone. Re-seeking every frame to
    /// chase <see cref="RetailStartupFrame.BeatSeconds"/> would restart the
    /// mixer's interpolation on each engine frame; instead the video follows the
    /// wall clock and the audio follows the device clock, from a common start.
    /// Over the cutscene's 123.80 s that is a drift of whatever those two clocks
    /// differ by, and it is recorded rather than argued away. Retail's Bink
    /// player instead presents video AGAINST the audio clock — reproducing that
    /// would mean driving <c>_elapsedSeconds</c> from
    /// <c>GetPlaybackPosition()</c>, whose per-buffer granularity would judder
    /// the 25 fps frame selection. Neither behaviour is measured; the one that
    /// does not visibly damage the measured presentation is the one built.</para>
    /// </summary>
    private void UpdateVoiceTrack(RetailStartupFrame frame)
    {
        if (_voice is null || !GodotObject.IsInstanceValid(_voice))
        {
            return;
        }

        if (frame.Kind != RetailStartupFrameKind.Video ||
            frame.Cue is not { } cue ||
            !_media.ClipAudio.ContainsKey(cue))
        {
            StopVoiceTrack();
            return;
        }

        if (_voiceCue == cue)
        {
            return;
        }

        StopVoiceTrack();
        if (!TryLoadVoiceTrack(cue, out AudioStreamWav? stream))
        {
            return;
        }

        _voice.Stream = stream;

        // Seek to the beat-local offset rather than to zero. The node is added
        // mid-frame and the frame that adds it is a long one — the world has
        // just been built — so the first _Process the movie sees can already be
        // a tenth of a second in. Observed: 0.1304 s and 0.1365 s on two runs.
        // Starting the track at zero would put the voice permanently that far
        // behind the picture; the video frame index is taken from the same
        // number, so seeking is what makes them start together.
        _voice.Play((float)frame.BeatSeconds);
        PlaybackRetirement.Observe(_voice);
        _voiceCue = cue;
    }

    private bool TryLoadVoiceTrack(RetailStartupCue cue, out AudioStreamWav? stream)
    {
        stream = null;
        RetailStartupClipAudio audio = _media.ClipAudio[cue];

        byte[] wave;
        try
        {
            wave = File.ReadAllBytes(Path.Combine(_media.Root, _media.AudioRelativePath(cue)));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                InvalidOperationException or ArgumentException)
        {
            // The index verified this file's bytes at load; a read failure now
            // is a disappearing cache, not a wrong decode. The movie plays on,
            // silent, exactly as it did before the track existed.
            GD.PushWarning($"Startup media {cue} audio track unreadable: {exception.Message}");
            return false;
        }

        // The index accepted only a canonical 44-byte-header PCM WAV whose
        // declared format matches this record, so the payload is the interleaved
        // little-endian s16 frames AudioStreamWav wants, with no conversion.
        const int HeaderBytes = 44;
        if (wave.Length <= HeaderBytes)
        {
            return false;
        }

        stream = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = audio.SampleRate,
            Stereo = audio.Channels == 2,
            Data = wave[HeaderBytes..],
            LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
        };
        return true;
    }

    private void StopVoiceTrack()
    {
        _voiceCue = null;
        if (_voice is null || !GodotObject.IsInstanceValid(_voice))
        {
            return;
        }

        _voice.Stop();
        // Releases the decoded track — 21.8 MB for the Level 100 cutscene.
        _voice.Stream = null;
    }

    private static Texture2D? LoadImageTexture(string path)
    {
        var image = new Image();
        if (image.Load(path) != Error.Ok)
        {
            GD.PushWarning($"Startup splash unreadable at {path}.");
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private void Finish()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Visible = false;
        SetProcess(false);
        SetProcessInput(false);
        // Before Completed fires, not after. The handler hands the screen to
        // gameplay and starts the tutorial bed; a voice track still running
        // underneath it would be the exact defect this lane exists to avoid.
        StopVoiceTrack();
        Array.Clear(_videoBuffers);
        _splashTexture = null;
        _residentFrame = null;
        Completed?.Invoke();
    }
}
