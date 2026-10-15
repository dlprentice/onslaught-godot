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

    /// <summary>The front end with every page the replays reach, on the given clock.</summary>
    private static (FrontEnd FrontEnd, PlatformInput Input) NewFrontEnd(PlatformClock clock)
    {
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
        frontEnd.NormalFont = new BitmapFont(Inflated(@"data\resources\textures\mustbe_font22.512.tga(0)A8R8G8B8.aya"), 32, charSwapHack: false);
        frontEnd.Texts = new FrontEndTexts(
            TextDatabase.Load(GameData.Read(TextDatabase.FileName(0, americanText: false)).Bytes),
            TextIds.Read(GameData.Read(TextIds.FileName).Bytes), input,
            pcText: id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id]);

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

        return (frontEnd, input);
    }

    [GameDataFact]
    public void TheTitleToLevel100ClickChainSetsEachPageAtRetailsPresent()
    {
        // The boot before the front end is retail's observed schedule: CFrontEnd::Init starts at 2287 completed Presents,
        // the first GetSysTimeFloat having come after Present 1.
        PlatformClock clock = new();
        clock.Present();
        clock.GetSysTimeFloat();
        while (clock.PresentCount < 2287) clock.Present();

        (FrontEnd frontEnd, PlatformInput input) = NewFrontEnd(clock);

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

    // quit-path2's mouse events (C's tape on a -skipfmv boot): the title click, Main's Quit item, then Yes.
    private static readonly (int Present, MouseMessage Message, int X, int Y, int WParam)[] QuitTape =
    [
        (300, MouseMessage.Move, 320, 240, 0), (302, MouseMessage.LeftDown, 320, 240, 1), (306, MouseMessage.LeftUp, 320, 240, 0),
        (418, MouseMessage.Move, 217, 424, 0), (420, MouseMessage.LeftDown, 217, 424, 1), (424, MouseMessage.LeftUp, 217, 424, 0),
        (558, MouseMessage.Move, 319, 233, 0), (560, MouseMessage.LeftDown, 319, 233, 1), (564, MouseMessage.LeftUp, 319, 233, 0),
    ];

    /// <summary>
    /// C's quit-path2 against retail (-skipfmv): CFrontEnd::Init at 2 completed Presents (C's qb1 breakpoint), SetPage
    /// Intro at 2 and Main at 306, Main's Quit item opens the Yes/No box at 424, and Yes ends the front end: the last
    /// Process at 565 (C's qp2 breakpoints on SetPage 0x00466ae0 and Process 0x00466ba0, both retail processes equal).
    /// </summary>
    [GameDataFact]
    public void QuitPath2QuitsAtRetailsPresent()
    {
        PlatformClock clock = new();
        clock.Present();
        clock.GetSysTimeFloat();
        while (clock.PresentCount < 2) clock.Present();

        (FrontEnd frontEnd, PlatformInput input) = NewFrontEnd(clock);
        List<(long Present, FrontEndPage Page, int Time)> pages = [];
        frontEnd.SetPageTrace = (page, time) => pages.Add((clock.PresentCount, page, time));

        Assert.True(frontEnd.Init(FrontEndEntry.Start));
        input.Window.FrontEndRunning = true;
        int next = 0;
        long lastProcess;
        long boxShown = -1;
        while (true)
        {
            lastProcess = clock.PresentCount;
            Assert.True(lastProcess < 1000, "the front end never returned");
            for (; next < QuitTape.Length && QuitTape[next].Present == lastProcess; next++)
                input.PostMouseMessage(QuitTape[next].Message, QuitTape[next].X, QuitTape[next].Y, QuitTape[next].WParam);
            bool running = frontEnd.Step();
            if (boxShown < 0 && frontEnd.MessageBox.BeingDisplayed && frontEnd.MessageBox.Option == MessageBoxOption.YesNo)
                boxShown = lastProcess;
            if (!running) break;
        }

        Assert.Equal([(2L, FrontEndPage.Intro, 0), (306L, FrontEndPage.Main, 50)], pages);
        Assert.Equal(424, boxShown);
        Assert.Equal(565, lastProcess);
        Assert.Equal(-1, frontEnd.Quit);
    }

    // load-path2's mouse events (C's tape on a -skipfmv boot): the title click, Main's Load Game item, the directory's
    // first row (BEA 1), then the level select's forward arrow.
    private static readonly (int Present, MouseMessage Message, int X, int Y, int WParam)[] LoadTape =
    [
        (300, MouseMessage.Move, 320, 240, 0), (302, MouseMessage.LeftDown, 320, 240, 1), (306, MouseMessage.LeftUp, 320, 240, 0),
        (418, MouseMessage.Move, 217, 344, 0), (420, MouseMessage.LeftDown, 217, 344, 1), (424, MouseMessage.LeftUp, 217, 344, 0),
        (558, MouseMessage.Move, 165, 146, 0), (560, MouseMessage.LeftDown, 165, 146, 1), (564, MouseMessage.LeftUp, 165, 146, 0),
        (698, MouseMessage.Move, 617, 454, 0), (700, MouseMessage.LeftDown, 617, 454, 1), (704, MouseMessage.LeftUp, 617, 454, 0),
    ];

    /// <summary>
    /// C's load-path2 against retail (-skipfmv, a career saved as BEA 1): SetPage Intro at 2, Main at 306, Directory at
    /// 424, LoadGame at 564, LevelSelect at 565 and Briefing at 704 (C's lp2 breakpoints on SetPage 0x00466ae0; both
    /// retail processes equal; the run was capped at Present 920, so no last Process is asserted). The career here is
    /// the repository's tracked save (retail's file for this run was not kept; the pages need only a career that loads).
    /// </summary>
    [GameDataFact]
    public void LoadPath2SetsEachPageAtRetailsPresent()
    {
        PlatformClock clock = new();
        clock.Present();
        clock.GetSysTimeFloat();
        while (clock.PresentCount < 2) clock.Present();

        (FrontEnd frontEnd, PlatformInput input) = NewFrontEnd(clock);
        frontEnd.MemoryCard.Folder.Write("BEA 1", File.ReadAllBytes(OnslaughtRebuild.Parity.Tests.Career.CareerTests.FixturePath));
        List<(long Present, FrontEndPage Page, int Time)> pages = [];
        frontEnd.SetPageTrace = (page, time) => pages.Add((clock.PresentCount, page, time));

        Assert.True(frontEnd.Init(FrontEndEntry.Start));
        input.Window.FrontEndRunning = true;
        int next = 0;
        while (clock.PresentCount < 920)
        {
            long present = clock.PresentCount;
            for (; next < LoadTape.Length && LoadTape[next].Present == present; next++)
                input.PostMouseMessage(LoadTape[next].Message, LoadTape[next].X, LoadTape[next].Y, LoadTape[next].WParam);
            Assert.True(frontEnd.Step(), $"the front end ended at {present}");
        }

        Assert.Equal(
        [
            (2L, FrontEndPage.Intro, 0),
            (306L, FrontEndPage.Main, 50),
            (424L, FrontEndPage.Directory, 70),
            (564L, FrontEndPage.LoadGame, 0),
            (565L, FrontEndPage.LevelSelect, 0),
            (704L, FrontEndPage.Briefing, 30),
        ], pages);
    }
}
