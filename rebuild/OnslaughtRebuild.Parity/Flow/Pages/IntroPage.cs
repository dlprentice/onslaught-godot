// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/FEPIntro.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The title page (<c>CFEPIntro</c>): select or a click anywhere goes to the main menu; 30 seconds idle quits the front
/// end to the attract videos. Entering it forgets player 0's port.
/// </summary>
/// <remarks>
/// Not modelled: question 0's answer, which sets the stress-test switch and quits to the attract videos (no caller that
/// asks question 0 on this page is identified), and the text panel the render draws while <c>gIntroUnknown</c> is set
/// (nothing in the executable sets it). <c>mUnknown10</c> is written but never read, so it is left out.
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

    /// <summary>
    /// <c>CFEPIntro::Render</c> (0x0051b840), only once the page has settled: the splash easing in from its large size,
    /// "press start" flashing with a black outline, the Lost Toys logo sliding in, and the title zooming down to its size
    /// with a flash as it lands. The title's zoom reads the clock whether or not anything is drawn, as retail does (a read
    /// counts toward the clock's spin escapes).
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        if (transition != 1f) return;
        Draw.FrontEndDraw? draw = frontEnd.Draw;
        if (draw is not null)
        {
            float t = PressStart > 1f ? 1f : PressStart;
            float s = ((float)Math.Cos(t * 3.14159265358979f) + 1f) * 0.5f * 0.75f + 0.46875f;
            draw.DrawColouredSprite(558f - s * 238f - 126.4375f, 18f - s * -222f - -117.9375f, 0.96f, Draw.FrontEndTexture.FeSplash1,
                0xffffffff, s, s, Draw.SpriteAlign.Centre);
            if (PressStart > 4f && PressStart % 4f < 2f && frontEnd.NormalFont is { } font && frontEnd.Texts is { } texts)
            {
                string text = texts.PcText(0x77);
                int cx = font.GetTextExtent(text).Width;
                float x = 320f - cx * 0.5f;
                var kind = Draw.CanvasTextureKind.NormalFont;
                draw.DrawTextScaled(font, kind, x - 1f, 401f, 0.1f, 1f, 1f, 0xff000000, text, 0);
                draw.DrawTextScaled(font, kind, x + 1f, 401f, 0.1f, 1f, 1f, 0xff000000, text, 0);
                draw.DrawTextScaled(font, kind, x - 1f, 399f, 0.1f, 1f, 1f, 0xff000000, text, 0);
                draw.DrawTextScaled(font, kind, x + 1f, 399f, 0.1f, 1f, 1f, 0xff000000, text, 0);
                draw.DrawTextScaled(font, kind, x, 400f, 0.1f, 1f, 1f, 0xffffffff, text, 0);
            }

            float r = Draw.FrontEndDraw.RangeTransition(PressStart, 4f, 5f);
            float off = (1f - r) * (1f - r) * 400f;
            draw.DrawColouredSprite(124f - off, -6f, 0.101f, Draw.FrontEndTexture.LostToys, 0x3f000000);
            draw.DrawColouredSprite(120f - off, -10f, 0.1f, Draw.FrontEndTexture.LostToys, 0xffffffff);
        }

        float p = (frontEnd.Clock.GetSysTimeFloat() - StartTime) * 1.2f;
        if (draw is null || !(p > 2f)) return;
        float flash = -1f;
        float zoom = (0.5f - p + 2f) * 10f;
        if (zoom < 1f)
        {
            flash = 1f - zoom;
            zoom = 1f;
        }

        int a = Numerics.X87.Fist(1f / zoom * 255f);
        uint shadow = Draw.FrontEndDraw.BlendAlpha2(0x9f000000, a);
        float sc = zoom * 0.5f;
        var title = Draw.FrontEndTexture.BeaTitle2;
        draw.DrawColouredSprite(252f, 292f, 0.05f, title, shadow, sc, sc, Draw.SpriteAlign.Centre);
        draw.DrawColouredSprite(248f, 292f, 0.05f, title, shadow, sc, sc, Draw.SpriteAlign.Centre);
        draw.DrawColouredSprite(252f, 288f, 0.05f, title, shadow, sc, sc, Draw.SpriteAlign.Centre);
        draw.DrawColouredSprite(248f, 288f, 0.05f, title, shadow, sc, sc, Draw.SpriteAlign.Centre);
        draw.DrawColouredSprite(250f, 290f, 0.04f, title, Draw.FrontEndDraw.BlendAlpha2(0xffffffff, a), sc, sc, Draw.SpriteAlign.Centre);
        if (flash < 3f && flash > 0f)
        {
            float f2 = flash * 2f;
            int fa = Numerics.X87.Fist(1f / (f2 * 2f) * 128f);
            sc = f2 * 0.5f;
            draw.DrawColouredSprite(250f, 290f, 0.02f, title, Draw.FrontEndDraw.BlendAlpha2(0xffffffff, fa), sc, sc, Draw.SpriteAlign.Centre);
        }
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
