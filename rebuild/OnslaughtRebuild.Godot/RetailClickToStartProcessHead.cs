// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The unread CFEPIntro::Process head —
/// <c>0x0051B6B0</c>–<c>0x0051B705</c> — recovered from the pristine
/// specimen (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> Clock, 30 s idle <c>-3</c>, the 0x00677614
/// handshake, and the full-window mouse rect are other helpers. This is
/// the unread prologue in front of them. Not a fade. Not pixels. Not
/// in the pinned GPL drop (<c>FEPIntro.cpp</c> is absent).</para>
///
/// <para><b>Early-out.</b> a load of stack local +4 into eax, then test, branching to 0x0051B83B when not equal
/// pops esi and a return that pops 4 bytes of arguments. A nonzero stack arg skips HUD, ResetFlags,
/// the 3 s dispatch, idle, clock, handshake, and mouse.</para>
///
/// <para><b>HUD.</b> Zero-arg fall-through does
/// (a load of constant 0x00704858 into ecx, then direct call to 0x0041A200). That body is
/// a zeroing of al, then return, so a zero test of al, branching to 0x0051B6D4 when equal never
/// reaches ResetFlags <c>0x0051B610</c> from this site. The object at
/// <c>0x00704858</c> is uninitialised <c>.data</c>.</para>
///
/// <para><b>Dev-mode confirm.</b> <c>0x00662DF4</c> is uninitialised
/// <c>.data</c> (image-initial 0). Nonzero plus
/// <c>GetTime()-[this+4]</c> strictly greater than <c>3.0f</c> at
/// <c>0x005D8CC0</c> (a bit test of ah against 0x41, branching when not equal) then
/// an indirect call through vtable slot 3 with action <c>0x2C</c> and
/// <c>0x3F800000</c> (1.0f). Cold Process therefore never auto-confirms.
/// Other bodies can write the dword at runtime; this page only reads it.</para>
///
/// <para>No Godot types. Image HUD is false and image-initial
/// <c>0x00662DF4</c> is 0, so <c>DrawClickToStart</c> must not invent
/// ResetFlags or a 3 s confirm. HandleKey, DrawLoading, and
/// DrawQuitConfirm stay untouched.</para>
/// </summary>
public static class RetailClickToStartProcessHead
{
    /// <summary>Function entry. Loads <c>[esp+4]</c>.</summary>
    public const uint ProcessVa = 0x0051B6B0u;

    /// <summary>Nonzero <c>[esp+4]</c> lands here: a pop into esi, then return that pops 4 bytes of arguments.</summary>
    public const uint EarlyOutVa = 0x0051B83Bu;

    /// <summary>Epilogue is a return that pops 4 bytes of arguments.</summary>
    public const int StackArgBytes = 4;

    /// <summary>Called at <c>0x0051B6C4</c>. Body is a zeroing of al, then return.</summary>
    public const uint GetShowHudVa = 0x0041A200u;

    /// <summary>ECX loaded at <c>0x0051B6BF</c>. Uninitialised <c>.data</c>.</summary>
    public const uint ShowHudObject = 0x00704858u;

    /// <summary>Called at <c>0x0051B6CF</c> only when AL is nonzero.</summary>
    public const uint ResetFlagsVa = 0x0051B610u;

    /// <summary>Image body of <see cref="GetShowHudVa"/> returns 0.</summary>
    public const bool ImageGetShowHud = false;

    /// <summary>Loaded at <c>0x0051B6D4</c>. Uninitialised <c>.data</c>.</summary>
    public const uint DevModeGlobal = 0x00662DF4u;

    /// <summary>
    /// Image-initial dword in uninitialised <c>.data</c>. Process
    /// <c>JE</c>s to <see cref="HeadEndVa"/>.
    /// </summary>
    public const uint ImageInitialDevMode = 0u;

    /// <summary>Called at <c>0x0051B6E2</c> when the dev-mode dword is live.</summary>
    public const uint GetTimeVa = 0x005159E0u;

    /// <summary>ECX loaded at <c>0x0051B6DD</c> before GetTime.</summary>
    public const uint GetTimeObject = 0x0088A0A8u;

    /// <summary><c>0x005D8CC0</c> = <c>3.0f</c>.</summary>
    public const uint ThreeSecondsVa = 0x005D8CC0u;

    /// <summary>
    /// Auto-confirm only when page elapsed is strictly greater than this
    /// (a bit test of ah against 0x41 keeps ≤).
    /// </summary>
    public const double AutoConfirmSeconds = 3.0;

    /// <summary>First instruction after the head: a load of field +0x0C of esi into eax.</summary>
    public const uint HeadEndVa = 0x0051B705u;

    /// <summary>A push of constant 0x2C at <c>0x0051B6FE</c>.</summary>
    public const int ConfirmAction = 0x2C;

    /// <summary>A push of constant 0x3F800000 at <c>0x0051B6F9</c>.</summary>
    public const uint ConfirmValueBits = 0x3F800000u;

    /// <summary>An indirect call through vtable slot 3 at <c>0x0051B702</c>.</summary>
    public const int VtableSlotOffset = 0x0C;

    /// <summary>One Process tick of the head. Writes, not pixels.</summary>
    public readonly record struct Tick(
        bool ReturnedImmediately,
        bool CalledResetFlags,
        bool DispatchConfirm);

    /// <summary>
    /// Observable writes of <c>0x0051B6B0</c>–<c>0x0051B705</c>.
    /// <paramref name="processState"/> is <c>[esp+4]</c>.
    /// <paramref name="showHud"/> is AL from <see cref="GetShowHudVa"/>.
    /// <paramref name="devMode"/> is <c>0x00662DF4</c>.
    /// <paramref name="pageSeconds"/> is <c>GetTime() - [this+4]</c>.
    /// </summary>
    public static Tick Evaluate(int processState, bool showHud, uint devMode, double pageSeconds)
    {
        if (processState != 0)
        {
            return new Tick(
                ReturnedImmediately: true,
                CalledResetFlags: false,
                DispatchConfirm: false);
        }

        return new Tick(
            ReturnedImmediately: false,
            CalledResetFlags: showHud,
            DispatchConfirm: devMode != 0u && pageSeconds > AutoConfirmSeconds);
    }
}
