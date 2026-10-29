// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client;

/// <summary>
/// The released main-menu decoration-shadow law, recovered from retail's own
/// draw calls on 2026-07-27.
///
/// <para><b>What was measured.</b> Source log
/// <c><maintainer evidence>\bea-frontend-pages\A2-menu-reveal-20260727-204557\d3d9-draws.log</c>
/// (the <c>-skipfmv</c> main menu, 4,499 frames, captured through the passive
/// d3d9 proxy). Four shadow/body pairs were extracted on every settled frame by
/// texture identity and diffuse, never by draw index:</para>
///
/// <list type="bullet">
/// <item><b>A</b> left arc, 256x256 DXT2, shadow <c>0x3E000000</c> / body
/// <c>0xFE7F7F7F</c>, body <c>(59,184)-(379,504)</c>.</item>
/// <item><b>B</b> right arc, 128x128 DXT2, body <c>(377,275)-(537,435)</c>.</item>
/// <item><b>C</b> selected-row icon, 128x128 DXT2, body
/// <c>(393,291)-(521,419)</c>.</item>
/// <item><b>D</b> title logo, 512x256 DXT2, shadow <c>0x3E000000</c> / body
/// <c>0xFE57677F</c>, body <c>(64,2)-(576,258)</c>.</item>
/// </list>
///
/// <para><b>The law.</b> On every settled frame and every pair:</para>
///
/// <code>
///   shadow = body scaled by exactly 1.05 about the BODY CENTRE,
///            then translated by (u,u) for A and (u,v) for B, C and D
///   u = 5 + 6 cos(theta)      v = 10 + 3 sin(theta)
/// </code>
///
/// <para><b>It is one animation, not four.</b> <c>u</c> is shared bit-for-bit by
/// all four pairs and the left arc's y-offset <i>is</i> its own <c>u</c>. The
/// bodies never move: every body edge of all four pairs has span 0.0000 px over
/// the settled range.</para>
///
/// <para><b>Verified in the main loop, not taken on report.</b> Frames 2900,
/// 3000, 3150, 3300, 3399, <b>4000 and 4400</b> — the last two outside the range
/// the measurement was reported over — were re-extracted with an independent
/// parser. Shadow/body size ratio is <c>1.050000</c> on every one, the shadow
/// extent is <c>336.0000 x 336.0000</c> for A on every one (it does <b>not</b>
/// change size), <c>A.u == D.u</c> to 1e-4, and the ellipse identity
/// <c>((u-5)/6)^2 + ((v-10)/3)^2 = 1</c> holds to 3e-5 including at f4000
/// (u=+10.5926, v=+8.9134) and f4400 (u=+3.8384, v=+7.0568).</para>
///
/// <para><b>The clock is dt-driven, and that is measured rather than assumed.</b>
/// A frame-counted animation advances the phase by a constant every frame. Over
/// frames 3000-3200 the measured per-frame phase step has mean -0.0034714 rad
/// with sd 0.0000338 (0.97 %) and a 5.0 % spread — it jitters with the real
/// frame time, so the driver is elapsed time, not a frame counter. So this class
/// exposes a phase in SECONDS.</para>
///
/// <para><b>The rate, and the one soft number in it.</b> A least-squares fit of
/// unwrapped phase over 3,756 settled frames gives -0.0035001 rad/frame, i.e. a
/// period of <b>1795.2 render frames</b> (residual rms 0.09 % of a cycle) —
/// that part is hard. Converting to seconds uses the frame rate the same
/// instrument measured on the same page under the same logging load, 141 fps
/// (2,820 frames in 20.0 s, run <c>A6-fps</c>), giving 0.4935 rad/s and a
/// 12.73 s period. <b>That conversion inherits the proxy's timing load</b>: the
/// proxy is measured not to be timing-neutral, so unlogged retail may run faster
/// and, if the animation were frame-counted after all, the true period in
/// seconds would be shorter. The frame period is the measurement; the seconds
/// rate is the measurement divided by another measurement.</para>
///
/// <para><b>The absolute phase is NOT recoverable and is not invented.</b> Retail's
/// phase is per-launch and the sweep records that frame indices are not
/// reproducible across runs, so no starting angle can be attributed to the
/// released build. <see cref="PhaseAtSeconds"/> simply starts at 0. Any phase is
/// as faithful as any other; a specific one would be a fabricated constant.</para>
///
/// <para><b>What this corrects, and what it does not.</b> The reconstruction
/// already drew these shadows at <c>(+5,+5)</c> and <c>(+5,+10)</c> from the
/// body, and already scaled them by 1.05. Those offsets are <b>exactly the
/// centre of the measured ellipse</b> — the animation's time-mean — so the
/// previous code was retail's law evaluated at its mean, not a wrong-signed
/// constant. What was missing is only the +-6 / +-3 px oscillation. This
/// specifically refutes the reading in
/// the private lab note D3D9-FULL-SWEEP-2026-07-27.md section 7 item 7, which
/// compared our constants against a single frame (A2 f3000) that happens to sit
/// 1.5 frames from the exact minimum of <c>u</c>.</para>
/// </summary>
public static class RetailFrontendDecorShadow
{
    /// <summary>
    /// Uniform scale of the shadow quad about the BODY centre. Exactly 1.050000
    /// on every sampled frame and every pair; the shadow never changes size.
    /// Corroborated by the shipped-byte constant already cited in the renderer
    /// (<c>0x005db4ac</c>).
    /// </summary>
    public const double ScaleAboutBodyCentre = 1.05;

