// SPDX-License-Identifier: GPL-3.0-or-later
// Expectations from pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// CPlayer::ReceiveButtonAction 004d3160..004d3199 and CGame::StartPlayingState 0046fec0. The matched source is
// bea-decomp src/Player.cpp:311..315, game.cpp:3102..3108; PCController.cpp:44..59 gives the input ordering.
// These execute the reconstruction against installed data; they are not a native runtime comparison.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Script;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SkipPanningTests
{
    [GameDataFact]
    public void SpaceSkipsPanAndKeepsOriginalScheduledDeadlines() => CheckKey(0x20, 0x39, pauses: false);

    [GameDataFact]
    public void ReturnSkipsPanAndKeepsOriginalScheduledDeadlines() => CheckKey(0x0d, 0x1c, pauses: false);

    [GameDataFact]
    public void KeypadEnterSkipsPanAndKeepsOriginalScheduledDeadlines() => CheckKey(0x0d, 0x9c, pauses: false);

    [GameDataFact]
    public void EscapeSkipsPanBeforeTheSameFlushOpensPause() => CheckKey(0x1b, 0x01, pauses: true);

    private static void CheckKey(int virtualKey, int scanCode, bool pauses)
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        AdvanceTo(session, 30);
        Assert.Equal(GameState.Panning, session.Game.State);
        Player player = Assert.IsType<Player>(session.Game.GetPlayer(0));
        PanCamera oldCamera = Assert.IsType<PanCamera>(session.Game.GetCurrentCamera(0));
        var oldCameraReader = new ActiveReader<Camera>();
        oldCameraReader.SetReader(oldCamera);
        EventManager events = session.Context.Events;
        ScheduledEvent panEnd = Assert.Single(Pending(events), e =>
            e.EventNum == (int)GameEvent.FinishedPanning && ReferenceEquals(e.ToCall, session.Game));
        ScheduledEvent playerView = Assert.Single(Pending(events), e =>
            e.EventNum == Player.GotoControlViewEvent && ReferenceEquals(e.ToCall, player));
        float originalDeadline = panEnd.Time;
        events.Log = [];

        // Ordinary PC window key -> Platform.Process -> Controller mappings -> Player. No mission/camera injection.
        session.Input.Input.PostTapeKey(virtualKey, scanCode, true);
        for (int i = 0; i < 4 && session.Game.State == GameState.Panning; i++) session.AdvanceToNextPresent();
        Assert.Equal(GameState.Playing, session.Game.State);
        Assert.Equal(PlayerView.FirstPerson, player.CurrentView);
        Camera controlCamera = Assert.IsType<ThingCamera>(session.Game.GetCurrentCamera(0));
        Assert.Null(oldCameraReader.ToRead); // SetCurrentCamera released the old monitor and its pending readers.
        Assert.Equal(0, oldCamera.DeletionCount);
        Assert.Equal(pauses, session.Game.Paused);
        Assert.Equal(pauses, session.Input.PauseMenu!.Active);
        Assert.Equal(0, session.Input.PauseMenu.Page);
        Assert.Equal(0, session.Input.PauseMenu.Chosen);
        Assert.Equal(0, session.Input.PauseMenu.Lists[0].Selected);
        Assert.False(session.Input.PauseMenu.IsConfirming);
        Assert.Contains(panEnd, Pending(events));
        Assert.Contains(playerView, Pending(events));
        Assert.Same(session.Game, panEnd.ToCall);
        Assert.Same(player, playerView.ToCall);
        Assert.Equal(originalDeadline, panEnd.Time);
        Assert.Equal(1, AllowedMessageAdds(events));

        session.Input.Input.PostTapeKey(virtualKey, scanCode, false);
        if (pauses)
        {
            // Real pause fade admission, then ordinary Escape resumes; no direct UnPause call.
            AdvanceTo(session, session.PresentCount + 24);
            session.Input.Input.PostTapeKey(0x1b, 0x01, true);
            for (int i = 0; i < 4 && session.Game.Paused; i++) session.AdvanceToNextPresent();
            Assert.False(session.Game.Paused);
            Assert.False(session.Input.PauseMenu.Active);
            Assert.True(session.Input.Mouse.Created);
            session.Input.Input.PostTapeKey(0x1b, 0x01, false);
        }

        while (events.Time <= originalDeadline + 0.15f && session.PresentCount < 1000)
            session.AdvanceToNextPresent();
        Assert.True(events.Time > originalDeadline + 0.15f, "the original pan deadline must actually execute");
        Assert.Same(controlCamera, session.Game.GetCurrentCamera(0));
        Assert.Equal(GameState.Playing, session.Game.State);
        Assert.Equal(PlayerView.FirstPerson, player.CurrentView);
        Assert.Equal(1, AllowedMessageAdds(events));
        Assert.Single(events.Log, e => e.Kind == 'F' && e.EventNum == (int)GameEvent.FinishedPanning && e.Target == "Game");
        Assert.Single(events.Log, e => e.Kind == 'F' && e.EventNum == Player.GotoControlViewEvent && e.Target == "Player");

        // Isolate the action guard once Playing: Space also has a separate morph binding, which is outside this
        // test. A direct repeated SkipPanning action cannot replace the camera or restart the message transition.
        player.ReceiveButtonAction(new Controller(0, 0, player), Buttons.SkipPanning, 1.0f);
        Assert.Same(controlCamera, session.Game.GetCurrentCamera(0));
        Assert.Equal(1, AllowedMessageAdds(events));
    }

    private static int AllowedMessageAdds(EventManager events) => events.Log!.Count(e =>
        e.Kind == 'A' && e.EventNum == MessageBox.AllowedToPlayMessages && e.Target == "MessageBox" && e.Dropped is null);

    private static List<ScheduledEvent> Pending(EventManager events)
    {
        var result = events.Overflow.ToList();
        for (int buffer = 0; buffer < EventManager.BufferCount; buffer++)
            for (int priority = 0; priority < EventManager.PriorityCount; priority++)
                result.AddRange(events.Buffer(buffer, (EventPriority)priority));
        return result;
    }

    private static void AdvanceTo(LevelSession session, long present)
    {
        while (session.PresentCount < present) session.AdvanceToNextPresent();
    }
}
