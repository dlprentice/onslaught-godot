// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::EnableWeapon</c> at <c>0x00534fb0</c> on specimen
/// <c>74154bfa…</c>. Official file <c>0x00134fb0</c> is
/// <c>56 8b f1 … ff 97 98 01 00 00 5f 5e c2 0c 00</c>.
/// Player override <c>0x0040dc30</c> forwards to walker
/// <c>0x00414970</c>, which at <c>0x004149c7</c> is
/// <c>c7 87 9c 00 00 00 01 00 00 00</c> =
/// <c>mov dword ptr [edi+0x9c], 1</c>. Isolated
/// <see cref="Level100WeaponAvailabilityChanged.Enabled"/> names
/// the rebuild bool, not this store. Disable's store-0 /
/// ChangeWeapon stay unclaimed. Mutation: increment so a
/// second Enable becomes 2. No new secondaries.
/// </summary>
public sealed class RetailEnableWeaponTests
{
    /// <summary>
    /// <c>mov [edi+0x9c], 1</c> writes literal 1. Isolated
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
