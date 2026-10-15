// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

public sealed class RetailFrontendSessionTests
{
    [Fact]
    public void ReleasedEntryPathRequiresClickMainMenuNameChoiceAndLevelSelection()
    {
        var frontend = new RetailFrontendSession();

        Assert.Equal(RetailFrontendScreen.ClickToStart, frontend.Screen);
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.MainMenu, frontend.Screen);
        Assert.Equal(RetailFrontendMenuItemKind.NewGame, frontend.SelectedMainItem.Kind);

        // New Game enters retail's FEP_DEVSELECT ("CHOOSE GAME NAME") page
        // before level select; CFrontEnd::Init drives the new-career entry with
        // SetPage(FEP_DEVSELECT, 0) at references/Onslaught/FrontEnd.cpp:182.
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.DevSelect, frontend.Screen);
        Assert.Equal(RetailFrontendSession.DefaultGameName, frontend.GameName);

        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        Assert.False(frontend.ConsumeLevel100LaunchRequest());

        // SELECT LEVEL -> MISSION BRIEFING -> SELECT CONFIGURATION -> LOADING is
        // the released order captured on 2026-07-25; one pristine 640x480
        // reference frame exists for each of the two intermediate pages.
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.MissionBriefing, frontend.Screen);
        Assert.False(frontend.ConsumeLevel100LaunchRequest());

        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.SelectConfiguration, frontend.Screen);
        Assert.False(frontend.ConsumeLevel100LaunchRequest());

        Assert.Equal(RetailFrontendSignal.LevelLaunchRequested, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
        Assert.Equal(0, frontend.SelectedConfigurationIndex);
        Assert.Equal("Aquila Prototype", frontend.SelectedConfiguration.AuthoredName);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        Assert.False(frontend.ConsumeLevel100LaunchRequest());

        frontend.CompleteLevel100Load();
        Assert.Equal(RetailFrontendScreen.Gameplay, frontend.Screen);
    }

    [Fact]
    public void MainNavigationIsBoundedAndBackReturnsFromLevelSelect()
    {
        var frontend = AtMainMenu();

        Assert.False(frontend.MovePrevious());
        for (int index = 1; index < frontend.Items.Count; index++)
        {
            Assert.True(frontend.MoveNext());
        }

        Assert.Equal(RetailFrontendMenuItemKind.Quit, frontend.SelectedMainItem.Kind);
        Assert.False(frontend.MoveNext());

        while (frontend.MovePrevious())
        {
        }

        Assert.Equal(RetailFrontendMenuItemKind.NewGame, frontend.SelectedMainItem.Kind);
        frontend.Confirm();
        Assert.Equal(RetailFrontendScreen.DevSelect, frontend.Screen);
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Back());
        Assert.Equal(RetailFrontendScreen.MainMenu, frontend.Screen);
    }

    [Fact]
    public void DevSelectWithoutInjectedCareersOffersTheReleasedDefaultName()
    {
        RetailFrontendSession frontend = AtDevSelect();

        // A plain session performs no implicit save discovery. Callers inject
        // already-read descriptors explicitly when they want Load Game rows.
        Assert.Empty(frontend.CareerNames);
        Assert.Equal(-1, frontend.SelectedCareerIndex);
        Assert.False(frontend.SelectCareerIndex(0));
        Assert.False(frontend.MoveNext());
        Assert.False(frontend.MovePrevious());

        // "BEA 1" is what the pristine 640x480 retail capture shows pre-filled
        // and highlighted in the name field.
        Assert.Equal("BEA 1", RetailFrontendSession.DefaultGameName);
        Assert.Equal("BEA 1", frontend.GameName);
    }

    [Fact]
    public void DevSelectNameFieldIsEditableAndBounded()
    {
        RetailFrontendSession frontend = AtDevSelect();

        Assert.True(frontend.RemoveGameNameCharacter());
        Assert.Equal(string.Empty, frontend.GameName);
        Assert.True(frontend.AppendGameNameCharacter('2', 0));
        Assert.Equal("2", frontend.GameName);
        Assert.False(frontend.AppendGameNameCharacter('\n', 0));

        while (frontend.GameName.Length < RetailFrontendSession.MaxGameNameLength)
        {
            Assert.True(frontend.AppendGameNameCharacter('x', 0));
        }

        Assert.False(frontend.AppendGameNameCharacter('x', 0));
        Assert.Equal(RetailFrontendSession.MaxGameNameLength, frontend.GameName.Length);

        while (frontend.RemoveGameNameCharacter())
        {
        }

        Assert.Equal(string.Empty, frontend.GameName);
    }

    [Fact]
    public void FreshNameReplacementAndCursorEditsPreserveSuffix()
    {
        var frontend = AtDevSelect();
        Assert.True(frontend.AppendGameNameCharacter('A', 1000));
        Assert.Equal("A", frontend.GameName);
        Assert.False(frontend.GameNameIsFresh);
        Assert.True(frontend.AppendGameNameCharacter('C', 10));
        Assert.True(frontend.MoveGameNameCursor(false));
        Assert.True(frontend.AppendGameNameCharacter('B', 20));
        Assert.Equal("ABC", frontend.GameName);
        Assert.Equal(2, frontend.GameNameCursor);
        Assert.True(frontend.RemoveGameNameCharacter());
        Assert.Equal("AC", frontend.GameName);
        Assert.Equal(1, frontend.GameNameCursor);
    }

    [Fact]
    public void WidthGateMeasuresExistingNameAndMovementEndsFreshSelection()
    {
        var frontend = AtDevSelect();
        Assert.True(frontend.MoveGameNameCursor(true));
        Assert.False(frontend.GameNameIsFresh);
        Assert.False(frontend.AppendGameNameCharacter('A', 384));
        Assert.Equal("BEA 1", frontend.GameName);
        Assert.True(frontend.AppendGameNameCharacter('A', 383));
        Assert.Equal("BEA 1A", frontend.GameName);
        Assert.True(frontend.MoveGameNameCursor(false));
        Assert.True(frontend.RemoveGameNameCharacter());
        Assert.Equal("BEA A", frontend.GameName);
    }

    [Theory]
    [InlineData('$', -1)]
    [InlineData('@', -1)]
    [InlineData('[', -1)]
    [InlineData('~', -1)]
    [InlineData('A', 33)]
    [InlineData('á', 96)]
    [InlineData('ß', 146)]
    [InlineData('’', 7)]
    [InlineData('–', 13)]
    public void PhysicalGlyphAcceptanceUsesRetailRemap(char character, int expected)
    {
        Assert.Equal(expected, RetailFrontendSession.GameNameGlyphIndex(character));
        var frontend = AtDevSelect();
        Assert.Equal(expected >= 0, frontend.AppendGameNameCharacter(character, 0));
        if (expected < 0) Assert.True(frontend.GameNameIsFresh);
    }

    [Theory]
    [InlineData('á', 96)]
    [InlineData('ß', 146)]
    [InlineData('¡', 136)]
    [InlineData('¿', 137)]
    [InlineData('’', 7)]
    [InlineData('–', 13)]
    [InlineData('$', 145)]
    [InlineData('漢', 145)]
    public void SmallNameFontUsesItsGlyphRemapAndFallback(char character, int expected)
    {
        Assert.Equal(expected, RetailFrontendSession.GameNameRenderGlyphIndex(character, swapInvertedPunctuation: false));
    }

    [Fact]
    public void Font0SwapsInvertedPunctuationButSmallNameFontDoesNot()
    {
        Assert.Equal(137, RetailFrontendSession.GameNameRenderGlyphIndex('¡', true));
        Assert.Equal(136, RetailFrontendSession.GameNameRenderGlyphIndex('¿', true));
        Assert.Equal(136, RetailFrontendSession.GameNameRenderGlyphIndex('¡', false));
        Assert.Equal(137, RetailFrontendSession.GameNameRenderGlyphIndex('¿', false));
        Assert.Equal(7, RetailFrontendSession.GameNameRenderGlyphIndex('’', true));
        Assert.Equal(13, RetailFrontendSession.GameNameRenderGlyphIndex('–', true));
    }

    [Fact]
    public void DevSelectNameEditsDoNotEscapeThePage()
    {
        var frontend = new RetailFrontendSession();
        Assert.False(frontend.AppendGameNameCharacter('x', 0));
        Assert.False(frontend.RemoveGameNameCharacter());
        Assert.False(frontend.SelectCareerIndex(0));

        RetailFrontendSession devSelect = AtDevSelect();
        Assert.True(devSelect.AppendGameNameCharacter('x', 0));
        Assert.Equal(RetailFrontendSignal.PageChanged, devSelect.Back());
        Assert.Equal(RetailFrontendScreen.MainMenu, devSelect.Screen);
        Assert.Equal(RetailFrontendSession.DefaultGameName, devSelect.GameName);
    }

    [Fact]
    public void BackFromLevelSelectReturnsToTheNameChoicePage()
    {
        RetailFrontendSession frontend = AtDevSelect();
        frontend.Confirm();

        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Back());
        Assert.Equal(RetailFrontendScreen.DevSelect, frontend.Screen);
    }

    [Fact]
    public void BriefingAndConfigurationSitBetweenLevelSelectAndLoading()
    {
        RetailFrontendSession frontend = AtDevSelect();
        frontend.Confirm();
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);

        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.MissionBriefing, frontend.Screen);

        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.SelectConfiguration, frontend.Screen);

        Assert.Equal(1, frontend.ConfigurationCount);
        RetailFrontendBattleEngineConfiguration configuration = frontend.SelectedConfiguration;
        Assert.Equal(0, frontend.SelectedConfigurationIndex);
        Assert.Same(configuration, frontend.SelectedConfiguration);
        Assert.Equal(3, configuration.CatalogRecordIndex);
        Assert.Equal("Aquila Prototype", configuration.AuthoredName);
        Assert.Equal("BE:A Unit-00 'Prototype'", configuration.DisplayName);
        Assert.Equal("Pulse Cannon Pod", configuration.WalkerPrimary.AuthoredName);
        Assert.Equal("Mech Twin Vulcan Cannon", configuration.WalkerSecondary.AuthoredName);
        Assert.Equal("Mech Vulcan Cannon", configuration.JetPrimary.AuthoredName);
        Assert.Equal("Missile Pod", configuration.JetSecondary.AuthoredName);
        Assert.False(frontend.MovePrevious());
        Assert.False(frontend.MoveNext());
        Assert.False(frontend.SelectConfigurationIndex(-1));
        Assert.False(frontend.SelectConfigurationIndex(1));

        // Back retraces the same chain one page at a time.
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Back());
        Assert.Equal(RetailFrontendScreen.MissionBriefing, frontend.Screen);
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Back());
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Back());
        Assert.Equal(RetailFrontendScreen.DevSelect, frontend.Screen);
    }

    [Fact]
    public void OnlySelectConfigurationRaisesTheLevel100Launch()
    {
        RetailFrontendSession frontend = AtLoading();

        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        Assert.False(frontend.ConsumeLevel100LaunchRequest());
    }

    [Fact]
    public void UnavailableItemsStayOnMainMenuAndIdentifyTheSelection()
    {
        var frontend = AtMainMenu();
        Assert.True(frontend.MoveNext());

        Assert.Equal(RetailFrontendSignal.Unavailable, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.MainMenu, frontend.Screen);
        Assert.Equal(
            RetailFrontendMenuItemKind.ContinueGame,
            frontend.UnavailableSelection);
        Assert.False(frontend.ConsumeLevel100LaunchRequest());

        Assert.True(frontend.MoveNext());
        Assert.Null(frontend.UnavailableSelection);
    }

    [Fact]
    public void PointerSelectionUsesTheSameBoundedMainMenuState()
    {
        var frontend = AtMainMenu();

        Assert.True(frontend.SelectMainIndex(3));
        Assert.Equal(RetailFrontendMenuItemKind.Multiplayer, frontend.SelectedMainItem.Kind);
        Assert.False(frontend.SelectMainIndex(3));
        Assert.False(frontend.SelectMainIndex(-1));
        Assert.False(frontend.SelectMainIndex(frontend.Items.Count));
    }

    [Fact]
    public void QuitIsTheOnlyOtherAvailableMainAction()
    {
        var frontend = AtMainMenu();
        while (frontend.MoveNext())
        {
        }

        Assert.Equal(RetailFrontendMenuItemKind.Quit, frontend.SelectedMainItem.Kind);
        Assert.True(frontend.SelectedMainItem.IsAvailable);
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.QuitConfirm, frontend.Screen);
        Assert.Equal(0, frontend.SelectedQuitConfirmIndex);

        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.MainMenu, frontend.Screen);

        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.True(frontend.MoveNext());
        Assert.Equal(1, frontend.SelectedQuitConfirmIndex);
        Assert.Equal(RetailFrontendSignal.ExitRequested, frontend.Confirm());

        Assert.Equal(
            [
                RetailFrontendMenuItemKind.ContinueGame,
            ],
            frontend.Items.Where(item => !item.IsAvailable).Select(item => item.Kind));
    }

    [Fact]
    public void QuitConfirmBackReturnsToMainMenuWithoutExiting()
    {
        var frontend = AtMainMenu();
        while (frontend.MoveNext())
        {
        }

        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Back());
        Assert.Equal(RetailFrontendScreen.MainMenu, frontend.Screen);
        Assert.Equal(RetailFrontendMenuItemKind.Quit, frontend.SelectedMainItem.Kind);
    }

    [Fact]
    public void LoadingCannotCompleteBeforeTheLaunchIsClaimed()
    {
        RetailFrontendSession frontend = AtLoading();

        Assert.Throws<InvalidOperationException>(frontend.CompleteLevel100Load);
    }

    [Fact]
    public void GameplayTransitionsExposePauseOwnedRestartAndExitLevelSeams()
    {
        var frontend = AtGameplay();

        Assert.Equal(
            RetailFrontendSignal.LevelLaunchRequested,
            frontend.RestartLevel100());
        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        frontend.CompleteLevel100Load();

        Assert.Equal(
            RetailFrontendSignal.ReturnToMainMenuRequested,
            frontend.LeaveLevel100ForMainMenu());
        Assert.Equal(RetailFrontendScreen.MainMenu, frontend.Screen);
    }

    [Fact]
    public void LifecycleTransitionsRequireActiveGameplay()
    {
        var frontend = AtMainMenu();
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = frontend.RestartLevel100();
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = frontend.LeaveLevel100ForMainMenu();
        });
    }

    private static RetailFrontendSession AtMainMenu()
    {
        var frontend = new RetailFrontendSession();
        frontend.Confirm();
        return frontend;
    }

    private static RetailFrontendSession AtDevSelect()
    {
        RetailFrontendSession frontend = AtMainMenu();
        frontend.Confirm();
        return frontend;
    }

    /// <summary>DevSelect -> LevelSelect -> MissionBriefing -> SelectConfiguration -> Loading.</summary>
    private static RetailFrontendSession AtLoading()
    {
        RetailFrontendSession frontend = AtDevSelect();
        frontend.Confirm();
        frontend.Confirm();
        frontend.Confirm();
        frontend.Confirm();
        return frontend;
    }

    private static RetailFrontendSession AtGameplay()
    {
        RetailFrontendSession frontend = AtLoading();
        frontend.ConsumeLevel100LaunchRequest();
        frontend.CompleteLevel100Load();
        return frontend;
    }
}
