// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The player-visible cold-start path the Godot scene drives: Lost Toys /
/// opening FMV / splash skip, then CFEPIntro click-to-start, then CFEPMain,
/// then Options apply pulse and dropdown confirm / right-click cancel,
/// then New Game campaign accept, campaign Back, QuitConfirm Yes/No,
/// then Loading → Gameplay (and the intro-cutscene handoff).
/// Isolated helper pins already exist. These cases kill a path that only
/// those helpers know about, or a host that still uses
/// <c>ConfirmForSmoke</c> instead of the same accept owner.
/// </summary>
public sealed class RetailFrontendScenePathTests
{
    [Fact]
    public void SkipThenClickReachesCfepMainAndDoesNotConfirmDuringTheMovie()
    {
        var path = new RetailFrontendScenePath();
        var session = new RetailFrontendSession();
        path.Begin([]);

        Assert.True(path.StartupMediaActive);
        Assert.Equal(RetailFrontendScreen.ClickToStart, session.Screen);
        Assert.False(path.TryAcceptClickToStartMouse(session, 320f, 240f));
        Assert.Equal(RetailFrontendScreen.ClickToStart, session.Screen);

        Assert.False(path.TrySkipStartup(left: false, middle: false, right: false, dik: 0x1E));
        Assert.True(path.StartupMediaActive);
        Assert.True(path.TrySkipStartup(left: false, middle: false, right: false, dik: 0x39));
        Assert.False(path.StartupMediaActive);

        Assert.True(path.TryAcceptClickToStartMouse(session, 320f, 240f));
        Assert.Equal(RetailFrontendScreen.MainMenu, session.Screen);
        Assert.Equal(RetailFrontendMenuItemKind.NewGame, session.SelectedMainItem.Kind);
    }

    [Fact]
    public void SkipfmvStartsOnClickToStartAndEnterReachesCfepMain()
    {
        var path = new RetailFrontendScenePath();
        var session = new RetailFrontendSession();
        path.Begin(["--skipfmv"]);

        Assert.False(path.StartupMediaActive);
        Assert.True(RetailFrontendScenePath.IsStartupSuppressed(["--skipfmv"]));
        Assert.False(RetailFrontendScenePath.IsStartupSuppressed([]));
        Assert.False(path.TrySkipStartup(left: true, middle: false, right: false, dik: 0));
        Assert.True(path.TryAcceptClickToStartKey(session, 0x1C));
        Assert.Equal(RetailFrontendScreen.MainMenu, session.Screen);
    }

    [Fact]
    public void ClickToStartAcceptsOnlyTheSpecimenMouseAndKeys()
    {
        var path = new RetailFrontendScenePath();
        path.Begin(["--skipfmv"]);

        Assert.True(
            RetailFrontendScenePath.AcceptsClickToStartMouse(
                RetailFrontendScreen.ClickToStart,
                320f,
                240f));
        Assert.False(
            RetailFrontendScenePath.AcceptsClickToStartMouse(
                RetailFrontendScreen.MainMenu,
                320f,
                240f));
        Assert.True(
            RetailFrontendScenePath.AcceptsClickToStartKey(
                RetailFrontendScreen.ClickToStart,
                0x1C));
        Assert.True(
            RetailFrontendScenePath.AcceptsClickToStartKey(
                RetailFrontendScreen.ClickToStart,
                0x39));
        Assert.False(
            RetailFrontendScenePath.AcceptsClickToStartKey(
                RetailFrontendScreen.ClickToStart,
                0x9C));
        Assert.False(
            RetailFrontendScenePath.AcceptsClickToStartKey(
                RetailFrontendScreen.ClickToStart,
                0x01));

        var session = new RetailFrontendSession();
        Assert.False(path.TryAcceptClickToStartKey(session, 0x9C));
        Assert.Equal(RetailFrontendScreen.ClickToStart, session.Screen);
    }

