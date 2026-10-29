// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Exact numbering from the released <c>data\MissionScripts\onsldef.msl</c>
/// lines 66-68, which the developers' own source <c>#include</c>s as a header
/// (<c>Career.cpp:11</c>, <c>game.cpp:46</c>):
/// <code>
/// #define FRIENDLY_ALLIGENCE   0
/// #define ENEMY_ALLIGENCE      1
/// #define NEUTRAL_ALLEGIANCE   2
/// </code>
/// (the released spelling of "allegiance" is inconsistent between the three;
/// that is theirs, not a transcription slip.)
///
/// <para>This enum was previously <c>Neutral = 0, Friendly = 1, Enemy = 2</c> -
/// a PERMUTATION, with every released value occupied by the wrong name and no
/// comment saying so, while the sibling enums in this same file all cite the
/// release. It read as released and was not.</para>
///
/// <para>It was latent rather than broken: the contact list is currently always
/// empty, and every use site names the member rather than its value, so the
/// renumbering changes no behaviour today. It was a TRAP, not a bug. Core
/// already stores allegiance in released numbering - <c>SetAllegiance</c> keeps
/// <c>command.Scalar</c> verbatim at
/// <c>Level100ActorMechanics.cs:207</c> - so the first commit to join those two
/// same-named <c>int</c>s would have painted every enemy friendly-blue at
/// <c>FirstFlightHud.cs:592</c> and picked the friendly classification texture
/// at <c>:1365</c>. The compiler cannot catch a permutation of ints.</para>
/// </summary>
public enum Level100HudAllegiance
{
    Friendly = 0,
    Enemy = 1,
    Neutral = 2,
}

public enum Level100HudContactSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
    RepairPad = 3,
}

public enum Level100HudWeapon
{
    None = 0,
    PulseCannon = 1,
    VulcanCannon = 2,
}

public enum Level100HudWeaponSelectionSlot
{
    None = 0,
    Side = 1,
    Front = 2,
    Top = 3,
}

public enum Level100HudHelpPrompt
{
    // Exact signed text IDs from the released text.stf/native CText table.
    Fire = 1_197_607,
    ZoomIn = 8_268_984,
    ZoomOut = 17_186_000,
    Transform = 31_505_972,
    RetroThrusters = 2_302_408,
    WeaponSelect = 488_286_858,
}

public enum Level100HudSpeaker
{
    // Exact PlayCharMessage speaker arguments from the released text table.
    Kramer = 919_601,
    Tatiana = 1_508_464,
    Technician = 10_565_784,
}

public enum Level100HudPart
{
    // Exact HighlightHudPart arguments from the released onsldef.msl.
    Health = 0,
    Energy = 1,
    Compass = 2,
    BattleLine = 3,
    Radar = 4,
    CurrentWeapon = 5,
}

public sealed record Level100HudWeaponSnapshot(
    Level100HudWeapon? SelectedWeapon,
    bool PulseCannonEnabled,
    bool VulcanCannonEnabled,
    bool? SelectionPanelVisible,
    Level100HudWeaponSelectionSlot? SelectionSlot,
    int? PulseHeatPermille,
    int? VulcanAmmo,
    int? ChargePermille,
    bool? PulseCannonOverheated)
{
    public static Level100HudWeaponSnapshot Unavailable { get; } = new(
        SelectedWeapon: null,
        PulseCannonEnabled: false,
        VulcanCannonEnabled: false,
        SelectionPanelVisible: null,
        SelectionSlot: null,
        PulseHeatPermille: null,
        VulcanAmmo: null,
        ChargePermille: null,
        PulseCannonOverheated: null);
}

public sealed record Level100HudContactSnapshot(
    int Id,
    SimVector2 Position,
    SimVector2 Velocity,
    Level100HudAllegiance Allegiance,
    Level100HudContactSize Size,
    bool IsObjective,
    bool OnScanner);

/// <summary>
/// One contact placed on the lower-left scanner: a design-space offset from the
/// scanner centre and the 0-255 alpha the released fade produced.
/// </summary>
public readonly record struct Level100ScannerPlacement(
    float OffsetX,
    float OffsetY,
    int Alpha,
    bool Drawn,
    bool Clamped);

