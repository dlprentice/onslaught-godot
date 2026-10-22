// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// CWeapon::Unknown509140's straight aim against retail's own code, run natively at control word 007F: each yaw and pitch
/// is what retail's block 005093c6..00509434 pushes for FMatrix(yaw, pitch, 0), and each matrix is what that constructor
/// (004062d0) returns. Seven of the vectors are seeded random draws. The first case is the level 100
/// trainer's Forseti Missile launch at P16144: the former inline-model FMatrix gave row 1's Y 3d6d55be where retail's
/// constructor gives 3d6d55bf, and the former FVector::Elevation pitch 3f1c7f79 where retail's sequence gives 3f1c7f7a.
/// The last case aims straight up.
/// </summary>
public sealed class StraightAimTests
{
    public static TheoryData<string, string, string, string, string, string> Cases => new()
    {
        { "c1d48938 3ff13900 41955678", "3fbfff51", "3f1c7f7a", "3d90e993 bf511c54 3f12900a", "3f7f5bbc 3d6d55bf bd26583b", "80000000 3f12ee52 3f51a2d8" },
        { "4213d3c5 c2b8375c 438cb6b6", "c030a43b", "3f9daa5c", "bf6d96c1 3dfda1ef beb3cdd5", "bebea830 be9e0881 3f60107c", "80000000 3f716d6f 3eaa478a" },
        { "c3c3ac1f c2be207a c314b2d8", "3fe79071", "beb51895", "be71b725 bf695dbe beac54fb", "3f78c402 be62c09c bda772ab", "80000000 beb157f2 3f702717" },
        { "42e4f83d c1684270 c296eb53", "bfd934a7", "bf144020", "be00cecd 3f548eeb 3f0afd41", "bf7df773 bdd79cc3 bd8cfc7e", "80000000 bf0c1a23 3f564298" },
        { "43a35dc0 c3c0bef0 43110614", "c01c1089", "3e8f17a5", "bf434a47 3f1f19b1 be36a3ce", "bf2585ce bf3bb67d 3e577c56", "80000000 3e8d3ca3 3f761130" },
        { "428a9162 c3af3cb7 c1f4f28a", "c03c9231", "bdaf1829", "bf7b23c8 3e45dcb9 3c87a938", "be469669 bf7a38f5 bdab8f9d", "80000000 bdaee193 3f7f10a1" },
        { "c3a3ccb5 c31981f3 438b329d", "40009366", "3f27e745", "bed93f35 bf37b708 3f0d5ef8", "3f67cff3 beac2bf2 3e847cfe", "80000000 3f1c1f33 3f4ae255" },
        { "c340b42c c3807fe5 c38af86e", "401fe298", "bf369a0b", "bf4cd145 bee8461f bec8fa51", "3f1993a4 bf1ae2cd bf060455", "80000000 bf2781c3 3f419747" },
        { "00000000 00000000 40a00000", "80000000", "3fc90fdb", "3f800000 80000000 00000000", "00000000 b33bbd2e bf800000", "00000000 3f800000 b33bbd2e" },
    };

    /// <summary>
    /// SpawnRound's tracking aim (00506c6a..00506ccc) run as retail's own block on the same terms: the first five vectors
    /// are seeded draws where the former FVector::Azimuth/Elevation models gave a different yaw or pitch.
    /// </summary>
    public static TheoryData<string, string, string, string, string, string> TrackingCases => new()
    {
        { "c386de02 435a84d2 42233947", "3f63d13e", "3defa3b8", "3f2125a7 bf458e50 3db9c775", "3f46ea86 3f200b8f bd968125", "80000000 3def17d2 3f7e3fdd" },
        { "c33fa66a 433cebcb 421a86d6", "3f4ae5ca", "3e120026", "3f33b778 bf347676 3dcf3f55", "3f36500d 3f31e49f bdcc4bdb", "80000000 3e1181a4 3f7d66ff" },
        { "433c7402 40fae598 42d12576", "bfc3bd57", "3f019878", "3d2a43e4 3f5fb2c4 bef80c09", "bf7fc75b 3d14e8fc bca51e50", "80000000 3ef842f8 3f5fe44e" },
        { "c328f853 c221ec7b 421d5208", "3fe72903", "3e63f318", "be6e92c9 bf72cfef 3e5bd9b1", "3f78f485 be68afc7 3d52aeab", "80000000 3e621255 3f79aeea" },
        { "c320405b c35cb036 c27d1f37", "4020ddc3", "be6975da", "bf4f25cc bf1286cb be07fd6d", "3f166b3f bf49c98b be3b46e5", "80000000 be677170 3f796020" },
        { "00000000 00000000 40a00000", "80000000", "3fc90fdb", "3f800000 80000000 00000000", "00000000 b33bbd2e bf800000", "00000000 3f800000 b33bbd2e" },
        { "00000000 00000000 00000000", "80000000", "00000000", "3f800000 00000000 00000000", "00000000 3f800000 80000000", "80000000 00000000 3f800000" },
        { "40400000 00000000 00000000", "bfc90fdb", "00000000", "b33bbd2e 3f800000 80000000", "bf800000 b33bbd2e 00000000", "80000000 00000000 3f800000" },
    };

    [Theory]
    [MemberData(nameof(TrackingCases))]
    public void TheTrackingAimTakesTheSameSequence(string d, string yaw, string pitch, string row0, string row1, string row2) =>
        TheStraightAimTakesRetailsSequenceAndTheOutOfLineConstructor(d, yaw, pitch, row0, row1, row2);

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheStraightAimTakesRetailsSequenceAndTheOutOfLineConstructor(string d, string yaw, string pitch, string row0, string row1, string row2)
    {
        FVector v = Vector(d);
        (float y, float p) = WeaponMaths.StraightAimAngles(v);
        Assert.Equal(yaw, Bits(y));
        Assert.Equal(pitch, Bits(p));
        FMatrix m = WeaponMaths.StraightAimOrientation(v);
        Assert.Equal(row0, Bits(m.Row0));
        Assert.Equal(row1, Bits(m.Row1));
        Assert.Equal(row2, Bits(m.Row2));
    }

    [Fact]
    public void TheTrainerLaunchDiffersFromTheInlineModel()
    {
        FVector v = Vector("c1d48938 3ff13900 41955678");
        // The former code: FVector::Azimuth and FVector::Elevation's models through the inline-model constructor.
        float formerPitch = (float)Math.Asin(v.Z / v.Magnitude());
        Assert.Equal("3f1c7f79", Bits(formerPitch));
        FMatrix inline = FMatrix.FromYawPitchRoll(-(float)X87.Atan2(v.X, v.Y), formerPitch, 0);
        Assert.Equal("3f7f5bbc 3d6d55be bd26583b", Bits(inline.Row1));
        Assert.Equal("3f7f5bbc 3d6d55bf bd26583b", Bits(WeaponMaths.StraightAimOrientation(v).Row1));
    }

    private static FVector Vector(string words)
    {
        float[] f = words.Split(' ').Select(w => BitConverter.UInt32BitsToSingle(uint.Parse(w, NumberStyles.HexNumber))).ToArray();
        return new FVector(f[0], f[1], f[2]);
    }

    private static string Bits(float f) => BitConverter.SingleToUInt32Bits(f).ToString("x8");

    private static string Bits(FVector v) => $"{Bits(v.X)} {Bits(v.Y)} {Bits(v.Z)}";
}
