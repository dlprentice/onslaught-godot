// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEMessBox.cpp and src/FEMessBox.h (reconstructed from the retail program; not in
// Stuart's release).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary><c>EFEMBOption</c>: the box's buttons.</summary>
public enum MessageBoxOption
{
    /// <summary>No buttons: dismissed by its timeout.</summary>
    None = 0,
    Ok,
    YesNo,
    AContinue,
}

/// <summary>The questions a box asks (<c>FEMB_Q_*</c>) and the answers (<c>FEMB_NO</c>, <c>FEMB_YES</c>).</summary>
public static class MessageBoxQuestion
{
    public const int NewCareer = 1;
    public const int LoadCareer = 2;
    public const int Overwrite = 3;
    public const int DeleteSaves = 4;
    public const int DeleteSave = 6;
    public const int NoAutoSave = 7;
    public const int Language = 8;
    public const int Format = 9;
    public const int Quit = 11;

    public const int No = 0;
    public const int Yes = 1;
}

/// <summary>
/// The front end's message box (<c>FEMESSBOX</c>): created with a question and its buttons, it fades in, waits (for a
/// choice, or its timeout when it has no buttons), fades out and closes. While it has buttons it takes the front end's
/// button actions. A box created with its layout (position, width, text and font) also takes mouse clicks in its render;
/// its alpha and drawing are presentation.
/// </summary>
public sealed class FrontEndMessageBox(FrontEnd frontEnd) : IFrontEndMessageBox
{
    public bool BeingDisplayed { get; private set; }
    public MessageBoxOption Option { get; private set; }
    public int Question { get; private set; }
    public int Cursor { get; private set; } = -1;

    /// <summary><c>mFadeState</c>: 0 fading in, 1 shown, 2 fading out.</summary>
    public int FadeState { get; set; }

    /// <summary><c>mFadeCount</c> and <c>mFadeTime</c>, in fiftieths of a second of frame step.</summary>
    public float FadeCount { get; set; }
    public float FadeTime { get; private set; }

    /// <summary><c>mTimeout</c> and <c>mTime</c>: how long a box without buttons stays.</summary>
    public float Timeout { get; private set; }
    public float Time { get; private set; }

    /// <summary><c>mUnknown1f90</c>: Create's ninth argument, a box that closes when <see cref="Progress"/> reaches 1.</summary>
    public bool ClosesOnProgress { get; private set; }

    /// <summary><c>mUnknown1f94</c>: that progress, set by the box's owner.</summary>
    public float Progress { get; set; }

    /// <summary><c>mOnDismissPage</c> and <c>mOnDismissTime</c>: a page to go to when the box is dismissed.</summary>
    public FrontEndPage OnDismissPage { get; set; } = FrontEndPage.None;
    public int OnDismissTime { get; private set; }

    private int _finalChoice = -1;

    /// <summary><c>mAlpha</c>: the box's fade, which only its drawing reads.</summary>
    public int Alpha { get; private set; }

    public bool IsInteractive => BeingDisplayed && Option != MessageBoxOption.None;

    /// <summary><c>CFEMessBox::Create</c> (0x0044d390): ignored while a box is displayed. A fade time of 0 shows it at once.</summary>
    public void Create(uint timeout, uint fadeTime, bool closesOnProgress, MessageBoxOption option, int question)
    {
        if (BeingDisplayed) return;
        BoxLayout = null;
        Cursor = -1;
        _finalChoice = -1;
        ClosesOnProgress = closesOnProgress;
        Timeout = timeout;
        FadeTime = fadeTime;
        Option = option;
        Progress = 0f;
        Question = question;
        Time = 0f;
        FadeCount = 0f;
        FadeState = 0;
        if (fadeTime == 0)
        {
            FadeState = 1;
            Alpha = 0xff;
        }
        if (ClosesOnProgress) Option = MessageBoxOption.None;
        else if (option == MessageBoxOption.YesNo) Cursor = 0;
        BeingDisplayed = true;
    }