/// <summary>
/// The released tactical-scanner projection, recovered from the SHIPPED BYTES.
/// The HUD has no source in the pinned GPL drop (rebuild/PROVENANCE.md places
/// <c>Hud.h</c>/<c>DXHud.h</c>/<c>PCHud.h</c> in the "recover from shipped
/// bytes" partition), so every constant below is a decoded .rdata float or a
/// decoded instruction immediate, cited by address.
///
/// <para>Owner: <c>CHud__RenderTacticalRadarContacts</c> @ <c>0x00484c50</c>,
/// called once from <c>CHud__RenderOverlayForViewpoint</c>. Its sole call site
/// at <c>0x00487bab</c> is a push of constant 0x42c00000 (<b>96.0f</b>) before
/// a copy of esi into ecx, then call - the function is a return that pops 4 bytes of arguments, so that float is
/// its one stack argument. Markers reach the screen through
/// <c>HudOverlay__DrawSpriteQuad</c> @ <c>0x004857e0</c>, a fixed forwarder to
/// <c>CVBufTexture__DrawSpriteEx</c> @ <c>0x00555be0</c> with anchor mode
/// <b>4</b> - the case that subtracts half the texture width AND half its
/// height - so (x, y) is the sprite CENTRE at 1:1 texel scale.</para>
///
/// <para>Decoded constants (read from the .rdata of the verified safe copy,
/// image base 0x00400000, .rdata VA 0x005d8000 / file 0x1d8000):</para>
/// <list type="table">
/// <item><term><c>0x005d8610</c></term><description>40.0f - scale numerator</description></item>
/// <item><term>call-site imm</term><description>96.0f - scale denominator</description></item>
/// <item><term><c>0x005dbe6c</c></term><description>46.0f - contact clamp radius, in PIXELS</description></item>
/// <item><term><c>0x005dbe70</c></term><description>8464.0f = 92^2 - cull radius squared, in PIXELS</description></item>
/// <item><term><c>0x005dbe68</c></term><description>0.021739131f = 1/46 - alpha fade per pixel past the rim</description></item>
/// <item><term><c>0x005d8c70</c></term><description>255.0f - alpha quantiser</description></item>
/// <item><term><c>0x005dbb70</c></term><description>69.0f - scanner centre x</description></item>
/// <item><term><c>0x005dbe74</c></term><description>44.0f - scanner centre y, up from the viewport bottom</description></item>
/// <item><term><c>0x005d857c</c></term><description>20.0f - second centre-y term</description></item>
/// <item><term><c>0x005d8568</c></term><description>1.0f - the -1 x / +1 y trim, and the clamp test</description></item>
/// </list>
///
/// <para>Those three radius constants are mutually consistent, which is the
/// cross-check that they were read correctly: the fade is
/// <c>1 - (r - 46)/46</c>, so it reaches exactly zero at r = 92, and the cull
/// test is exactly <c>r^2 &lt; 92^2</c>. Contacts past the rim are pinned TO the
/// rim and fade out over the next 46 pixels rather than vanishing.</para>
///
/// <para>VALIDATED against retail, not just read: with the released Level 100
/// start pose (288.6875, 243.25) yaw 0.509829998, this law places all eleven
/// authored allegiance-0 base-world objects on retail frame
/// <c>hud-timeline-run1/level100-t025065ms.png</c> to within 0.2 px of the
/// measured blue-blob centroids, and the pixel under each predicted centre is
/// literally <c>(80, 80, 174)</c> - the decoded friendly base tint. See the
/// tint constants below.</para>
/// </summary>
public static class Level100ScannerProjection
{
    /// <summary>0x005d8610 = 40.0f.</summary>
    public const float ScaleNumerator = 40f;

    /// <summary>Stack argument at the sole call site 0x00487bab: a push of constant 0x42c00000 = 96.0f.</summary>
    public const float ScaleDenominator = 96f;

    /// <summary>World units to scanner pixels: 40/96.</summary>
    public const float PixelsPerWorldUnit = ScaleNumerator / ScaleDenominator;

    /// <summary>0x005dbe6c = 46.0f, in PIXELS.</summary>
    public const float ClampRadiusPixels = 46f;

    /// <summary>0x005dbe70 = 8464.0f = 92^2, in PIXELS.</summary>
    public const float CullRadiusSquaredPixels = 8_464f;

    /// <summary>0x005dbe68 = 0.021739131f = 1/46.</summary>
    public const float FadePerPixel = 0.021739131f;

    /// <summary>0x005d8c70 = 255.0f.</summary>
    public const float AlphaQuantiser = 255f;

    /// <summary>
    /// 0x005dbb70 (69.0f) plus the two viewport-origin globals, which are
    /// zero for the single-player full-window viewport, minus 0x005d8568
    /// (1.0f).
    /// </summary>
    public const float CentreX = 69f - 1f;

    /// <summary>
    /// Viewport bottom (480 on the released 640x480 stage) minus 0x005dbe74
    /// (44.0f) minus 0x005d857c (20.0f) plus 0x005d8568 (1.0f). The
    /// 480-44-20 = 416 agrees with the 55-point circle fit of retail's
    /// radar-outline ring at y = 417.25 in the 640x480 frame.
    /// </summary>
    public const float CentreY = 480f - 44f - 20f + 1f;

    // Packed 0x00RRGGBB tint bases, read straight out of the three draw loops.
    // Each is OR-ed with alpha<<24 (0xFF while inside the rim). The "+" value is
    // added when unit flag 0x400 at unit+0x34 is set; Core does not currently
    // model that flag, so the client draws the base tint only.
    //
    // Friendly base 0x5050AF is CONFIRMED at the pixel level: retail's scanner
    // blobs at t025065 are literally (80, 80, 174) = 0x5050AE/AF.

    /// <summary>Allegiance 0 (FRIENDLY_ALLIGENCE), base at 0x00484d9f-ish loop: 0x5050AF.</summary>
    public const int FriendlyTintRgb = 0x5050AF;

    /// <summary>Friendly brightened by flag 0x400: +0x404050 -&gt; 0x9090FF.</summary>
    public const int FriendlyHighlightAdd = 0x404050;

    /// <summary>Allegiance 1 (ENEMY_ALLIGENCE): 0xAF0808.</summary>
    public const int EnemyTintRgb = 0xAF0808;

    /// <summary>Enemy brightened by flag 0x400: +0x504848 -&gt; 0xFF5050.</summary>
    public const int EnemyHighlightAdd = 0x504848;

    /// <summary>Every other allegiance (NEUTRAL_ALLEGIANCE = 2 and up): 0x606060.</summary>
    public const int NeutralTintRgb = 0x606060;

    /// <summary>Neutral brightened by flag 0x400: +0x101010 -&gt; 0x707070.</summary>
    public const int NeutralHighlightAdd = 0x101010;

