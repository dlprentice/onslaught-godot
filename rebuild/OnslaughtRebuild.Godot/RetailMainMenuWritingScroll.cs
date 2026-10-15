// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render prologue Y for the three <c>0x0089D7F0</c> Forseti
/// writing tiles — recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this cycle
/// (2,506,752 bytes).
///
/// <para><b>Prologue.</b> <c>0x00462D46</c> is
/// <c>fld [0x008A9570]; fmul [0x005D8CB4]; fld qword [0x005DB5F0];
/// call 0x0055E3EA; fsubr [0x005DB5E8]; fstp [esp+0x10]</c>.
/// The dword at <c>0x005D8CB4</c> is <c>0x3E99999A</c> (0.3). The qword
/// at <c>0x005DB5F0</c> is 350.0. The dword at <c>0x005DB5E8</c> is 175.0.
/// <c>0x0055E3EA</c> is the same CRT fmod thunk the Options Apply pulse
/// already names. Local <c>[esp+0x10]</c> is therefore
/// <c>175 − fmod(mCounter × 0.3, 350)</c>.</para>
///
/// <para><b>Tiles.</b> Three <c>CDXSurf__RenderSurface</c> calls on
/// <c>0x0089D7F0</c> consume that local before
/// <c>0x00462E96</c> overwrites the slot with <c>0x43860000</c> (268)
/// for the language row. Tile 0 pushes the local as Y. Tile 1
/// <c>fadd [0x005DB5E4]</c> (350). Tile 2 <c>fadd [0x005DB3B0]</c>
/// (700). X is the <c>0x43E50000</c> (458) immediate on each call.
/// This helper owns the Y scroll, not the 458 X and not the 0.9 Z.</para>
///
/// <para><b>Counter.</b> <c>0x008A9570</c> is uninitialised BSS — the
/// image is 2,506,752 bytes, so that VA is not in the file. Every
/// in-image immediate is <c>fld</c>. This type takes the float and
/// does not invent a Process increment. Cold / image-initial is 0,
/// which restores the three settled Y values 175 / 525 / 875.
/// FrontEnd.cpp:597 <c>mCounter++</c> is not the retail Process
/// head at <c>0x00466BA0</c>.</para>
///
/// <para><b>Not the twin fade.</b> <c>0x00464343</c> is the title-sheen
/// fmod, a different pool. <c>0x00463E8D</c> is not implemented.
/// DrawMainMenu is the consumer. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion, the
/// cursor, Apply, dropdown, and the colour AND stay untouched. This
/// is not <c>SetLanguage</c>.</para>
/// </summary>
public static class RetailMainMenuWritingScroll
{
    /// <summary><c>fld [0x008A9570]</c> at <c>0x00462D46</c>.</summary>
    public const uint CounterGlobal = 0x008A9570u;

    /// <summary>
    /// Uninitialised BSS. The image does not contain this VA, so the
    /// cold dword is 0.
    /// </summary>
    public const uint ImageInitialCounterBits = 0u;

    /// <summary>The <see cref="ImageInitialCounterBits"/> dword.</summary>
    public static float ImageInitialCounter =>
        BitConverter.UInt32BitsToSingle(ImageInitialCounterBits);

    /// <summary><c>fmul [0x005D8CB4]</c> at <c>0x00462D4E</c>.</summary>
    public const uint RateGlobal = 0x005D8CB4u;

    /// <summary>Image bits at <c>0x005D8CB4</c>.</summary>
    public const uint RateBits = 0x3E99999Au;

    /// <summary>The <see cref="RateBits"/> dword, not a second 0.3 literal.</summary>
    public static float Rate => BitConverter.UInt32BitsToSingle(RateBits);

    /// <summary><c>fld qword [0x005DB5F0]</c> at <c>0x00462D54</c>.</summary>
    public const uint PeriodGlobal = 0x005DB5F0u;

    /// <summary>Image bits at <c>0x005DB5F0</c>.</summary>
    public const ulong PeriodBits = 0x4075E00000000000UL;

    /// <summary>The <see cref="PeriodBits"/> qword, not a second 350 literal.</summary>
    public static double Period => BitConverter.Int64BitsToDouble(unchecked((long)PeriodBits));

    /// <summary><c>fsubr [0x005DB5E8]</c> at <c>0x00462D71</c>.</summary>
    public const uint OriginYGlobal = 0x005DB5E8u;

    /// <summary>Image bits at <c>0x005DB5E8</c>.</summary>
    public const uint OriginYBits = 0x432F0000u;

    /// <summary>The <see cref="OriginYBits"/> dword.</summary>
    public static float OriginY => BitConverter.UInt32BitsToSingle(OriginYBits);

    /// <summary>Tile 1 <c>fadd [0x005DB5E4]</c> at <c>0x00462E1C</c>.</summary>
    public const uint Tile1AddGlobal = 0x005DB5E4u;

    /// <summary>Image bits at <c>0x005DB5E4</c>.</summary>
    public const uint Tile1AddBits = 0x43AF0000u;

    /// <summary>The <see cref="Tile1AddBits"/> dword.</summary>
    public static float Tile1Add => BitConverter.UInt32BitsToSingle(Tile1AddBits);

    /// <summary>Tile 2 <c>fadd [0x005DB3B0]</c> at <c>0x00462E59</c>.</summary>
    public const uint Tile2AddGlobal = 0x005DB3B0u;

    /// <summary>Image bits at <c>0x005DB3B0</c>.</summary>
    public const uint Tile2AddBits = 0x442F0000u;

    /// <summary>The <see cref="Tile2AddBits"/> dword.</summary>
    public static float Tile2Add => BitConverter.UInt32BitsToSingle(Tile2AddBits);

    /// <summary>Three unrolled <c>0x0089D7F0</c> submits.</summary>
    public const int TileCount = 3;

    /// <summary><c>push 0x43E50000</c> on each of the three calls.</summary>
    public const float TileX = 458f;

    /// <summary><c>call 0x0055E3EA</c> at <c>0x00462D6C</c>.</summary>
    public const uint FmodSite = 0x0055E3EAu;

    /// <summary>
    /// <c>0x00462E96</c> reuses the slot for language Y. Not this scroll.
    /// </summary>
    public const float LanguageSlotOverwriteY = 268f;

    /// <summary>
    /// <c>175 − fmod(counter × 0.3, 350)</c>. The mul is the float
    /// rate; the remainder is CRT fmod (toward-zero).
    /// </summary>
    public static float OffsetY(float counter)
    {
        float scaled = counter * Rate;
        double period = Period;
        double wrapped = scaled - (period * Math.Truncate(scaled / period));
        return OriginY - (float)wrapped;
    }

    /// <summary>
    /// Tile 0 is the prologue local. Tile 1 adds 350. Tile 2 adds 700.
    /// </summary>
    public static float TileY(float counter, int tile) => tile switch
    {
        0 => OffsetY(counter),
        1 => OffsetY(counter) + Tile1Add,
        2 => OffsetY(counter) + Tile2Add,
        _ => throw new ArgumentOutOfRangeException(nameof(tile), tile, "Retail draws three tiles."),
    };
}
