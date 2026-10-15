// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

public sealed partial class FirstFlightHud : CanvasLayer
{
    // The released in-level HUD composes on a 640x480 stage, the same stage
    // RetailFrontendFlow already declares (RetailFrontendFlow.cs:18-19).
    //
    // Measured, not assumed. Against the retail 640x480 capture
    // local-lab/retail-reference-pristine/level-100-entry/09-level-100-entry-640x480.png:
    //   * font-13ps cell 'T' occupies 9x10 ink pixels on screen and 9x10 ink
    //     pixels in the 16px atlas cell -> glyphs are blitted 1:1, no scaling.
    //   * radar-outline's ring (texture centre 49,49) fits the frame's lower-left
    //     ring at centre (66.01, 417.25) r=46.56 (55-point circle fit, rms 0.20);
    //     the constants below place it at (66, 417) r=48.
    //   * the message panel body (objective-inner-centre rows 28..90 of 128)
    //     spans y 405.5..464.5 in the frame; the constants below predict
    //     405.25..464.3.
    // A 640x480 viewport therefore makes DesignTransform the identity, and the
    // frame is a direct test of every constant in this file.
    private const float DesignWidth = 640f;
    private const float DesignHeight = 480f;

    private const float ScannerNorthRadius = 45f;
    private const float CompassThreatRadius = 111.5f;
    private const float CompassDamageRadius = 96f;
    private const float CompassGaugeNeedleRadius = 110f;
    private const float CompassObjectiveRadius = 98f;

    // Lower-right battleline instrument. Every constant here is a decoded byte
    // from a device-level read of the safe copy, or a closed form over decoded
    // bytes; see local-lab/PORTRAIT-BATTLELINE-FIELD-2026-07-26.md and the
    // block comment on RetailHudBaseLayer.DrawBattleLine.
    //
    // CircleDarkener quad diffuse is 0x7fffffff.
    internal const float CircleDarkenerAlpha = 127f / 255f;

    // The portrait quad diffuse is 0x40ffffff and the quad is issued SIX times
    // per frame at identical position, scale, UV and z, under
    // SRCALPHA/INVSRCALPHA with ZWRITEENABLE=0 (so every one of the six passes
    // the ZFUNC=LESSEQUAL test the CircleMask depth stamp set up). Six such
    // draws leave 1 - (1 - 64/255)^6 of the page.
    internal const int PortraitDrawCount = 6;
    internal const float PortraitDrawAlpha = 64f / 255f;
    internal const float PortraitCompositeAlpha = 0.8234043f;

    // The message-noise quad diffuse alpha over 66 steady-state frames of one
    // message: 0x3c..0x4a, mean 66.2. 66/255 is used rather than the 71/255 the
    // framebuffer slope alone would imply, because 66 is the decoded byte and 71
    // is a fit; the two differ by less than the observed per-frame spread.
    internal const float MessageNoiseAlpha = 66f / 255f;

    // ...and WITHOUT one. The device log has two regimes, not one. Over the
    // twelve logged in-level frames of 2026-07-27, the message-noise quad's
    // diffuse alpha is 0x3c..0x4a whenever the six portrait draws precede it and
    // 0x6c..0x74 whenever they do not - the "promote gap" frames this client
    // already models as socket = PortraitAndNoise with no speaker. 0x70 = 112 is
    // the centre of the measured band (effective alpha 0.424-0.455). The single
    // 66/255 constant above is inside the first band and 40 % below the second,
    // so the three pinned gap frames (t011756, t019074, t031058) were drawn too
    // faint. local-lab/agent-notes-2026-07-27/inlevel-hud-coordinates.md section 6.
    internal const float MessageNoiseAlphaWithoutPortrait = 112f / 255f;
    internal const int MessageNoisePhaseCount = 16;

    private const string ReleasedAlphaTestBlendMixShaderCode = """
        shader_type canvas_item;
        render_mode blend_mix, unshaded;

        void fragment() {
            if (COLOR.a < (8.0 / 255.0)) {
                discard;
            }
        }
        """;

    private const string ReleasedAlphaTestBlendAddShaderCode = """
        shader_type canvas_item;
        render_mode blend_add, unshaded;

        void fragment() {
            if (COLOR.a < (8.0 / 255.0)) {
                discard;
            }
        }
        """;

    private static Shader? _releasedAlphaTestBlendMixShader;
    private static Shader? _releasedAlphaTestBlendAddShader;

    /// <summary>
    /// The additive diffuse both weapon-outline arc shells carry, #AE8E6E. Read
    /// off the device as the MODULATE2X quad diffuse 0xFF574737 on in-level draws
    /// 1163 (left) and 1167 (right), byte-identical in three independent
    /// captures; see <see cref="RetailHudGlowLayer"/>.DrawInstrumentOutlines.
    /// </summary>
    internal const uint RetailArcShellDiffuse = 0xffae8e6eu;

    private HudAssets _assets = null!;
    private Level100HudAssetCatalog _catalog = null!;
    // Replaced in Initialize with the authored-allegiance variant. Initialize
    // runs immediately after construction and before the first
    // ConsumeMissionEvents / UpdateFromSnapshot (FirstFlightGame.cs:643-646),
    // so no delivered state is lost by the swap.
    private Level100HudPresentationState _presentation = new();
    private int[] _level100DeliveredMessageIds = [];
    private RetailHudBaseLayer _baseLayer = null!;
    private RetailHudGlowLayer _glowLayer = null!;
    private RetailHudTextLayer _textLayer = null!;

    public bool IsReadyForSmoke =>
        IsInstanceValid(_baseLayer) &&
        IsInstanceValid(_glowLayer) &&
        IsInstanceValid(_textLayer) &&
        _baseLayer.IsReady &&
        _glowLayer.IsReady &&
        _textLayer.IsReady;

    public int Level100ObjectiveMarkerCount => _baseLayer.ObjectiveMarkerCount;
    public int Level100DeliveredMessageCount => _textLayer.DeliveredMessageCount;

    // Ordered, Core-event-derived and therefore deterministic for a given tick:
    // Level100HudPresentationState.Consume appends one entry per
    // Level100MessageRequested and never consults playback. This is the
    // evidence a caller can pin exactly; the mixer's audible message cannot be
    // (see Level100Audio.CharacterMessagePlayback).
    public IReadOnlyList<int> Level100DeliveredMessageIds =>
        _level100DeliveredMessageIds;

    public int Level100DeliveredHelpCount => _textLayer.DeliveredHelpCount;
    public int Level100Energy => _glowLayer.Energy;
    public int Level100Shield => _glowLayer.Shield;
    public int Level100Health => _glowLayer.Health;
    public bool Level100BattleLineInfluenceAvailable =>
        _baseLayer.BattleLineInfluenceAvailable;

    /// <summary>
    /// Which of the three recovered contents the lower-right socket is showing,
    /// per <see cref="Level100HudLowerRightSocketLaw"/>.
    /// </summary>
    public Level100HudLowerRightSocket Level100LowerRightSocket
    {
        get;
        private set;
    } = Level100HudLowerRightSocket.Indeterminate;
    public bool Level100MessagePlaybackAvailable { get; private set; }
    public bool Level100MessagePlaying { get; private set; }
    public double Level100MessagePlaybackPositionSeconds { get; private set; }
    public double Level100MessagePlaybackLengthSeconds { get; private set; }

    public void Initialize(Level100HudAssetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _presentation = new Level100HudPresentationState(
            Level100StaticWorldAsset.LoadAuthoredAllegiance());
        _assets = LoadAssets();
        _baseLayer = new RetailHudBaseLayer(_assets);
        AddFullScreenControl(_baseLayer);

        _glowLayer = new RetailHudGlowLayer(_assets);
        AddFullScreenControl(_glowLayer);

        _textLayer = new RetailHudTextLayer(_assets.Font13Ps, _assets.Font22, catalog);
        AddFullScreenControl(_textLayer);
    }

    public void ConsumeMissionEvents(IReadOnlyList<Level100MissionEvent> events) =>
        _presentation.Consume(events);

    public void UpdateFromSnapshot(
        WorldSnapshot snapshot,
        Level100MessagePlaybackState playback)
    {
        Level100HudSnapshot hud = _presentation.Project(snapshot, playback);
        _level100DeliveredMessageIds = hud.DeliveredMessages
            .Select(delivery => delivery.MessageId)
            .ToArray();
        Level100HudMessageDeliverySnapshot? activeDelivery =
            hud.ActiveMessage;
        Level100HudMessageDefinition? message =
            activeDelivery is null ? null : _catalog.GetRequired(activeDelivery.MessageId);
        // The reveal clock, the portrait pose and the noise phase all come from
        // Core's mission tick, NOT from playback.PositionSeconds. That property
        // is AudioStreamPlayer.GetPlaybackPosition() - the audio mixer's wall
        // clock - and under --fixed-fps it does not reproduce between two runs
        // of the same commit even though every engine frame and every Core tick
        // does. Measured cost of the old wiring: 21.28 % material cross-run on
        // the message panel and 25.15 % on portrait/compass, against 0.00 % on
        // five other regions. See Level100MessageSchedule.
        int missionTick = snapshot.Level100Mission.Tick;
        Level100MessageScheduleEntry? scheduled = Level100MessageSchedule.ActiveAt(
            hud.DeliveredMessages,
            missionTick);
        Level100MessagePlaybackSnapshot activePlayback =
            scheduled is Level100MessageScheduleEntry entry &&
            message is not null &&
            entry.Delivery.MessageId == message.MessageId
                ? new Level100MessagePlaybackSnapshot(
                    IsAvailable: true,
                    entry.Delivery.MessageId,
                    Playing: true,
                    entry.ElapsedSecondsAt(missionTick),
                    entry.DurationSeconds,
                    PortraitPoseIndex(entry, missionTick))
                : Level100MessagePlaybackSnapshot.Unavailable;
        // MessagePageIndex was removed 2026-07-26: retail does not page. The
        // message types on at 40 char/s into a three-line window that scrolls
        // up by exactly ONE line, proven from the captured frames (line 3 is
        // the bottom of one window and the top of the next, which a pager
        // would have discarded).
        if (activePlayback.PortraitPoseIndex is < 0 or > 3)
        {
            throw new InvalidDataException("Level 100 audio presentation state is out of range.");
        }
        Level100MessagePlaybackAvailable = activePlayback.IsAvailable;
        Level100MessagePlaying = activePlayback.Playing;
        Level100MessagePlaybackPositionSeconds = activePlayback.PositionSeconds;
        Level100MessagePlaybackLengthSeconds = activePlayback.LengthSeconds;

        // The lower-right socket's gate is retail's CMessageBox +0x8, which is
        // held through the six-tick promote gap that ActiveAt (the TEXT gate)
        // excludes. See Level100MessageSchedule.MessageBoxHoldsActiveMessage.
        Level100HudLowerRightSocket socket = Level100HudLowerRightSocketLaw.Select(
            Level100MessageSchedule.MessageBoxHoldsActiveMessage(
                hud.DeliveredMessages,
                missionTick),
            hud.BattleLine.InfluenceMap);
        Level100LowerRightSocket = socket;

        _baseLayer.SetState(
            snapshot,
            hud,
            socket,
            message,
            activeDelivery?.Speaker,
            activePlayback.PortraitPoseIndex,
            MessageNoisePhaseIndex(scheduled, missionTick));
        _glowLayer.SetState(snapshot, hud, socket);
        _textLayer.SetState(hud, message, activePlayback);
    }

    // A deterministic stand-in, not a measurement. Classifying retail's pose on
    // the 199 pose-discriminating Tatiana texels across the 27 hud-timeline-run1
    // frames gives aa 11 / ee 8 / mm 6 / oo 2 against these 40/12/40/8 weights -
    // oo and aa land, the ee/mm split does not (chi2 ~ 9.2 on 3 df, p ~ 0.03,
    // n = 27: suggestive, not enough to re-weight). The pose changes on 21 of 26
    // consecutive ~1 s samples, which refutes "retail holds a pose for over a
    // second" but cannot distinguish this 50 ms cadence from any other
    // sub-second one. Settling the owner's "faces move too fast" report needs a
    // retail capture at >= 10 Hz over one message; do not tune it against 27
    // samples. local-lab/PORTRAIT-COMPASS-FIT-2026-07-26.md section 6.
    private static int PortraitPoseIndex(
        Level100MessageScheduleEntry entry,
        int missionTick)
    {
        // The 20 Hz frame index WITHIN the message. Unchanged in law from the
        // version that read it off the audio stream position - the zero point
        // is the same (a message's audio starts at position 0) - but now driven
        // by the Core tick, so two runs of the same commit agree.
        int frame = Math.Max(
            0,
            (int)Math.Floor(entry.ElapsedSecondsAt(missionTick) / 0.05d));
        uint value = unchecked(
            ((uint)entry.Delivery.MessageId * 0x9E3779B9u) ^
            ((uint)frame * 0x85EBCA6Bu));
        value ^= value >> 16;
        int weighted = (int)(value % 100u);
        return weighted switch
        {
            < 8 => 0,
            < 20 => 1,
            < 60 => 2,
            _ => 3,
        };
    }

    /// <summary>
    /// The wrap phase of the message-noise pass. Retail's UV origin is
    /// (timer % 100) * k in both axes and changes every frame - 66 consecutive
    /// device-observed frames of one message carried 66 different origins, all
    /// with a sub-pixel component, which is exactly why an integer-offset
    /// correlation sweep of the retail frames had previously found the page
    /// "absent" on 21 of 26 frames. Re-running that sweep with quarter-pixel
    /// variants finds it on 16 of 22 frames at gain 0.17-0.30, which is the
    /// device-measured alpha band. The page's presence and its alpha are
    /// therefore measured; this phase index is a deterministic stand-in for a
    /// process-global timer phase that is not recoverable.
    /// </summary>
    private static int MessageNoisePhaseIndex(
        Level100MessageScheduleEntry? scheduled,
        int missionTick)
    {
        if (scheduled is not Level100MessageScheduleEntry entry)
        {
            return 0;
        }

        int frame = Math.Max(
            0,
            (int)Math.Floor(entry.ElapsedSecondsAt(missionTick) / 0.05d));
        uint value = unchecked(
            ((uint)entry.Delivery.MessageId * 0xC2B2AE35u) ^
            ((uint)frame * 0x27D4EB2Fu));
        value ^= value >> 15;
        return (int)(value % MessageNoisePhaseCount);
    }

    public void MarkInputActivity()
    {
        // The released HUD has no persistent controls legend to reveal or fade.
    }

