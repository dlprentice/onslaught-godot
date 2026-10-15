// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The selector name band and briefing body follow the SELECTED world, not a
/// pinned constant. Cold careers select the root (world 100), so the cold
/// page must still read "1.00 - Training Level" and draw the Tatiana pair —
/// the same pixels the 2026-08-22 worldselect probe recorded — while an
/// admitted world-110 selection must retitle both pages with its own
/// authored copy and never borrow another world's.
/// </summary>
public sealed class RetailFrontendSelectionTextTests
{
    private static RetailFrontendSession SessionWithWorld110Selected()
    {
        var frontend = new RetailFrontendSession();
        // Walk to level select (the same three confirms the flow tests use),
        // apply the pinned FillOut Won update the capture seam applies, then
        // select world 110 through the released admission law.
        frontend.Confirm();
        frontend.Confirm();
        frontend.Confirm();
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        frontend.Career.ApplyUpdate(RetailFillOutEndLevelData.ForLevel100Won());
        Assert.True(frontend.SelectWorld(110));
        return frontend;
    }

    [Fact]
    public void ColdSelection_NamesTheRootWorld()
    {
        var frontend = new RetailFrontendSession();

        Assert.Equal(100, frontend.SelectedWorldNumber);
        Assert.Equal("1.00 - Training Level", frontend.SelectedLevelName);
    }

    [Fact]
    public void World110Selection_RetitlesTheBandAndBriefing()
    {
        RetailFrontendSession frontend = SessionWithWorld110Selected();

        Assert.Equal("1.10 - Blackout", frontend.SelectedLevelName);
        IReadOnlyList<string> body = frontend.SelectedBriefingBody;
        Assert.Equal(2, body.Count);
        Assert.StartsWith(
            "Communications with the mainland have been lost", body[0]);
        Assert.Equal(110, frontend.ConsumeLaunchWorldNumber);
    }

    [Fact]
    public void World100Briefing_IsTheTranscribedPairByteForByte()
    {
        var frontend = new RetailFrontendSession();

        IReadOnlyList<string> body = frontend.SelectedBriefingBody;
        Assert.Equal(2, body.Count);
        Assert.Equal(
            "Tatiana will take you through the basics of piloting Battle " +
            "Engine Aquila. This will cover everything from basic movement " +
            "in both Walker and Jet modes as well as Weapons use.",
            body[0]);
        Assert.Equal(
            "Listen to her advice and try to keep Colonel Kramer happy.",
            body[1]);
    }
}
