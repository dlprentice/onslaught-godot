// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/frontend.h (EFrontEndTexture) and src/FrontEndData.cpp (CFrontEndData::Init 004687e0).

namespace OnslaughtRebuild.Parity.Flow.Draw;

/// <summary><c>EFrontEndTexture</c>: the front end's textures (<c>FED.GetTexture</c>), in the order Init loads them.</summary>
public enum FrontEndTexture
{
    /// <summary><c>FET_LEVEL_LINK</c>.</summary>
    LevelLink = 0,
    /// <summary><c>FET_BARL</c>.</summary>
    Barl = 1,
    /// <summary><c>FET_BARC</c>.</summary>
    Barc = 2,
    /// <summary><c>FET_BARR</c>.</summary>
    Barr = 3,
    /// <summary><c>FET_GLARE_ALPHA</c>.</summary>
    GlareAlpha = 4,
    /// <summary><c>FET_LOST_TOYS</c>.</summary>
    LostToys = 5,
    /// <summary><c>FET_RANKING_S</c>.</summary>
    RankingS = 6,
    /// <summary><c>FET_RANKING_A</c>.</summary>
    RankingA = 7,
    /// <summary><c>FET_RANKING_B</c>.</summary>
    RankingB = 8,
    /// <summary><c>FET_RANKING_C</c>.</summary>
    RankingC = 9,
    /// <summary><c>FET_RANKING_D</c>.</summary>
    RankingD = 10,
    /// <summary><c>FET_RANKING_E</c>.</summary>
    RankingE = 11,
    /// <summary><c>FET_RANKING_SMASK</c>.</summary>
    RankingSmask = 12,
    /// <summary><c>FET_RANKING_AMASK</c>.</summary>
    RankingAmask = 13,
    /// <summary><c>FET_RANKING_BMASK</c>.</summary>
    RankingBmask = 14,
    /// <summary><c>FET_RANKING_CMASK</c>.</summary>
    RankingCmask = 15,
    /// <summary><c>FET_RANKING_DMASK</c>.</summary>
    RankingDmask = 16,
    /// <summary><c>FET_RANKING_EMASK</c>.</summary>
    RankingEmask = 17,
    /// <summary><c>FET_FORSETI_TEXT</c>.</summary>
    ForsetiText = 18,
    /// <summary><c>FET_FORSETI_WRITING_SMALL</c>.</summary>
    ForsetiWritingSmall = 19,
    /// <summary><c>FET_WHITE_RING</c>.</summary>
    WhiteRing = 20,
    /// <summary><c>FET_LOGO_SHINE</c>.</summary>
    LogoShine = 21,
    /// <summary><c>FET_SELECT_LEVEL_RING_BRACKET01</c>.</summary>
    SelectLevelRingBracket01 = 22,
    /// <summary><c>FET_SELECT_LEVEL_RING_BRACKET02</c>.</summary>
    SelectLevelRingBracket02 = 23,
    /// <summary><c>FET_LEVEL_SELECT_MASK</c>.</summary>
    LevelSelectMask = 24,
    /// <summary><c>FET_METAL_RING_TRANS_FROM_LEVSEL1</c>.</summary>
    MetalRingTransFromLevsel1 = 25,
    /// <summary><c>FET_METAL_RING_TRANS_FROM_LEVSEL2</c>.</summary>
    MetalRingTransFromLevsel2 = 26,
    /// <summary><c>FET_LEVEL_SELECT_GLOW_BLUE</c>.</summary>
    LevelSelectGlowBlue = 27,
    /// <summary><c>FET_LEVEL_SELECT_GLOW_ORANGE</c>.</summary>
    LevelSelectGlowOrange = 28,
    /// <summary><c>FET_RANKING_SMALL_MASK_A</c>.</summary>
    RankingSmallMaskA = 29,
    /// <summary><c>FET_RANKING_SMALL_MASK_B</c>.</summary>
    RankingSmallMaskB = 30,
    /// <summary><c>FET_RANKING_SMALL_MASK_C</c>.</summary>
    RankingSmallMaskC = 31,
    /// <summary><c>FET_RANKING_SMALL_MASK_D</c>.</summary>
    RankingSmallMaskD = 32,
    /// <summary><c>FET_RANKING_SMALL_MASK_E</c>.</summary>
    RankingSmallMaskE = 33,
    /// <summary><c>FET_RANKING_SMALL_MASK_S</c>.</summary>
    RankingSmallMaskS = 34,
    /// <summary><c>FET2_WHITE</c>.</summary>
    White = 35,
    /// <summary><c>FET_SELECTION_MARKER</c>.</summary>
    SelectionMarker = 36,
    /// <summary><c>FET_AVI_BOX</c>.</summary>
    AviBox = 37,
    /// <summary><c>FET_HELP_ARROW</c>.</summary>
    HelpArrow = 38,
    /// <summary><c>FET_GOODIES1</c>.</summary>
    Goodies1 = 39,
    /// <summary><c>FET_GOODIES2</c>.</summary>
    Goodies2 = 40,
    /// <summary><c>FET_GOODIES3</c>.</summary>
    Goodies3 = 41,
    /// <summary><c>FET_GOODIES4</c>.</summary>
    Goodies4 = 42,
    /// <summary><c>FET_FLAG_UK</c>.</summary>
    FlagUk = 43,
    /// <summary><c>FET_FLAG_FR</c>.</summary>
    FlagFr = 44,
    /// <summary><c>FET_FLAG_GR</c>.</summary>
    FlagGr = 45,
    /// <summary><c>FET_FLAG_SP</c>.</summary>
    FlagSp = 46,
    /// <summary><c>FET_FLAG_IT</c>.</summary>
    FlagIt = 47,
    /// <summary><c>FET_I_TROOP</c>.</summary>
    ITroop = 48,
    /// <summary><c>FET_I_TANK</c>.</summary>
    ITank = 49,
    /// <summary><c>FET_I_PLANE</c>.</summary>
    IPlane = 50,
    /// <summary><c>FET_I_BILLY</c>.</summary>
    IBilly = 51,
    /// <summary><c>FET_I_LORENZO</c>.</summary>
    ILorenzo = 52,
    /// <summary><c>FET_I_TARA</c>.</summary>
    ITara = 53,
    /// <summary><c>FET_FE_SPLASH1</c>.</summary>
    FeSplash1 = 54,
    /// <summary><c>FET_FE_INFOGRAMES</c>.</summary>
    FeInfogrames = 55,
    /// <summary><c>FET_SELECT_LEVEL_EPISODE_CURVE</c>.</summary>
    SelectLevelEpisodeCurve = 56,
    /// <summary><c>FET_BEA_TITLE2</c>.</summary>
    BeaTitle2 = 57,
    /// <summary><c>FET_SMALL_BEA_LOGO</c>.</summary>
    SmallBeaLogo = 58,
    /// <summary><c>FET_BEA_TITLE_BRACKET01</c>.</summary>
    BeaTitleBracket01 = 59,
    /// <summary><c>FET_BEA_TITLE_BRACKET02</c>.</summary>
    BeaTitleBracket02 = 60,
    /// <summary><c>FET_BEA_TITLE_TEXT_BOX</c>.</summary>
    BeaTitleTextBox = 61,
    /// <summary><c>FET_BEA_TITLE_SYMBOL_BRACKET01</c>.</summary>
    BeaTitleSymbolBracket01 = 62,
    /// <summary><c>FET_BEA_TITLE_SYMBOL_BRACKET02</c>.</summary>
    BeaTitleSymbolBracket02 = 63,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_NEWGAME01</c>.</summary>
    BeaTitleNavSymbolNewgame01 = 64,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_CONTINUEGAME01</c>.</summary>
    BeaTitleNavSymbolContinuegame01 = 65,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_SAVEGAME01</c>.</summary>
    BeaTitleNavSymbolSavegame01 = 66,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_LOADGAME01</c>.</summary>
    BeaTitleNavSymbolLoadgame01 = 67,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_MULTIPLAYER01</c>.</summary>
    BeaTitleNavSymbolMultiplayer01 = 68,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_GOODIES01</c>.</summary>
    BeaTitleNavSymbolGoodies01 = 69,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_OPTIONS01</c>.</summary>
    BeaTitleNavSymbolOptions01 = 70,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_LANGUAGE01</c>.</summary>
    BeaTitleNavSymbolLanguage01 = 71,
    /// <summary><c>FET_BEA_TITLE_NAV_SYMBOL_QUITGAME01</c>.</summary>
    BeaTitleNavSymbolQuitgame01 = 72,
    /// <summary><c>FET3_HEADER_BRACKET1</c>.</summary>
    HeaderBracket1 = 73,
    /// <summary><c>FET3_HEADER_BRACKET2</c>.</summary>
    HeaderBracket2 = 74,
    /// <summary><c>FET_BEA_HEADER_BRACKET03</c>.</summary>
    BeaHeaderBracket03 = 75,
    /// <summary><c>FET3_HEADER_TEXT_BOX</c>.</summary>
    HeaderTextBox = 76,
    /// <summary><c>FET3_SELECT_BRACKET1</c>.</summary>
    SelectBracket1 = 77,
    /// <summary><c>FET3_SELECT_BRACKET2</c>.</summary>
    SelectBracket2 = 78,
    /// <summary><c>FET3_SELECT_BRACKET_MASK</c>.</summary>
    SelectBracketMask = 79,
    /// <summary><c>FET_SMALL_STAR</c>.</summary>
    SmallStar = 80,
    /// <summary><c>FET2_BLANK</c>.</summary>
    Blank = 81,
    /// <summary><c>FET_VK_KEY</c>.</summary>
    VkKey = 82,
    /// <summary><c>FET_XB_XBOX</c>.</summary>
    XbXbox = 83,
    /// <summary><c>FET_XB_MU</c>.</summary>
    XbMu = 84,
    /// <summary><c>FET_XB_MU_CORRUPT</c>.</summary>
    XbMuCorrupt = 85,
}