    private static HudAssets LoadAssets()
    {
        Texture2D circleMask = LoadHudTexture("circle-mask", 128, 128);
        Texture2D messageNoise = LoadHudTexture(
            "message-noise",
            128,
            128,
            CuratedAyaTextureLoader.Compression.Dxt1);
        Texture2D[][] sourcePortraits =
        [
            LoadPortraitSet("tatiana"),
            LoadPortraitSet("technician"),
            LoadPortraitSet("kramer"),
        ];

        return new HudAssets
        {
            BarLine = LoadHudTexture(
                "bar-line",
                16,
                64,
                CuratedAyaTextureLoader.Compression.Dxt1),
            BattleLineMarker = LoadHudTexture("battleline-marker", 16, 16),
            BattleLineOutline = LoadHudTexture(
                "battleline-outline",
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt1),
            CircleDarkener = LoadHudTexture("circle-darkener", 128, 128),
            CircleMask = circleMask,
            CompassObjectiveMarker = LoadHudTexture("compass-objective-marker", 16, 16),
            CrosshairDot = LoadHudTexture("crosshair-dot", 64, 64),
            CrosshairEnemy = LoadHudTexture("crosshair-enemy", 64, 64),
            CrosshairFriend = LoadHudTexture("crosshair-friend", 64, 64),
            CrosshairOutline = LoadHudTexture("crosshair-outline", 64, 64),
            CrosshairPredictor = LoadHudTexture("crosshair-predictor", 64, 64),
            CrosshairPrimary = LoadHudTexture("crosshair-primary", 64, 64),
            CrosshairSecondary = LoadHudTexture("crosshair-secondary", 128, 128),
            DamageFlash = LoadHudTexture(
                "damage-flash",
                128,
                32,
                CuratedAyaTextureLoader.Compression.Dxt1),
            Dial = LoadHudBytes("dial.raw", 8_192),
            Font13Ps = ApplyReleasedAlphaTest(LoadHudTexture(
                "font-13ps",
                256,
                256,
                CuratedAyaTextureLoader.Compression.Rgba8)),
            Font22 = ApplyReleasedAlphaTest(LoadHudTexture(
                "font-22",
                512,
                512,
                CuratedAyaTextureLoader.Compression.Rgba8)),
            GunsDarken = LoadHudTexture("guns-darken", 128, 128),
            GunsFront = LoadHudTexture(
                "guns-front",
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt1),
            GunsOutline = LoadHudTexture(
                "guns-outline",
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt1),
            GunsSide = LoadHudTexture(
                "guns-side",
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt1),
            GunsTop = LoadHudTexture(
                "guns-top",
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt1),
            ForsetiIcon = LoadHudTexture("forseti-icon", 64, 64),
            MessageNoise = messageNoise,
            ObjectiveInnerCentre = LoadHudTexture("objective-inner-centre", 64, 128),
            ObjectiveInnerLeft = LoadHudTexture("objective-inner-left", 64, 128),
            ObjectiveInnerRight = LoadHudTexture("objective-inner-right", 64, 128),
            ObjectiveLeft = LoadHudTexture("objective-left", 128, 128),
            ObjectiveRight = LoadHudTexture("objective-right", 128, 128),
            OffscreenArrow = LoadHudTexture("offscreen-arrow", 32, 32),
            RadarOutline = LoadHudTexture(
                "radar-outline",
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt1),
            RadioNorth = LoadHudTexture("radio-north", 32, 32),
            RadioView = LoadHudTexture("radio-view", 128, 128),
            ScannerBlobs =
            [
                LoadHudTexture("scanner-blob-small", 16, 16),
                LoadHudTexture("scanner-blob-medium", 16, 16),
                LoadHudTexture("scanner-blob-large", 16, 16),
                LoadHudTexture("scanner-blob-repair-pad", 16, 16),
            ],
            ScreenMarker = LoadHudTexture(
                "screen-marker",
                64,
                64,
                CuratedAyaTextureLoader.Compression.Dxt1),
            TargetSighted = LoadHudTexture("target-sighted", 64, 64),
            ThreatFlash = LoadHudTexture(
                "threat-flash",
                32,
                32,
                CuratedAyaTextureLoader.Compression.Dxt1),
            WeaponFill = LoadHudTexture("weapon-fill", 128, 128),
            WeaponOutline = LoadHudTexture(
                "weapon-outline",
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt1),
            WeaponIcons =
            [
                LoadHudTexture("weapon-plasma-cannon", 64, 64),
                LoadHudTexture("weapon-vulcan-cannon", 64, 64),
            ],
            Portraits = sourcePortraits
                .Select(set => ApplyReleasedPortraitMask(set, circleMask))
                .ToArray(),
            MessageNoiseDiscPhases = BuildMessageNoiseDiscPhases(messageNoise, circleMask),
        };
    }

