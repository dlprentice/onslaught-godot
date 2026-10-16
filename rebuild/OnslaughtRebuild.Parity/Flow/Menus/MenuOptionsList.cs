// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MenuOptionsList.cpp (CMenuOptionsList: Reset 004a4630, SelectNext 004a4790, Render
// 004a4810, PassToSelected 004a4cd0, ReceiveButtonAction 004a4d20, SetItemEnabled 004a4dd0; reconstructed from the
// retail program, not in Stuart's release).

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>CMenuOptionsList: a list of menu items driven as one menu.</summary>
public sealed class MenuOptionsList(FrontEnd frontEnd)
{
    public List<MenuItem> Items { get; } = [];

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

    /// <summary>The state Render (004a4810) changes: each item drawn, the selection as selected.</summary>
    public void Render()
    {
        for (int n = 0; n < Items.Count; n++)
        {
            Items[n].Render(n == Selected);
        }
    }
}
