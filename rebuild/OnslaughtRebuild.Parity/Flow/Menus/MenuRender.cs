// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/MenuItem.cpp (CMenuItem::IsMouseInRect 004a3100, MouseClickedInRect 004a3120; the statics
// CMenuMenuItem::mRenderOnTop/X/Y 0070486c..00704874 and gMenuChangesPending 00704a88), src/MessageLog.cpp
// (CMessageLog::RenderPanel 004b9010) and src/PauseMenu.cpp (the render states CPauseMenu::Render 004d11d0 sets).

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Text;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>
/// The mouse as the menus' renders read it: <c>CFrontEnd</c>'s tests (nothing while the front end's message box is
/// interactive) and the plain ones the controls page's bindings use.
/// </summary>
public interface IMenuMouse
{
    /// <summary>The cursor moved this frame or a button was released: a test could pass.</summary>
    bool Active { get; }

    /// <summary><c>CFrontEnd::GetCursorStateInRect</c> (<c>CMenuItem::IsMouseInRect</c>).</summary>
    bool CursorInRect(float left, float top, float right, float bottom);

    /// <summary><c>CFrontEnd::MouseClickedInRect</c> (<c>CMenuItem::MouseClickedInRect</c>): a left click inside, consumed.</summary>
    bool ClickedInRect(float left, float top, float right, float bottom);

    /// <summary>
    /// <c>!FEMESSBOX.IsInteractive() &amp;&amp; MOUSE_BUTTONS[2].mDown</c>, and when so the button's down cleared (an open
    /// list's close).
    /// </summary>
    bool TakeRightButtonDown();
}

/// <summary>The front end's mouse for the menus (the options page's).</summary>
public sealed class FrontEndMenuMouse(FrontEnd frontEnd) : IMenuMouse
{
    public bool Active
    {
        get
        {
            Platform.Mouse m = frontEnd.Platform.Mouse;
            return m.Moved || m.Buttons.Any(b => b.Released || b.Down);
        }
    }

    public bool CursorInRect(float left, float top, float right, float bottom) =>
        frontEnd.GetCursorStateInRect(left, top, right, bottom);

    public bool ClickedInRect(float left, float top, float right, float bottom) =>
        frontEnd.MouseClickedInRect(left, top, right, bottom);

    public bool TakeRightButtonDown()
    {
        Platform.MouseButton right = frontEnd.Platform.Mouse.Buttons[2];
        if (frontEnd.MessageBox.IsInteractive || !right.Down) return false;
        right.Down = false;
        return true;
    }
}

/// <summary>
/// What the menus measure and draw with: the small and normal fonts, the texts, and where to draw (null: nothing drawn,
/// the measuring still available to the mouse tests).
/// </summary>
/// <param name="OnOptionsPage"><c>FRONTEND.GetCurrentPage() == FEP_OPTIONS</c>: the dropdowns' blue background.</param>
public sealed record MenuDrawing(BitmapFont Small, BitmapFont Normal, FrontEndTexts Texts, FrontEndDraw? Draw, bool OnOptionsPage)
{
    /// <summary>The front end's, when it has its fonts and texts.</summary>
    public static MenuDrawing? Of(FrontEnd frontEnd) =>
        frontEnd.SmallFont is { } small && frontEnd.NormalFont is { } normal && frontEnd.Texts is { } texts
            ? new MenuDrawing(small, normal, texts, frontEnd.Draw, frontEnd.ActivePage == FrontEndPage.Options)
            : null;
}

/// <summary>A list's title: a text.stf text (<c>TEXT.GetString</c>) or, with text 0, a PC text.</summary>
public readonly record struct MenuTitle(int Text, int PCText)
{
    public string Resolve(FrontEndTexts texts) => Text != 0 ? texts.ById((uint)Text) : texts.PcText(PCText);
}

/// <summary>
/// One render of a menu: the clock, the optional measuring/drawing and mouse, and the statics retail keeps across a list's
/// render (the expanded list drawn last, the pending-changes flag).
/// </summary>
public sealed class MenuRender(IMenuHost host, bool optionsPage, MenuDrawing? drawing, IMenuMouse? mouse)
{
    /// <summary>The selected item's names in retail's drawing (front-end colours: the selection and a disabled item).</summary>
    public const uint SelectedColour = 0xffffcc00;

    public IMenuHost Host { get; } = host;

    public PlatformClock Clock { get; } = host.Clock;

    /// <summary>CPauseMenu::mOptionsPage.</summary>
    public bool OptionsPage { get; } = optionsPage;

    public MenuDrawing? Drawing { get; } = drawing;

    public IMenuMouse? Mouse { get; } = mouse;

    public FrontEndDraw? Draw => Drawing?.Draw;

    /// <summary>CMenuMenuItem::mRenderOnTop, mRenderOnTopX, mRenderOnTopY: the selected list, drawn after the others.</summary>
    internal MenuMenuItem? RenderOnTop { get; set; }

    internal float RenderOnTopX { get; set; }

    internal float RenderOnTopY { get; set; }

    /// <summary>gMenuChangesPending: a deferred item of the list has a choice not yet applied.</summary>
    public bool ChangesPending { get; internal set; }

    /// <summary>The fonts and texts a mouse test needs; a run without them cannot place the test.</summary>
    internal MenuDrawing Metrics => Drawing ?? throw new NotSupportedException(
        "the menus' mouse tests need the small font's metrics and the texts");

    /// <summary>The mouse could hit something this frame.</summary>
    internal bool MouseActive => Mouse is { Active: true };

    internal bool CursorInRect(float left, float top, float right, float bottom) =>
        Mouse is { } m && m.CursorInRect(left, top, right, bottom);

