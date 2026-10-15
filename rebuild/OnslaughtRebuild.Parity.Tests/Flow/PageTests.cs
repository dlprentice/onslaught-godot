// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class PageTests
{
    private sealed class Pages
    {
        public PlatformClock Clock { get; } = FrontEndTests.BootedClock();
        public FrontEnd FrontEnd { get; }
        public IntroPage Intro { get; }
        public MainPage Main { get; }
        public DevSelectPage DevSelect { get; }
        public List<string> Calls { get; } = [];

        private sealed class Keyboard(List<string> calls) : INameKeyboard
        {
            public void SetDefaultName() => calls.Add("SetDefaultName");
        }

        private sealed class LevelSelect(List<string> calls) : ILevelSelect
        {
            public void SyncToSelectedLevel() => calls.Add("SyncToSelectedLevel");
        }

        public Pages()
        {
            FrontEnd = FrontEndTests.NewFrontEnd(Clock);
            // The game blanks or loads the career before the front end runs; the main menu reads world 800's node.
            FrontEnd.Career.Blank();
            Intro = new IntroPage(FrontEnd);
            Main = new MainPage(FrontEnd);
            DevSelect = new DevSelectPage(FrontEnd);
            FrontEnd[FrontEndPage.Intro] = Intro;
            FrontEnd[FrontEndPage.Main] = Main;
            FrontEnd[FrontEndPage.DevSelect] = DevSelect;
            FrontEnd.DevSelect = DevSelect;
            FrontEnd.VirtualKeyboard = new Keyboard(Calls);
            FrontEnd.LevelSelect = new LevelSelect(Calls);
        }

        public void Key(int vk, int scan)
        {
            FrontEnd.Platform.PostTapeKey(vk, scan, down: true);
            FrontEnd.Step();
            FrontEnd.Platform.PostTapeKey(vk, scan, down: false);
            FrontEnd.Step();
        }

        public void RunUntil(Func<bool> done, int limit = 400)
        {
            for (int frame = 0; !done(); frame++)
            {
                Assert.True(frame < limit, "the flow did not get there");
                FrontEnd.Step();
            }
        }
    }

    [Fact]
    public void EnterOnTheTitleGoesToTheMainMenuWithA50Transition()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Intro);
        p.Key(0x0D, Dik.Return);
        Assert.Equal(FrontEndPage.Transition, p.FrontEnd.ActivePage);
        Assert.Equal(FrontEndPage.Main, p.FrontEnd.TransitionTo);
        Assert.Equal(50f, p.FrontEnd.TransitionTime);
        Assert.Contains(FrontEndSound.Select, p.FrontEnd.SoundsPlayed);
    }

    [Fact]
    public void AClickAnywhereOnTheTitleGoesToTheMainMenu()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Intro);
        p.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, 600, 470, 0x1);
        p.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftUp, 600, 470, 0);
        p.FrontEnd.Step();
        Assert.Equal(FrontEndPage.Main, p.FrontEnd.TransitionTo);
    }

    [Fact]
    public void ThirtySecondsIdleOnTheTitleQuitsToTheAttractVideos()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Intro);
        int frames = 0;
        while (p.FrontEnd.Step()) frames++;
        Assert.Equal(FrontEnd.QuitAttract, p.FrontEnd.Quit);
        // Entered at time 0; quits on the first process with more than 30 s elapsed: 901 frames of 1/30 s.
        Assert.Equal(901, frames);
    }

    [Fact]
    public void TheMainMenuSkipsContinueWithoutACareerAndWrapsAtTheEnds()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Main);
        Assert.Equal(0, p.Main.Selected);
        p.Key(0x28, Dik.Down);
        Assert.Equal(2, p.Main.Selected);
        p.Key(0x26, Dik.Up);
        p.Key(0x26, Dik.Up);
        Assert.Equal(-1, p.Main.Selected);
        for (int i = 0; i < 10; i++) p.Key(0x28, Dik.Down);
        Assert.Equal(6, p.Main.Selected);
    }

    [Fact]
    public void NewGameBlanksTheCareerAndOpensTheNameKeyboard()
    {
        Pages p = new();
        p.FrontEnd.Career.Record.CareerInProgress = 0;
        p.FrontEnd.Begin(FrontEndPage.Main);
        p.Key(0x0D, Dik.Return);
        Assert.Equal(["SetDefaultName", "SyncToSelectedLevel"], p.Calls);
        Assert.True(p.FrontEnd.SaveMode);
        Assert.Equal(AutoSaveMode.Not, p.FrontEnd.AutoSave);
        Assert.Equal(FrontEndPage.LevelSelect, p.FrontEnd.SuccessPage);
        Assert.Equal(0, p.FrontEnd.SuccessTransitionTime);
        Assert.Equal(100, p.FrontEnd.SelectedLevel);
        Assert.Equal(0, p.FrontEnd.MemoryCardNumber);
        Assert.Equal(FrontEndPage.VirtualKeyboard, p.FrontEnd.TransitionTo);
        Assert.Equal(70f, p.FrontEnd.TransitionTime);
        Assert.Equal(100, p.FrontEnd.Career.Record.NodeWorldNumber(0));
    }

    [Fact]
    public void WithACareerInProgressNewGameAsksFirst()
    {
        Pages p = new();
        p.FrontEnd.Career.Blank();
        p.FrontEnd.Career.Record.CareerInProgress = 1;
        p.FrontEnd.Begin(FrontEndPage.Main);
        // Entering preselects Continue.
        Assert.Equal(1, p.Main.Selected);
        p.Key(0x26, Dik.Up);
        p.Key(0x0D, Dik.Return);
        Assert.True(p.FrontEnd.MessageBox.BeingDisplayed);
        Assert.Equal(MessageBoxQuestion.NewCareer, p.FrontEnd.MessageBox.Question);
        Assert.Empty(p.Calls);
        p.RunUntil(() => p.FrontEnd.MessageBox.FadeState == 1);
        p.Key(0x26, Dik.Up);
        p.Key(0x0D, Dik.Return);
        p.FrontEnd.Step();
        Assert.Equal(["SetDefaultName", "SyncToSelectedLevel"], p.Calls);
        Assert.Equal(0, p.FrontEnd.Career.Record.CareerInProgress);
    }

    [Fact]
    public void QuitAsksAndYesQuitsTheFrontEnd()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Main);
        for (int i = 0; i < 10; i++) p.Key(0x28, Dik.Down);
        p.Key(0x0D, Dik.Return);
        Assert.Equal(MessageBoxQuestion.Quit, p.FrontEnd.MessageBox.Question);
        p.RunUntil(() => p.FrontEnd.MessageBox.FadeState == 1);
        p.FrontEnd.Platform.PostTapeKey(0x26, Dik.Up, down: true);
        p.FrontEnd.Step();
        p.FrontEnd.Platform.PostTapeKey(0x0D, Dik.Return, down: true);
        Assert.False(p.FrontEnd.Step());
        Assert.Equal(FrontEnd.QuitToSystem, p.FrontEnd.Quit);
    }

    [Fact]
    public void QuitAnsweredNoFadesTheBoxAndStaysOnTheMenu()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Main);
        for (int i = 0; i < 10; i++) p.Key(0x28, Dik.Down);
        p.Key(0x0D, Dik.Return);
        p.RunUntil(() => p.FrontEnd.MessageBox.FadeState == 1);
        p.Key(0x0D, Dik.Return);
        p.RunUntil(() => !p.FrontEnd.MessageBox.BeingDisplayed);
        Assert.Equal(FrontEnd.QuitRunning, p.FrontEnd.Quit);
        Assert.Equal(FrontEndPage.Main, p.FrontEnd.ActivePage);
    }

    [Fact]
    public void ChangingLanguageSavesOnceWhenItsBoxIsShown()
    {
        Pages p = new();
        int saves = 0;
        p.Main.SaveAfterLanguageChange = () => saves++;
        p.FrontEnd.Begin(FrontEndPage.Main);
        p.Key(0x26, Dik.Up);
        Assert.Equal(-1, p.Main.Selected);
        p.Key(0x27, Dik.Right);
        Assert.Equal(1, p.FrontEnd.Language);
        Assert.False(p.FrontEnd.MessageBox.BeingDisplayed);
        p.Key(0x28, Dik.Down);
        p.RunUntil(() => !p.FrontEnd.MessageBox.BeingDisplayed && saves > 0);
        Assert.Equal(1, saves);
        Assert.Equal(1, p.Main.LoadedSoundLanguage);
    }

    [Fact]
    public void OnceTheGameIsCompleteTheMenuDrawsFromTheMainThreadsRand()
    {
        Pages p = new();
        p.FrontEnd.Career.Record.SetNodeComplete(p.FrontEnd.Career.GetNodeFromWorldNo(800), 1);
        CrtRand crt = new();
        p.FrontEnd.Crt = crt;
        p.FrontEnd.Begin(FrontEndPage.Main);
        // From a seed of 1, rand() gives 41, 18467, 6334, then 26500: the fourth is a multiple of 50 and launches a
        // firework, whose particle draws are not modelled yet.
        for (int frame = 0; frame < 3; frame++) p.FrontEnd.Step();
        Assert.Equal(3, crt.MainThread.Calls);
        Assert.Throws<NotSupportedException>(() => p.FrontEnd.Step());
        Assert.Equal(6, crt.MainThread.Calls);
    }

    [Fact]
    public void TheDevicePageChoosesThePcFolderAndGoesOnByItself()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Main);
        Assert.Equal((1, 0, 0), (p.DevSelect.State, p.DevSelect.Cursor, p.DevSelect.Card));
        p.FrontEnd.SaveMode = false;
        p.FrontEnd.SetPage(FrontEndPage.DevSelect, 0);
        p.FrontEnd.Step();
        Assert.Equal(FrontEndPage.Directory, p.FrontEnd.TransitionTo);
    }

    [Fact]
    public void PlayingWithoutSavingTurnsAutosaveToPretend()
    {
        Pages p = new();
        p.FrontEnd.Begin(FrontEndPage.Main);
        p.FrontEnd.SaveMode = true;
        p.FrontEnd.SetSuccessPage(FrontEndPage.LevelSelect, 0);
        p.DevSelect.ButtonPressed(FrontEndButtons.MenuSkip, 1f);
        Assert.Equal(MessageBoxQuestion.NoAutoSave, p.FrontEnd.MessageBox.Question);
        p.RunUntil(() => p.FrontEnd.MessageBox.FadeState == 1);
        p.FrontEnd.MessageBox.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        p.FrontEnd.MessageBox.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        p.DevSelect.Process(FrontEndPageState.TransitioningTo);
        Assert.Equal(AutoSaveMode.Pretend, p.FrontEnd.AutoSave);
        Assert.Equal(1, p.FrontEnd.Career.Record.CareerInProgress);
        Assert.Equal(FrontEndPage.LevelSelect, p.FrontEnd.ActivePage);
    }
}
