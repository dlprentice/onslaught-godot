// SPDX-License-Identifier: GPL-3.0-or-later
// The front end's construction as WinMain and CFrontEnd's resources make it (bea-decomp src/FrontEnd.cpp, src/Career.cpp):
// the shipped options, the fonts, the texts and every page a player reaches on the way to a level.

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Text;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>The front end's controllers' view of the game while no level runs: not paused, single player, no debug buttons.</summary>
public sealed class FrontEndGame : IControllerGame
{
    public bool IsPaused => false;

    public bool IsMultiplayer => false;

    public void ReceiveButtonAction(Controller from, int button, float value)
    {
    }
}

/// <summary>A front end without films: no level briefing video exists.</summary>
public sealed class NoFrontEndVideo : IFrontEndVideo
{
    public bool Exists(string path) => false;

    public void Open(string path, int width, int height)
    {
    }
}

/// <summary>Makes the front end on a clock and an input, from the game's files.</summary>
public static class FrontEndSetup
{
    /// <summary>
    /// The front end with every page a player reaches on the way to a level (intro, main, developer select, virtual
    /// keyboard, directory, save, load, level select, briefing, battle-engine configuration, debriefing). WinMain's boot loads the
    /// shipped defaultoptions.bea into <paramref name="bindings"/> and the options (the career is wiped);
    /// <paramref name="options"/> is what it read, or null without the file. <paramref name="pcText"/> gives the PC texts
    /// (Localization_GetStringById), which come from the game's program.
    /// </summary>
    public static FrontEnd Create(PlatformClock clock, ControllerContext controls, ControlBindings bindings,
        Func<string, (string Name, byte[] Bytes)> read, Func<int, string>? pcText, out CareerOptions? options,
        IFrontEndVideo? video = null)
    {
        FrontEnd frontEnd = new(clock, new FrontEndTiming(), controls);
        CareerOptions? loaded = null;
        try
        {
            loaded = DefaultOptions.Load(frontEnd.Career, bindings, read(DefaultOptions.FileName).Bytes);
        }
        catch (FileNotFoundException)
        {
        }

        options = loaded;
        frontEnd.ReadDataFile = path => read(path).Bytes;
        frontEnd.CurrentOptions = () => loaded ?? CareerOptions.Read(new byte[CareerOptions.Size]);
        frontEnd.Video = video ?? new NoFrontEndVideo();
        (string bedName, byte[] bedBytes) = read(@"data\battle engine configurations.dat");
        BattleEngineDataManager engines = BattleEngineDataManager.Load(MemBuffer.FromFile(bedName, bedBytes));
        frontEnd.CockpitOf = name => engines.GetConfiguration(name)?.CockpitText;
        frontEnd.SmallFont = new BitmapFont(Inflated(read, @"data\resources\textures\mustbe_Font13PS.tga(0)A8R8G8B8.aya"), 16, charSwapHack: false);
        frontEnd.NormalFont = new BitmapFont(Inflated(read, @"data\resources\textures\mustbe_font22.512.tga(0)A8R8G8B8.aya"), 32, charSwapHack: false);
        frontEnd.Texts = new FrontEndTexts(
            TextDatabase.Load(read(TextDatabase.FileName(0, americanText: false)).Bytes),
            TextIds.Read(read(TextIds.FileName).Bytes), controls.Input, pcText: pcText);

        DevSelectPage devSelect = new(frontEnd);
        VirtualKeyboardPage keyboard = new(frontEnd);
        DirectoryPage directory = new(frontEnd);
        LevelSelectPage levelSelect = new(frontEnd);
        LoadGamePage loadGame = new(frontEnd);
        frontEnd[FrontEndPage.Intro] = new IntroPage(frontEnd);
        frontEnd[FrontEndPage.Main] = new MainPage(frontEnd);
        frontEnd[FrontEndPage.DevSelect] = devSelect;
        frontEnd[FrontEndPage.VirtualKeyboard] = keyboard;
        frontEnd[FrontEndPage.Directory] = directory;
        frontEnd[FrontEndPage.SaveGame] = new SaveGamePage(frontEnd);
        frontEnd[FrontEndPage.LoadGame] = loadGame;
        frontEnd[FrontEndPage.LevelSelect] = levelSelect;
        frontEnd[FrontEndPage.Briefing] = new BriefingPage(frontEnd);
        frontEnd[FrontEndPage.BeConfig] = new BeConfigPage(frontEnd);
        frontEnd[FrontEndPage.Debriefing] = new DebriefingPage(frontEnd);
        frontEnd.DevSelect = devSelect;
        frontEnd.VirtualKeyboard = keyboard;
        frontEnd.Directory = directory;
        frontEnd.LevelSelect = levelSelect;
        frontEnd.LoadGame = loadGame;
        return frontEnd;
    }

    private static byte[] Inflated(Func<string, (string Name, byte[] Bytes)> read, string gamePath)
    {
        (string name, byte[] bytes) = read(gamePath);
        MemBuffer file = MemBuffer.FromFile(name, bytes);
        byte[] data = new byte[file.Length];
        file.Read(data);
        return data;
    }
}
