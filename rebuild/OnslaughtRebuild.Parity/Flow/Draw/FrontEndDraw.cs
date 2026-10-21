// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/DXSpriteRenderer.cpp (CDXSpriteRenderer::DrawSprite 00555be0, DrawColouredSprite 005563d0),
// src/DXFont.cpp (CDXBitmapFont::DrawTextScaled 00540010, ModulateColour), src/Font.cpp (DrawTextDynamic 00465710),
// src/FrontEnd.cpp (CFrontEnd::DrawLine 00466de0, GetShadowOffsetX/Y 00468730/00468750, EnableAdditiveAlpha 004681c0,
// EnableModulateAlpha 004681e0), src/DXFrontEndVideo.cpp (CDXFrontEndVideo::Render 00541790, with no video open) and
// src/TransitionHelpers.h.

using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Text;

namespace OnslaughtRebuild.Parity.Flow.Draw;

/// <summary>
/// The front end's drawing primitives, as the game forms their vertices, sent to a canvas. Nothing here changes the
/// simulation's state: the pages call these only from their render path, with values they have already computed, and
/// the only state kept is the render state the front end sets between draws (the blend and the depth function), which
/// only drawing reads.
/// </summary>
public sealed class FrontEndDraw(FrontEndTextures textures)
{
    /// <summary>The window the front end draws in; its 640 x 480 virtual screen is scaled to it.</summary>
    public const int WindowWidth = 640, WindowHeight = 480;

    public FrontEndTextures Textures { get; } = textures;

    public IFrontEndCanvas Canvas { get; set; } = NullCanvas.Instance;

    /// <summary>The blend <c>EnableAdditiveAlpha</c>/<c>EnableModulateAlpha</c> last set.</summary>
    public CanvasBlend Blend { get; set; }

    /// <summary>The depth function: ALWAYS after <c>SRS(ZFUNC, ALWAYS)</c>, otherwise LESSEQUAL.</summary>
    public bool ZAlways { get; set; }

    /// <summary>The start of a frame's render (<c>CDXFrontEnd::RenderStart</c>): the clear, and the default state.</summary>
    public void BeginFrame()
    {
        Blend = CanvasBlend.Modulate;
        ZAlways = false;
        Canvas.Clear(0x001f1f3f);
    }

    public void Present(long present) => Canvas.Present(present);

    // --- TransitionHelpers.h -------------------------------------------------------------------------------------

    /// <summary><c>RangeTransition</c>: the progress through [start, end], clamped to [0, 1].</summary>
    public static float RangeTransition(float transition, float start, float end)
    {
        float t = (float)X87.Div(X87.Sub(transition, start), X87.Sub(end, start));
        return t < 0f ? 0f : t > 1f ? 1f : t;
    }

    /// <summary><c>MakeAlpha</c>: a fraction as an alpha byte.</summary>
    public static int MakeAlpha(float f)
    {
        int a = X87.Fist(X87.Mul(f, 255f));
        return a < 0 ? 0 : a > 0xff ? 0xff : a;
    }

    /// <summary><c>BlendAlpha2</c>: the colour with its alpha scaled by alpha / 256.</summary>
    public static uint BlendAlpha2(uint col, int alpha) => (col & 0x00ffffff) | (unchecked((col >> 24) * (uint)alpha << 16) & 0xff000000);

    /// <summary><c>Range</c>: between a and b as t goes from 0 to 1.</summary>
    public static float Range(float t, float a, float b) => a + (b - a) * t;

    /// <summary><c>CosResize</c>: a cosine ease between a (t = 0) and b (t = 1).</summary>
    public static float CosResize(float t, float a, float b)
    {
        float c = MathF.Cos(t * 3.14159265358979f);
        float sq = MathF.Abs(c) is var m && m != 0f ? MathF.Sqrt(m) : 0f;
        float f = (c < 0f ? -1f : 1f) * sq / 2f + 0.5f;
        return (1f - f) * b + f * a;
    }

    /// <summary><c>FO(x)</c>: a front-end depth from thousandths.</summary>
    public static float FO(int x) => (float)X87.Div(x, 1000f);

    // --- CFrontEnd ------------------------------------------------------------------------------------------------

