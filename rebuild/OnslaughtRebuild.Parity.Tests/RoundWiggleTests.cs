// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// CRound::Move's wiggle against retail's own code run natively at control word 007F: each expected velocity is what
/// retail's block 004d907d..004d9172 (the wiggle: FMatrix(yaw, pitch, 0) at 004062d0 times the velocity) or
/// 004d921e..004d92a2 (the unwiggle: FMatrix(-yaw, -pitch, 0) times the velocity after the flight) produces. The inputs
/// are the level 100 trainer's Forseti Missiles (P16400..16630); all but the last two cases of each set are frames the
/// former inline-model matrix and (Z + Y) + X product got wrong.
/// </summary>
public sealed class RoundWiggleTests
{
    public static TheoryData<string, string, string, string> Wiggles => new()
    {
        { "3c0eb64b", "bbc813db", "bdfc7408 3ef80d2d 3f0f20eb", "be0292cc 3ef93c47 3f0e5e5f" },
        { "3b5cb515", "3c12056f", "be0aa985 3ef3dabb 3f103558", "be0c4957 3ef10a17 3f114a10" },
        { "bc8b297e", "3b8cee8d", "be0c2f47 3ef35352 3f1056f5", "be03f0a9 3ef33cc4 3f10dc92" },
        { "3c689275", "bc834021", "be1eeeff 3eec151f 3f121f02", "be25c077 3eef94fd 3f103613" },
        { "3c6d2b57", "bca06387", "be22cfde 3eea3a5d 3f129979", "be29bda4 3eeeb83e 3f104753" },
        { "3ca3107a", "3c4cda50", "be24a60e 3ee93952 3f12df07", "be2dc0d7 3ee3d958 3f145156" },
        { "39470d23", "bc83847d", "be2c7e4b 3ee54bdb 3f13d9e3", "be2c9509 3ee9ff5d 3f11fdd5" },
        { "3ba0ae97", "bc1f01a8", "be2ec669 3ee439ea 3f1418ea", "be310a0f 3ee6a8a1 3f12fb9f" },
        { "bc787d50", "bc74b2db", "be3110d1 3ee32d06 3f1454ad", "be2a0560 3ee8e5ac 3f129e2a" },
        { "3c909f8b", "3b7f0378", "be33a2d3 3ee2036d 3f1494d1", "be3b8c02 3edf3c18 3f150519" },
        { "3bfc8012", "3bfbff64", "be3405f7 3ee1d672 3f149e68", "be37768b 3eded84e 3f157b96" },
        { "3c825032", "bc48ba11", "be319b35 3ee307b2 3f145896", "be38ebe7 3ee534c2 3f12f1b8" },
        { "3c4a3c1b", "bc1d4caf", "be1e8c58 3eebb541 3f124c2f", "be246c89 3eed8277 3f1128cd" },
        { "bc9bbe95", "bc545c77", "be153312 3eefb444 3f11490e", "be0bea90 3ef4d308 3f0fb841" },
        { "bbea0718", "bb7e021c", "be1208d4 3ef0d149 3f11069c", "be0e9330 3ef27489 3f108edc" },
        { "bbcc0935", "bc6e44d4", "be0ea66a 3ef23502 3f10a849", "be0b9435 3ef6d3fe 3f0ee186" },
        { "ba6e6d0a", "3ca4fb12", "be06f457 3ef4d815 3f1001ee", "be068507 3eef0e41 3f127193" },
        { "3c833411", "3c8d1010", "bdeb39c9 3efafcad 3f0e4dce", "bdfaf534 3ef5136d 3f107195" },
        { "3bf4e06e", "3b2eac2c", "bde1af6d 3efcf665 3f0dab60", "bde937c8 3efbc719 3f0e018c" },
        { "bc9d9e73", "bca2927b", "bd4a9ee4 3f09130c 3f05d7a3", "bd1f9405 3f0bebd0 3f03188c" },
        { "bbdfb3cc", "3c7583f5", "bdb36747 3efb737f 3f0f5e07", "bdaca6a2 3ef76d40 3f113c32" },
        { "bc193c86", "bbd55b25", "bc71c817 3f0def89 3f013e00", "bc1c4f80 3f0ecda3 3f0050b7" },
        { "bb3fee2c", "bc835d9e", "bd9fd6b2 3efef9d0 3f0e2aec", "bd9ccc4c 3f01cf25 3f0c1ae7" },
        { "bc2bad75", "3c4a3162", "bba02827 3f0f1c16 3efffb2f", "3a6d44cb 3f0d8651 3f01bf35" },
        { "3c8c2178", "3c6ead61", "bd9217cd 3f009f07 3f0d6037", "bda3636f 3efc6f39 3f0f3c07" },
        { "bca719f0", "3b327145", "bd8c3876 3f013261 3f0cf17c", "bd6e585c 3f0124ac 3f0d4b68" },
        { "3b98083a", "3c69810d", "bd89545f 3f018f06 3f0ca7c4", "bd8e0fe2 3efeeb7f 3f0e7cca" },
        { "bc06d0f3", "3b89195f", "bdfc8397 3ef80d31 3f0f207b", "bdf46173 3ef75ce0 3f0fa501" },
        { "3c9878d3", "bc16e6b3", "be0de165 3ef2ba72 3f107cc5", "be16fd4f 3ef40448 3f0f5d0c" },
    };

