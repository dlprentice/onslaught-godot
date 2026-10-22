// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/MCGroundVehicle.cpp (CMCGroundVehicle: GetPartTransform 00496ac0; reconstructed from the
// retail program, not in Stuart's release).

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CMCGroundVehicle: the motion controller of a ground vehicle without wheel motion. The "turret" part follows the unit's
/// yaw and the "barrel" parts its pitch, less the slope of the ground under the turret's heading; the hull (part 0) sits
/// by the unit's bob. Each is interpolated from last frame's value by GetFractionTime.
/// </summary>
public sealed class GroundVehicleMotionController(GroundVehicle unit) : IPartController
{

    /// <summary>GetPartTransform (00496ac0).</summary>
    public void GetPartTransform(MeshPart part, ref FVector pos, ref FMatrix ori, bool world)
    {
        float frac = unit.GetFractionTime();
        if (part.Name == "turret")
        {
            FMatrix oldOri = FMatrix.MakeRotationOutOfLine(unit.UnknownE4, 0, 0);
            FMatrix newOri = FMatrix.MakeRotationOutOfLine(unit.UnknownE0, 0, 0);
            ori = ori.Multiply(oldOri.Add(newOri.Add(oldOri.Scale(-1.0f)).Scale(frac))).NormaliseRows();
        }
        else if (part.Name.StartsWith("barrel", StringComparison.Ordinal))
        {
            // The ground's slope along the turret's heading.
            FMatrix heading = unit.Orientation.Multiply(FMatrix.MakeRotationOutOfLine(unit.UnknownE4, 0, 0));
            FVector up = heading.Transform(new FVector(0, 1, 0));
            float slope = Elevation(up);
            FMatrix oldOri = FMatrix.MakeRotationOutOfLine(0, unit.UnknownF0 - unit.UnknownF4 - slope, 0);
            FMatrix newOri = FMatrix.MakeRotationOutOfLine(0, unit.UnknownE8 - unit.UnknownF4 - slope, 0);
            ori = ori.Multiply(oldOri.Add(newOri.Add(oldOri.Scale(-1.0f)).Scale(frac))).NormaliseRows();
        }

        if (part.PartNumber == 0)
        {
            float drop = unit.OldBob;
            pos.Z -= drop + ((unit.Bob - drop) * frac);
        }
    }

    /// <summary>FVector::Elevation (maths.h): asinf(Z / |v|), 0 for a zero vector.</summary>
    private static float Elevation(FVector v)
    {
        float m = v.Magnitude();
        return m > 0.0f ? (float)X87.Asin(v.Z / m) : 0.0f;
    }
}
