// SPDX-License-Identifier: GPL-3.0-only
// Adapted from matched bea-decomp Plane.cpp: OpenWings004d1f90, CloseWings004d1fd0, FinishedPlayingCurrentAnimation004d2010.
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
namespace OnslaughtRebuild.Parity.Things;

public sealed partial class Plane
{
    public void OpenWings()
    {
        if (WingState != 1) return;
        SetAnimMode(AnimModeByName("wingopen"), true, false);
        WingState = 2;
    }

    public void CloseWings()
    {
        if (WingState != 4) return;
        SetAnimMode(AnimModeByName("wingclose"), true, false);
        WingState = 3;
    }

    public override bool FinishedPlayingCurrentAnimation()
    {
        if ((Animation?.AnimMode ?? Things.Animation.ModeNone) != -1)
        {
            switch (WingState)
            {
                case 2:
                    SetAnimMode(AnimModeByName("attack"), true, true);
                    WingState = 4;
                    break;
                case 3:
                    SetAnimMode(AnimModeByName("launch"), true, true);
                    WingState = 1;
                    break;
            }
        }

        return false; // No generic standing fallback and no script FinishedPlayingAnim notification.
    }
}
