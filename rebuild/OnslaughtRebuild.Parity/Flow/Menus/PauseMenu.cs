// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/PauseMenu.cpp and src/PauseMenu.h (CPauseMenu: the constructor 004cde60, Resume 004d06e0,
// ButtonPressed 004d0810, HandleEvent 004d0e50, Activate 004d0ff0, DeActivate 004d10b0, MenuItemChanged 004d1110, Render
// 004d11d0, ReceiveButtonAction 004d15d0; CSimpleGameMenu: ReceiveButtonAction 004d1810, AddItem; reconstructed from the
// retail program, not in Stuart's release).

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>
/// CPauseMenu: the in-game menu (Escape in a level) and, as <c>CPauseMenu(TRUE)</c>, the PC options page. Its lists: the
/// first (the game's continue, message log and briefing; controls, sound and display; the game's retry and quit, or the
/// options page's credits), the controls, the display and the sound, with the retry and quit confirmation. What their
/// settings change beyond the menu (a display reset, the key presets, the joysticks, god mode) stops a run that reaches it.
/// </summary>
public sealed class PauseMenu : IButtonReceiver
{
    /// <summary>PAUSE_MENU_YES / PAUSE_MENU_NO: the confirmation's events.</summary>
    public const int EventYes = 2000;
    public const int EventNo = 2001;

    private readonly IMenuHost _host;
    private readonly IPauseMenuGame? _game;
    private readonly PlatformClock _clock;
    private readonly int _back;
    private readonly int _credits;
    private readonly int _musicVolume;
    private readonly int _soundVolume;
    private readonly int _continue;
    private readonly int _messageLog;
    private readonly int _briefing;
    private readonly int _retry;
    private readonly int _quit;
    private readonly int _sure;
    private readonly int _no;
    private readonly int _yes;
    private readonly int _godOn;
    private readonly int _godOff;

    /// <param name="game">The game, for the in-game form; null for the options page.</param>
    public PauseMenu(IMenuHost host, bool optionsPage, IPauseMenuGame? game = null)
    {
        if (!optionsPage && game is null)
        {
            throw new ArgumentException("the in-game pause menu needs the game", nameof(game));
        }

        _host = host;
        _game = game;
        _clock = host.Clock;
        Text.TextIds ids = host.Ids;
        int Id(string name) => (int)ids[name];
        _back = Id("GI_BACK");
        _credits = Id("JCL_CREDITS");
        _musicVolume = Id("MUSIC_VOLUME");
        _soundVolume = Id("SOUND_VOLUME");
        _continue = Id("GI_CONTINUE");
        _messageLog = Id("GI_MESSAGE_LOG");
        _briefing = Id("GI_BRIEFING");
        _retry = Id("GI_RETRY");
        _quit = Id("GI_QUIT");
        _sure = Id("IG_SURE");
        _no = Id("IG_NO");
        _yes = Id("IG_YES");
        _godOn = Id("GI_GOD_ON");
        _godOff = Id("GI_GOD_OFF");
        OptionsPage = optionsPage;

        // The menu.
        var list = new MenuOptionsList(host);
        if (!optionsPage)
        {
            list.AddItem(new MenuItem(_continue));
            list.AddItem(new MenuItem(_messageLog));
            list.AddItem(new MenuItem(_briefing));
        }

        list.AddItem(new MenuItem(0, 3));
        list.AddItem(new MenuItem(0, 1));
        list.AddItem(new MenuItem(0, 2));
        if (optionsPage)
        {
            list.AddItem(new MenuItem(_credits));
        }
        else
        {
            list.AddItem(new MenuItem(_retry));
            list.AddItem(new MenuItem(_quit));
        }

        Lists.Add(list);

        // The controls and the display.
        Lists.Add(OptionsLists.Controls(host, _godOn, _godOff, _back));
        Lists.Add(OptionsLists.Display(host, _back));

        // The sound.
        Career.Career career = host.Career;
        list = new MenuOptionsList(host);
        list.AddItem(new ScaleMenuItem(host, _soundVolume, 0, career.Record.SoundVolume, MenuItemChanged, 10, true));
        list.AddItem(new ScaleMenuItem(host, _musicVolume, 0, career.Record.MusicVolume, MenuItemChanged, 10, true));
        list.AddItem(new BoolSettingMenuItem(host, 4, false, () => Options.FlipSpeakers,
            v => SetOptions(Options with { FlipSpeakers = v })));
        list.AddItem(new BoolSettingMenuItem(host, 7, true, () => Options.HardwareSound,
            v => SetOptions(Options with { HardwareSound = v })));
        list.AddItem(new SettingMenuItem(host, 0xd8, true, 3, () => Options.SoundRate,
            v => SetOptions(Options with { SoundRate = v })));
        list.AddItem(new SettingMenuItem(host, 9, true, Math.Max(1, host.SoundDevices), () => Options.SoundDevice,
            v => SetOptions(Options with { SoundDevice = v })));
        list.AddItem(new SettingMenuItem(host, 0xa, true, 3,
            () => Options.Sound3DMethod == -1 ? host.Sound3DMethod : Options.Sound3DMethod,
            v => SetOptions(Options with { Sound3DMethod = v })));
        list.AddItem(new ApplyMenuItem(list));
        list.AddItem(new MenuItem(_back));
        Lists.Add(list);
    }

