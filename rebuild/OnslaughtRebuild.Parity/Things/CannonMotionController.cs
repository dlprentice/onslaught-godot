// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/MCCannon.cpp: CMCCannon::GetPartTransform (004952a0), in pristine PC specimen
// SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750. Not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>The cannon's turret, barrel and root-height adjustments, after the part's animation transform.</summary>
public sealed class CannonMotionController(Cannon unit) : IPartController
{
    // The saved angles read by slot 8 (00495770). MeshPose::Update calls that pure comparison but ignores its result;
    // these values do not bypass the same-render-frame or 30-frame/force pose gates.
    internal float LastYaw { get; private set; } = -999.0f;
    internal float LastPitch { get; private set; } = -999.0f;

    /// <summary>004952a0: case-sensitive part names; world/local does not change this hook's work.</summary>
    public void GetPartTransform(MeshPart part, ref FVector pos, ref FMatrix ori, bool world)
    {
        float fraction = unit.GetFractionTime();
        if (part.Name == "turret")
        {
            FMatrix old = FMatrix.MakeRotationOutOfLine(unit.UnknownE4, 0, 0);
            FMatrix current = FMatrix.MakeRotationOutOfLine(unit.UnknownE0, 0, 0);
            ori = Adjust(ori, old, current, fraction);
        }
        else if (part.Name.StartsWith("barrel", StringComparison.Ordinal))
        {
            // Unlike CMCGroundVehicle, a cannon subtracts its current Euler pitch, not a ground-slope query.
            float pitch = unit.CurrentOrientation.Pitch;
            FMatrix old = FMatrix.MakeRotationOutOfLine(0, (unit.UnknownF0 - unit.UnknownF4) - pitch, 0);
            FMatrix current = FMatrix.MakeRotationOutOfLine(0, (unit.UnknownE8 - unit.UnknownF4) - pitch, 0);
            ori = Adjust(ori, old, current, fraction);
        }

        if (part.PartNumber == 0)
        {
            pos.Z -= unit.OldBob + ((unit.Bob - unit.OldBob) * fraction);
        }

        LastYaw = unit.UnknownE0;
        LastPitch = unit.UnknownE8;
    }

    private static FMatrix Adjust(FMatrix orientation, FMatrix old, FMatrix current, float fraction)
    {
        // Both branches call 0040d320 for the final product: use its nine accumulation orders. The intermediate
        // vector subtraction, scalar multiply and addition all store floats before the next operation.
        FMatrix blended = MatrixOps.Add(old, MatrixOps.Scale(MatrixOps.Sub(current, old), fraction));
        // Turret: inline Normalise at 0049546a. Barrel: the equivalent out-of-line body at 00425760.
        return orientation.MultiplyOutOfLine(blended).NormaliseRows();
    }
}
