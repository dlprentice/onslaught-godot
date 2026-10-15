// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailBattleEngineConfigurations"/> against
/// <c>references/Onslaught/BattleEngineConfigurations.cpp:80-93</c>,
/// <c>BattleEngineDataManager.h:272-298</c> and the pristine <c>74154bfa…</c>
/// bytes at <c>0x0040F2F0</c> and <c>0x0040F140</c>.
/// </summary>
public sealed class RetailBattleEngineConfigurationsTests
{
    private static readonly string?[] ThreeNames = { "aquila", "warspite", "fenrir" };

    [Fact]
    public void MaxConfigurations_IsTheShutDownLoopBound()
    {
        Assert.Equal(20, RetailBattleEngineConfigurations.MaxConfigurations);
        Assert.Equal(
            (int)(RetailBattleEngineConfigurations.ConfigurationCountAddress -
                RetailBattleEngineConfigurations.ConfigurationNameArrayAddress) / 4,
            RetailBattleEngineConfigurations.MaxConfigurations);
    }

    // The clamp is against sConfigurations, not against the array length, and its
    // failure value is zero rather than a saturation.
    [Theory]
    [InlineData(-1, 5, 0)]
    [InlineData(int.MinValue, 5, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(4, 5, 4)]
    [InlineData(5, 5, 0)]
    [InlineData(19, 5, 0)]
    [InlineData(0, 0, 0)]
    public void ClampConfigurationId_MatchesTheTwoComparesAndTheSharedZero(
        int id, int count, int expected) =>
        Assert.Equal(expected, RetailBattleEngineConfigurations.ClampConfigurationId(id, count));

    [Fact]
    public void GetConfiguration_ReturnsTheMatchingData()
    {
        var data = new string?[] { "fenrir", "warspite", "aquila" };

        Assert.Equal(2, RetailBattleEngineConfigurations.GetConfiguration(ThreeNames, 3, 0, data));
        Assert.Equal(1, RetailBattleEngineConfigurations.GetConfiguration(ThreeNames, 3, 1, data));
        Assert.Equal(0, RetailBattleEngineConfigurations.GetConfiguration(ThreeNames, 3, 2, data));
    }

    // An out-of-range id resolves to name 0 and then to whatever that name
    // matches - not to data element 0 directly.
    [Fact]
    public void GetConfiguration_ClampsBeforeItLooksTheNameUp()
    {
        var data = new string?[] { "fenrir", "warspite", "aquila" };

        Assert.Equal(2, RetailBattleEngineConfigurations.GetConfiguration(ThreeNames, 3, -1, data));
        Assert.Equal(2, RetailBattleEngineConfigurations.GetConfiguration(ThreeNames, 3, 99, data));
    }

    // A miss falls through to GetConfiguration(0), which the compiler folded to
    // "the first element". That is not the same as returning nothing.
    [Fact]
    public void GetConfiguration_FallsBackToTheFirstDataElementOnAMiss()
    {
        var names = new string?[] { "missing" };
        var data = new string?[] { "fenrir", "warspite" };

        Assert.Equal(0, RetailBattleEngineConfigurations.GetConfiguration(names, 1, 0, data));
    }

    // The fallback is the only path that can return nothing, and only for an
    // empty set.
    [Fact]
    public void GetConfiguration_ReturnsNothingOnlyForAnEmptyDataSet() =>
        Assert.Null(RetailBattleEngineConfigurations.GetConfiguration(
            ThreeNames, 3, 0, System.Array.Empty<string?>()));

    // The emptiness test comes BEFORE the name is read, so a career that loaded
    // no names and no data survives, and one that loaded data without names does
    // not. Ordering is the whole content of this pair.
    [Fact]
    public void GetConfiguration_OrdersTheEmptinessTestAheadOfTheNameRead()
    {
        var noNames = new string?[RetailBattleEngineConfigurations.MaxConfigurations];

        Assert.Null(RetailBattleEngineConfigurations.GetConfiguration(
            noNames, 0, 0, System.Array.Empty<string?>()));

        Assert.Throws<InvalidOperationException>(
            () => RetailBattleEngineConfigurations.GetConfiguration(
                noNames, 0, 0, new string?[] { "fenrir" }));
    }

    // sConfigurations of zero clamps every id to 0, which Initialise left null.
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(7)]
    public void GetConfiguration_FaultsOnANullNameWhateverIdWasAsked(int id)
    {
        var noNames = new string?[RetailBattleEngineConfigurations.MaxConfigurations];

        Assert.Throws<InvalidOperationException>(
            () => RetailBattleEngineConfigurations.GetConfiguration(
                noNames, 0, id, new string?[] { "fenrir" }));
    }

    [Fact]
    public void GetConfiguration_FaultsOnANullDataName() =>
        Assert.Throws<InvalidOperationException>(
            () => RetailBattleEngineConfigurations.GetConfiguration(
                ThreeNames, 3, 0, new string?[] { null, "aquila" }));

    // strcmp, not stricmp. A case-insensitive rebuild would answer 0 here.
    [Fact]
    public void CStringEquals_IsCaseSensitive()
    {
        Assert.False(RetailBattleEngineConfigurations.CStringEquals("Aquila", "aquila"));

        var data = new string?[] { "fenrir", "Aquila" };

        Assert.Equal(0, RetailBattleEngineConfigurations.GetConfiguration(ThreeNames, 3, 0, data));
    }

    // Comparison stops at the first NUL in either operand, which a rebuild using
    // full string equality on a buffer would get wrong.
    [Fact]
    public void CStringEquals_StopsAtTheTerminator()
    {
        Assert.True(RetailBattleEngineConfigurations.CStringEquals("aquila\0junk", "aquila"));
        Assert.True(RetailBattleEngineConfigurations.CStringEquals("aquila", "aquila\0junk"));
        Assert.True(RetailBattleEngineConfigurations.CStringEquals("\0anything", ""));
        Assert.True(RetailBattleEngineConfigurations.CStringEquals(string.Empty, string.Empty));
        Assert.False(RetailBattleEngineConfigurations.CStringEquals("aquila", "aquil"));
        Assert.False(RetailBattleEngineConfigurations.CStringEquals("aquil", "aquila"));
    }

    [Fact]
    public void GetConfiguration_MatchesThroughTheTerminator()
    {
        var names = new string?[] { "aquila\0trailing" };
        var data = new string?[] { "fenrir", "aquila" };

        Assert.Equal(1, RetailBattleEngineConfigurations.GetConfiguration(names, 1, 0, data));
    }

    // The scan is first-match in walk order.
    [Fact]
    public void GetConfiguration_TakesTheFirstMatchingElement()
    {
        var names = new string?[] { "aquila" };
        var data = new string?[] { "aquila", "aquila" };

        Assert.Equal(0, RetailBattleEngineConfigurations.GetConfiguration(names, 1, 0, data));
    }

    [Fact]
    public void GetConfiguration_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => RetailBattleEngineConfigurations.GetConfiguration(null!, 0, 0, ThreeNames));
        Assert.Throws<ArgumentNullException>(
            () => RetailBattleEngineConfigurations.GetConfiguration(ThreeNames, 3, 0, null!));
        Assert.Throws<ArgumentNullException>(
            () => RetailBattleEngineConfigurations.CStringEquals(null!, "a"));
    }
}
