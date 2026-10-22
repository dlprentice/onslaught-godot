// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/PauseMenu.cpp (the controls and display items: CShadowDetail 004ceef0, CTreeDetail 004cef50,
// CMouseSensitivityMenuItem 004cefe0/004cf000, CVideoDeviceSelect 004cf2c0, CSCModeMenuItem 004cf370, CVSync 004cf500,
// CLandscapeRes 004cf5e0, CTrilinear 004cf6c0, CTextureRes 004cf770, CEnable32Bit 004cf820, CMultiSample 004cfa70,
// CnVidia 004cfd00, CVideoDetailLevel 004cfdb0, CConfigName 004d01d0, CControllerBackMenuItem 004d0220,
// CJoystickEnableItem 004d0310; SyncDetailItems), src/RTTree.cpp (CRTMesh::SetQualityLevel 004dd6b0, GetQualityLevel
// 004dd770) and src/FEPControls.cpp (ApplyControlPreset 00453780, CControllerDefinition 00453970, AllControlBindingsSet
// 00456650; CControllerDefinition::Render 00453ad0, RenderBindingsList 00455010, GetWidth 00456610, GetHeight 00456620,
// IsExpanded 00456630, Refresh 00456640), and the choices' texts (GetChoiceText of each item, PauseMenu.cpp 004ceed0..
// 004d01f0). Reconstructed from the retail program, not in Stuart's release.

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>A choice of a setting the options file keeps, or of the machine's, read and written through the host.</summary>
public class OptionMenuItem(IMenuHost host, int pcText, bool deferred, Func<int> choices, Func<int> get, Action<int> set,
    Func<MenuRender, int, string> choiceText)
    : MenuMenuItem(host, pcText, deferred)
{
    public override int GetNumChoices() => choices();

    public override int GetValue() => get();

    public override void SetValue(int value) => set(value);

    public override string GetChoiceText(MenuRender r, int n) => choiceText(r, n);

    /// <summary>CBOOLMenuItem::GetChoiceText (004a4220): PC texts 6 and 5.</summary>
    public static string BoolText(MenuRender r, int n) => r.Pc(n == 0 ? 6 : 5);
}

/// <summary>CMouseSensitivityMenuItem: gMouseSensitivity in 20 steps of 3, told at every step.</summary>
public sealed class MouseSensitivityMenuItem : ScaleMenuItem
{
    private readonly IMenuHost _host;

    public MouseSensitivityMenuItem(IMenuHost host)
        : base(host, 0, 0x4e, 0.0f, null, 20, true)
    {
        _host = host;
        ReadValue();
    }

    /// <summary>ValueChanged (004cefe0): gMouseSensitivity = (value + 1) * 3.</summary>
    public override void ValueChanged() => _host.MouseSensitivity = (Value + 1) * 3.0f;

    /// <summary>ReadValue (004cf000): int(gMouseSensitivity / 3 + 0.5) - 1, rounded by /QIfist.</summary>
    public void ReadValue() =>
        CommittedValue = Value = X87.Fist((float)X87.Add(X87.Div(_host.MouseSensitivity, 3.0f), 0.5f)) - 1;
}

/// <summary>
/// CControllerDefinition: the key bindings' list (three columns, 230 high), which the keyboard never selects (IsEnabled is
/// FALSE) but the cursor can. Its scroll (mUnknown20) starts at 0 and only its own arrows, drawn while it is not 0, move
/// it, so they never show; its remap capture starts from a click on a binding.
/// </summary>
public sealed class ControllerDefinitionMenuItem() : MenuItem(0, 0x32)
{
    public override bool IsEnabled => false;

    /// <summary>mExpanded: only ever cleared (Refresh).</summary>
    public override bool IsExpanded => false;

    /// <summary>GetWidth (00456610).</summary>
    public override int GetWidth(MenuRender r) => 400;

    /// <summary>GetHeight (00456620).</summary>
    public override int GetHeight() => 230;

