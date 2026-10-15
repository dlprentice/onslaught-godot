// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The two PARITY gaps this lane closed, asserted at the wiring level the
/// way RetailClickToStartGlyphsTests and RetailFrontendPageFillEvidenceTests
/// read the flow source (the Godot type cannot be instantiated headless):
///
/// <para>1. the SELECT LEVEL name band draws the SELECTED node's row
/// (<c>_session.SelectedLevelName</c>), not an unconditional
/// <c>_level100Text</c>; and</para>
///
/// <para>2. MISSION BRIEFING draws the SELECTED world's own authored body
/// (<c>_session.SelectedBriefingBody</c>) through the measured 286px wrap
/// ceiling, with the transcribed literal demoted to the world-100 receipt
/// and the localization load cross-checking it.</para>
/// </summary>
public sealed class RetailFrontendSelectedWorldTextWiringTests
{
    [Fact]
    public void NameBandAndBriefingDrawTheSelectedWorldsRows()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        // Gap 1: the selector band follows the selection.
        Assert.Contains("_session.SelectedLevelName", flow);
        // Gap 2: the briefing page composes the selected world's own copy.
        Assert.Contains("_session.SelectedBriefingBody", flow);
        // ...wrapped at the measured ink ceiling, not drawn raw.
        Assert.Contains("WrapBriefingParagraphs", flow);
        Assert.Contains("BriefingBodyInkCeiling = 286f", flow);
    }

    [Fact]
    public void SessionExposesTheSelectionThroughTheReleasedStrings()
    {
        var frontend = new RetailFrontendSession();

        // Cold career selects the root; both pages must read its rows.
        Assert.Equal(100, frontend.SelectedWorldNumber);
        Assert.Equal("1.00 - Training Level", frontend.SelectedLevelName);
        Assert.Equal(
            OnslaughtRebuild.Core.RetailFrontendWorldStrings.Briefing(100),
            frontend.SelectedBriefingBody);
    }

    /// <summary>
    /// The transcribed literal stays byte-identical to the table pair so
    /// the world-100 fallback can never diverge from what retail authors.
    /// This is the mutation kill for "someone edits BriefingBody".
    /// </summary>
    [Fact]
    public void TranscribedReceipt_MatchesTheReleasedTablePair()
    {
        IReadOnlyList<string> released =
            RetailFrontendWorldStrings.Briefing(100);

        Assert.Equal(2, released.Count);
        Assert.Equal(
            "Tatiana will take you through the basics of piloting Battle " +
            "Engine Aquila. This will cover everything from basic movement " +
            "in both Walker and Jet modes as well as Weapons use.",
            released[0]);
        Assert.Equal(
            "Listen to her advice and try to keep Colonel Kramer happy.",
            released[1]);
    }
}