    /// <summary>
    /// The layout of a box created with its text: centre, width, wrapped lines, line height and height; the depth it is
    /// drawn at and the font its lines are drawn in.
    /// </summary>
    public sealed record Layout(float X, float Y, float Width, IReadOnlyList<string> Lines, int LineHeight, float Height,
        float Z = 0.1f, Text.BitmapFont? Font = null);

    /// <summary>The current box's layout, or null when it was created without one (its render then takes no clicks).</summary>
    public Layout? BoxLayout { get; private set; }

    /// <summary>
    /// <c>CFEMessBox::Create</c> (0x0044d390) with its layout: the text wraps at the width less 16 in
    /// <paramref name="font"/>; the height is the lines plus 16, plus one normal-font row for an OK or continue box,
    /// two for yes and no, or 32 for a progress box.
    /// </summary>
    public void Create(float x, float y, float width, string text, Text.BitmapFont font, Text.BitmapFont normalFont,
        uint timeout, uint fadeTime, bool closesOnProgress, MessageBoxOption option, int question, float z = 0.1f)
    {
        if (BeingDisplayed) return;
        Create(timeout, fadeTime, closesOnProgress, option, question);
        List<string> lines = font.WordWrap(text, (float)X87.Sub(width, 16f));
        int lineHeight = font.Height;
        // x87: float(mLineHeight) * mNumLines + 16, then the button rows' heights, each rounded and stored.
        float height = (float)X87.Add(X87.Mul(lineHeight, lines.Count), 16f);
        if (closesOnProgress) height = (float)X87.Add(height, 32f);
        else if (option is MessageBoxOption.Ok or MessageBoxOption.AContinue) height = (float)X87.Add(height, normalFont.Height);
        else if (option == MessageBoxOption.YesNo) height = (float)X87.Add(height, normalFont.Height * 2);
        BoxLayout = new Layout(x, y, width, lines, lineHeight, height, z, font);
    }

    public void Kill() => BeingDisplayed = false;