    /// <summary>
    /// The objective marker's colour: a push of constant 0xFFFFFF00, a hard immediate at
    /// <c>0x00485424</c>, i.e. opaque yellow. Retail draws it from the global
    /// objective list at <c>0x00855140</c> (`0x00485372`), inside the same
    /// scanner function and against the same scanner centre, AFTER the
    /// allegiance buckets. CONFIRMED at the pixel level: retail
    /// <c>hud-timeline-run1/level100-t041063ms.png</c> and
    /// <c>t042062ms.png</c> each carry 13 pixels of <c>(254, 254, 0)</c> in
    /// Medium's 5x5 diamond inside the scanner disc.
    /// </summary>
    public const int ObjectiveTintRgb = 0xFFFF00;

    public static int TintRgb(Level100HudAllegiance allegiance) => allegiance switch
    {
        Level100HudAllegiance.Friendly => FriendlyTintRgb,
        Level100HudAllegiance.Enemy => EnemyTintRgb,
        _ => NeutralTintRgb,
    };

    /// <summary>
    /// Places one contact. <paramref name="deltaX"/> / <paramref name="deltaZ"/>
    /// are the contact minus the player in RETAIL UNITS on Core's horizontal
    /// axes (X, Z), and <paramref name="yawRadians"/> is the player's facing.
    ///
    /// <para>Retail computes <c>sin(-yaw) * 40 / 96</c> and
    /// <c>cos(-yaw) * 40 / 96</c> once, then
    /// <c>rx = dx*cosT - dy*sinT</c>, <c>ry = dx*sinT + dy*cosT</c> - a rotation
    /// by MINUS the player yaw with the world-to-pixel scale folded in. The
    /// returned Y offset is already screen-down, i.e. <c>-ry</c>.</para>
    /// </summary>
    public static Level100ScannerPlacement Place(
        float deltaX,
        float deltaZ,
        float yawRadians)
    {
        float sin = MathF.Sin(-yawRadians) * PixelsPerWorldUnit;
        float cos = MathF.Cos(-yawRadians) * PixelsPerWorldUnit;
        float rx = (deltaX * cos) - (deltaZ * sin);
        float ry = (deltaX * sin) + (deltaZ * cos);

        float radiusSquared = (rx * rx) + (ry * ry);
        if (!(radiusSquared < CullRadiusSquaredPixels))
        {
            return new Level100ScannerPlacement(0f, 0f, 0, Drawn: false, Clamped: false);
        }

        float radius = MathF.Sqrt(radiusSquared);
        int alpha = 255;
        bool clamped = false;
        // Retail's test is `46/r < 1.0f`, which is false at r == 0 (division by
        // zero yields +inf), so a contact standing on the player is unclamped
        // and fully opaque.
        float scale = ClampRadiusPixels / radius;
        if (scale < 1f)
        {
            clamped = true;
            rx *= scale;
            ry *= scale;
            float fade = 1f - ((radius - ClampRadiusPixels) * FadePerPixel);
            alpha = (int)MathF.Round(fade * AlphaQuantiser, MidpointRounding.ToEven);
            alpha = Math.Clamp(alpha, 0, 255);
        }

        return new Level100ScannerPlacement(rx, -ry, alpha, Drawn: true, clamped);
    }

    /// <summary>
    /// Places one OBJECTIVE. Retail's objective loop (<c>0x00485372</c>) shares
    /// the rotation and the r = 46 clamp with <see cref="Place"/>, but it
    /// differs in two ways that are visible on screen and are read straight off
    /// the instruction stream:
    ///
    /// <para>It has NO cull test - there is no float compare against the value at 0x005dbe70 anywhere
    /// between <c>0x004853c1</c> and the draw at <c>0x00485462</c>, where the
    /// three allegiance loops all have one. An objective at any range is drawn,
    /// pinned to the rim.</para>
    ///
    /// <para>It has NO alpha fade - the colour is the hard immediate
    /// (a push of constant 0xFFFFFF00) at <c>0x00485424</c>, not an
    /// <c>alpha &lt;&lt; 24</c> composed from the rim distance. Alpha is always
    /// 255.</para>
    ///
    /// <para>The clamp itself is identical: <c>fsqrt</c>,
    /// a reversed float divide by the value at 0x005dbe6c, compare against <c>[0x005d8568]</c>, and scale
    /// both components when 46/r &lt; 1 (<c>0x004853f3</c>-<c>0x00485410</c>).</para>
    /// </summary>
    public static Level100ScannerPlacement PlaceObjective(
        float deltaX,
        float deltaZ,
        float yawRadians)
    {
        float sin = MathF.Sin(-yawRadians) * PixelsPerWorldUnit;
        float cos = MathF.Cos(-yawRadians) * PixelsPerWorldUnit;
        float rx = (deltaX * cos) - (deltaZ * sin);
        float ry = (deltaX * sin) + (deltaZ * cos);

        float radius = MathF.Sqrt((rx * rx) + (ry * ry));
        bool clamped = false;
        float scale = ClampRadiusPixels / radius;
        if (scale < 1f)
        {
            clamped = true;
            rx *= scale;
            ry *= scale;
        }

        return new Level100ScannerPlacement(rx, -ry, 255, Drawn: true, clamped);
    }

