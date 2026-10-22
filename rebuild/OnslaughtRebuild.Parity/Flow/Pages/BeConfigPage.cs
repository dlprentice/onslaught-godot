// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/FEPBEConfig.cpp (reconstructed from the retail program; not in Stuart's release):
// GetConfigDescription 0044eb30, GetNumJetWeapons 0044ed40, GetNumWalkerWeapons 0044eea0, GetWalkerWeaponRating 0044f030,
// GetJetWeaponRating 0044f300, GetWalkerWeaponName 0044f530, GetJetWeaponName 0044f830, RenderPreCommon 00450390,
// RenderRatingPips 00450460 and Render 004505b0.

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Physics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The Battle Engine configuration page (<c>CFEPBEConfig</c>): the selected level's configurations, chosen with up and
/// down. Select stores the choice and its cockpit in the game's front-end settings, then goes to the wingmen page (from
/// world 231 on, where wingmen are offered) or leaves the front end to play the level.
/// </summary>
/// <remarks>
/// Not modelled: the auto-configuration test's select; the turning Battle Engine model (<c>CFrontEndThing</c>, drawn
/// only, with its camera and lights), which is not drawn. Once the page is settled the arrows' click boxes are at x 162.
/// </remarks>
public sealed class BeConfigPage(FrontEnd frontEnd) : FrontEndPageBase
{
    public int Config { get; private set; }
    public float Time { get; private set; }
    public float UpFlash { get; private set; }
    public float DownFlash { get; private set; }
    public float ChangeTime { get; private set; }
    public float StartTime { get; private set; }

    private WorldHeader? CurrentLevel => frontEnd.WorldHeaders?.ForLevel(frontEnd.SelectedLevel);

    /// <summary><c>GetNumConfigs</c> (0x0044ecf0).</summary>
    public int NumConfigs => CurrentLevel?.Configurations.Count ?? 0;

    /// <summary><c>GetConfigName</c> (0x0044eab0): the n'th name, or null.</summary>
    public string? ConfigName(int n)
    {
        IReadOnlyList<string>? configs = CurrentLevel?.Configurations;
        return configs is not null && n >= 0 && n < configs.Count ? configs[n] : null;
    }

    private int NumWingmen => CurrentLevel?.NumWingmen ?? 0;

    /// <summary>
    /// <c>CFEPBEConfig::Init</c> (0x0044fa90): reads <c>data\WorldHeaders.dat</c>; without it the front end cannot start
    /// (retail reports a fatal file error).
    /// </summary>
    public override bool Init()
    {
        ChangeTime = -1000f;
        Time = 0f;
        Config = 0;
        UpFlash = 0f;
        DownFlash = 0f;
        byte[]? bytes = frontEnd.ReadDataFile?.Invoke(WorldHeaders.FileName);
        if (bytes is null) return false;
        frontEnd.WorldHeaders = WorldHeaders.Read(MemBuffer.FromFile(WorldHeaders.FileName, bytes));
        return true;
    }

    /// <summary><c>CFEPBEConfig::Process</c> (0x00450010).</summary>
    public override void Process(FrontEndPageState state)
    {
        Time = (float)X87.Add(Time, 0.01f);
        UpFlash = Math.Max((float)X87.Sub(UpFlash, 0.1f), 0f);
        DownFlash = Math.Max((float)X87.Sub(DownFlash, 0.1f), 0f);
    }

