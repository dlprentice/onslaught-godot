// SPDX-License-Identifier: GPL-3.0-or-later
// CGame::DeclarePlayerDead 0046f550 and HandleEvent 0046ff10, matched bea-decomp game.cpp.
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Game;

public sealed partial class Game
{
    public const int GameOverWater = 0x036a7c43;
    public const int GameOverDeath = 0x033e2e5e;

    /// <summary>mHackCurrentGameMasterVolume, reset to one by InitRestartLoop.</summary>
    public float GameSoundsMasterVolume { get; private set; } = 1.0f;

    /// <summary>
    /// Player numbers here are one-based. Replace the camera, stop the current message voice, then declare the loss;
    /// CBattleEngine calls this before notifying its script. Multiplayer respawn and free-camera switching remain
    /// explicit unsupported routes, rejected before this method changes the camera or game state.
    /// </summary>
    public void DeclarePlayerDead(int number)
    {
        BattleEngine engine = RequirePlayerDeathSupported(number);
        SetCurrentCamera(number - 1, new ViewPointCamera(Events, engine.Pos, .5f, 0, 6, 3));
        if (_services.MessageBox is not null) _services.StopCurrentMessageSound();
        DeclareLevelLost(engine.IsInWater() ? GameOverWater : GameOverDeath, playerDied: true);
    }

    // BE calls this before setting its Dying flag, so unsupported routes do not leave a partial transition.
    internal BattleEngine RequirePlayerDeathSupported(int number)
    {
        if (number != 1 || Players != 1 || _services.IsMultiplayer || CurrentLevel is >= 850 and < 900)
            throw new NotSupportedException("DeclarePlayerDead currently supports the single-player route only");
        if (FreeCameraOn)
            throw new NotSupportedException("DeclarePlayerDead free-camera switching is not ported");
        return GetPlayer(number - 1)?.BattleEngine as BattleEngine
            ?? throw new InvalidOperationException("DeclarePlayerDead requires the player's live battle engine");
    }

    private void ContinueFadeOutGameSounds()
    {
        GameSoundsMasterVolume -= .003f;
        if (GameSoundsMasterVolume < 0)
            GameSoundsMasterVolume = 0;
        else
            Events.AddEvent((int)GameEvent.ContinueFadeOutGameSounds, this, EventManager.NextFrame);
        // Retail sets the master and immediately recomputes volume for every sound event.
        _services.SetGameSoundsMasterVolume(GameSoundsMasterVolume);
    }
}
