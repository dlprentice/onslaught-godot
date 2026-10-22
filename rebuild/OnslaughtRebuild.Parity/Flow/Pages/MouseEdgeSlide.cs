// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPGoodies.cpp (FEMouseEdgeSlide), in Stuart's release.

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary><c>FEMouseEdgeSlide</c> (0x0045d730), shared by the goodies page and the level select.</summary>
public static class MouseEdgeSlide
{
    /// <summary>
    /// With the cursor above y 400 and right of x 540 (or left of x 100), the position moves by the cube of the
    /// distance; nothing while the message box is interactive or the page is not active. The result is clamped to
    /// 0..<paramref name="max"/>.
    /// </summary>
    public static float Apply(FrontEnd frontEnd, FrontEndPageState state, float pos, float max)
    {
        if (frontEnd.MessageBox.IsInteractive || state != FrontEndPageState.Active) return pos;
        Platform.Mouse mouse = frontEnd.Platform.Mouse;
        float speed = 0f;
        if (mouse.X > 540 && mouse.Y < 400) speed = mouse.X - 540;
        else if (mouse.X < 100 && mouse.Y < 400) speed = mouse.X - 100;
        float step = frontEnd.Timing.FrameStep;
        // x87: left to right, each product rounded to 24 bits, then the sum stored.
        pos = (float)X87.Add(pos, X87.Mul(X87.Mul(X87.Mul(X87.Mul(step, speed), speed), speed), 0.002f));
        if (pos < 0f) pos = 0f;
        if (pos > max) pos = max;
        return pos;
    }
}
