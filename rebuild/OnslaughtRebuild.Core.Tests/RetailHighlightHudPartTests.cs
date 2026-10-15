// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::HighlightHudPart</c> at <c>0x00535e60</c> on specimen
/// <c>74154bfa…</c>. Official file <c>0x00135e60</c> is
/// <c>8b 44 24 04 8b 08 8b 11 ff 52 30 c7 04 85 1c a5 8a 00 02 00 00 00 c2 0c 00</c>.
/// Twin UnHighlight at <c>0x00535e80</c> stores immediate 1, not 0.
/// Isolated <see cref="Level100HudEmphasisChanged.Emphasized"/> names
/// the rebuild bool, not these stores. Array extent and state-1/2
/// HUD meaning stay unclaimed. Mutation: UnHighlight writes 0, or
/// Highlight writes 1. No new secondaries.
/// </summary>
public sealed class RetailHighlightHudPartTests
{
    /// <summary>
    /// Highlight writes literal 2; UnHighlight writes literal 1.
    /// Isolated <c>Emphasized</c> true/false still passes if these
    /// stores are skipped. Mutation: <c>return 0</c> on Unhighlight,
    /// or <c>return 1</c> on Highlight.
    /// </summary>
    [Fact]
    public void HighlightAndUnhighlight_StoreTwoThenOneNotBoolMask()
    {
        Assert.Equal(0x00535e6bu, RetailHighlightHudPart.HighlightStoreSite);
        Assert.Equal(0x00535e8bu, RetailHighlightHudPart.UnhighlightStoreSite);
        Assert.Equal(0x008aa51cu, RetailHighlightHudPart.ArrayBaseAddress);
        Assert.Equal(2, RetailHighlightHudPart.Highlighted);
        Assert.Equal(1, RetailHighlightHudPart.Unhighlighted);
        Assert.Equal(2, RetailHighlightHudPart.CompassIndex);
        Assert.Equal(4, RetailHighlightHudPart.RadarIndex);
        Assert.Equal(2, RetailHighlightHudPart.Highlight(0));
        Assert.Equal(2, RetailHighlightHudPart.Highlight(2));
        Assert.NotEqual(1, RetailHighlightHudPart.Highlight(0));
        Assert.NotEqual(3, RetailHighlightHudPart.Highlight(2));
        Assert.Equal(1, RetailHighlightHudPart.Unhighlight(2));
        Assert.Equal(1, RetailHighlightHudPart.Unhighlight(0));
        Assert.NotEqual(0, RetailHighlightHudPart.Unhighlight(2));
        Assert.NotEqual(2, RetailHighlightHudPart.Unhighlight(2));
    }
}
