// SPDX-License-Identifier: GPL-3.0-only
// Expectations from pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// CPanCamera::Update's orientation block (00419bb0..00419d07), retail's own code run natively at control word 0x7F
// on recorded and generated position pairs (the retail oracle's "pan-camera-orientation-20261022" group).

using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class PanCameraOracleTests
{
    private const string Group = "pan-camera-orientation-20261022";

    [RetailOracleFact(Group)]
    public void FacingMatchesRetailsInlineBlock()
    {
        var lines = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases(Group))
        {
            Assert.Equal("PanCamera", function); Assert.Equal(6, w.Length);
            var orientation = new FMatrix();
            Assert.True(PanCamera.Facing(RetailOracle.V(w, 0) - RetailOracle.V(w, 3), ref orientation));
            string line = $"PanCamera {RetailOracle.Hv(orientation.Row0)} {RetailOracle.Hv(orientation.Row1)} "
                + RetailOracle.Hv(orientation.Row2);
            lines.Add(line);
            Assert.True(line == expected, $"case {lines.Count - 1}:\nours {line}\nretail {expected}");
        }

        Assert.Equal(20006, lines.Count);
        RetailOracle.WriteOurs(Group, lines.ToArray());
    }

    /// <summary>With the camera on the thing (magnitude 0) the block is skipped and the orientation kept.</summary>
    [Fact]
    public void FacingLeavesTheOrientationAtZeroMagnitude()
    {
        FMatrix orientation = FMatrix.Identity;
        Assert.False(PanCamera.Facing(new FVector(0, 0, 0), ref orientation));
        Assert.Equal(FMatrix.Identity, orientation);
    }
}