    /// <summary><c>CFEPBEConfig::ButtonPressed</c> (0x00450090). Down steps back through the list, up forward, both wrapping.</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuBack:
                frontEnd.SetPage(FrontEndPage.Briefing, 25);
                frontEnd.PlaySound(FrontEndSound.Back);
                break;
            case FrontEndButtons.MenuSelect:
            {
                frontEnd.Settings.Player1Configuration = Config;
                string? name = ConfigName(Config);
                if (name is not null)
                {
                    string? cockpit = (frontEnd.CockpitOf ?? throw new InvalidOperationException(
                        "Choosing a configuration needs the Battle Engine data (FrontEnd.CockpitOf).")).Invoke(name);
                    if (cockpit is not null) frontEnd.Settings.Player1CockpitMesh = cockpit;
                }
                if (frontEnd.SelectedLevel >= 231 && NumWingmen != 0)
                {
                    frontEnd.SetPage(FrontEndPage.Wingmen, 25);
                }
                else
                {
                    frontEnd.Quit = frontEnd.SelectedLevel;
                }
                frontEnd.PlaySound(FrontEndSound.Select);
                break;
            }
            case FrontEndButtons.MenuDown:
                if (NumConfigs > 1)
                {
                    if (--Config < 0) Config = NumConfigs - 1;
                    frontEnd.PlaySound(FrontEndSound.Move);
                    DownFlash = 1f;
                    ChangeTime = frontEnd.Clock.GetSysTimeFloat();
                }
                break;
            case FrontEndButtons.MenuUp:
                if (NumConfigs > 1)
                {
                    Config++;
                    if (Config > NumConfigs - 1) Config = 0;
                    frontEnd.PlaySound(FrontEndSound.Move);
                    UpFlash = 1f;
                    ChangeTime = frontEnd.Clock.GetSysTimeFloat();
                }
                break;
        }
    }

    /// <summary>
    /// <c>CFEPBEConfig::RenderPreCommon</c> (0x00450390): the selection marker, sliding away towards the wingmen page, or
    /// still once the page has settled.
    /// </summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest)
    {
        if (dest == FrontEndPage.Wingmen)
            frontEnd.RenderSelectionMarker((MathF.Cos(transition * 3.14159265358979f) + 1f) * 100f, 0f, 1.25f, 255);
        else if (transition == 1f)
            frontEnd.RenderSelectionMarker(0f, 0f, 1.25f, 255);
    }

    /// <summary>
    /// <c>CFEPBEConfig::Render</c> (0x004505b0). Its state-changing part: the help arrows, then, with more than one
    /// configuration, the up arrow's click box and, only while the down arrow is drawn (its blink is on, or it is
    /// flashing), the down arrow's. With <see cref="FrontEnd.Draw"/> it also draws: the panel's brackets (sliding in from
    /// the left, out to the right towards the wingmen page), the Forseti writing, the level select's ring and the small
    /// logo once settled, the configuration's name and its ground and air weapons (revealed again after a change), each
    /// weapon's ratings against troops, tanks and aircraft as stars, the up and down arrows (flashing larger after a
    /// press) and the title bar. The turning Battle Engine between the brackets and the writing is not drawn.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        // x87 (004505b6..004505ef): fcos of the 24-bit product, negated and less 1 straight from the register (the cosine is
        // not stored to a float first), then times 0.5 and 600, each rounded to 24 bits.
        float slide = (float)X87.Mul(X87.Mul(X87.Sub(-X87.Cos((float)X87.Mul(transition, MathF.PI)), 1f), 0.5f), 600f);
        float offset = dest == FrontEndPage.Wingmen ? -slide : slide;
        float x = (float)X87.Sub(162f, offset);
        FrontEndDraw? draw = frontEnd.Draw;

        if (draw is not null)
        {
            float shx = FrontEndDraw.ShadowOffsetX(frontEnd.Counter);
            // Retail offsets both shadow coordinates by the x shadow offset.
            draw.DrawColouredSprite(x + 5f + shx, 262f + 5f + shx, FrontEndDraw.FO(890), FrontEndTexture.HeaderBracket2, 0x3e000000,
                0.7f * 1.05f, 0.7f * 1.05f, SpriteAlign.Centre);
            draw.DrawColouredSprite(x, 262f, FrontEndDraw.FO(890), FrontEndTexture.BeaHeaderBracket03, 0xff037fa0, 0.7f, 0.7f, SpriteAlign.Centre);
            draw.DrawColouredSprite(x, 262f, FrontEndDraw.FO(890), FrontEndTexture.HeaderBracket2, 0xffffffff, 0.7f, 0.7f, SpriteAlign.Centre);

            // The Battle Engine (CFrontEndThing, under its own projection, camera and three lights) is not drawn.

            if (transition == 1f || dest == FrontEndPage.Wingmen) frontEnd.RenderSmallForsetiText(1f);
            if (transition == 1f || dest == FrontEndPage.Wingmen)
                draw.DrawColouredSprite(285f, 225f, FrontEndDraw.FO(900), FrontEndTexture.MetalRingTransFromLevsel2, 0xffffffff, 1.6f, 1.6f,
                    SpriteAlign.Centre);
            if (transition == 1f || dest == FrontEndPage.Wingmen) frontEnd.RenderSmallBEALogo(1f);
        }

        if (NumConfigs > 1) frontEnd.RenderHelpArrow(5, transition);
        else if (frontEnd.SelectedLevel >= 231 && NumWingmen != 0) frontEnd.RenderHelpArrow(4, transition);
        else frontEnd.RenderHelpArrow(0, transition);
        frontEnd.RenderHelpArrow(1, transition);

        RenderConfiguration(draw, offset);

        if (NumConfigs > 1)
        {
            float left = (float)X87.Sub(x, 40f), right = (float)X87.Add(x, 40f);
            frontEnd.ProcessMouseClickInRect(left, 101f, right, 181f, FrontEndButtons.MenuUp);
            // SINT(counter): fistp under /QIfist, to nearest (004517f4).
            if (draw is not null && (X87.Fist(frontEnd.Counter) % 64 < 50 || UpFlash > 0f))
                DrawArrow(draw, x, 141f, -(3.14159265358979f / 2f), UpFlash);
            if (X87.Fist(frontEnd.Counter) % 64 < 50 || DownFlash > 0f)
            {
                frontEnd.ProcessMouseClickInRect(left, 344f, right, 424f, FrontEndButtons.MenuDown);
                if (draw is not null) DrawArrow(draw, x, 384f, 3.14159265358979f / 2f, DownFlash);
            }
        }

        // The title is read whether or not it is drawn (each read checks the X key).
        if (frontEnd.Texts is { } texts)
        {
            string title = texts.GetTextByName("FETX_BE_CONFIG");
            if (dest != FrontEndPage.Wingmen) frontEnd.DrawTitleBar(title, transition, dest);
            else frontEnd.DrawTitleBar(title, 1f, FrontEndPage.Wingmen);
        }
    }

    // An arrow, then its flash: white, growing from twice its size as the flash fades.
    private static void DrawArrow(FrontEndDraw draw, float x, float y, float yaw, float flash)
    {
        draw.DrawColouredSprite(x, y, FrontEndDraw.FO(100), FrontEndTexture.HelpArrow, 0xffffffff, 1f, 1f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        uint col = FrontEndDraw.BlendAlpha2(0xffffffff, X87.Fist(flash * 255f));
        float scale = 1f - flash + 1f;
        draw.DrawColouredSprite(x, y, FrontEndDraw.FO(100), FrontEndTexture.HelpArrow, col, scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
    }

    /// <summary>
    /// The configuration's name (normal font), the ground and air weapons under their headings (small font; the names
    /// revealed from the last change), the rating columns' icons and each weapon's stars. The reads retail's render makes
    /// here are made whether or not anything is drawn, in its order: the clock for the reveal, the two headings'
    /// <c>FET.GetText</c> (each checks the X key), then a clock read whose value is unused, before the rating columns.
    /// The names from the language file (<c>TEXT.GetStringById</c>) have no side effect and are read only to be drawn.
    /// </summary>
    private void RenderConfiguration(FrontEndDraw? draw, float offset)
    {
        Text.FrontEndTexts? texts = frontEnd.Texts;
        float reveal = frontEnd.Clock.GetSysTimeFloat() - ChangeTime;
        string? ground = texts?.GetTextByName("FETX_GROUND_WEAPONS");
        string? air = texts?.GetTextByName("FETX_AIR_WEAPONS");
        frontEnd.Clock.GetSysTimeFloat();

        if (draw is null || texts is null || ground is null || air is null || frontEnd.NormalFont is not { } normal
            || frontEnd.SmallFont is not { } small) return;
        var normalKind = CanvasTextureKind.NormalFont;
        var smallKind = CanvasTextureKind.SmallFont;

        string text = ConfigDescription(texts, Config);
        int cx = normal.GetTextExtent(text).Width;
        draw.DrawTextDynamic(normal, normalKind, 400f - cx * 0.5f - offset, 100f, FrontEndDraw.FO(100), 1f, 1f, 0xffffffff, text, 1000f);

        float tx = 280f - offset;
        draw.DrawTextDynamic(small, smallKind, tx, 210f, FrontEndDraw.FO(100), 1f, 1f, 0xffff6f1f, ground, 1000f);

        float y = 226f;
        int walker = NumWalkerWeapons(Config);
        for (int i = 0; i < walker; i++)
        {
            draw.DrawTextDynamic(small, smallKind, tx, y, FrontEndDraw.FO(100), 1f, 1f, 0xffffffff, WalkerWeaponName(texts, Config, i), reveal);
            y += 16f;
        }

        y += 16f;
        draw.DrawTextDynamic(small, smallKind, tx, y, FrontEndDraw.FO(100), 1f, 1f, 0xffff6f1f, air, 1000f);
        y += 16f;

        int jet = NumJetWeapons(Config);
        for (int i = 0; i < jet; i++)
        {
            draw.DrawTextDynamic(small, smallKind, tx, y, FrontEndDraw.FO(100), 1f, 1f, 0xffffffff, JetWeaponName(texts, Config, i), reveal);
            y += 16f;
        }

        draw.DrawColouredSprite(445f - offset, 180f, FrontEndDraw.FO(100), FrontEndTexture.ITroop, 0xffffffff);
        draw.DrawColouredSprite(480f - offset, 180f, FrontEndDraw.FO(100), FrontEndTexture.ITank, 0xffffffff);
        draw.DrawColouredSprite(515f - offset, 180f, FrontEndDraw.FO(100), FrontEndTexture.IPlane, 0xffffffff);

        float py = 234f;
        for (int j = 0; j < walker; j++)
        {
            RenderRatingPips(draw, 460f - offset, py, WeaponRating(WalkerWeapon(Config, j), 0) / 5.0f, 0xffff6f4f);
            RenderRatingPips(draw, 495f - offset, py, WeaponRating(WalkerWeapon(Config, j), 1) / 5.0f, 0xff6fff4f);
            RenderRatingPips(draw, 530f - offset, py, WeaponRating(WalkerWeapon(Config, j), 2) / 5.0f, 0xff4f6fff);
            py += 16f;
        }

        py += 16f;
        py += 16f;
        for (int j = 0; j < jet; j++)
        {
            RenderRatingPips(draw, 460f - offset, py, WeaponRating(JetWeapon(Config, j), 0) / 5.0f, 0xffff6f4f);
            RenderRatingPips(draw, 495f - offset, py, WeaponRating(JetWeapon(Config, j), 1) / 5.0f, 0xff6fff4f);
            RenderRatingPips(draw, 530f - offset, py, WeaponRating(JetWeapon(Config, j), 2) / 5.0f, 0xff4f6fff);
            py += 16f;
        }
    }

    /// <summary>
    /// <c>CFEPBEConfig::RenderRatingPips</c> (0x00450460): value x 5 stars centred on x, 6 apart, each with its shadow two
    /// pixels down and right; one star is red, two or three blue, four or five green.
    /// </summary>
    private static void RenderRatingPips(FrontEndDraw draw, float x, float y, float value, uint colour)
    {
        // SINT(value * 5.01f): fistp under /QIfist.
        int n = X87.Fist(value * 5.01f);
        int alpha = (int)(colour >> 24);
        x -= (n - 1) * 6f * 0.5f;
        switch (n)
        {
            case 1: colour = 0xffff6f4f; break;
            case 2:
            case 3: colour = 0xff4f6fff; break;
            case 4:
            case 5: colour = 0xff6fff5f; break;
        }
        colour = FrontEndDraw.BlendAlpha2(colour, alpha);
        for (int i = 0; i < n; i++)
        {
            draw.DrawColouredSprite(x + 2f, y + 2f, FrontEndDraw.FO(100), FrontEndTexture.SmallStar, FrontEndDraw.BlendAlpha2(0x7f000000, alpha),
                0.7f, 0.7f, SpriteAlign.Centre);
            draw.DrawColouredSprite(x, y, FrontEndDraw.FO(100), FrontEndTexture.SmallStar, colour, 0.7f, 0.7f, SpriteAlign.Centre);
            x += 6f;
        }
    }

    // --- The configurations' weapons, for drawing only ------------------------------------------------------------

    private (BattleEngineDataManager? Engines, PhysicsRegistry? Physics)? _drawData;

    /// <summary>
    /// The Battle Engine configurations and the physics script's weapons (<c>UBattleEngineDataManager</c>,
    /// <c>UPhysicsManager</c>, which the game loads at start-up), read once through <see cref="FrontEnd.ReadDataFile"/>
    /// for the drawing alone: nothing in the simulation reads them from here. Either is null when the host does not supply its file.
    /// </summary>
    private (BattleEngineDataManager? Engines, PhysicsRegistry? Physics) DrawData => _drawData ??= (
        LoadDrawData(@"data\battle engine configurations.dat", m => BattleEngineDataManager.Load(m)),
        LoadDrawData(@"data\default physics.dat", m => PhysicsRegistry.Build(PhysicsScript.Read(m))));

    private T? LoadDrawData<T>(string name, Func<MemBuffer, T> load) where T : class
    {
        try
        {
            return frontEnd.ReadDataFile?.Invoke(name) is { } bytes ? load(MemBuffer.FromFile(name, bytes)) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private BattleEngineData? Configuration(int n) =>
        CurrentLevel is not null && ConfigName(n) is { } name ? DrawData.Engines?.GetConfiguration(name) : null;

    /// <summary><c>GetConfigDescription</c> (0x0044eb30): the configuration's name in the loaded language.</summary>
    private static string ConfigDescription(Text.FrontEndTexts texts, BattleEngineData? data) => data?.LanguageName switch
    {
        1 => texts.ById(0x97d47d6b),
        2 => texts.ById(0xe3fcbf7c),
        3 => texts.ById(0x3025018d),
        4 => texts.ById(0x7c4d439e),
        5 => texts.ById(0xc87585af),
        _ => "Unknown Configuration",
    };

    private string ConfigDescription(Text.FrontEndTexts texts, int n) => ConfigDescription(texts, Configuration(n));

    /// <summary><c>GetNumJetWeapons</c> (0x0044ed40).</summary>
    private int NumJetWeapons(int n) => Configuration(n)?.JetWeapons.Count ?? 0;

    /// <summary><c>GetNumWalkerWeapons</c> (0x0044eea0): the primary weapon (when it has a name at all, even an empty one) and the walker's.</summary>
    private int NumWalkerWeapons(int n) => Configuration(n) is { } data ? (data.PrimaryWeapon is not null ? 1 : 0) + data.WalkerWeapons.Count : 0;

    /// <summary>The walker's weapon as GetWalkerWeaponName/Rating find it (the primary weapon first, when it has one), or null.</summary>
    private WeaponData? WalkerWeapon(int n, int weapon)
    {
        if (Configuration(n) is not { } data) return null;
        if (data.PrimaryWeapon is not null)
        {
            if (weapon == 0) return DrawData.Physics?.GetWeaponData(data.PrimaryWeapon);
            weapon--;
        }
        return weapon >= 0 && weapon < data.WalkerWeapons.Count ? DrawData.Physics?.GetWeaponData(data.WalkerWeapons[weapon]) : null;
    }

    /// <summary>The jet's weapon as GetJetWeaponName/Rating find it, or null.</summary>
    private WeaponData? JetWeapon(int n, int weapon) =>
        Configuration(n) is { } data && weapon >= 0 && weapon < data.JetWeapons.Count ? DrawData.Physics?.GetWeaponData(data.JetWeapons[weapon]) : null;

    /// <summary><c>GetWalkerWeaponRating</c>/<c>GetJetWeaponRating</c> (0x0044f030, 0x0044f300): 0 without the weapon.</summary>
    private static int WeaponRating(WeaponData? weapon, int rating) => weapon?.Rating[rating] ?? 0;

    /// <summary><c>GetWalkerWeaponName</c> (0x0044f530).</summary>
    private string WalkerWeaponName(Text.FrontEndTexts texts, int n, int weapon) => WeaponName(texts, WalkerWeapon(n, weapon));

    /// <summary><c>GetJetWeaponName</c> (0x0044f830).</summary>
    private string JetWeaponName(Text.FrontEndTexts texts, int n, int weapon) => WeaponName(texts, JetWeapon(n, weapon));

    private static string WeaponName(Text.FrontEndTexts texts, WeaponData? weapon) =>
        weapon is not null ? texts.ById(unchecked((uint)weapon.LanguageName)) : "Unknown Weapon";

    /// <summary><c>CFEPBEConfig::TransitionNotification</c> (0x004519c0): the first configuration, or the second for world 710 when it has more than three.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        StartTime = frontEnd.Clock.GetSysTimeFloat();
        Config = 0;
        if (frontEnd.SelectedLevel == 710 && NumConfigs > 3) Config = 1;
    }
}