    /// <summary>
    /// The released message-noise pass is drawn with sampler ADDRESSU/ADDRESSV
    /// = WRAP and a per-frame UV origin, and is clipped to the instrument disc
    /// by the CircleMask z-stamp rather than by its own alpha (the page is
    /// DXT1 and opaque everywhere). Device-level reads of
    /// IDirect3DDevice::SetSamplerState inside 0x00487d10 CHud__RenderBattleline
    /// show sampler 0 switched from CLAMP (3) to WRAP (1) immediately before the
    /// portrait/noise draws and back afterwards
    /// (local-lab/PORTRAIT-BATTLELINE-FIELD-2026-07-26.md section 2).
    ///
    /// This client has no depth stamp, so the disc clip is baked as alpha from
    /// the same CircleMask the portrait already uses, and the wrap is baked as a
    /// fixed set of rolled phases. The alpha and the blend below are measured;
    /// the phase LATTICE is a bounded reconstruction, because retail's UV origin
    /// is (timer % 100) * k in both axes and the process-global timer phase is
    /// not recoverable.
    /// </summary>
    private static Texture2D[] BuildMessageNoiseDiscPhases(
        Texture2D messageNoise,
        Texture2D circleMask)
    {
        const int size = 128;
        Image noiseImage = messageNoise.GetImage();
        MakeCpuReadable(noiseImage);
        Image maskImage = circleMask.GetImage();
        MakeCpuReadable(maskImage);

        var phases = new Texture2D[MessageNoisePhaseCount];
        for (int phase = 0; phase < MessageNoisePhaseCount; phase++)
        {
            int shiftX = phase * (size / MessageNoisePhaseCount);
            int shiftY = ((phase * 5) % MessageNoisePhaseCount) * (size / MessageNoisePhaseCount);
            Image rolled = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color sourcePixel = noiseImage.GetPixel((x + shiftX) % size, (y + shiftY) % size);
                    sourcePixel.A = 1f - maskImage.GetPixel(x, y).A;
                    rolled.SetPixel(x, y, sourcePixel);
                }
            }
            phases[phase] = ImageTexture.CreateFromImage(rolled);
        }
        return phases;
    }

    /// <summary>
    /// Retail's <c>D3DRS_ALPHATESTENABLE = TRUE</c>,
    /// <c>ALPHAFUNC = GREATEREQUAL</c>, <c>ALPHAREF = 0x8</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured 2026-07-27: <b>every</b> one of the 74 in-level HUD draws runs
    /// that alpha test. It is not bookkeeping -
    /// <c>font-13ps</c> alone carries 603 texels with alpha 1..7, the glyph
    /// anti-aliasing fringe, which retail discards and this client composited.
    /// </para>
    /// <para>
    /// The test is on the value AFTER the alpha stage, i.e.
    /// <c>texelAlpha * diffuseAlpha</c>, so in general it cannot be baked into a
    /// texture: a page drawn at diffuse alpha 0.3412 has an effective texel
    /// cutoff of 8/(255*0.3412) = 92, not 8. It CAN be baked here, exactly,
    /// because both text draws carry a diffuse alpha of 0xFF (shadow
    /// 0xFF000000, glyph 0xFFFFFFFF) and the glyph blit is 1:1 under
    /// TextureFilterEnum.Nearest, so no interpolated alpha is ever produced.
    /// <see cref="CreateReleasedAlphaTestMaterial(bool)"/> now performs the
    /// general test on post-modulate alpha for all three HUD layers. This exact
    /// font pre-cut remains because glyph widths are also measured on the CPU
    /// atlas and the released full-alpha text draws make the bake equivalent;
    /// the layer shader is still the final draw-time gate.
    /// </para>
    /// </remarks>
    private static Texture2D ApplyReleasedAlphaTest(Texture2D atlas)
    {
        const float alphaRef = 8f / 255f;
        Image image = atlas.GetImage();
        MakeCpuReadable(image);
        int width = image.GetWidth();
        int height = image.GetHeight();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color texel = image.GetPixel(x, y);
                if (texel.A > 0f && texel.A < alphaRef)
                {
                    image.SetPixel(x, y, new Color(texel.R, texel.G, texel.B, 0f));
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static ShaderMaterial CreateReleasedAlphaTestMaterial(bool additive)
    {
        Shader shader = additive
            ? _releasedAlphaTestBlendAddShader ??= new Shader
            {
                Code = ReleasedAlphaTestBlendAddShaderCode,
            }
            : _releasedAlphaTestBlendMixShader ??= new Shader
            {
                Code = ReleasedAlphaTestBlendMixShaderCode,
            };
        return new ShaderMaterial { Shader = shader };
    }

    private static Texture2D[] LoadPortraitSet(string speaker) =>
    [
        LoadHudTexture($"{speaker}-portrait-oo", 128, 128),
        LoadHudTexture($"{speaker}-portrait-ee", 128, 128),
        LoadHudTexture($"{speaker}-portrait-mm", 128, 128),
        LoadHudTexture($"{speaker}-portrait", 128, 128),
    ];

    private static Texture2D[] ApplyReleasedPortraitMask(Texture2D[] source, Texture2D circleMask)
    {
        Image maskImage = circleMask.GetImage();
        MakeCpuReadable(maskImage);
        return source.Select(portrait => ApplyReleasedPortraitMask(portrait, maskImage)).ToArray();
    }

    private static Texture2D ApplyReleasedPortraitMask(Texture2D source, Image maskImage)
    {
        const int sourceSize = 128;
        const int portraitSize = 96;
        const int portraitInset = (sourceSize - portraitSize) / 2;
        Image sourceImage = source.GetImage();
        MakeCpuReadable(sourceImage);
        sourceImage.Resize(portraitSize, portraitSize, Image.Interpolation.Bilinear);
        Image masked = Image.CreateEmpty(sourceSize, sourceSize, false, Image.Format.Rgba8);
        for (int y = 0; y < sourceSize; y++)
        {
            for (int x = 0; x < sourceSize; x++)
            {
                Color sourcePixel = x is >= portraitInset and < portraitInset + portraitSize &&
                    y is >= portraitInset and < portraitInset + portraitSize
                        ? sourceImage.GetPixel(x - portraitInset, y - portraitInset)
                        : Colors.Transparent;
                Color maskPixel = maskImage.GetPixel(x, y);
                sourcePixel.A *= 1f - maskPixel.A;
                masked.SetPixel(x, y, sourcePixel);
            }
        }
        return ImageTexture.CreateFromImage(masked);
    }

    private static void MakeCpuReadable(Image image)
    {
        if (image.IsCompressed() && image.Decompress() != Error.Ok)
        {
            throw new InvalidDataException("Released HUD texture could not be decompressed for portrait masking.");
        }

        if (image.GetFormat() != Image.Format.Rgba8)
        {
            image.Convert(Image.Format.Rgba8);
        }
    }

    private static Texture2D LoadHudTexture(
        string name,
        int width,
        int height,
        CuratedAyaTextureLoader.Compression compression = CuratedAyaTextureLoader.Compression.Dxt2) =>
        CuratedAyaTextureLoader.Load(
            $"res://Assets/Hud/{name}.texture.aya",
            width,
            height,
            compression);

    private static byte[] LoadHudBytes(string name, int expectedLength)
    {
        string path = $"res://Assets/Hud/{name}";
        byte[] source = Godot.FileAccess.GetFileAsBytes(path);
        if (source.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"Released HUD asset has the wrong length: {path} ({source.Length}, expected {expectedLength})");
        }
        return source;
    }

    // THE CROSSHAIR PAGES ARE PURE WHITE. Read off the device 2026-07-27
    // (in-level draws 1156-1158, identical in three independent captures):
    // retail issues exactly THREE crosshair quads, all SRCALPHA/INVSRCALPHA,
    // all with an effective RGB of 255/255/255 and only the alpha differing:
    //
    //   1156  64x64  at (288.0098, 208.0001)  MODULATE   0xAFFFFFFF -> a 0.6863
    //   1157  128x128 at (256.0098, 176.0001) MODULATE2X 0x577F7F7F -> a 0.3412
    //   1158  64x64  at (288.0098, 208.0001)  MODULATE2X 0x577F7F7F -> a 0.3412
    //
    // The two colours this file used to carry - (0.818, 0.820, 0.906) at alpha 1
    // and (0.883, 0.891, 0.889) at alpha 0.53 - were regressed from the retail
    // framebuffer, and the regression could not separate "a white page at alpha
    // a" from "a slightly coloured page at alpha b". It picked the wrong one:
    // the inner ring is not blue-biased and the outer ring is not at 0.53.
    // Photometric cross-check on retail t029072 (background taken from the
    // adjacent radii): the r<=2 centre reads 156 where white at 0.3412 over a
    // 104 background predicts 153; the r 26..28 ring reads 206-215 where white
    // at 0.6863 over a ~150 background predicts 222 at full coverage.
    private static Color RetailCrosshairBright => new(1f, 1f, 1f, 0.6863f);

    private static Color RetailCrosshairFaint => new(1f, 1f, 1f, 0.3412f);

    // COMPASS GAUGE ARCS. The reconstruction drew these ADDITIVELY; that is
    // refuted by measurement, and retail's actual blend is now byte-backed.
    //
    // MECHANISM, from the shipped image. CDXCompass__RenderWorldSpaceOverlay
    // (0x0053cd30) binds a runtime-built overlay texture at this+0x3f04 and the
    // ring vertex buffer at this+0x3f10, then:
    //
    //     RenderState_Set(0x13, 2);   // SRCBLEND  = D3DBLEND_ONE
    //     RenderState_Set(0x14, 6);   // DESTBLEND = D3DBLEND_INVSRCALPHA
    //     CDXEngine__ApplyPendingRenderState(...);
    //     (**(code **)(*0x00888a50 + 0x144))();   // the draw
    //
    // That is a PREMULTIPLIED-ALPHA blend and a SINGLE draw:
    //
    //     out = P + (1 - a) * bg
    //
    // where P is the texture's premultiplied RGB and a its alpha. The texture is
    // built per frame by CDXCompass__UpdateDynamicOverlayTexture (0x0053c510) as
    // ARGB4444 - the alpha nibble is written as value * 0x1000 and the colour as
    // ((R * 0x10 + G) * 0x10 + B) - with a per-row alpha gradient, so `a` is not
    // constant across the 12 px band and the fitted value below is its mean.
    //
    // MAGNITUDE, from a perturbation capture - the instrument that needs no model
    // of what is drawn where. One capture with the base ring, gauges, dial and
    // needles switched off supplies a per-pixel background; only the 14,950
    // px/frame that RESPONDED to that perturbation are admitted. Over the 27
    // paired hud-timeline-run1 frames, regressing retail's pixel on that
    // background inside r 80..92:
    //
    //   green arc, bearings 65-145, n=38,024
    //     R slope 0.819 intercept  9.3      B slope 0.825 intercept  8.9
    //     G is CLIPPED in retail (mean 250.7) and its slope is not usable.
    //   violet arc, bearings 250-345, n=45,936
    //     R slope 0.750 intercept 15.7      G slope 0.801 intercept  7.9
    //     B is the strong channel and is not used for alpha.
    //
    // slope = 1 - a and intercept = P, giving a = 0.178 / P = (9.3, 72.6, 9.0)
    // for health and a = 0.225 / P = (15.7, 7.8, 79.6) for energy.
    //
    // ADDITIVE IS REFUTED, not merely beaten: retail's green arc LOWERS the
    // background by 22.1 (R) and 24.1 (B), and its violet arc by 26.3 (R) and
    // 21.3 (G). A ONE/ONE pass cannot lower any channel. The scene controls rule
    // out a background difference: at the SAME bearings, immediately inside the
    // arc (r 70..79) and outside the ring (r 102..112), retail and our
    // gauges-off capture agree to [2.3, 1.3, 1.6] and [-1.3, -1.6, -1.3] on the
    // green bearings and to [-5.7, -3.9, -2.3] on the violet ones.
    //
    // WITHDRAWN, and recorded because it was wrong for a day: an earlier draft of
    // this block claimed the arcs are issued TWICE, once under each of the two
    // blend states CDXCompass__Render (0x00427210) installs at its head
    // (0x00482090 sets SRCALPHA/INVSRCALPHA, 0x004821b0 then sets ONE/ONE).
    // There is NO DRAW between those two calls - the second simply overwrites the
    // first - and the sprites that follow them are BarLine and the threat/damage
    // flashes, not the gauge ring. Two independent adversarial passes caught this
    // separately, and the decompile confirms it. `out = (1-a)bg + K(1+a)` and
    // `out = (1-a)bg + P` are the same equation; a fit cannot choose a pass count.
    //
    // Godot exposes blend mode per CanvasItem, not per draw, so the single
    // premultiplied blend is REALISED here as two draws: the alpha-blended base
    // layer contributes (1-a)*bg + a*K and the additive glow layer adds K, with
    // K = P / (1 + a). That is an implementation identity, not a claim about
    // retail's draw count; CanvasItemMaterial.BlendModeEnum.PremultAlpha on a
    // third layer would express it directly and is the cleaner future form.
    //
    // The health arc's colour is measured only at full health - every retail
    // frame in the reference set is at full health - so the existing damage hue
    // ramp is retained and only its MAGNITUDE is replaced by the measurement.
    internal const float GaugeHealthBlendAlpha = 0.178f;
    internal const float GaugeEnergyBlendAlpha = 0.225f;
    private static Color GaugeHealthPaint(float health) => new(
        ((1f - health) * 61.6f / 255f) + (7.9f / 255f),
        health * 61.6f / 255f,
        7.6f / 255f);
    private static Color GaugeEnergyPaint => new(12.8f / 255f, 6.4f / 255f, 65.0f / 255f);

    private static Color RetailColor(uint argb) => new(
        ((argb >> 16) & 0xff) / 255f,
        ((argb >> 8) & 0xff) / 255f,
        (argb & 0xff) / 255f,
        ((argb >> 24) & 0xff) / 255f);

    /// <summary>
    /// The released scanner tint. These are the decoded packed constants from
    /// the three draw loops of <c>CHud__RenderTacticalRadarContacts</c>
    /// (0x00484c50) - 0x5050AF friendly, 0xAF0808 enemy, 0x606060 otherwise -
    /// with the projection's fade alpha in the top byte.
    ///
    /// <para>The previous hand-picked colours and the yellow objective override
    /// were both wrong: retail's objective bucket (the <c>unit+0x1f4</c> set)
    /// selects from the SAME three allegiance tints, and retail's friendly blob
    /// pixels on <c>hud-timeline-run1/level100-t025065ms.png</c> are literally
    /// (80, 80, 174) = 0x5050AE, not (64, 122, 255).</para>
    /// </summary>
    private static Color ContactColor(Level100HudContactSnapshot contact, int alpha) =>
        RetailColor(
            ((uint)Math.Clamp(alpha, 0, 255) << 24) |
            (uint)Level100ScannerProjection.TintRgb(contact.Allegiance));

    private static float RelativeYaw(WorldSnapshot snapshot, SimVector2 position)
    {
        float dx = position.X - snapshot.PlayerPosition.X;
        float dz = position.Z - snapshot.PlayerPosition.Z;
        float desiredYaw = Mathf.Atan2(-dx, dz);
        return NormalizeAngle((snapshot.FacingYawMicroRad / 1_000_000f) - desiredYaw);
    }

    /// <summary>
    /// Design-space x of an objective's on-screen reticle, or null when the
    /// objective is outside the released horizontal limit and the off-screen
    /// arrow is drawn instead.
    /// </summary>
    private static float? WorldMarkerScreenX(WorldSnapshot snapshot, SimVector2 position)
    {
        const float horizontalLimit = 1.05f;
        float relativeYaw = RelativeYaw(snapshot, position);
        return Math.Abs(relativeYaw) <= horizontalLimit
            ? (DesignWidth * 0.5f) + ((relativeYaw / horizontalLimit) * (DesignWidth * 0.42f))
            : null;
    }

    private static SimVector2 HorizontalPosition(SimVector3 position) =>
        new(position.X, position.Z);

    private static float NormalizeAngle(float angle)
    {
        while (angle > Mathf.Pi)
        {
            angle -= Mathf.Tau;
        }
        while (angle <= -Mathf.Pi)
        {
            angle += Mathf.Tau;
        }
        return angle;
    }

    private void AddFullScreenControl(Control control)
    {
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        // The 640x480 retail frame shows single-texel glyph stems and hard
        // instrument edges (font-13ps 'h' renders a one-pixel-wide stem), i.e.
        // the released HUD blitted texels 1:1 with no interpolation. At the
        // 640x480 design resolution DesignTransform is the identity and Nearest
        // reproduces that blit exactly. Above 640x480 retail has no measured
        // behaviour to match; Nearest is chosen because it keeps the hard edges
        // the frame demonstrates rather than softening every element.
        control.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        AddChild(control);
    }

    /// <summary>
    /// The shared 640x480 released stage. Every layer draws in design pixels and
    /// lets this map them onto the window, so a constant measured off the retail
    /// 640x480 capture stays at the same relative position and scale at any
    /// window size. This mirrors RetailFrontendFlow.DesignTransform.
    /// </summary>
    private abstract partial class RetailHudLayer : Control
    {
        protected static Vector2 DesignCenter => new(DesignWidth * 0.5f, DesignHeight * 0.5f);

        protected (float Scale, Vector2 Offset) DesignTransform()
        {
            float scale = Mathf.Min(Size.X / DesignWidth, Size.Y / DesignHeight);
            return (
                scale,
                new Vector2(
                    (Size.X - (DesignWidth * scale)) * 0.5f,
                    (Size.Y - (DesignHeight * scale)) * 0.5f));
        }

        protected void BeginDesignSpace()
        {
            (float scale, Vector2 offset) = DesignTransform();
            DrawSetTransform(offset, 0f, new Vector2(scale, scale));
        }

        protected void EndDesignSpace() => DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        /// <summary>
        /// DrawSetTransform replaces rather than nests, so a rotated blit has to
        /// re-compose the letterbox around its design-space pivot and then
        /// restore design space. Same shape as RetailFrontendFlow's helper.
        /// </summary>
        protected void DrawCenteredRotated(
            Texture2D texture,
            Vector2 center,
            Vector2 size,
            float rotation,
            Color modulate)
        {
            (float scale, Vector2 offset) = DesignTransform();
            DrawSetTransform(
                offset + (center * scale),
                rotation,
                new Vector2(scale, scale));
            DrawTextureRect(texture, new Rect2(-size * 0.5f, size), false, modulate);
            DrawSetTransform(offset, 0f, new Vector2(scale, scale));
        }

        /// <summary>
        /// ONE continuous polyline, not N independent antialiased quads.
        ///
        /// Retail's compass rings are each a SINGLE D3D triangle strip, read
        /// from the pristine specimen
        /// local-lab/safe-copy-bea-pristine/BEA.exe.original.backup, sha256
        /// 74154BFA...E7750:
        ///
        ///   CDXCompass__Init 0x0053be40 creates ring 1's vertex buffer at
        ///   0x0053c099 with push 0x102 / push 0x14 / push 0x66 - FVF
        ///   D3DFVF_XYZ|D3DFVF_TEX1, stride 20, 102 vertices - and ring 2's at
        ///   0x0053c133 with push 0x52, 82 vertices. 102 = 2*(50+1) and
        ///   82 = 2*(40+1): segmentCount pairs plus one closing pair.
        ///   CDXCompass__BuildRingGeometry 0x0053c1d0 writes those pairs and
        ///   closes the strip by copying vertex pair 0, setting u = 1.0 at
        ///   0x0053c2c9.
        ///   CDXCompass__RenderWorldSpaceOverlay 0x0053cd30 then issues exactly
        ///   two DrawPrimitive calls through device vtable byte 0x144:
        ///   0x0053cf95 push 0x50 / push 0 / push 5 and 0x0053d10a
        ///   push 0x64 / push 0 / push 5 - D3DPT_TRIANGLESTRIP, StartVertex 0,
        ///   PrimitiveCount 80 then 100.
        ///
        /// A strip has no interior boundary for a rasteriser to feather, so
        /// retail's ring carries no per-segment seam. The previous loop issued
        /// segmentCount independent DrawLine(..., antialiased: true) calls,
        /// which put an antialiasing falloff on BOTH sides of all 50 butt
        /// joints - task #106's second defect. DrawPolyline emits one primitive
        /// with joined segments, so only the closure seam remains, which is the
        /// one seam retail's duplicated vertex pair also has.
        ///
        /// The segment counts themselves are unchanged and are not a guess:
        /// CDXCompass__Init passes push 0x32 (50) at 0x0053c0e7 and push 0x28
        /// (40) at 0x0053c17e, and the device draw-call log independently
        /// recorded 100/102 and 80/82 TRISTRIP.
        /// </summary>
        protected void DrawSegmentedRing(
            Vector2 center,
            float radius,
            int segmentCount,
            float width,
            float startTurn,
            float turnLength,
            Color color)
        {
            int segmentStart = Math.Clamp((int)Math.Floor(startTurn * segmentCount), 0, segmentCount);
            int segmentEnd = Math.Clamp(
                (int)Math.Ceiling((startTurn + turnLength) * segmentCount),
                segmentStart,
                segmentCount);
            if (segmentEnd <= segmentStart)
            {
                return;
            }

            var points = new Vector2[segmentEnd - segmentStart + 1];
            for (int index = 0; index < points.Length; index++)
            {
                float turn = ((segmentStart + index) / (float)segmentCount) * Mathf.Tau;
                points[index] =
                    center + (new Vector2(Mathf.Sin(turn), -Mathf.Cos(turn)) * radius);
            }
            DrawPolyline(points, color, width, true);
        }

        /// <summary>
        /// One half of the compass gauge arcs. Retail issues them as a SINGLE
        /// premultiplied-alpha draw, SRCBLEND=ONE DESTBLEND=INVSRCALPHA, in
        /// CDXCompass__RenderWorldSpaceOverlay (0x0053cd30): out = P + (1-a)*bg.
        /// Godot's blend mode is per CanvasItem, so that one blend is realised as
        /// two draws - the alpha-blended base layer contributes (1-a)*bg + a*K
        /// and the additive glow layer adds K, with K = P/(1+a). See the block
        /// comment on GaugeHealthPaint for the byte evidence and the fit.
        ///
        /// The highlight pulse is applied to the additive half only: raising the
        /// alpha of the attenuating half would DARKEN an emphasised gauge, which
        /// is the opposite of what HighlightHudPart means.
        /// </summary>
        protected void DrawCompassGaugeArcs(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud,
            bool alphaBlendedHalf)
        {
            float health = Math.Clamp(
                snapshot.Hull / (float)SimulationConstants.MaximumHull, 0f, 1f);
            float energy = Math.Clamp(
                snapshot.Energy / (float)SimulationConstants.MaximumEnergy, 0f, 1f);
            const float radius =
                (CompassGaugeInnerRadius + CompassGaugeOuterRadius) * 0.5f;
            const float width = CompassGaugeOuterRadius - CompassGaugeInnerRadius;

            Color healthPaint = GaugeHealthPaint(health);
            Color energyPaint = GaugeEnergyPaint;
            if (alphaBlendedHalf)
            {
                healthPaint.A = GaugeHealthBlendAlpha;
                energyPaint.A = GaugeEnergyBlendAlpha;
            }
            else
            {
                healthPaint.A = 1f + HighlightAlpha(snapshot, hud, Level100HudPart.Health);
                energyPaint.A = 1f + HighlightAlpha(snapshot, hud, Level100HudPart.Energy);
            }

            DrawSegmentedRing(
                DesignCenter,
                radius,
                50,
                width,
                (150f - (health * 90f)) / 360f,
                health * (90f / 360f),
                healthPaint);
            DrawSegmentedRing(
                DesignCenter,
                radius,
                40,
                width,
                225f / 360f,
                energy * (135f / 360f),
                energyPaint);
        }

        protected static float HighlightAlpha(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud,
            Level100HudPart part)
        {
            if (!hud.EmphasizedParts.Contains(part))
            {
                return 0f;
            }
            return 0.22f + (0.18f * (Mathf.Sin(snapshot.Tick * 0.45f) + 1f));
        }
    }

    // MEASURED, not assumed. The centre compass's full-circle base ring is the
    // one compass element retail demonstrably alpha-blends.
    //
    // Method (hudA_compass): retail's own r 93-94 and r 102-104 are feature-free
    // (established when the ring radii were measured), so at a fixed bearing they
    // give a background reference 4 px from the ring. Regressing the ring's
    // frame-to-frame delta on the reference's delta over the 27
    // hud-timeline-run1 frames gives the background-retention factor s directly,
    // with no background model and no paint colour in the fit:
    //
    //   base ring   r 96-100, gauge-free bearings   n=10,607   s=0.733
    //   CONTROL     empty annulus r 70-72           n=30,665   s=1.035
    //   CONTROL     empty annulus r 106-108         n=14,305   s=0.996
    //
    // The two controls pin the method's error at about +-0.035 around the
    // additive answer s=1. The ring's 0.733 is eight times that away, i.e. retail
    // retains only 73% of the background under the ring: alpha 0.267.
    private const float CompassBaseRingInnerRadius = 95f;
    private const float CompassGaugeInnerRadius = 80f;
    private const float CompassGaugeOuterRadius = 92f;
    private const float CompassBaseRingOuterRadius = 101f;

    // COLOUR: task #106's first defect, and it is no longer a fit. The ring's
    // texel was READ OUT OF THE RUNNING GAME on 2026-07-28 and recovered on
    // 2026-07-29; see local-lab/HUD-LANE-RECOVERED-2026-07-29.md sections 1-2.
    //
    // Retail's base ring is compass ring 1: 50 segments, radius percent 31,
    // textured by CDXCompass__BuildByteSpriteOverlayTexture (0x0053c2e0) into a
    // 512x32 A4R4G4B4 surface. All 32 rows of that surface were dumped whole at
    // two positions of the Level 100 TTD trace, 16,384 texels each, read at the
    // UnlockRect instruction through the pointer LockRect handed the program:
    //
    //   <maintainer evidence>\bea-ttd\play-level100\q-texels\cdb.log
    //   <maintainer evidence>\bea-ttd\queries\34-ring-texels-fullrow.txt
    //
    // The ENTIRE ink is one value, 0x2444, on five rows, identical at !tt 45 and
    // !tt 90. The pristine specimen agrees independently: the whole ring-1
    // palette is written as four immediates at 0x0053c312..0x0053c327 -
    // 0x0000, 0x2222, 0x2444, 0x2666 - and every one of them has R == G == B.
    // (Specimen local-lab/safe-copy-bea-pristine/BEA.exe.original.backup,
    // sha256 74154BFAE14DDC8ECB87A0766F5BC381C7B7F1AB334ED7A753040EDA1E1E7750.)
    //
    //   0x2444 -> A = 2/15 = 0.133333   R = G = B = 4/15 = 0.266667 = 68/255
    //
    // RETAIL'S BASE RING IS ACHROMATIC. It cannot be blue: the vertex buffers
    // carry FVF 0x102 (XYZ|TEX1) with no D3DFVF_DIFFUSE, so D3D9 supplies opaque
    // white; and at the ring-1 DrawPrimitive (0x0053d10a), walked back with g-,
    // the stage is COLOROP=MODULATE, COLORARG1=D3DTA_TEXTURE,
    // COLORARG2=D3DTA_DIFFUSE - not TFACTOR. Blend at that draw, measured:
    // ALPHABLENDENABLE=1, SRCBLEND=ONE, DESTBLEND=INVSRCALPHA. So
    //
    //   out = texel.rgb + (1 - texel.a) * bg  =  4/15 + (13/15) * bg
    //
    // with no fitting and no free parameter. Godot's blend mode is per
    // CanvasItem, so as with the gauge arcs the one premultiplied draw is
    // realised as an alpha-blended half plus an additive half, and the paint is
    // K = P / (1 + a) = (4/15) / (17/15) = 4/17. That identity is the same one
    // the GaugeHealthPaint block derives and uses.
    //
    // This moves toward the independent pixel fit above as well as onto the
    // bytes. The old constant emitted intercept (134, 185, 287) against a
    // frame-fitted 93.1 and a byte-measured 68, at retention 0.75; the measured
    // one emits exactly 68 at retention 0.867 against that fitted 0.733.
    //
    // NOT changed, deliberately: the 95/101 radii above. The measured ink rows
    // (8..12) and the static memset's predicted texture rows (14..18) disagree
    // by six rows. The band WIDTH agrees - five of ring 1's 32 rows is 6.09 px
    // at the projection-derived 320 px/unit, against the frame-measured 6 px -
    // but the offset does not, so no radius here is derived from a texel row.
    // Section 1.3 of the note carries the open question and the cheapest probe.
    //
    // The emphasis pulse keeps its existing magnitudes and is now applied to all
    // three channels, so an emphasised ring stays achromatic.
    internal const float CompassBaseRingTexelAlpha = 2f / 15f;
    internal const float CompassBaseRingTexelPremultipliedRgb = 4f / 15f;
    internal const float CompassBaseRingPaint =
        CompassBaseRingTexelPremultipliedRgb / (1f + CompassBaseRingTexelAlpha);

    private static Color CompassBaseColor(
        WorldSnapshot snapshot,
        Level100HudSnapshot hud,
        float compassHighlight) =>
        new(
            CompassBaseRingPaint + compassHighlight,
            CompassBaseRingPaint + compassHighlight,
            CompassBaseRingPaint + compassHighlight,
            CompassBaseRingTexelAlpha + (compassHighlight * 0.4f));

    private sealed class HudAssets
    {
        public required Texture2D BarLine { get; init; }
        public required Texture2D BattleLineMarker { get; init; }
        public required Texture2D BattleLineOutline { get; init; }
        public required Texture2D CircleDarkener { get; init; }
        public required Texture2D CircleMask { get; init; }
        public required Texture2D CompassObjectiveMarker { get; init; }
        public required Texture2D CrosshairDot { get; init; }
        public required Texture2D CrosshairEnemy { get; init; }
        public required Texture2D CrosshairFriend { get; init; }
        public required Texture2D CrosshairOutline { get; init; }
        public required Texture2D CrosshairPredictor { get; init; }
        public required Texture2D CrosshairPrimary { get; init; }
        public required Texture2D CrosshairSecondary { get; init; }
        public required Texture2D DamageFlash { get; init; }
        public required byte[] Dial { get; init; }
        public required Texture2D Font13Ps { get; init; }
        public required Texture2D Font22 { get; init; }
        public required Texture2D GunsDarken { get; init; }
        public required Texture2D GunsFront { get; init; }
        public required Texture2D GunsOutline { get; init; }
        public required Texture2D GunsSide { get; init; }
        public required Texture2D GunsTop { get; init; }
        public required Texture2D MessageNoise { get; init; }
        public required Texture2D ObjectiveInnerCentre { get; init; }
        public required Texture2D ObjectiveInnerLeft { get; init; }
        public required Texture2D ObjectiveInnerRight { get; init; }
        /// <summary>
        /// <c>hud\ForsetiIcon.tga</c>, the sprite <c>CHud__LoadTextures</c>
        /// (<c>0x00481650</c>) puts in <c>[hud+0x1d4]</c> from the string at
        /// <c>0x0062ceb0</c>. 64x64 DXT2.
        /// </summary>
        public required Texture2D ForsetiIcon { get; init; }
        public required Texture2D ObjectiveLeft { get; init; }
        public required Texture2D ObjectiveRight { get; init; }
        public required Texture2D OffscreenArrow { get; init; }
        public required Texture2D RadarOutline { get; init; }
        public required Texture2D RadioNorth { get; init; }
        public required Texture2D RadioView { get; init; }
        public required Texture2D[] ScannerBlobs { get; init; }
        public required Texture2D ScreenMarker { get; init; }
        public required Texture2D TargetSighted { get; init; }
        public required Texture2D ThreatFlash { get; init; }
        public required Texture2D WeaponFill { get; init; }
        public required Texture2D[] WeaponIcons { get; init; }
        public required Texture2D WeaponOutline { get; init; }
        public required Texture2D[][] Portraits { get; init; }

        /// <summary>
        /// message-noise, rolled to <see cref="MessageNoisePhaseCount"/> wrap
        /// phases and clipped to the instrument disc by CircleMask alpha.
        /// </summary>
        public required Texture2D[] MessageNoiseDiscPhases { get; init; }
    }

    private sealed partial class RetailHudBaseLayer(HudAssets assets) : RetailHudLayer
    {
        private WorldSnapshot? _snapshot;
        private Level100HudSnapshot? _hud;
        private Level100HudMessageDefinition? _message;
        private Level100HudSpeaker? _speaker;
        private int? _portraitPoseIndex;
        private int _messageNoisePhase;
        private Level100HudLowerRightSocket _socket =
            Level100HudLowerRightSocket.Indeterminate;

        public bool IsReady =>
            assets.CircleDarkener.GetSize() == new Vector2I(128, 128) &&
            assets.CircleMask.GetSize() == new Vector2I(128, 128) &&
            assets.RadioView.GetSize() == new Vector2I(128, 128) &&
            assets.WeaponFill.GetSize() == new Vector2I(128, 128) &&
            assets.RadioNorth.GetSize() == new Vector2I(32, 32) &&
            assets.CompassObjectiveMarker.GetSize() == new Vector2I(16, 16) &&
            assets.ScannerBlobs.Length == 4 &&
            assets.ScannerBlobs.All(texture => texture.GetSize() == new Vector2I(16, 16)) &&
            assets.Portraits.Length == 3 &&
            assets.Portraits.All(PortraitSetIsReady);

        public int ObjectiveMarkerCount => _hud?.Objectives.Count ?? 0;

        public bool BattleLineInfluenceAvailable =>
            _hud?.BattleLine is Level100HudBattleLineSnapshot battleLine &&
            battleLine.HasInfluenceValues &&
            battleLine.InfluencePermille.Count == Level100HudInfluenceMap.Nodes.Count;

        public override void _Ready()
        {
            Material = CreateReleasedAlphaTestMaterial(additive: false);
        }

        public void SetState(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud,
            Level100HudLowerRightSocket socket,
            Level100HudMessageDefinition? message,
            Level100HudSpeaker? speaker,
            int? portraitPoseIndex,
            int messageNoisePhase)
        {
            _snapshot = snapshot;
            _hud = hud;
            _socket = socket;
            _message = message;
            _speaker = speaker;
            _portraitPoseIndex = portraitPoseIndex;
            _messageNoisePhase = messageNoisePhase;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_snapshot is not WorldSnapshot snapshot ||
                _hud is not Level100HudSnapshot hud)
            {
                return;
            }

            BeginDesignSpace();
            DrawCompassBaseRing(snapshot, hud);
            DrawLowerLeftInstrument(snapshot, hud);
            DrawLowerRightArcShellFill(snapshot, hud);
            DrawWeaponSelection(hud);
            DrawBattleLine();
            if (MessageBoxIsDeployed(snapshot))
            {
                DrawMessageFrame();
            }
            DrawWorldMarkers(snapshot, hud);
            DrawCrosshair(snapshot, hud);
            EndDesignSpace();
        }

        /// <summary>
        /// The compass base ring and its north dial, on the alpha-blended layer
        /// because retail alpha-blends them: see CompassBaseColor for the s=0.733
        /// measurement and its two s=1.0 controls. They are drawn here, before
        /// the additive layer, because retail's compass order is alpha-blended
        /// body, then ONE/ONE sprites, then the alpha-blended marker pass
        /// (CDXCompass__Render at 0x00427210 calls ApplyOverlaySpriteState,
        /// then the ONE/ONE state, then the SRCALPHA/INVSRCALPHA state).
        /// </summary>
        private void DrawCompassBaseRing(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            float compassHighlight = HighlightAlpha(snapshot, hud, Level100HudPart.Compass);
            Color baseColor = CompassBaseColor(snapshot, hud, compassHighlight);
            const float radius =
                (CompassBaseRingInnerRadius + CompassBaseRingOuterRadius) * 0.5f;
            const float width =
                CompassBaseRingOuterRadius - CompassBaseRingInnerRadius;
            DrawSegmentedRing(DesignCenter, radius, 50, width, 0f, 1f, baseColor);
            DrawCompassGaugeArcs(snapshot, hud, alphaBlendedHalf: true);
            DrawCompassObjectiveMarkers(snapshot, hud);
        }

        /// <summary>
        /// compass-objective-marker was the ONE page on the additive layer that
        /// is not DXT1: it is DXT2 and carries a real alpha channel - 222 of its
        /// 256 texels are alpha 0, across 13 distinct alpha levels - so it has
        /// the coverage an alpha-blended draw needs, and drawing it additively
        /// threw that coverage away and added its black field's RGB instead.
        ///
        /// Retail agrees by name: CDXCompass__Render (0x00427210) runs
        /// HudRenderState__ApplyOverlaySpriteState, then the ONE/ONE state for
        /// the compass body sprites, then restores SRCALPHA/INVSRCALPHA for its
        /// final marker pass.
        ///
        /// Retail's marker pass is last, after the additive body; this draws it
        /// with the rest of the alpha-blended compass instead, so an additive
        /// threat or damage sprite that happens to overlap a marker will land on
        /// top of it rather than under it. The blend is the measured part; the
        /// intra-compass ordering is not, and no retail frame in
        /// hud-timeline-run1 has an overlap that would show the difference.
        /// </summary>
        private void DrawCompassObjectiveMarkers(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            foreach (Level100HudObjectiveSnapshot objective in hud.Objectives)
            {
                float relativeYaw = RelativeYaw(
                    snapshot,
                    HorizontalPosition(objective.PositionMillimeters));
                Vector2 position = DesignCenter + new Vector2(
                    Mathf.Sin(relativeYaw) * CompassObjectiveRadius,
                    -Mathf.Cos(relativeYaw) * CompassObjectiveRadius);
                DrawTextureRect(
                    assets.CompassObjectiveMarker,
                    new Rect2(position - new Vector2(8f, 8f), new Vector2(16f, 16f)),
                    false,
                    new Color(1f, 0.91f, 0.08f, 1f));
            }
        }

        private void DrawLowerLeftInstrument(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            Rect2 radarRect = new(17f, DesignHeight - 112f, 128f, 128f);
            Rect2 weaponRect = new(9f, DesignHeight - 141f, 128f, 128f);
            DrawTextureRect(assets.RadioView, radarRect, false, RetailColor(0x6fffffff));
            DrawTextureRect(assets.WeaponFill, weaponRect, false, RetailColor(0x3f000000));

            DrawWeaponResource(snapshot, hud, weaponRect);
            DrawWeaponIcon(hud.Weapon, weaponRect);

            // Recovered scanner centre: 0x005dbb70 (69) - 1 in x, and
            // (480 - 44 [0x005dbe74]) - 20 [0x005d857c] + 1 in y. Retail's
            // sprite helper anchors mode 4, i.e. the CENTRE of the quad, so
            // this is the centre of a 16x16 blob.
            var radarCenter = new Vector2(
                Level100ScannerProjection.CentreX,
                Level100ScannerProjection.CentreY);
            float yawRadians = snapshot.FacingYawMicroRad / 1_000_000f;
            foreach (Level100HudContactSnapshot contact in hud.Contacts
                         .Where(contact => contact.OnScanner))
            {
                Level100ScannerPlacement placement = Level100ScannerProjection.Place(
                    (contact.Position.X - snapshot.PlayerPosition.X) / 1_000f,
                    (contact.Position.Z - snapshot.PlayerPosition.Z) / 1_000f,
                    yawRadians);
                if (!placement.Drawn)
                {
                    continue;
                }

                var offset = new Vector2(placement.OffsetX, placement.OffsetY);
                int textureIndex = Math.Clamp((int)contact.Size, 0, assets.ScannerBlobs.Length - 1);
                DrawTextureRect(
                    assets.ScannerBlobs[textureIndex],
                    new Rect2(radarCenter + offset - new Vector2(8f, 8f), new Vector2(16f, 16f)),
                    false,
                    ContactColor(contact, placement.Alpha));
            }

            // Retail's objective pass is step 4 of six in
            // CHud__RenderTacticalRadarContacts, after the friendly, enemy and
            // repair-pad buckets, so it lands ON TOP of a contact for the same
            // unit. It walks the global objective list 0x00855140, takes the
            // same blob from the size selector - Medium for an unremarkable
            // unit - and paints it with the hard immediate 0xFFFFFF00.
            foreach (Level100HudObjectiveSnapshot objective in hud.Objectives)
            {
                Level100ScannerPlacement placement =
                    Level100ScannerProjection.PlaceObjective(
                        (objective.PositionMillimeters.X - snapshot.PlayerPosition.X) / 1_000f,
                        (objective.PositionMillimeters.Z - snapshot.PlayerPosition.Z) / 1_000f,
                        yawRadians);
                DrawTextureRect(
                    assets.ScannerBlobs[(int)Level100HudContactSize.Medium],
                    new Rect2(
                        radarCenter + new Vector2(placement.OffsetX, placement.OffsetY) -
                            new Vector2(8f, 8f),
                        new Vector2(16f, 16f)),
                    false,
                    RetailColor(0xff000000u | Level100ScannerProjection.ObjectiveTintRgb));
            }

            float yaw = snapshot.FacingYawMicroRad / 1_000_000f;
            Vector2 northCenter = new(65f, DesignHeight - 64f);
            Vector2 northPosition = northCenter + new Vector2(
                Mathf.Sin(yaw) * ScannerNorthRadius,
                -Mathf.Cos(yaw) * ScannerNorthRadius);
            DrawCenteredRotated(
                assets.RadioNorth,
                northPosition,
                new Vector2(32f, 32f),
                yaw,
                RetailColor(0xff5f7fff));
        }

        /// <summary>
        /// The alpha-blended half of the lower-right arc shell. Retail's
        /// <c>CHud__RenderObjectiveSlotFillPanel</c> (<c>0x00486940</c>) sets
        /// <c>SRCBLEND = SRCALPHA</c> / <c>DESTBLEND = INVSRCALPHA</c>
        /// (<c>RenderState_Set(0x13, 5)</c>, <c>(0x14, 6)</c> at
        /// <c>0x004869f6</c>) and draws <c>this+0x128</c> - WeaponFill - with the
        /// packed colour <b><c>0x3f000000</c></b>, a hard immediate
        /// (<c>push 0x3f000000</c> at <c>0x00486a5d</c>). Colour zero, alpha
        /// 0x3f: the fill is a pure 24.7 % darkener, which is why it is here and
        /// not on the additive layer. See
        /// <see cref="LowerRightArcShellRect"/> for the placement and the mirror.
        /// </summary>
        private void DrawLowerRightArcShellFill(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            DrawTextureRectRegion(
                assets.WeaponFill,
                LowerRightArcShellRect(),
                MirroredWeaponPageSource(),
                RetailColor(0x3f000000));
            // Retail's issue order is backing (1165), bar (1166), shell (1167).
            DrawLowerRightWeaponResource(snapshot, hud);
        }

        /// <summary>
        /// The weapon resource bar, on BOTH panels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Read off the device 2026-07-27 (in-level draws 1162 and 1166, three
        /// independent captures). Retail's bar is <b>horizontal and
        /// edge-anchored, not vertical</b>: the left one is anchored at x = 9
        /// with u = 0 at the anchor and spans the panel's full height
        /// y 339..467; the right one is anchored at x = 627 and mirrors it. Both
        /// were at width 0 (0 %) in every logged frame, which is why no captured
        /// retail pixel could ever have shown the axis - the geometry is the only
        /// evidence there is, and it is unambiguous.
        /// </para>
        /// <para>
        /// Both quads carry the pre-halved MODULATE2X diffuse 0x..3F7F1F, i.e.
        /// the effective colour <b>#7EFE3E</b>, with alpha <b>0x7F</b> on the
        /// left and <b>0xCC</b> on the right. This client previously drew a
        /// vertical, bottom-anchored bar at a fitted (0.28, 0.92, 0.38, 0.64) and
        /// drew no right-hand bar at all.
        /// </para>
        /// <para>
        /// The selected Pulse/Vulcan identity now reaches this owner, but the
        /// resource bars remain absent because heat, ammo and charge are still
        /// unprojected. The overheat flash tint is retained unmeasured -
        /// retail's overheat state never occurs in the captured timeline.
        /// </para>
        /// </remarks>
        private void DrawWeaponResource(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud,
            Rect2 rect)
        {
            if (ResourceFraction(hud) is not float fraction)
            {
                return;
            }

            float width = rect.Size.X * fraction;
            DrawTextureRectRegion(
                assets.WeaponFill,
                new Rect2(rect.Position.X, rect.Position.Y, width, rect.Size.Y),
                new Rect2(0f, 0f, 128f * fraction, 128f),
                ResourceTint(snapshot, hud, RetailColor(0x7f7efe3eu)));
        }

        /// <summary>The mirrored right-hand bar, retail draw 1166.</summary>
        private void DrawLowerRightWeaponResource(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            if (ResourceFraction(hud) is not float fraction)
            {
                return;
            }

            Rect2 rect = LowerRightArcShellRect();
            float width = rect.Size.X * fraction;
            // u = 0 sits at the anchor, x = 627, and grows leftwards; a negative
            // source width is how a Godot canvas item asks for that flip.
            DrawTextureRectRegion(
                assets.WeaponFill,
                new Rect2(rect.End.X - width, rect.Position.Y, width, rect.Size.Y),
                new Rect2(128f * fraction, 0f, -128f * fraction, 128f),
                ResourceTint(snapshot, hud, RetailColor(0xcc7efe3eu)));
        }

        private static float? ResourceFraction(Level100HudSnapshot hud)
        {
            Level100HudWeaponSnapshot weapon = hud.Weapon;
            int? resource = weapon.SelectedWeapon switch
            {
                Level100HudWeapon.PulseCannon => weapon.PulseHeatPermille is int heat
                    ? 1_000 - heat
                    : null,
                _ => null,
            };
            return resource is int resourcePermille
                ? Math.Clamp(resourcePermille / 1_000f, 0f, 1f)
                : null;
        }

        private static Color ResourceTint(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud,
            Color measured) =>
            // A 0.2 s half-cycle overheat blink: TicksPerSecond/5 ticks on,
            // the same off. This was a bare `/ 3` when Core ran at 30 Hz.
            hud.Weapon.PulseCannonOverheated == true &&
            (snapshot.Tick / (SimulationConstants.TicksPerSecond / 5)) % 2 == 0
                ? new Color(1f, 0.15f, 0.05f, measured.A)
                : measured;

        private void DrawWeaponIcon(Level100HudWeaponSnapshot weapon, Rect2 panel)
        {
            Texture2D? icon = weapon.SelectedWeapon switch
            {
                Level100HudWeapon.PulseCannon when weapon.PulseCannonEnabled => assets.WeaponIcons[0],
                Level100HudWeapon.VulcanCannon when weapon.VulcanCannonEnabled => assets.WeaponIcons[1],
                _ => null,
            };
            if (icon is null)
            {
                return;
            }

            DrawTextureRect(
                icon,
                new Rect2(panel.Position + new Vector2(32f, 30f), new Vector2(64f, 64f)),
                false,
                Colors.White);
        }

        private void DrawWeaponSelection(Level100HudSnapshot hud)
        {
            Level100HudWeaponSnapshot weapon = hud.Weapon;
            if (weapon.SelectionPanelVisible != true ||
                weapon.SelectionSlot is not Level100HudWeaponSelectionSlot selectionSlot ||
                selectionSlot == Level100HudWeaponSelectionSlot.None)
            {
                return;
            }

            Rect2 rect = GunsRect();
            DrawTextureRect(assets.GunsDarken, rect, false, RetailColor(0x78000000));
            Texture2D? selectedSlotTexture = selectionSlot switch
            {
                Level100HudWeaponSelectionSlot.Side => assets.GunsSide,
                Level100HudWeaponSelectionSlot.Front => assets.GunsFront,
                Level100HudWeaponSelectionSlot.Top => assets.GunsTop,
                _ => null,
            };
            if (selectedSlotTexture is not null)
            {
                DrawTextureRect(
                    selectedSlotTexture,
                    rect,
                    false,
                    new Color(0.50f, 0.75f, 0.88f, 0.80f));
            }
        }

        // The lower-right instrument, MEASURED end to end 2026-07-26. Two
        // independent sources agree and are recorded in
        // local-lab/PORTRAIT-BATTLELINE-FIELD-2026-07-26.md:
        //
        // (a) FRAMEBUFFER. On a pose-invariant clean mask - texels where all four
        //     shipped Tatiana poses are fully covered and agree to <= 4 levels,
        //     intersected with r < 40 about the fitted ring centre - across 22
        //     clean paired frames, retail draws the shipped texels through a
        //     channel-flat affine law retail = 0.5937*texel + 25.09, against our
        //     opaque 1.033*texel.
        //
        // (b) DEVICE. A controlled copied-runtime read of the safe copy
        //     (sha256 E1436EF7...) breaking on the sprite call sites inside
        //     0x00487d10 CHud__RenderBattleline and on the D3D device's own
        //     SetRenderState / SetSamplerState entries gives the whole pass:
        //
        //       CircleDarkener   diffuse 0x7fffffff  SRCALPHA/INVSRCALPHA
        //       CircleMask       diffuse 0xffffffff  SRCBLEND=ZERO DESTBLEND=ONE,
        //                        ZFUNC=ALWAYS, ZWRITE=1 - a colour-less depth
        //                        stamp that clips everything after it to the disc
        //       portrait         diffuse 0x40ffffff  SRCALPHA/INVSRCALPHA,
        //                        scale 0.75, UV 0..1, drawn SIX TIMES per frame
        //                        (408 of 444 observed draws carried 0x40 exactly;
        //                        the other 36 are the six opening interference
        //                        frames)
        //       message-noise    diffuse 0x3c..0x4a ffffff, SRCALPHA/INVSRCALPHA,
        //                        sampler WRAP, sub-pixel scrolled UV, drawn OVER
        //                        the portrait on EVERY frame of a message
        //       BattleLineOutline diffuse 0xff6f8faf, SRCBLEND=ONE DESTBLEND=ONE
        //
        // (c) THE ISSUED RECTANGLES, from the d3d9 draw log of 2026-07-27, which
        //     is the first evidence that gives them rather than implying them.
        //     All three captures agree byte for byte:
        //
        //       CircleDarkener   (519, 368) 128x128            <- BattleLineInstrumentRect
        //       CircleMask       (471, 320) 192x192, UV -0.25..1.25
        //       portrait x6      (519, 368)  96x96             <- 0.75 about (567, 416)
        //       message-noise    (503, 352) 128x128            <- BattleLinePortraitRect
        //       BattleLineOutline(519, 368) 128x128
        //
        //     The mask's -0.25..1.25 over a 192 px quad is 1.5 UV units across
        //     192 px, i.e. 128 px per unit: the page lands 1:1 on (503, 352)
        //     .. (631, 480) with its border texels CLAMP-extended 32 px on every
        //     side. So retail's depth-stamped disc is centred on (567.5, 416.5)
        //     at r 46.5, which is exactly where this client's baked CircleMask
        //     alpha puts it. The stamp is reproduced, not merely approximated.
        //
        // The two reconcile exactly. Six stacked draws at alpha 64/255 pass
        // 1 - (1 - 64/255)^6 = 0.8234 of the page; the noise drawn over them at
        // alpha ~= 66/255 = 0.259 attenuates that to 0.8234 * 0.741 = 0.610,
        // against the framebuffer's 0.5937. The apparent "alpha 0.594 over a
        // neutral field of 61.8" was those two stages collapsed into one.
        //
        // THE NEUTRAL FIELD IS THE NOISE PAGE. message-noise has page mean
        // [52.23, 52.39, 52.23] - neutral to B/R = 1.000 - so at alpha 0.26 it
        // contributes ~14 neutral levels of the 25.09 intercept and swamps the
        // remaining 0.1766 * 0.502 = 8.9% of the scene that still shows through
        // the half-opaque darkener. That is why the pedestal reads neutral and
        // terrain-independent without any render target being involved: the
        // earlier ruling-out of the terrain was right about the symptom and wrong
        // about the cause.
        private void DrawBattleLine()
        {
            // 0x7fffffff: alpha 127/255. The old 0.76 was unmeasured.
            DrawTextureRect(
                assets.CircleDarkener,
                BattleLineInstrumentRect(),
                false,
                new Color(1f, 1f, 1f, CircleDarkenerAlpha));

            // WHICH of the three recovered contents goes in the socket is
            // Level100HudLowerRightSocketLaw's decision, not this method's.
            // InfluenceOverlay and ForsetiIcon are the additive layer's;
            // Indeterminate draws the darkener and nothing else.
            if (_socket != Level100HudLowerRightSocket.PortraitAndNoise)
            {
                return;
            }

            bool portraitDrawn = false;
            if (_speaker is Level100HudSpeaker speaker &&
                _portraitPoseIndex is int pose)
            {
                int speakerIndex = speaker switch
                {
                    Level100HudSpeaker.Tatiana => 0,
                    Level100HudSpeaker.Technician => 1,
                    Level100HudSpeaker.Kramer => 2,
                    _ => -1,
                };
                if (speakerIndex >= 0)
                {
                    // One draw at the closed-form composite of retail's six.
                    //
                    // THE 0.75 SCALE IS ALREADY APPLIED, in the texture rather
                    // than in the rect, and that is easy to misread as missing.
                    // ApplyReleasedPortraitMask resamples the 128x128 page to
                    // 96x96 and insets it by 16, so drawing the result 1:1 into
                    // this 128x128 rect at (503, 352) puts the portrait IMAGERY
                    // at (519, 368) 96x96 - exactly the rect retail issues
                    // (device draws 1221-1226, 2026-07-27). Moving the rect to
                    // (519, 368, 96, 96) would apply the scale a SECOND time and
                    // shrink the face to 72x72.
                    DrawTextureRect(
                        assets.Portraits[speakerIndex][pose],
                        BattleLinePortraitRect(),
                        false,
                        new Color(1f, 1f, 1f, PortraitCompositeAlpha));
                    portraitDrawn = true;
                }
            }

            // OVER the portrait, neutral, every frame of a message - and, in
            // the promote gap where retail has an active CMessage* but no
            // selected portrait, ALONE. Retail's noise sprite is drawn after
            // the six portrait draws in 0x004b82b0 and outside their
            // this+0x24 gate, which is why the three pinned gap frames
            // (t011756, t019074, t031058) show noise and no face.
            //
            // Retail's diffuse alpha is NOT one constant: it is 0x3c..0x4a with
            // a portrait under it and 0x6c..0x74 without. Both regimes are
            // modelled; see MessageNoiseAlphaWithoutPortrait. Retail's rect for
            // this draw is (503, 352) 128x128 - the portrait rect, not the
            // instrument rect - and that is confirmed exactly.
            DrawTextureRect(
                assets.MessageNoiseDiscPhases[
                    _messageNoisePhase % assets.MessageNoiseDiscPhases.Length],
                BattleLinePortraitRect(),
                false,
                new Color(
                    1f,
                    1f,
                    1f,
                    portraitDrawn ? MessageNoiseAlpha : MessageNoiseAlphaWithoutPortrait));
        }

        /// <summary>
        /// Whether the panel box art is on screen. This is NOT "a message is
        /// active" - that was the defect.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>CMessageBox__RenderOverlay</c> (<c>0x004b8850</c>) gates the
        /// segmented meter bar on its deploy animator <c>+0x2c4</c> alone, and
        /// gates the text separately on the active <c>CMessage*</c> at
        /// <c>+0x8</c>. The animator retracts only when there is no active
        /// message AND the queue at <c>+0x18</c> is empty, so through an
        /// inter-message gap it is frozen fully open and the box does not
        /// blink. Level 100's script queues its messages from level start, so
        /// the queue is never empty across the captured timeline.
        /// </para>
        /// <para>
        /// Measured, not assumed: the box is present on ALL 68 HUD-visible
        /// retail reference frames, including the four whose text rectangle is
        /// completely empty (<c>t011756</c>, <c>t019074</c>, <c>t025065</c>,
        /// <c>t031058</c> - the first in both independent opening-pan runs).
        /// It is also already fully deployed on the very first frame the HUD is
        /// visible at all (<c>t006248</c>/<c>t006256</c>), because the deploy
        /// ran unseen behind the opening pan, so no fade-in is modelled here.
        /// </para>
        /// <para>
        /// The gate is the same one Core uses to release the first message -
        /// Stuart's <c>ALLOWED_TO_PLAY_MESSAGES</c>
        /// (<c>references/Onslaught/game.cpp:3026-3031</c>) - because no
        /// capture can separate it from HUD visibility: the HUD appears at tick
        /// ~179 and the box is allowed at tick 182, one 250 ms sample apart.
        /// </para>
        /// </remarks>
        private static bool MessageBoxIsDeployed(WorldSnapshot snapshot) =>
            snapshot.Level100Mission.Tick >= Level100MissionTiming.MessageBoxAllowedTick;

        private void DrawMessageFrame()
        {
            const float frameWidth = 252f;
            const float pieceHeight = 120f;
            const float innerWidth = 60f;
            Color innerTint = RetailColor(0x90000000);
            // Measured against the retail 640x480 frame: the panel body's
            // horizontal midpoint is x 341.5 (the two rim edges sit at 186.5 and
            // 496.5) and its hard top/bottom edges are y 405.5 and 464.5. These
            // constants predict 342, 405.25 and 464.3.
            float centerX = (DesignWidth * 0.5f) + 22f;
            float centerY = DesignHeight - 41f;
            float leftCenter = centerX - (frameWidth * 0.5f);
            float rightCenter = centerX + (frameWidth * 0.5f);
            float top = centerY - (pieceHeight * 0.5f);

            DrawTextureRect(
                assets.ObjectiveLeft,
                new Rect2(leftCenter - (pieceHeight * 0.5f), top, pieceHeight, pieceHeight),
                false);
            DrawTextureRect(
                assets.ObjectiveInnerLeft,
                new Rect2(leftCenter - innerWidth, top, innerWidth, pieceHeight),
                false,
                innerTint);
            DrawTextureRect(
                assets.ObjectiveRight,
                new Rect2(rightCenter - (pieceHeight * 0.5f), top, pieceHeight, pieceHeight),
                false);
            DrawTextureRect(
                assets.ObjectiveInnerRight,
                new Rect2(rightCenter, top, innerWidth, pieceHeight),
                false,
                innerTint);

            float remaining = frameWidth;
            float x = leftCenter;
            while (remaining > 0f)
            {
                float width = Mathf.Min(innerWidth, remaining);
                DrawTextureRectRegion(
                    assets.ObjectiveInnerCentre,
                    new Rect2(x, top, width, pieceHeight),
                    new Rect2(0f, 0f, 64f * (width / innerWidth), 128f),
                    innerTint);
                x += width;
                remaining -= width;
            }
        }

        // Only the OFF-screen arrow is drawn here. The on-screen reticle moved
        // to the additive glow layer: see WorldMarkerScreenX for the measured
        // reason, and RetailHudGlowLayer.DrawWorldMarkerReticles for the draw.
        //
        // offscreen-arrow is a DXT2 page with a real straight-alpha channel -
        // 71.9% of its texels are alpha 0 - so it composites correctly here and
        // would render as a solid white square on an additive layer, because
        // those transparent texels carry RGB 255.
        private void DrawWorldMarkers(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            foreach (Level100HudObjectiveSnapshot objective in hud.Objectives)
            {
                SimVector2 position = HorizontalPosition(objective.PositionMillimeters);
                if (WorldMarkerScreenX(snapshot, position) is not null)
                {
                    continue;
                }

                float side = Math.Sign(RelativeYaw(snapshot, position));
                Vector2 arrowCenter = new(
                    side < 0f ? 28f : DesignWidth - 28f,
                    DesignCenter.Y);
                DrawCenteredRotated(
                    assets.OffscreenArrow,
                    arrowCenter,
                    new Vector2(32f, 32f),
                    side < 0f ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f,
                    new Color(1f, 0.92f, 0.08f, 1f));
            }
        }

        private void DrawCrosshair(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            // The retail frame's crosshair rings are centred on (320, 240) at
            // 640x480, i.e. exactly the design-stage centre, and the device log
            // confirms the rects to the hundredth of a pixel: the 64x64 pages at
            // (288.0098, 208.0001) and the 128x128 page at (256.0098,
            // 176.0001), against this code's (288, 208) and (256, 176).
            //
            // ORDER, and it is retail's, not ours: 64 -> 128 -> 64. This file
            // used to draw 128 -> 64 -> 64, which puts the faint outer ring
            // UNDER the bright reticle instead of over it.
            //
            // COUNT: retail issues THREE crosshair quads in this frame and this
            // path used to issue FOUR. The proxy cannot name which two 64x64
            // pages it saw - it records size and format, not identity - so the
            // assignment below is an inference from the photometry noted on
            // RetailCrosshairBright, not a measurement: a 64x64 page at alpha
            // 0.3412 accounts for retail's r<=2 centre dot exactly, and one at
            // 0.6863 accounts for the r 26..28 reticle, which leaves no
            // brightness budget for a fourth draw into the same band.
            // crosshair-outline is therefore no longer drawn in the untargeted
            // state; this file's own note that it "draws into the same r 25..29
            // band as crosshair-primary" is why it is the one that goes.
            //
            // Confirmed against retail t029072 by capture, not left as an
            // argument. Radial mean brightness about (320,240), before -> after
            // -> retail: r0 216 -> 161 -> 156; r2 179 -> 135 -> 121; r13
            // 161 -> 111 -> 116; r27 203 -> 212 -> 212; r35 200 -> 199 -> 200.
            // The r 12..15 band was 25-45 DN hot and only the removed fourth
            // draw explains it.
            Vector2 center = DesignCenter;
            DrawTextureRect(
                assets.CrosshairPrimary,
                new Rect2(center - new Vector2(32f, 32f), new Vector2(64f, 64f)),
                false,
                RetailCrosshairBright);
            DrawTextureRect(
                assets.CrosshairSecondary,
                new Rect2(center - new Vector2(64f, 64f), new Vector2(128f, 128f)),
                false,
                RetailCrosshairFaint);
            DrawTextureRect(
                assets.CrosshairDot,
                new Rect2(center - new Vector2(32f, 32f), new Vector2(64f, 64f)),
                false,
                RetailCrosshairFaint);

            if (hud.Target is not Level100HudTargetSnapshot target)
            {
                return;
            }

            Level100HudContactSnapshot? contact = hud.Contacts.FirstOrDefault(
                candidate => candidate.Id == target.ContactId);
            Texture2D classification = contact?.Allegiance == Level100HudAllegiance.Friendly
                ? assets.CrosshairFriend
                : assets.CrosshairEnemy;
            Color classificationTint =
                contact is null ? Colors.White : ContactColor(contact, 255);
            DrawTextureRect(
                classification,
                new Rect2(center - new Vector2(32f, 32f), new Vector2(64f, 64f)),
                false,
                classificationTint);
            DrawTextureRect(
                assets.TargetSighted,
                new Rect2(center - new Vector2(32f, 32f), new Vector2(64f, 64f)),
                false,
                new Color(1f, 1f, 1f, Math.Clamp(target.LockPermille / 1_000f, 0f, 1f)));

            float predictedYaw = RelativeYaw(snapshot, target.PredictedPosition);
            float predictorX = center.X + ((predictedYaw / 1.05f) * (DesignWidth * 0.42f));
            predictorX = Math.Clamp(predictorX, 32f, DesignWidth - 32f);
            DrawTextureRect(
                assets.CrosshairPredictor,
                new Rect2(new Vector2(predictorX, center.Y) - new Vector2(32f, 32f), new Vector2(64f, 64f)),
                false,
                classificationTint);

        }

        private static bool PortraitSetIsReady(Texture2D[] portraits) =>
            portraits.Length == 4 &&
            portraits.All(portrait => portrait.GetSize() == new Vector2I(128, 128));
    }

    // battleline-outline and circle-darkener carry their ring in the top-left
    // 98x98 of a 128x128 page (content bbox x[1..97] y[1..97], centre 49,49),
    // while circle-mask - and therefore the masked portrait - carries its disc
    // centred on the page at (64.5, 64.5) r=46.5. The two are 15.5px apart, so
    // one rect cannot place both.
    //
    // Measured on the retail 640x480 frame, the lower-right ring is a circle at
    // centre (568.08, 416.46) r=46.76 (50-point fit, rms 0.34), matching the
    // lower-left ring's r=46.56 to within 0.2px. That puts:
    //   ring/darkener page origin at (568.08-49, 416.46-49) = (519.1, 367.5)
    //   portrait page origin at (568.08-64.5, 416.46-64.5) = (503.6, 352.0)
    // The old single rect (DesignWidth-137, DesignHeight-128) = (503, 352) was
    // right for the portrait and 15.5px up-and-left for the ring.
    private static Rect2 BattleLineInstrumentRect() =>
        new(DesignWidth - 121f, DesignHeight - 112f, 128f, 128f);

    /// <summary>
    /// The pale hooked arc shell around the lower-right instrument: a
    /// horizontally MIRRORED copy of the same 128x128 weapon page the lower-left
    /// panel uses, at <c>Rect2(DesignWidth - 141, DesignHeight - 141, 128, 128)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PLACEMENT, read out of <c>CHud__RenderObjectiveSlotFillPanel</c>
    /// (<c>0x00486940</c>) in the pristine image rather than fitted. Its
    /// single-player energy-weapon branch computes
    /// <c>x = ((width - _DAT_005dbe98) + _DAT_005dbe7c) - _DAT_005dbe34) - _DAT_0067a628</c>
    /// and <c>y = (height - _DAT_005dbe80) - _DAT_0067a62c</c>, with
    /// <c>_DAT_005dbe98 = 122.0</c>, <c>_DAT_005dbe7c = 9.0</c>,
    /// <c>_DAT_005dbe34 = 28.0</c> and <c>_DAT_005dbe80 = 13.0</c>. The two
    /// <c>0x0067a6xx</c> viewport terms are zero in single player - that is the
    /// same pair already tied to zero by the ForsetiIcon/darkener fit in
    /// <see cref="BattleLineInstrumentRect"/>. So x = width - 141 = 499 and the
    /// bottom-anchored y = height - 13 = 467, i.e. the page occupies
    /// (499, 339)..(627, 467). That is EXACTLY the mirror of the lower-left
    /// weapon rect (9, 339)..(137, 467) under x' = 635 - x.
    /// </para>
    /// <para>
    /// The MIRROR itself is pixel-derived, not byte-derived. Correlating a
    /// median-high-passed 27-frame mean of
    /// <c>retail-reference-pristine/level100-gameplay/hud-timeline-run1/</c>
    /// against the high-passed weapon template in band r 48..82 about the fitted
    /// right ring centre (568.08, 416.46) gives <b>r = +0.532 mirrored at
    /// dx = dy = 0</b> against <b>r = +0.072 unmirrored</b> (best of an 81-offset
    /// sweep); the same estimator on the left band about (66.01, 417.25) gives
    /// <b>+0.481 unmirrored</b> against <b>+0.076 mirrored</b>. Ghidra's reading
    /// of the shipped call's trailing <c>(0.0, 1.0)</c> argument pair - which the
    /// split-screen branch swaps to <c>(1.0, 0.0)</c>, exactly the shape of a u
    /// range - would say this draw is UNmirrored.
    /// </para>
    /// <para>
    /// RESOLVED 2026-07-27 in favour of the pixels, from the issued vertices
    /// rather than from either inference: the device log gives
    /// <b>u = 1 at x = 499 and u = 0 at x = 627</b> on this draw AND on the
    /// WeaponFill backing under it, in all three independent captures. The draw
    /// is MIRRORED and the Ghidra argument mapping is the thing that was wrong.
    /// The rect itself is confirmed byte-for-byte: retail issues
    /// (499, 339) 128x128.
    /// </para>
    /// <para>
    /// The dynamic resource/heat fill and the weapon icon are deliberately NOT
    /// mirrored here. Retail's function draws a middle
    /// <c>CVBufTexture__DrawSpriteEx</c> between the two below, reusing the same
    /// <c>this+0x128</c> page scaled by the ammo percentage and tinted from it;
    /// this client's producer does not carry that state for a right-hand slot,
    /// and mirroring the left panel's heat bar would be an invention. Retail's
    /// <c>0x008aa530</c>/<c>0x008aa534</c> visibility gate is likewise not
    /// implemented: the shell is drawn unconditionally, as the left panel is.
    /// </para>
    /// </remarks>
    private static Rect2 LowerRightArcShellRect() =>
        new(DesignWidth - 141f, DesignHeight - 141f, 128f, 128f);

    /// <summary>
    /// A negative-width source rect, which is how Godot's canvas item asks for a
    /// horizontal flip of the whole 128x128 page.
    /// </summary>
    private static Rect2 MirroredWeaponPageSource() => new(0f, 0f, -128f, 128f);

    /// <summary>
    /// The 128x128 page rect at (503, 352) shared by the message-noise draw and
    /// by the pre-scaled, pre-masked portrait page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retail issues message-noise at exactly this rect (device draw 1227,
    /// 2026-07-27) and the portrait at <b>(519, 368) 96x96</b> (draws
    /// 1221-1226). Those are not two different placements of the same thing:
    /// <see cref="ApplyReleasedPortraitMask(Texture2D, Image)"/> has always
    /// resampled the 128x128 portrait page to 96x96 and inset it by 16, so the
    /// portrait IMAGERY this rect puts on screen already occupies
    /// (519, 368) 96x96. Retail's 0.75 is applied in the quad; ours is applied
    /// in the texture; the screen result is the same rectangle.
    /// </para>
    /// <para>
    /// Do not "fix" this to (519, 368, 96, 96). That would scale the already
    /// scaled page again - a 72x72 face - and would shrink the baked disc clip
    /// from retail's r 46.5 to r 34.9.
    /// </para>
    /// </remarks>
    private static Rect2 BattleLinePortraitRect() =>
        new(DesignWidth - 137f, DesignHeight - 128f, 128f, 128f);

    // 104 and 32 are _DAT_005dbeb0 and _DAT_005db2b8 from the ForsetiIcon call
    // in 0x00487d10, against the same hudX/fVar1 pair the two rects above are
    // expressed in. See RetailHudGlowLayer.DrawForsetiIcon.
    private static Rect2 ForsetiIconRect() =>
        new(DesignWidth - 104f, (DesignHeight - 128f) - 32f, 64f, 64f);

    // The weapon-selection page is not on screen in the retail reference frame,
    // so its offsets are unverified and deliberately left as they were - note
    // that its 137 is no longer tied to the battleline's corrected 121.
    private static Rect2 GunsRect() =>
        new(DesignWidth - 137f, DesignHeight - 240f, 128f, 128f);

    /// <summary>
    /// Retail's ONE/ONE passes. Everything drawn here is one of the twelve DXT1
    /// HUD pages, and that is decided by the DDS bytes, not by convention.
    ///
    /// Every one of those twelve pages carries alpha 255 at EVERY texel. DXT1's
    /// 3-colour (punch-through) block mode is present in all of them -
    /// radar-outline 562/1024 blocks, screen-marker 189/256, guns-front 963/1024,
    /// battleline-outline 920/1024, weapon-outline 863/1024, guns-outline 901,
    /// guns-side 904, guns-top 981, bar-line 32/64, damage-flash 148/256,
    /// message-noise 6/1024, threat-flash 1/64 - but the transparent index-3
    /// texel is used ZERO times in ZERO of those blocks, 12 pages out of 12. So
    /// none of them carries anything that could serve as coverage.
    ///
    /// Retail has nowhere to derive coverage from either. Every
    /// D3DXCreateTextureFromFileEx call site passes ColorKey=0, so the only
    /// routine that applies a colour key, the linked D3DX
    /// D3DXTex::CCodec::ColorKey (0x00581E1C), is inert; and the binary's
    /// luminance code is all L8/A8L8/A4L4/L16 surface-format packing, none of
    /// which writes luminance into alpha. There is no luminance-keying and no
    /// colour-keying in the released engine.
    ///
    /// And retail selects FOUR blends, not the three this comment used to
    /// claim. Across 6,429 exported decompilations, SRCBLEND/DESTBLEND take
    /// SRCALPHA/INVSRCALPHA (58/54 sites), ONE/ONE (20/24) and ZERO/ONE (8/2) -
    /// no SRCCOLOR, INVSRCCOLOR, DESTALPHA or DESTCOLOR appears anywhere. The
    /// fourth was measured at the device on 2026-07-27 rather than counted in
    /// the decompilations: the two threat-compass rings (in-level draws 1153 and
    /// 1154) are issued <b>ONE/INVSRCALPHA</b>, a PREMULTIPLIED-alpha blend,
    /// which CDXCompass__RenderWorldSpaceOverlay already sets at 0x0053cd30 (see
    /// the block on GaugeHealthBlendAlpha). It is a fourth pair, not one of the
    /// three, and any statement of the form "retail only ever uses two blends"
    /// or "...three blends" is wrong. A DXT1 page drawn
    /// SRCALPHA/INVSRCALPHA is therefore an opaque rectangle, which retail
    /// plainly does not show; the twelve DXT1 pages are its ONE/ONE passes, and
    /// they stay additive. CDXCompass__Render and CHud__RenderBattleline both
    /// contain an explicit ONE/ONE section, which is where they belong.
    ///
    /// The pages that DO carry coverage - the DXT2 pages, with real per-texel
    /// alpha - are on the alpha-blended base layer, including
    /// compass-objective-marker, which used to be the one DXT2 page drawn here.
    /// </summary>
    private sealed partial class RetailHudGlowLayer(HudAssets assets) : RetailHudLayer
    {
        private WorldSnapshot? _snapshot;
        private Level100HudSnapshot? _hud;
        private Level100HudLowerRightSocket _socket =
            Level100HudLowerRightSocket.Indeterminate;

        public bool IsReady =>
            assets.ForsetiIcon.GetSize() == new Vector2I(64, 64) &&
            assets.BattleLineMarker.GetSize() == new Vector2I(16, 16) &&
            assets.RadarOutline.GetSize() == new Vector2I(128, 128) &&
            assets.WeaponOutline.GetSize() == new Vector2I(128, 128) &&
            assets.BattleLineOutline.GetSize() == new Vector2I(128, 128) &&
            assets.MessageNoise.GetSize() == new Vector2I(128, 128) &&
            assets.Dial.Length == 8_192 &&
            assets.BarLine.GetSize() == new Vector2I(16, 64) &&
            assets.ThreatFlash.GetSize() == new Vector2I(32, 32) &&
            assets.DamageFlash.GetSize() == new Vector2I(128, 32);

        public int Energy { get; private set; }
        public int Shield { get; private set; }
        public int Health { get; private set; }

        public override void _Ready()
        {
            Material = CreateReleasedAlphaTestMaterial(additive: true);
        }

        public void SetState(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud,
            Level100HudLowerRightSocket socket)
        {
            _snapshot = snapshot;
            _hud = hud;
            _socket = socket;
            Energy = snapshot.Energy;
            Shield = snapshot.Shield;
            Health = snapshot.Hull;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_snapshot is not WorldSnapshot snapshot ||
                _hud is not Level100HudSnapshot hud)
            {
                return;
            }

            BeginDesignSpace();
            DrawInstrumentOutlines(snapshot, hud);
            DrawDynamicCompass(snapshot, hud);
            DrawBattleLineInfluence(hud);
            DrawBattleLineOutline(snapshot, hud);
            DrawForsetiIcon();
            DrawWorldMarkerReticles(snapshot, hud);
            EndDesignSpace();
        }

        /// <summary>
        /// Provisional node view of Level 100's populated influence map. The
        /// authored node positions and signed values are real; the compact
        /// normalization and equal-weight actor accumulation are navigation
        /// hypotheses, not the retail terrain-triangulated interior.
        /// </summary>
        private void DrawBattleLineInfluence(Level100HudSnapshot hud)
        {
            if (_socket != Level100HudLowerRightSocket.InfluenceOverlay ||
                !hud.BattleLine.HasInfluenceValues ||
                hud.BattleLine.InfluencePermille.Count != Level100HudInfluenceMap.Nodes.Count)
            {
                return;
            }

            IReadOnlyList<Level100HudInfluenceNode> nodes = Level100HudInfluenceMap.Nodes;
            int minimumX = nodes.Min(node => node.Position.X);
            int maximumX = nodes.Max(node => node.Position.X);
            int minimumZ = nodes.Min(node => node.Position.Z);
            int maximumZ = nodes.Max(node => node.Position.Z);
            Rect2 instrument = BattleLineInstrumentRect();
            Vector2 center = instrument.Position + new Vector2(49.08f, 48.46f);
            const float span = 55f;
            Vector2 markerHalfSize = assets.BattleLineMarker.GetSize() * 0.5f;

            for (int index = 0; index < nodes.Count; index++)
            {
                Level100HudInfluenceNode node = nodes[index];
                float x = center.X +
                    ((((float)node.Position.X - minimumX) / (maximumX - minimumX) - 0.5f) * span);
                float y = center.Y +
                    ((0.5f - ((float)node.Position.Z - minimumZ) / (maximumZ - minimumZ)) * span);
                short value = hud.BattleLine.InfluencePermille[index];
                Color tint = value switch
                {
                    > 0 => RetailColor(0xff5050afu),
                    < 0 => RetailColor(0xffaf0808u),
                    _ => RetailColor(0xff606060u),
                };
                tint.A = 0.35f + (0.65f * MathF.Abs(value) / 1_000f);
                DrawTextureRect(
                    assets.BattleLineMarker,
                    new Rect2(new Vector2(x, y) - markerHalfSize, assets.BattleLineMarker.GetSize()),
                    false,
                    tint);
            }
        }

        /// <summary>
        /// The third row of <see cref="Level100HudLowerRightSocketLaw"/>: the
        /// single <c>hud\ForsetiIcon.tga</c> sprite retail draws when no message
        /// is active and the influence-map list is empty.
        /// </summary>
        /// <remarks>
        /// <para>
        /// GEOMETRY, from the shipped call in <c>CHud__RenderBattleline</c>
        /// (<c>0x00487d10</c>):
        /// <c>DrawSpriteEx((hudX - _DAT_005dbeb0) - shift, fVar1 - _DAT_005db2b8,
        /// 0.001, [this+0x1d4], 2, 0, 1.0, 0.0, …, 1.0, 1.0, 0.0, 1.0, 0.0, 1.0)</c>
        /// - mode 2, unit scale, full UV. The three constants read out of the
        /// pristine image are <c>_DAT_005dbeb0 = 104.0</c>,
        /// <c>_DAT_005db2b8 = 32.0</c> and, on the darkener's own call,
        /// <c>_DAT_005dbeb8 = 121.0</c> against this client's already-fitted
        /// <see cref="BattleLineInstrumentRect"/> x of
        /// <c>DesignWidth - 121</c>, which is what ties retail's
        /// <c>hudX</c> to <c>DesignWidth</c> and <c>fVar1</c> to
        /// <c>DesignHeight - 128</c> (the portrait page origin, likewise
        /// already fitted). The icon page is 64x64, so it lands at
        /// (536, 320)..(600, 384) on the 640x480 frame: horizontally centred on
        /// the fitted ring centre x = 568.08 to 0.1 px, and sitting high, with
        /// its lower edge 33 px above the ring centre.
        /// </para>
        /// <para>
        /// BLEND: the branch inherits the <c>ONE/ONE</c> state set for the
        /// BattleLineOutline draw immediately above it and sets none of its own,
        /// so it is additive and belongs on this layer.
        /// </para>
        /// <para>
        /// UNVERIFIED AGAINST PIXELS, and it cannot be: this state occurs on
        /// ZERO of the 183 pinned retail frames, so nothing
        /// here - placement, blend or premultiplication - has a retail frame to
        /// be checked against. Level 100's populated projection never reaches
        /// this draw; it exists so the recovered third row is implemented rather
        /// than merely described.
        /// </para>
        /// </remarks>
        private void DrawForsetiIcon()
        {
            if (_socket != Level100HudLowerRightSocket.ForsetiIcon)
            {
                return;
            }

            DrawTextureRect(assets.ForsetiIcon, ForsetiIconRect(), false, Colors.White);
        }

        /// <summary>
        /// screen-marker is a DXT1 page: it carries NO alpha channel at all -
        /// all 4096 texels decode to alpha 255 - and stores a white segmented
        /// reticle on a black field, exactly like the eleven other DXT1 HUD
        /// pages (radar-outline, battleline-outline, weapon-outline, guns-*,
        /// message-noise, threat-flash, damage-flash, bar-line), every one of
        /// which this layer already draws additively so that the black field is
        /// a no-op.
        ///
        /// It was the only one drawn on the alpha-blended base layer, which
        /// composited that opaque black field as a solid 64x64 black box around
        /// the reticle. Reproduced in a 640x480 gameplay capture at level offset
        /// t+38063 ms: 1959 pixels of RGB &lt; 12 in a 64 px square centred on the
        /// marker, where the matched retail frame
        /// hud-timeline-run1/level100-t038063ms.png has none.
        /// </summary>
        private void DrawWorldMarkerReticles(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            foreach (Level100HudObjectiveSnapshot objective in hud.Objectives)
            {
                if (WorldMarkerScreenX(
                        snapshot,
                        HorizontalPosition(objective.PositionMillimeters)) is not float x)
                {
                    continue;
                }

                DrawTextureRect(
                    assets.ScreenMarker,
                    new Rect2(
                        new Vector2(x, DesignCenter.Y) - new Vector2(32f, 32f),
                        new Vector2(64f, 64f)),
                    false,
                    new Color(1f, 0.92f, 0.08f, 1f));
            }
        }

        private void DrawInstrumentOutlines(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            float radarHighlight = HighlightAlpha(snapshot, hud, Level100HudPart.Radar);
            float weaponHighlight = HighlightAlpha(
                snapshot,
                hud,
                Level100HudPart.CurrentWeapon);
            DrawTextureRect(
                assets.RadarOutline,
                new Rect2(17f, DesignHeight - 112f, 128f, 128f),
                false,
                new Color(0.44f + radarHighlight, 0.56f + (radarHighlight * 0.35f), 0.69f, 1f));
            // THE LEFT AND RIGHT ARC SHELLS CARRY THE SAME DIFFUSE. There is no
            // asymmetry, and the one this file used to draw was an artefact of
            // reading the two shells from two different kinds of evidence.
            //
            // Read off the device 2026-07-27 (in-level draws 1163 and 1167, three
            // independent captures, byte-identical in all three): BOTH shells are
            // issued ONE/ONE with s0.COLOROP = MODULATE2X and the quad diffuse
            // 0xFF574737, so both reach the framebuffer as the additive colour
            // 0x57*2 / 0x47*2 / 0x37*2 = #AE8E6E. This layer draws MODULATE with
            // the doubled colour, which is the same arithmetic (see the
            // MODULATE2X block on RetailHudGlowLayer).
            //
            // The right shell was previously 0xffaf8f6f, from the hard immediate
            // `push 0xffaf8f6f` at 0x00486c7b, and the left was 0xff7f7f7f from
            // the corresponding left-panel immediate. Retail HALVES those
            // immediates into the MODULATE2X diffuse, and 0xAF >> 1 = 0x57 loses
            // the low bit, so what is actually issued is #AE8E6E on both sides.
            // The 1-LSB difference is why the right shell measured "correct"; the
            // left one did not, and the "1.49 energy surplus" investigation that
            // chased the asymmetry was chasing a phantom.
            // local-lab/agent-notes-2026-07-27/inlevel-hud-coordinates.md section 5.
            //
            // Confirmed against retail t029072 on the shell's own 1,054 ink
            // texels rather than on a box mean: the signed error went from
            // (-3.3, +7.9, +20.5) at 0xff7f7f7f to (+13.8, +14.1, +13.8) at
            // this colour - blue-tilted to achromatic within 0.4 DN. The flat
            // +14 that remains is a brightness residual on this panel that
            // predates the colour and is not the diffuse.
            DrawTextureRect(
                assets.WeaponOutline,
                new Rect2(9f, DesignHeight - 141f, 128f, 128f),
                false,
                weaponHighlight > 0f
                    ? new Color(0.50f, 1f, 0.25f, 1f)
                    : RetailColor(RetailArcShellDiffuse));

            // The additive half of the lower-right arc shell, mirrored. The
            // MIRROR is no longer an inference: the device log gives the issued
            // UVs directly - u = 1 at x = 499 and u = 0 at x = 627 on BOTH the
            // outline and the WeaponFill backing under it - which settles the
            // "recorded as unresolved" note on LowerRightArcShellRect.
            DrawTextureRectRegion(
                assets.WeaponOutline,
                LowerRightArcShellRect(),
                MirroredWeaponPageSource(),
                RetailColor(RetailArcShellDiffuse));

            Rect2 gunsRect = GunsRect();
            Level100HudWeaponSnapshot weapon = hud.Weapon;
            if (weapon.SelectionPanelVisible == true &&
                weapon.SelectionSlot is Level100HudWeaponSelectionSlot selectionSlot &&
                selectionSlot != Level100HudWeaponSelectionSlot.None)
            {
                DrawTextureRect(assets.GunsOutline, gunsRect, false, RetailColor(0xff6f8faf));
            }
        }

        private void DrawDynamicCompass(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            // Radii MEASURED off the 27 retail frames in
            // local-lab/retail-reference-pristine/level100-gameplay/hud-timeline-run1/,
            // as a per-radius median of the annulus at 1 px steps about
            // (320, 240), taken separately in gauge-free bearings (155-215 deg
            // and 350-50 deg) and in the two gauge bearings:
            //
            //   base ring   present at EVERY bearing, rises at r 95, plateaus
            //               r 96..100, falls at r 101, back to background by
            //               r 102 -> inner 95, outer 101.
            //   gauge band  present ONLY where a gauge is drawn: green over
            //               bearings 57-148 deg, blue/violet over 220-345 deg;
            //               both rise from r 80..82, peak at r 91, and are gone
            //               by r 93 -> inner 80, outer 92.
            //   between them retail has NOTHING: in the gauge-free bearings
            //               r 70..94 is a smooth background gradient with no
            //               feature at all.
            //
            // The previous CDXCompass-derived generator produced bands at
            // 67.2..100 and 65.6..79.9 - a 32.8 px stroke where retail draws 6,
            // plus a second full-circle band retail does not draw. That is the
            // measured cause of the heavy centre glow. The generator model is
            // replaced by the measurement rather than renormalised, because no
            // normalisation constant can turn two overlapping bands into one
            // ring plus a gauge-only band.
            Vector2 center = DesignCenter;
            float compassHighlight = HighlightAlpha(snapshot, hud, Level100HudPart.Compass);
            // The two halves of the one premultiplied draw MUST use the same
            // paint, so this reads the shared constant rather than repeating a
            // literal - the duplicate literal is how the two halves drifted
            // apart before. See the CompassBaseColor block for the texel.
            Color baseColor = CompassBaseColor(snapshot, hud, compassHighlight);

            // The gauge band radii now live on CompassGaugeInnerRadius /
            // CompassGaugeOuterRadius, because both halves of the arc need them.
            const float outerRadius =
                (CompassBaseRingInnerRadius + CompassBaseRingOuterRadius) * 0.5f;
            const float outerWidth =
                CompassBaseRingOuterRadius - CompassBaseRingInnerRadius;

            // 50 SEGMENTS IS CONFIRMED AT THE DEVICE, not inferred from
            // CDXCompass__BuildRingGeometry: in-level draw 1154 is a 100-triangle,
            // 102-vertex TRISTRIP - 50 quads - and draw 1153 is 80/82, i.e. 40.
            // Their pages are 512x32 and 256x8 A4R4G4B4, which identifies the
            // "dynamically written 16-bit ring pixels" PROVENANCE lists as
            // unproven. What the log CANNOT give is where they land: both rings
            // are the only HUD elements issued in 3-D (fvf=0x102), so their
            // positions pass through SetTransform, which the proxy does not
            // shadow, and their colour comes entirely from those dynamic
            // textures, which it does not record. Tasks #96, #117 and #106's
            // base-ring colour therefore remain open for a STRUCTURAL reason.
            DrawSegmentedRing(center, outerRadius, 50, outerWidth, 0f, 1f, baseColor);

            // Retail's base ring is the only full-circle band, and retail draws
            // it TWICE - once alpha-blended, once additive. That is forced by the
            // measurement, not chosen: regressing the retail ring on its own
            // background gives retention s=0.733 AND intercept c=93.1 together
            // (n=10,607, gauge-free bearings, r 96-100 against retail's
            // feature-free r 93-94 / r 102-104, with empty-annulus controls at
            // s=1.035 and s=0.996). A single SRCALPHA/INVSRCALPHA pass with
            // s=0.733 has alpha 0.267 and can emit at most 0.267*255 = 68, so it
            // cannot reach 93.1; a single ONE/ONE pass has s=1 and cannot reach
            // 0.733. Only alpha-blend-then-add satisfies both, which is exactly
            // the shape of CDXCompass__Render (0x00427210): overlay sprite state,
            // then the ONE/ONE state, then SRCALPHA/INVSRCALPHA.
            //
            // The attenuating half is RetailHudBaseLayer.DrawCompassBaseRing.
            // This is the additive half, and the dial north overlay rides the
            // same additive pass it always did. Both halves now read one shared
            // CompassBaseColor, whose paint and alpha come from the ring's own
            // texel rather than from this regression - see that block. The fit
            // recorded here is retained because it is the independent check the
            // texel now agrees with, not because anything is fitted to it.
            //
            // The gauge band carries no track of its own outside the two gauge
            // arcs, so none is drawn here.
            //
            // The gauge ARCS stay additive. Measured the same way as the base
            // ring, over the bearings where each arc is actually drawn:
            //   green  gauge arc r 88-91   n= 8,117   s=0.922
            //   violet gauge arc r 88-91   n=14,898   s=0.697
            // against controls of s=1.035 and s=0.996 for empty annuli. Green is
            // within a few percent of additive. Violet is not (alpha 0.30), but
            // this file draws both arcs from one code path at alpha 0.88, and
            // alpha-blending them at 0.88 would retain only 12% of the background
            // where retail retains 70-92%. Additive is the closer of the two
            // available answers for both, so both are left additive rather than
            // converted; see the deliverable note.
            float health = Math.Clamp(
                Health / (float)SimulationConstants.MaximumHull,
                0f,
                1f);
            float energy = Math.Clamp(
                Energy / (float)SimulationConstants.MaximumEnergy,
                0f,
                1f);
            Color healthColor = new Color(1f - health, health, 0.08f, 0.88f);
            Color energyColor = new Color(0.18f, 0.62f, 1f, 0.88f);
            float healthHighlight = HighlightAlpha(snapshot, hud, Level100HudPart.Health);
            float energyHighlight = HighlightAlpha(snapshot, hud, Level100HudPart.Energy);
            healthColor.A = Math.Clamp(healthColor.A + healthHighlight, 0f, 1f);
            energyColor.A = Math.Clamp(energyColor.A + energyHighlight, 0f, 1f);
            // Both gauges share retail's single r 80..92 band. Their bearings
            // already agree with the measurement: the health sweep runs
            // 60-150 deg against a green arc measured over 57-148 deg, and the
            // energy sweep runs 225-360 deg against a violet arc measured over
            // 220-345 deg. Only the radii were wrong.
            _ = healthColor;
            _ = energyColor;
            DrawCompassGaugeArcs(snapshot, hud, alphaBlendedHalf: false);
            DrawDialNorthOverlay(
                snapshot,
                center,
                CompassBaseRingInnerRadius,
                CompassBaseRingOuterRadius,
                baseColor);

            DrawThreats(hud, center);
            DrawDamageFlashes(hud, center);
            DrawGaugeNeedles(center, health, energy);
        }

        private void DrawThreats(Level100HudSnapshot hud, Vector2 center)
        {
            foreach (Level100HudThreatSnapshot threat in hud.Threats)
            {
                float angle = threat.RelativeYawMicroRad / 1_000_000f;
                float alpha = Math.Clamp(threat.TicksRemaining / 600f, 0f, 1f);
                Vector2 position = center +
                    new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * CompassThreatRadius;
                DrawCenteredRotated(
                    assets.ThreatFlash,
                    position,
                    new Vector2(32f, 32f),
                    angle,
                    new Color(1f, 1f, 1f, alpha));
            }
        }

        private void DrawDamageFlashes(Level100HudSnapshot hud, Vector2 center)
        {
            foreach (Level100HudDamageFlashSnapshot flash in hud.DamageFlashes)
            {
                float angle = flash.RelativeYawMicroRad / 1_000_000f;
                float fade = Math.Clamp(
                    flash.TicksRemaining /
                        (float)SimulationConstants.Level100DamageFlashLifetimeTicks,
                    0f,
                    1f);
                Vector2 position = center +
                    new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * CompassDamageRadius;
                DrawCenteredRotated(
                    assets.DamageFlash,
                    position,
                    new Vector2(128f, 32f),
                    angle,
                    // Retail keeps diffuse alpha opaque and fades RGB under
                    // the compass ONE/ONE pass.
                    new Color(fade, fade, fade, 1f));
            }
        }

        // bar-line is DXT1 - alpha 255 at every texel - so it stays additive
        // with the rest of the compass body sprites.
        //
        // Read off the device 2026-07-27 (in-level draw 1155, four bar-line
        // quads, ONE/ONE + MODULATE2X, identical in three captures):
        //
        //   r = 110.000 exactly, about (320, 240)   <- CompassGaugeNeedleRadius
        //   bearings 0, 59.063, 149.063, 225.000 degrees
        //   diffuse 0xFF1F1F1F on the health needle -> additive #3E3E3E
        //   diffuse 0xFF0F0F0F on the other three   -> additive #1E1E1E
        //
        // Two corrections follow. The ANCHOR is 149.063 deg, not 150: this
        // client's health track ran 150 - health*90 and its energy track
        // 225 + energy*135, which at the captured full health/full energy pose
        // gives 60/150/360/225 against retail's 59.063/149.063/0/225. Shifting
        // the health anchor by -0.937 deg reproduces all four bearings, and
        // confirms that the 225 and 360 ends were already exact.
        //
        // And the health needle is TWICE as bright as the other three, which
        // this file drew identically. #3E3E3E and #1E1E1E are the effective
        // additive colours; the pre-halved MODULATE2X diffuses are 0x1F and 0x0F.
        // local-lab/agent-notes-2026-07-27/inlevel-hud-coordinates.md section 5.
        private const float CompassGaugeHealthAnchorDegrees = 149.063f;
        private const float CompassGaugeEnergyAnchorDegrees = 225f;

        private void DrawGaugeNeedles(Vector2 center, float health, float energy)
        {
            DrawGaugeNeedle(
                center,
                Mathf.DegToRad(CompassGaugeHealthAnchorDegrees - (health * 90f)),
                RetailColor(0xff3e3e3eu));
            DrawGaugeNeedle(
                center,
                Mathf.DegToRad(CompassGaugeHealthAnchorDegrees),
                RetailColor(0xff1e1e1eu));
            DrawGaugeNeedle(
                center,
                Mathf.DegToRad(CompassGaugeEnergyAnchorDegrees + (energy * 135f)),
                RetailColor(0xff1e1e1eu));
            DrawGaugeNeedle(
                center,
                Mathf.DegToRad(CompassGaugeEnergyAnchorDegrees),
                RetailColor(0xff1e1e1eu));
        }

        private void DrawGaugeNeedle(Vector2 center, float angle, Color tint)
        {
            Vector2 position = center +
                new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * CompassGaugeNeedleRadius;
            DrawCenteredRotated(
                assets.BarLine,
                position,
                new Vector2(16f, 64f),
                angle,
                tint);
        }

        private void DrawDialNorthOverlay(
            WorldSnapshot snapshot,
            Vector2 center,
            float innerRadius,
            float outerRadius,
            Color color)
        {
            const int frameSize = 16;
            const int dialTextureWidth = 512;
            const int dialTextureHeight = 32;
            const int frameTop = 15;
            float heading = snapshot.FacingYawMicroRad / 1_000_000f;
            float angularStep = Mathf.Tau / dialTextureWidth;
            float radialWidth = (outerRadius - innerRadius) / dialTextureHeight;
            for (int y = 0; y < frameSize; y++)
            {
                float sourceV = frameTop + y + 0.5f;
                float radius = Mathf.Lerp(outerRadius, innerRadius, sourceV / dialTextureHeight);
                for (int x = 0; x < frameSize; x++)
                {
                    byte paletteIndex = assets.Dial[(y * frameSize) + x];
                    if (paletteIndex == 0)
                    {
                        continue;
                    }

                    float firstAngle = heading + ((x - (frameSize * 0.5f)) * angularStep);
                    float secondAngle = firstAngle + angularStep;
                    Color tint = color;
                    tint.A *= paletteIndex / 15f;
                    DrawLine(
                        center + new Vector2(Mathf.Sin(firstAngle), -Mathf.Cos(firstAngle)) * radius,
                        center + new Vector2(Mathf.Sin(secondAngle), -Mathf.Cos(secondAngle)) * radius,
                        tint,
                        radialWidth,
                        true);
                }
            }
        }

        // The message-noise pass that used to live here - additive, blue-tinted
        // (0.48, 0.66, 1, 0.16), unscrolled, on the ONE/ONE layer ABOVE the
        // instrument, on the instrument rect rather than the portrait rect - was
        // wrong on all of those counts and has moved to
        // RetailHudBaseLayer.DrawBattleLine. The device-level read of
        // 0x00487d10 CHud__RenderBattleline puts it between the six portrait
        // draws and the outline, under SRCALPHA/INVSRCALPHA with a neutral
        // 0x..ffffff diffuse; the outline is the only ONE/ONE draw in that pass.
        //
        // The outline's tint below is the outline quad's own diffuse DWORD,
        // 0xff6f8faf = (0.4353, 0.5608, 0.6863), confirmed at the device.
        private void DrawBattleLineOutline(
            WorldSnapshot snapshot,
            Level100HudSnapshot hud)
        {
            Rect2 rect = BattleLineInstrumentRect();
            float highlight = HighlightAlpha(snapshot, hud, Level100HudPart.BattleLine);
            DrawTextureRect(
                assets.BattleLineOutline,
                rect,
                false,
                new Color(0.44f + highlight, 0.56f + (highlight * 0.35f), 0.69f, 1f));
        }

    }

    private sealed partial class RetailHudTextLayer : RetailHudLayer
    {
        private const int FirstGlyph = 32;
        private const int GlyphColumns = 16;
        private const int SmallGlyphCellSize = 16;
        private const int LargeGlyphCellSize = 32;
        // Message panel metrics live in Level100MessagePanel, which is pure and
        // unit tested; the constant below only names the pitch this layer also
        // uses for the (unmeasured) help-prompt stack.
        private const float MessageLineHeight = Level100MessagePanel.LineHeightPixels;

        private readonly Texture2D _fontAtlas;
        private readonly int[] _glyphWidths;
        private readonly Texture2D _largeFontAtlas;
        private readonly int[] _largeGlyphWidths;
        private readonly Level100HudAssetCatalog _catalog;
        private string[] _messageWindow = [];
        private Level100HudHelpDefinition[] _activeHelp = [];
        private Level100HudWeaponSnapshot _weapon = Level100HudWeaponSnapshot.Unavailable;
        private Level100HudTerminalSnapshot _terminal = new(
            Visible: false,
            Level100MissionOutcome.Running,
            Level100MissionFailureReason.None,
            TicksRemaining: 0);
        private int _terminalDarkenerAlpha;

        public RetailHudTextLayer(
            Texture2D fontAtlas,
            Texture2D largeFontAtlas,
            Level100HudAssetCatalog catalog)
        {
            _fontAtlas = fontAtlas;
            _glyphWidths = MeasureGlyphWidths(fontAtlas.GetImage(), SmallGlyphCellSize);
            // font-13ps' space cell carries no ink, so its advance cannot be
            // measured off the atlas; it is measured off the screen instead.
            // Predicting the ink span of the twelve message lines rendered in
            // level100-gameplay t016011/t022080/t028057/t037063 with a space
            // advance of 8 lands within 2px on every line (total absolute error
            // 10px); 9 costs 28px and 7 costs 40px. The advance a glyph width
            // produces is width+1, so the space width is 7.
            _glyphWidths[0] = (SmallGlyphCellSize / 2) - 1;
            _largeFontAtlas = largeFontAtlas;
            _largeGlyphWidths = MeasureGlyphWidths(largeFontAtlas.GetImage(), LargeGlyphCellSize);
            _catalog = catalog;
        }

        public bool IsReady =>
            _fontAtlas.GetSize() == new Vector2I(256, 256) &&
            _glyphWidths.Length == 96 &&
            _largeFontAtlas.GetSize() == new Vector2I(512, 512) &&
            _largeGlyphWidths.Length == 96;

        public override void _Ready()
        {
            Material = CreateReleasedAlphaTestMaterial(additive: false);
        }

        public int DeliveredMessageCount { get; private set; }
        public int DeliveredHelpCount { get; private set; }

        public void SetState(
            Level100HudSnapshot hud,
            Level100HudMessageDefinition? message,
            Level100MessagePlaybackSnapshot playback)
        {
            if (message is null)
            {
                _messageWindow = [];
            }
            else
            {
                IReadOnlyList<Level100MessageLine> lines =
                    Level100MessagePanel.Wrap(message.Text);
                // Retail types the message on at 40 characters per second into a
                // three-line window that scrolls up one line at a time; see
                // Level100MessagePanel for the captures that measure both.
                // PositionSeconds is the only per-message clock this layer has,
                // and a message's type-on and its audio start together in every
                // captured pair, so it is the reveal clock. Without playback
                // there is no clock, so the settled (fully typed) window shows.
                int revealed = playback.IsAvailable
                    ? Level100MessagePanel.RevealedCharacters(playback.PositionSeconds)
                    : Level100MessagePanel.SourceLength(lines);
                _messageWindow = Level100MessagePanel.Window(lines, revealed).ToArray();
            }
            _activeHelp = hud.ActiveHelp
                .Select(_catalog.GetRequired)
                .ToArray();
            DeliveredMessageCount = hud.DeliveredMessages.Count;
            DeliveredHelpCount = hud.DeliveredHelp.Count;
            _weapon = hud.Weapon;
            bool enteringTerminal = hud.Terminal.Visible &&
                (!_terminal.Visible ||
                 _terminal.Outcome != hud.Terminal.Outcome ||
                 _terminal.FailureReason != hud.Terminal.FailureReason);
            _terminal = hud.Terminal;
            _terminalDarkenerAlpha = hud.Terminal.Visible
                ? Math.Min(0xa0, (enteringTerminal ? 0 : _terminalDarkenerAlpha) + 0x10)
                : 0;
            QueueRedraw();
        }

        public override void _Draw()
        {
            // The retail 640x480 frame renders font-13ps glyphs at exactly their
            // 16px atlas cell size ('T' measures 9x10 ink pixels on screen and
            // 9x10 in the atlas), so the released text path is a 1:1 blit on the
            // 640x480 stage. Drawing it in design space preserves that.
            BeginDesignSpace();
            DrawMessageWindow();
            DrawHelpPrompts();
            DrawWeaponAmmo();
            DrawTerminalOverlay();
            EndDesignSpace();
        }

        private void DrawTerminalOverlay()
        {
            if (!_terminal.Visible)
            {
                return;
            }

            DrawRect(
                new Rect2(0f, 0f, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0f, _terminalDarkenerAlpha / 255f));

            string title = _terminal.Outcome switch
            {
                Level100MissionOutcome.Won => _catalog.TerminalStrings.Victory,
                Level100MissionOutcome.Lost => _catalog.TerminalStrings.Defeat,
                _ => throw new InvalidDataException(
                    "A visible Level 100 terminal overlay has no terminal outcome."),
            };
            DrawTextLine(
                title,
                (DesignWidth - MeasureText(title, _largeGlyphWidths)) * 0.5f,
                50f,
                _largeFontAtlas,
                _largeGlyphWidths,
                LargeGlyphCellSize,
                drawShadow: true,
                clip: null);

            if (_terminal.Outcome != Level100MissionOutcome.Lost)
            {
                return;
            }

            float top = 90f;
            string reason = _catalog.TerminalStrings.GetFailureReason(
                _terminal.FailureReason);
            foreach (string line in WrapIntoLines(reason, 500f))
            {
                DrawTextLine(line, 65f, top);
                top += 16f;
            }
        }

        private void DrawMessageWindow()
        {
            if (_messageWindow.Length == 0)
            {
                return;
            }

            // Measured against the 640x480 gameplay captures, not fitted: the
            // three glyph-cell rows of
            // level100-gameplay/opening-pan-run1/level100-t016011ms.png start on
            // screen rows 412, 427 and 442 with their leading white ink at x 205,
            // and the drop shadow is offset (+1,+1) from the white glyph, so the
            // pen origin is (206, 413) with a 15px pitch. That block, cell rows
            // 412..458, is centred to the half pixel in the panel body this file
            // already pins at y 405.5..464.5. The d3d9 log's issued vertex
            // (203.5, 411.5) was tested against this and rejected; see
            // Level100MessagePanel.TextPenLeft.
            //
            // The clip is the panel body itself. No captured retail message
            // reaches it - the widest 25-column line in the released text table
            // ("Now make your way to the") ends at x 441 against a body that
            // ends at 496.5 - so it is a guard, not a measured behaviour.
            var clip = new Rect2(
                Level100MessagePanel.PanelBodyLeft,
                Level100MessagePanel.PanelBodyTop,
                Level100MessagePanel.PanelBodyRight - Level100MessagePanel.PanelBodyLeft,
                Level100MessagePanel.PanelBodyBottom - Level100MessagePanel.PanelBodyTop);
            for (int lineIndex = 0; lineIndex < _messageWindow.Length; lineIndex++)
            {
                DrawTextLine(
                    _messageWindow[lineIndex],
                    Level100MessagePanel.TextPenLeft,
                    Level100MessagePanel.FirstLinePenTop +
                        (lineIndex * Level100MessagePanel.LineHeightPixels),
                    clip);
            }
        }

        private void DrawHelpPrompts()
        {
            float y = 28f;
            foreach (Level100HudHelpDefinition prompt in _activeHelp)
            {
                string[] lines = WrapIntoLines(prompt.Text, 360f).Take(2).ToArray();
                foreach (string line in lines)
                {
                    float width = MeasureText(line);
                    DrawTextLine(line, (DesignWidth - width) * 0.5f, y);
                    y += MessageLineHeight;
                }
                y += 4f;
            }
        }

        private void DrawWeaponAmmo()
        {
            if (_weapon.SelectedWeapon != Level100HudWeapon.VulcanCannon ||
                !_weapon.VulcanCannonEnabled ||
                _weapon.VulcanAmmo is not int ammo)
            {
                return;
            }

            string text = ammo.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var bounds = new Rect2(9f, DesignHeight - 141f, 128f, LargeGlyphCellSize);
            float left = bounds.End.X - MeasureText(text, _largeGlyphWidths) - 8f;
            DrawTextLine(
                text,
                left,
                bounds.Position.Y,
                _largeFontAtlas,
                _largeGlyphWidths,
                LargeGlyphCellSize,
                drawShadow: false,
                clip: bounds);
        }

        private void DrawTextLine(string line, float left, float top, Rect2? clip = null)
        {
            DrawTextLine(
                line,
                left,
                top,
                _fontAtlas,
                _glyphWidths,
                SmallGlyphCellSize,
                drawShadow: true,
                clip: clip);
        }

        private void DrawTextLine(
            string line,
            float left,
            float top,
            Texture2D atlas,
            int[] glyphWidths,
            int cellSize,
            bool drawShadow,
            Rect2? clip)
        {
            float x = left;
            foreach (char character in line)
            {
                x += DrawGlyph(
                    character,
                    x,
                    top,
                    atlas,
                    glyphWidths,
                    cellSize,
                    drawShadow,
                    clip);
            }
        }

        private void DrawTextureRectRegionClipped(
            Texture2D atlas,
            Rect2 destination,
            Rect2 source,
            Color modulate,
            Rect2? clip)
        {
            if (clip is not Rect2 clipRect)
            {
                DrawTextureRectRegion(atlas, destination, source, modulate);
                return;
            }

            float left = Math.Max(destination.Position.X, clipRect.Position.X);
            float top = Math.Max(destination.Position.Y, clipRect.Position.Y);
            float right = Math.Min(destination.End.X, clipRect.End.X);
            float bottom = Math.Min(destination.End.Y, clipRect.End.Y);
            if (right <= left || bottom <= top)
            {
                return;
            }

            var clippedDestination = new Rect2(left, top, right - left, bottom - top);
            var clippedSource = new Rect2(
                source.Position + clippedDestination.Position - destination.Position,
                clippedDestination.Size);
            DrawTextureRectRegion(atlas, clippedDestination, clippedSource, modulate);
        }

        private int DrawGlyph(
            char character,
            float x,
            float y,
            Texture2D atlas,
            int[] glyphWidths,
            int cellSize,
            bool drawShadow,
            Rect2? clip)
        {
            int code = NormalizeGlyph(character);
            int glyph = code - FirstGlyph;
            int glyphWidth = glyphWidths[glyph];
            var source = new Rect2(
                (glyph % GlyphColumns) * cellSize,
                (glyph / GlyphColumns) * cellSize,
                glyphWidth,
                cellSize);
            if (drawShadow)
            {
                DrawTextureRectRegionClipped(
                    atlas,
                    new Rect2(x, y, glyphWidth, cellSize),
                    source,
                    Colors.Black,
                    clip);
            }
            DrawTextureRectRegionClipped(
                atlas,
                new Rect2(
                    drawShadow ? x - 1f : x,
                    drawShadow ? y - 1f : y,
                    glyphWidth,
                    cellSize),
                source,
                Colors.White,
                clip);
            return glyphWidth + 1;
        }

        private float MeasureText(string text)
        {
            return MeasureText(text, _glyphWidths);
        }

        private static float MeasureText(string text, int[] glyphWidths)
        {
            float width = 0f;
            foreach (char character in text)
            {
                width += glyphWidths[NormalizeGlyph(character) - FirstGlyph] + 1;
            }
            return width;
        }

        private static int NormalizeGlyph(char character) =>
            character is >= (char)FirstGlyph and < (char)(FirstGlyph + 96)
                ? character
                : '?';

        private static int[] MeasureGlyphWidths(Image image, int cellSize)
        {
            var widths = new int[96];
            widths[0] = cellSize / 2;
            for (int glyph = 1; glyph < widths.Length; glyph++)
            {
                int cellX = (glyph % GlyphColumns) * cellSize;
                int cellY = (glyph / GlyphColumns) * cellSize;
                int rightmost = cellX;
                for (int x = cellX + cellSize - 2; x >= cellX; x--)
                {
                    bool occupied = false;
                    for (int y = cellY; y < cellY + cellSize - 1; y++)
                    {
                        if (image.GetPixel(x, y).A > (16f / 255f))
                        {
                            occupied = true;
                            break;
                        }
                    }
                    if (occupied)
                    {
                        rightmost = x;
                        break;
                    }
                }
                widths[glyph] = (rightmost - cellX) + 2;
            }
            return widths;
        }

        // Pixel-width wrap, used only by the help-prompt stack. The message
        // panel does NOT use it: retail wraps messages by character column, not
        // by pixel width (Level100MessagePanel.WrapColumns). Whether the help
        // prompts wrap the same way is unmeasured - no capture in
        // local-lab/retail-reference-pristine/ shows a help prompt long enough
        // to break - so this path is left exactly as it was.
        private string[] WrapIntoLines(string text, float maximumWidth)
        {
            var lines = new List<string>();
            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            foreach (string paragraph in normalized.Split('\n'))
            {
                string current = string.Empty;
                int position = 0;
                while (position < paragraph.Length)
                {
                    int separatorStart = position;
                    while (position < paragraph.Length && char.IsWhiteSpace(paragraph[position]))
                    {
                        position++;
                    }
                    string separator = paragraph[separatorStart..position];

                    int wordStart = position;
                    while (position < paragraph.Length && !char.IsWhiteSpace(paragraph[position]))
                    {
                        position++;
                    }
                    if (wordStart == position)
                    {
                        break;
                    }

                    string word = paragraph[wordStart..position];
                    string candidate = current.Length == 0
                        ? word
                        : current + separator + word;
                    if (MeasureText(candidate) <= maximumWidth)
                    {
                        current = candidate;
                        continue;
                    }

                    if (current.Length > 0)
                    {
                        lines.Add(current);
                        current = string.Empty;
                    }

                    string remaining = word;
                    while (remaining.Length > 0 && MeasureText(remaining) > maximumWidth)
                    {
                        int split = 1;
                        while (split < remaining.Length &&
                               MeasureText(remaining[..(split + 1)]) <= maximumWidth)
                        {
                            split++;
                        }
                        lines.Add(remaining[..split]);
                        remaining = remaining[split..];
                    }
                    current = remaining;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }
                else if (paragraph.Length == 0)
                {
                    lines.Add(string.Empty);
                }
            }
            return lines.ToArray();
        }

    }
}
