// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class FrontEndInitTests
{
    private sealed class CountingPage : FrontEndPageBase
    {
        public List<string> Calls { get; } = [];
        public override void TransitionNotification(FrontEndPage from) => Calls.Add($"T({from})");
        public override void ActiveNotification(FrontEndPage from) => Calls.Add($"A({from})");
        public override void DeActiveNotification() => Calls.Add("D");
        public override void Shutdown() => Calls.Add("S");
    }

    private sealed class Rig
    {
        public FrontEnd FrontEnd { get; } = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        public LevelSelectPage LevelSelect { get; }
        public Dictionary<FrontEndPage, CountingPage> Pages { get; } = [];

        public Rig()
        {
            FrontEnd.Career.Blank();
            LevelSelect = new LevelSelectPage(FrontEnd);
            FrontEnd[FrontEndPage.LevelSelect] = LevelSelect;
            foreach (FrontEndPage page in new[] { FrontEndPage.Intro, FrontEndPage.Main, FrontEndPage.SaveGame, FrontEndPage.Debriefing, FrontEndPage.Goodies, FrontEndPage.Multiplayer })
            {
                Pages[page] = new CountingPage();
                FrontEnd[page] = Pages[page];
            }
        }
    }

    [Fact]
    public void TheFirstStartEntersTheTitlePageTwiceOverAndResetsTheSelectedLevel()
    {
        Rig r = new();
        Assert.Equal(-1, r.FrontEnd.SelectedLevel);
        Assert.True(r.FrontEnd.Init(FrontEndEntry.Start));
        Assert.Equal(FrontEndPage.Intro, r.FrontEnd.ActivePage);
        // SetPage from the unknown page, then Init's own notifications from the common page.
        Assert.Equal(["T(Unknown)", "A(Unknown)", "T(Common)", "A(Common)"], r.Pages[FrontEndPage.Intro].Calls);
        Assert.False(r.FrontEnd.FirstRun);
        Assert.Equal(100, r.FrontEnd.SelectedLevel);
        Assert.True(r.FrontEnd.SupervisorMode);
        Assert.All(r.FrontEnd.Controllers, Assert.NotNull);
    }

    [Fact]
    public void AfterAWonLevelTheCareerUpdatesAndTheAutosaveComesBeforeTheDebriefing()
    {
        Rig r = new();
        r.FrontEnd.FirstRun = false;
        r.FrontEnd.AutoSave = AutoSaveMode.Normal;
        r.FrontEnd.SelectedLevel = 110;
        r.FrontEnd.EndLevelData.WorldFinished = 100;
        r.FrontEnd.EndLevelData.FinalState = EndLevelState.Won;
        r.FrontEnd.EndLevelData.Ranking = 0.5f;
        Assert.True(r.FrontEnd.Init(FrontEndEntry.Start));
        Assert.Equal(1, r.FrontEnd.Career.Record.NodeCompleteFlag(0));
        Assert.Equal(FrontEndPage.SaveGame, r.FrontEnd.ActivePage);
        Assert.True(r.FrontEnd.SaveMode);
        Assert.Equal((FrontEndPage.Debriefing, 50), (r.FrontEnd.SuccessPage, r.FrontEnd.SuccessTransitionTime));
        Assert.Equal(["T(Unknown)", "A(Unknown)", "T(Common)", "A(Common)"], r.Pages[FrontEndPage.SaveGame].Calls);
        // The level select initialised on the level just played, before the selected level went back to 100.
        Assert.Equal((1, 0), (r.LevelSelect.Column, r.LevelSelect.Row));
        Assert.Equal(100, r.FrontEnd.SelectedLevel);
    }

    [Fact]
    public void APretendAutosaveOrAllCheatsGoStraightToTheDebriefing()
    {
        foreach ((AutoSaveMode mode, bool cheats) in new[] { (AutoSaveMode.Pretend, false), (AutoSaveMode.Normal, true) })
        {
            Rig r = new();
            r.FrontEnd.FirstRun = false;
            r.FrontEnd.AutoSave = mode;
            r.FrontEnd.AllCheatsEnabled = cheats;
            Assert.True(r.FrontEnd.Init(FrontEndEntry.Start));
            Assert.Equal(FrontEndPage.Debriefing, r.FrontEnd.ActivePage);
            Assert.Equal(["T(Common)", "A(Common)"], r.Pages[FrontEndPage.Debriefing].Calls);
        }
    }

    [Fact]
    public void TheAttractAndTitleEntriesAndTheOtherLevelRanges()
    {
        Rig r = new();
        r.FrontEnd.FirstRun = false;
        r.FrontEnd.Init(FrontEndEntry.FromAttract);
        Assert.Equal(FrontEndPage.Intro, r.FrontEnd.ActivePage);
        r.FrontEnd.Init(FrontEndEntry.TitleScreen);
        Assert.Equal(FrontEndPage.Main, r.FrontEnd.ActivePage);

        r.FrontEnd.SelectedLevel = 903;
        r.FrontEnd.AutoSave = AutoSaveMode.Normal;
        r.FrontEnd.Init(FrontEndEntry.Start);
        Assert.Equal((FrontEndPage.SaveGame, FrontEndPage.Goodies), (r.FrontEnd.ActivePage, r.FrontEnd.SuccessPage));
        r.FrontEnd.SelectedLevel = 860;
        r.FrontEnd.Init(FrontEndEntry.Start);
        Assert.Equal(FrontEndPage.Multiplayer, r.FrontEnd.ActivePage);
    }

    [Fact]
    public void RunEndsWithShutdownAndReturnsTheQuitValue()
    {
        Rig r = new();
        r.FrontEnd[FrontEndPage.Intro] = new IntroPage(r.FrontEnd);
        Assert.Equal(FrontEnd.QuitAttract, r.FrontEnd.Run(FrontEndEntry.Start));
        Assert.All(r.FrontEnd.Controllers, Assert.Null);
        Assert.False(r.FrontEnd.Platform.Window.FrontEndRunning);
        Assert.Equal("S", r.Pages[FrontEndPage.Main].Calls.Single());
        // Once the game has been told to quit, the front end does not start again.
        r.FrontEnd.Quit = FrontEnd.QuitToSystem;
        Assert.Equal(FrontEnd.QuitToSystem, r.FrontEnd.Run(FrontEndEntry.Start));
        Assert.Single(r.Pages[FrontEndPage.Main].Calls);
    }
}
