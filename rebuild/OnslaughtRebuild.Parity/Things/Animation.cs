// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/animation.cpp and src/animation.h (CAnimation: AnimModeLoops 004048f0, the constructor
// 004046d0, HandleEvent 00404750, Process 00404790, SetAnimMode 00404860, GetRenderFrame 004048c0) and src/thing.cpp
// (CComplexThing::SetAnimMode 004f44a0, GetRenderFrame 004f45c0, FinishedPlayingCurrentAnimation 004f45a0;
// CThing::GetRenderThingFrameIncrement 004f3c80). Reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CAnimation: a complex thing's animation clock. Each frame it steps the frame (0..1) by the mode's rate, looping the
/// looping modes; a mode that ends tells the thing and falls back to standing.
/// </summary>
public sealed class Animation : Monitor
{
    /// <summary>ANIMATION_EVENT_PROCESS: the per-frame step.</summary>
    public const int ProcessEvent = 3000;

    /// <summary>AM_NONE, AM_STAND, AM_IDLE (EAnimMode; AM_INVALID is -1): the modes from AM_WALK (1) to AM_IDLE loop.</summary>
    public const int ModeNone = 0;
    public const int ModeStand = 3;
    public const int ModeIdle = 10;

    private readonly ComplexThing _thing;

    public Animation(ComplexThing thing)
    {
        _thing = thing;
        Frame = 0;
        LastFrame = 0;
        AnimIndex = -1;
        AnimMode = ModeNone;
        FrameIncrement = 0;
        ForceLooped = false;
        thing.Context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame);
    }

    /// <summary>mFrame: 0..1.</summary>
    public float Frame;

    /// <summary>mLastFrame: the frame before this step, for render interpolation.</summary>
    public float LastFrame;

    /// <summary>mAnimIndex: the render thing's index for the mode.</summary>
    public int AnimIndex;

    /// <summary>mAnimMode (EAnimMode; -1 is AM_INVALID).</summary>
    public int AnimMode;

    public float FrameIncrement;

    public bool ForceLooped;

    /// <summary>AnimModeLoops: AM_WALK (1) to AM_IDLE.</summary>
    public static bool AnimModeLoops(int mode) => mode > ModeNone && mode <= ModeIdle;

    public override void HandleEvent(Event e)
    {
        Process();
        _thing.Context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame, EventPriority.StartOfFrame, null, e as ScheduledEvent);
    }

    /// <summary>Process: one frame's step.</summary>
    public void Process()
    {
        LastFrame = Frame;
        if (AnimMode == ModeNone || AnimMode == -1)
        {
            return;
        }

        Frame += FrameIncrement;
        if (Frame < 1.0)
        {
            return;
        }

        if (AnimModeLoops(AnimMode) || ForceLooped)
        {
            Frame -= 1.0f;
        }
        else if (_thing.FinishedPlayingCurrentAnimation() && Frame >= 1.0f)
        {
            FrameIncrement = _thing.GetRenderThingFrameIncrement(ModeStand, ref AnimIndex);
            Frame = 0;
            AnimMode = ModeStand;
            if (FrameIncrement <= 0.0001)
            {
                FrameIncrement = 1.0f;
            }
        }

        if (Frame >= 1.0f)
        {
            Frame = 1.0f;
        }
    }

    /// <summary>SetAnimMode: a new mode (or the same one reset) takes the render thing's rate for it (1 when none).</summary>
    public bool SetAnimMode(int mode, bool resetFrame, bool forceLooped)
    {
        if (mode != AnimMode || resetFrame)
        {
            ForceLooped = forceLooped;
            float inc = _thing.GetRenderThingFrameIncrement(mode, ref AnimIndex);
            if (resetFrame)
            {
                Frame = 0;
            }

            FrameIncrement = inc;
            AnimMode = mode;
            if (FrameIncrement <= 0.0001)
            {
                FrameIncrement = 1.0f;
            }
        }

        return true;
    }

    /// <summary>GetRenderFrame: the frame between the last step and this one at the render fraction (this one after a wrap).</summary>
    public float GetRenderFrame()
    {
        if (LastFrame > Frame)
        {
            return Frame;
        }

        float fraction = _thing.Context.Game?.Loop.Fraction ?? 0.0f;
        return ((Frame - LastFrame) * fraction) + LastFrame;
    }
}