    /// <summary>mLists.</summary>
    public List<MenuOptionsList> Lists { get; } = [];

    /// <summary>mOptionsPage.</summary>
    public bool OptionsPage { get; }

    public bool Active { get; private set; }

    /// <summary>mPage: the list shown.</summary>
    public int Page { get; private set; }

    /// <summary>mResult: 1 when credits were chosen.</summary>
    public int Result { get; private set; }

    /// <summary>mChosen: the text of the last item pressed.</summary>
    public int Chosen { get; private set; }

    public float ActivateTime { get; private set; }

    public float OpenTime { get; private set; }

    public float CloseTime { get; private set; } = -1.0f;

    public bool NoFade { get; private set; }

    /// <summary>GAME.GetLevelBriefingLog(): the log the Briefing item opens.</summary>
    public BriefingLog? BriefingLog { get; set; }

    /// <summary>GAME.GetMessageLog(): the log the Message Log item opens.</summary>
    public MessageLog? MessageLog { get; set; }

    /// <summary>mConfirm: the retry or quit confirmation while it is up.</summary>
    public ConfirmMenu? Confirm { get; private set; }

    /// <summary>IsConfirming (004d05c0).</summary>
    public bool IsConfirming => Confirm is { Active: true };

    bool IButtonReceiver.CanBeControlledWhenInPause => true;

    ControlType IButtonReceiver.ControlType => ControlType.GameMenu;

    private CareerOptions Options => _host.Options;

    private void SetOptions(CareerOptions options) => _host.Options = options;

    private IPauseMenuGame Game => _game ?? throw new InvalidOperationException("the options page's menu has no game");

    /// <summary>Activate (004d0ff0): open on the first list; the briefing only for a level that has one.</summary>
    public void Activate(bool fade)
    {
        Result = 0;
        Active = true;
        Chosen = 0;
        ActivateTime = _clock.GetSysTimeFloat();
        OpenTime = _clock.GetSysTimeFloat();
        Page = 0;
        Lists[0].Reset();
        NoFade = !fade;
        if (_game is { } game)
        {
            bool noBriefing = (game.NumPrimaryObjectives == 0 && game.NumSecondaryObjectives == 0 && game.IsMultiplayer)
                || game.CurrentLevel >= 900;
            Lists[0].SetItemEnabled(_briefing, !noBriefing);
        }
        else
        {
            Lists[0].SetItemEnabled(_briefing, true);
        }
    }

    /// <summary>DeActivate (004d10b0): closed, and the confirmation gone.</summary>
    public void DeActivate(bool fade)
    {
        Active = false;
        CloseTime = _clock.GetSysTimeFloat();
        Confirm = null;
        NoFade = !fade;
    }

    /// <summary>
    /// Resume (004d06e0): in a game, the menu's controllers let it go, the game unpauses, the mouse comes back and the
    /// career and options are saved; then the menu closes.
    /// </summary>
    public void Resume()
    {
        if (!OptionsPage)
        {
            Game.RelinquishControl(this);
            Game.UnPause();
            Game.InitMouse();
            _host.SaveFromPause();
        }

        DeActivate(true);
    }

