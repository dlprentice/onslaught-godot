// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::EnableFlightMode</c> at <c>0x00535070</c> on specimen
/// <c>74154bfa…</c>. Official file <c>0x00135070</c> holds the 17-byte body.
/// <c>0x00535079</c> is a direct call to 0x0040dcb0 (W001 inbound). Callee
/// <c>0x0040dcb0</c> is a store of constant 1 to field +0x58c of ecx / <c>ret</c>. Body SHA-256
/// <c>aac88ccb37a4df2655331f224a89213ae051d37715c820078229e3ebef65b4a7</c>.
/// Isolated <see cref="Level100MissionSnapshot.FlightModeEnabled"/> =
/// true names the rebuild bool, not this store. Disable's
/// <c>+0x58c=0</c> / morph-if-state-3 stay unclaimed. Mutation:
/// increment so a second Enable becomes 2. No new secondaries.
/// </summary>
public sealed class RetailEnableFlightModeTests
{
    /// <summary>
    /// A store of constant 1 to field +0x58c of ecx writes literal 1. Isolated
    /// <c>FlightModeEnabled</c> = true still passes if this store
    /// is skipped. Mutation: <c>return current + 1</c>.
    /// </summary>
    [Fact]
    public void Enable_StoresLiteralOneAtCBattleEnginePlus58CNotIncrement()
    {
        Assert.Equal(0x58c, RetailEnableFlightMode.FlagOffset);
        Assert.Equal(1, RetailEnableFlightMode.FlagEnabled);
        Assert.Equal(0, RetailEnableFlightMode.FlagDisabled);
        Assert.Equal(1, RetailEnableFlightMode.Enable(0));
        Assert.Equal(1, RetailEnableFlightMode.Enable(1));
        Assert.NotEqual(2, RetailEnableFlightMode.Enable(1));
        Assert.NotEqual(0, RetailEnableFlightMode.Enable(0));
    }
}
