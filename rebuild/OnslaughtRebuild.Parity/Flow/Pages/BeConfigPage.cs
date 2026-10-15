// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPBEConfig.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The Battle Engine configuration page (<c>CFEPBEConfig</c>): the selected level's configurations, chosen with up and
/// down. Select stores the choice and its cockpit in the game's front-end settings, then goes to the wingmen page (from
/// world 231 on, where wingmen are offered) or leaves the front end to play the level.
/// </summary>
/// <remarks>
/// Not modelled: the auto-configuration test's select; the turning Battle Engine model (<c>CFrontEndThing</c>, drawn
/// only) and the rest of the drawing. Once the page is settled the arrows' click boxes are at x 162.
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
    /// The state-changing part of <c>CFEPBEConfig::Render</c> (0x004505b0): the help arrows, then, with more than one
    /// configuration, the up arrow's click box and, only while the down arrow is drawn (its blink is on, or it is
    /// flashing), the down arrow's.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        // x87 (004505b6..004505ef): fcos of the 24-bit product, negated and less 1 straight from the register (the cosine is
        // not stored to a float first), then times 0.5 and 600, each rounded to 24 bits.
        float slide = (float)X87.Mul(X87.Mul(X87.Sub(-X87.Cos((float)X87.Mul(transition, MathF.PI)), 1f), 0.5f), 600f);
        float offset = dest == FrontEndPage.Wingmen ? -slide : slide;
        float x = (float)X87.Sub(162f, offset);

        if (NumConfigs > 1) frontEnd.RenderHelpArrow(5, transition);
        else if (frontEnd.SelectedLevel >= 231 && NumWingmen != 0) frontEnd.RenderHelpArrow(4, transition);
        else frontEnd.RenderHelpArrow(0, transition);
        frontEnd.RenderHelpArrow(1, transition);

        if (NumConfigs > 1)
        {
            float left = (float)X87.Sub(x, 40f), right = (float)X87.Add(x, 40f);
            frontEnd.ProcessMouseClickInRect(left, 101f, right, 181f, FrontEndButtons.MenuUp);
            // SINT(counter): _ftol truncates.
            // SINT(counter): fistp under /QIfist, to nearest (004517f4).
            if (X87.Fist(frontEnd.Counter) % 64 < 50 || DownFlash > 0f)
                frontEnd.ProcessMouseClickInRect(left, 344f, right, 424f, FrontEndButtons.MenuDown);
        }
    }

    /// <summary><c>CFEPBEConfig::TransitionNotification</c> (0x004519c0): the first configuration, or the second for world 710 when it has more than three.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        StartTime = frontEnd.Clock.GetSysTimeFloat();
        Config = 0;
        if (frontEnd.SelectedLevel == 710 && NumConfigs > 3) Config = 1;
    }
}
