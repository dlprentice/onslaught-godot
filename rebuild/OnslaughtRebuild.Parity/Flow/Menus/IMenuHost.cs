// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>What the menus read and call: the front end's when it shows them, the game's in a level.</summary>
public interface IMenuHost
{
    /// <summary>CFrontEnd::PlaySound: SOUND.PlayEffect of the front-end sound (a rand() draw).</summary>
    void PlaySound(FrontEndSound sound);

    /// <summary>SOUND.SetMasterVolume: retain the career option and refresh the process's live event gains.</summary>
    void SetSoundVolume(float volume);

    PlatformClock Clock { get; }

    /// <summary>CAREER.</summary>
    Career.Career Career { get; }

    /// <summary>text.stf's names.</summary>
    Text.TextIds Ids { get; }

    /// <summary>The live options (the options file's CLIPARAMS fields).</summary>
    CareerOptions Options { get; set; }

    /// <summary>CPCSoundManager::GetNumDevices.</summary>
    int SoundDevices { get; }

    /// <summary>SOUND.m3DSoundMethod.</summary>
    int Sound3DMethod { get; }

    /// <summary>CONTROL_BINDINGS.</summary>
    Platform.ControlBindings Bindings { get; }

    /// <summary>gMouseSensitivity, the live value (the options file's copy is written with it).</summary>
    float MouseSensitivity { get; set; }

    /// <summary>FRONTEND.mFEPSaveGame.IsCheatActive: the save name's cheat, or every cheat when enabled.</summary>
    bool IsCheatActive(int cheat);

    /// <summary>The display and joysticks the options' display and controls lists read.</summary>
    MachineDisplay Machine { get; }

    /// <summary>
    /// CPauseMenu::Resume's save (004d06e0): CAREER.Save's image to the career's memory-card slot when it has a name,
    /// and to defaultoptions.bea.
    /// </summary>
    void SaveFromPause();
}

/// <summary>The game as the in-game pause menu reaches it (GAME and its controllers).</summary>
public interface IPauseMenuGame
{
    /// <summary>CGame::SetQuit.</summary>
    void SetQuit(Clock.QuitType quit);

    /// <summary>CGame::UnPause.</summary>
    void UnPause();

    /// <summary>InitMouse: the DirectInput mouse taken again.</summary>
    void InitMouse();

    /// <summary>
    /// The pause menu's HandControl: each game controller controlling <paramref name="from"/> takes <paramref name="to"/>.
    /// </summary>
    void HandControl(Platform.IButtonReceiver from, Platform.IButtonReceiver to);

    /// <summary>CGameMenu::RelinquishControl: each game controller controlling <paramref name="menu"/> lets it go.</summary>
    void RelinquishControl(Platform.IButtonReceiver menu);

    bool IsMultiplayer { get; }

    /// <summary>PausePlayer(0)->SetIsGod: the pause menu's god mode item.</summary>
    void SetPlayerGod(bool god);

    int NumPrimaryObjectives { get; }

    int NumSecondaryObjectives { get; }

    /// <summary>GetCurrentlyRunningLevelNum.</summary>
    int CurrentLevel { get; }
}