    /// <summary>
    /// Render (00453ad0): RenderBindingsList's three columns. They are drawn in FONT_DEBUG with the keys' Windows names
    /// (GetKeyNameTextA), neither of which this port has, so nothing is drawn; selected under a live cursor, the bindings'
    /// hovers and clicks (which start a remap capture through a key trap) need those metrics, so such a run stops. The
    /// key trap's capture (gKeytrapReleased clear) is never started without that click.
    /// </summary>
    public override void Render(MenuRender r, float x, float y, bool selected)
    {
        if (selected && r.MouseActive)
            throw new NotSupportedException("the controls page's bindings list under the cursor needs FONT_DEBUG and the keys' names");
    }
}

/// <summary>
/// CControllerBackMenuItem: back from the controls, reading the binding warning (PC text 0xe8 or 0xe9) instead of Back
/// while a player has no free binding, drawn in pink then.
/// </summary>
public sealed class ControllerBackMenuItem(IMenuHost host, int back) : MenuItem(back)
{
    /// <summary>CPauseMenu::GetBindingWarning (004d0de0)'s PC text, or 0; the pause menu sets the game's form.</summary>
    public Func<int> Warning { get; set; } = () => OptionSettings.AllControlBindingsSet(host.Bindings, 0) ? 0 : 0xe8;

    /// <summary>GetText (004d0220): the warning, or text 0x6f9da.</summary>
    public override string GetText(MenuRender r) => Warning() is var w and not 0 ? r.Pc(w) : r.Text(0x6f9da);

    /// <summary>Render (004d0290).</summary>
    public override void Render(MenuRender r, float x, float y, bool selected)
    {
        if (r.Draw is null) return;
        uint col = Warning() != 0 ? 0xffff8080 : 0xffffffff;
        RenderText(r, x, y, selected, col, GetText(r));
    }
}

/// <summary>
/// CJoystickEnableItem: a joystick's name and whether it is used. Its text (read for each render's widths and drawing)
/// enables it only for a joystick that is there; until the first render it is enabled.
/// </summary>
public sealed class JoystickEnableMenuItem(IMenuHost host, int joystick) : MenuItem(0, 0x5d)
{
    public int Joystick { get; } = joystick;

    /// <summary>GetHeight: 17.</summary>
    public override int GetHeight() => 17;

    /// <summary>GetText (004d0310)'s enable: enabled when LT has the joystick (and DirectInput its name).</summary>
    public override void TextSideEffects() => Enabled = Joystick < host.Machine.Joypads;

    /// <summary>
    /// GetText (004d0310): "Joystick n: " and, for a joystick that is there, its instance name and whether it is used
    /// (PC texts 0x64/0x65), else PC text 0x63. The machine model has no joystick names (the harness has no joysticks).
    /// </summary>
    public override string GetText(MenuRender r)
    {
        TextSideEffects();
        string text = r.Pc(Joystick + 0x5e) + ": ";
        if (Enabled)
            throw new NotSupportedException("a present joystick's instance name is not modelled");
        return text + r.Pc(0x63);
    }

    /// <summary>Render: CMenuItem's, which reads the text (and so the enable) again.</summary>
    public override void Render(MenuRender r, float x, float y, bool selected)
    {
        if (r.Draw is null)
        {
            TextSideEffects();
            return;
        }

        RenderText(r, x, y, selected, 0xffffffff, GetText(r));
    }

    /// <summary>
    /// ReceiveButtonAction (004d0410, PauseMenu.cpp:1374): a joystick taken in or out (a joystick taken out loses its
    /// joystick inputs), then the controls preset again. The item is enabled only for a joystick the machine has, so the
    /// harness (no joysticks) never reaches it; the preset's joystick branches are not ported, so a preset applied while
    /// a present joystick is in use stops the run.
    /// </summary>
    public override void ReceiveButtonAction(int button, float value)
    {
        if (button == FrontEndButtons.MenuSelect)
        {
            OptionSettings.ToggleJoystick(host.Machine, host.Bindings, Joystick);
            if (Enumerable.Range(0, Math.Min(host.Machine.Joypads, host.Machine.JoystickInUse.Length)).Any(j => host.Machine.JoystickInUse[j]))
            {
                throw new NotSupportedException("ApplyControlPreset with a joystick in use: the preset's joystick branches are not ported");
            }

            OptionSettings.ApplyControlPreset(host, host.Options.ControlPreset);
        }
    }
}

