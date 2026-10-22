// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class FrontEndMessageBoxTests
{
    [Fact]
    public void ABoxWithoutButtonsFadesInWaitsItsTimeoutThenFadesOut()
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        frontEnd.Begin(FrontEndPage.Main);
        FrontEndMessageBox box = frontEnd.MessageBox;
        box.Create(timeout: 10, fadeTime: 5, closesOnProgress: false, MessageBoxOption.None, MessageBoxQuestion.Language);
        Assert.True(box.BeingDisplayed);
        Assert.False(box.IsInteractive);
        List<int> states = [];
        while (box.BeingDisplayed)
        {
            states.Add(box.FadeState);
            frontEnd.Step();
            Assert.True(states.Count < 200);
        }
        Assert.Equal(0, box.FadeState);
        // Fading in and waiting advance by 50 x the frame step; fading out by one per frame, so it takes exactly 5.
        Assert.Equal(states.OrderBy(state => state), states);
        Assert.Equal(5, states.Count(state => state == 2));
        Assert.Contains(1, states);
    }

    [Fact]
    public void YesNoTakesTheCursorAndTheChoiceResetsWhenRead()
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        frontEnd.Begin(FrontEndPage.Main);
        FrontEndMessageBox box = frontEnd.MessageBox;
        box.Create(0, 0, false, MessageBoxOption.YesNo, MessageBoxQuestion.Quit);
        Assert.Equal(MessageBoxQuestion.No, box.Cursor);
        box.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        box.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal(2, box.FadeState);
        Assert.Equal(MessageBoxQuestion.Yes, box.GetFinalChoice());
        Assert.Equal(-1, box.GetFinalChoice());
    }

    [Fact]
    public void OkGoesToItsDismissPage()
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        frontEnd.Begin(FrontEndPage.Main);
        FrontEndMessageBox box = frontEnd.MessageBox;
        box.Create(0, 0, false, MessageBoxOption.Ok, 0);
        box.SetOnDismiss(FrontEndPage.Intro, 0);
        box.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal(FrontEndPage.Intro, frontEnd.ActivePage);
        Assert.Equal(FrontEndPage.None, box.OnDismissPage);
    }

    [Fact]
    public void ASecondBoxIsIgnoredWhileOneIsShown()
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        FrontEndMessageBox box = frontEnd.MessageBox;
        box.Create(0, 0, false, MessageBoxOption.YesNo, MessageBoxQuestion.Quit);
        box.Create(0, 0, false, MessageBoxOption.Ok, MessageBoxQuestion.Language);
        Assert.Equal(MessageBoxQuestion.Quit, box.Question);
        Assert.Equal(MessageBoxOption.YesNo, box.Option);
    }
}
