// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The remaining CFEPIntro::Process arm after the timer —
/// <c>0x0051B79E</c>–<c>0x0051B801</c> — recovered from the pristine
/// specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> After the <c>2*dt</c> add, Process loads
/// <c>0x00677614</c> and <c>JE 0x0051B801</c> when the dword is 0. The
/// skip target is the already-shipped full-window mouse dispatch. Clock,
/// 30 s idle <c>-3</c>, and the mouse rect are other helpers. This is
/// not those, not a fade, and not a Render pass.</para>
///
/// <para><b>Gate.</b> The three handshake dwords
/// (<c>0x00677614</c>, <c>0x00677624</c>, <c>0x0067762C</c>) and the
/// stash (<c>0x00663058</c>) sit in uninitialised <c>.data</c>. The
/// image-initial flag is 0, so cold Process never enters. Other
/// frontend bodies also read the cluster (kinds 3/6/8/9/11); this
/// page accepts only 0 and 10.</para>
///
/// <para><b>Kind 0.</b> Load <c>0x0067762C</c>, store <c>-1</c>,
/// <c>dec / jne 0x0051B7DB</c>. Latch 1 writes <c>-3</c> to
/// <c>0x008A956C</c> and stashes <c>this+0x14</c> at
/// <c>0x00663058</c>. Fall-through then reloads the latch (now
/// <c>-1</c>) so the page-field reset does not fire.</para>
///
/// <para><b>Kind 10.</b> Jump straight to <c>0x0051B7DB</c>. Latch 1
/// writes <c>this+0x0C=0</c>, <c>this+0x10=1</c>, and clears the
/// flag. No <c>-3</c>. No stash.</para>
///
/// <para>No Godot types. Image-initial 0 means
/// <c>DrawClickToStart</c> must not invent this handshake on the cold
/// path. HandleKey, DrawLoading, and DrawQuitConfirm stay untouched.</para>
/// </summary>
public static class RetailClickToStartProcess
{
    /// <summary>Loaded at <c>0x0051B79E</c>. Uninitialised <c>.data</c>.</summary>
    public const uint FlagGlobal = 0x00677614u;

    /// <summary>Loaded at <c>0x0051B7A7</c>. Compared to 0 then 10.</summary>
    public const uint KindGlobal = 0x00677624u;

    /// <summary>Loaded at <c>0x0051B7BA</c> and again at <c>0x0051B7DB</c>.</summary>
    public const uint LatchGlobal = 0x0067762Cu;

    /// <summary>Store of <c>this+0x14</c> at <c>0x0051B7D5</c>.</summary>
    public const uint StashGlobal = 0x00663058u;

    /// <summary>Same <c>-3</c> dword as the 30 s idle write.</summary>
    public const uint ResultGlobal = 0x008A956Cu;

    /// <summary>
    /// Image-initial dword in uninitialised <c>.data</c>. Process
    /// <c>JE</c>s to the mouse dispatch.
    /// </summary>
    public const uint ImageInitialFlag = 0u;

    /// <summary><c>test eax,eax / je 0x0051B7BA</c> after the kind load.</summary>
    public const int KindZero = 0;

    /// <summary><c>cmp eax, 0x0A / je 0x0051B7DB</c> at <c>0x0051B7B3</c>.</summary>
    public const int KindTen = 10;

    /// <summary><c>dec eax / jne</c> falls through only when the latch was 1.</summary>
    public const int LatchReady = 1;

    /// <summary><c>or ecx, -1</c> at <c>0x0051B7AC</c>; stored to the latch.</summary>
    public const int LatchConsumed = -1;

    /// <summary>Immediate at <c>0x0051B7CB</c>. Same as <see cref="RetailClickToStartPrompt.IdleResult"/>.</summary>
    public const int AttractResult = -3;

    /// <summary><c>this+0x14</c> copied at <c>0x0051B7C8</c>.</summary>
    public const int StashFieldOffset = 0x14;

    /// <summary><c>mov [esi+0x0C], 0</c> at <c>0x0051B7E9</c>.</summary>
    public const int SubstateOffset = 0x0C;

    /// <summary>Immediate stored at <c>0x0051B7E9</c>.</summary>
    public const int SubstateReset = 0;

    /// <summary><c>mov [esi+0x10], 1</c> at <c>0x0051B7F0</c>.</summary>
    public const int Field10Offset = 0x10;

    /// <summary>Immediate stored at <c>0x0051B7F0</c>.</summary>
    public const int Field10Set = 1;

    /// <summary>One Process tick of the arm. Writes, not pixels.</summary>
    public readonly record struct Tick(
        bool WriteAttractResult,
        bool StashPageField,
        bool ResetPageFields,
        bool ClearFlag,
        bool WriteConsumedLatch);

    /// <summary>
    /// Whether Process would enter the arm
    /// (<c>0x0051B79E</c>–<c>0x0051B7A5</c>).
    /// </summary>
    public static bool ShouldEnter(uint flag) => flag != 0u;

    /// <summary>
    /// Observable writes of <c>0x0051B79E</c>–<c>0x0051B801</c>.
    /// Kind 0 stores <c>-1</c> to the latch before the second
    /// compare, so it cannot also reset the page fields.
    /// </summary>
    public static Tick Evaluate(uint flag, int kind, int latch)
    {
        if (flag == 0u)
        {
            return default;
        }

        if (kind == KindZero)
        {
            return new Tick(
                WriteAttractResult: latch == LatchReady,
                StashPageField: latch == LatchReady,
                ResetPageFields: false,
                ClearFlag: false,
                WriteConsumedLatch: true);
        }

        if (kind == KindTen)
        {
            bool ready = latch == LatchReady;
            return new Tick(
                WriteAttractResult: false,
                StashPageField: false,
                ResetPageFields: ready,
                ClearFlag: ready,
                WriteConsumedLatch: true);
        }

        return default;
    }
}
