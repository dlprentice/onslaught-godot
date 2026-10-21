// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MenuItem.cpp and src/MenuItem.h (CMenuItem, CScaleMenuItem, CMenuMenuItem, CBOOLMenuItem,
// CApplyMenuItem; reconstructed from the retail program, not in Stuart's release): the items' decisions, and their Render
// (CMenuItem::Render 004a3260/004a32c0, GetText 004a3190, GetWidth 004a3420; CScaleMenuItem::Render 004a37c0;
// CMenuMenuItem::Render 004a3be0, RenderList 004a3c30, DeferredRender 004a3bb0; CApplyMenuItem::Render 004a4310), which
// draws through MenuRender when it has a drawing and changes state (clock reads, mouse hovers and clicks, the right button
// that closes an open list) whether drawn or not.

using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>CMenuItem: a line of a menu, a text.stf text or (with text 0) a PC text.</summary>
public class MenuItem(int text, int pcText = 0)
{
    /// <summary>The disabled item's colour (CMenuItem::Render).</summary>
    public const uint DisabledColour = 0x50505050;

    /// <summary>mText: a text.stf id, or 0 for a PC text.</summary>
    public int Text { get; set; } = text;

    /// <summary>mPCText: the Localization_GetStringById id when <see cref="Text"/> is 0.</summary>
    public int PCText { get; } = pcText;

    /// <summary>mEnabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>mColour.</summary>
    public uint Colour { get; set; } = 0xffd6d6d6;

    public virtual bool IsEnabled => Enabled;

    public virtual bool IsExpanded => false;

    public virtual bool TakesInput => false;

    public virtual bool HasPendingChange => false;

    public virtual void ReceiveButtonAction(int button, float value)
    {
    }

    public virtual void Commit()
    {
    }

    public virtual void Refresh()
    {
    }

    /// <summary>
    /// GetText (004a3190): the PC text, or text.stf's. (The FMV_1_* texts' " - " forms are not built: no menu here uses
    /// them.)
    /// </summary>
    public virtual string GetText(MenuRender r) => Text == 0 ? r.Pc(PCText) : r.Text(Text);

    /// <summary>What GetText changes, for a render that does not read the text (only the joystick item's enable).</summary>
    public virtual void TextSideEffects()
    {
    }

    /// <summary>GetWidth (004a3420): the text's width in the small font.</summary>
    public virtual int GetWidth(MenuRender r) => r.SmallExtent(GetText(r)).Width;

    /// <summary>GetHeight: 20 (40 with a label, which no item here has).</summary>
    public virtual int GetHeight() => 20;

    /// <summary>Render (004a3260): the text in white's mask.</summary>
    public virtual void Render(MenuRender r, float x, float y, bool selected)
    {
        if (r.Draw is null) return;
        RenderText(r, x, y, selected, 0xffffffff, GetText(r));
    }

    /// <summary>
    /// Render (004a32c0): centred on x in the small font with its shadow, yellow when selected, grey when disabled,
    /// squeezed to start at 5 when it would pass the left edge.
    /// </summary>
    protected void RenderText(MenuRender r, float x, float y, bool selected, uint colour, string text)
    {
        uint col = Colour;
        if (selected) col = MenuRender.SelectedColour;
        if (!Enabled) col = DisabledColour;
        int half = r.SmallExtent(text).Width / 2;
        float tx = x - half;
        float scale = 1f;
        if (tx < 5f)
        {
            scale = (x - 5f) / half;
            tx = 5f;
        }

        r.SmallTextWithShadow(tx, y, col & colour, text, scale);
    }
}

/// <summary>CScaleMenuItem (0x38 bytes): a bar of <see cref="Steps"/> segments, moved left and right.</summary>
public class ScaleMenuItem : MenuItem
{
    private readonly IMenuHost _frontEnd;
    private readonly Action<ScaleMenuItem>? _changed;

    /// <param name="changed">CGameMenu::MenuItemChanged of the menu told when the value is committed.</param>
    public ScaleMenuItem(IMenuHost frontEnd, int text, int pcText, float value, Action<ScaleMenuItem>? changed, int steps, bool immediate)
        : base(text, pcText)
    {
        _frontEnd = frontEnd;
        _changed = changed;
        Steps = steps;
        Immediate = immediate;

        // int(float(steps) * value + 0.48f): /QIfist rounds to nearest.
        CommittedValue = Value = X87.Fist((float)X87.Add(X87.Mul(steps, value), 0.48f));
    }

