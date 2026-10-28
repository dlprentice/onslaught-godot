// SPDX-License-Identifier: GPL-3.0-only
// CGame::ShutdownRestartLoop (game.cpp:622, retail 0046cd1f) calls ShutdownMouse before return or retry.
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class LevelMouseShutdownTests
{
    // The behavior under test does not require the installation's user-created options file.
    private static (string Name, byte[] Bytes) WithoutPlayerOptions(string path) =>
        string.Equals(path, DefaultOptions.FileName, StringComparison.OrdinalIgnoreCase)
            ? throw new FileNotFoundException(path) : GameData.Read(path);

    private static LevelSession Finished(QuitType quit)
    {
        var level = LevelSession.Load(100, WithoutPlayerOptions);
        level.AdvanceToNextPresent();
        level.Game.SetQuit(quit);
        for (int i = 0; i < 60 && !level.Ended; i++) level.AdvanceToNextPresent();
        Assert.True(level.Ended);
        Assert.True(level.Input.Mouse.Created);
        Assert.True(level.Input.Input.Mouse.InputActive);
        return level;
    }

    [GameDataFact]
    public void ReturningReleasesDirectInputBeforeWindowMouseMovesAndOnlyOnce()
    {
        var level = Finished(QuitType.QuitToFrontEnd);
        level.EndLevelData.FinalState = EndLevelState.Lost; // no outro film in this authored control
        level.Input.Input.Mouse.SystemCursor = (77, 88);
        level.Shutdown(null!);
        Assert.False(level.Input.Mouse.Created);
        Assert.False(level.Input.Input.Mouse.InputActive);
        Assert.False(level.Input.Context.DirectInputCreated);
        Assert.Equal((77, 88), (level.Input.Input.Mouse.X, level.Input.Input.Mouse.Y));
        level.Input.Window.FrontEndRunning = true;
        level.Input.Input.PostMouseMessage(MouseMessage.Move, 550, 260, 0);
        level.Input.Input.Process(level.Clock.QueryPerformanceCounter());
        Assert.Equal((550, 260), (level.Input.Input.Mouse.X, level.Input.Input.Mouse.Y));
        level.Shutdown(null!);
        Assert.Equal((550, 260), (level.Input.Input.Mouse.X, level.Input.Input.Mouse.Y));
    }

    [GameDataFact]
    public void RetryReleasesTheOldMouseBeforeCreatingANewOneAndRetainsReplayButtons()
    {
        var level = Finished(QuitType.RestartLevel);
        level.Input.Deliver([new RetailMouseEvent(level.Clock.PresentCount, 0, 0, 0, 1)]);
        level.Input.Mouse.PollState();
        Assert.True(level.Input.Input.Mouse.Held[0]);
        var retry = level.Restart();
        Assert.False(level.Input.Mouse.Created);
        Assert.False(level.Input.Context.DirectInputCreated);
        Assert.True(retry.Input.Mouse.Created);
        Assert.True(retry.Input.Context.DirectInputCreated);
        Assert.True(retry.Input.Input.Mouse.InputActive);
        retry.Input.Mouse.PollState();
        Assert.True(retry.Input.Input.Mouse.Held[0]);
    }
}
