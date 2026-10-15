// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Two released camera laws that are pure functions of state the simulation
/// already owns: the viewport aspect ratio, and the movie camera's
/// once-per-event-frame zoom memo.
/// </summary>
/// <remarks>
/// Owner in the pinned drop: <c>references/Onslaught/Camera.cpp</c> and
/// <c>Camera.h</c>. Neither law here renders anything; the field of view is an
/// input, supplied by whatever produced it.
/// </remarks>
public static class RetailCameraLaws
{
    /// <summary>Multiplayer aspect ratio — <c>0x005D85EC</c>.</summary>
    public const float MultiplayerAspectRatio = 0.5f;

    /// <summary>Single-player aspect ratio — <c>0x005D8BC4</c>.</summary>
    public const float SinglePlayerAspectRatio = 0.75f;

    /// <summary>
    /// Retail's reciprocal for the 90-degree reference FOV —
    /// <c>0x005D9338</c>, which is <c>0x3C360B61</c>, the correctly-rounded
    /// float nearest 1/90. <b>Not</b> 1/90.
    /// </summary>
    public const float InverseReferenceFieldOfView = 1.0f / 90.0f;

    /// <summary>Zoom returned when there is no thing or no render thing — <c>0x005D8568</c>.</summary>
    public const float DefaultZoom = 1.0f;

    /// <summary>
    /// <c>CCamera::GetAspectRatio</c> — <c>Camera.cpp:852-858</c>,
    /// <c>0x0041B070</c>.
    /// </summary>
    /// <remarks>
    /// Source and retail agree exactly. The compiled body is a call to
    /// <c>CGame::IsMultiplayer</c> on the <c>GAME</c> singleton at
    /// <c>0x008A9A98</c> and a two-way <c>fld</c> of the constants at
    /// <c>0x005D85EC</c> (<c>0x3F000000</c>) and <c>0x005D8BC4</c>
    /// (<c>0x3F400000</c>) — bit-exact <c>0.5f</c> and <c>0.75f</c>, with no
    /// arithmetic and no dependence on the actual window. It is a fixed pair,
    /// not a computed ratio, so a rebuild that derives it from a resolution is
    /// wrong even when the number happens to match.
    /// </remarks>
    public static float AspectRatio(bool multiplayer) =>
        multiplayer ? MultiplayerAspectRatio : SinglePlayerAspectRatio;
}

/// <summary>
/// <c>CMovieCamera</c>'s zoom memo — <c>Camera.cpp:629-672</c>,
/// <c>0x0041A630</c> (<c>GetZoom</c>) and <c>0x0041A6E0</c>
/// (<c>GetOldZoom</c>), constructed as <c>Camera.cpp:552-563</c>,
/// <c>0x0041A210</c>.
/// </summary>
/// <remarks>
/// <para>
/// The behaviour is a one-slot cache keyed on <b>float equality</b> with
/// <c>EVENT_MANAGER.GetTime()</c>: the first call in an event frame recomputes
/// and shadows the previous result into <c>mOldZoom</c>; every later call in
/// the same frame returns the cache and does <b>not</b> shadow again. Because
/// the key is compared with <c>fcomp</c> against the live <c>mTime</c> at
/// <c>0x00672FD0</c>, the sentinel the constructor plants has to be a value the
/// clock never takes: <c>0x0041A32D</c> stores <c>0xC0000000</c> to
/// <c>+0x50</c>, i.e. <c>-2.0f</c>, alongside <c>1.0f</c> into both
/// <c>mLastCalcZoom</c> (<c>+0x54</c>) and <c>mOldZoom</c> (<c>+0x98</c>).
/// Retail's clock only ever takes non-negative values
/// (<c>mFrameCount * CLOCK_TICK</c>), so the first call always misses.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE in the arithmetic, and retail wins.</b>
/// <c>Camera.cpp:650</c> is <c>mLastCalcZoom = (fov/90)/2.0f</c>. The shipped
/// body divides by nothing: <c>fmul dword ptr [0x005D9338]</c> then
/// <c>fmul dword ptr [0x005D85EC]</c>, i.e. <c>fov * (1/90 as float) * 0.5f</c>.
/// The second factor is exact (a power of two) but the first is not, so
/// multiplying by the rounded reciprocal is a different function from dividing
/// by 90 for a large share of inputs. This type multiplies.
/// </para>
/// <para>
/// <b>One nuance is deliberately not claimed.</b> Retail caches with
/// <c>fst</c>, not <c>fstp</c>: the value written to <c>mLastCalcZoom</c> is
/// rounded to float, but the value left in <c>st(0)</c> and returned to the
/// caller is the unrounded x87 result. So retail's first call in a frame can
/// hand back marginally more precision than the second call does. This type
/// returns the float-rounded value in both cases, because every consumer found
/// so far (<c>CInterpolatedCamera</c>, <c>Camera.cpp:836-839</c>) assigns into
/// a float. If a consumer is ever found that keeps the value in a register
/// across arithmetic, that consumer — not this type — is where the difference
/// would have to be modelled.
/// </para>
/// <para>
/// <b>Not established here.</b> Where the field of view comes from:
/// <c>CRenderThing::GetMovieCameraPosition</c> is a render virtual
/// (<c>vtable +0x44</c>) and stays outside Core. The absent-thing and
/// absent-render-thing arms are modelled as a null <c>fieldOfView</c>, which
/// retail reaches at <c>0x0041A6B9</c> and <c>0x0041A69E</c> respectively;
/// both write the cache and both return <c>1.0f</c>, so Core cannot tell them
/// apart and does not pretend to.
/// </para>
/// </remarks>
public sealed class RetailMovieCameraZoom
{
    private float _lastCalcZoomTime = -2.0f;
    private float _lastCalcZoom = RetailCameraLaws.DefaultZoom;
    private float _oldZoom = RetailCameraLaws.DefaultZoom;

    /// <summary><c>mLastCalcZoomTime</c>; <c>-2.0f</c> until the first sample.</summary>
    public float LastCalcZoomTime => _lastCalcZoomTime;

    /// <summary><c>mLastCalcZoom</c>.</summary>
    public float LastCalcZoom => _lastCalcZoom;

    /// <summary><c>CMovieCamera::GetOldZoom</c> — <c>Camera.cpp:669-672</c>, <c>0x0041A6E0</c>.</summary>
    public float OldZoom => _oldZoom;

    /// <summary>
    /// <c>CMovieCamera::GetZoom</c> — <c>Camera.cpp:629-666</c>,
    /// <c>0x0041A630</c>.
    /// </summary>
    /// <param name="eventManagerTime">
    /// <c>EVENT_MANAGER.GetTime()</c>. Compared for exact float equality, as
    /// retail does.
    /// </param>
    /// <param name="fieldOfView">
    /// The render thing's FOV in degrees, or <c>null</c> for retail's two
    /// no-render-thing arms.
    /// </param>
    public float GetZoom(float eventManagerTime, float? fieldOfView)
    {
        if (_lastCalcZoomTime == eventManagerTime)
        {
            return _lastCalcZoom;
        }

        _oldZoom = _lastCalcZoom;

        float zoom = fieldOfView is float fov
            ? (float)((double)fov *
                (double)RetailCameraLaws.InverseReferenceFieldOfView * 0.5)
            : RetailCameraLaws.DefaultZoom;

        _lastCalcZoom = zoom;
        _lastCalcZoomTime = eventManagerTime;
        return zoom;
    }
}
