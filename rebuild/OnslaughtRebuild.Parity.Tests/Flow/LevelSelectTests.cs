// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class LevelSelectTests
{
    private static (FrontEnd FrontEnd, LevelSelectPage Page) NewLevelSelect(string saveName = "BEA 1")
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        frontEnd.Career.Blank();
        frontEnd.SaveGameName = saveName;
        LevelSelectPage page = new(frontEnd);
        frontEnd[FrontEndPage.LevelSelect] = page;
        frontEnd.LevelSelect = page;
        frontEnd.Begin(FrontEndPage.LevelSelect);
        return (frontEnd, page);
    }

    private static void Click(FrontEnd frontEnd, int x, int y)
    {
        frontEnd.Platform.PostMouseMessage(MouseMessage.Move, x, y, 0);
        frontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, x, y, 0x1);
        frontEnd.Platform.PostMouseMessage(MouseMessage.LeftUp, x, y, 0);
        frontEnd.Step();
    }

    [Fact]
    public void TheGridHasAColumnPerEpisodeStartAndBranch()
    {
        (_, LevelSelectPage page) = NewLevelSelect();
        Assert.Equal(26, page.NumColumns);
        Assert.Equal((1, 100), (page.ColumnLength(0), page.Level(0, 0)));
        Assert.Equal((1, 110), (page.ColumnLength(1), page.Level(1, 0)));
        Assert.Equal([521, 522, 523, 524], Enumerable.Range(0, page.ColumnLength(16)).Select(r => page.Level(16, r)));
        Assert.Equal(800, page.Level(25, 0));
        // The cursor starts on the selected level, 100.
        Assert.Equal((0, 0, 0f), (page.Column, page.Row, page.Pos));
    }

    [Fact]
    public void OnlyWorld100IsOpenOnABlankCareerAndSelectGoesToItsBriefing()
    {
        (FrontEnd frontEnd, LevelSelectPage page) = NewLevelSelect();
        Assert.True(page.IsWorldUnlocked(100));
        Assert.False(page.IsWorldUnlocked(110));
        page.ButtonPressed(FrontEndButtons.MenuRight, 1f);
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.NotEqual(FrontEndPage.Transition, frontEnd.ActivePage);
        page.ButtonPressed(FrontEndButtons.MenuLeft, 1f);
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal((FrontEndPage.Briefing, 30f), (frontEnd.TransitionTo, frontEnd.TransitionTime));
        Assert.Equal(100, frontEnd.SelectedLevel);
    }

    [Fact]
    public void ACompletedLinkOpensItsWorldAndTurkeyOpensEverything()
    {
        (FrontEnd frontEnd, LevelSelectPage page) = NewLevelSelect();
        OnslaughtRebuild.Parity.Career.Career career = frontEnd.Career;
        int link = career.ParentLinks(career.GetNodeFromWorldNo(110)).Single();
        career.Record.SetLinkType(link, CareerLinkType.Complete);
        Assert.True(page.IsWorldUnlocked(110));
        Assert.False(page.IsWorldUnlocked(200));
        career.Record.SetLinkType(link, CareerLinkType.CompleteBroken);
        Assert.False(page.IsWorldUnlocked(110));

        (_, LevelSelectPage cheat) = NewLevelSelect("Mr TURKEY");
        Assert.True(cheat.IsWorldUnlocked(800));
    }

    [Fact]
    public void ClickingWorld100SelectsIt()
    {
        (FrontEnd frontEnd, _) = NewLevelSelect();
        // Column 0 sits at x 148 and a one-row column at y 320; the hit box is 20 either side.
        Click(frontEnd, 148, 320);
        Assert.Equal(FrontEndPage.Briefing, frontEnd.TransitionTo);
        Assert.Equal(100, frontEnd.SelectedLevel);
    }

    [Fact]
    public void ClickingALockedWorldDoesNothing()
    {
        (FrontEnd frontEnd, _) = NewLevelSelect();
        Click(frontEnd, 208, 320);
        Assert.Equal(FrontEndPage.LevelSelect, frontEnd.ActivePage);
    }

    [Fact]
    public void HoveringMovesTheCursorToAnOpenWorld()
    {
        (FrontEnd frontEnd, LevelSelectPage page) = NewLevelSelect("TURKEY");
        // Column 3 (211, 212) is at x 148 + 180; its two rows at y 350 and 290.
        frontEnd.Platform.PostMouseMessage(MouseMessage.Move, 328, 290, 0);
        frontEnd.Step();
        Assert.Equal((3, 1, 212), (page.Column, page.Row, page.Level(3, 1)));
    }

    [Fact]
    public void TheHelpArrowsSelectAndGoBack()
    {
        (FrontEnd frontEnd, _) = NewLevelSelect();
        Click(frontEnd, 618, 450);
        Assert.Equal(FrontEndPage.Briefing, frontEnd.TransitionTo);

        (frontEnd, _) = NewLevelSelect();
        Click(frontEnd, 22, 450);
        Assert.Equal((FrontEndPage.Main, 70f), (frontEnd.TransitionTo, frontEnd.TransitionTime));
    }

    [Fact]
    public void TheMouseAtTheRightEdgeScrollsTheGridAndDragsTheCursorAlong()
    {
        (FrontEnd frontEnd, LevelSelectPage page) = NewLevelSelect();
        frontEnd.Platform.PostMouseMessage(MouseMessage.Move, 639, 100, 0);
        frontEnd.Step();
        // The first step is a sixtieth: 99 cubed times 0.002 times 1/60.
        Assert.Equal((float)(1.0 / 60 * 99 * 99 * 99 * 0.002), page.Pos, 2);
        for (int i = 0; i < 200; i++) frontEnd.Step();
        Assert.Equal(1100f, page.Pos);
        // Column 0 is now far off to the left, so the render moved the cursor to the first column inside the band.
        Assert.True(148f - page.Pos + page.Column * 60f >= 90f);
        Assert.True(148f - page.Pos + (page.Column - 1) * 60f < 90f);
    }

    [Fact]
    public void LeftAndRightAroundTheFourRowColumnRemapTheRowAsRetailDoes()
    {
        (_, LevelSelectPage page) = NewLevelSelect("TURKEY");
        for (int i = 0; i < 15; i++) page.ButtonPressed(FrontEndButtons.MenuRight, 1f);
        page.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        Assert.Equal((15, 1), (page.Column, page.Row));
        // Into column 16 from row 1 lands on row 2; out of it from row 3 lands on row 1, even in a one-row column:
        // only the reads (ClampedRow) and up/down clamp the row to the column.
        page.ButtonPressed(FrontEndButtons.MenuRight, 1f);
        Assert.Equal((16, 2), (page.Column, page.Row));
        page.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        page.ButtonPressed(FrontEndButtons.MenuRight, 1f);
        Assert.Equal((17, 1, 1), (page.Column, page.Row, page.ColumnLength(17)));
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal(600, page.Level(17, 0));
    }
}