/// <summary>
/// The front end's textures as <c>CFrontEndData::Init</c> finds them (<c>CTEXTURE::FindTexture(name, A8R8G8B8, ...)</c>), and
/// each one's size (<c>GetRealWidth</c>/<c>GetRealHeight</c>: the loaded image's, shifted back by the load's resolution
/// loss, which the front end's textures do not have). The pixels are the host's.
/// </summary>
public sealed class FrontEndTextures
{
    /// <summary>Each texture's name as Init passes it to FindTexture.</summary>
    public static readonly string[] Names =
    [
        @"FrontEnd\LevelLink.tga",
        @"FrontEnd\BarL.tga",
        @"FrontEnd\BarC.tga",
        @"FrontEnd\BarR.tga",
        @"FrontEnd\GlareAlpha.tga",
        @"FrontEnd\LostToys.tga",
        @"FrontEnd\RankingS.tga",
        @"FrontEnd\RankingA.tga",
        @"FrontEnd\RankingB.tga",
        @"FrontEnd\RankingC.tga",
        @"FrontEnd\RankingD.tga",
        @"FrontEnd\RankingE.tga",
        @"FrontEnd\RankingSMask.tga",
        @"FrontEnd\RankingAMask.tga",
        @"FrontEnd\RankingBMask.tga",
        @"FrontEnd\RankingCMask.tga",
        @"FrontEnd\RankingDMask.tga",
        @"FrontEnd\RankingEMask.tga",
        @"FrontEnd\v2\FE_Forseti_Writing_large.tga",
        @"FrontEnd\v2\FE_Forseti_Writing_small.tga",
        @"FrontEnd\v2\FE_white_ring.tga",
        @"FrontEnd\v2\FE_Reflection_map.tga",
        @"FrontEnd\v3\FE_select_level_ring_bracket01.tga",
        @"FrontEnd\v3\FE_select_level_ring_bracket02.tga",
        @"FrontEnd\v2\FE_level_select_mask.tga",
        @"FrontEnd\v2\FE_metal_ring_trans_from_levsel1.tga",
        @"FrontEnd\v2\FE_metal_ring_trans_from_levsel2.tga",
        @"FrontEnd\v2\FE_level_select_glow_blue.tga",
        @"FrontEnd\v2\FE_level_select_glow_orange.tga",
        @"FrontEnd\v2\FE_Ranking_Small_Mask_A.tga",
        @"FrontEnd\v2\FE_Ranking_Small_Mask_B.tga",
        @"FrontEnd\v2\FE_Ranking_Small_Mask_C.tga",
        @"FrontEnd\v2\FE_Ranking_Small_Mask_D.tga",
        @"FrontEnd\v2\FE_Ranking_Small_Mask_E.tga",
        @"FrontEnd\v2\FE_Ranking_Small_Mask_S.tga",
        @"FrontEnd\v2\FE_White.tga",
        @"FrontEnd\v2\FE_Rock_Background.tga",
        @"FrontEnd\v2\FE_avi_box.tga",
        @"FrontEnd\v2\FE_Arrow.tga",
        @"FrontEnd\v2\FE_Goodies1.tga",
        @"FrontEnd\v2\FE_Goodies2.tga",
        @"FrontEnd\v2\FE_Goodies3.tga",
        @"FrontEnd\v2\FE_Goodies4.tga",
        @"FrontEnd\v2\Flag_UK.tga",
        @"FrontEnd\v2\Flag_FR.tga",
        @"FrontEnd\v2\Flag_GR.tga",
        @"FrontEnd\v2\Flag_SP.tga",
        @"FrontEnd\v2\Flag_IT.tga",
        @"FrontEnd\v2\i_troop.tga",
        @"FrontEnd\v2\i_tank.tga",
        @"FrontEnd\v2\i_plane.tga",
        @"FrontEnd\v2\i_Billy.tga",
        @"FrontEnd\v2\i_Lorenzo.tga",
        @"FrontEnd\v2\i_Tara.tga",
        @"FrontEnd\v2\fe_splash1.tga",
        @"FrontEnd\v2\fe_infogrames.tga",
        @"FrontEnd\v3\FE_select_level_episode_curve.tga",
        @"FrontEnd\v3\FE_BEA_Title2.tga",
        @"FrontEnd\v3\FE_BEA_Gamelogo_small.tga",
        @"FrontEnd\v3\FE_BEA_title_bracket01.tga",
        @"FrontEnd\v3\FE_BEA_title_bracket02.tga",
        @"FrontEnd\v3\FE_BEA_title_text_box.tga",
        @"FrontEnd\v3\FE_BEA_title_symbol_bracket01.tga",
        @"FrontEnd\v3\FE_BEA_title_symbol_bracket02.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_newgame01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_continuegame01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_savegame01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_loadgame01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_multiplayer01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_goodies01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_options01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_language01.tga",
        @"FrontEnd\v3\FE_BEA_title_nav_symbol_quitgame01.tga",
        @"FrontEnd\v3\FE_BEA_header_bracket01.tga",
        @"FrontEnd\v3\FE_BEA_header_bracket02.tga",
        @"FrontEnd\v3\FE_BEA_header_bracket03.tga",
        @"FrontEnd\v3\FE_BEA_header_box.tga",
        @"FrontEnd\v3\FE_select_level_bracket01.tga",
        @"FrontEnd\v3\FE_select_level_bracket02.tga",
        @"FrontEnd\v3\FE_select_level_bracket_mask.tga",
        @"FrontEnd\v3\SmallStar.tga",
        @"FrontEnd\v2\FE_Blank.tga",
        @"FrontEnd\v2\FE_VK_Key.tga",
        @"FrontEnd\v2\FE_XB_Xbox.tga",
        @"FrontEnd\v2\FE_XB_MU.tga",
        @"FrontEnd\v2\FE_XB_MU_Corrupt.tga",
    ];

