// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FrontEnd.cpp (CFrontEnd::DrawSlidingTextBordersAndMask 00467200, DrawStandardVideoBackground
// 004679e0, DrawTitleBar 00467bd0) and src/FEPCommon.cpp (CFEPCommon::RenderVideoBackground 00452ce0,
// RenderSmallBEALogo 00452df0, RenderSmallForsetiText 00452fd0, RenderSelectionMarker 004530b0).

using OnslaughtRebuild.Parity.Flow.Draw;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>
/// The front end's shared drawing, which the pages call from their render path. Each draws only (through
/// <see cref="Draw"/>) and does nothing without it; none changes the simulation.
/// </summary>
public sealed partial class FrontEnd
{
    /// <summary>The pages with the sliding text, the metal borders and the mask (<c>got_standard_SlidingTextBordersAndMask</c>).</summary>
    private static bool HasStandardBorders(FrontEndPage page) => page is FrontEndPage.DevSelect or FrontEndPage.Directory
        or FrontEndPage.Goodies or FrontEndPage.LevelSelect or FrontEndPage.LoadGame or FrontEndPage.Multiplayer
        or FrontEndPage.SaveGame or FrontEndPage.VirtualKeyboard or FrontEndPage.ScreenPos or FrontEndPage.Options;

    /// <summary>
    /// <c>CFEPCommon::RenderVideoBackground</c>: the background video, scaled about (cx, cy) (the centre of the window
    /// unless given), at depth 0.96.
    /// </summary>
    public void RenderVideoBackground(float scale, uint colour, float cx = -9999999f, float cy = -9999999f)
    {
        if (Draw is not { } draw) return;
        if (cx == -9999999f)
        {
            cx = FrontEndDraw.WindowWidth * 0.5f;
            cy = FrontEndDraw.WindowHeight * 0.5f;
        }

        draw.VideoWithoutFilm(cx, cy, 0.96f, FrontEndDraw.WindowWidth * scale * 0.0087890625f,
            FrontEndDraw.WindowHeight * scale * 0.0079101566f, colour, true);
    }

    /// <summary><c>CFrontEnd::DrawStandardVideoBackground</c>: the video background, fading in over the transition's second half (its first half towards the main page).</summary>
    public void DrawStandardVideoBackground(float transition, uint colour, FrontEndPage dest)
    {
        int alpha = dest == FrontEndPage.Main
            ? FrontEndDraw.MakeAlpha(FrontEndDraw.RangeTransition(transition, 0f, 0.5f))
            : FrontEndDraw.MakeAlpha(FrontEndDraw.RangeTransition(transition, 0.5f, 1f));
        RenderVideoBackground(1f, FrontEndDraw.BlendAlpha2(colour, alpha));
    }

    /// <summary><c>CFEPCommon::RenderSmallBEALogo</c>: the small logo with its shadow, and the shine passing over it.</summary>
    public void RenderSmallBEALogo(float transition)
    {
        if (Draw is not { } draw || !(transition > 0f)) return;
        int alpha = Numerics.X87.Fist(transition * 255f);
        draw.DrawColouredSprite(65f + FrontEndDraw.ShadowOffsetX(Counter), 35f + FrontEndDraw.ShadowOffsetY(Counter), 0.045f,
            FrontEndTexture.SmallBeaLogo, FrontEndDraw.BlendAlpha2(0x3f000000, alpha), 0.84f, 0.84f);
        draw.ZAlways = true;
        draw.DrawColouredSprite(60f, 25f, 0.999f, FrontEndTexture.SmallBeaLogo, FrontEndDraw.BlendAlpha2(0xffafcfff, alpha), 0.8f, 0.8f);
        draw.ZAlways = false;
        float x = 256f - (float)((double)(Counter * 0.6f) % 512.0) - 80f;
        draw.Blend = CanvasBlend.Additive;
        uint c = (uint)(alpha * 0x7f) >> 8;
        draw.DrawColouredSprite(x, 100f, 0.998f, FrontEndTexture.LogoShine, c * 0x010101 + 0xff000000, 1f, 2f, SpriteAlign.Centre);
        draw.DrawColouredSprite(x + 512f, 100f, 0.998f, FrontEndTexture.LogoShine, c * 0x010101 + 0xff000000, 1f, 2f, SpriteAlign.Centre);
        draw.Blend = CanvasBlend.Modulate;
    }

    /// <summary><c>CFEPCommon::RenderSmallForsetiText</c>: four copies of the small Forseti writing scrolling up the left.</summary>
    public void RenderSmallForsetiText(float transition)
    {
        if (Draw is not { } draw) return;
        float y = 90f - (float)((double)(Counter * 0.3f) % 180.0);
        int alpha = Numerics.X87.Fist(transition * 255f);
        for (int c0 = 0; c0 < 4; c0++)
        {
            draw.DrawColouredSprite(86f, y, 0.9f, FrontEndTexture.ForsetiText, FrontEndDraw.BlendAlpha2(0x3fffffff, alpha), 0.5f, 0.5f, SpriteAlign.Centre);
            y += 180f;
        }
    }