    public int Value { get; set; }

    public int CommittedValue { get; set; }

    public int Steps { get; }

    /// <summary>mImmediate: tell the menu at every step.</summary>
    public bool Immediate { get; }

    public virtual void ValueChanged()
    {
    }

    /// <summary>CScaleMenuItem::ReceiveButtonAction (004a43a0).</summary>
    public override void ReceiveButtonAction(int button, float value)
    {
        bool changed = false;
        switch (button)
        {
            case FrontEndButtons.MenuRight:
                Value++;
                if (Value > Steps)
                {
                    Value = Steps;
                }
                else
                {
                    ValueChanged();
                    _frontEnd.PlaySound(FrontEndSound.Move);
                }

                changed = true;
                break;
            case FrontEndButtons.MenuLeft:
                if (--Value < 0)
                {
                    Value = 0;
                }
                else
                {
                    ValueChanged();
                    _frontEnd.PlaySound(FrontEndSound.Move);
                }

                changed = true;
                break;
            case FrontEndButtons.MenuSelect:
                _changed?.Invoke(this);
                CommittedValue = Value;
                break;
        }

        if (Immediate && changed)
        {
            _changed?.Invoke(this);
            CommittedValue = Value;
        }
    }

    /// <summary>GetWidth (004a4450): the text and the bar.</summary>
    public override int GetWidth(MenuRender r) => base.GetWidth(r) + 106;

    /// <summary>
    /// CScaleMenuItem::Render (004a37c0): not drawn as the selection, an uncommitted value goes back. The text left of
    /// centre, the two arrows and the segments: green for the committed value, pulsing (a clock read each) where the shown
    /// and committed values differ. Selected, a click on the left or right part of the bar steps it.
    /// </summary>
    public override void Render(MenuRender r, float x, float y, bool selected)
    {
        bool measure = r.Draw is not null || (selected && r.MouseActive);
        string? text = measure ? GetText(r) : null;
        uint col = Colour;
        if (selected)
        {
            col = MenuRender.SelectedColour;
        }
        else
        {
            Value = CommittedValue;
            ValueChanged();
        }

        if (!Enabled) col = 0xff505050;

        if (text is not null)
        {
            int cx = r.SmallExtent(text).Width;
            float tx = x - cx / 2 - 53f;
            x = tx + cx + 2f;
            r.SmallTextWithShadow(tx, y, col, text);
            r.Sprite(MenuRender.Arrow, x + 5f, y + 8f, 0.003f, 0xffffffff, 0.3f, 0.3f, SpriteAlign.Centre, 3.14159265358979f);
            r.Sprite(MenuRender.Arrow, x + 99f, y + 8f, 0.003f, 0xffffffff, 0.3f, 0.3f, SpriteAlign.Centre);
        }

        for (int i = 0; i < Steps; i++)
        {
            uint c = 0xff505050;
            if (i < Value && i < CommittedValue)
            {
                c = 0xff00a000;
            }
            else if (i < Value || i < CommittedValue)
            {
                float t = r.Clock.GetSysTimeFloat();
                float f = ((float)Math.Sin((float)(t % 2.0) * 3.14159265358979f) + 1f) * 0.5f;
                float g = 1f - f;
                c = (uint)((X87.Fist(g * 255f + f * 255f) << 24) + (X87.Fist(g * 160f + f * 80f) << 8)
                    + (X87.Fist(f * 80f) << 16) + X87.Fist(f * 80f));
            }

            if (text is not null)
                r.Sprite(MenuRender.Blank, x + i * 80f / Steps + 13f, y + 8f, 0.003f, c, (80f / Steps - 1f) / 16f, 0.7f, SpriteAlign.LeftMiddle);
        }

        if (selected && text is not null)
        {
            if (r.ClickedInRect(x, y, x + 40f, y + 16f))
                ReceiveButtonAction(FrontEndButtons.MenuLeft, 1f);
            else if (r.ClickedInRect(x + 60f, y, x + 120f, y + 16f))
                ReceiveButtonAction(FrontEndButtons.MenuRight, 1f);
        }
    }
}

/// <summary>CMenuMenuItem (0x28 bytes): a choice from a list, dropped down when selected.</summary>
public abstract class MenuMenuItem(IMenuHost frontEnd, int pcText, bool deferred) : MenuItem(0, pcText)
{
    protected IMenuHost FrontEnd { get; } = frontEnd;