    /// <summary>
    /// The same placement expressed in released design-space pixels, i.e. with
    /// the recovered scanner centre added.
    /// </summary>
    public static Level100ScannerPlacement PlaceInDesignSpace(
        float deltaX,
        float deltaZ,
        float yawRadians)
    {
        Level100ScannerPlacement placement = Place(deltaX, deltaZ, yawRadians);
        return placement.Drawn
            ? placement with
            {
                OffsetX = CentreX + placement.OffsetX,
                OffsetY = CentreY + placement.OffsetY,
            }
            : placement;
    }
}

public sealed record Level100HudThreatSnapshot(
    int RelativeYawMicroRad,
    int TicksRemaining);

public sealed record Level100HudDamageFlashSnapshot(
    int RelativeYawMicroRad,
    int TicksRemaining);

public sealed record Level100HudTargetSnapshot(
    int ContactId,
    int HullPermille,
    SimVector2 PredictedPosition,
    int LockPermille);

public sealed record Level100HudObjectiveSnapshot(
    Level100ActorId ActorId,
    string ThingName,
    SimVector3 PositionMillimeters);

public sealed record Level100HudMessageDeliverySnapshot(
    int Tick,
    Level100HudSpeaker Speaker,
    int MessageId,
    bool ScriptWaitsForDuration,
    int ExpectedPlaybackTicks);

/// <summary>
/// Whether <c>CInfluenceMapManager</c> holds any influence maps. Three-valued
/// on purpose: <see cref="Unknown"/> is not <see cref="Empty"/>.
/// </summary>
/// <remarks>
/// <c>CInfluenceMapManager__IsEmpty</c> (<c>0x0048c2d0</c>) is
/// <c>return *(int *)(this + 0x14) &lt; 1</c> - a list count. Level 100's authored
/// BSWD nodes and the overlay seen in 20 pinned frames establish
/// <see cref="Populated"/> for this level. <see cref="Unknown"/> remains distinct
/// from <see cref="Empty"/> for callers without that level-specific evidence.
/// </remarks>
public enum Level100HudInfluenceMapState
{
    Unknown = 0,
    Empty = 1,
    Populated = 2,
}

/// <summary>
/// What occupies the lower-right HUD socket.
/// </summary>
public enum Level100HudLowerRightSocket
{
    /// <summary>Nothing beyond the darkener - this reconstruction cannot say.</summary>
    Indeterminate = 0,

    /// <summary>The speaker portrait and the message-noise page over it.</summary>
    PortraitAndNoise = 1,

    /// <summary>The battleline / influence overlay.</summary>
    InfluenceOverlay = 2,

    /// <summary>The single <c>hud\ForsetiIcon.tga</c> sprite.</summary>
    ForsetiIcon = 3,
}

/// <summary>
/// The recovered two-factor state table for the lower-right socket.
/// </summary>
/// <remarks>
/// <para>
/// <c>CHud__RenderBattleline</c> (<c>0x00487d10</c>) branches on
/// <c>CInfluenceMapManager__IsEmpty</c>: the non-empty arm calls
/// <c>CDXBattleLine__PopulateBattleLineAndInfluenceOverlayVertices</c> then
/// <c>CDXBattleLine__Render</c>; the empty arm draws the single sprite at
/// <c>[hud+0x1d4]</c> - filled by <c>CHud__LoadTextures</c> from the string at
/// <c>0x0062ceb0</c>, <c>hud\ForsetiIcon.tga</c> - but only when
/// <c>*(int *)(CMessageBox + 8) == 0</c>.
/// </para>
/// <para>
/// The second factor is the one both earlier single-factor readings missed.
/// Inside <c>CDXBattleLine__Render</c> (<c>0x0053abe0</c>) EVERY draw is
/// separately guarded by the same <c>*(int *)(0x008a9d84 + 8) == 0</c> test:
/// the three <c>CEngine__DrawIndexedPrimitives</c> calls, the
/// <c>CDXBattleLine__RenderTriOverlayPass</c> call, and the BattleEngine marker
/// sprite loop. So the overlay is invoked after the portrait pass but paints
/// nothing while a message is active. "The battleline draws last, over the
/// portrait" is false and must not be reinstated.
/// </para>
/// <para>
/// The resulting table, which is what this class implements:
/// </para>
/// <code>
/// active message | influence list | visible content
/// ---------------|----------------|-------------------------------------
/// yes            | either         | portrait/noise; battleline SUPPRESSED
/// no             | non-empty      | battleline / influence overlay
/// no             | empty          | Forseti icon
/// </code>
/// </remarks>
public static class Level100HudLowerRightSocketLaw
{
    public static Level100HudLowerRightSocket Select(
        bool messageBoxHoldsActiveMessage,
        Level100HudInfluenceMapState influenceMap) =>
        messageBoxHoldsActiveMessage
            ? Level100HudLowerRightSocket.PortraitAndNoise
            : influenceMap switch
            {
                Level100HudInfluenceMapState.Populated =>
                    Level100HudLowerRightSocket.InfluenceOverlay,
                Level100HudInfluenceMapState.Empty =>
                    Level100HudLowerRightSocket.ForsetiIcon,
                _ => Level100HudLowerRightSocket.Indeterminate,
            };
}

