// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/console.cpp (CConsole::SetLoading, SetLoadingRange 0042cf40, SetLoadingFraction,
// RenderLoadingScreen): the loading screen's state and when it presents.

using OnslaughtRebuild.Parity.Clock;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>
/// CONSOLE's loading screen, as far as it decides Presents: it renders (PLATFORM.Process, then a Present) when its
/// fraction is set, unless the last render was under 0.05 s ago on the clock (100% always renders). While the clock
/// stands still between Presents only the first render of a load gets through, so a load costs at most one Present
/// before its 100% screen. The picture is the host's.
/// </summary>
public sealed class LoadingScreen(PlatformClock clock, Action? process = null)
{
    /// <summary>mLoading.</summary>
    public bool Loading { get; private set; }

    /// <summary>mLoadingScreen: the picture is loaded (SetLoading with its first flag), so the screen can render.</summary>
    public bool HasScreen { get; private set; }

    public float Fraction { get; private set; }

    public float From { get; private set; }

    public float To { get; private set; }

    /// <summary>mLastLoadingRender: the clock when the screen last rendered (0 before the first).</summary>
    public float LastRender { get; private set; }

    /// <summary>The Presents the screen has made.</summary>
    public int Renders { get; private set; }

    /// <summary>
    /// A render the boot made (its Presents after the films are the loading screen's): the throttle counts from now.
    /// </summary>
    public void RenderedByBoot() => LastRender = clock.GetSysTimeFloat();

    /// <summary>CConsole::SetLoading: on, the start time read and (with <paramref name="loadScreen"/>) the picture loaded; off, released.</summary>
    public void SetLoading(bool on, bool loadScreen = true)
    {
        if (Loading == on)
        {
            return;
        }

        if (on)
        {
            clock.GetSysTimeFloat();
            if (loadScreen)
            {
                HasScreen = true;
            }

            Fraction = 0;
            To = 0;
            From = 0;
        }
        else
        {
            HasScreen = false;
            clock.GetSysTimeFloat();
        }

        Loading = on;
    }

    /// <summary>CConsole::SetLoadingRange (0x0042cf40): the range, the fraction at its start, and a render.</summary>
    public void SetLoadingRange(float from, float to)
    {
        From = from;
        To = to;
        Fraction = from;
        Render();
    }

    /// <summary>
    /// The level load's end: its last range runs to 100 and SetLoadingFraction(1.0f) right after PreRun brings the bar
    /// there, where the screen always renders.
    /// </summary>
    public void Complete()
    {
        To = 100f;
        SetLoadingFraction(1.0f);
    }

    /// <summary>CConsole::SetLoadingFraction: the fraction within the range, and a render.</summary>
    public void SetLoadingFraction(float fraction)
    {
        Fraction = ((To - From) * fraction) + From;
        Render();
    }

    /// <summary>
    /// CConsole::RenderLoadingScreen: nothing without the picture (modes 2 and 3 render anyway); below 100% and outside
    /// mode 1, nothing within 0.05 s after the last render; otherwise PLATFORM.Process (modes 0 and 1), the time noted
    /// and a Present.
    /// </summary>
    public void Render(int mode = 0)
    {
        if (!HasScreen && mode != 2 && mode != 3)
        {
            return;
        }

        if (Fraction != 100f && mode != 1)
        {
            if (clock.GetSysTimeFloat() < LastRender + 0.05f && clock.GetSysTimeFloat() >= LastRender)
            {
                return;
            }
        }

        if (mode < 2)
        {
            process?.Invoke();
        }

        LastRender = clock.GetSysTimeFloat();
        Renders++;
        clock.Present();
    }
}
