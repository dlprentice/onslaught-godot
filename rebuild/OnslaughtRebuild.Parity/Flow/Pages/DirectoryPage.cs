// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPDirectory.cpp and src/FEPDirectory.h (reconstructed from the retail program; not in
// Stuart's release).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The PC's list of saves (<c>CFEPDirectory</c>): pick one to load or, in save mode, to delete. The name keyboard draws
/// the same list beside it (<see cref="RenderSaveFileList"/>), where picking a name copies it.
/// </summary>
public sealed class DirectoryPage(FrontEnd frontEnd) : FrontEndPageBase
{
    public const int MaxSaves = 0x1000;
    private const int VisibleRows = 11;
    private const float Top = 130f, Left = 132f, Line = 24f;

    private readonly string?[] _saves = new string?[MaxSaves];

    public int Cursor { get; private set; }

    /// <summary><c>mUnknown400c</c>: the first row shown.</summary>
    public int FirstShown { get; private set; }

    public int NumSaves { get; private set; }
    public float Time { get; private set; }

    public string? Save(int index) => _saves[index];

    /// <summary><c>CFEPDirectory::Init</c> (0x0051aa90).</summary>
    public override bool Init()
    {
        Cursor = 0;
        FirstShown = 0;
        NumSaves = 0;
        Time = 0f;
        Array.Clear(_saves);
        return true;
    }

    /// <summary><c>CFEPDirectory::Shutdown</c> (0x0051aac0).</summary>
    public override void Shutdown() => Array.Clear(_saves);