    /// <summary>
    /// MenuItemChanged (004d1110): the volumes, as MUSIC.SetVolume and SOUND.SetMasterVolume set them, which keep them
    /// in the career (Music.cpp 572, SoundManager.cpp 178).
    /// </summary>
    public void MenuItemChanged(MenuItem item)
    {
        if (item is ScaleMenuItem scale && (item.Text == _musicVolume || item.Text == _soundVolume))
        {
            float volume = scale.Value;
            volume = (float)X87.Div(volume, scale.Steps);
            if (item.Text == _musicVolume)
            {
                _host.Career.Record.MusicVolume = volume;
            }
            else
            {
                _host.Career.Record.SoundVolume = volume;
            }
        }
    }

    /// <summary>ButtonPressed (004d0810).</summary>
    public void ButtonPressed(MenuItem item)
    {
        bool backSound = false;
        Chosen = item.Text;
        if (item.Text == _credits && OptionsPage)
        {
            DeActivate(false);
            Result = 1;
        }
        else if (item.Text == _retry || item.Text == _quit)
        {
            var confirm = new ConfirmMenu(_host);
            confirm.AddItem(new MenuItem(_no), this, EventNo);
            confirm.AddItem(new MenuItem(_yes), this, EventYes);
            Confirm = confirm;
            Game.HandControl(this, confirm);
            confirm.Active = true;
        }
        else if (item.Text == _back)
        {
            if (!CanReturnToPauseRoot())
            {
                return;
            }

            Page = 0;
            Lists[0].Reset();
            backSound = true;
        }
        else if (item.Text == 0)
        {
            switch (item.PCText)
            {
                case 2:
                    Page += 2;
                    Lists[Page].Reset();
                    break;
                case 1:
                    Page += 3;
                    Lists[Page].Reset();
                    break;
                case 3:
                    Page++;
                    Lists[Page].Reset();
                    break;
                case 78:
                case 79:
                    return;
            }
        }
        else if (item.Text == _godOff && _godOff != 0)
        {
            item.Text = _godOn;
            Game.SetPlayerGod(true);
        }
        else if (item.Text == _godOn && _godOn != 0)
        {
            item.Text = _godOff;
            Game.SetPlayerGod(false);
        }
        else if (item.Text == _briefing)
        {
            // The briefing log takes the menu's controllers and opens.
            BriefingLog log = BriefingLog ?? throw new InvalidOperationException("the pause menu has no briefing log");
            DeActivate(false);
            Game.HandControl(this, log);
            log.Activate();
        }
        else if (item.Text == _continue && !OptionsPage)
        {
            Resume();
            backSound = true;
        }
        else if (item.Text == _musicVolume || item.Text == _soundVolume)
        {
            return;
        }
        else if (item.Text == _messageLog)
        {
            // The log takes the menu's controllers and opens.
            MessageLog log = MessageLog ?? throw new InvalidOperationException("the pause menu has no message log");
            DeActivate(false);
            Game.HandControl(this, log);
            log.Activate();
        }

        _host.PlaySound(backSound ? FrontEndSound.Back : FrontEndSound.Select);
    }

    /// <summary>
    /// HandleEvent (004d0e50): the confirmation's No closes it (its controllers come back to the menu); its Yes sets the
    /// quit (to the front end, or the level again).
    /// </summary>
    public void HandleEvent(int eventNum)
    {
        switch (eventNum)
        {
            case EventNo:
                _host.PlaySound(FrontEndSound.Back);
                if (Confirm is { } confirm)
                {
                    confirm.Active = false;
                    Game.RelinquishControl(confirm);
                    Confirm = null;
                }

                break;
            case EventYes:
                if (Confirm is not null)
                {
                    if (Chosen == _quit)
                    {
                        Game.SetQuit(QuitType.UserQuitToFrontEnd);
                    }

                    if (Chosen == _retry)
                    {
                        Game.SetQuit(QuitType.RestartLevel);
                    }
                }

                break;
        }
    }

