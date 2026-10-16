// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/PauseMenu.cpp and src/PauseMenu.h (CPauseMenu: the constructor 004cde60, Resume 004d06e0,
// ButtonPressed 004d0810, Activate 004d0ff0, DeActivate 004d10b0, MenuItemChanged 004d1110, Render 004d11d0,
// ReceiveButtonAction 004d15d0; reconstructed from the retail program, not in Stuart's release).

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Menus;

/// <summary>
/// CPauseMenu as the PC options page shows it (<c>CPauseMenu(TRUE)</c>): four lists, the first (controls, sound, display,
/// credits) and the sound list ported; the controls and display lists' items are not ported and stop a run that enters
/// them. The in-game form (continue, message log, briefing, retry, quit and their confirmation) is not ported yet.
/// </summary>
public sealed class PauseMenu
{
    private readonly FrontEnd _frontEnd;
    private readonly PlatformClock _clock;
    private readonly int _back;
    private readonly int _credits;
    private readonly int _musicVolume;
    private readonly int _soundVolume;

    public PauseMenu(FrontEnd frontEnd, bool optionsPage)
    {
        if (!optionsPage)
        {
            throw new NotSupportedException("CPauseMenu's in-game form is not ported");
        }

        _frontEnd = frontEnd;
        _clock = frontEnd.Clock;
        Text.TextIds ids = frontEnd.Texts?.Ids ?? throw new InvalidOperationException("the pause menu needs the text ids");
        int Id(string name) => (int)ids[name];
        _back = Id("GI_BACK");
        _credits = Id("JCL_CREDITS");
        _musicVolume = Id("MUSIC_VOLUME");
        _soundVolume = Id("SOUND_VOLUME");
        OptionsPage = optionsPage;

        // The menu: controls (PC text 3), sound (1), display (2), credits.
        var list = new MenuOptionsList(frontEnd);
        list.AddItem(new MenuItem(0, 3));
        list.AddItem(new MenuItem(0, 1));
        list.AddItem(new MenuItem(0, 2));
        list.AddItem(new MenuItem(_credits));
        Lists.Add(list);

        // The controls (mouse sensitivity, configuration name, god mode with its cheat, the controller definition, four
        // joysticks, back) and the display (the detail items and the device): not ported.
        list = new MenuOptionsList(frontEnd);
        list.AddItem(new NotPortedMenuItem("the options' controls list"));
        Lists.Add(list);
        list = new MenuOptionsList(frontEnd);
        list.AddItem(new NotPortedMenuItem("the options' display list"));
        Lists.Add(list);

        // The sound.
        Career.Career career = frontEnd.Career;
        list = new MenuOptionsList(frontEnd);
        list.AddItem(new ScaleMenuItem(frontEnd, _soundVolume, 0, career.Record.SoundVolume, MenuItemChanged, 10, true));
        list.AddItem(new ScaleMenuItem(frontEnd, _musicVolume, 0, career.Record.MusicVolume, MenuItemChanged, 10, true));
        list.AddItem(new BoolSettingMenuItem(frontEnd, 4, false, () => Options.FlipSpeakers,
            v => SetOptions(Options with { FlipSpeakers = v })));
        list.AddItem(new BoolSettingMenuItem(frontEnd, 7, true, () => Options.HardwareSound,
            v => SetOptions(Options with { HardwareSound = v })));
        list.AddItem(new SettingMenuItem(frontEnd, 0xd8, true, 3, () => Options.SoundRate,
            v => SetOptions(Options with { SoundRate = v })));
        list.AddItem(new SettingMenuItem(frontEnd, 9, true, Math.Max(1, frontEnd.SoundDevices), () => Options.SoundDevice,
            v => SetOptions(Options with { SoundDevice = v })));
        list.AddItem(new SettingMenuItem(frontEnd, 0xa, true, 3,
            () => Options.Sound3DMethod == -1 ? frontEnd.Sound3DMethod : Options.Sound3DMethod,
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

    private CareerOptions Options => _frontEnd.CurrentOptions?.Invoke() ?? CareerOptions.Read(new byte[CareerOptions.Size]);

    private void SetOptions(CareerOptions options) =>
        (_frontEnd.SetOptions ?? throw new InvalidOperationException("the options cannot be changed (FrontEnd.SetOptions)"))(options);

    /// <summary>Activate (004d0ff0): open on the first list. (The briefing item is the in-game form's.)</summary>
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
    }

    /// <summary>DeActivate (004d10b0).</summary>
    public void DeActivate(bool fade)
    {
        Active = false;
        CloseTime = _clock.GetSysTimeFloat();
        NoFade = !fade;
    }

    /// <summary>Resume (004d06e0): the options page only closes (the game's form saves and unpauses).</summary>
    public void Resume() => DeActivate(true);

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
                _frontEnd.Career.Record.MusicVolume = volume;
            }
            else
            {
                _frontEnd.Career.Record.SoundVolume = volume;
            }
        }
    }

    /// <summary>ButtonPressed (004d0810): the options form's items.</summary>
    public void ButtonPressed(MenuItem item)
    {
        bool backSound = false;
        Chosen = item.Text;
        if (item.Text == _credits)
        {
            DeActivate(false);
            Result = 1;
        }
        else if (item.Text == _back)
        {
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
        else if (item.Text == _musicVolume || item.Text == _soundVolume)
        {
            return;
        }

        _frontEnd.PlaySound(backSound ? FrontEndSound.Back : FrontEndSound.Select);
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
            case Platform.Buttons.PauseMenuSelect:
                MenuOptionsList list = Lists[Page];
                ButtonPressed(list.Items[list.Selected]);
                break;
            case FrontEndButtons.MenuBack:
                if (Page > 0)
                {
                    // BackToFirstPage (the controls page's binding warning is the controls list's, not ported).
                    Page = 0;
                    Lists[0].Reset();
                    _frontEnd.PlaySound(FrontEndSound.Back);
                }
                else
                {
                    Resume();
                    _frontEnd.PlaySound(FrontEndSound.Back);
                }

                break;
        }
    }

    /// <summary>
    /// The state Render (004d11d0) changes: closed for 0.4 seconds or more, nothing; open for more than 0.4 seconds, the
    /// shown list renders. (SyncDetailItems is the display list's, not ported.)
    /// </summary>
    public void Render()
    {
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
        }
    }
}