/// <summary>The settings the options' controls and display lists change, as functions of the host's state.</summary>
public static class OptionSettings
{
    /// <summary>AllControlBindingsSet (00456650): every configurable binding has the input on that side.</summary>
    public static bool AllControlBindingsSet(ControlBindings bindings, int index)
    {
        for (int b = 0; bindings.Id(b) != -1; b++)
        {
            if (bindings.Configurable(b) && bindings.InputPad(b, index) == -1)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// ApplyControlPreset (00453780): the menu's custom or keyboard/mouse preset, followed by its saved selector.
    /// </summary>
    public static void ApplyControlPreset(IMenuHost host, int preset)
    {
        host.Bindings.ApplyPreset(preset);
        host.Options = host.Options with { ControlPreset = (short)preset };
    }

    /// <summary>
    /// CJoystickEnableItem's toggle (004d0410): LT.mUnknown33474[joystick] flips; taken out, every binding input on that
    /// pad of a joystick type (BUTTON_ON .. ANALOGUE_MINUS_ACT_AS_BUTTON_REPEAT: buttons and axes, not keys or the mouse) loses
    /// its pad.
    /// </summary>
    public static void ToggleJoystick(MachineDisplay machine, ControlBindings bindings, int joystick)
    {
        machine.JoystickInUse[joystick] = !machine.JoystickInUse[joystick];
        if (machine.JoystickInUse[joystick])
        {
            return;
        }

        for (int b = 0; bindings.Id(b) != -1; b++)
        {
            for (int i = 0; i < 2; i++)
            {
                if (bindings.InputPad(b, i) == joystick && bindings.InputType(b, i) >= PushType.ButtonOn
                    && bindings.InputType(b, i) <= PushType.AnalogueMinusActAsButtonRepeat)
                {
                    bindings.SetInputPad(b, i, -1);
                }
            }
        }
    }

    /// <summary>CRTMesh::GetQualityLevel (004dd770): by TREE_MESH_DISTANCE.</summary>
    public static int GetQualityLevel(CareerOptions o)
    {
        float tree = BitConverter.Int32BitsToSingle(o.TreeMeshDistanceBits);
        if (tree < 15.0f)
        {
            return 0;
        }

        return tree > 40.0f ? 2 : 1;
    }

    /// <summary>
    /// CRTMesh::SetQualityLevel (004dd6b0): the tree and mesh distances, the trail detail, the blob shadows' fade and the
    /// landscape's low-resolution geometry for the level. The world render reads the mesh distances from these options
    /// (<see cref="Things.ThingContext.RenderOptions"/>); the trail detail, blob shadows and landscape geometry are drawing only.
    /// </summary>
    public static CareerOptions SetQualityLevel(CareerOptions o, int level)
    {
        static int Bits(float f) => BitConverter.SingleToInt32Bits(f);
        CareerOptions set = level switch
        {
            2 => o with { TreeMeshDistanceBits = Bits(70.0f), MeshLodBias = 0.3f, TrailDetailBits = Bits(2.0f), BlobShadowFadeDistance = Bits(40.0f), LandscapeLowResGeometry = 0 },
            1 => o with { TreeMeshDistanceBits = Bits(30.0f), MeshLodBias = 1.0f, TrailDetailBits = Bits(1.0f), BlobShadowFadeDistance = Bits(30.0f), LandscapeLowResGeometry = 0 },
            0 => o with { TreeMeshDistanceBits = Bits(10.0f), MeshLodBias = 3.0f, TrailDetailBits = Bits(0.1f), BlobShadowFadeDistance = Bits(15.0f), LandscapeLowResGeometry = 1 },
            _ => o,
        };
        return set;
    }

    /// <summary>CShadowDetail::GetValue (004cef30).</summary>
    public static int GetShadowDetail(CareerOptions o) => o.Shadows != 0 ? 2 : (o.BlobShadows != 0 ? 1 : 0);

    /// <summary>CShadowDetail::SetValue (004ceef0).</summary>
    public static CareerOptions SetShadowDetail(CareerOptions o, int value) => value switch
    {
        2 => o with { Shadows = 1, BlobShadows = 1 },
        1 => o with { Shadows = 0, BlobShadows = 1 },
        _ => o with { Shadows = 0, BlobShadows = 0 },
    };

    /// <summary>
    /// SyncDetailItems (CPauseMenu's constructor and each Render): the shadow and tree settings written back as their
    /// items read them.
    /// </summary>
    public static void SyncDetailItems(IMenuHost host)
    {
        CareerOptions o = host.Options;
        o = SetShadowDetail(o, GetShadowDetail(o));
        o = SetQualityLevel(o, GetQualityLevel(o));
        if (!o.Equals(host.Options))
        {
            host.Options = o;
        }
    }
}

/// <summary>CVideoDetailLevel: a preset that sets the other display items' choices, and reads which preset they make.</summary>
public sealed class VideoDetailLevelMenuItem : MenuMenuItem
{
    private readonly MachineDisplay _machine;

    public VideoDetailLevelMenuItem(IMenuHost host)
        : base(host, 0xdf, false)
    {
        _machine = host.Machine;
        LowVSync = _machine.ForceVSync != 0 ? 1 : 0;
        if (_machine.LandscapeMaxLevels < 4)
        {
            MediumLandscapeRes = 0;
            HighLandscapeRes = 0;
        }
        else if (_machine.LandscapeMaxLevels < 5)
        {
            MediumLandscapeRes = 0;
            HighLandscapeRes = 4;
        }
        else
        {
            MediumLandscapeRes = 4;
            HighLandscapeRes = 5;
        }
    }

    public MenuMenuItem Resolution { get; set; } = null!;
    public MenuMenuItem ShadowDetail { get; set; } = null!;
    public MenuMenuItem TreeDetail { get; set; } = null!;
    public MenuMenuItem Trilinear { get; set; } = null!;
    public MenuMenuItem VSync { get; set; } = null!;
    public MenuMenuItem LandscapeRes { get; set; } = null!;
    public MenuMenuItem TextureRes { get; set; } = null!;
    public MenuMenuItem Bits32 { get; set; } = null!;
    public MenuMenuItem MultiSample { get; set; } = null!;

    /// <summary>CEnable32Bit::GetDefault: TEXTURE_ALLOW_32_BIT.</summary>
    public Func<int> Default32Bit { get; set; } = () => 0;

    public int LowVSync { get; }
    public int MediumLandscapeRes { get; }
    public int HighLandscapeRes { get; }

    public override int GetNumChoices() => 4;

    /// <summary>GetChoiceText (004d01a0): PC texts 0xe0 on.</summary>
    public override string GetChoiceText(MenuRender r, int n) => r.Pc(n + 0xe0);

    /// <summary>SetValue (004cfdb0): each item's choice for the preset.</summary>
    public override void SetValue(int value)
    {
        switch (value)
        {
            case 3:
                Resolution.Selection = _machine.HighDetailMode;
                ShadowDetail.Selection = 2;
                TreeDetail.Selection = 2;
                VSync.Selection = 1;
                LandscapeRes.Selection = HighLandscapeRes;
                Trilinear.Selection = 1;
                TextureRes.Selection = _machine.TextureResLossShift;
                Bits32.Selection = Default32Bit();
                if (MultiSample.GetNumChoices() > 1)
                {
                    MultiSample.Selection = 1;
                }

                break;
            case 2:
                Resolution.Selection = _machine.MediumDetailMode;
                ShadowDetail.Selection = 1;
                TreeDetail.Selection = 1;
                VSync.Selection = 1;
                LandscapeRes.Selection = MediumLandscapeRes;
                Trilinear.Selection = 1;
                TextureRes.Selection = _machine.TextureResLossShift;
                Bits32.Selection = Default32Bit();
                MultiSample.Selection = 0;
                break;
            case 1:
                Resolution.Selection = _machine.LowDetailMode;
                ShadowDetail.Selection = 0;
                TreeDetail.Selection = 0;
                VSync.Selection = LowVSync;
                LandscapeRes.Selection = 0;
                Trilinear.Selection = 0;
                TextureRes.Selection = 2;
                Bits32.Selection = 1;
                MultiSample.Selection = 0;
                break;
        }
    }

    /// <summary>GetValue (004cffd0): the preset the items' choices make, or 0 (custom).</summary>
    public override int GetValue()
    {
        if (Resolution.Selection == _machine.LowDetailMode && ShadowDetail.Selection == 0 && TreeDetail.Selection == 0
            && VSync.Selection == LowVSync && LandscapeRes.Selection == 0 && Trilinear.Selection == 0 && TextureRes.Selection == 2
            && Bits32.Selection == 1 && MultiSample.Selection == 0)
        {
            return 1;
        }

        if (Resolution.Selection == _machine.MediumDetailMode && ShadowDetail.Selection == 1 && TreeDetail.Selection == 1
            && VSync.Selection == 1 && LandscapeRes.Selection == MediumLandscapeRes && Trilinear.Selection == 1
            && TextureRes.Selection == _machine.TextureResLossShift && Bits32.Selection == Default32Bit()
            && MultiSample.Selection == 0)
        {
            return 2;
        }

        if (Resolution.Selection == _machine.HighDetailMode && ShadowDetail.Selection == 2 && TreeDetail.Selection == 2
            && VSync.Selection == 1 && LandscapeRes.Selection == HighLandscapeRes && Trilinear.Selection == 1
            && TextureRes.Selection == _machine.TextureResLossShift && Bits32.Selection == Default32Bit()
            && (MultiSample.GetNumChoices() == 1 || MultiSample.Selection == 1))
        {
            return 3;
        }

        return 0;
    }
}

/// <summary>Builds the pause menu's controls and display lists (CPauseMenu's constructor, PauseMenu.cpp 1368-1441).</summary>
public static class OptionsLists
{
    /// <summary>The controls: mouse sensitivity, the preset, god mode with its cheat, the bindings, four joysticks, back.</summary>
    /// <param name="y">The lists' centre: 300 on the options page, 240 in a game.</param>
    public static MenuOptionsList Controls(IMenuHost host, int godOn, int godOff, int back, float y = 300f)
    {
        var list = new MenuOptionsList(host, new MenuTitle(0, 3), 320f, y);
        list.AddItem(new MouseSensitivityMenuItem(host));
        // CConfigName: PC texts 0xf0 (custom) and 0xf3.
        list.AddItem(new OptionMenuItem(host, 0xef, false, () => 2, () => host.Options.ControlPreset,
            v => OptionSettings.ApplyControlPreset(host, v), (r, n) => r.Pc(n == 0 ? 0xf0 : 0xf3)));
        if (host.IsCheatActive(3))
        {
            list.AddItem(new MenuItem(host.Career.Record.IsGod(0) == 1 ? godOn : godOff));
        }

        list.AddItem(new ControllerDefinitionMenuItem());
        for (int j = 0; j < 4; j++)
        {
            list.AddItem(new JoystickEnableMenuItem(host, j));
        }

        list.AddItem(new ControllerBackMenuItem(host, back));
        return list;
    }

    /// <summary>The display: the GeForce FX features, the detail preset and its items, the device, VSync, apply, back.</summary>
    public static MenuOptionsList Display(IMenuHost host, int back, float y = 300f)
    {
        MachineDisplay m = host.Machine;
        var list = new MenuOptionsList(host, new MenuTitle(0, 2), 320f, y);
        bool fx() => m.GeForceFxPower != 0 && m.PixelShaderVersion >= 0xffff0200;
        // CnVidia: off/on with the GeForce FX features, else PC text 0xf8.
        list.AddItem(new OptionMenuItem(host, 0xf7, true, () => fx() ? 2 : 1, () => fx() ? m.ExtraGeForceFxFeatures : 0,
            v => { m.ExtraGeForceFxFeatures = v; m.DisplaySettingChanged(); }, (r, n) => fx() ? OptionMenuItem.BoolText(r, n) : r.Pc(0xf8)));
        var detail = new VideoDetailLevelMenuItem(host);
        list.AddItem(detail);
        // CSCModeMenuItem: "%ld x %ld".
        detail.Resolution = Add(list, new OptionMenuItem(host, 0x13, true, () => m.Modes.Count, () => m.CurrentMode,
            v => { m.CurrentMode = v; host.Options = host.Options with { DisplayModeKey = m.CurrentModeKey }; m.DisplaySettingChanged(); },
            (r, n) => $"{m.Modes[n].Width} x {m.Modes[n].Height}"));
        // CDetailItem: PC texts 0x10 on.
        detail.ShadowDetail = Add(list, new OptionMenuItem(host, 0xe, false, () => 3, () => OptionSettings.GetShadowDetail(host.Options),
            v => host.Options = OptionSettings.SetShadowDetail(host.Options, v), (r, n) => r.Pc(n + 0x10)));
        detail.TreeDetail = Add(list, new OptionMenuItem(host, 0xf, false, () => 3, () => OptionSettings.GetQualityLevel(host.Options),
            v => host.Options = OptionSettings.SetQualityLevel(host.Options, v), (r, n) => r.Pc(n + 0x10)));
        detail.Trilinear = Add(list, new OptionMenuItem(host, 0x26, false, () => 2, () => host.Options.DisallowMipmapping == 0 ? 1 : 0,
            v => host.Options = host.Options with { DisallowMipmapping = v == 0 ? 1 : 0 }, OptionMenuItem.BoolText));
        // CVideoDeviceSelect: the adapter's description.
        list.AddItem(new OptionMenuItem(host, 0xde, true, () => m.NumAdapters, () => m.Adapter,
            v => { m.Adapter = v; m.DisplaySettingChanged(); }, (r, n) => m.AdapterDescription(n)));
        if (m.AllowWidescreenModes != 0)
        {
            throw new NotSupportedException("CScreenShape (ALLOW_WIDESCREEN_MODES) is not ported");
        }

        detail.VSync = Add(list, new OptionMenuItem(host, 0x14, true, () => m.ForceVSync != 0 ? 1 : 2,
            () => m.ForceVSync != 0 ? 0 : host.Options.VSync,
            v => { host.Options = host.Options with { VSync = v }; m.DisplaySettingChanged(); },
            (r, n) => m.ForceVSync != 0 ? r.Pc(5) : OptionMenuItem.BoolText(r, n)));
        detail.LandscapeRes = Add(list, new OptionMenuItem(host, 0x15, false, () => LandscapeResChoices(m), () => GetLandscapeRes(host, m),
            v => SetLandscapeRes(host, m, v), (r, n) => r.Pc(n switch { 0 => 0x25, 1 => 0x24, 2 => 0x23, _ => 0x22 })));
        detail.TextureRes = Add(list, new OptionMenuItem(host, 0x16, true, () => 3,
            () => host.Options.TextureResolutionLossShift > -1 ? host.Options.TextureResolutionLossShift : m.TextureResLossShift,
            v => { host.Options = host.Options with { TextureResolutionLossShift = v }; m.DisplaySettingChanged(); },
            (r, n) => r.Pc(n == m.TextureResLossShift ? n switch { 0 => 0x17, 1 => 0x18, _ => 0x19 } : n switch { 0 => 0x1a, 1 => 0x1b, _ => 0x1c })));
        detail.Bits32 = Add(list, new OptionMenuItem(host, 0x1d, true, () => (m.CurrentModeInfo.Flags & 4) != 0 ? 3 : 1,
            () => host.Options.TextureAllow32Bit != -1 ? host.Options.TextureAllow32Bit : m.TextureAllow32Bit,
            v => { host.Options = host.Options with { TextureAllow32Bit = v }; m.DisplaySettingChanged(); },
            (r, n) => r.Pc(n == m.TextureAllow32Bit ? n switch { 0 => 0x20, 1 => 0x21, _ => 0x1f } : n switch { 0 => 6, 1 => 0x1e, _ => 5 })));
        detail.Default32Bit = () => m.TextureAllow32Bit;
        detail.MultiSample = Add(list, new OptionMenuItem(host, 0xd3, true, () => MultiSampleChoices(m), () => GetMultiSample(host, m),
            v => { host.Options = host.Options with { MultiSampleType = MultiSampleType(m, v) }; m.DisplaySettingChanged(); },
            (r, n) => MultiSampleType(m, n) is var t and >= 2 and <= 16 ? t.ToString(System.Globalization.CultureInfo.InvariantCulture) : r.Pc(0xd4)));
        list.AddItem(new ApplyMenuItem(list));
        OptionSettings.SyncDetailItems(host);
        list.AddItem(new MenuItem(back));
        return list;
    }

    private static MenuMenuItem Add(MenuOptionsList list, MenuMenuItem item)
    {
        list.AddItem(item);
        return item;
    }

    /// <summary>CLandscapeRes::GetNumChoices (004cf5e0).</summary>
    private static int LandscapeResChoices(MachineDisplay m) => m.LandscapeMaxLevels switch
    {
        1 or 2 => 1,
        3 => 2,
        4 => 3,
        _ => 4,
    };

    /// <summary>CLandscapeRes::GetValue (004cf690).</summary>
    private static int GetLandscapeRes(IMenuHost host, MachineDisplay m)
    {
        int max = m.LandscapeMaxLevels;
        int user = host.Options.LandscapeMaxLevels;
        if (user > max)
        {
            return max;
        }

        if (user == 1)
        {
            return 0;
        }

        if (user == 3)
        {
            return 1;
        }

        return (user != 4 ? 1 : 0) + 2;
    }

    /// <summary>CLandscapeRes::SetValue (004cf620): LANDSCAPE_MAXLEVELS_USER 1, 3, 4 or 5.</summary>
    private static void SetLandscapeRes(IMenuHost host, MachineDisplay m, int value)
    {
        if (value > m.LandscapeMaxLevels)
        {
            value = m.LandscapeMaxLevels;
        }

        int user = value switch
        {
            0 => 1,
            1 => 3,
            2 => 4,
            _ => 5,
        };
        host.Options = host.Options with { LandscapeMaxLevels = user };
    }

    private static readonly (int Bit, int Type)[] MultiSampleBits = [(0x20, 2), (0x40, 3), (0x80, 4), (0x100, 6), (0x200, 8)];

    /// <summary>CMultiSample::GetNumChoices (004cfa70): none and each type the mode supports.</summary>
    private static int MultiSampleChoices(MachineDisplay m) => 1 + MultiSampleBits.Count(b => (m.CurrentModeInfo.Flags & b.Bit) != 0);

    /// <summary>The n-th supported type (1-based; 0 none), or 0 past them.</summary>
    private static int MultiSampleType(MachineDisplay m, int n)
    {
        if (n == 0)
        {
            return 0;
        }

        foreach ((int bit, int type) in MultiSampleBits)
        {
            if ((m.CurrentModeInfo.Flags & bit) != 0)
            {
                n--;
            }

            if (n == 0)
            {
                return type;
            }
        }

        return 0;
    }

    /// <summary>The choice of a type, or 0.</summary>
    private static int MultiSampleIndex(MachineDisplay m, int type)
    {
        int n = 0;
        foreach ((int bit, int t) in MultiSampleBits)
        {
            if ((m.CurrentModeInfo.Flags & bit) != 0)
            {
                n++;
                if (type == t)
                {
                    return n;
                }
            }
        }

        return 0;
    }

    /// <summary>CMultiSample::GetValue (004cfbd0): the device's type, else SUGGEST_MULTISAMPLE_TYPE's.</summary>
    private static int GetMultiSample(IMenuHost host, MachineDisplay m)
    {
        int type = host.Options.MultiSampleType;
        if (type != -1)
        {
            if (type == 0)
            {
                return 0;
            }

            int index = MultiSampleIndex(m, type);
            if (index != -1)
            {
                return index;
            }
        }

        int suggest = m.SuggestMultiSampleType;
        if (suggest == -1)
        {
            return -1;
        }

        return suggest == 0 ? 0 : MultiSampleIndex(m, suggest);
    }
}