    internal bool ClickedInRect(float left, float top, float right, float bottom) =>
        Mouse is { } m && m.ClickedInRect(left, top, right, bottom);

    /// <summary>A PC text (<c>Localization_GetStringById</c>).</summary>
    internal string Pc(int id) => Metrics.Texts.PcText(id);

    /// <summary>A text.stf text (<c>TEXT.GetString</c>).</summary>
    internal string Text(int id) => Metrics.Texts.ById((uint)id);

    /// <summary>The small font's extent.</summary>
    internal (int Width, int Height) SmallExtent(string text) => Metrics.Small.GetTextExtent(text);

    /// <summary><c>PLATFORM.Font(FONT_SMALL)->DrawTextWithShadow(x, y, colour, text, 0, 0, scalex, scaley)</c>.</summary>
    internal void SmallTextWithShadow(float x, float y, uint colour, string text, float scalex = 1f, float scaley = 1f)
    {
        if (Drawing is { Draw: { } draw } d)
            draw.DrawTextWithShadow(d.Small, new QuadTexture(CanvasTextureKind.SmallFont), x, y, colour, text, 0, 0f, scalex, scaley);
    }

    /// <summary>
    /// <c>CSPRITERENDERER::DrawSprite</c> of a texture found by name, as the menus draw it: with the depth test and writes
    /// off and the texture addressing clamped (CPauseMenu::Render's states).
    /// </summary>
    internal void Sprite(string name, float x, float y, float z, uint col, float xscale, float yscale,
        SpriteAlign align = SpriteAlign.TopLeft, float yaw = 0f, float u0 = 0f, float u1 = 1f, float v0 = 0f, float v1 = 1f)
    {
        if (Draw is not { } draw) return;
        IFrontEndCanvas canvas = draw.Canvas;
        draw.Canvas = new DepthOffCanvas(canvas);
        try
        {
            draw.DrawNamedSprite(name, x, y, z, col, xscale, yscale, align, SpriteType.None, 1f, yaw, u0, u1, v0, v1, clamp: true);
        }
        finally
        {
            draw.Canvas = canvas;
        }
    }

    /// <summary>
    /// <c>CMessageLog::RenderPanel</c> (004b9010): the four end curves at the corners, the blank texture along the edges and
    /// over the middle, all in black at alpha * 160, at least 64 square.
    /// </summary>
    internal void RenderPanel(int x, int y, int width, int height, float alpha)
    {
        if (Draw is null) return;
        int a = Numerics.X87.Fist(alpha * 160f);
        uint col = (uint)a << 24;
        if (width < 64) width = 64;
        if (height < 64) height = 64;
        const string curve = @"MessageLog\endcurve.tga";
        const float pi = 3.14159265358979f;
        float fy = y;
        Sprite(curve, x, fy, 0.004f, col, 1f, 1f, SpriteAlign.TopLeft, 0f, 1f, 0f, 0f, 1f);
        float rx = width + (float)x - 32f;
        Sprite(curve, rx, fy, 0.004f, col, 1f, 1f, SpriteAlign.TopLeft, 0f, 0f, 1f, 0f, 1f);
        float by = height + fy - 32f;
        Sprite(curve, rx, by, 0.004f, col, 1f, 1f, SpriteAlign.TopLeft, pi, 1f, 0f, 0f, 1f);
        Sprite(curve, x, by, 0.004f, col, 1f, 1f, SpriteAlign.TopLeft, pi, 0f, 1f, 0f, 1f);
        int w = width - 64, h = height - 64;
        if (w != 0)
        {
            Sprite(Blank, x + 32, fy, 0.004f, col, w * 0.0625f, 2f);
            Sprite(Blank, x + 32, y + height - 32, 0.004f, col, w * 0.0625f, 2f);
        }

        if (h != 0)
        {
            Sprite(Blank, x, y + 32, 0.004f, col, 2f, h * 0.0625f);
            Sprite(Blank, x + width - 32, y + 32, 0.004f, col, 2f, h * 0.0625f);
        }

        if (w != 0 && h != 0) Sprite(Blank, x + 32, y + 32, 0.004f, col, w * 0.0625f, h * 0.0625f);
    }

    /// <summary>The blank texture the lists, the scale bars and the dropdowns stretch.</summary>
    internal const string Blank = @"FrontEnd\v2\FE_Blank.tga";

    /// <summary>The scale bars' arrows.</summary>
    internal const string Arrow = @"FrontEnd\v2\FE_Arrow.tga";

    /// <summary>
    /// <c>(cos(float(fmod(t, 2.0)) * 2 PI) + 1) / 2</c> as a grey from white (255 - f * 255), the pulse of a pending choice.
    /// </summary>
    internal static uint PendingPulse(float time)
    {
        float f = ((float)Math.Cos((float)(time % 2.0) * (3.14159265358979f * 2f)) + 1f) * 0.5f;
        uint c = (uint)(255 - Numerics.X87.Fist(f * 255f));
        return 0xff000000 | (c << 16) | (c << 8) | c;
    }
}

/// <summary>
/// A canvas that passes quads on with the depth test (and so the depth writes) off: CPauseMenu::Render draws its sprites
/// under ZENABLE FALSE and ZWRITEENABLE FALSE. Belongs with <see cref="FrontEndDraw"/> as a depth-off sprite call.
/// </summary>
internal sealed class DepthOffCanvas(IFrontEndCanvas inner) : IFrontEndCanvas
{
    public void Clear(uint colour) => inner.Clear(colour);

    public void Quad(in CanvasQuad quad) => inner.Quad(quad with { ZEnable = false });

    public void Present(long present) => inner.Present(present);
}
