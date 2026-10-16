// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MenuItem.cpp and src/MenuItem.h (CMenuItem, CScaleMenuItem, CMenuMenuItem, CBOOLMenuItem,
// CApplyMenuItem; reconstructed from the retail program, not in Stuart's release). The pictures are the host's; these are
// the items' decisions, including the state their Render changes. The mouse paths (hover, clicks, the right button that
// closes an open list) need the cursor to have moved and are not modelled: the runs that drive these menus are keyboard
// only.

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>CMenuItem: a line of a menu, a text.stf text or (with text 0) a PC text.</summary>
public class MenuItem(int text, int pcText = 0)
{
    /// <summary>mText: a text.stf id, or 0 for a PC text.</summary>
    public int Text { get; set; } = text;

    /// <summary>mPCText: the Localization_GetStringById id when <see cref="Text"/> is 0.</summary>
    public int PCText { get; } = pcText;

    /// <summary>mEnabled.</summary>
    public bool Enabled { get; set; } = true;

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

    /// <summary>The state the item's Render changes (selected: drawn as the menu's selection).</summary>
    public virtual void Render(bool selected)
    {
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

    /// <summary>CScaleMenuItem::Render (004a37c0): not drawn as the selection, an uncommitted value goes back.</summary>
    public override void Render(bool selected)
    {
        if (!selected)
        {
            Value = CommittedValue;
            ValueChanged();
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

    /// <summary>RenderList (004a3c30), drawn each frame: a setting changed elsewhere resets the shown choice.</summary>
    public override void Render(bool selected)
    {
        int value = GetValue();
        if (value != Value)
        {
            Value = value;
            Selection = value;
        }
    }
}

/// <summary>CBOOLMenuItem: off and on.</summary>
public abstract class BoolMenuItem(IMenuHost frontEnd, int pcText, bool deferred) : MenuMenuItem(frontEnd, pcText, deferred)
{
    public override int GetNumChoices() => 2;
}

/// <summary>A choice of one of the options' settings (the CLIPARAMS fields the options file keeps).</summary>
public sealed class SettingMenuItem(IMenuHost frontEnd, int pcText, bool deferred, int choices, Func<int> get, Action<int> set)
    : MenuMenuItem(frontEnd, pcText, deferred)
{
    public override int GetNumChoices() => choices;

    public override int GetValue() => get();

    public override void SetValue(int value) => set(value);
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
}

/// <summary>An item the core does not port yet: the list it is in cannot be shown.</summary>
public sealed class NotPortedMenuItem(string what) : MenuItem(0)
{
    public override void Refresh() => throw new NotSupportedException($"{what} is not ported");

    public override void ReceiveButtonAction(int button, float value) => throw new NotSupportedException($"{what} is not ported");

    public override void Render(bool selected) => throw new NotSupportedException($"{what} is not ported");
}
