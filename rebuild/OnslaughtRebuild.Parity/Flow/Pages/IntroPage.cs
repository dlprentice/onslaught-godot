// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPIntro.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The title page (<c>CFEPIntro</c>): select or a click anywhere goes to the main menu; 30 seconds idle quits the front
/// end to the attract videos. Entering it forgets player 0's port.
/// </summary>
/// <remarks>
/// Not modelled: question 0's answer, which sets the stress-test switch and quits to the attract videos (no caller that
/// asks question 0 on this page is identified), and the render's splash easing and title flash (presentation;
/// <c>Render</c> 0x0051b840, unverified). <c>mUnknown10</c> is written but never read, so it is left out.
/// </remarks>
public sealed class IntroPage(FrontEnd frontEnd) : FrontEndPageBase
{
    /// <summary><c>mUnknown0c</c>: while set, select does nothing and the idle timer stands still.</summary>
    public bool Locked { get; set; }

    /// <summary><c>gIntroUnknown</c>: when set, the page never times out (nothing in the executable sets it).</summary>
    public bool NeverTimesOut { get; set; }

    public float StartTime { get; private set; }

    /// <summary><c>mPressStart</c>: the "press start" prompt's timer, from 1 s after entry.</summary>
    public float PressStart { get; private set; }

    /// <summary><c>CFEPIntro::Init</c> (0x0051b640) and <c>Reset</c> (0x0051b610).</summary>
    public override bool Init()
    {
        Locked = false;
        frontEnd.MessageBox.Kill();
        return true;
    }

    /// <summary><c>CFEPIntro::ButtonPressed</c> (0x0051b660): select goes to the main menu (supervisor mode, as the front end sets it).</summary>
    public override void ButtonPressed(int button, float value)
    {
        if (button != FrontEndButtons.MenuSelect || Locked) return;
        frontEnd.AttractMode = false;
        frontEnd.SetPage(frontEnd.SupervisorMode ? FrontEndPage.Main : FrontEndPage.E3LevelSelect, frontEnd.SupervisorMode ? 50 : 1);
        frontEnd.PlaySound(FrontEndSound.Select);
    }

    /// <summary><c>CFEPIntro::Process</c> (0x0051b6b0). The PC memory card never changes and the auto-configuration test is off.</summary>
    public override void Process(FrontEndPageState state)
    {
        if (state != FrontEndPageState.Active) return;
        float now = frontEnd.Clock.GetSysTimeFloat();
        if (!Locked && !NeverTimesOut)
        {
            // x87: the difference rounds to 24 bits and compares with 30 exactly.
            if (X87.Sub(now, StartTime) > 30.0) frontEnd.Quit = FrontEnd.QuitAttract;
        }
        else
        {
            StartTime = now;
            PressStart = 0f;
        }
        if (PressStart == 0f && X87.Sub(frontEnd.Clock.GetSysTimeFloat(), StartTime) > 1.0) PressStart = 0.00001f;
        if (PressStart != 0f) PressStart = (float)X87.Add(PressStart, X87.Mul(frontEnd.Timing.FrameStep, 2f));

        if (frontEnd.MessageBox.BeingDisplayed && frontEnd.MessageBox.Question == 10 && frontEnd.MessageBox.GetFinalChoice() == MessageBoxQuestion.Yes)
        {
            Locked = false;
            frontEnd.MessageBox.Kill();
        }

        // Retail passes the window's width for both the right and the bottom edge, so the whole screen is the target.
        float width = frontEnd.Platform.Window.Width;
        frontEnd.ProcessMouseClickInRect(0f, 0f, width, width, FrontEndButtons.MenuSelect);
    }

    /// <summary><c>CFEPIntro::TransitionNotification</c> (0x0051be70): the timers restart, player 0 is forgotten and both reconnect prompts reset.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        StartTime = frontEnd.Clock.GetSysTimeFloat();
        frontEnd.ForgetPlayer0();
        frontEnd.Reconnect[0].Reset(ReconnectState.Ok);
        frontEnd.Reconnect[1].Reset(ReconnectState.Ok);
        PressStart = 0f;
    }
}
