// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class CylinderLineTests
{
    // A line/cylinder encounter from the level-100 pilot-win tape on the frozen native port. The original executable's
    // Cylinder::CollideWithLine uses Y+Z+X for these two magnitudes (the port's patch 0095); XYZ puts the response's Z
    // one ULP short and changes the next explosion's damage. No retail asset or save is needed for the fixture.
    [Fact]
    public void LineContactUsesTheRetailMagnitudeOrder()
    {
        static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
        static FVector V(uint x, uint y, uint z) => new(F(x), F(y), F(z));
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
}
