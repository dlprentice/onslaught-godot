// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render language-selected sine — recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Gate.</b> <c>0x00463191</c> is <c>mov eax, [edi+8]</c>.
/// <c>0x00463194</c> seeds <c>edx = 0xFF7F7F7F</c>.
/// <c>cmp eax, -1 / jne 0x004631D3</c> keeps that skip pack.
/// The hit arm is only the selected-row case. Session cannot hold
/// <c>-1</c>; this type does not invent <c>SetLanguage</c> or a
/// Process increment to light it. DrawLanguageSelector is not a
/// consumer.</para>
///
/// <para><b>Sine.</b> <c>0x0046319E</c> is
/// <c>fld [0x008A9570]; fmul [0x005DB4F4]; fmul [0x005DB4F0];
/// fsin; fmul [0x005DB5DC]; fistp qword [esp+0x28]</c>.
/// The dword at <c>0x005DB4F4</c> is <c>0x4048F5C3</c> (3.14).
/// The dword at <c>0x005DB4F0</c> is <c>0x3D088889</c> (1/30).
/// The dword at <c>0x005DB5DC</c> is <c>0xC2000000</c> (−32).
/// EAX is the low dword of that integer. The pack is
/// <c>eax * 0x010101</c> via <c>shl / add</c>, then
/// <c>edx = 0xFFDFDFDF - eax</c>.</para>
///
/// <para><b>Not the flag tint.</b> Frame 3000 draw 6 is still
/// <c>0xFD3F3F3F</c> after the ESI fade. This helper owns the
/// pre-fade pack only. The 0.9 <c>fcomp</c> at <c>0x004631EF</c>
/// is the already-shipped hover gate, interleaved after the pack.
/// <c>0x00464343</c> is the title-sheen fmod. <c>0x00463E8D</c>
/// is not implemented. HandleKey, DrawLoading, DrawQuitConfirm,
/// HandlePointerConfirm, HandlePointerMotion, the cursor, Apply,
/// dropdown, the colour AND, and the writing-chrome Y stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuLanguageSine
{
    /// <summary><c>fld [0x008A9570]</c> at <c>0x0046319E</c>.</summary>
    public const uint CounterGlobal = 0x008A9570u;

    /// <summary>
    /// Uninitialised BSS. The image does not contain this VA, so the
    /// cold dword is 0.
    /// </summary>
    public const uint ImageInitialCounterBits = 0u;

    /// <summary>The <see cref="ImageInitialCounterBits"/> dword.</summary>
    public static float ImageInitialCounter =>
        BitConverter.UInt32BitsToSingle(ImageInitialCounterBits);

    /// <summary><c>fmul [0x005DB4F4]</c> at <c>0x004631A4</c>.</summary>
    public const uint PiApproxGlobal = 0x005DB4F4u;

    /// <summary>Image bits at <c>0x005DB4F4</c>.</summary>
    public const uint PiApproxBits = 0x4048F5C3u;

    /// <summary>The <see cref="PiApproxBits"/> dword, not a second 3.14 literal.</summary>
    public static float PiApprox => BitConverter.UInt32BitsToSingle(PiApproxBits);

    /// <summary><c>fmul [0x005DB4F0]</c> at <c>0x004631AA</c>.</summary>
    public const uint ReciprocalThirtyGlobal = 0x005DB4F0u;

    /// <summary>Image bits at <c>0x005DB4F0</c>.</summary>
    public const uint ReciprocalThirtyBits = 0x3D088889u;

    /// <summary>The <see cref="ReciprocalThirtyBits"/> dword.</summary>
    public static float ReciprocalThirty =>
        BitConverter.UInt32BitsToSingle(ReciprocalThirtyBits);

    /// <summary><c>fmul [0x005DB5DC]</c> at <c>0x004631B2</c>.</summary>
    public const uint AmplitudeGlobal = 0x005DB5DCu;

    /// <summary>Image bits at <c>0x005DB5DC</c>.</summary>
    public const uint AmplitudeBits = 0xC2000000u;

    /// <summary>The <see cref="AmplitudeBits"/> dword.</summary>
    public static float Amplitude => BitConverter.UInt32BitsToSingle(AmplitudeBits);

    /// <summary><c>mov edx, 0xFF7F7F7F</c> at <c>0x00463194</c>.</summary>
    public const uint SkipPackedColor = 0xFF7F7F7Fu;

    /// <summary><c>mov edx, 0xFFDFDFDF</c> at <c>0x004631CC</c>.</summary>
    public const uint SelectedBasePackedColor = 0xFFDFDFDFu;

    /// <summary><c>cmp eax, -1</c> at <c>0x00463199</c>.</summary>
    public const int LanguageSelectedIndex = -1;

    /// <summary>
    /// Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.
    /// </summary>
    public const bool IsSetLanguage = false;

    /// <summary>
    /// Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.
    /// </summary>
    public const bool IsButtonPressed = false;

    /// <summary><c>cmp [edi+8], -1</c>. Session cannot store this.</summary>
    public static bool IsSelected(int index) => index == LanguageSelectedIndex;

    /// <summary>
    /// Low dword after <c>fistp</c>. Default x87 RC is round-to-nearest-even.
    /// </summary>
    public static int Channel(float counter)
    {
        double scaled = Math.Sin(counter * PiApprox * ReciprocalThirty) * Amplitude;
        return (int)Math.Round(scaled, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Packed ARGB before the ESI fade. Skip is <c>0xFF7F7F7F</c>.
    /// Selected is <c>0xFFDFDFDF - channel * 0x010101</c>.
    /// </summary>
    public static uint PackedColor(bool selected, float counter)
    {
        if (!selected)
        {
            return SkipPackedColor;
        }

        unchecked
        {
            uint spread = (uint)Channel(counter) * 0x010101u;
            return SelectedBasePackedColor - spread;
        }
    }
}