    /// <summary><c>GetShadowOffsetX</c>: <c>sinf(counter / 100) * 6</c>.</summary>
    public static float ShadowOffsetX(float counter) => (float)X87.Mul(MathF.Sin((float)X87.Div(counter, 100f)), 6f);

    /// <summary><c>GetShadowOffsetY</c>: <c>cosf(counter / 100) * 3</c>.</summary>
    public static float ShadowOffsetY(float counter) => (float)X87.Mul(MathF.Cos((float)X87.Div(counter, 100f)), 3f);

    /// <summary><c>CFrontEnd::DrawLine</c>: the level-link texture stretched between the points, centred and turned.</summary>
    public void DrawLine(float sx, float sy, float ex, float ey, uint col, float width, float depth, float perc)
    {
        float dx = ex - sx, dy = ey - sy;
        float cx = (sx + ex) / 2f, cy = (sy + ey) / 2f;
        float yaw = MathF.Atan2(dy, dx);
        float xscale = MathF.Sqrt(dx * dx + dy * dy) / 32f * perc;
        float yscale = width / 32f / 2f;
        DrawColouredSprite(cx, cy, depth, FrontEndTexture.LevelLink, col, xscale, yscale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
    }

    // --- CDXSpriteRenderer ----------------------------------------------------------------------------------------

    /// <summary>
    /// <c>DrawColouredSprite</c> / <c>DrawSprite</c>: the texture at its real size times the scales, aligned on (x, y), as
    /// a quad (filled or scrolled by <paramref name="a"/> for the fill and scroll types), turned by <paramref name="yaw"/>
    /// about its centre, scaled from the virtual screen to the window. A texture the installation lacks draws nothing.
    /// </summary>
    public void DrawColouredSprite(float x, float y, float z, FrontEndTexture texture, uint col, float xscale = 1f,
        float yscale = 1f, SpriteAlign align = SpriteAlign.TopLeft, SpriteType type = SpriteType.None, float a = 1f, float yaw = 0f,
        float u0 = 0f, float u1 = 1f, float v0 = 0f, float v1 = 1f, bool clamp = false)
    {
        if (Textures.Size(texture) is not { } size) return;
        DrawSprite(QuadTexture.Fed(texture), size, x, y, z, col, xscale, yscale, align, type, a, yaw, u0, u1, v0, v1, clamp);
    }

    /// <summary>
    /// <c>CDXSpriteRenderer::DrawSprite</c> with a texture found by name (the pause menus' and message log's); nothing
    /// when the installation lacks it.
    /// </summary>
    public void DrawNamedSprite(string name, float x, float y, float z, uint col, float xscale = 1f, float yscale = 1f,
        SpriteAlign align = SpriteAlign.TopLeft, SpriteType type = SpriteType.None, float a = 1f, float yaw = 0f,
        float u0 = 0f, float u1 = 1f, float v0 = 0f, float v1 = 1f, bool clamp = false)
    {
        if (Textures.NamedSize(name) is not { } size) return;
        DrawSprite(QuadTexture.Find(name), size, x, y, z, col, xscale, yscale, align, type, a, yaw, u0, u1, v0, v1, clamp);
    }

    /// <summary>
    /// <c>RenderMouseCursor</c> (0x00523a70), at the end of every front-end render: the cursor texture at a quarter size
    /// from (MOUSE_X, MOUSE_Y), its last texel row and column left out, in white at <c>MOUSE_CURSOR_ALPHA</c>.
    /// </summary>
    public void DrawMouseCursor(int x, int y, byte alpha)
    {
        if (Textures.CursorSize is not { } size) return;
        DrawSprite(new QuadTexture(CanvasTextureKind.Cursor), size, x, y, 0.001f, ((uint)alpha << 24) | 0xffffff, 0.25f, 0.25f,
            SpriteAlign.TopLeft, SpriteType.None, 1f, 0f, 0f, 0.96875f, 0f, 0.96875f, false);
    }

    private void DrawSprite(QuadTexture texture, (int Width, int Height) size, float x, float y, float z, uint col, float xscale,
        float yscale, SpriteAlign align, SpriteType type, float a, float yaw, float u0, float u1, float v0, float v1, bool clamp)
    {
        if (z > 0.996f) z = (z - 0.996f) * 0.5f + 0.996f;
        float w = size.Width * xscale;
        float h = size.Height * yscale;
        switch ((int)align)
        {
            case 1: x -= w; break;
            case 2: y -= h; break;
            case 3: x -= w; y -= h; break;
            case 4: x -= w * 0.5f; y -= h * 0.5f; break;
            case 5: x -= w * 0.5f; y -= h; break;
            case 6: x -= w * 0.5f; break;
            case 7: x -= w; y -= h * 0.5f; break;
            case 8: y -= h * 0.5f; break;
        }

        float rhw = 1f / z;
        if (rhw > 4f) rhw = 4f;
        var v = new (float X, float Y, float U, float V)[4];
        for (int i = 0; i < 4; i++)
        {
            bool right = (i & 1) != 0, bottom = (i & 2) != 0;
            float vx = x, vy = y + (bottom ? h : 0f);
            float tu = right ? u1 : u0, tv = bottom ? v1 : v0;
            switch (type)
            {
                case SpriteType.None:
                    vx += right ? w : 0f;
                    break;
                case SpriteType.FillRight:
                    vx += right ? w * a : 0f;
                    tu = right ? a : 0f;
                    break;
                case SpriteType.FillLeft:
                    vx = w + x - (right ? 0f : w * a);
                    tu = right ? 0f : a;
                    break;
                case SpriteType.ScrollV:
                    vx += right ? w : 0f;
                    tv += (float)(a % 1.0);
                    break;
                case SpriteType.ScrollRight:
                    vx += right ? w : 0f;
                    tu += (float)(a % 1.0);
                    break;
            }
            v[i] = (vx, vy, tu, tv);
        }

        if (yaw != 0f)
        {
            float hw = w * 0.5f, ox = hw + x, hh = h * 0.5f, oy = hh + y;
            float c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw);
            for (int i = 0; i < 4; i++)
            {
                float px = v[i].X - ox, py = v[i].Y - oy;
                v[i] = (c * px - s * py + hw + x, s * px + c * py + hh + y, v[i].U, v[i].V);
            }
        }

        // The front end is running: the virtual screen is scaled to the window.
        const float sx = WindowWidth * (1f / 640f), sy = WindowHeight * (1f / 480f);
        CanvasVertex Vertex(int i) => new(v[i].X * sx, v[i].Y * sy, z, rhw, col, v[i].U, v[i].V);
        Canvas.Quad(new CanvasQuad(texture, Blend, true, ZAlways, Vertex(0), Vertex(1), Vertex(2), Vertex(3)) { Clamp = clamp });
    }