    /// <summary>mValue: the value as the setting has it.</summary>
    public int Value { get; set; }

    /// <summary>mSelection: the choice the list shows.</summary>
    public int Selection { get; set; }

    /// <summary>mExpanded.</summary>
    public bool Expanded { get; set; }

    /// <summary>mDeferred: committed only through an apply item.</summary>
    public bool Deferred { get; } = deferred;

    public override bool IsExpanded => Expanded;

    public override bool TakesInput => Expanded;

    /// <summary>HasPendingChange (004a42f0).</summary>
    public override bool HasPendingChange => Deferred && Value != Selection;

    public abstract void SetValue(int value);

    public abstract int GetValue();

    public abstract int GetNumChoices();

    /// <summary>GetChoiceText: a choice's name.</summary>
    public abstract string GetChoiceText(MenuRender r, int n);

    /// <summary>Refresh (004a3b50).</summary>
    public override void Refresh() => Selection = Value = GetValue();

    /// <summary>Commit (004a40f0).</summary>
    public override void Commit()
    {
        if (Selection != Value)
        {
            SetValue(Selection);
            Value = Selection;
        }
    }

    /// <summary>CMenuMenuItem::ReceiveButtonAction (004a4110).</summary>
    public override void ReceiveButtonAction(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuDown:
            case FrontEndButtons.MenuRight:
                FrontEnd.PlaySound(FrontEndSound.Move);
                Selection++;
                if (Selection >= GetNumChoices())
                {
                    Selection = GetNumChoices() - 1;
                }

                Expanded = true;
                break;
            case FrontEndButtons.MenuUp:
            case FrontEndButtons.MenuLeft:
                FrontEnd.PlaySound(FrontEndSound.Move);
                if (--Selection < 0)
                {
                    Selection = 0;
                }

                Expanded = true;
                break;
            case FrontEndButtons.MenuSelect:
                FrontEnd.PlaySound(FrontEndSound.Select);
                if (!Expanded && GetNumChoices() < 2)
                {
                    FrontEnd.PlaySound(FrontEndSound.Back);
                    return;
                }

                Expanded = !Expanded;
                if (!Expanded && !Deferred && Value != Selection)
                {
                    Value = Selection;
                    SetValue(Selection);
                }

                break;
            case FrontEndButtons.MenuBack:
                if (Expanded)
                {
                    FrontEnd.PlaySound(FrontEndSound.Back);
                    Expanded = false;
                    Selection = Value;
                }

                break;
        }
    }

    /// <summary>Render (004a3be0): the list's selection waits for DeferredRender, drawn after the other items.</summary>
    public override void Render(MenuRender r, float x, float y, bool selected)
    {
        if (selected && r.RenderOnTop is null)
        {
            r.RenderOnTopX = x;
            r.RenderOnTopY = y;
            r.RenderOnTop = this;
            return;
        }

        RenderList(r, x, y, false);
    }

    /// <summary>
    /// RenderList (004a3c30): a setting changed elsewhere resets the shown choice; a choice not yet the setting pulses (a
    /// clock read). The name right-aligned on x, then the choice; expanded, the choices dropped down on a panel, the one
    /// under the cursor selected, a click choosing it (committed unless deferred), the right button closing the list.
    /// </summary>
    public void RenderList(MenuRender r, float x, float y, bool onTop)
    {
        int value = GetValue();
        if (value != Value)
        {
            Value = value;
            Selection = value;
        }

        uint col = Colour;
        if (Value != Selection)
            col = MenuRender.PendingPulse(r.Clock.GetSysTimeFloat());
        if (onTop) col = (col & 0xffff0000) + ((((col & 0xff00) * 204) >> 8) & 0xff00);
        if (!Enabled) col = 0xff505050;

        // Unmeasured, nothing below changes state: the rows' tests need the mouse.
        if (r.Draw is null && !(Expanded && r.MouseActive)) return;

        string text = GetText(r);
        (int cx, int cy) = r.SmallExtent(text);
        float scale = 1f;
        float tx = x - cx;
        float lx = tx + cx + 2f;
        if (tx < 5f)
        {
            scale = (x - 5f) / cx;
            tx = 5f;
        }

        r.SmallTextWithShadow(tx, y, col, text, scale);
        if (!Expanded)
        {
            r.SmallTextWithShadow(lx, y, col, GetChoiceText(r, Selection));
            return;
        }

        int n = GetNumChoices();
        int widest = 0;
        if (r.Draw is not null)
        {
            for (int i = 0; i < n; i++)
            {
                int w = r.SmallExtent(GetChoiceText(r, i)).Width;
                if (w > widest) widest = w;
            }
        }

        int top = X87.Fist(y - (n - 1) * cy / 2);
        float ly = top;
        float yscale = 1f;
        float height = n * cy;
        if (ly < 0f) ly = 0f;
        if (height + ly > 480f)
        {
            if (height > 480f)
            {
                yscale = 480f / height;
                ly = 0f;
            }
            else
            {
                ly = 480f - height;
            }
        }

        uint back = r.Drawing is { OnOptionsPage: true } ? 0xff283969 : 0xff404040;
        r.Sprite(MenuRender.Blank, lx, ly, 0.004f, back, (widest + 3) / 16f, n * cy * yscale / 16f);

        float rowh = cy * yscale;
        for (int i = 0; i < n; i++)
        {
            uint c = i == Selection ? 0xffffffff : 0xff404040;
            float x2 = cx + lx + 2f;
            float ny = ly + rowh;
            if (r.CursorInRect(lx + 2f, ly, x2, ny)) Selection = i;
            if (r.Draw is not null) r.SmallTextWithShadow(lx + 2f, ly, c, GetChoiceText(r, i), 1f, yscale);
            if (r.ClickedInRect(lx + 2f, ly, cx + lx + 2f, ly + cy))
            {
                Selection = i;
                Expanded = false;
                if (!Deferred && Value != i)
                {
                    Value = i;
                    SetValue(i);
                }

                FrontEnd.PlaySound(FrontEndSound.Select);
            }

            ly = ny;
        }

        if (r.Mouse is { } mouse && mouse.TakeRightButtonDown())
        {
            Expanded = false;
            Selection = Value;
            FrontEnd.PlaySound(FrontEndSound.Back);
        }
    }
}

