// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailCameraLaws"/> and
/// <see cref="RetailMovieCameraZoom"/> against
/// <c>references/Onslaught/Camera.cpp:552-672</c> and <c>:852-858</c>, and the
/// pristine <c>74154bfa…</c> bytes at <c>0x0041A210</c>, <c>0x0041A630</c> and
/// <c>0x0041B070</c>.
/// </summary>
public sealed class RetailCameraLawsTests
{
    // Pins CCamera::GetAspectRatio as a two-valued constant, bit for bit
    // (0x005D85EC = 0x3F000000, 0x005D8BC4 = 0x3F400000). Retail computes
    // nothing and reads no window, so a rebuild that derived this from a
    // resolution would be wrong even at 4:3 - and would fail the multiplayer
    // row, which is not any real viewport's aspect.
    [Theory]
    [InlineData(true, 0x3F000000u)]
    [InlineData(false, 0x3F400000u)]
    public void AspectRatio_IsAFixedPairNotAComputedRatio(
        bool multiplayer, uint expectedBits)
    {
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(RetailCameraLaws.AspectRatio(multiplayer)));
    }

    // Pins the constructor state at 0x0041A32D-0x0041A34C: mLastCalcZoomTime is
    // the sentinel -2.0f (0xC0000000) and both zoom slots start at 1.0f. The
    // sentinel is load-bearing: the cache key is compared for exact float
    // equality against EVENT_MANAGER's clock, which is frameCount * 0.05f and so
    // never negative, so a rebuild that started the key at 0.0f would return a
    // stale 1.0f on the very first frame.
    [Fact]
    public void MovieCameraZoom_StartsAtTheNegativeTwoSentinelWithUnitZoom()
    {
        var zoom = new RetailMovieCameraZoom();

        Assert.Equal(
            0xC0000000u, BitConverter.SingleToUInt32Bits(zoom.LastCalcZoomTime));
        Assert.Equal(
            0x3F800000u, BitConverter.SingleToUInt32Bits(zoom.LastCalcZoom));
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(zoom.OldZoom));

        // Time 0.0f is the first clock value retail can present, and it must
        // miss the cache.
        Assert.Equal(
            0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(0.0f, 90.0f)));
    }

    // THE divergence test. Camera.cpp:650 says (fov/90)/2.0f; 0x0041A681 says
    // fmul [0x005D9338] then fmul [0x005D85EC], i.e. fov * (1/90 as float) *
    // 0.5f, and 0x005D9338 is 0x3C360B61 - the rounded reciprocal, not 1/90. At
    // fov 65 the two differ in the last ulp: 0x3EB8E38F against 0x3EB8E38E. 41
    // of the 180 integer FOVs from 1 to 180 diverge. The 90 and 45 rows are
    // there so the test still covers the agreeing majority.
    //
    // NON-VACUITY: three of these four rows deliberately agree with the source
    // text, so only the 65.0 row can observe the divergence at all. The fourth
    // column declares which rows discriminate and the test proves that
    // classification at run time, and the sweep below proves the discriminating
    // population is exactly the 41 integer FOVs the comment claims - measured,
    // not asserted in prose. Delete the 65.0 row and the sweep still fails.
    [Theory]
    [InlineData(65.0f, 0x3EB8E38Fu, 0x3EB8E38Eu, true)]
    [InlineData(90.0f, 0x3F000000u, 0x3F000000u, false)]
    [InlineData(45.0f, 0x3E800000u, 0x3E800000u, false)]
    [InlineData(120.0f, 0x3F2AAAABu, 0x3F2AAAABu, false)]
    public void MovieCameraZoom_MultipliesByTheRoundedReciprocalOfNinety(
        float fieldOfView, uint retailBits, uint sourceTextBits, bool divergent)
    {
        var zoom = new RetailMovieCameraZoom();

        Assert.Equal(divergent, retailBits != sourceTextBits);
        Assert.Equal(
            0x3C360B61u,
            BitConverter.SingleToUInt32Bits(RetailCameraLaws.InverseReferenceFieldOfView));
        Assert.Equal(
            retailBits,
            BitConverter.SingleToUInt32Bits(zoom.GetZoom(1.0f, fieldOfView)));
        Assert.Equal(
            sourceTextBits,
            BitConverter.SingleToUInt32Bits(fieldOfView / 90.0f / 2.0f));

        // The exercised population, counted rather than claimed: over the 180
        // integer fields of view the reciprocal multiply and the source-text
        // divide disagree on exactly 41, and this row's own FOV is on the side
        // the fourth column says it is.
        int divergentFieldsOfView = 0;
        for (int fov = 1; fov <= 180; fov++)
        {
            uint shipped = BitConverter.SingleToUInt32Bits(
                new RetailMovieCameraZoom().GetZoom(1.0f, fov));
            uint sourceText = BitConverter.SingleToUInt32Bits(fov / 90.0f / 2.0f);
            if (shipped != sourceText)
            {
                divergentFieldsOfView++;
            }
        }

        Assert.Equal(41, divergentFieldsOfView);
    }

    // Pins the memo itself (Camera.cpp:631-635 / 0x0041A636-0x0041A651): the
    // FIRST call in an event frame recomputes and shadows the previous result
    // into mOldZoom; every later call in the same frame returns the cache and
    // shadows nothing. So mOldZoom lags by exactly one frame no matter how many
    // times the frame asks. An implementation that shadowed on every call, or
    // that recomputed on every call, fails: the third assertion block would see
    // mOldZoom equal to the current zoom instead of the previous frame's.
    [Fact]
    public void MovieCameraZoom_ShadowsOncePerFrameNoMatterHowOftenItIsAsked()
    {
        var zoom = new RetailMovieCameraZoom();

        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(0.05f, 90.0f)));
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(zoom.OldZoom));

        // Same frame, different FOV: the cache wins and nothing shadows.
        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(0.05f, 45.0f)));
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(zoom.OldZoom));
        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.LastCalcZoom));

        // New frame: recompute, and the previous frame's 0.5f shadows.
        Assert.Equal(0x3E800000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(0.1f, 45.0f)));
        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.OldZoom));

        // And asking twice more in the new frame still leaves mOldZoom alone.
        zoom.GetZoom(0.1f, 120.0f);
        zoom.GetZoom(0.1f, 120.0f);
        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.OldZoom));
        Assert.Equal(0x3E800000u, BitConverter.SingleToUInt32Bits(zoom.LastCalcZoom));
    }

    // Pins the two no-render-thing arms (0x0041A69E and 0x0041A6B9): both write
    // 1.0f into the cache AND stamp the time, so the frame is still consumed and
    // a later call in that frame does not retry. A rebuild that returned 1.0f
    // without stamping would recompute - and re-shadow - on the second call.
    [Fact]
    public void MovieCameraZoom_MissingRenderThingCachesUnityAndStampsTheFrame()
    {
        var zoom = new RetailMovieCameraZoom();
        zoom.GetZoom(0.05f, 120.0f);

        Assert.Equal(
            0x3F800000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(0.1f, null)));
        Assert.Equal(0.1f, zoom.LastCalcZoomTime);
        Assert.Equal(0x3F2AAAABu, BitConverter.SingleToUInt32Bits(zoom.OldZoom));

        zoom.GetZoom(0.1f, 90.0f);
        Assert.Equal(0x3F2AAAABu, BitConverter.SingleToUInt32Bits(zoom.OldZoom));
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(zoom.LastCalcZoom));
    }

    // Ties the memo key to the clock this repository already models: the event
    // manager's mTime is the only thing retail compares against, so a zoom memo
    // driven by anything else - a render frame, a wall clock - would re-shadow at
    // a different rate. Two GetZoom calls inside one scheduler frame must
    // collapse to one recompute.
    [Fact]
    public void MovieCameraZoom_KeysOnTheEventManagerClockNotTheRenderRate()
    {
        var scheduler = new RetailEventScheduler();
        var zoom = new RetailMovieCameraZoom();

        scheduler.AdvanceTime();
        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(scheduler.Time, 90.0f)));
        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(scheduler.Time, 45.0f)));
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(zoom.OldZoom));

        scheduler.AdvanceTime();
        Assert.Equal(0x3E800000u, BitConverter.SingleToUInt32Bits(zoom.GetZoom(scheduler.Time, 45.0f)));
        Assert.Equal(0x3F000000u, BitConverter.SingleToUInt32Bits(zoom.OldZoom));
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(scheduler.Time),
            BitConverter.SingleToUInt32Bits(zoom.LastCalcZoomTime));
    }
}