    public static TheoryData<string, string, string, string> Unwiggles => new()
    {
        { "3c6d2b57", "bca06387", "be29bda3 3eeeb83d 3f104753", "be22fa12 3eea3a95 3f129675" },
        { "3ca3107a", "3c4cda50", "be2dc0d7 3ee3d957 3f145156", "be248070 3ee9392b 3f12e1b9" },
        { "3ca174f9", "bc78b84b", "be33b593 3ee90c99 3f11d54f", "be2aaa07 3ee65240 3f1395d6" },
        { "bc787d50", "bc74b2db", "be2a0560 3ee8e5ab 3f129e2a", "be30ee9b 3ee32d1e 3f145731" },
        { "3c825032", "bc48ba11", "be38ebe7 3ee534c1 3f12f1b8", "be31b8aa 3ee307d8 3f145652" },
        { "3c833411", "3c8d1010", "bdfaf534 3ef5136c 3f107195", "bdeae8ce 3efafc8e 3f0e4ff5" },
        { "bc9d9e73", "bca2927b", "bd1f9405 3f0bebd1 3f03188c", "bd49cfb6 3f091329 3f05d8be" },
        { "3c73cefc", "3c5bf6be", "3c3a88ff 3f112b9d 3efb3ddd", "3ca32e12 3f12c8be 3ef75237" },
        { "3c0eb64b", "bbc813db", "be0292cd 3ef93c47 3f0e5e5f", "bdfc8397 3ef80d31 3f0f207b" },
        { "bc06d0f3", "3b89195f", "bdf46173 3ef75ce0 3f0fa501", "bdfc8db3 3ef80d2e 3f0f2036" },
    };

    [Theory]
    [MemberData(nameof(Wiggles))]
    public void TheWiggleTurnsTheVelocityAsRetailDoes(string yaw, string pitch, string velocity, string expected)
    {
        FMatrix wiggle = FMatrix.MakeRotationOutOfLine(Float(yaw), Float(pitch), 0);
        Assert.Equal(expected, Bits(Round.WiggleVelocity(wiggle, Vector(velocity))));
    }

    [Theory]
    [MemberData(nameof(Unwiggles))]
    public void TheUnwiggleTurnsTheVelocityBackAsRetailDoes(string yaw, string pitch, string velocity, string expected)
    {
        FMatrix unwiggle = FMatrix.MakeRotationOutOfLine(-Float(yaw), -Float(pitch), 0);
        Assert.Equal(expected, Bits(Round.UnwiggleVelocity(unwiggle, Vector(velocity))));
    }

    private static float Float(string word) => BitConverter.UInt32BitsToSingle(uint.Parse(word, NumberStyles.HexNumber));

    private static FVector Vector(string words)
    {
        float[] f = words.Split(' ').Select(Float).ToArray();
        return new FVector(f[0], f[1], f[2]);
    }

    private static string Bits(FVector v) =>
        $"{BitConverter.SingleToUInt32Bits(v.X):x8} {BitConverter.SingleToUInt32Bits(v.Y):x8} {BitConverter.SingleToUInt32Bits(v.Z):x8}";
}
