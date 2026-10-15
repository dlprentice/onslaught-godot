// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailUnitEulerTests
{
    // Outputs are from the unchanged pristine 847-byte function, executed in
    // an isolated ELF32 probe with an explicit PC24/RN control word. Only its
    // virtual multiplier is supplied; no game frame, resource or RNG is used.
    // Specimen/body pins and private probe path are in CComplexThing.cpp.md.
    [Theory]
    [MemberData(nameof(NativeCases))]
    public void SmoothMatchesIsolatedRetailRoutine(string scenario,
        Level100FloatVector3Bits current, Level100FloatVector3Bits desired,
        Level100FloatVector3Bits rate, float multiplier, Level100FloatVector3Bits expected)
    {
        Level100FloatVector3Bits actual = RetailUnitEuler.Smooth(current, desired, rate, multiplier);
        Assert.True(expected == actual, $"{scenario}: expected {expected}, actual {actual}");
    }

    public static IEnumerable<object[]> NativeCases()
    {
        Level100FloatVector3Bits zero = Uniform(0);
        Level100FloatVector3Bits one = Uniform(0x3f800000);
        yield return ["zero", zero, zero, one, 1f, zero];
        yield return ["equal-negative-zero", Uniform(0x80000000), zero, one, 1f, Uniform(0x80000000)];
        yield return ["equal-outside-pi", Floats(7, -7, 7), Floats(7, -7, 7), one, 1f, Floats(7, -7, 7)];
        yield return ["wrapped-versus-pitch", Floats(3.125f, 3.125f, 3.125f), Floats(-3.125f, -3.125f, -3.125f),
            one, 1f, Words(0x4048365f, 0x40200000, 0x4048365f)];
        yield return ["reverse-wrapped-versus-pitch", Floats(-3.125f, -3.125f, -3.125f), Floats(3.125f, 3.125f, 3.125f),
            one, 1f, Words(0xc048365f, 0xc0200000, 0xc048365f)];
        yield return ["pi-direction", zero, Uniform(0x40490fdb), one, 1f, Uniform(0x3ea0d97c)];
        yield return ["pi-next-direction", zero, Uniform(0x40490fdc), one, 1f, Words(0xbea0d97d, 0x3ea0d97d, 0xbea0d97d)];
        yield return ["negative-pi-direction", zero, Uniform(0xc0490fdb), one, 1f, Uniform(0xbea0d97c)];
        yield return ["negative-pi-next-direction", zero, Uniform(0xc0490fdc), one, 1f, Words(0x3ea0d97d, 0xbea0d97d, 0x3ea0d97d)];
        yield return ["equal-cap", zero, one, Uniform(0x3dcccccd), 1f, Uniform(0x3dcccccd)];
        yield return ["lower-cap", zero, one, Uniform(0x3dcccccc), 1f, Uniform(0x3dcccccc)];
        yield return ["plane-noncanonical-roll", zero, Words(0, 0, 0x40e2a975), one, 1f, Words(0, 0, 0xbf35545e)];
        yield return ["single-wrap-only", Floats(15, 15, 15), Floats(16, 16, 16), one, 1f,
            Words(0x410d11ac, 0x4171999a, 0x410d11ac)];
        yield return ["four-tick-multiplier", Floats(.3f, -.2f, .1f), Floats(.6f, -.9f, .8f),
            Floats(.01f, .3f, .2f), 4f, Words(0x3eae147b, 0xbef5c290, 0x3ec28f5c)];
        yield return ["coefficient-not-division", zero, Floats(.1f, .1f, .1f), one, 1f, Uniform(0x3c23d70b)];
        yield return ["retained-subnormal-step", Uniform(1), Uniform(6), one, 1f, Uniform(2)];
        yield return ["half-pi-boundary", Uniform(0xbfc90fdb), Floats(5, 5, 5), one, 1f,
            Words(0xc00e9585, 0xbf69e957, 0xc00e9585)];
        yield return ["past-half-pi", Uniform(0xbfc90fdc), Floats(5, 5, 5), one, 1f,
            Words(0xbfccbe4e, 0xbf69e959, 0xbfccbe4e)];
        Level100FloatVector3Bits mixed = Words(0x3f333333, 0x3f8ccccd, 0xbecccccd);
        yield return ["equal-mixed", mixed, mixed, one, 1f, mixed];
    }

    // Matrix outputs come from the same unchanged native function. Twelve
    // additional inputs were read at actual Plane calls in the isolated
    // WineD3D observation. Eleven outputs also match the following live pose.
    // These finite cases do not establish universal managed-trig equivalence.
    [Theory]
    [MemberData(nameof(NativeBasisCases))]
    public void BasisMatchesNativeSuffix(string scenario,
        Level100FloatVector3Bits euler, Level100FloatBasis3Bits expected)
    {
        Level100FloatBasis3Bits actual = RetailUnitEuler.BuildBasis(euler);
        Assert.True(expected == actual, $"{scenario}: expected {expected}, actual {actual}");
    }

    public static IEnumerable<object[]> NativeBasisCases()
    {
        yield return ["zero", Words(0x00000000, 0x00000000, 0x00000000),
            Basis(0x3f800000, 0x80000000, 0x00000000,
                  0x00000000, 0x3f800000, 0x00000000,
                  0x80000000, 0x00000000, 0x3f800000)];
        yield return ["equal-negative-zero", Words(0x80000000, 0x80000000, 0x80000000),
            Basis(0x3f800000, 0x00000000, 0x00000000,
                  0x00000000, 0x3f800000, 0x00000000,
                  0x00000000, 0x80000000, 0x3f800000)];
        yield return ["equal-outside-pi", Words(0x40e00000, 0xc0e00000, 0x40e00000),
            Basis(0x3f5a190c, 0xbefd9872, 0x3e2df92a,
                  0x3e2df92c, 0x3f11809b, 0x3f4e173b,
                  0xbefd9872, 0xbf283046, 0x3f11809b)];
        yield return ["wrapped-versus-pitch", Words(0x4048365f, 0x40200000, 0x4048365f),
            Basis(0x3f7fed8b, 0x3c2e3ade, 0xbcadccc0,
                  0xbcadccc0, 0x3f4d131f, 0xbf192304,
                  0x3c2e3ade, 0x3f193578, 0x3f4d131f)];
        yield return ["reverse-wrapped-versus-pitch", Words(0xc048365f, 0xc0200000, 0xc048365f),
            Basis(0x3f7ffb5d, 0xbc2e3ade, 0x3baea162,
                  0x3baea162, 0x3f4d131f, 0x3f193a1c,
                  0xbc2e3ade, 0xbf193578, 0x3f4d131f)];
        yield return ["pi-direction", Words(0x3ea0d97c, 0x3ea0d97c, 0x3ea0d97c),
            Basis(0x3f600001, 0xbe967919, 0x3ec4f8c6,
                  0x3ec4f8c6, 0x3f678ddf, 0xbe3c6ef6,
                  0xbe967919, 0x3e9e377a, 0x3f678ddf)];
        yield return ["pi-next-direction", Words(0xbea0d97d, 0x3ea0d97d, 0xbea0d97d),
            Basis(0x3f5ffffe, 0x3e967919, 0xbec4f8c6,
                  0xbec4f8c6, 0x3f678ddd, 0xbe3c6ef2,
                  0x3e967919, 0x3e9e377b, 0x3f678ddd)];
        yield return ["negative-pi-direction", Words(0xbea0d97c, 0xbea0d97c, 0xbea0d97c),
            Basis(0x3f6f1bbd, 0x3e967919, 0xbe4ff2d9,
                  0xbe4ff2d9, 0x3f678ddf, 0x3ec00001,
                  0x3e967919, 0xbe9e377a, 0x3f678ddf)];
        yield return ["negative-pi-next-direction", Words(0x3ea0d97d, 0xbea0d97d, 0x3ea0d97d),
            Basis(0x3f6f1bbc, 0xbe967919, 0x3e4ff2d8,
                  0x3e4ff2d8, 0x3f678ddd, 0x3ec00001,
                  0xbe967919, 0xbe9e377b, 0x3f678ddd)];
        yield return ["equal-cap", Words(0x3dcccccd, 0x3dcccccd, 0x3dcccccd),
            Basis(0x3f7d319d, 0xbdcb6ffa, 0x3ddfbf4b,
                  0x3ddfbf4c, 0x3f7d72d3, 0xbdb6025d,
                  0xbdcb6ff9, 0x3dcc7577, 0x3f7d72d3)];
        yield return ["lower-cap", Words(0x3dcccccc, 0x3dcccccc, 0x3dcccccc),
            Basis(0x3f7d319d, 0xbdcb6ff9, 0x3ddfbf4a,
                  0x3ddfbf4b, 0x3f7d72d3, 0xbdb6025c,
                  0xbdcb6ff8, 0x3dcc7576, 0x3f7d72d3)];
        yield return ["plane-noncanonical-roll", Words(0x00000000, 0x00000000, 0xbf35545e),
            Basis(0x3f426bc3, 0x80000000, 0xbf268af4,
                  0x00000000, 0x3f800000, 0x80000000,
                  0x3f268af4, 0x00000000, 0x3f426bc3)];
        yield return ["single-wrap-only", Words(0x410d11ac, 0x4171999a, 0x410d11ac),
            Basis(0x3ef988cc, 0x3ef00c9b, 0xbf3c950a,
                  0xbf3c950a, 0x3f2c79d8, 0xbd6fe560,
                  0x3ef00c9b, 0x3f1239f0, 0x3f2c79d8)];
        yield return ["four-tick-multiplier", Words(0x3eae147b, 0xbef5c290, 0x3ec28f5c),
            Basis(0x3f6ec06e, 0xbe977346, 0x3e53a2a1,
                  0x3e17c6d9, 0x3f56126c, 0x3f072a1b,
                  0xbea87349, 0xbeec6e53, 0x3f52df5e)];
        yield return ["coefficient-not-division", Words(0x3c23d70b, 0x3c23d70b, 0x3c23d70b),
            Basis(0x3f7ff961, 0xbc23d43f, 0x3c2577a4,
                  0x3c2577a4, 0x3f7ff972, 0xbc222ebb,
                  0xbc23d43f, 0x3c23d658, 0x3f7ff972)];
        yield return ["retained-subnormal-step", Words(0x00000002, 0x00000002, 0x00000002),
            Basis(0x3f800000, 0x80000002, 0x00000002,
                  0x00000002, 0x3f800000, 0x80000002,
                  0x80000002, 0x00000002, 0x3f800000)];
        yield return ["half-pi-boundary", Words(0xc00e9585, 0xbf69e957, 0xc00e9585),
            Basis(0x3f5e9529, 0x3ef79da1, 0x3dce3c1c,
                  0x3dce3c1c, 0xbebf050c, 0x3f6c1ce6,
                  0x3ef79da1, 0xbf4ab20e, 0xbebf050c)];
        yield return ["past-half-pi", Words(0xbfccbe4e, 0xbf69e959, 0xbfccbe4e),
            Basis(0x3f4abd59, 0x3f1c4d4c, 0x3bc42120,
                  0x3bc42118, 0xbc8fe4e0, 0x3f7ff4b7,
                  0x3f1c4d4c, 0xbf4ab20f, 0xbc8fe4e0)];
        yield return ["retained-trig", Words(0x3f333333, 0x3f8ccccd, 0xbecccccd),
            Basis(0x3f6d9446, 0xbe959d27, 0x3e6c827e,
                  0x3ea7e592, 0x3eb1a0bc, 0xbf60f234,
                  0x3e34e0c2, 0x3f64262b, 0x3ed5e887)];
        yield return ["live-1", Words(0x4048fdcf, 0xbd32b8c2, 0xbd32b8c2),
            Basis(0xbf7fc1b9, 0xba9039e2, 0x3d3277e3,
                  0xba5250e4, 0xbf7fc196, 0xbd32b10f,
                  0x3d327eb6, 0xbd32aa3e, 0x3f7f834f)];
        yield return ["live-2", Words(0x4048ed91, 0xbd3c1492, 0xbd255c95),
            Basis(0xbf7fcab6, 0xbb090180, 0x3d24ec59,
                  0x397a0100, 0xbf7fbac7, 0xbd3c34e5,
                  0x3d25247a, 0xbd3c03a7, 0x3f7f8594)];
        yield return ["live-3", Words(0x4048dede, 0xbd44a03f, 0xbd18da4c),
            Basis(0xbf7fd26e, 0xbb43b8ac, 0x3d183ab3,
                  0x3a9cf6bc, 0xbf7fb436, 0xbd44dea0,
                  0x3d18a426, 0xbd448cea, 0x3f7f86ed)];
        yield return ["live-4", Words(0x4048d190, 0xbd4c5c04, 0xbd0d3644),
            Basis(0xbf7fd904, 0xbb78db01, 0x3d0c6824,
                  0x3b085c5e, 0xbf7fadfa, 0xbd4cb048,
                  0x3d0d0221, 0xbd4c4650, 0x3f7f878e)];
        yield return ["live-5", Words(0x4048c586, 0xbd535bb0, 0xbd02639d),
            Basis(0xbf7fde9c, 0xbb94767b, 0x3d016864,
                  0x3b3d9612, 0xbf7fa818, 0xbd53bf21,
                  0x3d02318e, 0xbd5343ae, 0x3f7f879c)];
        yield return ["live-6", Words(0x4048baa1, 0xbd59b12d, 0xbcf0ab9b),
            Basis(0xbf7fe35d, 0xbbaa3575, 0x3cee5ea9,
                  0x3b6e7cea, 0xbf7fa294, 0xbd5a1e5f,
                  0x3cf04bc3, 0xbd5996f2, 0x3f7f8739)];
        yield return ["live-7", Words(0x4048b0c4, 0xbd5f6caf, 0xbcde0198),
            Basis(0xbf7fe765, 0xbbbde495, 0x3cdb6259,
                  0x3b8db295, 0xbf7f9d6c, 0xbd5fdf46,
                  0x3cdda61e, 0xbd5f5053, 0x3f7f8680)];
        yield return ["live-8", Words(0x4048a7d7, 0xbd649cde, 0xbcccb216),
            Basis(0xbf7feacd, 0xbbcfb3fc, 0x3cc9c51c,
                  0x3ba24acd, 0xbf7f98a2, 0xbd65115e,
                  0x3ccc5b0b, 0xbd647e7c, 0x3f7f8586)];
        yield return ["live-9", Words(0x40489fc3, 0xbd694efc, 0xbcbca6ed),
            Basis(0xbf7fedac, 0xbbdfd1bf, 0x3cb970f0,
                  0x3bb52b3f, 0xbf7f942f, 0xbd69c2a8,
                  0x3cbc5457, 0xbd692eb1, 0x3f7f845d)];
        yield return ["live-10", Words(0x40489873, 0xbd6d8f07, 0xbcadcafc),
            Basis(0xbf7ff019, 0xbbee67ef, 0x3caa50be,
                  0x3bc6764d, 0xbf7f9012, 0xbd6dffc1,
                  0x3cad7cd9, 0xbd6d6cf0, 0x3f7f8316)];
        yield return ["live-11", Words(0x404891d5, 0xbd7167d9, 0xbca00a23),
            Basis(0xbf7ff223, 0xbbfb9a99, 0x3c9c505c,
                  0x3bd64a31, 0xbf7f8c47, 0xbd71d406,
                  0x3c9fc066, 0xbd714414, 0x3f7f81bb)];
        yield return ["live-12", Words(0x40488bd8, 0xbd74e344, 0xbc935153),
            Basis(0xbf7ff3d7, 0xbc03c5e4, 0x3c8f5c9a,
                  0x3be4c4b0, 0xbf7f88c8, 0xbd7549ae,
                  0x3c930bea, 0xbd74bded, 0x3f7f8054)];
    }

    private static Level100FloatBasis3Bits Basis(
        uint ax, uint ay, uint az, uint bx, uint by, uint bz, uint cx, uint cy, uint cz) =>
        new(unchecked((int)ax), unchecked((int)ay), unchecked((int)az),
            unchecked((int)bx), unchecked((int)by), unchecked((int)bz),
            unchecked((int)cx), unchecked((int)cy), unchecked((int)cz));

    private static Level100FloatVector3Bits Uniform(uint value) => Words(value, value, value);
    private static Level100FloatVector3Bits Words(uint x, uint y, uint z) =>
        new(unchecked((int)x), unchecked((int)y), unchecked((int)z));
    private static Level100FloatVector3Bits Floats(float x, float y, float z) =>
        new(BitConverter.SingleToInt32Bits(x), BitConverter.SingleToInt32Bits(y), BitConverter.SingleToInt32Bits(z));
}
