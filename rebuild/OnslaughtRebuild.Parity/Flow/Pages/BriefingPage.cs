// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPBriefing.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The mission briefing (<c>CFEPBriefing</c>): the selected level's video and text. Select goes on to the Battle Engine
/// configuration page; back returns to the level select and restarts the background video.
/// </summary>
/// <remarks>
/// The videos are the host's (<see cref="FrontEnd.Video"/>): this page says which file to open and when. Not modelled:
/// the auto-configuration test's automatic select, and the drawing.
/// </remarks>
public sealed class BriefingPage(FrontEnd frontEnd) : FrontEndPageBase
{
    public float Timer { get; private set; }
    public float TextFade { get; private set; }
    public float StartTime { get; private set; }
    public bool VideoOpen { get; private set; }

    /// <summary><c>gBriefingVideoScaleX</c> and <c>gBriefingVideoScaleY</c>: the video's size against the full-size one.</summary>
    public (float X, float Y) VideoScale { get; private set; } = (1f, 1f);

    /// <summary><c>CFEPBriefing::Init</c> (0x00451b70).</summary>
    public override bool Init()
    {
        Timer = 0f;
        VideoOpen = false;
        return true;
    }

    /// <summary><c>CFEPBriefing::Process</c> (0x00451b80): the text fades in while active and out while leaving.</summary>
    public override void Process(FrontEndPageState state)
    {
        Timer = (float)X87.Add(Timer, 0.01f);
        if (state == FrontEndPageState.Active) TextFade = (float)X87.Add(TextFade, 0.05f);
        else if (state == FrontEndPageState.TransitioningFrom) TextFade = (float)X87.Sub(TextFade, 0.15f);
        if (TextFade < 0f) TextFade = 0f;
        if (TextFade > 1f) TextFade = 1f;
    }

    /// <summary><c>CFEPBriefing::ButtonPressed</c> (0x00451c20).</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuSelect:
                frontEnd.PlaySound(FrontEndSound.Select);
                frontEnd.SetPage(FrontEndPage.BeConfig, 25);
                break;
            case FrontEndButtons.MenuBack:
                frontEnd.PlaySound(FrontEndSound.Back);
                frontEnd.SetPage(frontEnd.SupervisorMode ? FrontEndPage.LevelSelect : FrontEndPage.E3LevelSelect, 30);
                // CFEPCommon::StartVideo: the front end's background video again.
                frontEnd.Video?.Open(@"data\video\FEBack128.vid", 128, 128);
                VideoOpen = false;
                break;
        }
    }

    /// <summary>
    /// The state-changing part of <c>CFEPBriefing::Render</c> (0x00451d50): once the page is three quarters in, the text
    /// reveal's start time comes forward to now; then the help arrows (4 select, 1 back).
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        if (transition > 0.75f && frontEnd.Clock.GetSysTimeFloat() < StartTime) StartTime = frontEnd.Clock.GetSysTimeFloat();
        frontEnd.RenderHelpArrow(4, transition);
        frontEnd.RenderHelpArrow(1, transition);
    }

    /// <summary><c>CFEPBriefing::TransitionNotification</c> (0x00452430).</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        StartTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 10000f);
        TextFade = 0f;
    }

    /// <summary>
    /// <c>CFEPBriefing::ActiveNotification</c> (0x00452460): opens the level's briefing video: the full-size one if it
    /// exists, else the smaller one (drawn larger), else the quarter-size one. A level's variants share the first
    /// variant's briefing, except 511 and 512.
    /// </summary>
    public override void ActiveNotification(FrontEndPage from)
    {
        int level = BriefingLevel(frontEnd.SelectedLevel);
        IFrontEndVideo video = frontEnd.Video ?? throw new InvalidOperationException("The briefing needs the host's video player (FrontEnd.Video).");
        string name = $@"data\video\Briefings\PC_{level:000}_exact.vid";
        VideoScale = (1f, 1f);
        if (!video.Exists(name))
        {
            name = $@"data\video\Briefings\PC_{level:000}.vid";
            VideoScale = (0.78515625f, 0.58203125f);
            if (!video.Exists(name)) name = $@"data\video\Briefings\PC_{level:000}_25.vid";
        }
        // SINT(201 / scale): _ftol truncates the quotient (256 for the smaller video, whose scales are 201/256 and 149/256).
        video.Open(name, X87.Ftol(X87.Div(201f, VideoScale.X)), X87.Ftol(X87.Div(149f, VideoScale.Y)));
        VideoOpen = true;
    }

    /// <summary>The level whose briefing a level shows. An unlisted level leaves retail's local unset; none is reachable.</summary>
    public static int BriefingLevel(int selected) => selected switch
    {
        212 => 211,
        222 => 221,
        232 => 231,
        312 => 311,
        322 => 321,
        332 => 331,
        412 => 411,
        422 => 421,
        432 => 431,
        522 => 521,
        524 => 523,
        612 => 611,
        622 => 621,
        732 => 731,
        742 => 741,
        100 or 110 or 200 or 211 or 221 or 231 or 300 or 311 or 321 or 331 or 400 or 411 or 421 or 431 or 500 or 511
            or 512 or 521 or 523 or 600 or 611 or 621 or 700 or 710 or 720 or 731 or 741 or 800 => selected,
        _ => throw new ArgumentOutOfRangeException(nameof(selected), selected, "No briefing for this level."),
    };
}
