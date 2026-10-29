// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// What confirms CFEPIntro click-to-start — recovered from the pristine
/// specimen (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this cycle
/// (2,506,752 bytes).
///
/// <para><b>Action.</b> <c>CFEPIntro</c> handler <c>0x0051B660</c>–
/// <c>0x0051B6AA</c> (a return that pops 8 bytes of arguments):</para>
/// <list type="number">
/// <item>A compare of stack local +4 with 0x2C, then conditional branch to ret (taken when not equal).</item>
/// <item>A load of field +0x0C of ecx into eax / <c>test</c> / a conditional branch to ret (taken when not zero) —
/// page substate must be zero.</item>
/// <item>Cold-start globals then
/// a push of constant 0x32, then push of constant 0, then load of constant 0x0089D758 into ecx, then direct call to 0x00466AE0
/// (<c>CFrontEnd::SetPage(FEP_MAIN, 50)</c>). Two other SetPage pairs
/// sit behind <c>[0x008A9AB4]</c> and <c>[0x0083D448]</c> and are not
/// this path.</item>
/// </list>
///
/// <para><b>Default keys.</b>
/// <c>OptionsEntries__InitDefaultSingleBindingsTable</c> <c>0x00514210</c>
/// issues two <c>KEY_ONCE=8</c> rows for action <c>0x2C</c>: DIK
/// <c>0x1C</c> Enter and <c>0x39</c> Space. Escape and Numpad Enter are
/// not those rows. This helper does not rewrite <c>HandleKey</c>.</para>
///
/// <para><b>Mouse.</b> <c>CFEPIntro::Process</c> at <c>0x0051B801</c>
/// pushes <c>0x2C</c>, calls <c>PLATFORM__GetWindowWidth 0x00515940</c>
/// twice (not <c>GetWindowHeight 0x00515B00</c>), then
/// <c>0x00469390</c> with <c>(0, 0, width, width, 0x2C)</c>. The
/// consume path at <c>0x00523BC0</c> hit-tests the cursor against that
/// window rect and dispatches action <c>0x2C</c> through
/// <c>0x004669A0</c>. There is no glyph, splash, or title hit-rect.</para>
///
/// <para>No Godot types. No attract splash. No TWIMTBP.</para>
/// </summary>
public static class RetailClickToStartInput
{
    /// <summary>A compare of stack local +4 with 0x2C at <c>0x0051B660</c>.</summary>
    public const int ConfirmAction = 0x2C;

    /// <summary>A push of constant 0 at <c>0x0051B690</c>. FEP_MAIN.</summary>
    public const int SetPageOrdinal = 0;

    /// <summary>A push of constant 0x32 at <c>0x0051B686</c>.</summary>
    public const int SetPageFrames = 50;

    /// <summary>
    /// DIK scan codes on the two default <c>KEY_ONCE</c> rows for action
    /// <c>0x2C</c>. Enter, Space.
    /// </summary>
    public static readonly int[] DefaultConfirmScanCodes = [0x1C, 0x39];

    /// <summary>A push of constant 0 at <c>0x0051B831</c> — dest X.</summary>
    public const float MouseRectLeft = 0f;

    /// <summary>A push of constant 0 at <c>0x0051B82F</c> — dest Y.</summary>
    public const float MouseRectTop = 0f;

    /// <summary>
    /// Both extents are <c>PLATFORM__GetWindowWidth</c>, not height.
    /// </summary>
    public const bool MouseRectUsesWindowWidthForBothExtents = true;

    /// <summary>
    /// Process does not hit-test the prompt, slide, splash, or title dests.
    /// </summary>
    public const bool HasGlyphHitRect = false;

    /// <summary>
    /// <c>0x0051B660</c> then <c>0x0051B667</c>.
    /// <paramref name="pageSubstate"/> is <c>this+0x0C</c>.
    /// </summary>
    public static bool AcceptsAction(int action, int pageSubstate) =>
        action == ConfirmAction && pageSubstate == 0;

    /// <summary>Whether a default-table DIK would raise action <c>0x2C</c>.</summary>
    public static bool AcceptsDefaultConfirmScanCode(int dik) =>
        dik is 0x1C or 0x39;

    /// <summary>
    /// The reconstruction's 640-class stage sits inside the specimen
    /// window rect <c>[0, width) × [0, width)</c> on any landscape
    /// client. Coordinates are accepted because there is no page-local
    /// glyph box, not because a 32×32 hotspot was invented.
    /// </summary>
    public static bool AcceptsMouseAt(float x, float y)
    {
        _ = x;
        _ = y;
        return !HasGlyphHitRect;
    }
}