    private readonly (int Width, int Height)?[] _sizes = new (int, int)?[Names.Length];

    /// <summary>
    /// Reads each texture's size from its frame file's DDS header: data\resources\textures\NAME(0)A8R8G8B8.aya, then the
    /// DXTn copy in data\resources\dxtntextures, then the uncompressible mustbe_ name (names use % for \). A texture with no
    /// file has no size.
    /// </summary>
    public FrontEndTextures(Func<string, (string Name, byte[] Bytes)> read)
    {
        _read = read;
        for (int i = 0; i < Names.Length; i++)
        {
            _sizes[i] = SizeOf(read, Names[i]);
        }

        CursorSize = SizeOf(read, CursorName);
    }

    /// <summary>
    /// The paths a texture's frame 0 may be read from, in the order they are tried. As LoadFrame builds them
    /// (<c>BuildTextureFramePath</c>, DXTexture.cpp), a path longer than its folder's length (0x18 for textures\, 0x1c for
    /// dxtntextures\) plus 0x41 is cut to 0x3d characters past that length and ends ".aya" again: the installation's long
    /// names are stored cut the same way.
    /// </summary>
    public static string[] FramePaths(string name, string format = DefaultFormat)
    {
        string file = name.Replace('\\', '%') + "(0)" + format + ".aya";
        return [Cut($@"data\resources\textures\{file}", 0x18), Cut($@"data\resources\dxtntextures\{file}", 0x1c),
            Cut($@"data\resources\textures\mustbe_{file}", 0x18)];
    }

