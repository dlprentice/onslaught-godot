// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MCDropship.cpp: AdjustFrame 00496100, saved-state comparison 00496200.
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.

using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>The dropship's door animation, independently of its whole-unit animation.</summary>
public sealed class DropshipMotionController(Dropship unit) : IPartController
{
    internal int LastDoorState { get; private set; } = -1;
    internal float LastDoorOpen { get; private set; } = -999.0f;

    /// <summary>00496100: ASCII case-insensitive door prefix and first matching opening/closing mode.</summary>
    public void AdjustFrame(MeshPart part, ref float time)
    {
        if (!part.Name.StartsWith("door", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string name = unit.DoorState is 5 or 7 ? "doorclosing" : "dooropening";
        foreach (AnimMode mode in part.Mesh.AnimModes)
        {
            if (!string.Equals(mode.ModeName, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 004961ac..004961d8 retains the intermediate in x87 despite its stores through time. Integer
            // scaler/start operands are loaded exactly; no float conversion or store may round them first.
            float fraction = unit.Context.Game?.Loop.Fraction ?? 0.0f;
            double frame = X87.Add(X87.Mul(X87.Sub(unit.Unknown2A4, unit.Unknown2A8), fraction), unit.Unknown2A8);
            frame = X87.Mul(frame, mode.FrameScaler);
            time = (float)X87.Add(mode.Start, frame);
            break;
        }

        // Missing modes still update these fields. A non-door part returns before touching them.
        // The pose's slot-8 caller ignores its pure comparison result, so this does not bypass its cache gates.
        LastDoorState = unit.DoorState;
        LastDoorOpen = unit.Unknown2A4;
    }
}
