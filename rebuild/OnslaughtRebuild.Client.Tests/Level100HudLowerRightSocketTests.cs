// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The lower-right HUD socket is a TWO-FACTOR state table, and both
/// single-factor readings of it that this project shipped notes about were half
/// right.
///
/// <para><b>The law, from the shipped bytes.</b>
/// <c>CHud__RenderBattleline</c> (<c>0x00487d10</c>) branches on
/// <c>CInfluenceMapManager__IsEmpty</c> (<c>0x0048c2d0</c>, a list count at
/// <c>+0x14</c>). The non-empty arm populates and renders
/// <c>CDXBattleLine</c>; the empty arm draws one sprite from
/// <c>[hud+0x1d4]</c> - <c>hud\ForsetiIcon.tga</c>, per
/// <c>CHud__LoadTextures</c> (<c>0x00481650</c>) and the string at
/// <c>0x0062ceb0</c> - and only when <c>*(int *)(CMessageBox + 8) == 0</c>.
/// Separately, INSIDE <c>CDXBattleLine__Render</c> (<c>0x0053abe0</c>) every
/// draw carries the same guard: all three <c>CEngine__DrawIndexedPrimitives</c>
/// calls, the <c>CDXBattleLine__RenderTriOverlayPass</c> call, and the
/// BattleEngine marker sprite loop. So an active message suppresses the overlay
/// even when the influence list is non-empty.</para>
///
/// <para>These tests assert the recovered table, not a pixel threshold. The
/// table has three outcomes over two factors and this file states every cell of
/// it, including the two that the refuted single-factor readings would have got
/// wrong.</para>
/// </summary>
public sealed class Level100HudLowerRightSocketTests
{
    [Theory]
    [InlineData(Level100HudInfluenceMapState.Empty)]
    [InlineData(Level100HudInfluenceMapState.Populated)]
    [InlineData(Level100HudInfluenceMapState.Unknown)]
    public void AnActiveMessageShowsThePortraitWhateverTheInfluenceListHolds(
        Level100HudInfluenceMapState influenceMap)
    {
        // Row 1. The cell that refutes "the battleline draws last, over the
        // portrait": with a populated list AND an active message, retail
        // invokes CDXBattleLine__Render and it paints nothing.
        Assert.Equal(
            Level100HudLowerRightSocket.PortraitAndNoise,
            Level100HudLowerRightSocketLaw.Select(
                messageBoxHoldsActiveMessage: true,
                influenceMap));
    }

    [Fact]
    public void NoMessageAndAPopulatedListShowsTheInfluenceOverlay()
    {
        // Row 2. The cell that refutes "map when no message, portrait when
        // message" as a ONE-factor rule: the second factor is required to
        // reach this row at all.
        Assert.Equal(
            Level100HudLowerRightSocket.InfluenceOverlay,
            Level100HudLowerRightSocketLaw.Select(
                messageBoxHoldsActiveMessage: false,
                Level100HudInfluenceMapState.Populated));
    }

    [Fact]
    public void NoMessageAndAnEmptyListShowsTheForsetiIcon()
    {
        // Row 3.
        Assert.Equal(
            Level100HudLowerRightSocket.ForsetiIcon,
            Level100HudLowerRightSocketLaw.Select(
                messageBoxHoldsActiveMessage: false,
                Level100HudInfluenceMapState.Empty));
    }

    /// <summary>
    /// Unknown is not Empty. Callers without level-specific evidence must not
    /// guess: the wrong answer can select a different visible socket page.
    /// </summary>
    [Fact]
    public void AnUnknownInfluenceListDrawsNeitherRatherThanGuessing()
    {
        Assert.Equal(
            Level100HudLowerRightSocket.Indeterminate,
            Level100HudLowerRightSocketLaw.Select(
                messageBoxHoldsActiveMessage: false,
                Level100HudInfluenceMapState.Unknown));
    }

    [Fact]
    public void AnUnavailableSnapshotReportsUnknownRatherThanEmpty()
    {
        Assert.Equal(
            Level100HudInfluenceMapState.Unknown,
            Level100HudBattleLineSnapshot.Unavailable.InfluenceMap);
    }
}