    private static string Cut(string path, int len) => path.Length > len + 0x41 ? path[..(len + 0x3d)] + ".aya" : path;

    /// <summary>The mouse cursor's texture, which <c>RenderMouseCursor</c> finds by name.</summary>
    public const string CursorName = "mouse.tga";

    /// <summary>The format the front end's textures are found with (<c>FindTexture(name, A8R8G8B8, ...)</c>).</summary>
    public const string DefaultFormat = "A8R8G8B8";

    /// <summary>The cursor texture's size, or null without its file.</summary>
    public (int Width, int Height)? CursorSize { get; private set; }

    private readonly Func<string, (string Name, byte[] Bytes)> _read = null!;
    private readonly Dictionary<(string, string), (int Width, int Height)?> _named = new();

    /// <summary>A texture found by name (<c>CDXTexture::FindTexture</c>)'s size, read on first use; null without its file.</summary>
    public (int Width, int Height)? NamedSize(string name, string format = DefaultFormat)
    {
        var key = (name.ToLowerInvariant(), format);
        if (!_named.TryGetValue(key, out (int Width, int Height)? size))
        {
            size = SizeOf(_read, name, format);
            _named[key] = size;
        }

        return size;
    }

    /// <summary>The texture's real size, or null when the installation has no file for it.</summary>
    public (int Width, int Height)? Size(FrontEndTexture texture) => _sizes[(int)texture];

    private static (int Width, int Height)? SizeOf(Func<string, (string Name, byte[] Bytes)> read, string name, string format = DefaultFormat)
    {
        foreach (string path in FramePaths(name, format))
        {
            (string Name, byte[] Bytes) found;
            try
            {
                found = read(path);
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            Data.MemBuffer mb = Data.MemBuffer.FromFile(found.Name, found.Bytes);
            var dds = new byte[Math.Min(mb.Length, 20)];
            mb.Read(dds);
            // "DDS ", the header's size, flags, then height and width.
            if (dds.Length == 20 && dds[0] == (byte)'D' && dds[1] == (byte)'D' && dds[2] == (byte)'S')
            {
                return (BitConverter.ToInt32(dds, 16), BitConverter.ToInt32(dds, 12));
            }
        }

        return null;
    }
}