    // --- CDXBitmapFont --------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>ModulateColour</c>: font colours are authored for a modulate-2x blend; the canvas modulates plainly, so they are
    /// doubled (each channel clamped), as the game does when the card lacks modulate 2x.
    /// </summary>
    public static uint ModulateColour(uint c)
    {
        uint r = Math.Min((c >> 15) & 0x1fe, 0xffu), g = Math.Min((c >> 7) & 0x1fe, 0xffu), b = Math.Min((c & 0xff) << 1, 0xffu);
        return (c & 0xff000000) + (r << 16) + (g << 8) + b;
    }

    /// <summary>
    /// <c>DrawTextScaled</c>: a quad per character from the cell's texture coordinates, at its scaled size, half a pixel
    /// up and left; a newline returns to the start a row lower. Without flag 8 the depth test is off; without flag 0x10
    /// y is floored. Positions and scales go from the virtual screen to the window first.
    /// </summary>
    public void DrawTextScaled(BitmapFont font, CanvasTextureKind kind, float x, float y, float z, float scalex, float scaley,
        uint colour, string text, int flags, ReadOnlySpan<uint> colours = default)
    {
        int count = 0;
        foreach (char ch in text)
        {
            if (ch == '\0') break;
            if (ch != '\r' && ch != '\n') count++;
        }
        if (count == 0 || count > 2500) return;

        x = WindowWidth * x / 640f;
        y = WindowHeight * y / 480f;
        scalex = WindowWidth * scalex / 640f;
        scaley = WindowHeight * scaley / 480f;
        bool zEnable = (flags & 8) != 0;
        if ((flags & 0x10) == 0) y = MathF.Floor(y);
        float sx = MathF.Floor(x), sy = y, startX = sx;
        float lineHeight = (font.TexCoord(0, 3) - font.TexCoord(0, 1)) * font.TexHeight;
        int k = 0;
        foreach (char ch in text)
        {
            if (ch == '\0') break;
            if (ch == '\n')
            {
                sx = startX;
                sy += lineHeight * scaley;
                continue;
            }
            if (ch == '\r') continue;
            int cell = font.RemapChar(ch);
            float tx1 = font.TexCoord(cell, 0), ty1 = font.TexCoord(cell, 1), tx2 = font.TexCoord(cell, 2), ty2 = font.TexCoord(cell, 3);
            float w = (tx2 - tx1) * font.TexWidth * scalex;
            float h = (ty2 - ty1) * font.TexHeight * scaley;
            uint col = ModulateColour(colours.IsEmpty ? colour : colours[k++]);
            float x1 = sx - 0.5f, x2 = sx + w - 0.5f, y1 = sy - 0.5f, y2 = sy + h - 0.5f;
            Canvas.Quad(new CanvasQuad(new QuadTexture(kind), Blend, zEnable, ZAlways,
                new(x1, y1, z, 1f, col, tx1, ty1), new(x2, y1, z, 1f, col, tx2, ty1),
                new(x1, y2, z, 1f, col, tx1, ty2), new(x2, y2, z, 1f, col, tx2, ty2)));
            sx += w + scalex;
        }
    }

