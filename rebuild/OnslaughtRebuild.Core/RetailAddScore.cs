// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::AddScore</c> — the <c>add [0x008a9b8c], eax</c>
/// incrementer. First-play elapsed and FillOut's copy of
/// <c>this+0xf4</c> stay on the already-pinned
/// <see cref="RetailFillOutEndLevelData.ScoreWord"/>.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: <c>IScript__AddScore</c> at <c>0x005343c0</c>
/// through <c>ret 0xc</c> at <c>0x005343d2</c> (20 bytes) on the
/// official <c>74154bfa…</c> specimen. File offset = VA − 0x400000.
/// Re-derived this session from
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>
/// (2,506,752 bytes). Body is
/// <c>8b 44 24 04 8b 08 8b 11 ff 52 30 01 05 8c 9b 8a 00 c2 0c 00</c>,
/// SHA-256
/// <c>b4b6efb14498a8c71f3d1caa99f5e463f98393736c2f191bc365834245718b88</c>.
/// Function note
/// <c>reverse-engineering/binary-analysis/functions/game.cpp/CGame__FillOutEndLevelData.md</c>
/// already names <c>0x005343cb</c> as
/// <c>add [0x008a9b8c], eax</c> = <c>this+0xf4</c>.
/// </para>
/// <para>
/// <b>The store is an integer add, not a replace.</b>
/// <c>0x005343cb</c> is <c>01 05 8c 9b 8a 00</c>.
/// <c>CGame</c> singleton <c>0x008a9a98</c> + <c>0xf4</c> is
/// <c>0x008a9b8c</c>. Source <c>game.h:210</c> is
/// <c>IncScore(SINT inScore) { mScore+=inScore; }</c>.
/// <c>CGame::LoadLevel</c> writes 0
/// (<c>game.cpp:689</c> / FillOut note). First-play Level 100
/// <c>TUTORIAL_DODGE_GOOD</c> posts one <c>AddScore(50)</c>;
/// replace and add both leave 50 there, so isolated
/// <c>ScoreDelta</c> = 50 is not unique versus this incrementer.
/// Mutation: <c>return delta</c>.
/// </para>
/// <para>
/// Isolated FillOut <see cref="RetailFillOutEndLevelData.ScoreWord"/>
/// copies a parameterized dword and does not go through AddScore.
/// Isolated score-time arm later fistp-rewrites <c>CGame+0xf4</c>
/// after that copy. Do not invent first-play elapsed or the FillOut
/// snapshot score. Live <c>GAME.mSlots</c> stay unclaimed. No new
/// secondaries.
/// </para>
/// </remarks>
public static class RetailAddScore
{
    /// <summary><c>CGame</c> singleton — 146 image <c>mov ecx, 0x008a9a98</c> (149 operand references in all).</summary>
    public const uint GameSingletonAddress = 0x008a9a98u;

    /// <summary><c>CGame.mScore</c> — <c>this+0xf4</c>.</summary>
    public const int GameScoreOffset = 0xf4;

    /// <summary>
    /// The <c>add [0x008a9b8c], eax</c> destination at
    /// <c>0x005343cb</c>.
    /// </summary>
    public const uint GameScoreAddress = 0x008a9b8cu;

    /// <summary>
    /// <c>CGame::LoadLevel</c> store. First-play starts here before
    /// any <c>AddScore</c>.
    /// </summary>
    public const int LoadLevelZero = 0;

    /// <summary>
    /// The incrementer. Mutation: replace so a second +50 stays 50.
    /// </summary>
    public static int Add(int gameScore, int delta) =>
        unchecked(gameScore + delta);
}