    /// <summary>
    /// The state-changing part of <c>CFEMessBox::Render</c> (0x0044d6f0), after the pages' render, for a box with a
    /// layout. An OK or continue box is clicked anywhere inside. Yes and no are rows of the normal font's height below
    /// the text (yes first); in each, hovering moves the cursor (and then the click is not tested that frame), and a
    /// click answers and starts the fade-out. The box uses the plain mouse tests, which the box's own interactivity does
    /// not gate. Its front-end texts are read in retail's order (each read checks the X key).
    /// </summary>
    public void Render(Text.BitmapFont normalFont, Text.FrontEndTexts texts)
    {
        if (!BeingDisplayed || BoxLayout is not { } layout) return;
        Draw.FrontEndDraw? draw = frontEnd.Draw;
        Platform.PlatformInput platform = frontEnd.Platform;
        float halfWidth = (float)X87.Mul(layout.Width, 0.5f), halfHeight = (float)X87.Mul(layout.Height, 0.5f);
        float left = (float)X87.Sub(layout.X, halfWidth), right = (float)X87.Add(layout.X, halfWidth);
        float top = (float)X87.Sub(layout.Y, halfHeight), bottom = (float)X87.Add(layout.Y, halfHeight);
        uint back = Faded(0xaf000000), text = Faded(0xffffffff), grey = Faded(0xff7f7f7f), highlight = Faded(0xaf40ff40);
        var normal = new Draw.QuadTexture(Draw.CanvasTextureKind.NormalFont);
        draw?.DrawPanel(left, top, right, bottom, layout.Z, back);
        if ((Option is MessageBoxOption.Ok or MessageBoxOption.AContinue or (MessageBoxOption)4)
            && platform.Mouse.ClickedInRect(left, top, right, bottom, platform.KeyTrap))
            ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        if (draw is not null)
        {
            draw.DrawBox(left, top, right, bottom, grey, 2f, layout.Z);
            if (layout.Font is { } font)
            {
                var kind = new Draw.QuadTexture(ReferenceEquals(font, frontEnd.NormalFont) ? Draw.CanvasTextureKind.NormalFont : Draw.CanvasTextureKind.SmallFont);
                for (int c0 = 0; c0 < layout.Lines.Count; c0++)
                {
                    int cx = font.GetTextExtent(layout.Lines[c0]).Width;
                    draw.DrawTextWithShadow(font, kind, layout.X - cx / 2, layout.LineHeight * c0 + top + 8f, text, layout.Lines[c0]);
                }
            }
        }
        if (ClosesOnProgress) return;
        switch (Option)
        {
            case MessageBoxOption.Ok:
            case MessageBoxOption.AContinue:
            {
                string ok = texts.GetText(Text.FrontEndText.Ok);
                (int cx, int cy) = normalFont.GetTextExtent(ok);
                float bx = layout.X - cx / 2 - 4f, y = layout.LineHeight * layout.Lines.Count + top + 8f;
                draw?.DrawPanel(bx, y, bx + cx + 8f, y + cy, layout.Z, highlight);
                draw?.DrawTextWithShadow(normalFont, normal, layout.X - cx / 2, y, text, texts.GetText(Text.FrontEndText.Ok));
                break;
            }
            case MessageBoxOption.YesNo:
            {
                (int sx, int rowHeight) = normalFont.GetTextExtent(texts.GetText(Cursor != 0 ? Text.FrontEndText.Yes : Text.FrontEndText.No));
                if (draw is not null)
                {
                    float bx = layout.X - sx / 2 - 4f;
                    float hy = (float)X87.Add(X87.Add(X87.Add((1 - Cursor) * rowHeight, layout.LineHeight * layout.Lines.Count), top), 8f);
                    draw.DrawPanel(bx, hy, bx + sx + 8f, hy + rowHeight, layout.Z, highlight);
                }
                for (int c0 = 0; c0 < 2; c0++)
                {
                    // x87: the int products, then the adds rounded to 24 bits (0044da24, 0044da97, 0044dc27).
                    float y = (float)X87.Add(X87.Add(X87.Add((1 - c0) * rowHeight, layout.LineHeight * layout.Lines.Count), top), 8f);
                    float yBottom = (float)X87.Add(y, rowHeight);
                    if (platform.Mouse.OverRect(left, y, right, yBottom, platform.KeyTrap))
                    {
                        Cursor = c0;
                        break;
                    }
                    if (platform.Mouse.ClickedInRect(left, y, right, yBottom, platform.KeyTrap))
                    {
                        frontEnd.PlaySound(FrontEndSound.Select);
                        Cursor = c0;
                        _finalChoice = c0;
                        StartFadeOut();
                        break;
                    }
                }
                string yes = texts.GetText(Text.FrontEndText.Yes);
                int yesWidth = normalFont.GetTextExtent(yes).Width;
                string yesText = texts.GetText(Text.FrontEndText.Yes);
                draw?.DrawTextWithShadow(normalFont, normal, layout.X - yesWidth / 2, layout.LineHeight * layout.Lines.Count + top + 8f, text, yesText);
                string no = texts.GetText(Text.FrontEndText.No);
                (int noWidth, int noHeight) = normalFont.GetTextExtent(no);
                string noText = texts.GetText(Text.FrontEndText.No);
                draw?.DrawTextWithShadow(normalFont, normal, layout.X - noWidth / 2,
                    (float)X87.Add(X87.Add(X87.Add(layout.LineHeight * layout.Lines.Count, noHeight), top), 8f), text, noText);
                break;
            }
        }
    }

    // The box's colours at its fade: the alpha scaled by mAlpha / 256 (ScaledAlphaBits), the colour kept.
    private uint Faded(uint col) => unchecked(((col >> 24) * (uint)Alpha & 0xffffff00) * 0x10000) | (col & 0x00ffffff);

    /// <summary>
    /// <c>CFEMessBox::Init</c> (0x0044d320), at each front-end start: no cursor or choice, times and fades zeroed, not
    /// displayed, no page on dismissal. The question, option and fade state are left as they were.
    /// </summary>
    public void Init()
    {
        Cursor = -1;
        _finalChoice = -1;
        Time = 0f;
        Timeout = 0f;
        FadeCount = 0f;
        FadeTime = 0f;
        BeingDisplayed = false;
        OnDismissPage = FrontEndPage.None;
    }