    /// <summary>
    /// <c>DrawTextDynamic</c>: the text revealed from <paramref name="reveal"/> (each character 0.01 behind the last; fading
    /// in from white over 0.25, to the colour over the next 0.25, fading out after with <paramref name="fadeOut"/>), drawn
    /// twice: a shadow one pixel down and right, then the text.
    /// </summary>
    public void DrawTextDynamic(BitmapFont font, CanvasTextureKind kind, float x, float y, float z, float scalex, float scaley,
        uint colour, string text, float reveal, bool fadeOut = false, int flags = 0)
    {
        int len = text.IndexOf('\0') is var nul && nul >= 0 ? nul : text.Length;
        if (len > 1000) len = 1000;
        uint alpha = colour >> 24;
        var buf = text.ToCharArray(0, len);
        var colours = new uint[len];
        var shadows = new uint[len];
        uint any = 0;
        int shown = len;
        for (int i = 0; i < len; i++)
        {
            if (reveal < 0f)
            {
                shown = i;
                break;
            }
            uint col;
            if (reveal < 0.25f)
            {
                uint a = (uint)X87.Fist(X87.Mul(X87.Mul(reveal, 4f), 255f)) * alpha >> 8;
                col = (a << 24) + 0xffffff;
            }
            else if (reveal < 0.5f)
            {
                float f = (reveal - 0.25f) * 4f;
                float w = (1f - f) * 255f;
                col = alpha;
                col = (col << 8) + (uint)X87.Fist((int)((colour >> 16) & 0xff) * f + w);
                col = (col << 8) + (uint)X87.Fist((int)((colour >> 8) & 0xff) * f + w);
                col = (col << 8) + (uint)X87.Fist((int)(colour & 0xff) * f + w);
            }
            else if (fadeOut)
            {
                col = reveal < 15f ? ((uint)X87.Fist((1f - (reveal - 0.5f) * (1f / 14.5f)) * alpha) << 24) + (colour & 0xffffff) : 0;
            }
            else
            {
                col = colour;
            }
            colours[i] = col;
            shadows[i] = (alpha << 24) & col;
            any |= col;
            reveal -= 0.01f;
        }
        if (any == 0) return;
        string shownText = new(buf, 0, shown);
        DrawTextScaled(font, kind, x + 1f, y + 1f, z, scalex, scaley, colour, shownText, flags, shadows);
        DrawTextScaled(font, kind, x, y, z, scalex, scaley, colour, shownText, flags, colours);
    }

    /// <summary><c>DrawTextWithShadow</c> (0x004659a0): the text in the colour's alpha one pixel down and right, then the text.</summary>
    public void DrawTextWithShadow(BitmapFont font, QuadTexture texture, float x, float y, uint colour, string text, int flags = 0,
        float z = 1f, float scalex = 1f, float scaley = 1f)
    {
        DrawTextScaled(font, texture.Kind, x + 1f, y + 1f, z, scalex, scaley, colour & 0xff000000, text, flags);
        DrawTextScaled(font, texture.Kind, x, y, z, scalex, scaley, colour, text, flags);
    }

