// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class CylinderLineTests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static FVector V(uint x, uint y, uint z) => new(F(x), F(y), F(z));

    // A line/cylinder encounter from the level-100 pilot-win tape on the frozen native port. The original executable's
    // Cylinder::CollideWithLine uses Y+Z+X for these two magnitudes (the port's patch 0095); XYZ puts the response's Z
    // one ULP short and changes the next explosion's damage. No retail asset or save is needed for the fixture.
    [Fact]
    public void LineContactUsesTheRetailMagnitudeOrder()
    {
        var cylinder = new CylinderVolume(F(0x3ecccccd), F(0x3f733333));
        var line = new LineVolume(V(0x3ca34000, 0x3eea9000, 0xbf17ee30), new FVector(0, 0, 0));
        var own = new CollisionMove { Pos = V(0x4365194b, 0x43779f11, 0xc128a50a), Move = new FVector(0, 0, 0) };
        var move = new CollisionMove
        {
            Pos = V(0x43651876, 0x43778561, 0xc12677ac),
            Move = V(0xbca34000, 0xbeea9000, 0x3f17ee30),
        };
        var report = new CollisionReport { TestTriangles = 1 };

        Assert.True(cylinder.CollideWithLine(ref own, ref move, line, report));
        Assert.True(move.Stopped);
        Assert.Equal(0x43651c55u, BitConverter.SingleToUInt32Bits(move.Pos.X));
        Assert.Equal(0x4377de5du, BitConverter.SingleToUInt32Bits(move.Pos.Y));
        Assert.Equal(0xc12dac0du, BitConverter.SingleToUInt32Bits(move.Pos.Z));
    }

    // The starting point is a small perturbation of a captured pilot-win encounter. For this radius the X-first
    // sum equals radius squared, whereas retail's Y+Z+X sum is one ULP larger. The true line goes away from the
    // cylinder, so the latter enters the fraction test and rejects it. The adjacent radius is a negative control.
    [Theory]
    [InlineData(0x3f14bb85u, false)]
    [InlineData(0x3f14bb86u, true)]
    public void StartingDistanceUsesTheRetailSumOrder(uint radius, bool expected)
    {
        var cylinder = new CylinderVolume(F(radius), 100.0f);
        var line = new LineVolume(V(0x3c8893d0, 0x3eb72897, 0xbeea3bdf),
                                  V(0x3d0893d0, 0x3f372897, 0xbf6a3bdf));
        var own = new CollisionMove();
        var move = new CollisionMove();
        Assert.Equal(expected, cylinder.CollideWithLine(ref own, ref move, line, new CollisionReport()));
        Assert.False(move.Stopped);
    }

    // The direction is the delta from another captured pilot-win encounter. Its X-first and Y+Z+X squared
    // magnitudes differ by one ULP, placing the computed fraction at 1.0 versus just above 1.0. A zero end
    // lies inside the radius and bypasses the fraction test, providing a distinct control.
    [Theory]
    [InlineData(0x32abcc77u, false)]
    [InlineData(0u, true)]
    public void DirectionDistanceUsesTheRetailSumOrder(uint endZ, bool expected)
    {
        var cylinder = new CylinderVolume(F(0x322bcc77), 100.0f);
        var line = new LineVolume(V(0x3d409000, 0x3edcf600, 0xbf1c90d0), V(0, 0, endZ));
        var own = new CollisionMove();
        var move = new CollisionMove();
        Assert.Equal(expected, cylinder.CollideWithLine(ref own, ref move, line, new CollisionReport()));
        Assert.False(move.Stopped);
    }
}
