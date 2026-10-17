// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPDebriefing.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The debriefing (<c>CFEPDebriefing</c>): the level's result, then each new goodie flies across the screen, one every
/// half second. Once the last goodie has crossed the middle, the page fades in its prompt, and select, back or a click
/// anywhere returns to the level select. The first goodie ever won also explains goodies in a message box.
/// </summary>
/// <remarks>
/// The existing shared particle simulation owns effect creation and its CRT draws. Not modelled: the
/// auto-configuration test's presses and box timeout, and the drawing.
/// </remarks>
public sealed class DebriefingPage(FrontEnd frontEnd) : FrontEndPageBase
{
    public const int MaxGoodies = 100;

    private FVector[]? _positions;
    private ParticleEffectLink[]? _links;

    public float FastUntil { get; private set; }
    public int NumShown { get; private set; }
    public float NextTime { get; private set; }
    public int NumGoodies { get; private set; }
    public bool FirstGoodie { get; private set; }
    public float Fade { get; private set; }
    public float StartTime { get; private set; }

    /// <summary>The goodie's position along the screen (<c>mPositions[i].X</c>), which starts at -3 when it is shown.</summary>
    public float PositionX(int goodie) => Position(goodie).X;

    public FVector Position(int goodie) => _positions![goodie];
    public ParticleEffectLink? Link(int goodie) => _links?[goodie];

    /// <summary>
    /// What <c>mPositions</c> holds before a goodie is shown. <c>FVector</c>'s constructor leaves it uninitialised, so
    /// retail's value is whatever the game's heap last held there: an open question, answered for a run by when its
    /// prompt appears with two or more goodies (the last goodie's X, moving 0.1 a frame, starts the fade once above 0).
    /// Zero by default.
    /// </summary>
    public float UnshownPositionX { get; set; }

    /// <summary><c>CFEPDebriefing::Init</c> (0x00456780).</summary>
    public override bool Init()
    {
        _positions = new FVector[MaxGoodies];
        Array.Fill(_positions, new FVector(UnshownPositionX, 0, 0));
        _links = frontEnd.Particles is { } particles
            ? Enumerable.Range(0, MaxGoodies).Select(_ => new ParticleEffectLink(particles.Manager)).ToArray()
            : null;
        FastUntil = 0f;
        NumShown = 0;
        NextTime = 0f;
        return true;
    }

    /// <summary><c>CFEPDebriefing::Shutdown</c> (0x00456850).</summary>
    public override void Shutdown()
    {
        if (_links is not null) foreach (ParticleEffectLink link in _links) link.Release();
        _links = null;
        _positions = null;
    }

    /// <summary><c>CFEPDebriefing::ButtonPressed</c> (0x004568a0): select or back once the prompt is half in; the cheat button hurries the effect.</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuSelect:
            case FrontEndButtons.MenuBack:
                if (Fade > 0.5f)
                {
                    frontEnd.SetPage(FrontEndPage.LevelSelect, 30);
                    frontEnd.PlaySound(FrontEndSound.Select);
                    if (_links is not null) foreach (ParticleEffectLink link in _links) link.Stop(false);
                }
                break;
            case FrontEndButtons.Cheat:
                FastUntil = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 0.1f);
                break;
        }
    }

    /// <summary>
    /// <c>CFEPDebriefing::Process</c> (0x00456930): while active and no box is up, every goodie moves 0.1 along; the first
    /// goodie ever explains itself once it is shown and has reached 0.45; a new goodie appears every half second (the
    /// faster effect while hurried). The prompt starts fading in once the last goodie is past 0, at 0.02 a frame.
    /// </summary>
    public override void Process(FrontEndPageState state)
    {
        FVector[] positions = _positions!;
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (box.BeingDisplayed)
        {
            // The auto-configuration test's box timeout (not modelled).
        }
        else if (state == FrontEndPageState.Active)
        {
            for (int i = 0; i < NumGoodies; i++)
            {
                positions[i].X = (float)X87.Add(positions[i].X, 0.1f);
                _links?[i].SetPos(positions[i]);
            }
            if (FirstGoodie && NumShown > 0 && positions[0].X >= 0.45f)
            {
                FirstGoodie = false;
                // TEXT id 0x37416e9: the first goodie's explanation.
                frontEnd.ShowMessage(320f, 270f, 350f, t => t.ById(0x37416e9), 0, 10, MessageBoxOption.Ok, 0);
                box.SetOnDismiss(FrontEndPage.None, 0);
            }
            bool fast = frontEnd.Clock.GetSysTimeFloat() < FastUntil;
            if (NumShown < NumGoodies && frontEnd.Clock.GetSysTimeFloat() > NextTime)
            {
                ParticleManager manager = frontEnd.Particles?.Manager ?? throw new InvalidOperationException(
                    "a new goodie needs the front-end particle resources");
                positions[NumShown] = new FVector(-3f, 0f, 1f);
                manager.CreateEffect(manager.Set.GetPD(fast ? "Goodie Effect 2" : "Goodie Effect"),
                    _links![NumShown], default, false, false);
                _links[NumShown].SetPos(positions[NumShown]);
                NumShown++;
                NextTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 0.5f);
            }
        }

        // With no goodies the fade has normally already started, so the last position is never read. While the page is
        // inactive before its first transition (no goodies, no fade) retail reads mPositions[-1], the memory manager's
        // word before the array: whatever it does to the fade, TransitionNotification sets the fade again before the
        // page shows, so the core reads nothing there.
        if (Fade == 0f && NumGoodies > 0 && positions[NumGoodies - 1].X > 0f) Fade = 0.001f;
        if (Fade != 0f)
        {
            Fade = (float)X87.Add(Fade, 0.02f);
            if (Fade > 1f) Fade = 1f;
        }
    }

    /// <summary>RenderPreCommon (00456d40) sets FED's camera before either page's ordinary render.</summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest)
    {
        if (frontEnd.Particles is { } particles) particles.Camera = (new FVector(0, -2, 0), FMatrix.Identity);
    }

    /// <summary>
    /// The state-changing part of <c>CFEPDebriefing::Render</c> (0x00456dd0): the help arrow (select, sliding with the
    /// prompt's fade unless leaving for the level select), then, once settled and with no interactive box, a click
    /// anywhere selects.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        frontEnd.RenderHelpArrow(4, dest == FrontEndPage.LevelSelect ? transition : Fade);
        if (transition == 1f && !frontEnd.MessageBox.IsInteractive)
            frontEnd.ProcessMouseClickInRect(0f, 0f, 640f, 480f, FrontEndButtons.MenuSelect);
    }

    /// <summary>
    /// <c>CFEPDebriefing::TransitionNotification</c> (0x00457cf0): the career counts its goodies and hands over how many
    /// are new (at most 99) and whether the first ever was among them; with none the prompt starts fading in at once.
    /// </summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        OnslaughtRebuild.Parity.Career.Career career = frontEnd.Career;
        career.CountGoodies();
        NumGoodies = career.GetAndResetGoodieNewCount();
        FirstGoodie = career.GetAndResetFirstGoodie();
        if (NumGoodies >= MaxGoodies) NumGoodies = MaxGoodies - 1;
        Fade = NumGoodies > 0 ? 0f : 0.001f;
        StartTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 0.2f);
    }
}
