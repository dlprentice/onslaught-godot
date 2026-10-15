// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

public sealed class RetailCareerLoadFlowTests
{
    [Fact]
    public void NewCareerSeedsFirstCaseSensitiveUnusedNameAndStopsAt4096()
    {
        RetailCareerSave career = ReadGoldCareer();
        var frontend = new RetailFrontendSession([
            new(0, "BEA 1", career), new(1, "bea 2", career), new(2, "BEA 3", career)]);
        frontend.Confirm();
        frontend.Confirm();
        Assert.Equal("BEA 2", frontend.GameName);
        var full = new RetailFrontendSession(Enumerable.Range(1, 4096)
            .Select(index => new RetailCareerDescriptor(index, $"BEA {index}", career)));
        full.Confirm();
        full.Confirm();
        Assert.Equal("BEA 4096", full.GameName);
        Assert.True(full.GameNameIsFresh);
    }

    [Fact]
    public void LoadPageKeepsDefaultResetAndCannotEditCursor()
    {
        var frontend = new RetailFrontendSession([new(0, "BEA 1", ReadGoldCareer())]);
        EnterLoadGame(frontend);
        Assert.Equal("BEA 1", frontend.GameName);
        Assert.False(frontend.MoveGameNameCursor(false));
        Assert.False(frontend.AppendGameNameCharacter('X', 0));
        Assert.False(frontend.RemoveGameNameCharacter());
        Assert.Equal(RetailFrontendSession.DefaultGameName.Length, frontend.GameNameCursor);
    }

    [Fact]
    public void LoadGame_PreservesInjectedCareerOrderAndMovesSelection()
    {
        RetailCareerSave career = ReadGoldCareer();
        RetailCareerDescriptor secondSlot = new(9, "SECOND SLOT", career);
        RetailCareerDescriptor firstSlot = new(2, "FIRST SLOT", career);
        var frontend = new RetailFrontendSession([secondSlot, firstSlot]);

        EnterLoadGame(frontend);

        Assert.Equal(RetailFrontendCareerPageMode.Load, frontend.CareerPageMode);
        Assert.Equal(["SECOND SLOT", "FIRST SLOT"], frontend.CareerNames);
        Assert.Equal([secondSlot, firstSlot], frontend.CareerDescriptors);
        Assert.Equal(-1, frontend.SelectedCareerIndex);
        Assert.True(frontend.MoveNext());
        Assert.Equal(0, frontend.SelectedCareerIndex);
        Assert.True(frontend.MoveNext());
        Assert.Equal(1, frontend.SelectedCareerIndex);
        Assert.False(frontend.MoveNext());
    }

    [Fact]
    public void LoadGame_AcceptHandsOffTheSelectedCareerAndItsSuggestedWorld()
    {
        RetailCareerSave career = ReadGoldCareer();
        RetailCareerDescriptor first = new(2, "FIRST SLOT", career);
        RetailCareerDescriptor selected = new(9, "SELECTED SLOT", career);
        var frontend = new RetailFrontendSession([first, selected]);
        EnterLoadGame(frontend);
        Assert.True(frontend.SelectCareerIndex(1));

        RetailFrontendSignal signal = frontend.Confirm();

        Assert.Equal(RetailFrontendSignal.CareerLoadRequested, signal);
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        Assert.Equal(800, frontend.SelectedWorldNumber);
        Assert.Same(selected, frontend.ConsumeSelectedCareerLoadRequest());
        Assert.Null(frontend.ConsumeSelectedCareerLoadRequest());
        Assert.True(frontend.SelectWorld(100));
    }

    [Fact]
    public void LoadGame_AcceptWithoutASelectedCareerStaysOnThePage()
    {
        RetailCareerDescriptor descriptor = new(2, "ONLY SLOT", ReadGoldCareer());
        var frontend = new RetailFrontendSession([descriptor]);
        EnterLoadGame(frontend);

        RetailFrontendSignal signal = frontend.Confirm();

        Assert.Equal(RetailFrontendSignal.Unavailable, signal);
        Assert.Equal(RetailFrontendScreen.DevSelect, frontend.Screen);
        Assert.Equal(RetailFrontendMenuItemKind.LoadGame, frontend.UnavailableSelection);
        Assert.Null(frontend.ConsumeSelectedCareerLoadRequest());
    }

    [Fact]
    public void LoadGame_BackReturnsToMainMenuWithoutASelectionHandoff()
    {
        RetailCareerDescriptor descriptor = new(2, "ONLY SLOT", ReadGoldCareer());
        var frontend = new RetailFrontendSession([descriptor]);
        EnterLoadGame(frontend);
        Assert.True(frontend.MoveNext());

        RetailFrontendSignal signal = frontend.Back();

        Assert.Equal(RetailFrontendSignal.PageChanged, signal);
        Assert.Equal(RetailFrontendScreen.MainMenu, frontend.Screen);
        Assert.Equal(RetailFrontendCareerPageMode.New, frontend.CareerPageMode);
        Assert.Equal(-1, frontend.SelectedCareerIndex);
        Assert.Equal(RetailFrontendSession.DefaultGameName, frontend.GameName);
        Assert.Null(frontend.ConsumeSelectedCareerLoadRequest());
    }

    [Fact]
    public void LoadGame_DoesNotEditTheInjectedSaveName()
    {
        RetailCareerDescriptor descriptor = new(2, "ONLY SLOT", ReadGoldCareer());
        var frontend = new RetailFrontendSession([descriptor]);
        EnterLoadGame(frontend);
        Assert.True(frontend.MoveNext());

        Assert.False(frontend.AppendGameNameCharacter('X', 0));
        Assert.False(frontend.RemoveGameNameCharacter());
        Assert.Equal("ONLY SLOT", frontend.GameName);
    }

    [Fact]
    public void ScenePath_AcceptsTheSelectedLoadCareerHandoff()
    {
        RetailCareerDescriptor descriptor = new(2, "ONLY SLOT", ReadGoldCareer());
        var frontend = new RetailFrontendSession([descriptor]);
        var path = new RetailFrontendScenePath();
        Assert.True(path.TryAcceptClickToStartKey(frontend, 0x1C));
        Assert.True(path.TryAcceptMainMenuRow(frontend, 2));
        Assert.True(frontend.MoveNext());

        Assert.True(path.TryAcceptDevSelect(frontend));
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        Assert.Same(descriptor, frontend.ConsumeSelectedCareerLoadRequest());
    }

    private static void EnterLoadGame(RetailFrontendSession frontend)
    {
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.True(frontend.SelectMainIndex(2));
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.DevSelect, frontend.Screen);
    }

    private static RetailCareerSave ReadGoldCareer() => RetailCareerSaveCodec.Read(
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "gold_career_save.bin")));
}
