// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for the Level 100 ConfirmedKill incrementer. Caller
/// <c>0x0040a560</c> then <c>0x004d30d0</c>. First-play values stay
/// unclaimed — a Won that never takes this path still snapshots five
/// zeros. Do not invent secondaries.
/// </summary>
public sealed class RetailConfirmedKillTests
{
    /// <summary>
    /// Independently re-read specimen <c>74154bfa…</c>:
    /// <c>0x0040a564</c> <c>cmp [eax+0x138],1</c> / <c>jne</c> skips
    /// <c>0x004d30d0</c>. <c>tools/call_xref_scan.py</c> on
    /// <c>0x004d30d0</c> is one <c>E8</c> at <c>0x0040a578</c>.
    /// Mutation: increment even when <c>+0x138</c> is not 1. Do not
    /// invent first-play totals or secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ConfirmedKillDoesNotIncrementWhenThingAllegianceIsNotOne()
    {
        int[] after = RetailConfirmedKill.Apply(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            thingFlags: 0x400 | 0x20000 | 0x40000 | 0x4000 | 0x800,
            thingAllegiance: 0);

        Assert.Equal(new[] { 0, 0, 0, 0, 0 }, after);
        Assert.Equal(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            after);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Same body. <c>0x004d30d7</c> <c>test dh,4</c> is bit
    /// <c>0x400</c> into <c>player+8</c>. The other four flags stay
    /// off. Mutation: increment slot 0 on any flag. First-play totals
    /// stay unclaimed. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ConfirmedKillIncrementsSlotZeroOnFlag400()
    {
        int[] after = RetailConfirmedKill.Apply(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            thingFlags: 0x400,
            thingAllegiance: 1);

        Assert.Equal(new[] { 1, 0, 0, 0, 0 }, after);
        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            RetailFillOutEndLevelData.ForLevel100Won().ThingsKilled);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Same incrementer. Independently re-read <c>0x004d30df</c>
    /// <c>test [eax+0x34],0x20000</c> into <c>player+0xc</c>. Mutation:
    /// write that bit into slot 0. First-play totals stay unclaimed.
    /// Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ConfirmedKillIncrementsSlotOneOnFlag20000()
    {
        int[] after = RetailConfirmedKill.Apply(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            thingFlags: 0x20000,
            thingAllegiance: 1);

        Assert.Equal(new[] { 0, 1, 0, 0, 0 }, after);
        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            RetailFillOutEndLevelData.ForLevel100Won().ThingsKilled);
    }

    /// <summary>
    /// Same incrementer. Independently re-read specimen
    /// <c>74154bfa…</c>: <c>0x004d30eb</c>
    /// <c>test [eax+0x34],0x40000</c> / <c>inc [ecx+0x10]</c>.
    /// Mutation: write that bit into slot 0. First-play totals stay
    /// unclaimed. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ConfirmedKillIncrementsSlotTwoOnFlag40000()
    {
        int[] after = RetailConfirmedKill.Apply(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            thingFlags: RetailConfirmedKill.Slot2Flag,
            thingAllegiance: 1);

        Assert.Equal(new[] { 0, 0, 1, 0, 0 }, after);
        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            RetailFillOutEndLevelData.ForLevel100Won().ThingsKilled);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Same incrementer. Independently re-read <c>0x004d30fa</c>
    /// <c>test dh,0x40</c> = bit <c>0x4000</c> into <c>player+0x14</c>.
    /// Mutation: write that bit into slot 0. First-play totals stay
    /// unclaimed. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ConfirmedKillIncrementsSlotThreeOnFlag4000()
    {
        int[] after = RetailConfirmedKill.Apply(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            thingFlags: RetailConfirmedKill.Slot3Flag,
            thingAllegiance: 1);

        Assert.Equal(new[] { 0, 0, 0, 1, 0 }, after);
        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            RetailFillOutEndLevelData.ForLevel100Won().ThingsKilled);
    }

    /// <summary>
    /// Same incrementer. Independently re-read <c>0x004d3105</c>
    /// <c>test dh,8</c> = bit <c>0x800</c> into <c>player+0x18</c>.
    /// Mutation: write that bit into slot 0. First-play totals stay
    /// unclaimed. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ConfirmedKillIncrementsSlotFourOnFlag800()
    {
        int[] after = RetailConfirmedKill.Apply(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            thingFlags: RetailConfirmedKill.Slot4Flag,
            thingAllegiance: 1);

        Assert.Equal(new[] { 0, 0, 0, 0, 1 }, after);
        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            RetailFillOutEndLevelData.ForLevel100Won().ThingsKilled);
    }

    /// <summary>
    /// Independently re-read specimen <c>74154bfa…</c>: after the
    /// allegiance compare, <c>0x0040a56d</c> <c>mov ecx,[ecx+0x574]</c>
    /// / <c>0x0040a573</c> <c>test ecx,ecx</c> / <c>je 0x0040a57d</c>
    /// skips <c>0x004d30d0</c>. Bytes are a null pointer, not source
    /// <c>ToRead()</c> dying-flag. Mutation: increment anyway.
    /// First-play totals stay unclaimed. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ConfirmedKillDoesNotIncrementWhenPlayerReaderIsNull()
    {
        int[] after = RetailConfirmedKill.Apply(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            thingFlags: RetailConfirmedKill.Slot0Flag |
                RetailConfirmedKill.Slot1Flag |
                RetailConfirmedKill.Slot2Flag |
                RetailConfirmedKill.Slot3Flag |
                RetailConfirmedKill.Slot4Flag,
            thingAllegiance: 1,
            playerReaderNonNull: false);

        Assert.Equal(new[] { 0, 0, 0, 0, 0 }, after);
        Assert.Equal(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            after);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }
}