    /// <summary><c>CFEPCommon::RenderSelectionMarker</c>: the rock background placed and scaled about (x, y).</summary>
    public void RenderSelectionMarker(float x, float y, float scale, int alpha)
    {
        if (Draw is not { } draw) return;
        float sy = 240f - y * scale;
        float sx = 320f - x * scale; // NOSCROLL_BACKGROUND is 0
        draw.DrawColouredSprite(sx, sy, 0.955f, FrontEndTexture.SelectionMarker, FrontEndDraw.BlendAlpha2(0xffbfbfff, alpha), scale, scale, SpriteAlign.Centre);
    }

    /// <summary>
    /// <c>CFrontEnd::DrawSlidingTextBordersAndMask</c>: the small Forseti writing, the selection brackets (opening as the main
    /// page hands over, resizing to and from the keyboard and options pages, flying apart otherwise), and, once the page is
    /// three quarters in, the bracket mask and a strip down the left drawn into depth only. Between two pages that both
    /// have it, only the higher-numbered page draws it.
    /// </summary>
    public void DrawSlidingTextBordersAndMask(float transition, FrontEndPage dest)
    {
        if (Draw is not { } draw) return;
        FrontEndPage from = FrontEndPage.Unknown;
        if (ActivePage == FrontEndPage.Transition)
        {
            from = TransitionTo == dest ? TransitionFrom : TransitionTo;
            if (HasStandardBorders(from) && HasStandardBorders(dest) && (int)dest > (int)from) return;
        }

        float tr = dest is not (FrontEndPage.Briefing or FrontEndPage.Debriefing or FrontEndPage.MultiplayerStart)
            ? FrontEndDraw.RangeTransition(transition, 0.75f, 1f) : 1f;
        RenderSmallForsetiText(tr);

        bool resizing = from is FrontEndPage.VirtualKeyboard or FrontEndPage.Options;
        if (HasStandardBorders(dest) && !resizing && dest is not (FrontEndPage.VirtualKeyboard or FrontEndPage.Options)) transition = 1f;

        const float bx = 328f, by = 343f, scale1 = 1.25f, scale2 = 1.4f;
        int alpha = 0xff;
        float scale = resizing || dest is FrontEndPage.VirtualKeyboard or FrontEndPage.Options
            || ActivePage is FrontEndPage.VirtualKeyboard or FrontEndPage.Options ? scale2 : scale1;
        float yaw = 0f;
        if (transition < 1f && dest == FrontEndPage.Main)
        {
            if (transition < 0.2f) alpha = 0;
            if (transition < 0.5f)
            {
                tr = FrontEndDraw.RangeTransition(transition, 0.2f, 0.5f);
                alpha = FrontEndDraw.MakeAlpha(tr);
                scale = tr;
                yaw = -(tr * 0.3f);
            }
            else if (transition < 0.6f) scale = 1f;
            else if (transition < 0.7f) scale = FrontEndDraw.Range(FrontEndDraw.RangeTransition(transition, 0.6f, 0.7f), 1f, scale);
        }
        else if (transition < 1f && from == FrontEndPage.VirtualKeyboard)
        {
            scale = FrontEndDraw.CosResize(transition, scale1, scale2);
        }
        else if (transition < 1f)
        {
            tr = 1f - FrontEndDraw.RangeTransition(transition, 0.5f, 1f);
            alpha = FrontEndDraw.MakeAlpha(1f - tr);
            scale = scale1 + tr * 1.4f;
            yaw = -tr;
        }

        float shx = FrontEndDraw.ShadowOffsetX(Counter), shy = FrontEndDraw.ShadowOffsetY(Counter);
        draw.DrawColouredSprite(bx + 5f + shx, by + 10f + shy, 0.35f, FrontEndTexture.SelectBracket1, FrontEndDraw.BlendAlpha2(0x3f000000, alpha),
            scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        draw.DrawColouredSprite(bx, by, 0.2f, FrontEndTexture.SelectBracket1, FrontEndDraw.BlendAlpha2(0xffffffff, alpha),
            scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        float savescale = scale;

        alpha = 0xff;
        scale = 1f;
        yaw = 0f;
        bool drawOuter = false;
        if (transition < 1f && dest == FrontEndPage.Main)
        {
            if (transition < 0.5f)
            {
                tr = FrontEndDraw.RangeTransition(transition, 0.2f, 0.5f);
                alpha = FrontEndDraw.MakeAlpha(tr);
                scale = tr;
                yaw = -(tr * 0.3f);
                drawOuter = true;
            }
            else if (transition < 0.7f)
            {
                scale = 1f;
                drawOuter = true;
            }
            else if (transition < 0.9f)
            {
                tr = 1f - FrontEndDraw.RangeTransition(transition, 0.7f, 0.9f);
                alpha = FrontEndDraw.MakeAlpha(tr);
                scale = tr;
                yaw = -(tr * 0.3f);
                drawOuter = true;
            }
        }

        if (drawOuter)
        {
            draw.DrawColouredSprite(bx + 5f + shx, by + 10f + shy, 0.35f, FrontEndTexture.SelectBracket2, FrontEndDraw.BlendAlpha2(0x3f000000, alpha),
                scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
            draw.DrawColouredSprite(bx, by, 0.2f, FrontEndTexture.SelectBracket2, FrontEndDraw.BlendAlpha2(0xffffffff, alpha),
                scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        }

        if (transition > 0.75f || resizing)
        {
            draw.Blend = CanvasBlend.Keep;
            draw.DrawColouredSprite(bx, by, 0.1f, FrontEndTexture.SelectBracketMask, 0xffffffff, savescale, savescale, SpriteAlign.Centre, clamp: true);
            draw.DrawColouredSprite(10f, 240f, 0.1f, FrontEndTexture.White, 0xffffffff, 2f, 15f, SpriteAlign.Centre, clamp: true);
            draw.Blend = CanvasBlend.Modulate;
        }
    }

    /// <summary>
    /// <c>CFrontEnd::DrawTitleBar</c>: the title's dark box, its brackets (opening as the main page hands over) and the title
    /// in the normal font; leaving for any page but the main one, the bar drops out of sight over the transition's first half.
    /// </summary>
    public void DrawTitleBar(string text, float transition, FrontEndPage dest)
    {
        if (Draw is not { } draw || NormalFont is not { } font) return;
        float yo = 0f;
        if (transition < 1f && dest != FrontEndPage.Main)
        {
            if (transition < 0.5f) return;
            yo = -MathF.Sin(transition * 3.14159265358979f) * 200f;
        }

        const float hx = 390f, hy = 69f, hscale = 1.25f;
        float tr = FrontEndDraw.RangeTransition(transition, 0.8f, 1f);
        draw.DrawColouredSprite(hx, hy + yo + 10f, 0.33f, FrontEndTexture.HeaderTextBox, FrontEndDraw.BlendAlpha2(0x7f000000, FrontEndDraw.MakeAlpha(tr)),
            1f, 1f, SpriteAlign.Centre);

        int alpha = 0xff;
        float scale = hscale, yaw = 0f;
        if (transition < 1f && dest == FrontEndPage.Main)
        {
            if (transition < 0.3f) alpha = 0;
            else if (transition < 0.7f)
            {
                tr = FrontEndDraw.RangeTransition(transition, 0.3f, 0.7f);
                alpha = FrontEndDraw.MakeAlpha(tr);
                scale = tr;
                yaw = -((1f - tr) * 0.3f);
            }
            else if (transition < 0.8f) scale = 1f;
            else scale = FrontEndDraw.Range(FrontEndDraw.RangeTransition(transition, 0.8f, 1f), 1f, hscale);
        }

        float shx = FrontEndDraw.ShadowOffsetX(Counter);
        // Retail offsets both shadow coordinates by the x shadow offset.
        draw.DrawColouredSprite(hx + 5f + shx, hy + yo + 5f + shx, 0.35f, FrontEndTexture.HeaderBracket1, FrontEndDraw.BlendAlpha2(0x3f000000, alpha),
            scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        draw.DrawColouredSprite(hx, hy + yo, 0.3f, FrontEndTexture.HeaderBracket1, FrontEndDraw.BlendAlpha2(0xffffffff, alpha),
            scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);

        alpha = 0xff;
        scale = 1f;
        yaw = 0f;
        bool drawOuter = false;
        if (transition < 1f && dest == FrontEndPage.Main)
        {
            if (transition < 0.3f)
            {
            }
            else if (transition < 0.7f)
            {
                tr = FrontEndDraw.RangeTransition(transition, 0.3f, 0.7f);
                alpha = FrontEndDraw.MakeAlpha(tr);
                scale = tr;
                yaw = -((1f - tr) * 0.3f);
                drawOuter = true;
            }
            else if (transition < 0.9f)
            {
                scale = 1f;
                drawOuter = true;
            }
            else
            {
                tr = 1f - FrontEndDraw.RangeTransition(transition, 0.9f, 1f);
                alpha = FrontEndDraw.MakeAlpha(tr);
                scale = tr;
                yaw = -((1f - tr) * 0.3f);
                drawOuter = true;
            }
        }

        if (drawOuter)
        {
            draw.DrawColouredSprite(hx + 5f + shx, hy + yo + 5f + shx, 0.35f, FrontEndTexture.HeaderBracket2, FrontEndDraw.BlendAlpha2(0x3f000000, alpha),
                scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
            draw.DrawColouredSprite(hx, hy + yo, 0.3f, FrontEndTexture.HeaderBracket2, FrontEndDraw.BlendAlpha2(0xffffffff, alpha),
                scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        }

        uint col = 0xff7f7f7f;
        if (dest == FrontEndPage.Main) col = FrontEndDraw.BlendAlpha2(col, FrontEndDraw.MakeAlpha(FrontEndDraw.RangeTransition(transition, 0.9f, 1f)));
        int cx = font.GetTextExtent(text).Width;
        draw.DrawTextDynamic(font, CanvasTextureKind.NormalFont, hx - cx * 0.5f, hy + yo - 4f, 0.1f, 1f, 1f, col, text, 1000f);
    }
}