    /// <summary><c>GetFinalChoice</c>: the answer, or -1; reading it resets it.</summary>
    public int GetFinalChoice()
    {
        int choice = _finalChoice;
        _finalChoice = -1;
        return choice;
    }

    public void SetOnDismiss(FrontEndPage page, int time)
    {
        OnDismissPage = page;
        OnDismissTime = time;
    }

    public void StartFadeOut()
    {
        FadeCount = 0f;
        FadeState = 2;
        OnDismissPage = FrontEndPage.None;
    }

    /// <summary>
    /// <c>CFEMessBox::Process</c> (0x0044d560): fading in by the frame step, then shown (a box without buttons counts
    /// down its timeout; a progress box waits for progress 1), then fading out by one per frame until closed. x87: the
    /// step products and sums round to 24 bits and are stored to float members.
    /// </summary>
    public void Process()
    {
        if (!BeingDisplayed) return;
        switch (FadeState)
        {
            case 0:
                FadeCount = (float)X87.Add(FadeCount, X87.Mul(frontEnd.Timing.FrameStep, 50f));
                if (FadeCount >= FadeTime) FadeState = 1;
                Alpha = Draw.FrontEndDraw.MakeAlpha((float)X87.Div(FadeCount, FadeTime));
                break;
            case 1:
                if (ClosesOnProgress)
                {
                    if (Progress >= 1f)
                    {
                        FadeState = 2;
                        Alpha = 0xff;
                        break;
                    }
                }
                else if (Option == MessageBoxOption.None)
                {
                    Time = (float)X87.Add(Time, X87.Mul(frontEnd.Timing.FrameStep, 50f));
                    if (Time >= Timeout)
                    {
                        FadeCount = 0f;
                        FadeState = 2;
                        OnDismissPage = FrontEndPage.None;
                    }
                }
                Alpha = 0xff;
                break;
            case 2:
                FadeCount = (float)X87.Add(FadeCount, 1.0);
                if (FadeCount >= FadeTime)
                {
                    FadeState = 0;
                    BeingDisplayed = false;
                    break;
                }
                Alpha = Draw.FrontEndDraw.MakeAlpha((float)X87.Sub(1f, X87.Div(FadeCount, FadeTime)));
                break;
        }
    }

    /// <summary><c>CFEMessBox::ButtonPressed</c> (0x0044dd60): only while shown.</summary>
    public void ButtonPressed(int button, float value)
    {
        if (FadeState != 1 || !BeingDisplayed) return;
        switch (Option)
        {
            case MessageBoxOption.AContinue:
            case (MessageBoxOption)4:
                if (button == FrontEndButtons.MenuSelect)
                {
                    frontEnd.PlaySound(FrontEndSound.Select);
                    if (OnDismissPage != FrontEndPage.None) frontEnd.SetPage(OnDismissPage, OnDismissTime);
                    _finalChoice = 1;
                    StartFadeOut();
                }
                break;
            case MessageBoxOption.Ok:
                if (button == FrontEndButtons.MenuSelect)
                {
                    frontEnd.PlaySound(FrontEndSound.Select);
                    if (OnDismissPage != FrontEndPage.None) frontEnd.SetPage(OnDismissPage, OnDismissTime);
                    StartFadeOut();
                }
                break;
            case MessageBoxOption.YesNo:
                if (button == FrontEndButtons.MenuUp)
                {
                    frontEnd.PlaySound(FrontEndSound.Move);
                    Cursor = 1;
                }
                else if (button == FrontEndButtons.MenuDown)
                {
                    frontEnd.PlaySound(FrontEndSound.Move);
                    Cursor = 0;
                }
                else if (button == FrontEndButtons.MenuSelect)
                {
                    frontEnd.PlaySound(FrontEndSound.Select);
                    _finalChoice = Cursor;
                    StartFadeOut();
                }
                break;
        }
    }
}
