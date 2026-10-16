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
    internal static readonly (int Present, MouseMessage Message, int X, int Y, int WParam)[] ClickTape =
    [
        (2590, MouseMessage.Move, 320, 240, 0), (2592, MouseMessage.LeftDown, 320, 240, 1), (2596, MouseMessage.LeftUp, 320, 240, 0),
        (2698, MouseMessage.Move, 219, 304, 0), (2700, MouseMessage.LeftDown, 219, 304, 1), (2704, MouseMessage.LeftUp, 219, 304, 0),
        (2798, MouseMessage.Move, 617, 454, 0), (2800, MouseMessage.LeftDown, 617, 454, 1), (2804, MouseMessage.LeftUp, 617, 454, 0),
        (2898, MouseMessage.Move, 150, 295, 0), (2900, MouseMessage.LeftDown, 150, 295, 1), (2904, MouseMessage.LeftUp, 150, 295, 0),
        (3098, MouseMessage.Move, 617, 454, 0), (3100, MouseMessage.LeftDown, 617, 454, 1), (3104, MouseMessage.LeftUp, 617, 454, 0),
        (3298, MouseMessage.Move, 617, 454, 0), (3300, MouseMessage.LeftDown, 617, 454, 1), (3304, MouseMessage.LeftUp, 617, 454, 0),
        (3498, MouseMessage.Move, 617, 454, 0), (3500, MouseMessage.LeftDown, 617, 454, 1), (3504, MouseMessage.LeftUp, 617, 454, 0),
    ];

    /// <summary>The films' Bink headers, read from the player's own installation.</summary>
    internal sealed class InstalledFilms : IFilmFiles
    {
        public FilmHeader? Probe(string name)
        {
            byte[] bytes;
            try
            {
                bytes = GameData.Read($@"data\video\{name}.vid").Bytes;
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            return new FilmHeader(BitConverter.ToInt32(bytes, 8), BitConverter.ToInt32(bytes, 28), BitConverter.ToInt32(bytes, 32));
        }
    }

    /// <summary>
    /// The boot up to <c>CFrontEnd::Init</c>: CSystem::Init's films on the harness's pacing (none with -skipfmv), then the
    /// observed Presents before the front end.
    /// </summary>
    internal static PlatformClock Boot(bool skipFmv)
    {
        PlatformClock clock = new();
        clock.GetSysTimeFloat();
        PlatformInput input = new(new PlatformWindow());
        ControllerContext controls = new(ControlBindings.Defaults(), input, new ControllerShared(), new FrontEndTests.NoGame(), clock.GetSysTimeFloat);
        FilmPlayer player = new(clock, input, controls, new InstalledFilms(), new HarnessFilmPacing());
        input.FlushInputBuffers();
        BootFilms.Play(player, new BootFilms.Settings(LevelGiven: false, StraightToDeviceSelect: false, SkipFmv: skipFmv, Attract: false));
        for (int i = 0; i < BootFilms.PresentsAfterFilms; i++) clock.Present();
        return clock;
    }

    /// <summary>The front end with every page the replays reach, on the given clock.</summary>
    private static (FrontEnd FrontEnd, PlatformInput Input) NewFrontEnd(PlatformClock clock)
    {
        PlatformInput input = new(new PlatformWindow());
        ControlBindings bindings = ControlBindings.Defaults();
        ControllerContext controls = new(bindings, input, new ControllerShared(), new FrontEndTests.NoGame(), clock.GetSysTimeFloat);
        FrontEnd frontEnd = FrontEndSetup.Create(clock, controls, bindings, GameData.Read,
            id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id], out _, new NoVideo());
        return (frontEnd, input);
    }

    [GameDataFact]
    public void TheTitleToLevel100ClickChainSetsEachPageAtRetailsPresent()
    {
        // The boot: the films (no -skipfmv), then the two observed Presents; CFrontEnd::Init then starts at 2287 completed
        // Presents, as C measured.
        PlatformClock clock = Boot(skipFmv: false);
        Assert.Equal(2287, clock.PresentCount);

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
            for (; next < ClickTape.Length && ClickTape[next].Present == lastProcess; next++)
                input.PostMouseMessage(ClickTape[next].Message, ClickTape[next].X, ClickTape[next].Y, ClickTape[next].WParam);
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
        byte[] saved = frontEnd.MemoryCard.Folder.Read("BEA 1");
        Assert.Equal(CareerSaveGame.Size, saved.Length);

        // Retail's own save from the same new-career chain (a contributor's -skipfmv run, the autosave at the configuration
        // step): byte for byte, career, bindings and options alike. A retail recording, read where present.
        if (RetailGolden.File("career/after-level-taken-a.bes") is { } retailSave)
        {
            Assert.Equal(File.ReadAllBytes(retailSave), saved);
        }
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
        PlatformClock clock = Boot(skipFmv: true);
        Assert.Equal(2, clock.PresentCount);

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
        PlatformClock clock = Boot(skipFmv: true);
        Assert.Equal(2, clock.PresentCount);

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

    /// <summary>
    /// The boot films against C's retail boot capture (pres1, two retail processes): the logo's Presents are 1..230 and the
    /// opening film's 231..2285 (each film one Present before it, its frames but the last, and one after); the two
    /// observed Presents follow, and the front end starts at 2287.
    /// </summary>
    [GameDataFact]
    public void TheBootFilmsTakeRetailsPresents()
    {
        PlatformClock clock = new();
        PlatformInput input = new(new PlatformWindow());
        ControllerContext controls = new(ControlBindings.Defaults(), input, new ControllerShared(), new FrontEndTests.NoGame(), clock.GetSysTimeFloat);
        FilmPlayer player = new(clock, input, controls, new InstalledFilms(), new HarnessFilmPacing());
        BootFilms.Play(player, new BootFilms.Settings(LevelGiven: false, StraightToDeviceSelect: false, SkipFmv: false, Attract: false));
        Assert.Equal([("ltlogo", 228, false), ("openingfmv", 2053, false)], player.Trace);
        Assert.Equal(2285, clock.PresentCount);
        PlatformClock booted = Boot(skipFmv: false);
        Assert.Equal(2287, booted.PresentCount);
        Assert.Equal(2, Boot(skipFmv: true).PresentCount);
    }

    /// <summary>A mouse click during a film skips it: the frame being drawn is the last, and the film's last Present follows.</summary>
    [GameDataFact]
    public void AClickSkipsAFilm()
    {
        PlatformClock clock = new();
        PlatformInput input = new(new PlatformWindow());
        ControllerContext controls = new(ControlBindings.Defaults(), input, new ControllerShared(), new FrontEndTests.NoGame(), clock.GetSysTimeFloat);
        FilmPlayer player = new(clock, input, controls, new InstalledFilms(), new HarnessFilmPacing());
        input.PostMouseMessage(MouseMessage.LeftDown, 10, 10, 1);
        Assert.True(player.PlayFullscreen("ltlogo", false));
        Assert.Equal(("ltlogo", 1, true), player.Trace[0]);
        Assert.Equal(3, clock.PresentCount);
    }

    /// <summary>The system's parts for a replay: the front end on a step loop, the films through the player.</summary>
    private sealed class ReplayParts(Func<FrontEndEntry, int> frontEnd, FilmPlayer films) : ISystemParts
    {
        public int RunFrontEnd(FrontEndEntry entry) => frontEnd(entry);

        public QuitType RunLevel(int level) => throw new InvalidOperationException("no level in this replay");

        public bool PlayAttractFilm(string name) => films.PlayAttractFilm(name);

        public bool PlayVendorFilm => false;
    }

    /// <summary>
    /// The whole idle flow from boot: the films, the title page with no input until its 30 s timeout (the last Process
    /// at 3188, C's measurement), then CSystem::Run's attract films from the next Present, 3189 as retail (the logo
    /// 3189-3418, the opening film 3419-5473), and the front end entered again from the attract videos.
    /// </summary>
    [GameDataFact]
    public void IdleOnTheTitleThePlayerSeesTheAttractFilmsFromRetailsPresent()
    {
        PlatformClock clock = Boot(skipFmv: false);
        (FrontEnd frontEnd, PlatformInput input) = NewFrontEnd(clock);
        ControllerContext controls = new(ControlBindings.Defaults(), input, new ControllerShared(), new FrontEndTests.NoGame(), clock.GetSysTimeFloat);
        FilmPlayer player = new(clock, input, controls, new InstalledFilms(), new HarnessFilmPacing());
        long lastProcess = 0;
        var entries = new List<(FrontEndEntry Entry, long Present)>();
        int Run(FrontEndEntry entry)
        {
            entries.Add((entry, clock.PresentCount));
            if (entries.Count > 1) return SystemFlow.QuitToSystem;
            Assert.True(frontEnd.Init(entry));
            input.Window.FrontEndRunning = true;
            while (true)
            {
                lastProcess = clock.PresentCount;
                Assert.True(lastProcess < 4000, "the title never timed out");
                if (!frontEnd.Step()) break;
            }

            input.Window.FrontEndRunning = false;
            return frontEnd.Quit;
        }

        var parts = new ReplayParts(Run, player);
        var flow = new SystemFlow(parts);
        Assert.Equal(SystemFlow.QuitToSystem, flow.Run());
        Assert.Equal(3188, lastProcess);
        Assert.Equal(["front end Start", "attract", "front end FromAttract"], flow.Trace);
        Assert.Equal([("ltlogo", 228, false), ("openingfmv", 2053, false)], player.Trace);
        Assert.Equal((FrontEndEntry.FromAttract, 3188L + 230 + 2055), entries[1]);
    }
}
