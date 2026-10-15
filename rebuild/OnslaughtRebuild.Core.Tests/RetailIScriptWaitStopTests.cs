// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::Pause</c> at <c>0x00537c70</c> and
/// <c>IScript::PlayCharMessageWait</c> at <c>0x005375f0</c>
/// on specimen <c>74154bfa…</c>. Official
/// <c>0x00537d55</c> / <c>0x005376f9</c> are
/// <c>c7 05 00 c8 89 00 01 00 00 00</c> =
/// <c>mov dword ptr [0x0089c800], 1</c>. Isolated
/// <see cref="Level100MissionTiming.PauseTicks"/> /
/// <see cref="Level100MissionTiming.MessagePlaybackTicks"/>
/// name the rebuild sleep, not this store. CVM snapshot /
/// 0.05f / FollowWaypointWait stay unclaimed. Mutation:
/// increment so a second Stop becomes 2. No new
/// secondaries.
/// </summary>
public sealed class RetailIScriptWaitStopTests
{
    /// <summary>
    /// <c>mov [0x0089c800], 1</c> writes literal 1.
    /// Isolated wait duration still passes if this store
    /// is skipped. Mutation: <c>return current + 1</c>.
    /// </summary>
    [Fact]
    public void Stop_StoresLiteralOneAtCvmSingletonPlus220NotIncrement()
    {
        Assert.Equal(0x0089c800, RetailIScriptWaitStop.FlagAddress);
        Assert.Equal(0x220, RetailIScriptWaitStop.SingletonOffset);
        Assert.Equal(1, RetailIScriptWaitStop.FlagStopped);
        Assert.Equal(0, RetailIScriptWaitStop.FlagIdle);
        Assert.Equal(1, RetailIScriptWaitStop.Stop(0));
        Assert.Equal(1, RetailIScriptWaitStop.Stop(1));
        Assert.NotEqual(2, RetailIScriptWaitStop.Stop(1));
        Assert.NotEqual(0, RetailIScriptWaitStop.Stop(0));
        Assert.Equal(1, RetailIScriptWaitStop.Stop(7));
    }
}