public sealed record Level100HudBattleLineSnapshot(
    bool HasInfluenceValues,
    IReadOnlyList<short> InfluencePermille,
    Level100HudInfluenceMapState InfluenceMap)
{
    /// <summary>
    /// No influence magnitudes and no answer about whether a released manager
    /// holds any maps. Level 100's projection no longer uses this fallback.
    /// </summary>
    /// <remarks>
    /// Reporting <see cref="Level100HudInfluenceMapState.Empty"/> here would be
    /// a measured falsehood for Level 100. Classifying the socket disc across
    /// all 183 pinned retail frames in
    /// a private retail reference capture finds the
    /// influence overlay on 20 of them - all 17 of
    /// <c>settled-timeline-run3</c>, plus <c>hud-timeline-run1/t025065</c> and
    /// <c>t011756</c> in BOTH opening-pan runs, which puts the overlay on
    /// screen 11.8 s into the level - and
    /// the ForsetiIcon page on ZERO - template correlation against the decoded
    /// 64x64 page peaks at 0.29 anywhere in the lower-right quadrant on every
    /// frame, where an injected control scores 0.79 additive / 0.94 alpha at
    /// exactly the byte-predicted origin (536, 320). Level 100's manager is
    /// therefore non-empty for at least part of the level and its Forseti arm
    /// is never taken; a client that reported <c>Empty</c> would draw an icon
    /// retail never shows.
    /// </remarks>
    public static Level100HudBattleLineSnapshot Unavailable { get; } = new(
        HasInfluenceValues: false,
        Array.AsReadOnly(Array.Empty<short>()),
        Level100HudInfluenceMapState.Unknown);
}

public sealed record Level100HudSnapshot(
    Level100HudWeaponSnapshot Weapon,
    IReadOnlyList<Level100HudContactSnapshot> Contacts,
    IReadOnlyList<Level100HudObjectiveSnapshot> Objectives,
    IReadOnlyList<Level100HudThreatSnapshot> Threats,
    IReadOnlyList<Level100HudDamageFlashSnapshot> DamageFlashes,
    Level100HudTargetSnapshot? Target,
    Level100HudMessageDeliverySnapshot? ActiveMessage,
    IReadOnlyList<Level100HudPart> EmphasizedParts,
    IReadOnlyList<Level100HudMessageDeliverySnapshot> DeliveredMessages,
    IReadOnlyList<Level100HudHelpPrompt> ActiveHelp,
    IReadOnlyList<Level100HudHelpPrompt> DeliveredHelp,
    Level100HudBattleLineSnapshot BattleLine,
    Level100HudTerminalSnapshot Terminal);

public sealed record Level100HudTerminalSnapshot(
    bool Visible,
    Level100MissionOutcome Outcome,
    Level100MissionFailureReason FailureReason,
    int TicksRemaining);

public sealed record Level100HudInfluenceNode(
    int Id,
    SimVector2 Position,
    int RadiusMillimeters);

public readonly record struct Level100HudInfluenceLink(int FirstNodeId, int SecondNodeId);

public static class Level100HudInfluenceMap
{
    // Exact version-1 influence nodes and links embedded in Level 100's BSWD.
    // Positions are translated from the released player-one origin in the same
    // millimetre coordinate system used by the rest of deterministic Core.
    private static readonly ReadOnlyCollection<Level100HudInfluenceNode> s_nodes =
        Array.AsReadOnly(new[]
        {
            new Level100HudInfluenceNode(0, new SimVector2(-3_688, 83_750), 10_000),
            new Level100HudInfluenceNode(1, new SimVector2(-30_688, 59_750), 10_000),
            new Level100HudInfluenceNode(2, new SimVector2(-70_688, 51_750), 10_000),
            new Level100HudInfluenceNode(3, new SimVector2(-95_688, -42_250), 10_000),
            new Level100HudInfluenceNode(4, new SimVector2(-99_688, -3_250), 10_000),
            new Level100HudInfluenceNode(5, new SimVector2(-109_688, 30_750), 10_000),
            new Level100HudInfluenceNode(6, new SimVector2(-66_688, 16_750), 10_000),
            new Level100HudInfluenceNode(7, new SimVector2(-77_688, 84_750), 10_000),
            new Level100HudInfluenceNode(8, new SimVector2(-38_688, 94_750), 10_000),
            new Level100HudInfluenceNode(9, new SimVector2(33_313, 69_750), 10_000),
            new Level100HudInfluenceNode(10, new SimVector2(-688, 48_750), 10_000),
            new Level100HudInfluenceNode(11, new SimVector2(33_813, 21_250), 10_000),
            new Level100HudInfluenceNode(12, new SimVector2(-13_188, 12_250), 10_000),
        });

    private static readonly ReadOnlyCollection<Level100HudInfluenceLink> s_links =
        Array.AsReadOnly(new[]
        {
            new Level100HudInfluenceLink(0, 9),
            new Level100HudInfluenceLink(0, 10),
            new Level100HudInfluenceLink(0, 1),
            new Level100HudInfluenceLink(10, 1),
            new Level100HudInfluenceLink(1, 8),
            new Level100HudInfluenceLink(1, 7),
            new Level100HudInfluenceLink(1, 2),
            new Level100HudInfluenceLink(2, 5),
            new Level100HudInfluenceLink(2, 6),
            new Level100HudInfluenceLink(12, 2),
            new Level100HudInfluenceLink(2, 7),
            new Level100HudInfluenceLink(10, 9),
            new Level100HudInfluenceLink(10, 11),
            new Level100HudInfluenceLink(12, 11),
            new Level100HudInfluenceLink(11, 9),
            new Level100HudInfluenceLink(10, 12),
            new Level100HudInfluenceLink(6, 12),
            new Level100HudInfluenceLink(8, 7),
            new Level100HudInfluenceLink(6, 5),
            new Level100HudInfluenceLink(6, 4),
            new Level100HudInfluenceLink(5, 4),
            new Level100HudInfluenceLink(4, 3),
        });

