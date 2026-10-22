// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPBriefing.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Text;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The mission briefing (<c>CFEPBriefing</c>): the selected level's video and text. Select goes on to the Battle Engine
/// configuration page; back returns to the level select and restarts the background video.
/// </summary>
/// <remarks>
/// The videos are the host's (<see cref="FrontEnd.Video"/>): this page says which file to open and when. Not modelled:
/// the auto-configuration test's automatic select.
/// </remarks>
public sealed class BriefingPage(FrontEnd frontEnd) : FrontEndPageBase
{
    public float Timer { get; private set; }
    public float TextFade { get; private set; }
    public float StartTime { get; private set; }
    public bool VideoOpen { get; private set; }

    /// <summary><c>gBriefingVideoScaleX</c> and <c>gBriefingVideoScaleY</c>: the video's size against the full-size one.</summary>
    public (float X, float Y) VideoScale { get; private set; } = (1f, 1f);

    // The size the video was opened at (FEV's mWidth and mHeight), which its quad takes while no film plays.
    private int _videoWidth = 128, _videoHeight = 128;

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
    /// <c>CFEPBriefing::RenderPreCommon</c> (0x00451c90): the rock background, settled while the page is up or leaving for
    /// the configuration page, sliding to the right and shrinking towards the level select.
    /// </summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest)
    {
        if (frontEnd.Draw is null) return;
        int alpha;
        float scale, x;
        if (dest is FrontEndPage.LevelSelect or FrontEndPage.E3LevelSelect)
        {
            alpha = X87.Fist(X87.Mul(FrontEndDraw.RangeTransition(transition, 0.25f, 1f), 255f));
            scale = transition * 0.25f + 1f;
            x = -200f;
        }
        else
        {
            alpha = 0xff;
            scale = 1.25f;
            x = MathF.Cos(transition * Pi) * 100f - 100f;
        }
        frontEnd.RenderSelectionMarker(x, 0f, scale, alpha);
    }

