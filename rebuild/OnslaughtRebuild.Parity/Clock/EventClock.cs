// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/eventmanager.cpp (CEventManager::AdvanceTime, 0044b600), in Stuart's release.

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Clock;

/// <summary>
/// The event manager's game clock: one CLOCK_TICK (0.05 s) per AdvanceTime. Only the time part of CEventManager lives
/// here; its event buffers belong to the event manager itself.
/// </summary>
public sealed class EventClock
{
    /// <summary>CLOCK_TICK (thing.h): 0.05f.</summary>
    public const float ClockTick = 0.05f;

    /// <summary>mFrameCount (+0x14): ticks since the level started.</summary>
    public int FrameCount { get; private set; }

    /// <summary>mTime (+0x08): game seconds, the EVT probe.</summary>
    public float Time { get; private set; }

    /// <summary>
    /// The tick: <c>mTime = ++mFrameCount * CLOCK_TICK</c>. The compiled code loads the count exactly (fild qword),
    /// multiplies by the float constant (fmul dword, rounded to 24 bits) and stores a float.
    /// </summary>
    public void AdvanceTime()
    {
        FrameCount++;
        Time = (float)X87.Mul(FrameCount, ClockTick);
    }

    /// <summary>The time part of CEventManager::Init (0044b060): mTime and mFrameCount back to zero.</summary>
    public void Init()
    {
        FrameCount = 0;
        Time = 0;
    }
}
