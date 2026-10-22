// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The CFEPIntro::Render tail after the sixth title pass — one gated
/// <c>CDXSurf__RenderSurface</c> and a font-1 <c>CDXFont__DrawText</c>
/// walk — recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> <c>0x0051BD8C</c>–<c>0x0051BE66</c> (<c>RET 8</c>).
/// Not in the pinned GPL drop (<c>FEPIntro.cpp</c> is absent). This is not
/// a seventh title pass, not Infogrames <c>+0x124</c>, not attract
/// <c>vectorlosttoyssplash</c>, and not TWIMTBP.</para>
///
/// <para><b>Gate.</b> After the sixth-pass <c>ADD ESP, 0x2C</c> /
/// <c>JMP 0x0051BD93</c>, Render loads <c>0x0089BB68</c> and
/// <c>JE 0x0051BE5E</c> when the dword is 0. The three globals
/// (<c>0x0089BB68</c>, <c>0x0089BB6C</c>, <c>0x00897B28</c>) sit in
/// uninitialised <c>.data</c>, so the image-initial flag is 0. An operand
/// scan of the specimen finds no store of those immediates. Process also
/// reads the flag at <c>0x0051B70C</c>: nonzero suppresses the 30 s idle
/// <c>-3</c> and resets <c>this+4</c> / <c>this+0x18</c>. That Process
/// arm is cited, not re-owned here.</para>
///
/// <para><b>Backdrop.</b> When the flag is live, one mode-0 call on
/// <c>0x0089D880</c> (same splash as the pulse) at (0, 0), z 0, colour
/// <c>0xFF000000</c>, sx=sy=10. The 11-dword cdecl pack matches
/// <see cref="RetailClickToStartSplash"/>; only dest/mode/scale/colour
/// change.</para>
///
/// <para><b>Text.</b> A second gate tests the first byte of
/// <c>0x00897B28</c>. Nonzero walks ASCII, skips one leading 0x0A per
/// line, widens bytes to wchar, and calls <c>CPlatform__Font(0x0088A0A8,
/// 1)</c> then <c>CDXFont__DrawText</c> (<c>0x00540640</c>) at X=64,
/// Y=<c>64 − 0x0089BB6C</c>, stepping +16. Colour is <c>push -1</c>.</para>
///
/// <para>No Godot types. Image-initial 0 means
/// <c>DrawClickToStart</c> must not invent this overlay on the cold path.
/// <see cref="RetailClickToStartTitle.Passes"/> stays length 5.</para>
/// </summary>
public static class RetailClickToStartOverlay
{
    /// <summary>Loaded at <c>0x0051BD93</c>. Uninitialised <c>.data</c>.</summary>
    public const uint FlagGlobal = 0x0089BB68u;

    /// <summary>Subtracted from 64 at <c>0x0051BDD2</c>.</summary>
    public const uint ScrollGlobal = 0x0089BB6Cu;

    /// <summary>ASCII buffer walked at <c>0x0051BDE2</c>.</summary>
    public const uint TextGlobal = 0x00897B28u;

    /// <summary>
    /// Image-initial dword in uninitialised <c>.data</c>. Render <c>JE</c>s
    /// the tail. Not a PE-encoded nonzero default.
    /// </summary>
    public const uint ImageInitialFlag = 0u;

    /// <summary>Same global as the pulse splash, loaded at <c>0x0051BDA1</c>.</summary>
    public const uint BackdropTextureGlobal = 0x0089D880u;

    /// <summary>Fourth-from-last push at <c>0x0051BDAF</c>.</summary>
    public const int BackdropMode = 0;

    /// <summary>Colour immediate at <c>0x0051BDBB</c>.</summary>
    public const uint BackdropColor = 0xFF000000u;

    /// <summary>sx=sy immediate <c>0x41200000</c> at <c>0x0051BDB1</c>.</summary>
    public const float BackdropScale = 10f;

    /// <summary>X immediate at <c>0x0051BDC5</c>.</summary>
    public const float BackdropX = 0f;

    /// <summary>Y immediate at <c>0x0051BDC3</c>.</summary>
    public const float BackdropY = 0f;

    /// <summary>Z immediate at <c>0x0051BDC1</c>.</summary>
    public const uint BackdropZBits = 0u;

    /// <summary><c>push 1</c> before <c>CPlatform__Font</c> at <c>0x0051BE38</c>.</summary>
    public const int FontSlot = 1;

    /// <summary>X immediate <c>0x42800000</c> at <c>0x0051BE33</c>.</summary>
    public const float TextX = 64f;

    /// <summary><c>0x005DBB64</c> = <c>64.0f</c>.</summary>
    public const float TextOriginY = 64f;

    /// <summary><c>0x005D8BC0</c> = <c>16.0f</c>.</summary>
    public const float LineStep = 16f;

    /// <summary>Colour immediate at <c>0x0051BE30</c> (<c>push -1</c>).</summary>
    public const uint TextColor = 0xFFFFFFFFu;

    /// <summary>
    /// Whether Render would submit the tail backdrop
    /// (<c>0x0051BD93</c>–<c>0x0051BD9B</c>).
    /// </summary>
    public static bool ShouldDraw(uint flag) => flag != 0u;

    /// <summary>
    /// Whether Render would enter the ASCII walk. Backdrop already submitted
    /// when <paramref name="flag"/> is live; this is the second gate at
    /// <c>0x0051BDE0</c>.
    /// </summary>
    public static bool ShouldDrawText(uint flag, byte firstByte) =>
        flag != 0u && firstByte != 0;

    /// <summary><c>64 − scroll</c> from <c>0x0051BDCC</c>–<c>0x0051BDE7</c>.</summary>
    public static float TextY(float scroll) => TextOriginY - scroll;

    /// <summary>
    /// The lines Render would submit. One leading 0x0A is skipped at the
    /// start of each iteration; a line ends on 0x0A or NUL.
    /// </summary>
    public static string[] Lines(string ascii)
    {
        if (ascii.Length == 0 || ascii[0] == '\0')
        {
            return [];
        }

        var lines = new List<string>();
        int i = 0;
        while (true)
        {
            if (i < ascii.Length && ascii[i] == '\n')
            {
                i++;
            }

            int start = i;
            while (i < ascii.Length && ascii[i] != '\0' && ascii[i] != '\n')
            {
                i++;
            }

            lines.Add(start < i ? ascii[start..i] : "");
            if (i >= ascii.Length || ascii[i] == '\0')
            {
                break;
            }
        }

        return [.. lines];
    }
}
