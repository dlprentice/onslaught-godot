// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::EnableWeapon</c> at <c>0x00534fb0</c> on specimen
/// <c>74154bfa…</c>. Official file <c>0x00134fb0</c> holds the 41-byte body.
/// Player override <c>0x0040dc30</c> forwards to walker
/// <c>0x00414970</c>, which at <c>0x004149c7</c> is
/// a store of constant 1 to field +0x9c of edi. Isolated
/// <see cref="Level100WeaponAvailabilityChanged.Enabled"/> names
/// the rebuild bool, not this store. Disable's store-0 /
/// ChangeWeapon stay unclaimed. Mutation: increment so a
/// second Enable becomes 2. No new secondaries.
/// </summary>
public sealed class RetailEnableWeaponTests
{
    /// <summary>
    /// A store of constant 1 to field +0x9c of edi writes literal 1. Isolated
    /// <c>Enabled</c> = true still passes if this store is
    /// skipped. Mutation: <c>return current + 1</c>.
    /// </summary>
    [Fact]
    public void Enable_StoresLiteralOneAtWeaponPlus9CNotIncrement()
    {
        Assert.Equal(0x9c, RetailEnableWeapon.FlagOffset);
        Assert.Equal(1, RetailEnableWeapon.FlagEnabled);
        Assert.Equal(0, RetailEnableWeapon.FlagDisabled);
        Assert.Equal(1, RetailEnableWeapon.Enable(0));
        Assert.Equal(1, RetailEnableWeapon.Enable(1));
        Assert.NotEqual(2, RetailEnableWeapon.Enable(1));
        Assert.NotEqual(0, RetailEnableWeapon.Enable(0));
    }
}
