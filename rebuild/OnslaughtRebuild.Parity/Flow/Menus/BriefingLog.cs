// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/LevelBriefingLog.cpp (CLevelBriefingLog: the constructor 0048f540, Render 0048f620, Activate
// 0048ff90, ReceiveButtonAction 0048ffa0; reconstructed from the retail program, not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>
/// CLevelBriefingLog: the level's briefing and objectives, shown from the pause menu. Its panel grows in and shrinks
/// away by mFade; the text is the host's picture. The back arrow's click needs the cursor to have moved and is not
/// modelled.
/// </summary>
public sealed class BriefingLog(IMenuHost host, IPauseMenuGame game) : IButtonReceiver
{
    /// <summary>The pause menu the log returns to.</summary>
    public PauseMenu? PauseMenu { get; set; }

    /// <summary>mActive.</summary>
    public bool Active { get; private set; }

    /// <summary>mFade: 0 hidden, 1 shown.</summary>
    public float Fade { get; private set; }

    bool IButtonReceiver.CanBeControlledWhenInPause => true;

    ControlType IButtonReceiver.ControlType => ControlType.LevelBriefingLog;

    /// <summary>Activate (0048ff90).</summary>
    public void Activate()
    {
        Active = true;
        Fade = 0.1f;
    }

    /// <summary>ReceiveButtonAction (0048ffa0): back to the pause menu.</summary>
    public void ReceiveButtonAction(Controller from, int button, float value)
    {
        if (button == FrontEndButtons.MenuBack)
        {
            host.PlaySound(FrontEndSound.Back);
            Active = false;
            PauseMenu?.Activate(false);
            game.RelinquishControl(this);
        }
    }

    /// <summary>
    /// The state Render (0048f620) changes: closed, the fade shrinks a tenth each 60th of a second (by PLATFORM.GetFPS);
    /// open, it grows to 1.
    /// </summary>
    public void Render()
    {
        if (Fade <= 0.0f)
        {
            return;
        }

        float ft = (float)X87.Div(60.0f, host.Clock.FramesPerSec);
        if (!Active)
        {
            Fade = (float)X87.Sub(Fade, X87.Mul(ft, 0.1f));
            return;
        }

        if (Fade < 1.0f)
        {
            Fade = (float)X87.Add(Fade, X87.Mul(ft, 0.1f));
            if (Fade > 1.0f)
            {
                Fade = 1.0f;
            }
        }
    }
}