    /// <summary><c>CFrontEnd::DrawPanel</c> (0x00467010): the blank texture stretched over the rectangle, its edges clamped.</summary>
    public void DrawPanel(float tlx, float tly, float brx, float bry, float z, uint col) =>
        DrawColouredSprite(tlx, tly, z, FrontEndTexture.Blank, col, (brx - tlx) / 16f, (bry - tly) / 16f, clamp: true);

    /// <summary><c>CFrontEnd::DrawBarGraph</c> (0x004670b0): the background panel, and the filled part over it.</summary>
    public void DrawBarGraph(float tlx, float tly, float brx, float bry, float num, float max, float z, uint backcol, uint forecol)
    {
        DrawPanel(tlx, tly, brx, bry, z, backcol);
        if (num != 0f) DrawPanel(tlx, tly, tlx + (brx - tlx) * (num / max), bry, z, forecol);
    }

    /// <summary>
    /// <c>CFrontEnd::DrawBar</c> (0x00467ae0): a left end, <paramref name="segs"/> middles and a right end, 64 pixels each
    /// at the scale, centred on (sx, sy), edges clamped.
    /// </summary>
    public void DrawBar(float sx, float sy, float z, int segs, uint col, float scale)
    {
        float y = sy - 32f * scale;
        float x = sx - 32f * (segs + 2) * scale;
        for (int c0 = 0; c0 < segs + 2; c0++)
        {
            FrontEndTexture tex = c0 == 0 ? FrontEndTexture.Barl : c0 == segs + 1 ? FrontEndTexture.Barr : FrontEndTexture.Barc;
            DrawColouredSprite(x, y, z, tex, col, scale, scale, clamp: true);
            x += 64f * scale;
        }
    }

    /// <summary><c>CFrontEnd::DrawBox</c> (0x00466e70): four lines round the rectangle.</summary>
    public void DrawBox(float tlx, float tly, float brx, float bry, uint col, float width, float depth)
    {
        DrawLine(tlx, tly, brx, tly, col, width, depth, 1f);
        DrawLine(brx, tly, brx, bry, col, width, depth, 1f);
        DrawLine(brx, bry, tlx, bry, col, width, depth, 1f);
        DrawLine(tlx, bry, tlx, tly, col, width, depth, 1f);
    }

    // --- CDXFrontEndVideo -----------------------------------------------------------------------------------------

    /// <summary>
    /// <c>CDXFrontEndVideo::Render</c> while no video is open (every -skipfmv run: the open thread opens nothing): the
    /// default texture at the requested 128 x 128, its colour reduced to the alpha, centred when asked.
    /// </summary>
    public void VideoWithoutFilm(float x, float y, float z, float xscale, float yscale, uint colour, bool centred, int width = 128, int height = 128)
    {
        if (z == 0f) z = 0.0001f;
        // DrawPrimitiveUP with the current blend (the front end's modulate), alpha test off.
        colour &= 0xff000000;
        float w = width * xscale, h = height * yscale;
        if (centred)
        {
            x -= w * 0.5f;
            y -= h * 0.5f;
        }
        Canvas.Quad(new CanvasQuad(new QuadTexture(CanvasTextureKind.Default), CanvasBlend.Modulate, true, ZAlways,
            new(x, y, z, 1f, colour, 0f, 0f), new(x + w, y, z, 1f, colour, 1f, 0f),
            new(x, y + h, z, 1f, colour, 0f, 1f), new(x + w, y + h, z, 1f, colour, 1f, 1f)));
    }
}

/// <summary><c>ESpriteAlignment</c>: where (x, y) sits on the sprite.</summary>
public enum SpriteAlign
{
    TopLeft = 0,
    Centre = 4,
    LeftMiddle = 8,
}

/// <summary><c>ESpriteType</c>.</summary>
public enum SpriteType
{
    None = 0,
    FillRight = 1,
    FillLeft = 2,
    ScrollV = 3,
    ScrollRight = 4,
}