    [Fact]
    public void FirstFlightGameAndFlowDriveThePathInsteadOfSmokeConfirm()
    {
        string game = ReadGodotSource("FirstFlightGame.cs");
        string flow = ReadGodotSource("RetailFrontendFlow.cs");
        string sequence = ReadGodotSource("RetailStartupSequence.cs");
        string startMedia = Slice(game, "private void StartRetailStartupMedia()");
        string pointer = Slice(flow, "private bool HandlePointerConfirm(");
        string key = Slice(flow, "private bool HandleKey(");
        string sequenceInput = Slice(sequence, "public override void _Input(");
        string clickArm = CaseArm(pointer, "case RetailFrontendScreen.ClickToStart:");

        Assert.Contains("RetailFrontendScenePath.IsStartupSuppressed", startMedia, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.AcceptsStartupSkip", sequenceInput, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.AcceptsClickToStartMouse", clickArm, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.AcceptsClickToStartKey", key, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmForSmoke", startMedia, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmForSmoke", pointer, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmForSmoke", key, StringComparison.Ordinal);
        Assert.Contains("RetailClickToStartSplash.Scale", flow, StringComparison.Ordinal);
        Assert.Contains("RetailClickToStartSlide.ShouldDraw", flow, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendScenePath", Slice(flow, "private void DrawClickToStart()"), StringComparison.Ordinal);
    }

    [Fact]
    public void MainMenuAcceptChangesCampaignLoadOptionsAndExitPages()
    {
        var path = new RetailFrontendScenePath();
        var campaign = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(campaign, 0));
        Assert.Equal(RetailFrontendScreen.DevSelect, campaign.Screen);

        var options = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(options, 5));
        Assert.Equal(RetailFrontendScreen.Options, options.Screen);

