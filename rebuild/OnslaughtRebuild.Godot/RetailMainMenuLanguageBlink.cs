// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render language-chevron blink — recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Sites.</b> Left <c>0x00463334</c>, right <c>0x004634C0</c>.
/// Both <c>fld [0x008A9570]; fistp; and edx, 0x8000003F</c>, then the
/// MSVC negative fixup <c>dec / or 0xFFFFFFC0 / inc</c>, then
/// <c>cmp edx, 0x32 / jl draw</c>. Hidden band falls through to
/// <c>fld [edi+0x1C]</c> (right: <c>+0x20</c>)
/// <c>fcomp [0x005D856C]</c>. That dword is 0. <c>test ah, 0x41 / jne
/// skip</c> means timer ≤ 0 hides. This type takes the float and
/// does not invent a Process increment.</para>
///
/// <para><b>Cold.</b> <c>0x008A9570</c> is uninitialised BSS. Remainder
/// 0 is below 50, so settled frames draw both 51.2 chevrons. The 2x
/// copies at diffuse <c>0x007F7F7F</c> stay no-ops — this helper does
/// not submit them. Not <c>SetLanguage</c>. Not the 0x00463E8D twin
/// fade. DrawLanguageSelector is the consumer. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion, the
/// cursor, Apply, dropdown, the colour AND, and the writing-chrome Y
/// stay untouched.</para>
/// </summary>
public static class RetailMainMenuLanguageBlink
{
    /// <summary><c>fld [0x008A9570]</c> at <c>0x00463334</c>.</summary>
    public const uint CounterGlobal = 0x008A9570u;

    /// <summary>
    /// Uninitialised BSS. The image does not contain this VA, so the
    /// cold dword is 0.
    /// </summary>
    public const uint ImageInitialCounterBits = 0u;

    /// <summary>The <see cref="ImageInitialCounterBits"/> dword.</summary>
    public static float ImageInitialCounter =>
        BitConverter.UInt32BitsToSingle(ImageInitialCounterBits);

    /// <summary>Left chevron <c>fld</c> at <c>0x00463334</c>.</summary>
    public const uint LeftSite = 0x00463334u;

    /// <summary>Right chevron <c>fld</c> at <c>0x004634C0</c>.</summary>
    public const uint RightSite = 0x004634C0u;

    /// <summary><c>and edx, 0x8000003F</c> at <c>0x00463345</c>.</summary>
    public const uint SignedMask = 0x8000003Fu;

    /// <summary><c>or edx, 0xFFFFFFC0</c> at <c>0x0046334E</c>.</summary>
    public const uint NegativeFixup = 0xFFFFFFC0u;

    /// <summary>The 64-tick period implied by the 6-bit remainder.</summary>
    public const int Period = 64;

    /// <summary><c>cmp edx, 0x32</c> at <c>0x00463352</c>.</summary>
    public const int VisibleBelow = 0x32;

    /// <summary><c>fcomp [0x005D856C]</c> at <c>0x0046335A</c>.</summary>
    public const uint TimerThresholdGlobal = 0x005D856Cu;

    /// <summary>Image bits at <c>0x005D856C</c>.</summary>
    public const uint TimerThresholdBits = 0u;

    /// <summary>The <see cref="TimerThresholdBits"/> dword.</summary>
    public static float TimerThreshold =>
        BitConverter.UInt32BitsToSingle(TimerThresholdBits);

    /// <summary>
    /// <c>[edi+0x1C]</c> / <c>[edi+0x20]</c> have no in-image store
    /// recovered here. Cold is 0, matching the compare immediate.
    /// </summary>
    public const float ImageInitialTimer = 0f;

    /// <summary>
    /// Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.
    /// </summary>
    public const bool IsSetLanguage = false;

    /// <summary>
    /// Draws 8 and 10 stay no-ops. This helper does not submit them.
    /// </summary>
    public const bool DrawsDoubleCopy = false;

    /// <summary>
    /// MSVC signed remainder after <c>fistp</c> and the
    /// <c>0x8000003F</c> fixup.
    /// </summary>
    public static int Remainder(float counter)
    {
        int value = (int)Math.Round(counter, MidpointRounding.ToEven);
        unchecked
        {
            uint edx = (uint)value & SignedMask;
            if ((int)edx >= 0)
            {
                return (int)edx;
            }

            edx--;
            edx |= NegativeFixup;
            edx++;
            return (int)edx;
        }
    }

    /// <summary>
    /// <c>cmp edx, 0x32 / jl draw</c>, else timer &gt; 0.
    /// </summary>
    public static bool ShouldDraw(float counter, float timer)
    {
        if (Remainder(counter) < VisibleBelow)
        {
            return true;
        }

        return timer > TimerThreshold;
    }
}