    /// <summary>
    /// <c>CFEPBriefing::Render</c> (0x00451d50). Its state-changing part: once the page is three quarters in, the text
    /// reveal's start time comes forward to now; then the help arrows (4 select, 1 back). With <see cref="FrontEnd.Draw"/>
    /// it also draws: the Forseti writing, the metal ring and the small logo (while settled or leaving for the
    /// configuration page), the video's box with its two faint lines (easing in, or sliding across to the configuration
    /// page), the video once settled, the level's name and the briefing's nine paragraphs revealed over time, and the
    /// title bar.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        FrontEndDraw? draw = frontEnd.Draw;
        float x = 0f, y = 0f;
        if (draw is not null) (x, y) = RenderVideo(draw, transition, dest);
        if (transition > 0.75f && frontEnd.Clock.GetSysTimeFloat() < StartTime) StartTime = frontEnd.Clock.GetSysTimeFloat();
        // The reveal's clock read is made whether or not anything is drawn, as retail makes it.
        float reveal = (frontEnd.Clock.GetSysTimeFloat() - StartTime) * 0.5f;
        if (draw is not null) RenderText(draw, transition, reveal);
        frontEnd.RenderHelpArrow(4, transition);
        frontEnd.RenderHelpArrow(1, transition);
        // The title is read whether or not it is drawn (each read checks the X key).
        if (frontEnd.Texts is { } texts) frontEnd.DrawTitleBar(texts.GetText(FrontEndText.MissionBriefing), transition, dest);
    }

    private const float Pi = 3.14159265358979f;

    /// <summary>The render's first part: the settled page's ring and logo, the video's box and lines, and the video. Returns the box's centre.</summary>
    private (float X, float Y) RenderVideo(FrontEndDraw draw, float transition, FrontEndPage dest)
    {
        bool settled = transition == 1f || dest == FrontEndPage.BeConfig;
        if (settled) frontEnd.RenderSmallForsetiText(1f);
        if (settled)
            draw.DrawColouredSprite(285f, 225f, FrontEndDraw.FO(900), FrontEndTexture.MetalRingTransFromLevsel2, 0xffffffff, 1.6f, 1.6f, SpriteAlign.Centre);
        if (settled) frontEnd.RenderSmallBEALogo(1f);

        float x, y;
        if (dest == FrontEndPage.BeConfig)
        {
            y = 252f;
            x = (-MathF.Cos(transition * Pi) - 1f) * 0.5f * 600f + 480f;
        }
        else
        {
            float f = 1f - FrontEndDraw.RangeTransition(transition, 0.25f, 0.75f);
            x = 480f - (1f - (1f - f * f)) * 700f;
            f = 1f - FrontEndDraw.RangeTransition(transition, 0.25f, 1f);
            y = (1f - (1f - f * f)) * 400f + 252f;
        }
        draw.DrawColouredSprite(x, y, FrontEndDraw.FO(100), FrontEndTexture.AviBox, 0xffffffff, 1f, 1f, SpriteAlign.Centre);
        float fx = MathF.Floor(x);
        draw.DrawLine(fx - 103f, 0f, fx - 103f, FrontEndDraw.WindowHeight, 0x3fffffff, 1f, 0.1f, 1f);
        draw.DrawLine(fx + 103f, 0f, fx + 103f, FrontEndDraw.WindowHeight, 0x3fffffff, 1f, 0.1f, 1f);

        if (transition == 1f && VideoOpen)
        {
            int a = FrontEndDraw.MakeAlpha(TextFade);
            float cx = FrontEndDraw.WindowWidth * (x - 103f + 3f) / 640f;
            float cy = FrontEndDraw.WindowHeight * (y - 77f + 2f) / 480f;
            float sx = FrontEndDraw.WindowWidth * VideoScale.X / 640f;
            float sy = FrontEndDraw.WindowHeight * VideoScale.Y / 480f;
            draw.VideoWithoutFilm(cx, cy, 0.1f, sx, sy, FrontEndDraw.BlendAlpha2(0xffffffff, a), false, _videoWidth, _videoHeight);
        }
        return (x, y);
    }

    /// <summary>
    /// The level's name and its briefing's nine paragraphs (word-wrapped to 300 in the small font, each line revealed a
    /// little after the last), sliding in from the left over the transition's last quarter.
    /// </summary>
    private void RenderText(FrontEndDraw draw, float transition, float reveal)
    {
        if (frontEnd.NormalFont is not { } normal || frontEnd.SmallFont is not { } small || frontEnd.Texts is not { } texts) return;
        float y = 164f;
        if (frontEnd.SelectedLevel is 611 or 612 && frontEnd.Language == 2) y = 148f;
        float c = FrontEndDraw.RangeTransition(transition, 0.75f, 1f);
        int alpha = X87.Fist(X87.Mul(c, 255f));
        float f = 1f - c;
        float off = f * f * 200f;

        int level = frontEnd.SelectedLevel;
        // Retail sets the colour stage to plain modulate before the video (LT.STS(0, COLOROP, MODULATE)) and restores it
        // only after the text (LT.SetModulate(0)), so the text's colours are not doubled.
        draw.Modulate2x = false;
        draw.DrawTextDynamic(normal, CanvasTextureKind.NormalFont, 180f, 118f, 0.01f, 0.7f, 1f, FrontEndDraw.BlendAlpha2(0xffffffff, alpha),
            LevelText.LevelName(frontEnd, texts, level), reveal);
        for (int n = 0; n < 9; n++)
        {
            List<string> lines = small.WordWrap(LevelText.BriefingText(frontEnd, texts, level, n), 300f);
            float lx = 80f - off;
            foreach (string line in lines)
            {
                draw.DrawTextDynamic(small, CanvasTextureKind.SmallFont, lx, y, 0.01f, 1f, 1f,
                    FrontEndDraw.BlendAlpha2(LevelText.BriefingTextColour, alpha), line, reveal * 2f);
                reveal -= 0.025f;
                y += 16f;
            }
            y += 10f;
        }
        draw.Modulate2x = true;
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
        // SINT(201 / scale): fdiv then fistp, rounding to nearest under /QIfist (00452699..004526c4); the quotients are
        // exact (201 and 149, or 256 for the smaller video, whose scales are 201/256 and 149/256).
        _videoWidth = X87.Fist(X87.Div(201f, VideoScale.X));
        _videoHeight = X87.Fist(X87.Div(149f, VideoScale.Y));
        video.Open(name, _videoWidth, _videoHeight);
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
