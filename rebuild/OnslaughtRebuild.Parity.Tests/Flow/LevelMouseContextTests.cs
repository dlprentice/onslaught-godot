// SPDX-License-Identifier: GPL-3.0-only
// CController::DoMappings checks the live sMouse after GameInterface or the pause menu releases it.
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Flow.Menus;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class LevelMouseContextTests
{
    [GameDataFact]
    public void LevelEndMenuDoesNotTreatWindowMouseMotionAsControllerActivity()
    {
        var level = LevelSession.Load(100, GameData.Read);
        level.AdvanceToNextPresent();
        level.Input.ShowLevelEndMenu();
        Assert.False(level.Input.Mouse.Created);
        Assert.False(level.Input.Context.DirectInputCreated);

        level.Input.Input.PostMouseMessage(MouseMessage.Move, 500, 240, 0);
        level.Input.Input.Process(level.Clock.QueryPerformanceCounter());
        Assert.Equal(500, level.Input.Input.Mouse.X);
        level.Input.Shared.LastTimeAnythingPressed = -123f;
        level.Input.FlushController(0);
        Assert.Equal(-123f, level.Input.Shared.LastTimeAnythingPressed);

        // ToggleMenuDisplay's other edge calls InitMouse again; active mouse axes can then send activity.
        level.Input.GameInterface!.ToggleMenuDisplay();
        Assert.True(level.Input.Mouse.Created);
        Assert.True(level.Input.Context.DirectInputCreated);
        level.Input.Input.Mouse.X = 500;
        level.Input.FlushController(0);
        Assert.NotEqual(-123f, level.Input.Shared.LastTimeAnythingPressed);
    }

    [GameDataFact]
    public void PauseReleaseAndResumeKeepTheControllerDeviceCheckInStep()
    {
        var level = LevelSession.Load(100, GameData.Read);
        var controller = new Controller(0, 0, null);
        level.Input.ShowPauseMenu(controller);
        Assert.False(level.Input.Mouse.Created);
        Assert.False(level.Input.Context.DirectInputCreated);

        // PauseMenu.Resume calls this interface method after relinquishing control.
        ((IPauseMenuGame)level.Input).InitMouse();
        Assert.True(level.Input.Mouse.Created);
        Assert.True(level.Input.Context.DirectInputCreated);
    }
}
