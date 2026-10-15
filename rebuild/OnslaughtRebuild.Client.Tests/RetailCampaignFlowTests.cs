// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Campaign-flow parity for the released selector law across the first two
/// career nodes: New Game → SELECT LEVEL offers only the root on a cold
/// career; a Level 100 Won update applied to the same career unlocks world
/// 110; and the launch edge carries the selected world number
/// (<see cref="RetailFrontendSession.SelectedWorldNumber"/>) so the client
/// constructs the world the player chose, not always world 100.
///
/// <para>Selectability is the measured ReCalcLinks unlock
/// (<c>references/Onslaught/Career.cpp:379-515</c>): the child node's own
/// <c>mComplete</c> stays 0 while its incoming link leaves CN_NOT_COMPLETE.
/// The FillOut snapshot is the already-pinned
/// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>.</para>
/// </summary>
public sealed class RetailCampaignFlowTests
{
    [Fact]
    public void ColdCareer_SelectorOffersOnlyWorld100_AndLaunchCarriesIt()
    {
        var frontend = AtLevelSelect();

        Assert.False(frontend.SelectWorld(110));
        Assert.False(frontend.SelectWorld(200));
        Assert.Equal(100, frontend.ConsumeLaunchWorldNumber);

        DriveToLoading(frontend);
        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        Assert.Equal(100, frontend.ConsumeLaunchWorldNumber);
    }

    [Fact]
    public void WonRoot_UnlocksWorld110_AndLaunchCarriesIt()
    {
        var frontend = AtLevelSelect();

        // The released post-Won career state: FillOut + ReCalcLinks ran when
        // the mission reached FrontEndHandoffReady after Won.
        frontend.Career.ApplyUpdate(RetailFillOutEndLevelData.ForLevel100Won());

        Assert.True(frontend.SelectWorld(110));
        Assert.Equal(110, frontend.ConsumeLaunchWorldNumber);

        DriveToLoading(frontend);
        Assert.Equal(RetailFrontendSignal.LevelLaunchRequested, _lastConfirmSignal);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        Assert.Equal(110, frontend.ConsumeLaunchWorldNumber);

        // The distant graph stays locked even after one Won.
        Assert.False(frontend.SelectWorld(200));
        Assert.False(frontend.SelectWorld(500));
        Assert.Equal(110, frontend.ConsumeLaunchWorldNumber);
    }

    [Fact]
    public void SelectWorld_IsRejectedOutsideLevelSelect()
    {
        var frontend = new RetailFrontendSession();

        Assert.False(frontend.SelectWorld(100));

        frontend.Confirm(); // ClickToStart -> MainMenu
        Assert.False(frontend.SelectWorld(100));
    }

    [Fact]
    public void WonHandoff_ShowsDebriefing_ThenReturnsToUnlockedLevelSelect()
    {
        var frontend = AtGameplay();

        Assert.True(frontend.TryAcceptWonHandoff(
            Level100MissionOutcome.Won,
            Level100MissionTerminalState.FrontEndHandoffReady));
        Assert.Equal(RetailFrontendScreen.Debriefing, frontend.Screen);
        Assert.True(RetailWorldCatalog.IsWorldSelectable(frontend.Career, 110));
        Assert.Equal(100, frontend.ConsumeLaunchWorldNumber);
        Assert.True(frontend.SelectedWorldIsConstructible);

        RetailDebriefingProjection debriefing =
            Assert.IsType<RetailDebriefingProjection>(frontend.Debriefing);
        Assert.Equal(100, debriefing.WorldFinished);
        Assert.Equal(RetailDebriefingMissionStatus.Victory, debriefing.MissionStatus);
        Assert.Equal(
            RetailDebriefingObjectiveSummary.Complete,
            debriefing.PrimaryObjectives);
        Assert.Equal(
            RetailDebriefingObjectiveSummary.Hidden,
            debriefing.SecondaryObjectives);
        Assert.Equal((byte)'S', debriefing.GradeByte);
        Assert.Equal(5, debriefing.NewGoodieCount);
        Assert.True(debriefing.FirstGoodie);
        Assert.Equal(0, frontend.Career.Counters.NewGoodieCount);
        Assert.Equal(0, frontend.Career.Counters.FirstGoodie);

        // Career.Update has already unlocked the child, but the selector is
        // not interactive behind the released debriefing page.
        Assert.False(frontend.SelectWorld(110));
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        Assert.Null(frontend.Debriefing);

        Assert.True(frontend.SelectWorld(110));
        Assert.Equal(110, frontend.ConsumeLaunchWorldNumber);
        Assert.False(frontend.SelectedWorldIsConstructible);

        DriveToLoading(frontend);
        Assert.Equal(RetailFrontendSignal.LevelLaunchRequested, _lastConfirmSignal);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        Assert.Equal(110, frontend.ConsumeLaunchWorldNumber);
        Assert.True(frontend.ReturnUnconstructibleLaunchToLevelSelect());
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        Assert.Equal(110, frontend.ConsumeLaunchWorldNumber);
    }

