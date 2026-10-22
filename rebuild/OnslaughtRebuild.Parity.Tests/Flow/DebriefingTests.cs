// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class DebriefingTests
{
    private sealed class Rig
    {
        public FrontEnd FrontEnd { get; } = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        public DebriefingPage Page { get; }

        // A start after a level, with a pretend autosave so the debriefing comes straight up.
        public Rig(bool wonLevel100, float unshownX = 0f)
        {
            FrontEnd.Career.Blank();
            FrontEnd.FirstRun = false;
            FrontEnd.AutoSave = AutoSaveMode.Pretend;
            FrontEndParticleTests.ConfigureMinimal(FrontEnd);
            if (wonLevel100)
            {
                FrontEnd.EndLevelData.WorldFinished = 100;
                FrontEnd.EndLevelData.FinalState = EndLevelState.Won;
                FrontEnd.EndLevelData.Ranking = 0.9f;
            }
            // The level and its loading screens presented before the front end starts again.
            for (int i = 0; i < 300; i++) FrontEnd.Clock.Present();
            Page = new DebriefingPage(FrontEnd) { UnshownPositionX = unshownX };
            FrontEnd[FrontEndPage.Debriefing] = Page;
            Assert.True(FrontEnd.Init(FrontEndEntry.Start));
            Assert.Equal(FrontEndPage.Debriefing, FrontEnd.ActivePage);
        }
    }

    [Fact]
    public void WithNoNewGoodiesThePromptFadesInAndSelectWaitsForHalfOfIt()
    {
        Rig r = new(wonLevel100: false);
        Assert.Equal((0, 0.001f), (r.Page.NumGoodies, r.Page.Fade));
        int frames = 0;
        while (r.Page.Fade <= 0.5f)
        {
            r.Page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
            Assert.Equal(FrontEndPage.Debriefing, r.FrontEnd.ActivePage);
            r.FrontEnd.Step();
            frames++;
        }
        // 0.001 + 25 steps of 0.02.
        Assert.Equal(25, frames);
        r.Page.ButtonPressed(FrontEndButtons.MenuBack, 1f);
        Assert.Equal((FrontEndPage.LevelSelect, 30f), (r.FrontEnd.TransitionTo, r.FrontEnd.TransitionTime));
    }

    [Fact]
    public void AClickAnywhereContinuesOnceThePromptIsIn()
    {
        Rig r = new(wonLevel100: false);
        for (int i = 0; i < 30; i++) r.FrontEnd.Step();
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.Move, 300, 200, 0);
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, 300, 200, 0x1);
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftUp, 300, 200, 0);
        r.FrontEnd.Step();
        Assert.Equal(FrontEndPage.LevelSelect, r.FrontEnd.TransitionTo);
    }

    [Fact]
    public void WinningLevel100ShowsItsNewGoodiesHalfASecondApart()
    {
        Rig r = new(wonLevel100: true);
        int goodies = r.Page.NumGoodies;
        Assert.InRange(goodies, 1, 99);
        Assert.Equal(0f, r.Page.Fade);
        r.FrontEnd.Step();
        Assert.Equal(1, r.Page.NumShown);
        Assert.Equal("Goodie Effect", r.FrontEnd.Particles!.Manager.Particles!.PD!.Name);
        Assert.Equal(-3f, r.Page.PositionX(0));
        // The next appears on the first frame more than half a second (on the replay clock) after the last.
        for (int i = 0; i < 15; i++) r.FrontEnd.Step();
        Assert.Equal(1, r.Page.NumShown);
        r.FrontEnd.Step();
        Assert.Equal(Math.Min(goodies, 2), r.Page.NumShown);
    }

    [Fact]
    public void TheLastGoodiesUninitialisedPositionDecidesWhenThePromptStarts()
    {
        // A blank career that wins level 100 has five new goodies.
        Rig zero = new(wonLevel100: true);
        Assert.Equal(5, zero.Page.NumGoodies);
        // With zeroed memory the fifth goodie, not yet shown, is past 0 after the first frame's move.
        zero.FrontEnd.Step();
        Assert.NotEqual(0f, zero.Page.Fade);

        // With memory far below zero the prompt waits for the fifth goodie. The first goodie ever explains itself in a
        // box, which holds the goodies still until it is answered; answering at once, the fifth is past 0 on its 31st
        // active frame after it appears.
        Rig low = new(wonLevel100: true, unshownX: -1000f);
        int frames = 0, shownAt = -1, boxes = 0;
        while (low.Page.Fade == 0f)
        {
            Assert.True(frames < 2000, "the prompt never started");
            FrontEndMessageBox box = low.FrontEnd.MessageBox;
            if (box.BeingDisplayed && box.FadeState == 1)
            {
                box.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
                boxes++;
            }
            bool active = !box.BeingDisplayed;
            low.FrontEnd.Step();
            if (active) frames++;
            if (low.Page.NumShown == 5 && shownAt < 0) shownAt = frames;
        }
        Assert.Equal(1, boxes);
        Assert.Equal(shownAt + 31, frames);
    }
}
