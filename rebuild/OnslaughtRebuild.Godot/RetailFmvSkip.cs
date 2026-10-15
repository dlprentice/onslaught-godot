// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// What aborts a full-screen FMV — recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this cycle
/// (2,506,752 bytes).
///
/// <para><b>Receiver.</b> <c>CFMV::ReceiveButtonAction</c>
/// <c>0x004656E0</c>–<c>0x00465708</c> (<c>RET 0x0C</c>):</para>
/// <list type="number">
/// <item><c>cmp eax, 7</c> / <c>je write</c> —
/// <c>BUTTON_SKIP_CUTSCENE</c> (<c>Controller.h:98</c>).</item>
/// <item><c>cmp eax, 0x42</c> / <c>jne ret</c>, then
/// <c>test [ecx+0x0C]</c> / <c>jz ret</c> —
/// <c>BUTTON_BREAK_ATTRACT_MODE</c> 66 only when the attract/loading
/// gate is nonzero.</item>
/// <item>Every other button returns without a write.</item>
/// </list>
/// The shared write is <c>mov [ecx+8], 1</c> and
/// <c>mov [0x006630CC], 0</c>.
///
/// <para><b>Default keys.</b>
/// <c>OptionsEntries__InitDefaultSingleBindingsTable</c> <c>0x00514210</c>
/// issues four <c>KEY_ONCE=8</c> rows for button 7:
/// DIK <c>0x39</c> Space, <c>0x1C</c> Enter, <c>0x01</c> Escape,
/// <c>0x9C</c> Numpad Enter. A focused Space press has been observed to
/// abort the startup movie
/// (<c>reverse-engineering/source-code/frontend/fep-systems.md</c>).</para>
///
/// <para><b>Mouse.</b> The DirectX backend at <c>0x0053F190</c> ORs three
/// dwords at <c>0x0089BDF8</c> / <c>0x0089BE10</c> / <c>0x0089BE28</c>
/// (<c>0x0053F2EB</c>) and takes the same quit write. Those slots are the
/// L/M/R transient latches named by
/// <c>CGame__RunIntroFMV.md</c>. Extra mouse buttons are not those
/// slots.</para>
///
/// <para>No Godot types. No attract splash. No TWIMTBP. The
/// <c>CFMV+0x0C</c> gate is exposed rather than assumed: this lane does
/// not claim when attract playback sets it.</para>
/// </summary>
public static class RetailFmvSkip
{
    /// <summary><c>BUTTON_SKIP_CUTSCENE</c>. <c>cmp eax,7</c> at <c>0x004656E4</c>.</summary>
    public const int SkipCutsceneButton = 7;

    /// <summary><c>BUTTON_BREAK_ATTRACT_MODE</c>. <c>cmp eax,0x42</c> at <c>0x004656E9</c>.</summary>
    public const int BreakAttractButton = 66;

    /// <summary>
    /// DIK scan codes on the four default <c>KEY_ONCE</c> rows for button 7.
    /// Space, Enter, Escape, Numpad Enter.
    /// </summary>
    public static readonly int[] DefaultSkipScanCodes = [0x39, 0x1C, 0x01, 0x9C];

    /// <summary>
    /// Extra mouse buttons are not among the three backend latch dwords.
    /// </summary>
    public const bool AcceptsExtraMouseButton = false;

    /// <summary>
    /// <c>0x004656E4</c> then <c>0x004656E9</c> / <c>0x004656EE</c>.
    /// <paramref name="attractGate"/> is <c>CFMV+0x0C</c>.
    /// </summary>
    public static bool AcceptsButton(int button, bool attractGate) =>
        button == SkipCutsceneButton || (button == BreakAttractButton && attractGate);

    /// <summary>Whether a default-table DIK would raise button 7.</summary>
    public static bool AcceptsDefaultSkipScanCode(int dik) =>
        dik is 0x39 or 0x1C or 0x01 or 0x9C;

    /// <summary>
    /// The three backend latch dwords at <c>0x0053F2EB</c>. Any one nonzero
    /// takes the quit write. Slot-to-button names come from
    /// <c>CGame__RunIntroFMV.md</c>, not from this re-read.
    /// </summary>
    public static bool AcceptsMouseLatch(bool left, bool middle, bool right) =>
        left || middle || right;
}
