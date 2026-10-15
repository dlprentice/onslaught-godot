// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client;

/// <summary>
/// The 22-row, three-column bindings grid inside Controller Options.
///
/// <para><b>Row set and action codes: bytes.</b></para>
/// <c>ControlsUI__RenderBindingsList</c> (<c>0x00455010</c>) derives
/// <c>action_code = rowIndex + 0x37</c> and loops while <c>rowIndex &lt; 0x16</c>,
/// so the list is exactly 22 rows over codes <c>0x37</c>-<c>0x4C</c>, with the
/// labels coming from <c>Localization__GetStringById(action_code)</c>. Rows
/// <c>0x3A</c>, <c>0x3F</c>, <c>0x44</c> and <c>0x47</c> resolve to
/// <c>&amp;0x00677D78</c> in the BSS tail and render blank.
///
/// <para><b>Column contents: pixels.</b></para>
/// Every binding string below is read off
/// <c>local-lab/retail-captures-options-pause-2026-07-27/fep-options-controller-640x480.png</c>.
/// They corroborate the shipped default table
/// (<c>OptionsEntries__InitDefaultDualBindingsTable</c> <c>0x00453460</c>) on
/// every key it names, and they corroborate <c>rebuild/PROVENANCE.md</c>'s
/// controlled-runtime finding that movement is bound to both WASD and the arrow
/// keys while Look consumes the mouse axes.
///
/// <para><b>What the two columns actually are - and what this settles.</b></para>
/// The recovery note left it open whether the two persisted binding slots are two
/// players or two alternate bindings for one player: the render path reads
/// per-player, a capture said both slots were live for player one. The frame
/// settles it in favour of SLOTS. A single-keyboard install would have to show
/// player 2 as <c>&lt;undefined&gt;</c> (Localization <c>0x78</c>) on every row;
/// instead the right column carries a complete WASD/IJKL set, which is exactly
/// slot 1 of the shipped single-player default table. The "Player 1"/"Player 2"
/// headers are drawn over the slot columns regardless. This type therefore
/// presents two slots and does NOT model a second player.
/// </summary>
public static class RetailControlBindings
{
    /// <summary>Grid rows. First action code is <c>0x37</c>.</summary>
    public const int RowCount = 22;

    /// <summary>
    /// Grid row pitch, MEASURED: the SystemFont bands on the retail frame sit at
    /// y = 151 + 10*row for every one of the 18 non-blank rows.
    /// </summary>
    public const float RowPitch = 10f;

    /// <summary>Pad between the sub-widget's own top and its first grid row.</summary>
    public const float TopPad = 5f;

    /// <summary>
    /// Left column origin. Retail's left-column ink starts at x=51 on 15 of the 18
    /// non-blank rows; the SystemFont glyph carries a 1px left bearing inside its
    /// 7px cell, so the cell origin is 50.
    /// </summary>
    public const float LeftColumnX = 50f;

    /// <summary>
    /// Right column advance end. Retail's right-column ink ends at x=588 on 12 of
    /// the 18 rows; the same 1px bearing on the trailing side puts the advance at
    /// 590.
    /// </summary>
    public const float RightColumnRight = 590f;

    /// <summary>
    /// Header row. Localization <c>0x33</c>/<c>0x34</c> supply "Player 1" and
    /// "Player 2" at columns 0 and 2.
    /// </summary>
    public const string Player1Header = "Player 1";
    public const string Player2Header = "Player 2";

    public static IReadOnlyList<RetailControlBindingRow> Rows { get; } =
    [
        new(0x37, RetailControlBindingRowKind.Header, "Control configuration details", Player1Header, Player2Header),
        new(0x38, RetailControlBindingRowKind.InvertWalker, "Walker mode invert Y axis", "", ""),
        new(0x39, RetailControlBindingRowKind.InvertFlight, "Flight mode invert Y axis", "", ""),
        new(0x3A, RetailControlBindingRowKind.Spacer, "", "", ""),
        new(0x3B, RetailControlBindingRowKind.Binding, "Movement: Forward", "UP", "Key W"),
        new(0x3C, RetailControlBindingRowKind.Binding, "Backward", "DOWN", "Key S"),
        new(0x3D, RetailControlBindingRowKind.Binding, "Left", "LEFT", "Key A"),
        new(0x3E, RetailControlBindingRowKind.Binding, "Right", "RIGHT", "Key D"),
        new(0x3F, RetailControlBindingRowKind.Spacer, "", "", ""),
        new(0x40, RetailControlBindingRowKind.Binding, "Look: Up", "Mouse", "Key I"),
        new(0x41, RetailControlBindingRowKind.Binding, "Down", "Mouse", "Key K"),
        new(0x42, RetailControlBindingRowKind.Binding, "Left", "Mouse", "Key J"),
        new(0x43, RetailControlBindingRowKind.Binding, "Right", "Mouse", "Key L"),
        new(0x44, RetailControlBindingRowKind.Spacer, "", "", ""),
        new(0x45, RetailControlBindingRowKind.Binding, "Zoom: In", "Mousewheel down", "Key ="),
        new(0x46, RetailControlBindingRowKind.Binding, "Out", "Mousewheel up", "Key -"),
        new(0x47, RetailControlBindingRowKind.Spacer, "", "", ""),
        new(0x48, RetailControlBindingRowKind.Binding, "Others: Fire weapon", "Left Mouse Button", "Caps Lock"),
        new(0x49, RetailControlBindingRowKind.Binding, "Select Weapon", "Right Mouse Button", "Key ;"),
        new(0x4A, RetailControlBindingRowKind.Binding, "Transform", "Num 0", "Space"),
        new(0x4B, RetailControlBindingRowKind.Binding, "Air Brake", "RIGHT CONTROL", "Shift"),
        new(0x4C, RetailControlBindingRowKind.Binding, "Special function", "Right Shift", "Tab"),
    ];

    /// <summary>
    /// The word the invert-Y rows draw for a flag value.
    ///
    /// <b>The rendered word is the opposite of the effect, and that is deliberate
    /// - do not "fix" it.</b> <c>ControlsUI__RenderBindingsList</c> draws
    /// Localization <c>0x36</c> "Off" when the flag is NON-ZERO and <c>0x35</c>
    /// "On" when it is zero, while <c>references/Onslaught/Player.cpp:325-332</c>
    /// negates pitch when the flag is TRUE.
    /// </summary>
    public static string InvertYLabel(bool inverted) => inverted ? "Off" : "On";
}

public enum RetailControlBindingRowKind
{
    Header,
    Spacer,
    Binding,
    InvertWalker,
    InvertFlight,
}

/// <param name="ActionCode">Localization index the label is drawn from.</param>
/// <param name="Slot0">Binding shown in the left column (slot 0).</param>
/// <param name="Slot1">Binding shown in the right column (slot 1).</param>
public readonly record struct RetailControlBindingRow(
    int ActionCode,
    RetailControlBindingRowKind Kind,
    string Label,
    string Slot0,
    string Slot1);
