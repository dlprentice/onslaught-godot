// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/MessageLog.cpp (CMessageLog: the constructor 004b8dd0, Render 004b93f0, RenderMessageCard
// 004b9a80, Activate 004b9ea0, ReceiveButtonAction 004b9ec0; reconstructed from the retail program, not in Stuart's
// release).

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Script;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>
/// CMessageLog: the messages already shown, listed from the pause menu, newest first, scrolled a card at a time. Its
/// picture is the host's; this is the state its Render and buttons change. The mouse paths (the arrows and back clicked,
/// the right button) need the cursor to have moved and are not modelled.
/// </summary>
public sealed class MessageLog(IMenuHost host, IPauseMenuGame game, Func<IReadOnlyList<ScriptMessage>> shown, int charsPerLine)
    : IButtonReceiver
{
    /// <summary>
    /// The message box's mCharsPerLine: 8930 over the small font's width of the box's pangram (CMessageBox's constructor,
    /// MessageBox.cpp:232); for the shipped Font13PS that width is 395, so 22.
    /// </summary>
    public const int ShippedCharsPerLine = 22;

    private readonly PlatformClock _clock = host.Clock;

    /// <summary>The pause menu the log returns to.</summary>
    public PauseMenu? PauseMenu { get; set; }

    /// <summary>mActive.</summary>
    public bool Active { get; private set; }

    public float Scroll { get; private set; }

    public float ScrollTarget { get; private set; }

    /// <summary>mCanScrollDown: set by Render when the list runs past the panel.</summary>
    public bool CanScrollDown { get; private set; }

    /// <summary>mTop: the first message shown.</summary>
    public int Top { get; private set; }

    /// <summary>mSelected.</summary>
    public int Selected { get; private set; }

    bool IButtonReceiver.CanBeControlledWhenInPause => true;

    ControlType IButtonReceiver.ControlType => ControlType.MessageLog;

    /// <summary>mMessages: SPtrSet::Add puts each new message at the head, so the newest is first.</summary>
    private ScriptMessage MessageAt(int i)
    {
        IReadOnlyList<ScriptMessage> all = shown();
        return all[all.Count - 1 - i];
    }

    private int Count => shown().Count;

    /// <summary>RenderMessageCard (004b9a80) measuring: 16 a line plus 10, at least 74.</summary>
    public int CardHeight(ScriptMessage message)
    {
        string[] lines = message.WordWrap(charsPerLine + 9, 8);
        int n = lines.Count(l => l.Length != 0);
        float h = (float)X87.Add(X87.Mul(n, 16.0f), 10.0f);
        if (h < 74.0f)
        {
            h = 74.0f;
        }

        return X87.Fist(h);
    }

    /// <summary>Activate (004b9ea0): shown from the top.</summary>
    public void Activate()
    {
        Active = true;
        Scroll = 0;
        ScrollTarget = 0;
        Top = 0;
        Selected = 0;
    }

    /// <summary>The distance to the selected card, negative when it is above the top.</summary>
    private float ScrollDistance()
    {
        if (Selected == Top)
        {
            return 0;
        }

        int c0 = Top;
        float total = 0;
        do
        {
            if (c0 > Selected)
            {
                c0--;
            }

            total = (float)X87.Add(total, CardHeight(MessageAt(c0)) + 8);
            if (c0 < Selected)
            {
                c0++;
            }
        }
        while (c0 != Selected);

        return Selected > Top ? total : -total;
    }

    /// <summary>ReceiveButtonAction (004b9ec0): back to the pause menu, or a card down or up.</summary>
    public void ReceiveButtonAction(Controller from, int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuBack:
                Active = false;
                PauseMenu?.Activate(false);
                game.RelinquishControl(this);
                host.PlaySound(FrontEndSound.Back);
                break;
            case FrontEndButtons.MenuDown:
                if (Selected < Count - 1 && CanScrollDown)
                {
                    Selected++;
                    ScrollTarget = ScrollDistance();
                    host.PlaySound(FrontEndSound.Move);
                }

                break;
            case FrontEndButtons.MenuUp:
                if (Selected > 0)
                {
                    Selected--;
                    ScrollTarget = ScrollDistance();
                    host.PlaySound(FrontEndSound.Move);
                }

                break;
        }
    }

    /// <summary>
    /// The state Render (004b93f0) changes: whether any card reaches below the panel (mCanScrollDown), the scroll easing
    /// a sixth of the way each 60th of a second (by PLATFORM.GetFPS), and once within 1 of its target the top moving to
    /// the selection.
    /// </summary>
    public void Render()
    {
        if (!Active)
        {
            return;
        }

        CanScrollDown = false;
        int y = X87.Fist((float)X87.Sub(120.0f, Scroll));
        if (Top >= 1)
        {
            for (int i = 0; i < Top; i++)
            {
                y -= CardHeight(MessageAt(i)) + 8;
            }
        }

        bool moreBelow = false;
        for (int i = 0; i < Count; i++)
        {
            int height = CardHeight(MessageAt(i));
            if (y >= 315.0f)
            {
                moreBelow = true;
            }

            y += height + 8;
        }

        float ft = (float)X87.Div(60.0f, _clock.FramesPerSec);
        Scroll = (float)X87.Add(Scroll, X87.Mul(X87.Mul(X87.Sub(ScrollTarget, Scroll), 1.0f / 6.0f), ft));
        if (Math.Abs(Scroll - ScrollTarget) < 1.0)
        {
            Scroll = 0;
            ScrollTarget = 0;
            if (Top != Selected)
            {
                Top = Selected;
            }
        }

        if (moreBelow)
        {
            CanScrollDown = true;
        }
    }
}
