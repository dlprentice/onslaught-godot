// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/MenuOptionsList.cpp (CMenuOptionsList: Reset 004a4630, SelectNext 004a4790, Render
// 004a4810, PassToSelected 004a4cd0, ReceiveButtonAction 004a4d20, SetItemEnabled 004a4dd0; reconstructed from the
// retail program, not in Stuart's release).

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>
/// CMenuOptionsList: a list of menu items driven as one menu, laid out down from (x, y), with its title (drawn above it
/// outside the options page), optionally on a message-log panel. A confirmation's list (<c>CConfirmMenuOptionsList</c>)
/// takes the mouse while its confirmation is up.
/// </summary>
public sealed class MenuOptionsList(IMenuHost frontEnd, MenuTitle title = default, float x = 320f, float y = 300f,
    bool panel = false, bool confirmation = false)
{
    public List<MenuItem> Items { get; } = [];

    /// <summary>mTitle.</summary>
    public MenuTitle Title { get; } = title;

    /// <summary>mX, mY.</summary>
    public float X { get; } = x;

    public float Y { get; } = y;

    /// <summary>mPanel.</summary>
    public bool Panel { get; } = panel;

    /// <summary>IsConfirmation.</summary>
    public bool IsConfirmation { get; } = confirmation;

    /// <summary>mSelected.</summary>
    public int Selected { get; set; }

    public void AddItem(MenuItem item) => Items.Add(item);

    /// <summary>Reset (004a4630): the first item, each item refreshed.</summary>
    public void Reset()
    {
        Selected = 0;
        foreach (MenuItem item in Items)
        {
            item.Refresh();
        }
    }

    /// <summary>SelectNext (004a4790): the next enabled item, wrapping when the list has more than two.</summary>
    public void SelectNext()
    {
        int old = Selected;
        if (Selected >= Items.Count - 1)
        {
            if (Items.Count <= 2)
            {
                return;
            }

            Selected = 0;
        }
        else
        {
            Selected++;
        }

        while (!Items[Selected].IsEnabled)
        {
            Selected++;
            if (Selected >= Items.Count)
            {
                Selected = old;
                return;
            }
        }

        frontEnd.PlaySound(FrontEndSound.Move);
    }

    /// <summary>SelectPrevious (inlined): the previous enabled item, wrapping when the list has more than two.</summary>
    public void SelectPrevious()
    {
        int old = Selected;
        if (Selected <= 0)
        {
            if (Items.Count <= 2)
            {
                return;
            }

            Selected = Items.Count - 1;
        }
        else
        {
            Selected--;
        }

        while (!Items[Selected].IsEnabled)
        {
            if (--Selected < 0)
            {
                Selected = old;
                return;
            }
        }

        frontEnd.PlaySound(FrontEndSound.Move);
    }

    /// <summary>PassToSelected (004a4cd0): an expanded item takes the button.</summary>
    public bool PassToSelected(int button, float value)
    {
        if (Items[Selected].IsExpanded)
        {
            Items[Selected].ReceiveButtonAction(button, value);
            return true;
        }

        return false;
    }

    /// <summary>ReceiveButtonAction (004a4d20): up and down move; anything else goes to the selection.</summary>
    public void ReceiveButtonAction(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuUp:
                SelectPrevious();
                break;
            case FrontEndButtons.MenuDown:
                SelectNext();
                break;
            default:
                Items[Selected].ReceiveButtonAction(button, value);
                break;
        }
    }

    /// <summary>SetItemEnabled (004a4dd0): by text id.</summary>
    public void SetItemEnabled(int text, bool enabled)
    {
        foreach (MenuItem item in Items)
        {
            if (item.Text == text)
            {
                item.Enabled = enabled;
            }
        }
    }

    /// <summary>
    /// Render (004a4810): the items down from the list's top, each the selection when the cursor is over its row (while
    /// the selection takes no input, and no confirmation is up unless this is one), the selected dropdown drawn last; a
    /// click on a row selects it and, after the items, presses select (the front end's, then the pause menu's) on the
    /// menu or its confirmation. Returns the title.
    /// </summary>
    public MenuTitle Render(PauseMenu menu, MenuRender r)
    {
        r.RenderOnTop = null;
        int height = 32;
        foreach (MenuItem item in Items)
        {
            height += item.GetHeight();
        }

        height -= 32;
        float ty = Y - height * 0.5f - 15f;

        if (Panel && r.Draw is not null)
        {
            // GetMenuPanelSize: the widest of the title and the items, plus 16, and the heights plus 32, each times 1.1.
            int width = r.Drawing!.Normal.GetTextExtent(Title.Resolve(r.Drawing.Texts)).Width;
            int widestItem = 0;
            foreach (MenuItem item in Items)
            {
                int w = item.GetWidth(r);
                if (w > widestItem) widestItem = w;
            }

            if (widestItem > width) width = widestItem;
            float pw = (width + 16) * 1.1f;
            int ph = 32;
            foreach (MenuItem item in Items)
            {
                ph += item.GetHeight();
            }

            float fh = ph * 1.1f;
            r.RenderPanel(Numerics.X87.Fist(X - pw * 0.5f), Numerics.X87.Fist(Y - fh * 0.5f), Numerics.X87.Fist(pw), Numerics.X87.Fist(fh), 1.2f);
        }

        if (!r.OptionsPage && Title != default && r.Drawing is { Draw: { } draw } d)
        {
            string title = Title.Resolve(d.Texts);
            int half = d.Normal.GetTextExtent(title).Width / 2;
            draw.DrawTextScaled(d.Normal, OnslaughtRebuild.Parity.Flow.Draw.CanvasTextureKind.NormalFont, X - half, ty, 1f, 1f, 1f, 0xff505050, title, 0);
            ty += 30f;
        }

        // The widest item (measured for nothing; the joystick items' texts set their enables).
        foreach (MenuItem item in Items)
        {
            if (r.Drawing is not null) item.GetWidth(r);
            else item.TextSideEffects();
        }

        r.ChangesPending = false;
        bool clicked = false;
        bool mouseTakes = !menu.IsConfirming || IsConfirmation;
        for (int n = 0; n < Items.Count; n++)
        {
            MenuItem item = Items[n];
            if (mouseTakes && !Items[Selected].TakesInput && r.CursorInRect(0f, ty, 640f, ty + item.GetHeight()))
                Selected = n;

            item.Render(r, X, ty, n == Selected);

            if (mouseTakes && !Items[Selected].TakesInput && r.ClickedInRect(0f, ty, 640f, ty + item.GetHeight()))
            {
                clicked = true;
                Selected = n;
            }

            if (item.HasPendingChange) r.ChangesPending = true;
            ty += item.GetHeight();
        }

        // DeferredRender.
        MenuMenuItem? onTop = r.RenderOnTop;
        r.RenderOnTop = null;
        onTop?.RenderList(r, r.RenderOnTopX, r.RenderOnTopY, true);

        if (clicked)
        {
            if (IsConfirmation)
            {
                ConfirmMenu confirm = menu.Confirm!;
                confirm.ReceiveButtonAction(null!, FrontEndButtons.MenuSelect, 1f);
                confirm.ReceiveButtonAction(null!, Platform.Buttons.PauseMenuSelect, 1f);
            }
            else
            {
                menu.ReceiveButtonAction(FrontEndButtons.MenuSelect, 1f);
                menu.ReceiveButtonAction(Platform.Buttons.PauseMenuSelect, 1f);
            }
        }

        return Title;
    }
}