    /// <summary>ReceiveButtonAction (004d15d0): nothing in the first 0.4 seconds; an open list takes it first.</summary>
    public void ReceiveButtonAction(int button, float value)
    {
        if (X87.Sub(_clock.GetSysTimeFloat(), OpenTime) < 0.4f)
        {
            return;
        }

        if (Lists[Page].PassToSelected(button, value))
        {
            return;
        }

        switch (button)
        {
            default:
                Lists[Page].ReceiveButtonAction(button, value);
                break;
            case Buttons.PauseMenuSelect:
                MenuOptionsList list = Lists[Page];
                ButtonPressed(list.Items[list.Selected]);
                break;
            case FrontEndButtons.MenuBack:
                if (Page > 0)
                {
                    // BackToFirstPage: not from the controls page while a player has no free binding.
                    if (CanReturnToPauseRoot())
                    {
                        Page = 0;
                        Lists[0].Reset();
                        _host.PlaySound(FrontEndSound.Back);
                    }
                }
                else
                {
                    Resume();
                    _host.PlaySound(FrontEndSound.Back);
                }

                break;
        }
    }

    void IButtonReceiver.ReceiveButtonAction(Controller from, int button, float value) => ReceiveButtonAction(button, value);

    /// <summary>
    /// GetBindingWarning (004d0de0): player 1 has a configurable action with no key, or in a running multiplayer game
    /// player 2 has.
    /// </summary>
    public bool HasBindingWarning() =>
        !OptionSettings.AllControlBindingsSet(_host.Bindings, 0)
        || (_game is { IsMultiplayer: true } && !OptionSettings.AllControlBindingsSet(_host.Bindings, 1));

    private bool CanReturnToPauseRoot() => Page != 1 || !HasBindingWarning();

    /// <summary>
    /// The state Render (004d11d0) changes: closed for 0.4 seconds or more, nothing; open for more than 0.4 seconds, the
    /// shown list renders, then the confirmation's. First, whether shown or not, SyncDetailItems.
    /// </summary>
    public void Render()
    {
        OptionSettings.SyncDetailItems(_host);
        if (!Active)
        {
            if (CloseTime < 0.0f || X87.Sub(_clock.GetSysTimeFloat(), CloseTime) > 0.4f)
            {
                return;
            }
        }

        float fade = Active
            ? (float)X87.Sub(_clock.GetSysTimeFloat(), OpenTime)
            : (float)X87.Sub(X87.Add(CloseTime, 0.4f), _clock.GetSysTimeFloat());
        if (fade > 0.4f)
        {
            Lists[Page].Render();
            Confirm?.List.Render();
        }
    }
}

/// <summary>CSimpleGameMenu (0x4c bytes): the confirmation a pause menu raises; each item sends its event to the menu.</summary>
public sealed class ConfirmMenu(IMenuHost host) : IButtonReceiver
{
    private readonly List<(MenuItem Item, PauseMenu ToCall, int Event)> _events = [];

    /// <summary>mActive.</summary>
    public bool Active { get; set; }

    /// <summary>mList (a CConfirmMenuOptionsList).</summary>
    public MenuOptionsList List { get; } = new(host);

    bool IButtonReceiver.CanBeControlledWhenInPause => true;

    ControlType IButtonReceiver.ControlType => ControlType.GameMenu;

    /// <summary>AddItem: the item, and the event it sends.</summary>
    public void AddItem(MenuItem item, PauseMenu toCall, int eventNum)
    {
        _events.Insert(0, (item, toCall, eventNum)); // mEvents.Add: SPtrSet::Add puts it at the head
        List.AddItem(item);
    }

    /// <summary>SendEvent: the item's event, straight to its menu.</summary>
    private void SendEvent(MenuItem item)
    {
        foreach ((MenuItem i, PauseMenu toCall, int eventNum) in _events)
        {
            if (i == item)
            {
                toCall.HandleEvent(eventNum);
                return;
            }
        }
    }

    /// <summary>ReceiveButtonAction (004d1810): up and down move; select sends the selection's event; back sends No's.</summary>
    public void ReceiveButtonAction(Controller from, int button, float value)
    {
        switch (button)
        {
            default:
                List.ReceiveButtonAction(button, value);
                break;
            case Buttons.PauseMenuSelect:
                SendEvent(List.Items[List.Selected]);
                break;
            case FrontEndButtons.MenuBack:
                SendEvent(List.Items[0]);
                break;
        }
    }
}
