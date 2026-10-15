// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Original-code observations from provider-selection-20260912.py under the
/// private linux-route-20260906-af1sa_l9 owner. Pristine 74154bfa…7750,
/// selector body 8cafb481…106df, PC24/RN. No retail payload is needed here.
/// This is an isolated selector contract, not the actor firing integration.
/// </summary>
public sealed class RetailUnitWeaponSelectionTests
{
    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    private static float Value(int bits) => BitConverter.Int32BitsToSingle(bits);
    private static Level100FloatVector3Bits Position(float x, float y = 0, float z = 0) =>
        new(Bits(x), Bits(y), Bits(z));
    private static readonly RetailUnitAttackSelection Previous = new(20, 123);

    private static RetailUnitWeaponSelectionCandidate Weapon(int identity = 10) =>
        new(identity, 1, 1, 0, 3, Bits(9), Bits(0), Bits(200), Bits(-10), Bits(10000));

    private static RetailUnitAttackSelection Choose(
        IReadOnlyList<RetailUnitWeaponSelectionCandidate> weapons,
        int nowBits = 0x41200000,
        Level100FloatVector3Bits? target = null,
        uint mask = 1,
        int terrain = 0, int water = 0) =>
        RetailUnitWeaponSelection.Select(weapons, 0, Previous, default,
            target ?? Position(100), mask, terrain, water, nowBits);

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 20)]
    public void EqualScoresRetainAttachmentOrder(bool reverse, int expected)
    {
        RetailUnitWeaponSelectionCandidate[] weapons = [Weapon(10), Weapon(20)];
        if (reverse) Array.Reverse(weapons);
        Assert.Equal(new RetailUnitAttackSelection(expected, null), Choose(weapons));
    }

    [Fact]
    public void EmptyListClearsBothSelections() =>
        Assert.Equal(new RetailUnitAttackSelection(null, null), Choose([]));

    [Fact]
    public void NullTargetPreservesBothSelections() =>
        Assert.Equal(Previous, RetailUnitWeaponSelection.Select([Weapon()], 0,
            Previous, default, null, 1, 0, 0, Bits(10)));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(0, -1)]
    public void AnySignedIncompleteBurstPreservesBothSelections(int index, int counter)
    {
        RetailUnitWeaponSelectionCandidate[] weapons = [Weapon(10), Weapon(20)];
        weapons[index] = weapons[index] with { BurstCounter = counter };
        Assert.Equal(Previous, Choose(weapons));
    }

    [Fact]
    public void CompletedBurstReleasesSelection() =>
        Assert.Equal(new RetailUnitAttackSelection(10, null),
            Choose([Weapon() with { BurstCounter = 3 }, Weapon(20)]));

    [Fact]
    public void BurstScanPrecedesActiveAndMaskEligibility() =>
        Assert.Equal(Previous, Choose([
            Weapon() with { ActiveWord = 0, TargetMask = 0, BurstCounter = 1 }, Weapon(20)]));

    [Theory]
    [InlineData(0, 1u, 1u, 20)]
    [InlineData(-1, 1u, 1u, 10)]
    [InlineData(1, 2u, 1u, 20)]
    [InlineData(1, 1u, 4u, null)]
    public void ActiveWordAndMaskAdmitCandidates(int active, uint weaponMask, uint targetMask, int? expected)
    {
        var result = Choose([Weapon() with { ActiveWord = active, TargetMask = weaponMask }, Weapon(20)],
            mask: targetMask);
        Assert.Equal(new RetailUnitAttackSelection(expected, null), result);
    }

    [Fact]
    public void Shared80000MaskAddsTwoMillion()
    {
        var bonus = Weapon(20) with { TargetMask = 0x80001 };
        Assert.Equal(new RetailUnitAttackSelection(20, null), Choose([Weapon(), bonus], mask: 0x80001));
        Assert.Equal(0x4a742400, Bits(RetailUnitWeaponSelection.Score(bonus, Position(100), 0x80001, 100, 0, 0, 10)!.Value));
    }

    [Theory]
    [InlineData(0x411fffff, 20)]
    [InlineData(0x41200000, 20)]
    [InlineData(0x41200001, 10)]
    public void ReadinessRequiresNowStrictlyAfterDeadline(int nowBits, int expected) =>
        Assert.Equal(new RetailUnitAttackSelection(expected, null),
            Choose([Weapon() with { ReadyAtTimeFloatBits = Bits(10) }, Weapon(20)], nowBits));

    [Theory]
    [InlineData(0x411fffff, 0x49742400)]
    [InlineData(0x41200000, 0x49f42400)]
    [InlineData(0x41200001, 0x49f42400)]
    [InlineData(0x419fffff, 0x49f42400)]
    [InlineData(0x41a00000, 0x49f42400)]
    [InlineData(0x41a00001, 0x49742400)]
    public void RangeEndpointsAreInclusiveAndOutsideStillScores(int distanceBits, int expectedScoreBits)
    {
        var weapon = Weapon() with { MinimumRangeFloatBits = Bits(10), MaximumRangeFloatBits = Bits(20) };
        var target = new Level100FloatVector3Bits(distanceBits, 0, 0);
        Assert.Equal(new RetailUnitAttackSelection(10, null), Choose([weapon], target: target));
        Assert.Equal(expectedScoreBits,
            Bits(RetailUnitWeaponSelection.Score(weapon, target, 1, Value(distanceBits), 0, 0, 10)!.Value));
    }

    [Theory]
    [InlineData(true, -1, 10)]
    [InlineData(true, 0, null)]
    [InlineData(true, 1, null)]
    [InlineData(false, -1, null)]
    [InlineData(false, 0, null)]
    [InlineData(false, 1, 10)]
    public void HeightEndpointsAreStrict(bool minimum, int limit, int? expected)
    {
        var weapon = minimum ? Weapon() with { MinimumTargetHeightFloatBits = Bits(limit) }
            : Weapon() with { MaximumTargetHeightFloatBits = Bits(limit) };
        Assert.Equal(new RetailUnitAttackSelection(expected, null), Choose([weapon]));
    }

    [Theory]
    [InlineData(-5, 50, 10)]
    [InlineData(50, -5, 10)]
    [InlineData(50, -10, null)]
    public void HeightUsesLowerOfTerrainAndWater(float terrain, float water, int? expected) =>
        Assert.Equal(new RetailUnitAttackSelection(expected, null),
            Choose([Weapon()], terrain: Bits(terrain), water: Bits(water)));

    [Fact]
    public void FarOutsideCandidateCanOutscoreInRangeCandidate()
    {
        var outside = Weapon() with { MaximumRangeFloatBits = Bits(5) };
        var inside = Weapon(20) with { MaximumRangeFloatBits = Bits(3_000_000) };
        Assert.Equal(new RetailUnitAttackSelection(10, null),
            Choose([outside, inside], nowBits: Bits(0), target: Position(2_000_000)));
        Assert.Equal(0x49f423d8,
            Bits(RetailUnitWeaponSelection.Score(outside, Position(2_000_000), 1, 2_000_000, 0, 0, 0)!.Value));
    }

    [Theory]
    [InlineData(0, 3, 4, 12, 0x41500000)]
    [InlineData(0, 16777216, 4096, 4096, 0x4b800000)]
    [InlineData(1, 33554432, 33554432, 33554432, 0x4c5db3d7)]
    [InlineData(0, 0.1f, 0.1f, 1.1f, 0x3f8df578)]
    public void RawDistanceMatchesOriginalPc24Observations(float owner, float x, float y, float z, int expectedBits) =>
        Assert.Equal(expectedBits, Bits(RetailUnitWeaponSelection.RawDistance(
            Position(owner, owner, owner), Position(x, y, z))));

    [Theory]
    [InlineData(0, 16777216, 4096, 4096, 4096, 0x4b87a0bc)]
    [InlineData(1, 33554432, 33554432, 33554432, 33554432, 0x4c618435)]
    public void ScoreRetainsNativeArithmeticAndFloatStores(float owner, float x, float y, float z,
        float ground, int expectedBits)
    {
        var target = Position(x, y, z);
        float distance = RetailUnitWeaponSelection.RawDistance(Position(owner, owner, owner), target);
        Assert.Equal(expectedBits, Bits(RetailUnitWeaponSelection.Score(Weapon(), target, 1,
            distance, Bits(ground), Bits(ground), 10)!.Value));
    }

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(0, false, false)]
    [InlineData(0, true, true)]
    public void UnsupportedRoutesAreNotSilentlyApproximated(int spawners, bool currentMode, bool ballistic) =>
        Assert.Throws<NotSupportedException>(() => RetailUnitWeaponSelection.Select(
            [Weapon() with { HasCurrentMode = currentMode, UsesBallisticArc = ballistic }],
            spawners, Previous, default, Position(100), 1, 0, 0, Bits(10)));

    [Fact]
    public void NonfinitePositionIsOutsideTheAdmittedDomain() =>
        Assert.Throws<NotSupportedException>(() => Choose([Weapon()], target: Position(float.NaN)));

    [Fact]
    public void NullProjectileRangeFallbackIsNotAssumedToUseModeLimits() =>
        Assert.Throws<NotSupportedException>(() => Choose([Weapon() with { HasProjectileDefinition = false }]));
}
