// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::HighlightHudPart</c> / <c>UnHighlightHudPart</c> —
/// the <c>mov dword [eax*4+0x008aa51c], imm</c> stores.
/// Isolated <see cref="Level100HudEmphasisChanged.Emphasized"/>
/// stays the rebuild bool. Array extent, state-1/2 HUD
/// meaning, and readers stay unclaimed.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: <c>IScript__HighlightHudPart</c> at
/// <c>0x00535e60</c> through <c>ret 0xc</c> at <c>0x00535e76</c>
/// (25 bytes) on the official <c>74154bfa…</c> specimen. File
/// offset = VA − 0x400000. Re-derived this session from
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>
/// (2,506,752 bytes). Body is
/// <c>8b 44 24 04 8b 08 8b 11 ff 52 30 c7 04 85 1c a5 8a 00 02 00 00 00 c2 0c 00</c>,
/// SHA-256
/// <c>d2a93e3bebd2d503307b5f1e3433926730059a7c413993ea8efbbdc0e479fc39</c>
/// (matches the 2026-08-13 boundary TSV). Twin
/// <c>IScript__UnHighlightHudPart</c> at <c>0x00535e80</c>
/// through <c>0x00535e96</c> is the same 25-byte shape with
/// immediate 1, SHA-256
/// <c>d273f5d1af93a54b9f7a9b3bbd2a56c0e9f84696f85048e2b3757e69eb9e9bd3</c>.
/// Already cited in
/// <c>reverse-engineering/binary-analysis/mission-script-registry-new-function-static-contracts-2026-08-13.md</c>.
/// </para>
/// <para>
/// <b>Highlight writes literal 2; UnHighlight writes literal 1.</b>
/// <c>0x00535e6b</c> is
/// <c>c7 04 85 1c a5 8a 00 02 00 00 00</c> =
/// <c>mov dword ptr [eax*4+0x008aa51c], 2</c>.
/// <c>0x00535e8b</c> is the same form with immediate 1, not 0.
/// Isolated <c>Emphasized</c> true/false names the rebuild
/// bitmask; skip these stores still leaves that bool.
/// Mutation: UnHighlight writes 0, or Highlight writes 1.
/// </para>
/// <para>
/// Array extent, malformed-index behavior, and HUD-reader
/// meaning of states 1/2 stay unclaimed. ChargeWeapon stays
/// unclaimed. Live <c>GAME.mSlots</c> stay unclaimed. No new
/// secondaries.
/// </para>
/// </remarks>
public static class RetailHighlightHudPart
{
    /// <summary>
    /// Start of the 11-byte Highlight <c>C7</c> store.
    /// </summary>
    public const uint HighlightStoreSite = 0x00535e6bu;

    /// <summary>
    /// Start of the 11-byte UnHighlight <c>C7</c> store.
    /// </summary>
    public const uint UnhighlightStoreSite = 0x00535e8bu;

    /// <summary>
    /// Indexed dword base at <c>0x00535e6b</c> /
    /// <c>0x00535e8b</c>.
    /// </summary>
    public const uint ArrayBaseAddress = 0x008aa51cu;

    /// <summary>
    /// The Highlight immediate at <c>0x00535e6b</c>.
    /// </summary>
    public const int Highlighted = 2;

    /// <summary>
    /// The UnHighlight immediate at <c>0x00535e8b</c>.
    /// Not zero.
    /// </summary>
    public const int Unhighlighted = 1;

    /// <summary>
    /// Released <c>onsldef.msl</c> / <c>msl-scripting.md</c>
    /// <c>HUD_COMPASS</c>. Not an array-extent claim.
    /// </summary>
    public const int CompassIndex = 2;

    /// <summary>
    /// Released <c>HUD_RADAR</c>. Not an array-extent claim.
    /// </summary>
    public const int RadarIndex = 4;

    /// <summary>
    /// The literal-2 store. Mutation: write 1.
    /// </summary>
    public static int Highlight(int currentWord)
    {
        _ = currentWord;
        return Highlighted;
    }

    /// <summary>
    /// The literal-1 store. Mutation: write 0.
    /// </summary>
    public static int Unhighlight(int currentWord)
    {
        _ = currentWord;
        return Unhighlighted;
    }
}
