// SPDX-License-Identifier: GPL-3.0-only
// The machine state the options' controls and display lists read (bea-decomp src/PauseMenu.cpp: LT's adapters, devices
// and modes, the display tweaks that the options file does not keep, the joysticks).

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>
/// What the display list reads from the machine: CD3DApplication's adapters, the current device's modes and caps, the
/// tweaks the hardware sets (LANDSCAPE_MAXLEVELS, TEXTURE_ALLOW_32_BIT, ...) and the joysticks. The defaults are the
/// port's harness at a level-100 pause (offscreen, <c>-forcewindowed -res 640 480</c>; a read of 2026-10-16):
/// one adapter, three 4:3 modes with flags 0x2bf, pixel shader 3.0, no joysticks. The mode list is the harness's: a
/// real display lists others, so references through these lists hold for that configuration only.
/// </summary>
public sealed class MachineDisplay(Platform.DisplayReset reset)
{
    /// <summary>
    /// A display mode: size and flags (D3DModeInfo +0x10: bit 2 allows 32-bit textures, bits 5-9 multisample types).
    /// Bits32 is the format classification used by CDisplayModeKey::Set: D3DFMT_R8G8B8/A8R8G8B8/X8R8G8B8
    /// (20/21/22) are true; other formats are false. It is not inferred from the dimensions or texture flags.
    /// </summary>
    public sealed record Mode(int Width, int Height, int Flags, bool Bits32);

    /// <summary>LT.m_dwNumAdapters.</summary>
    public int NumAdapters { get; init; } = 1;

    /// <summary>
    /// Each adapter's d3dAdapterIdentifier.Description (CVideoDeviceSelect's choices; -1 is the current adapter). The
    /// harness's is not recorded: drawing only.
    /// </summary>
    public Func<int, string> AdapterDescription { get; init; } = _ => "";

    /// <summary>LT.m_dwAdapter.</summary>
    public int Adapter { get; set; }

    /// <summary>The current device's modes.</summary>
    public IReadOnlyList<Mode> Modes { get; init; } =
    [
        new(640, 480, 0x2bf, true),
        new(800, 600, 0x2bf, true),
        new(1024, 768, 0x2bf, true),
    ];

    /// <summary>dwCurrentMode.</summary>
    public int CurrentMode { get; set; }

    /// <summary>dwLowDetailMode, dwMediumDetailMode, dwHighDetailMode.</summary>
    public int LowDetailMode { get; init; }

    public int MediumDetailMode { get; init; } = 1;

    public int HighDetailMode { get; init; } = 2;

    /// <summary>m_d3dCaps.PixelShaderVersion.</summary>
    public uint PixelShaderVersion { get; init; } = 0xffff0300;

    /// <summary>FORCE_VSYNC.</summary>
    public int ForceVSync { get; init; }

    /// <summary>LANDSCAPE_MAXLEVELS (the user's choice is the options file's).</summary>
    public int LandscapeMaxLevels { get; init; } = 5;

    /// <summary>SUGGEST_MULTISAMPLE_TYPE.</summary>
    public int SuggestMultiSampleType { get; init; }

    /// <summary>TEXTURE_ALLOW_32_BIT.</summary>
    public int TextureAllow32Bit { get; init; } = 2;

    /// <summary>TEXTURE_RES_LOSS_SHIFT.</summary>
    public int TextureResLossShift { get; init; }

    /// <summary>GEFORCE_FX_POWER.</summary>
    public int GeForceFxPower { get; init; }

    /// <summary>EXTRA_GEFORCE_FX_FEATURES.</summary>
    public int ExtraGeForceFxFeatures { get; set; }

    /// <summary>ALLOW_WIDESCREEN_MODES.</summary>
    public int AllowWidescreenModes { get; init; }

    /// <summary>LT.mJoypads.</summary>
    public int Joypads { get; init; }

    /// <summary>LT.mUnknown33474[MAX_JOYPADS]: whether each joystick is used (TRUE for each pad at start).</summary>
    public bool[] JoystickInUse { get; } = [true, true, true, true];

    /// <summary>The process-wide request; this is the clock's owner, not a menu-local copy.</summary>
    public Platform.DisplayReset Reset { get; } = reset;

    /// <summary>sDisplayApplied (00631170): the apply count the display last reset for.</summary>
    private int _displayApplied = -1;

    public Mode CurrentModeInfo => Modes[CurrentMode];

    /// <summary>CCareer::WriteOptions obtains the mode key from the currently selected device mode.</summary>
    public uint CurrentModeKey => ((uint)CurrentModeInfo.Width & 0xffffu) | (((uint)CurrentModeInfo.Height & 0x7fffu) << 16)
        | (CurrentModeInfo.Bits32 ? 0x80000000u : 0u);

    /// <summary>
    /// DisplaySettingChanged: once per Apply serial, queue state 1 and return so every setting commits. The next
    /// platform Flip performs the reset; a host without device/resource reset support fails at that boundary.
    /// </summary>
    public void DisplaySettingChanged()
    {
        if (_displayApplied != ApplyMenuItem.ApplyCount)
        {
            Reset.RequestApply();
            _displayApplied = ApplyMenuItem.ApplyCount;
        }
    }
}