    /// <summary>Ellipse centre — and the exact time-mean of the offset.</summary>
    public const double OffsetCenterX = 5.0;

    /// <inheritdoc cref="OffsetCenterX"/>
    public const double OffsetCenterY = 10.0;

    /// <summary>Ellipse semi-axes. Ratio 2:1, no tilt.</summary>
    public const double OffsetSemiAxisX = 6.0;

    /// <inheritdoc cref="OffsetSemiAxisX"/>
    public const double OffsetSemiAxisY = 3.0;

    /// <summary>
    /// Measured period of the offset cycle in RENDER FRAMES. This is the hard
    /// number: least squares over 3,756 settled frames, residual rms 0.09 %.
    /// </summary>
    public const double PeriodRenderFrames = 1795.2;

    /// <summary>
    /// Frame rate the same instrument measured on the same settled page under
    /// the same logging load, used only to convert
    /// <see cref="PeriodRenderFrames"/> into seconds.
    /// </summary>
    public const double MeasuredRenderFramesPerSecond = 141.0;

    /// <summary>
    /// Phase rate in radians per second: 2*pi / (1795.2 / 141). The sign is the
    /// measured one — the phase DECREASES with time.
    /// </summary>
    public const double PhaseRadiansPerSecond =
        -2.0 * Math.PI * MeasuredRenderFramesPerSecond / PeriodRenderFrames;

    /// <summary>Phase at <paramref name="seconds"/> after an arbitrary origin.</summary>
    public static double PhaseAtSeconds(double seconds) => PhaseRadiansPerSecond * seconds;

    /// <summary>
    /// The shared offset vector (u, v). The right arc, the selected-row icon and
    /// the title logo all take this verbatim.
    /// </summary>
    public static (double X, double Y) OffsetAtPhase(double phase) =>
        (OffsetCenterX + (OffsetSemiAxisX * Math.Cos(phase)),
         OffsetCenterY + (OffsetSemiAxisY * Math.Sin(phase)));

    /// <summary>
    /// The left arc's offset. It is <c>(u, u)</c> — it reuses the shared
    /// horizontal term on BOTH axes rather than taking <c>v</c>, which is why its
    /// shadow reads as a pure diagonal while the other three do not.
    /// </summary>
    public static (double X, double Y) LeftArcOffsetAtPhase(double phase)
    {
        double u = OffsetCenterX + (OffsetSemiAxisX * Math.Cos(phase));
        return (u, u);
    }
}
