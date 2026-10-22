// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Guards the main-menu decoration-shadow law recovered on 2026-07-27 from
/// retail's own draw calls.
///
/// <para>The samples below are shadow-centre-minus-body-centre offsets read out
/// of <c><maintainer evidence>\bea-frontend-pages\A2-menu-reveal-20260727-204557\d3d9-draws.log</c>
/// with an independent parser, matching draws by texture dimensions and diffuse
/// rather than by index. <b>A</b> is the 256x256 left arc, <b>D</b> the 512x256
/// title logo. Frames 4000 and 4400 lie outside the range the original
/// measurement was reported over and are included precisely for that reason.</para>
///
/// <para>What this asserts is the LAW, not our pixels: that the measured offsets
/// lie on the ellipse <see cref="RetailFrontendDecorShadow"/> encodes, that the
/// left arc reuses the horizontal term on both axes, that the two pairs share
/// one driver, and that the constants the renderer used before this change are
/// that ellipse's time-mean. It says nothing about the phase, which is not
/// recoverable, and nothing about what Godot rasterizes.</para>
/// </summary>
public sealed class RetailFrontendDecorShadowTests
{
    /// <summary>frame, left-arc (x,y), title-logo (x,y) — measured.</summary>
    private static readonly (int Frame, double Ax, double Ay, double Dx, double Dy)[] Measured =
    [
        (2900, -0.6466, -0.6466, -0.6467, 8.9857),
        (3000, -0.9999, -0.9999, -1.0000, 10.0171),
        (3150, -0.1877, -0.1877, -0.1877, 11.5073),
        (3300, 2.0115, 2.0115, 2.0115, 12.6014),
        (3399, 3.9465, 3.9465, 3.9465, 12.9534),
        (4000, 10.5926, 10.5926, 10.5926, 8.9134),
        (4400, 3.8384, 3.8384, 3.8384, 7.0568),
    ];

