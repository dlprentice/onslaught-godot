// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// FEMessBox Create() arguments and the cited YESNO chrome slice for the
/// main-menu Quit confirm.
///
/// <para><b>Create() width and centre</b> were recovered from
/// <c>CFEPMain__DoAction</c> <c>0x004623E0</c>–<c>0x00462618</c> in the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Selection 6 pushes localization id
/// <c>0xE4</c> at <c>0x004625D1</c>, then:</para>
/// <code>
/// 0x004625E1  push 0x3DCCCCCD   ; 0.1f — not claimed as a pixel height
/// 0x004625E6  push 400.0f
/// 0x004625EB  push 240.0f
/// 0x004625F0  push 320.0f
/// 0x004625F5  call Create
/// </code>
///
/// <para><b>Remaining chrome, not tiles from FEMessBox.cpp.</b>
/// <c>FEMessBox.cpp</c> / <c>FEMessBox.h</c> are absent from the GPL drop and
/// there is no quit-confirm capture under
/// <c>local-lab/retail-reference-pristine/</c>. Pixel height therefore stays
/// reconstruction. The panel, four-edge box, YESNO stack, and init colours
/// are cited from owners that do exist:</para>
/// <list type="bullet">
/// <item>
/// <c>CFrontEnd::DrawPanel</c> / <c>DrawBox</c> at
/// <c>references/Onslaught/FrontEnd.cpp:737-755</c> — blank sprite + four
/// edges of width 2.0. <c>FET2_BLANK</c> is the already-materialized
/// <c>FrontEnd%v2%FE_Blank.tga</c> (16×16).
/// </item>
/// <item>
/// <c>CFrontEnd__RenderAndProcessModalPanel</c> <c>0x0044d6f0</c> option_mode
/// 2 draws FrontEndText tokens <c>0x1b</c> then <c>0x1c</c> as a vertical
/// stack. ASCII fallbacks at VA <c>0x0062b7d4</c> / <c>0x0062b7d0</c> are
/// <c>"Yes"</c> / <c>"No"</c>. Selected 0 highlights the lower (No) row.
/// </item>
/// <item>
/// Panel / text / highlight / border colours are the Init defaults at
/// <c>this+0x1f68/0x1f6c/0x1f70/0x1f74</c> from
/// <c>CFrontEnd__InitPageStateDefaults</c> <c>0x0044d320</c>.
/// </item>
/// <item>
/// Half-extent 0.5, text pad 8, highlight pad 4 are floats
/// <c>0x005d85ec</c> / <c>0x005d8c44</c> / <c>0x005d85bc</c> in the
/// same specimen.
/// </item>
/// </list>
/// </summary>
public static class RetailFeMessBox
{
    /// <summary>Localization id for "Are you sure you want to quit the game?"</summary>
    public const int QuitLocalizationId = 0xE4;

    /// <summary>Create() X. Immediate at <c>0x004625F0</c>.</summary>
    public const float QuitCenterX = 320f;

    /// <summary>Create() Y. Immediate at <c>0x004625EB</c>.</summary>
    public const float QuitCenterY = 240f;

    /// <summary>Create() width. Immediate at <c>0x004625E6</c>.</summary>
    public const float QuitWidth = 400f;

    /// <summary>Left edge of a 400-wide box centred on <see cref="QuitCenterX"/>.</summary>
    public const float QuitLeft = QuitCenterX - (QuitWidth * 0.5f);

    /// <summary>
    /// Reconstruction height. Create()'s fourth immediate is 0.1f (FO(100)),
    /// not a pixel extent, and font+0x54 is not a named field.
    /// </summary>
    public const float ReconstructionHeight = 140f;

    /// <summary>Top of the reconstruction box centred on <see cref="QuitCenterY"/>.</summary>
    public const float BoxTop = QuitCenterY - (ReconstructionHeight * 0.5f);

    /// <summary><c>BLANK_PANEL_SIZE</c> at FrontEnd.cpp:747. FET2_BLANK is 16×16.</summary>
    public const float BlankPanelSize = 16f;

    /// <summary>DrawBox width argument at FrontEnd.cpp:63 / modal renderer 2.0.</summary>
    public const float BoxLineWidth = 2f;

    /// <summary><c>this+0x1f68</c> Init default. DrawPanel fill.</summary>
    public const uint PanelColor = 0xAF000000;

    /// <summary><c>this+0x1f6c</c> Init default. Prompt and choice text.</summary>
    public const uint TextColor = 0xFFFFFFFF;

    /// <summary><c>this+0x1f70</c> Init default. YESNO selected-row DrawPanel.</summary>
    public const uint HighlightColor = 0xAF40FF40;

    /// <summary><c>this+0x1f74</c> Init default. DrawBox edges.</summary>
    public const uint BorderColor = 0xFF7F7F7F;

    /// <summary><c>0x005d8c44</c> = 8.0. Prompt / choice Y pad below the box top.</summary>
    public const float TextPadY = 8f;

    /// <summary><c>0x005d85bc</c> = 4.0. Highlight panel X inset past the label.</summary>
    public const float HighlightPadX = 4f;

    /// <summary>Create() option_mode for YESNO. Quit DoAction pushes 2.</summary>
    public const int OptionYesNo = 2;

    /// <summary>FrontEndText token 0x1b. ASCII fallback "Yes" at VA 0x0062b7d4.</summary>
    public const int YesToken = 0x1B;

    /// <summary>FrontEndText token 0x1c. ASCII fallback "No" at VA 0x0062b7d0.</summary>
    public const int NoToken = 0x1C;

    public const string YesLabel = "Yes";

    public const string NoLabel = "No";

    /// <summary>
    /// Create() writes <c>this+0x1fa0 = 0</c> for option_mode 2. Index 0 is No.
    /// </summary>
    public const int DefaultChoiceIndex = 0;

    /// <summary>
    /// Upper YESNO row. <c>CFrontEnd__HandleModalPanelButton</c> <c>0x0044dd60</c>
    /// option_mode 2 writes <c>this+0x1fa0 = 1</c> on button <c>0x2a</c>
    /// (<c>BUTTON_FRONTEND_MENU_UP</c>).
    /// </summary>
    public const int YesChoiceIndex = 1;

    /// <summary>FONT_SMALL (Font13PS) atlas cell. Used as the prompt line pitch.</summary>
    public const float FontSmallLine = 16f;

    /// <summary>FONT_NORMAL (font22) atlas cell. Used as the YESNO row pitch.</summary>
    public const float FontNormalLine = 32f;

    /// <summary>Prompt origin. One wrap line of 0xe4 fits wrap_width − 16 = 384.</summary>
    public const float PromptTop = BoxTop + TextPadY;

    /// <summary>Upper YESNO row. Token 0x1b is drawn first.</summary>
    public const float YesChoiceTop = PromptTop + FontSmallLine;

    /// <summary>Lower YESNO row. Token 0x1c; selected 0 highlights this row.</summary>
    public const float NoChoiceTop = YesChoiceTop + FontNormalLine;

    public const float ChoiceRowHeight = FontNormalLine;
}