    /// <summary><c>CFEPDirectory::ButtonPressed</c> (0x0051aaf0).</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuDown:
                if (Cursor < MaxSaves - 1 && _saves[Cursor + 1] is not null)
                {
                    frontEnd.PlaySound(FrontEndSound.Select);
                    Cursor++;
                }
                Time = frontEnd.Counter;
                break;
            case FrontEndButtons.MenuUp:
                if (Cursor > 0)
                {
                    frontEnd.PlaySound(FrontEndSound.Select);
                    Cursor--;
                }
                Time = frontEnd.Counter;
                break;
            case FrontEndButtons.MenuSelect:
                if (_saves[Cursor] is not { } name) break;
                frontEnd.PlaySound(FrontEndSound.Select);
                if (frontEnd.SaveMode)
                {
                    frontEnd.MessageBox.Kill();
                    frontEnd.ShowMessage(320f, 240f, 400f, t => t.GetText(Text.FrontEndText.ConfirmSaveGameDelete), 0, 10, MessageBoxOption.YesNo, MessageBoxQuestion.DeleteSave);
                }
                else
                {
                    LoadGamePage load = frontEnd.LoadGame ?? throw new InvalidOperationException("The load page is not registered.");
                    load.SaveGameNumber = Cursor;
                    load.SaveGameName = name;
                    frontEnd.SetPage(FrontEndPage.LoadGame, 0);
                }
                break;
            case FrontEndButtons.MenuBack:
                frontEnd.PlaySound(FrontEndSound.Back);
                frontEnd.SetPage(FrontEndPage.Main, 50);
                break;
        }
    }

    /// <summary><c>CFEPDirectory::Process</c> (0x0051ac40): the list refreshes, and a delete question answered yes deletes the save.</summary>
    public override void Process(FrontEndPageState state)
    {
        if (state == FrontEndPageState.Inactive) return;
        RefreshSaveFileList(state);
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (!box.BeingDisplayed || box.Question != MessageBoxQuestion.DeleteSave) return;
        if (box.GetFinalChoice() != MessageBoxQuestion.Yes) return;
        if (_saves[Cursor] is { } name && frontEnd.MemoryCard.DeleteSave(name) == MemoryCard.Success) return;
        // "Could not delete" (FEX_SAVEGAME_COULD_NOT_DELETE), dismissed with OK.
        frontEnd.ShowMessage(320f, 240f, 400f, t => t.GetText(Text.FrontEndText.SaveGameCouldNotDelete), 0, 10, MessageBoxOption.Ok, 0);
    }

    /// <summary><c>RefreshSaveFileList</c> (0x0051ad30): the card's save names in its order, and the cursor kept in range.</summary>
    public void RefreshSaveFileList(FrontEndPageState state)
    {
        MemoryCard card = frontEnd.MemoryCard;
        card.Update();
        // The PC's folder is always present, so the page never falls back to the main menu here.
        NumSaves = card.NumSaves;
        for (int i = 0; i < MaxSaves; i++) _saves[i] = i < NumSaves ? card.GetSaveName(i) : null;
        if (Cursor >= NumSaves) Cursor = NumSaves - 1;
        if (Cursor < 0) Cursor = 0;
    }

    /// <summary>
    /// The state-changing part of <c>RenderSaveFileList</c> (0x0051ae70): the list scrolls to keep the cursor shown;
    /// hovering a row moves the cursor and clicking one picks it (returned); dragging in the scroll bar moves the cursor
    /// and the list; then the help arrows (select when there are saves, then back).
    /// </summary>
    public string? RenderSaveFileList(float transition, FrontEndPage dest)
    {
        // x87: 24 * 11 + 130 + 7 is exact.
        const float bottom = Line * VisibleRows + Top + 7f;
        string? clicked = null;
        Draw.FrontEndDraw? draw = frontEnd.Draw;
        int alpha = Numerics.X87.Fist(Draw.FrontEndDraw.RangeTransition(transition, 0.5f, 1f) * 255f);
        frontEnd.DrawSlidingTextBordersAndMask(transition, dest);
        uint boxcol = transition > 0.5f ? Draw.FrontEndDraw.BlendAlpha2(0x80ffffff, Numerics.X87.Fist((transition - 0.5f) * 510f)) : 0;
        draw?.DrawPanel(Left - 4f, Top, 530f, bottom, 0.55f, Draw.FrontEndDraw.BlendAlpha2(0xa0000000, alpha));
        draw?.DrawBox(Left - 4f, Top, 530f, bottom, boxcol, 2f, 0.5f);
        while (FirstShown + VisibleRows <= Cursor) FirstShown++;
        while (FirstShown > Cursor) FirstShown--;

        int i;
        for (i = FirstShown; i < MaxSaves; i++)
        {
            if (_saves[i] is null) break;
            int row = i - FirstShown;
            if (row >= VisibleRows) break;
            float y = (float)X87.Add(X87.Mul(row, Line), Top);
            float yBottom = (float)X87.Add(y, Line);
            if (draw is not null && frontEnd.NormalFont is { } font)
            {
                uint col = 0xff404040;
                if (i == Cursor)
                {
                    int pulse = X87.Fist((float)Math.Cos(frontEnd.Counter * 3.14f * (1f / 30f)) * -100f);
                    col = unchecked(0xff969696 - (uint)(pulse * 0x010101));
                }
                // The name, cut to 17 characters and "..." when longer.
                string name = _saves[i]!;
                string shown = name.Length > 17 ? name[..17] + "..." : name;
                draw.DrawTextDynamic(font, Draw.CanvasTextureKind.NormalFont, Left, y, 0.1f, 1f, 1f, Draw.FrontEndDraw.BlendAlpha2(col, alpha), shown, 1000f);
            }
            if (frontEnd.MouseOverRect(Left, y, 510f, yBottom))
            {
                Cursor = i;
                Time = frontEnd.Counter;
            }
            if (frontEnd.MouseClickedInRect(Left, y, 510f, yBottom))
            {
                Cursor = i;
                Time = frontEnd.Counter;
                clicked = _saves[i];
            }
        }

        if (i == 0)
        {
            // The empty list's text, measured then drawn: two reads of it, each checking the X key, drawn or not.
            string empty = frontEnd.Texts?.GetText(Text.FrontEndText.EmptyDirectory) ?? "";
            string emptyText = frontEnd.Texts?.GetText(Text.FrontEndText.EmptyDirectory) ?? "";
            if (draw is not null && frontEnd.NormalFont is { } font)
            {
                float x = (640f - font.GetTextExtent(empty).Width) * 0.5f;
                draw.DrawTextDynamic(font, Draw.CanvasTextureKind.NormalFont, x, Line * 5.5f + Top, 0.1f, 1f, 1f,
                    Draw.FrontEndDraw.BlendAlpha2(0x80ffffff, alpha), emptyText, 1000f);
            }
        }
        else
        {
            const float height = bottom - Top;
            float num = NumSaves;
            float per = (float)X87.Div(height - 10f, num);
            float barTop = (float)X87.Add(X87.Add(X87.Mul(FirstShown, per), Top), 5f);
            float barBottom = (float)X87.Add(X87.Mul(i - FirstShown, per), barTop);
            float bar = (float)X87.Sub(barBottom, barTop);
            draw?.DrawBox(510f, Top, 530f, bottom, boxcol, 2f, 0.5f);
            uint barcol = transition > 0.5f ? Draw.FrontEndDraw.BlendAlpha2(0xffffffff, Numerics.X87.Fist((transition - 0.5f) * 510f)) : 0;
            draw?.DrawBox(515f, barTop, 525f, barBottom, barcol, 5f, 0.5f);
            Platform.Mouse mouse = frontEnd.Platform.Mouse;
            if (frontEnd.GetCursorStateInRect(510f, Top, 530f, bottom) && NumSaves != 0 && mouse.Held[0])
            {
                // SINT(...) is fistp under /QIfist, rounding to nearest (0051b3cb, 0051b41a), not a truncation.
                double along = X87.Div(X87.Sub(X87.Sub(mouse.Y, Top), X87.Mul(bar, 0.5f)), X87.Sub(height, bar));
                Cursor = X87.Fist(X87.Mul(along, X87.Sub(NumSaves, 0.1f)));
                if (Cursor < 0) Cursor = 0;
                if (Cursor >= NumSaves) Cursor = NumSaves - 1;
                FirstShown = NumSaves > VisibleRows
                    ? X87.Fist(X87.Mul(X87.Div(X87.Sub(mouse.Y, Top), height), X87.Add(NumSaves - VisibleRows, 0.9f)))
                    : 0;
            }
            frontEnd.RenderHelpArrow(5, transition);
        }
        frontEnd.RenderHelpArrow(1, transition);
        return clicked;
    }

    /// <summary><c>CFEPDirectory::Render</c> (0x0051b460): a picked save is selected.</summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        if (RenderSaveFileList(transition, dest) is not null) ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        // The title is read whether or not it is drawn (each read checks the X key).
        if (frontEnd.Texts is { } texts)
            frontEnd.DrawTitleBar(texts.GetText(frontEnd.SaveMode ? Text.FrontEndText.DeleteGame2 : Text.FrontEndText.SelectLoadGame2), transition, dest);
        frontEnd.RenderSmallBEALogo(Draw.FrontEndDraw.RangeTransition(transition, 0.75f, 1f));
    }

    /// <summary><c>CFEPDirectory::RenderPreCommon</c> (0x0051ae50): the standard video background.</summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest) =>
        frontEnd.DrawStandardVideoBackground(transition, 0x3fffffff, dest);
}