    /// <summary>
    /// The transition from a Level 100 win to World 110: FillOut reads the
    /// base-world survivors from the level's end state, ReCalcLinks copies them
    /// onto World 110's node, and World 110's session skips the rows it marks
    /// lost (the RE lane's World 110 seed contract, "base-world carry-over").
    /// </summary>
    [Fact]
    public void WonHandoff_CarriesTheLevel100SurvivorsIntoWorld110sSession()
    {
        Level100ActorDefinitionSet level100 = Level100TestActorDefinitions.LoadMaterialized();
        WorldSnapshot end = new InteractiveSession(1u, level100).CurrentSnapshot;
        Level100ActorId factory = end.Level100Actors.Actors.Single(actor => actor.Name == "Tank Factory").ActorId;
        WorldSnapshot factoryLost = end with
        {
            Level100Actors = end.Level100Actors with
            {
                Actors = end.Level100Actors.Actors.Select(actor => actor.ActorId == factory
                    ? actor with { Lifecycle = Level100ActorLifecycle.Destroyed }
                    : actor).ToArray(),
            },
        };

        var frontend = AtGameplay();
        Assert.True(frontend.TryAcceptWonHandoff(
            Level100MissionOutcome.Won,
            Level100MissionTerminalState.FrontEndHandoffReady,
            RetailFillOutEndLevelData.BaseThingsLeft(factoryLost, level100)));
        Assert.Equal(RetailFrontendSignal.PageChanged, frontend.Confirm());
        Assert.True(frontend.SelectWorld(110));
        Assert.Equal([1], frontend.SelectedWorldLostBaseRows);

        var world110 = new InteractiveSession(
            123_456u, Level100TestActorDefinitions.LoadMaterializedWorld110(), 110, frontend.SelectedWorldLostBaseRows);
        Assert.Equal(110, world110.CurrentSnapshot.Level100Mission.WorldNumber);
        Assert.DoesNotContain(world110.CurrentSnapshot.Level100Actors.Actors, actor => actor.Name == "Tank Factory");
        Assert.Contains(world110.CurrentSnapshot.Level100Actors.Actors, actor => actor.Name == "Control Tower");

        // A win that loses nothing hands World 110 the whole base world.
        var intact = AtGameplay();
        Assert.True(intact.TryAcceptWonHandoff(
            Level100MissionOutcome.Won,
            Level100MissionTerminalState.FrontEndHandoffReady,
            RetailFillOutEndLevelData.BaseThingsLeft(end, level100)));
        Assert.Equal(RetailFrontendSignal.PageChanged, intact.Confirm());
        Assert.True(intact.SelectWorld(110));
        Assert.Empty(intact.SelectedWorldLostBaseRows);
    }

    /// <summary>
    /// <c>IScript::SetSlotSave</c> (<c>0x00533900</c>) calls
    /// <c>CCareer::SetSlot</c> at once, so a tutorial slot the mission saves
    /// reaches the player's career before any win (the host forwards the
    /// mission's <see cref="Level100TutorialSlotSaved"/>).
    /// </summary>
    [Fact]
    public void SlotSave_ReachesTheCareerBeforeAnyWin()
    {
        var frontend = AtGameplay();
        Assert.Equal(0, frontend.Career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        frontend.SaveSlot(new Level100TutorialSlotSaved(5, RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(1, frontend.Career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.False(RetailWorldCatalog.IsWorldSelectable(frontend.Career, 110));
    }

    [Fact]
    public void WonHandoff_IsRejectedUnlessGameplayWonIsReady()
    {
        var frontend = AtLevelSelect();
        Assert.False(frontend.TryAcceptWonHandoff(
            Level100MissionOutcome.Won,
            Level100MissionTerminalState.FrontEndHandoffReady));
        Assert.False(RetailWorldCatalog.IsWorldSelectable(frontend.Career, 110));

        DriveToLoading(frontend);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        frontend.CompleteLevel100Load();
        Assert.Equal(RetailFrontendScreen.Gameplay, frontend.Screen);

        Assert.False(frontend.TryAcceptWonHandoff(
            Level100MissionOutcome.Lost,
            Level100MissionTerminalState.FrontEndHandoffReady));
        Assert.Equal(RetailFrontendScreen.Gameplay, frontend.Screen);
        Assert.False(RetailWorldCatalog.IsWorldSelectable(frontend.Career, 110));
    }

    private static RetailFrontendSession AtGameplay()
    {
        var frontend = AtLevelSelect();
        DriveToLoading(frontend);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        frontend.CompleteLevel100Load();
        Assert.Equal(RetailFrontendScreen.Gameplay, frontend.Screen);
        return frontend;
    }

    private static RetailFrontendSignal _lastConfirmSignal;

    /// <summary>ClickToStart → MainMenu → DevSelect → LevelSelect.</summary>
    private static RetailFrontendSession AtLevelSelect()
    {
        var frontend = new RetailFrontendSession();
        frontend.Confirm();
        frontend.Confirm();
        frontend.Confirm();
        Assert.Equal(RetailFrontendScreen.LevelSelect, frontend.Screen);
        return frontend;
    }

    /// <summary>
    /// LevelSelect → MissionBriefing → SelectConfiguration → Loading,
    /// recording each Confirm signal for assertions.
    /// </summary>
    private static void DriveToLoading(RetailFrontendSession frontend)
    {
        _lastConfirmSignal = frontend.Confirm();
        Assert.Equal(RetailFrontendScreen.MissionBriefing, frontend.Screen);
        _lastConfirmSignal = frontend.Confirm();
        Assert.Equal(RetailFrontendScreen.SelectConfiguration, frontend.Screen);
        _lastConfirmSignal = frontend.Confirm();
        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
    }
}
