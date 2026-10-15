// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for the per-world frontend strings decoded from the pinned
/// English language table (english-worlds.json, SHA-256
/// <c>ffe3d3f8…5408</c>, exact-reproduced from english.dat
/// <c>789ecff6…371a</c>). The Core literals are generated mechanically from
/// that document; these tests pin the laws that make them retail evidence.
/// </summary>
public sealed class RetailFrontendWorldStringsTests
{
    [Fact]
    public void EveryCareerWorldCarriesANameRow()
    {
        foreach (RetailWorldNode node in RetailWorldCatalog.Nodes)
        {
            string? name = RetailFrontendWorldStrings.LevelName(node.WorldNumber);
            Assert.True(name is not null, $"world {node.WorldNumber} lost its name row");
            Assert.Matches(@"^\d{1,2}\.\d{2} - .+$", name!);
        }
    }

    [Fact]
    public void World100And110Names_AreTheMeasuredReleasedRows()
    {
        Assert.Equal("1.00 - Training Level", RetailFrontendWorldStrings.LevelName(100));
        Assert.Equal("1.10 - Blackout", RetailFrontendWorldStrings.LevelName(110));
        Assert.Equal("2.00 - Interception", RetailFrontendWorldStrings.LevelName(200));
        Assert.Equal("3.00 - Liberation of Russo", RetailFrontendWorldStrings.LevelName(300));
    }

    /// <summary>
    /// The world-100 briefing pair must remain byte-identical to the
    /// transcription corroborated against the pristine capture's measured
    /// ink widths (PARITY.md briefing section) — it is the bridge that ties
    /// the pool-slot law to pixels.
    /// </summary>
    [Fact]
    public void World100Briefing_IsTheTranscribedPair()
    {
        IReadOnlyList<string> body = RetailFrontendWorldStrings.Briefing(100);

        Assert.Equal(2, body.Count);
        Assert.StartsWith("Tatiana will take you through", body[0]);
        Assert.EndsWith("Weapons use.", body[0]);
        Assert.Equal(
            "Listen to her advice and try to keep Colonel Kramer happy.",
            body[1]);
    }

    [Fact]
    public void World110Briefing_IsItsOwnAuthoredCopy()
    {
        IReadOnlyList<string> body = RetailFrontendWorldStrings.Briefing(110);

        Assert.Equal(2, body.Count);
        Assert.Equal(
            "Communications with the mainland have been lost. Numerous " +
            "enemy contacts are heading towards the facilities on RI-04.",
            body[0]);
        Assert.Equal(
            "Defend the base and prevent the invasion force from taking " +
            "over the island. Ensure that the Battle Engine is protected " +
            "at all times.",
            body[1]);
    }

    [Fact]
    public void World200Briefing_IsItsOwnAuthoredCopy()
    {
        IReadOnlyList<string> body = RetailFrontendWorldStrings.Briefing(200);

        Assert.Equal(2, body.Count);
        Assert.StartsWith("The transports taking the Battle Engine", body[0]);
        Assert.StartsWith("Protect the transport convoy", body[1]);
    }

    /// <summary>
    /// MEASURED 2026-08-22: exactly four worlds carry a third paragraph —
    /// the Tara-note epilogue variants of episode six — and no other world
    /// has one.
    /// </summary>
    [Fact]
    public void ExactlyTheFourMeasuredWorlds_CarryAThirdParagraph()
    {
        int[] expected = [611, 612, 621, 622];
        foreach (int world in RetailFrontendWorldStrings.Briefings.Keys)
        {
            bool hasThird =
                RetailFrontendWorldStrings.Briefing(world).Count == 3;
            if (expected.Contains(world) != hasThird)
            {
                Assert.Fail(
                    $"world {world} third-paragraph state diverged");
            }
        }
    }

    [Fact]
    public void EveryBriefingSlotPair_IsNonEmptyAcrossTheCareerGraph()
    {
        // The decoder refuses empty primary/secondary slots; assert the
        // generated literals still hold that law (world 742's group was
        // absorbed by its sibling's page in the shipped data).
        foreach (RetailWorldNode node in RetailWorldCatalog.Nodes)
        {
            IReadOnlyList<string> body =
                RetailFrontendWorldStrings.Briefing(node.WorldNumber);
            Assert.True(body.Count >= 2,
                $"world {node.WorldNumber} lost a slot");
            Assert.All(body, line => Assert.NotEqual(string.Empty, line));
        }
    }

    [Fact]
    public void UnknownWorlds_DrawNothingRatherThanBorrowedCopy()
    {
        // World 999 is not in the career graph: no name row, no briefing.
        Assert.Null(RetailFrontendWorldStrings.LevelName(999));
        Assert.Empty(RetailFrontendWorldStrings.Briefing(999));
    }
}