    public static IReadOnlyList<Level100HudInfluenceNode> Nodes => s_nodes;

    public static IReadOnlyList<Level100HudInfluenceLink> Links => s_links;
}

/// <summary>
/// Presentation-only projection of canonical mission events, actor state, and
/// audio playback. It never feeds simulation or mission progression.
/// </summary>
public sealed class Level100HudPresentationState
{
    private readonly List<Level100HudMessageDeliverySnapshot> _deliveredMessages = [];
    private readonly List<Level100HudHelpPrompt> _deliveredHelp = [];
    private readonly IReadOnlyDictionary<string, int> _authoredAllegiance;

    // The mission tick at the last projection, used ONLY to notice a restart.
    // int.MinValue so the very first projection is never mistaken for one.
    private int _lastProjectedMissionTick = int.MinValue;

    public Level100HudPresentationState()
        : this(null)
    {
    }

    /// <summary>
    /// <paramref name="authoredAllegianceByDefinitionIdentity"/> is the WRES
    /// <c>allegiance</c> int32 the released base-world records carry, keyed by
    /// the same <c>wres:bswd:NNNN</c> identity Core's actor definitions use. It
    /// is presentation input only: retail reads allegiance off the live unit at
    /// <c>unit+0x138</c>, and the scanner's whole colour partition
    /// (<c>0x5050AF</c> / <c>0xAF0808</c> / <c>0x606060</c>) is that one field.
    /// Core's own <c>Level100ActorMechanics</c> allegiance - the released
    /// <c>SetAllegiance</c> script command - takes precedence wherever it
    /// exists, because that is the field retail would have overwritten.
    /// </summary>
    public Level100HudPresentationState(
        IReadOnlyDictionary<string, int>? authoredAllegianceByDefinitionIdentity)
    {
        _authoredAllegiance = authoredAllegianceByDefinitionIdentity ??
            new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public void Consume(IReadOnlyList<Level100MissionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (Level100MissionEvent missionEvent in events)
        {
            switch (missionEvent)
            {
                case Level100MessageRequested message:
                    _deliveredMessages.Add(new Level100HudMessageDeliverySnapshot(
                        message.Tick,
                        ParseSpeaker(message.SpeakerId),
                        message.MessageId,
                        message.ScriptWaitsForDuration,
                        message.ExpectedPlaybackTicks));
                    break;
                case Level100HelpRequested help:
                {
                    var prompt = (Level100HudHelpPrompt)help.HelpMessageId;
                    if (!Enum.IsDefined(prompt))
                    {
                        throw new InvalidDataException(
                            $"Released Level 100 help ID {help.HelpMessageId} is unsupported.");
                    }
                    _deliveredHelp.Add(prompt);
                    break;
                }
            }
        }
    }

    public Level100HudSnapshot Project(
        WorldSnapshot snapshot,
        Level100MessagePlaybackState playback)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Level100ActorSnapshot[] objectiveActors = snapshot.Level100Actors.Actors
            .Where(actor =>
                actor.Active &&
                actor.IsObjective &&
                actor.Lifecycle != Level100ActorLifecycle.Destroyed)
            .ToArray();
        Level100HudObjectiveSnapshot[] objectives = objectiveActors
            .Select(actor => new Level100HudObjectiveSnapshot(
                actor.ActorId,
                actor.Name,
                actor.Pose.PositionMillimeters))
            .ToArray();

        Level100MissionSnapshot mission = snapshot.Level100Mission;

        // A RESTART CLEARS THE DELIVERY LOG. `SimActions.Reset` restarts the
        // released mission and its tick returns to the opening, but this state
        // is presentation-side and kept accumulating across it: every message
        // and help prompt from the previous run stayed in the log, so after a
        // reset `ActiveAt` resolved against deliveries that had not happened
        // yet in the run now on screen.
        //
        // The tick going BACKWARDS is the signal, and it is derived from
        // simulation state rather than from a new event, so Core needs no change
        // and no reset notification can be missed. A mission tick never
        // decreases within a run.
        if (mission.Tick < _lastProjectedMissionTick)
        {
            _deliveredMessages.Clear();
            _deliveredHelp.Clear();
        }

        _lastProjectedMissionTick = mission.Tick;
        // The active message is resolved from Core's OWN tick, not from the
        // audio mixer. `playback` is retained on the signature because the
        // audio adapter is still the sole playback owner and callers pass it,
        // but it is deliberately NOT consulted here: see the header of
        // Level100MessageSchedule for the 21-25 % cross-run self-noise this
        // caused and the measurement that identified it.
        _ = playback;
        Level100HudMessageDeliverySnapshot? activeDelivery =
            Level100MessageSchedule
                .ActiveAt(_deliveredMessages, mission.Tick)
                ?.Delivery;
        bool pulseEnabled =
            mission.PulseCannonAvailability == Level100MissionWeaponAvailability.Enabled;
        bool vulcanEnabled =
            mission.TwinVulcanAvailability == Level100MissionWeaponAvailability.Enabled ||
            mission.MechVulcanAvailability == Level100MissionWeaponAvailability.Enabled;
        Level100MissionWeapon selectedMissionWeapon = snapshot.Mode switch
        {
            VehicleMode.Walker => snapshot.Level100WalkerSelectedWeapon,
            VehicleMode.Jet => snapshot.Level100JetSelectedWeapon,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot.Mode)),
        };
        Level100HudWeapon? selectedWeapon = selectedMissionWeapon switch
        {
            Level100MissionWeapon.PulseCannonPod => Level100HudWeapon.PulseCannon,
            Level100MissionWeapon.MechTwinVulcanCannon or
                Level100MissionWeapon.MechVulcanCannon => Level100HudWeapon.VulcanCannon,
            // The retained HUD set has no independently identified Missile Pod icon.
            Level100MissionWeapon.MissilePod => null,
            _ => null,
        };
        var weapon = new Level100HudWeaponSnapshot(
            SelectedWeapon: selectedWeapon,
            pulseEnabled,
            vulcanEnabled,
            SelectionPanelVisible: null,
            SelectionSlot: null,
            PulseHeatPermille: null,
            VulcanAmmo: null,
            ChargePermille: null,
            PulseCannonOverheated: null);

        Level100HudPart[] emphasizedParts = Enum.GetValues<Level100HudPart>()
            .Where(part => (snapshot.Level100HudEmphasisMask & (1 << (int)part)) != 0)
            .ToArray();
        int terminalOverlayTicksRemaining =
            mission.Outcome == Level100MissionOutcome.Lost
                ? Level100MissionTiming.FailureOverlayTicksRemaining(
                    mission.FailureReason,
                    mission.TerminalTicksRemaining)
                : mission.TerminalTicksRemaining;
        var terminal = new Level100HudTerminalSnapshot(
            Visible: mission.Outcome != Level100MissionOutcome.Running &&
                terminalOverlayTicksRemaining > 0,
            mission.Outcome,
            mission.FailureReason,
            terminalOverlayTicksRemaining);

        return new Level100HudSnapshot(
            weapon,
            Array.AsReadOnly(ProjectContacts(snapshot)),
            Array.AsReadOnly(objectives),
            Array.AsReadOnly(Array.Empty<Level100HudThreatSnapshot>()),
            Array.AsReadOnly(ProjectDamageFlashes(snapshot)),
            Target: null,
            activeDelivery,
            Array.AsReadOnly(emphasizedParts),
            Array.AsReadOnly(_deliveredMessages.ToArray()),
            Array.AsReadOnly(Array.Empty<Level100HudHelpPrompt>()),
            Array.AsReadOnly(_deliveredHelp.ToArray()),
            ProjectBattleLine(snapshot),
            terminal);
    }

    private static Level100HudDamageFlashSnapshot[] ProjectDamageFlashes(
        WorldSnapshot snapshot) => snapshot.Level100DamageFlashes
            .Select(flash => new Level100HudDamageFlashSnapshot(
                flash.RelativeYawMicroRad,
                SimulationConstants.Level100DamageFlashLifetimeTicks -
                    (snapshot.Tick - flash.StartTick)))
            .Where(flash => flash.TicksRemaining > 0)
            .ToArray();

    /// <summary>
    /// Presentation-only Level 100 influence estimate over the exact authored
    /// BSWD nodes. Retail's target law at <c>0x0048c3b0</c> is the signed
    /// friendly-minus-enemy ratio; equal contribution by each nearby live actor
    /// is deliberately provisional until the per-unit accumulator is recovered.
    /// </summary>
    private Level100HudBattleLineSnapshot ProjectBattleLine(WorldSnapshot snapshot)
    {
        Dictionary<int, int> commandedAllegiance = snapshot.Level100ActorMechanics.Actors
            .Where(actor => actor.HasAllegianceOverride)
            .GroupBy(actor => actor.ActorId.Value)
            .ToDictionary(group => group.Key, group => group.Last().Allegiance);
        Level100ActorSnapshot[] actors = snapshot.Level100Actors.Actors
            .Where(actor =>
                actor.Active &&
                actor.Lifecycle != Level100ActorLifecycle.Destroyed &&
                !actor.Trigger.HasValue)
            .ToArray();

        IReadOnlyList<Level100HudInfluenceNode> nodes = Level100HudInfluenceMap.Nodes;
        int[] friendlyByNode = new int[nodes.Count];
        int[] enemyByNode = new int[nodes.Count];
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            Level100HudInfluenceNode node = nodes[nodeIndex];
            long radiusSquared = (long)node.RadiusMillimeters * node.RadiusMillimeters;
            foreach (Level100ActorSnapshot actor in actors)
            {
                SimVector3 position = actor.Pose.PositionMillimeters;
                long deltaX = (long)position.X - node.Position.X;
                long deltaZ = (long)position.Z - node.Position.Z;
                if ((deltaX * deltaX) + (deltaZ * deltaZ) > radiusSquared)
                {
                    continue;
                }

                switch (ResolveAllegiance(actor, commandedAllegiance))
                {
                    case Level100HudAllegiance.Friendly:
                        friendlyByNode[nodeIndex]++;
                        break;
                    case Level100HudAllegiance.Enemy:
                        enemyByNode[nodeIndex]++;
                        break;
                }
            }
        }

        short[] influence = nodes
            .Select((node, nodeIndex) =>
            {
                int friendly = friendlyByNode[nodeIndex];
                int enemy = enemyByNode[nodeIndex];
                int total = friendly + enemy;
                if (total > 0)
                {
                    return (short)(((friendly - enemy) * 1_000) / total);
                }

                int neighborFriendly = 0;
                int neighborEnemy = 0;
                foreach (Level100HudInfluenceLink link in Level100HudInfluenceMap.Links)
                {
                    int neighborIndex = link.FirstNodeId == node.Id
                        ? link.SecondNodeId
                        : link.SecondNodeId == node.Id
                            ? link.FirstNodeId
                            : -1;
                    if (neighborIndex >= 0)
                    {
                        neighborFriendly += friendlyByNode[neighborIndex];
                        neighborEnemy += enemyByNode[neighborIndex];
                    }
                }

                // Retail's zero-local-total arm chooses enemy only for a strict
                // neighboring enemy majority; ties and no-neighbor influence
                // resolve friendly. Smoothing toward this target remains open.
                return (short)(neighborEnemy > neighborFriendly ? -1_000 : 1_000);
            })
            .ToArray();

        return new Level100HudBattleLineSnapshot(
            HasInfluenceValues: true,
            Array.AsReadOnly(influence),
            Level100HudInfluenceMapState.Populated);
    }

    /// <summary>
    /// Live scanner contacts, from the canonical actor registry. Retail walks
    /// its global unit list at <c>0x008550d0</c> inside
    /// <c>CHud__RenderTacticalRadarContacts</c> (<c>0x00484c50</c>), partitions
    /// it by the allegiance field at <c>unit+0x138</c>, and draws each surviving
    /// entry through the projection in <see cref="Level100ScannerProjection"/>.
    ///
    /// <para>What this reproduces: the membership test that an actor is active
    /// and not destroyed, the released allegiance partition, and the exact
    /// projection/clamp/fade. What it does NOT reproduce, because Core does not
    /// model the fields: retail's four extra gates - the <c>+0x1a4</c> vfunc
    /// predicate, <c>CThing</c> flag <c>0x4</c> at <c>+0x2c</c>, unit flag
    /// <c>0x8</c> at <c>+0x34</c>, and the neutral bucket's additional
    /// <c>+0x68</c> vfunc / <c>0x10</c> flag / non-null <c>+0x30</c> tests. The
    /// player's own Battle Engine and the trigger volumes are excluded here
    /// because neither is a member of that unit list.</para>
    /// </summary>
    private Level100HudContactSnapshot[] ProjectContacts(WorldSnapshot snapshot)
    {
        Dictionary<int, int> commandedAllegiance = snapshot.Level100ActorMechanics.Actors
            .Where(actor => actor.HasAllegianceOverride)
            .GroupBy(actor => actor.ActorId.Value)
            .ToDictionary(group => group.Key, group => group.Last().Allegiance);

        float yaw = snapshot.FacingYawMicroRad / 1_000_000f;
        var contacts = new List<Level100HudContactSnapshot>();
        foreach (Level100ActorSnapshot actor in snapshot.Level100Actors.Actors)
        {
            if (!actor.Active ||
                actor.Lifecycle == Level100ActorLifecycle.Destroyed ||
                actor.Trigger.HasValue ||
                StringComparer.Ordinal.Equals(actor.Name, "Player 1"))
            {
                continue;
            }

            SimVector3 position = actor.Pose.PositionMillimeters;
            float deltaX = (position.X - snapshot.PlayerPosition.X) / 1_000f;
            float deltaZ = (position.Z - snapshot.PlayerPosition.Z) / 1_000f;
            Level100ScannerPlacement placement =
                Level100ScannerProjection.Place(deltaX, deltaZ, yaw);

            Level100HudAllegiance allegiance = ResolveAllegiance(
                actor,
                commandedAllegiance);

            contacts.Add(new Level100HudContactSnapshot(
                actor.ActorId.Value,
                new SimVector2(position.X, position.Z),
                new SimVector2(
                    actor.Pose.LinearVelocityMillimetersPerTick.X,
                    actor.Pose.LinearVelocityMillimetersPerTick.Z),
                allegiance,
                // Medium is retail's DEFAULT blob, not a guess.
                // CHud__LoadTextures (0x00481650) binds
                // hud\ScannerBlobSmall/Medium/Large/RepairPad to CHud slots
                // +0x1a0/+0x1a4/+0x1a8/+0x1ac in that order, and
                // CHud__SelectMarkerTextureIndexByUnitFlags (0x00485830)
                // returns +0x1a4 - Medium - for the unremarkable unit: the one
                // with none of flags 0x8000000, 0x4000, 0x4008100 or 0x40 set
                // at unit+0x34. Small and Large need flags Core does not model,
                // so every contact takes the default. Corroborated on the retail
                // frame: every scanner blob on t025065 has ScannerBlobMedium's
                // 5x5 ink footprint, not Small's 4x4 or Large's 8x8.
                Level100HudContactSize.Medium,
                actor.IsObjective,
                placement.Drawn));
        }

        return [.. contacts];
    }

    private Level100HudAllegiance ResolveAllegiance(
        Level100ActorSnapshot actor,
        IReadOnlyDictionary<int, int> commandedAllegiance)
    {
        int allegiance = commandedAllegiance.TryGetValue(actor.ActorId.Value, out int commanded)
            ? commanded
            : _authoredAllegiance.TryGetValue(actor.DefinitionIdentity, out int authored)
                ? authored
                : (int)Level100HudAllegiance.Neutral;
        return Enum.IsDefined((Level100HudAllegiance)allegiance)
            ? (Level100HudAllegiance)allegiance
            : Level100HudAllegiance.Neutral;
    }

    private static Level100HudSpeaker ParseSpeaker(int speakerId)
    {
        var speaker = (Level100HudSpeaker)speakerId;
        return Enum.IsDefined(speaker)
            ? speaker
            : throw new InvalidDataException(
                $"Released Level 100 speaker ID {speakerId} is unsupported.");
    }
}