        var exit = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(exit, 6));
        Assert.Equal(RetailFrontendScreen.QuitConfirm, exit.Screen);

        var continueGame = AfterClickToStart(path);
        Assert.False(path.TryAcceptMainMenuRow(continueGame, 1));
        Assert.Equal(RetailFrontendScreen.MainMenu, continueGame.Screen);

        var loadGame = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(loadGame, 2));
        Assert.Equal(RetailFrontendScreen.DevSelect, loadGame.Screen);
        Assert.Equal(RetailFrontendCareerPageMode.Load, loadGame.CareerPageMode);
    }

    [Fact]
    public void FlowAcceptsMainMenuRowsThroughThePathAndDoesNotTreatLatchAsAccept()
    {
        string flow = ReadGodotSource("RetailFrontendFlow.cs");
        string pointer = Slice(flow, "private bool HandlePointerConfirm(");
        string mainArm = CaseArm(pointer, "case RetailFrontendScreen.MainMenu:");
        string options = ReadGodotSource("RetailFrontendFlow.Options.cs");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");

        Assert.Contains("RetailFrontendScenePath.CanAcceptMainMenuRow", mainArm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", mainArm, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.AcceptsOptionsPointerCancel", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton.Set", mainArm, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsDeferredDropdownPulsesApplyUntilApplyAndRightClickCancels()
    {
        var path = new RetailFrontendScenePath();
        var session = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(session, 5));
        Assert.Equal(RetailFrontendScreen.Options, session.Screen);

        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Sound);
        int soundQuality = IndexOfLabel(menu, "Sound quality:");
        Assert.Equal(RetailOptionsApplyTiming.OnApply, menu.Rows[soundQuality].Timing);
        Assert.True(menu.Hover(soundQuality));
        Assert.True(path.TryConfirmOptions(menu, out RetailOptionsSignal expand));
        Assert.Equal(RetailOptionsSignal.ValueChanged, expand);
        Assert.True(menu.IsExpanded);

        int committed = menu.SelectedRow.CommittedIndex;
        int other = committed == 0 ? 1 : 0;
        Assert.True(menu.SelectState(other));
        Assert.True(path.TryConfirmOptions(menu, out RetailOptionsSignal closed));
        Assert.Equal(RetailOptionsSignal.ValueChanged, closed);
        Assert.False(menu.IsExpanded);
        Assert.Equal(other, menu.Rows[soundQuality].CurrentIndex);
        Assert.Equal(committed, menu.Rows[soundQuality].CommittedIndex);
        Assert.Equal(0, menu.Settings.SoundQuality);
        Assert.True(RetailFrontendScenePath.ApplyPulseIsPending(menu));
        Assert.True(
            RetailOptionsApplyPulse.DropdownRowIsPending(
                menu.Rows[soundQuality].CommittedIndex,
                menu.Rows[soundQuality].CurrentIndex));
        Assert.NotEqual(
            RetailOptionsApplyPulse.IdlePackedColor,
            RetailOptionsApplyPulse.PackedColor(
                RetailFrontendScenePath.ApplyPulseIsPending(menu),
                0f));

        int apply = IndexOfLabel(menu, "Apply");
        Assert.True(menu.Hover(apply));
        Assert.True(path.TryConfirmOptions(menu, out RetailOptionsSignal applied));
        Assert.Equal(RetailOptionsSignal.Applied, applied);
        Assert.False(RetailFrontendScenePath.ApplyPulseIsPending(menu));
        Assert.Equal(other, menu.Settings.SoundQuality);

        Assert.True(menu.Hover(soundQuality));
        Assert.True(path.TryConfirmOptions(menu, out _));
        int next = menu.SelectedRow.CurrentIndex == 0 ? 1 : 0;
        Assert.True(menu.SelectState(next));
        Assert.False(path.TryCancelOptionsDropdown(menu, rightDown: false));
        Assert.True(menu.IsExpanded);
        Assert.True(path.TryCancelOptionsDropdown(menu, rightDown: true));
        Assert.False(menu.IsExpanded);
        Assert.Equal(other, menu.SelectedRow.CurrentIndex);
        Assert.False(RetailFrontendScenePath.AcceptsOptionsPointerCancel(rightDown: false));
        Assert.True(RetailFrontendScenePath.AcceptsOptionsPointerCancel(rightDown: true));
    }

    [Fact]
    public void FlowDrivesOptionsApplyConfirmAndRightClickCancelFromThePath()
    {
        string flow = ReadGodotSource("RetailFrontendFlow.cs");
        string options = ReadGodotSource("RetailFrontendFlow.Options.cs");
        string input = SliceUntil(flow, "public override void _Input", "public override void _Draw");
        string confirm = Slice(options, "private void ConfirmOptions(");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        string draw = Slice(options, "private void DrawOptionRow");
        string pointerCancel = Slice(flow, "private bool HandlePointerCancel(");

        Assert.Contains("MouseButton.Right", input, StringComparison.Ordinal);
        Assert.Contains("HandlePointerCancel", input, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.TryConfirmOptions", confirm, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.AcceptsOptionsPointerCancel", cancel, StringComparison.Ordinal);
        Assert.Contains("HandleOptionsPointerCancel", pointerCancel, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsApplyPulse.PackedColor", draw, StringComparison.Ordinal);
        Assert.Contains("DropdownRowIsPending", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendScenePath", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmForSmoke", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", Slice(flow, "private bool HandlePointerConfirm("), StringComparison.Ordinal);
    }

    [Fact]
    public void NewGameAcceptWalksToLevel100LaunchThroughThePath()
    {
        var path = new RetailFrontendScenePath();
        var session = AfterClickToStart(path);

        Assert.False(path.TryAcceptDevSelect(session));
        Assert.Equal(RetailFrontendScreen.MainMenu, session.Screen);

        Assert.True(path.TryAcceptMainMenuRow(session, 0));
        Assert.Equal(RetailFrontendScreen.DevSelect, session.Screen);
        Assert.Equal(RetailFrontendSession.DefaultGameName, session.GameName);

        Assert.True(path.TryAcceptDevSelect(session));
        Assert.Equal(RetailFrontendScreen.LevelSelect, session.Screen);
        Assert.False(session.ConsumeLevel100LaunchRequest());

        Assert.True(path.TryAcceptLevelSelect(session));
        Assert.Equal(RetailFrontendScreen.MissionBriefing, session.Screen);

        Assert.True(path.TryAcceptMissionBriefing(session));
        Assert.Equal(RetailFrontendScreen.SelectConfiguration, session.Screen);

        Assert.True(path.TryAcceptSelectConfiguration(session, out RetailFrontendSignal launch));
        Assert.Equal(RetailFrontendSignal.LevelLaunchRequested, launch);
        Assert.Equal(RetailFrontendScreen.Loading, session.Screen);
        Assert.True(session.ConsumeLevel100LaunchRequest());

        Assert.False(path.TryAcceptDevSelect(session));
        Assert.False(path.TryAcceptLevelSelect(session));
        Assert.False(path.TryAcceptMissionBriefing(session));
        Assert.False(path.TryAcceptSelectConfiguration(session, out RetailFrontendSignal idle));
        Assert.Equal(RetailFrontendSignal.None, idle);
    }

    [Fact]
    public void StartupMediaBlocksCampaignAcceptUntilSkip()
    {
        var path = new RetailFrontendScenePath();
        var session = new RetailFrontendSession();
        path.Begin([]);
        session.Confirm();
        session.Confirm();
        Assert.Equal(RetailFrontendScreen.DevSelect, session.Screen);
        Assert.True(path.StartupMediaActive);

        Assert.False(path.TryAcceptDevSelect(session));
        Assert.Equal(RetailFrontendScreen.DevSelect, session.Screen);

        Assert.True(path.TrySkipStartup(left: true, middle: false, right: false, dik: 0));
        Assert.True(path.TryAcceptDevSelect(session));
        Assert.Equal(RetailFrontendScreen.LevelSelect, session.Screen);
    }

    [Fact]
    public void QuitConfirmYesExitsAndNoReturnsToMainMenu()
    {
        var path = new RetailFrontendScenePath();
        var no = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(no, 6));
        Assert.Equal(RetailFrontendScreen.QuitConfirm, no.Screen);
        Assert.Equal(0, no.SelectedQuitConfirmIndex);
        Assert.True(path.TryAcceptQuitConfirm(no, out RetailFrontendSignal cancelled));
        Assert.Equal(RetailFrontendSignal.PageChanged, cancelled);
        Assert.Equal(RetailFrontendScreen.MainMenu, no.Screen);

        var yes = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(yes, 6));
        Assert.True(yes.SelectQuitConfirmIndex(1));
        Assert.True(path.TryAcceptQuitConfirm(yes, out RetailFrontendSignal exit));
        Assert.Equal(RetailFrontendSignal.ExitRequested, exit);
        Assert.Equal(RetailFrontendScreen.QuitConfirm, yes.Screen);
    }

    [Fact]
    public void FlowConfirmsCampaignAndQuitThroughThePath()
    {
        string flow = ReadGodotSource("RetailFrontendFlow.cs");
        string confirm = Slice(flow, "private void Confirm(");
        string pointer = Slice(flow, "private bool HandlePointerConfirm(");
        string key = Slice(flow, "private bool HandleKey(");
        string devArm = CaseArm(pointer, "case RetailFrontendScreen.DevSelect:");
        string debriefingArm = CaseArm(pointer, "case RetailFrontendScreen.Debriefing:");
        string levelArm = CaseArm(pointer, "case RetailFrontendScreen.LevelSelect:");
        string configArm = CaseArm(pointer, "case RetailFrontendScreen.SelectConfiguration:");
        string quitArm = CaseArm(pointer, "case RetailFrontendScreen.QuitConfirm:");

        Assert.Contains("RetailFrontendScenePath.TryConfirmPage", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("_session.Confirm()", confirm, StringComparison.Ordinal);
        Assert.Contains("Confirm();", devArm, StringComparison.Ordinal);
        Assert.Contains("Confirm();", debriefingArm, StringComparison.Ordinal);
        Assert.Contains(
            "new Rect2(0f, 0f, DesignWidth, DesignHeight).HasPoint(design)",
            debriefingArm,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TryBackPage", debriefingArm, StringComparison.Ordinal);
        Assert.Contains("Confirm();", levelArm, StringComparison.Ordinal);
        Assert.Contains("Confirm();", configArm, StringComparison.Ordinal);
        Assert.Contains("Confirm();", quitArm, StringComparison.Ordinal);
        Assert.Contains("Confirm();", key, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmForSmoke", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater", confirm, StringComparison.Ordinal);
    }

    [Fact]
    public void WonHandoff_DebriefingConfirmAndBackUseTheCentralPagePath()
    {
        var path = new RetailFrontendScenePath();

        RetailFrontendSession confirm = AtDebriefing(path);
        Assert.True(RetailFrontendScenePath.TryConfirmPage(
            confirm,
            startupMediaActive: false,
            out RetailFrontendSignal confirmSignal));
        Assert.Equal(RetailFrontendSignal.PageChanged, confirmSignal);
        Assert.Equal(RetailFrontendScreen.LevelSelect, confirm.Screen);

        RetailFrontendSession back = AtDebriefing(path);
        Assert.True(RetailFrontendScenePath.TryBackPage(
            back,
            startupMediaActive: false,
            out RetailFrontendSignal backSignal));
        Assert.Equal(RetailFrontendSignal.PageChanged, backSignal);
        Assert.Equal(RetailFrontendScreen.LevelSelect, back.Screen);
    }

    [Fact]
    public void DebriefingDrawUsesWritingChromeAndNoPageChevrons()
    {
        string flow = ReadGodotSource("RetailFrontendFlow.cs");
        string draw = Slice(flow, "private void DrawDebriefing(");
        string chrome = Slice(flow, "private void DrawDebriefingWritingChrome(");
        string loads = Slice(flow, "private void LoadTextures(");
        string debriefingLoads = loads[
            loads.IndexOf("_debriefingMetalRing", StringComparison.Ordinal)..
            loads.IndexOf("_loadingScreen", StringComparison.Ordinal)];

        Assert.Contains("DrawDebriefingWritingChrome();", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawBriefingStage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawPageChevrons", draw, StringComparison.Ordinal);
        Assert.Contains("_forsetiWritingLarge", chrome, StringComparison.Ordinal);
        Assert.Contains("index < 4", chrome, StringComparison.Ordinal);
        Assert.Contains(
            "RetailColor(0xfeffffff)",
            flow,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            draw.Split("DebriefingGradeBodyTint", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            7,
            debriefingLoads.Split(
                "CuratedAyaTextureLoader.Compression.Dxt2",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(
            "CuratedAyaTextureLoader.Compression.Rgba8",
            debriefingLoads,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CampaignBackWalksHomeThroughThePath()
    {
        var path = new RetailFrontendScenePath();
        var session = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(session, 0));
        Assert.True(path.TryAcceptDevSelect(session));
        Assert.True(path.TryAcceptLevelSelect(session));
        Assert.True(path.TryAcceptMissionBriefing(session));
        Assert.Equal(RetailFrontendScreen.SelectConfiguration, session.Screen);

        Assert.True(path.TryBack(session));
        Assert.Equal(RetailFrontendScreen.MissionBriefing, session.Screen);
        Assert.True(path.TryBack(session));
        Assert.Equal(RetailFrontendScreen.LevelSelect, session.Screen);
        Assert.True(path.TryBack(session));
        Assert.Equal(RetailFrontendScreen.DevSelect, session.Screen);
        Assert.True(path.TryBack(session));
        Assert.Equal(RetailFrontendScreen.MainMenu, session.Screen);
        Assert.False(path.TryBack(session));
        Assert.Equal(RetailFrontendScreen.MainMenu, session.Screen);
    }

    [Fact]
    public void StartupMediaBlocksCampaignBackUntilSkip()
    {
        var path = new RetailFrontendScenePath();
        var session = new RetailFrontendSession();
        path.Begin([]);
        session.Confirm();
        session.Confirm();
        Assert.Equal(RetailFrontendScreen.DevSelect, session.Screen);
        Assert.True(path.StartupMediaActive);
        Assert.False(path.TryBack(session));
        Assert.Equal(RetailFrontendScreen.DevSelect, session.Screen);

        Assert.True(path.TrySkipStartup(left: true, middle: false, right: false, dik: 0));
        Assert.True(path.TryBack(session));
        Assert.Equal(RetailFrontendScreen.MainMenu, session.Screen);
    }

    [Fact]
    public void LoadingCompleteWalksToGameplayThroughThePath()
    {
        var path = new RetailFrontendScenePath();
        var session = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(session, 0));
        Assert.True(path.TryAcceptDevSelect(session));
        Assert.True(path.TryAcceptLevelSelect(session));
        Assert.True(path.TryAcceptMissionBriefing(session));
        Assert.True(path.TryAcceptSelectConfiguration(session, out _));
        Assert.Equal(RetailFrontendScreen.Loading, session.Screen);

        Assert.False(path.TryCompleteLoading(session, launchConsumed: false));
        Assert.Equal(RetailFrontendScreen.Loading, session.Screen);
        Assert.True(session.ConsumeLevel100LaunchRequest());
        Assert.True(path.TryCompleteLoading(session, launchConsumed: true));
        Assert.Equal(RetailFrontendScreen.Gameplay, session.Screen);
        Assert.False(path.TryCompleteLoading(session, launchConsumed: true));
        Assert.Equal(RetailFrontendScreen.Gameplay, session.Screen);
    }

    [Fact]
    public void StartupMediaBlocksLoadingCompleteUntilSkip()
    {
        var path = new RetailFrontendScenePath();
        var session = new RetailFrontendSession();
        path.Begin([]);
        session.Confirm();
        session.Confirm();
        session.Confirm();
        session.Confirm();
        session.Confirm();
        session.Confirm();
        Assert.Equal(RetailFrontendScreen.Loading, session.Screen);
        Assert.True(session.ConsumeLevel100LaunchRequest());
        Assert.True(path.StartupMediaActive);
        Assert.False(path.TryCompleteLoading(session, launchConsumed: true));
        Assert.Equal(RetailFrontendScreen.Loading, session.Screen);

        Assert.True(path.TrySkipStartup(left: true, middle: false, right: false, dik: 0));
        Assert.True(path.TryCompleteLoading(session, launchConsumed: true));
        Assert.Equal(RetailFrontendScreen.Gameplay, session.Screen);
    }

    [Fact]
    public void FlowCompletesLoadingThroughThePath()
    {
        string flow = ReadGodotSource("RetailFrontendFlow.cs");
        string process = Slice(flow, "public override void _Process(");
        string cutscene = ReadGodotSource("RetailFrontendFlow.Cutscene.cs");
        string finish = Slice(cutscene, "private void FinishLevel100IntroCutscene(");

        Assert.Contains("RetailFrontendScenePath.TryCompleteLoading", process, StringComparison.Ordinal);
        Assert.DoesNotContain("_session.CompleteLevel100Load()", process, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.TryCompleteIntroCutscene", finish, StringComparison.Ordinal);
        Assert.DoesNotContain("_session.CompleteLevel100IntroCutscene()", finish, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater", process, StringComparison.Ordinal);
    }

    [Fact]
    public void IntroCutsceneCompleteWalksToGameplayThroughThePath()
    {
        var path = new RetailFrontendScenePath();
        var session = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(session, 0));
        Assert.True(path.TryAcceptDevSelect(session));
        Assert.True(path.TryAcceptLevelSelect(session));
        Assert.True(path.TryAcceptMissionBriefing(session));
        Assert.True(path.TryAcceptSelectConfiguration(session, out _));
        Assert.True(session.ConsumeLevel100LaunchRequest());
        session.BeginLevel100IntroCutscene();
        Assert.Equal(RetailFrontendScreen.IntroCutscene, session.Screen);

        Assert.False(path.TryCompleteLoading(session, launchConsumed: true));
        Assert.Equal(RetailFrontendScreen.IntroCutscene, session.Screen);
        Assert.True(path.TryCompleteIntroCutscene(session));
        Assert.Equal(RetailFrontendScreen.Gameplay, session.Screen);
        Assert.False(path.TryCompleteIntroCutscene(session));
    }

    [Fact]
    public void FlowDrivesCampaignBackFromThePath()
    {
        string flow = ReadGodotSource("RetailFrontendFlow.cs");
        string options = ReadGodotSource("RetailFrontendFlow.Options.cs");
        string pointer = Slice(flow, "private bool HandlePointerConfirm(");
        string key = Slice(flow, "private bool HandleKey(");
        string backFromOptions = Slice(options, "private void BackFromOptions(");
        string devArm = CaseArm(pointer, "case RetailFrontendScreen.DevSelect:");
        string levelArm = CaseArm(pointer, "case RetailFrontendScreen.LevelSelect:");
        string configArm = CaseArm(pointer, "case RetailFrontendScreen.SelectConfiguration:");

        Assert.Contains("RetailFrontendScenePath.TryBackPage", key, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.TryBackPage", devArm, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.TryBackPage", levelArm, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.TryBackPage", configArm, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendScenePath.TryBackPage", backFromOptions, StringComparison.Ordinal);
        Assert.DoesNotContain("_session.Back()", key, StringComparison.Ordinal);
        Assert.DoesNotContain("_session.Back()", pointer, StringComparison.Ordinal);
        Assert.DoesNotContain("_session.Back()", backFromOptions, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater", key, StringComparison.Ordinal);
    }

    private static RetailFrontendSession AfterClickToStart(RetailFrontendScenePath path)
    {
        var session = new RetailFrontendSession();
        path.Begin(["--skipfmv"]);
        Assert.True(path.TryAcceptClickToStartMouse(session, 320f, 240f));
        Assert.Equal(RetailFrontendScreen.MainMenu, session.Screen);
        return session;
    }

    private static RetailFrontendSession AtDebriefing(RetailFrontendScenePath path)
    {
        RetailFrontendSession session = AfterClickToStart(path);
        Assert.True(path.TryAcceptMainMenuRow(session, 0));
        Assert.True(path.TryAcceptDevSelect(session));
        Assert.True(path.TryAcceptLevelSelect(session));
        Assert.True(path.TryAcceptMissionBriefing(session));
        Assert.True(path.TryAcceptSelectConfiguration(session, out _));
        Assert.True(session.ConsumeLevel100LaunchRequest());
        Assert.True(path.TryCompleteLoading(session, launchConsumed: true));
        Assert.True(RetailFrontendScenePath.TryAcceptWonHandoff(
            session,
            Level100MissionOutcome.Won,
            Level100MissionTerminalState.FrontEndHandoffReady));
        Assert.Equal(RetailFrontendScreen.Debriefing, session.Screen);
        return session;
    }

    private static int IndexOfLabel(RetailOptionsMenu menu, string label)
    {
        for (int i = 0; i < menu.Rows.Count; i++)
        {
            if (string.Equals(menu.Rows[i].Label, label, StringComparison.Ordinal))
            {
                return i;
            }
        }

        Assert.Fail(label);
        return -1;
    }

    private static string ReadGodotSource(string fileName) =>
        File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", fileName));

    private static string Slice(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        string rest = source[start..];
        int next = rest.IndexOf("\n    private ", signature.Length, StringComparison.Ordinal);
        if (next < 0)
        {
            next = rest.IndexOf("\n    public ", signature.Length, StringComparison.Ordinal);
        }

        return next >= 0 ? rest[..next] : rest;
    }

    private static string CaseArm(string handlePointerConfirm, string caseLabel)
    {
        int click = handlePointerConfirm.IndexOf(caseLabel, StringComparison.Ordinal);
        Assert.True(click >= 0, caseLabel);
        string arm = handlePointerConfirm[click..];
        int next = arm.IndexOf(
            "case RetailFrontendScreen.",
            caseLabel.Length,
            StringComparison.Ordinal);
        return next >= 0 ? arm[..next] : arm;
    }

    private static string SliceUntil(string source, string signature, string endSignature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        string rest = source[start..];
        int next = rest.IndexOf(endSignature, signature.Length, StringComparison.Ordinal);
        return next >= 0 ? rest[..next] : rest;
    }
}