    /// <summary>
    /// Every measured offset is reproduced by <see cref="RetailFrontendDecorShadow"/>
    /// at some phase, to well inside the log's own print resolution.
    /// </summary>
    [Fact]
    public void MeasuredOffsetsLieOnTheRecoveredEllipse()
    {
        var failures = new List<string>();
        foreach ((int frame, _, _, double dx, double dy) in Measured)
        {
            double phase = Math.Atan2(
                (dy - RetailFrontendDecorShadow.OffsetCenterY) / RetailFrontendDecorShadow.OffsetSemiAxisY,
                (dx - RetailFrontendDecorShadow.OffsetCenterX) / RetailFrontendDecorShadow.OffsetSemiAxisX);

            (double x, double y) = RetailFrontendDecorShadow.OffsetAtPhase(phase);
            if (Math.Abs(x - dx) > 1e-3 || Math.Abs(y - dy) > 1e-3)
            {
                failures.Add(
                    $"frame {frame}: measured ({dx:F4},{dy:F4}), law gives ({x:F4},{y:F4}).");
            }
        }

        Assert.True(
            failures.Count == 0,
            "The recovered ellipse no longer reproduces retail's measured shadow offsets:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The left arc takes <c>(u, u)</c>, not <c>(u, v)</c>, and its horizontal
    /// term is the SAME term the title logo uses. One animation drives both.
    /// </summary>
    [Fact]
    public void LeftArcReusesTheSharedHorizontalTermOnBothAxes()
    {
        foreach ((int frame, double ax, double ay, double dx, _) in Measured)
        {
            Assert.True(
                Math.Abs(ax - ay) < 1e-3,
                $"frame {frame}: left-arc offset ({ax:F4},{ay:F4}) is not diagonal.");
            Assert.True(
                Math.Abs(ax - dx) < 1e-3,
                $"frame {frame}: left arc x {ax:F4} and title logo x {dx:F4} are not the same driver.");
        }

        // And the implementation agrees with that reading at an arbitrary phase.
        const double phase = 0.7;
        (double sharedX, _) = RetailFrontendDecorShadow.OffsetAtPhase(phase);
        (double leftX, double leftY) = RetailFrontendDecorShadow.LeftArcOffsetAtPhase(phase);
        Assert.Equal(sharedX, leftX, 12);
        Assert.Equal(leftX, leftY, 12);
    }

    /// <summary>
    /// The offsets the renderer used before the animation was recovered —
    /// <c>(+5,+5)</c> for the left arc and <c>(+5,+10)</c> for the right arc, the
    /// row icon and the title logo — are the exact time-mean of the measured
    /// cycle.
    ///
    /// <para>This is the assertion that refutes
    /// <c>D3D9-FULL-SWEEP-2026-07-27.md</c> section 7 item 7's reading of those
    /// constants as "wrong-signed": they were the animation's mean, sampled
    /// against a single retail frame that sits at the cycle's extreme.</para>
    /// </summary>
    [Fact]
    public void TheReplacedStaticOffsetsAreTheCycleMean()
    {
        const int samples = 20_000;
        double sumX = 0d;
        double sumY = 0d;
        double sumLeftY = 0d;
        for (int i = 0; i < samples; i++)
        {
            double phase = 2d * Math.PI * i / samples;
            (double x, double y) = RetailFrontendDecorShadow.OffsetAtPhase(phase);
            sumX += x;
            sumY += y;
            sumLeftY += RetailFrontendDecorShadow.LeftArcOffsetAtPhase(phase).Y;
        }

        Assert.Equal(5d, sumX / samples, 6);
        Assert.Equal(10d, sumY / samples, 6);
        Assert.Equal(5d, sumLeftY / samples, 6);
    }

    /// <summary>
    /// The phase DECREASES with time and completes 1795.2 render frames' worth of
    /// cycle in the seconds those frames took at the measured rate. The frame
    /// period is the hard measurement; the conversion is the soft one, and this
    /// pins them together so neither can drift alone.
    /// </summary>
    [Fact]
    public void PhaseRunsBackwardAtTheMeasuredPeriod()
    {
        Assert.True(
            RetailFrontendDecorShadow.PhaseRadiansPerSecond < 0d,
            "Retail's measured phase step is negative on every sampled frame pair.");

        double periodSeconds =
            RetailFrontendDecorShadow.PeriodRenderFrames /
            RetailFrontendDecorShadow.MeasuredRenderFramesPerSecond;

        // 1795.2 frames at 141 fps is 12.73 s.
        Assert.Equal(12.73d, periodSeconds, 2);

        // One period returns the offset to where it started.
        (double x0, double y0) = RetailFrontendDecorShadow.OffsetAtPhase(
            RetailFrontendDecorShadow.PhaseAtSeconds(0d));
        (double x1, double y1) = RetailFrontendDecorShadow.OffsetAtPhase(
            RetailFrontendDecorShadow.PhaseAtSeconds(periodSeconds));
        Assert.Equal(x0, x1, 9);
        Assert.Equal(y0, y1, 9);
    }

    /// <summary>
    /// The shadow is a fixed 1.05 outset about the body centre. Measured
    /// invariant on every sampled frame — the shadow extent of the 320x320 left
    /// arc is 336.0000 x 336.0000 throughout, which is what refutes the sweep's
    /// "336 -> 328 breathing outset" reading.
    /// </summary>
    [Fact]
    public void ShadowScaleIsFixed()
    {
        Assert.Equal(1.05d, RetailFrontendDecorShadow.ScaleAboutBodyCentre, 10);
        Assert.Equal(336d, 320d * RetailFrontendDecorShadow.ScaleAboutBodyCentre, 6);
        Assert.Equal(537.6d, 512d * RetailFrontendDecorShadow.ScaleAboutBodyCentre, 6);
        Assert.Equal(268.8d, 256d * RetailFrontendDecorShadow.ScaleAboutBodyCentre, 6);
    }
}