/// <summary>CBOOLMenuItem: off and on.</summary>
public abstract class BoolMenuItem(IMenuHost frontEnd, int pcText, bool deferred) : MenuMenuItem(frontEnd, pcText, deferred)
{
    public override int GetNumChoices() => 2;

    /// <summary>GetChoiceText (004a4220): PC texts 6 and 5.</summary>
    public override string GetChoiceText(MenuRender r, int n) => r.Pc(n == 0 ? 6 : 5);
}

/// <summary>A choice of one of the options' settings (the CLIPARAMS fields the options file keeps).</summary>
public sealed class SettingMenuItem(IMenuHost frontEnd, int pcText, bool deferred, int choices, Func<int> get, Action<int> set,
    Func<MenuRender, int, string> choiceText)
    : MenuMenuItem(frontEnd, pcText, deferred)
{
    public override int GetNumChoices() => choices;

    public override int GetValue() => get();

    public override void SetValue(int value) => set(value);

    public override string GetChoiceText(MenuRender r, int n) => choiceText(r, n);
}

/// <summary>A boolean setting.</summary>
public sealed class BoolSettingMenuItem(IMenuHost frontEnd, int pcText, bool deferred, Func<int> get, Action<int> set)
    : BoolMenuItem(frontEnd, pcText, deferred)
{
    public override int GetValue() => get();

    public override void SetValue(int value) => set(value);
}

/// <summary>CApplyMenuItem (0x20 bytes): commits every item of its list.</summary>
public sealed class ApplyMenuItem(MenuOptionsList list) : MenuItem(0)
{
    /// <summary>gMenuApplyCount (00704868).</summary>
    public static int ApplyCount { get; private set; }

    /// <summary>ReceiveButtonAction (004a4290).</summary>
    public override void ReceiveButtonAction(int button, float value)
    {
        if (button == FrontEndButtons.MenuSelect)
        {
            ApplyCount++;
            foreach (MenuItem item in list.Items)
            {
                item.Commit();
            }
        }
    }

    /// <summary>Render (004a4310): pulsing (a clock read) while an earlier item of the list has a change to apply.</summary>
    public override void Render(MenuRender r, float x, float y, bool selected)
    {
        uint col = 0xffffffff;
        if (r.ChangesPending) col = MenuRender.PendingPulse(r.Clock.GetSysTimeFloat());
        if (r.Draw is not null) RenderText(r, x, y, selected, col, GetText(r));
    }
}
