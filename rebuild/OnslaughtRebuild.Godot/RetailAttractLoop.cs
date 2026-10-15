// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// What <c>CLTShell::RunFrontEndAndGameLoop</c> does with
/// <c>CFrontEnd::Run</c>'s return — recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this cycle
/// (2,506,752 bytes).
///
/// <para><b>Body.</b> <c>0x004F0330</c>–<c>0x004F0535</c>. After
/// <c>CFrontEnd::Run</c> (<c>0x004684D0</c>, callsite <c>0x004F03C0</c>) the
/// shell compares the returned dword:</para>
/// <list type="number">
/// <item><c>cmp ebp, -1</c> at <c>0x004F03ED</c> (<c>83 fd ff</c>) /
/// <c>je</c> — process exit.</item>
/// <item><c>cmp ebp, -3</c> at <c>0x004F03F2</c> (<c>83 fd fd</c>) /
/// <c>jne</c> to <c>CGame::RunLevel</c> — the taken arm is attract
/// restart. Debug string <c>"we're in attract mode"</c> at
/// <c>0x00632B98</c>.</item>
/// </list>
///
/// <para><b>Attract clips, retail PC.</b> The taken arm plays
/// <c>"ltlogo"</c> then <c>"openingfmv"</c> (strings at <c>0x00632B90</c> /
/// <c>0x00632B68</c>). The optional <c>TWIMTBP_GefFX_640x480_Audio</c> clip
/// is omitted for the same measured-hardware reason as
/// <see cref="RetailStartupSchedule"/>. Demo inserts <c>publisher</c>
/// before <c>ltlogo</c>; retail does not
/// (<c>reverse-engineering/binary-analysis/pc-demo-retail-shell-fmv-lineage-2026-08-11.md</c>).
/// No splash fade is claimed: the post-movie splash beat is a cold-start
/// observation, not this arm.</para>
///
/// <para><b>Re-entry.</b> The next loop head does <c>cmp ebp, -3</c> /
/// <c>sete al</c> at <c>0x004F038F</c> and keeps that in <c>esi</c> —
/// <c>FEE_FROM_ATTRACT = 1</c> (<c>references/Onslaught/Frontend.h:54-56</c>).
/// <c>CFrontEnd::Init</c> then <c>SetPage(FEP_INTRO, 0)</c>
/// (<c>references/Onslaught/FrontEnd.cpp:188-202</c>).</para>
///
/// <para>The producer of <c>-3</c> is already pinned:
/// <see cref="RetailClickToStartPrompt.ShouldWriteIdleResult"/>. This type
/// owns only the shell consumer. No Godot types. No fade curve.</para>
/// </summary>
public static class RetailAttractLoop
{
    /// <summary>
    /// <c>FRONTEND.mQuit</c> init (<c>FrontEnd.cpp:172</c>). <c>CFrontEnd::Run</c>
    /// stays inside the page loop while the dword equals this.
    /// </summary>
    public const int StayInFrontend = -2;

    /// <summary><c>cmp ebp, -1</c> at <c>0x004F03ED</c>.</summary>
    public const int ProcessExitResult = -1;

    /// <summary>
    /// Immediate written at <c>CFEPIntro::Process</c> <c>0x0051B72F</c> and
    /// compared at <c>0x004F03F2</c>. Same value as
    /// <see cref="RetailClickToStartPrompt.IdleResult"/>.
    /// </summary>
    public const int AttractRestartResult = -3;

    /// <summary><c>FEE_START</c> (<c>Frontend.h:54</c>).</summary>
    public const int StartEntry = 0;

    /// <summary><c>FEE_FROM_ATTRACT</c> (<c>Frontend.h:55</c>).</summary>
    public const int FromAttractEntry = 1;

    /// <summary><c>FEE_TITLE_SCREEN</c> (<c>Frontend.h:56</c>).</summary>
    public const int TitleScreenEntry = 2;

    /// <summary>What the WinMain session loop does with a <c>Run</c> return.</summary>
    public enum ShellAction
    {
        ProcessExit,
        AttractRestart,
        RunLevel,
    }

    /// <summary>
    /// Retail attract order. Not the cold-start chain: splash is not a beat
    /// here, and TWIMTBP / publisher are not claimed.
    /// </summary>
    public static readonly RetailStartupCue[] AttractCues =
    [
        RetailStartupCue.LostToysLogo,
        RetailStartupCue.OpeningMontage,
    ];

    /// <summary>
    /// <c>0x004F03ED</c> then <c>0x004F03F2</c>. Any other dword is a world
    /// id for <c>CGame::RunLevel</c> at <c>0x0046E240</c>.
    /// </summary>
    public static ShellAction AfterFrontEndRun(int runResult) => runResult switch
    {
        ProcessExitResult => ShellAction.ProcessExit,
        AttractRestartResult => ShellAction.AttractRestart,
        _ => ShellAction.RunLevel,
    };

    /// <summary>
    /// <c>Init</c> takes this entry to <c>FEP_INTRO</c>
    /// (<c>FrontEnd.cpp:188-202</c>). <c>FEE_TITLE_SCREEN</c> goes to
    /// <c>FEP_MAIN</c> instead and is not this path.
    /// </summary>
    public static bool ReentersIntro(int entry) =>
        entry is StartEntry or FromAttractEntry;

    /// <summary>
    /// Whether the click-to-start page has produced the <c>-3</c> that this
    /// loop consumes. Compare is page elapsed, same as Process.
    /// </summary>
    public static bool ShouldRestartAttract(double pageSeconds) =>
        RetailClickToStartPrompt.ShouldWriteIdleResult(pageSeconds);
}
