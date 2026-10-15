// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Text;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The front end against retail, Present for Present: C's click tape from the title page to level 100 (fe-explore4),
/// replayed into the flow from the Present at which retail's CFrontEnd::Init starts.
/// </summary>
public sealed class RetailChainTests
{
    private sealed class NoVideo : IFrontEndVideo
    {
        public bool Exists(string path) => false;
        public void Open(string path, int width, int height) { }
    }

    private static byte[] Inflated(string gamePath)
    {
        (string name, byte[] bytes) = GameData.Read(gamePath);
        MemBuffer file = MemBuffer.FromFile(name, bytes);
        byte[] data = new byte[file.Length];
        file.Read(data);
        return data;
    }

    // fe-explore4's mouse events: (completed Presents when retail's Process handles it, message, x, y, wParam).
    private static readonly (int Present, MouseMessage Message, int X, int Y, int WParam)[] Tape =
    [
        (2590, MouseMessage.Move, 320, 240, 0), (2592, MouseMessage.LeftDown, 320, 240, 1), (2596, MouseMessage.LeftUp, 320, 240, 0),
        (2698, MouseMessage.Move, 219, 304, 0), (2700, MouseMessage.LeftDown, 219, 304, 1), (2704, MouseMessage.LeftUp, 219, 304, 0),
        (2798, MouseMessage.Move, 617, 454, 0), (2800, MouseMessage.LeftDown, 617, 454, 1), (2804, MouseMessage.LeftUp, 617, 454, 0),
        (2898, MouseMessage.Move, 150, 295, 0), (2900, MouseMessage.LeftDown, 150, 295, 1), (2904, MouseMessage.LeftUp, 150, 295, 0),
        (3098, MouseMessage.Move, 617, 454, 0), (3100, MouseMessage.LeftDown, 617, 454, 1), (3104, MouseMessage.LeftUp, 617, 454, 0),
        (3298, MouseMessage.Move, 617, 454, 0), (3300, MouseMessage.LeftDown, 617, 454, 1), (3304, MouseMessage.LeftUp, 617, 454, 0),
        (3498, MouseMessage.Move, 617, 454, 0), (3500, MouseMessage.LeftDown, 617, 454, 1), (3504, MouseMessage.LeftUp, 617, 454, 0),
    ];

    [GameDataFact]
    public void TheTitleToLevel100ClickChainSetsEachPageAtRetailsPresent()
    {
        // The boot before the front end is retail's observed schedule: CFrontEnd::Init starts at 2287 completed Presents,
        // the first GetSysTimeFloat having come after Present 1.
        PlatformClock clock = new();
        clock.Present();
        clock.GetSysTimeFloat();
        while (clock.PresentCount < 2287) clock.Present();

        PlatformInput input = new(new PlatformWindow());
        ControllerContext controls = new(ControlBindings.Defaults(), input, new ControllerShared(), new FrontEndTests.NoGame(), clock.GetSysTimeFloat);
        FrontEnd frontEnd = new(clock, new FrontEndTiming(), controls);
        frontEnd.Career.Blank();
        frontEnd.ReadDataFile = path => GameData.Read(path).Bytes;
        frontEnd.CurrentOptions = () => CareerOptions.Read(new byte[CareerOptions.Size]);
        frontEnd.Video = new NoVideo();
        (string bedName, byte[] bedBytes) = GameData.Read(@"data\battle engine configurations.dat");
        BattleEngineDataManager engines = BattleEngineDataManager.Load(MemBuffer.FromFile(bedName, bedBytes));
        frontEnd.CockpitOf = name => engines.GetConfiguration(name)?.CockpitText;
        frontEnd.SmallFont = new BitmapFont(Inflated(@"data\resources\textures\mustbe_Font13PS.tga(0)A8R8G8B8.aya"), 16, charSwapHack: false);
        frontEnd.Texts = new FrontEndTexts(
            TextDatabase.Load(GameData.Read(TextDatabase.FileName(0, americanText: false)).Bytes),
            TextIds.Read(GameData.Read(TextIds.FileName).Bytes), input);

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
        frontEnd.DevSelect = devSelect;
        frontEnd.VirtualKeyboard = keyboard;
        frontEnd.Directory = directory;
        frontEnd.LevelSelect = levelSelect;
        frontEnd.LoadGame = loadGame;

        List<(long Present, FrontEndPage Page, int Time)> pages = [];
        frontEnd.SetPageTrace = (page, time) => pages.Add((clock.PresentCount, page, time));

        Assert.True(frontEnd.Init(FrontEndEntry.Start));
        input.Window.FrontEndRunning = true;
        int next = 0;
        long lastProcess;
        while (true)
        {
            lastProcess = clock.PresentCount;
            Assert.True(lastProcess < 4000, "the front end never returned");
            for (; next < Tape.Length && Tape[next].Present == lastProcess; next++)
                input.PostMouseMessage(Tape[next].Message, Tape[next].X, Tape[next].Y, Tape[next].WParam);
            if (!frontEnd.Step()) break;
        }

        // C's breakpoints on retail CFrontEnd::SetPage (0x00466ae0) and the last CFrontEnd::Process (0x00466ba0).
        Assert.Equal(
        [
            (2287L, FrontEndPage.Intro, 0),
            (2596L, FrontEndPage.Main, 50),
            (2704L, FrontEndPage.VirtualKeyboard, 70),
            (2804L, FrontEndPage.SaveGame, 20),
            (2817L, FrontEndPage.LevelSelect, 0),
            (3104L, FrontEndPage.Briefing, 30),
            (3304L, FrontEndPage.BeConfig, 25),
        ], pages);
        Assert.Equal(3504, lastProcess);
        Assert.Equal(100, frontEnd.Quit);
        Assert.Equal("BEA 1", frontEnd.SaveGameName);
        Assert.Equal(CareerSaveGame.Size, frontEnd.MemoryCard.Folder.Read("BEA 1").Length);
    }
}
