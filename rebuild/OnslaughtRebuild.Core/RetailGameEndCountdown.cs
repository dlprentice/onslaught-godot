// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The two immediate countdown stores in <c>CGame::DeclareLevelLost</c> and
/// <c>CGame::DeclareLevelWon</c>, and the two world-ids that take the
/// zero-length Won arm instead.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/game.cpp:75-76</c>
/// defines <c>GAME_COUNT_WHEN_LOST_OR_DRAW 5.0f</c> and
/// <c>GAME_COUNT_WHEN_WON 5.0f</c>. Retail identities in the pristine
/// <c>74154bfa…</c> image, file offset = VA − 0x400000:
/// </para>
/// <list type="bullet">
/// <item><c>0x0046F430</c> <c>CGame::DeclareLevelLost</c> —
/// <c>0x0046F4A8</c> is <c>c7 43 48 00 00 00 40</c>,
/// <c>mov dword ptr [ebx+0x48], 0x40000000</c> = <c>2.0f</c>.</item>
/// <item><c>0x0046F2F0</c> <c>CGame::DeclareLevelWon</c> —
/// <c>0x0046F338</c> is <c>c7 43 48 00 00 a0 40</c>,
/// <c>mov dword ptr [ebx+0x48], 0x40A00000</c> = <c>5.0f</c>, after
/// <c>cmp eax, 0x2E5 / je</c> and <c>cmp eax, 0x2E6 / je</c> miss.</item>
/// </list>
/// <para>
/// <b>Source and retail diverge on Lost, and retail wins.</b> The Xbox
/// source uses 5.0 s for both outcomes. The shipped Lost body writes the
/// 2.0 f bit pattern as an immediate; there is no load of
/// <c>0x005D85D8</c> (the 5.0 f dword) anywhere in that function. A
/// rebuild written from the header would hold the Tutorial-Broken overlay
/// for 100 Core ticks instead of 40.
/// </para>
/// <para>
/// <b>Won agrees with the source for Level 100.</b> Worlds
/// <c>0x2E5</c> (741) and <c>0x2E6</c> (742) store
/// <c>0x00000000</c> at the same <c>+0x48</c> slot
/// (<c>0x0046F350</c>). Level 100 is world 100, so it takes the 5.0 f
/// arm. Those two special cases are recorded so a reader does not treat
/// the 5.0 f store as unconditional.
/// </para>
/// <para>
/// <b>Not established here.</b> The 15.0 f <c>PAUSE_GAME</c> schedule at
/// <c>0x0046F508</c> (<c>00 00 70 41</c>) after a player-death loss —
/// <see cref="Level100MissionTiming.DeathPauseDelayTicks"/> already
/// carries that duration. The <c>-1.0f</c> fade event at
/// <c>0x0046F52C</c>. What <c>this+0x48</c> is named in the header
/// beyond being the end-level timer the frontend overlay reads.
/// </para>
/// </remarks>
public static class RetailGameEndCountdown
{
    /// <summary>
    /// The Lost store — <c>0x0046F4A8</c> <c>c7 43 48 00 00 00 40</c>.
    /// </summary>
    public const uint LostCountdownBits = 0x40000000u;

    /// <summary>The same dword as a float — exactly 2.0.</summary>
    public const float LostCountdownSeconds = 2.0f;

    /// <summary>
    /// The Won store for every world that is not 741 or 742 —
    /// <c>0x0046F338</c> <c>c7 43 48 00 00 a0 40</c>.
    /// </summary>
    public const uint WonCountdownBits = 0x40A00000u;

    /// <summary>The same dword as a float — exactly 5.0.</summary>
    public const float WonCountdownSeconds = 5.0f;

    /// <summary>World 741 — <c>cmp eax, 0x2E5</c> at <c>0x0046F31C</c>.</summary>
    public const int ZeroWonCountdownWorldA = 0x2E5;

    /// <summary>World 742 — <c>cmp eax, 0x2E6</c> at <c>0x0046F32C</c>.</summary>
    public const int ZeroWonCountdownWorldB = 0x2E6;

    /// <summary>Lost overlay length at the released 20 Hz step.</summary>
    public const int LostTicks =
        (int)(LostCountdownSeconds * SimulationConstants.TicksPerSecond);

    /// <summary>Won overlay length at the released 20 Hz step.</summary>
    public const int WonTicks =
        (int)(WonCountdownSeconds * SimulationConstants.TicksPerSecond);

    /// <summary>
    /// Whether <c>DeclareLevelWon</c> stores +0.0 f instead of 5.0 f.
    /// Level 100 is world 100 and returns false.
    /// </summary>
    public static bool UsesZeroWonCountdown(int worldId) =>
        worldId == ZeroWonCountdownWorldA || worldId == ZeroWonCountdownWorldB;
}
