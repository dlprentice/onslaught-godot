// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Menus;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// The menus' host in a -level run, where no front end ran: the level's sounds and rand(), the boot's wiped CAREER, the
/// options defaultoptions.bea gave, and no save name (so the pause menu's save writes only defaultoptions.bea).
/// </summary>
public sealed class LevelMenuHost : IMenuHost
{
    private readonly ThingContext _context;

    public LevelMenuHost(ThingContext context, PlatformClock clock, OnslaughtRebuild.Parity.Career.Career career,
        Text.TextIds ids, CareerOptions options, Platform.ControlBindings bindings)
    {
        _context = context;
        Clock = clock;
        Career = career;
        Ids = ids;
        Options = options;
        Bindings = bindings;
        Machine = new MachineDisplay(clock.DisplayReset);
        clock.DisplayReset.SaveDefaultOptions = () =>
        {
            Options = Options with { DisplayModeKey = Machine.CurrentModeKey };
            SaveFromPause(); // In a -level run this writes default options only, never a named career slot.
        };
    }

    public PlatformClock Clock { get; }

    public OnslaughtRebuild.Parity.Career.Career Career { get; }

    public Text.TextIds Ids { get; }

    public CareerOptions Options { get; set; }

    public int SoundDevices => 1;

    /// <summary>SOUND.m3DSoundMethod: 2, as the sound manager chooses with a device and the options at -1.</summary>
    public int Sound3DMethod => 2;

    public Platform.ControlBindings Bindings { get; }

    /// <summary>gMouseSensitivity: the mouse look's (the level's controllers read the same shared value).</summary>
    public float MouseSensitivity
    {
        get => _context.MouseLook.Sensitivity;
        set
        {
            _context.MouseLook.Sensitivity = value;
            Options = Options with { MouseSensitivity = value };
        }
    }

    /// <summary>No save name in a -level run: a cheat is active only when every cheat is enabled, which nothing sets here.</summary>
    public bool IsCheatActive(int cheat) => false;

    public MachineDisplay Machine { get; }

    /// <summary>defaultoptions.bea as the pause menu last wrote it, or null.</summary>
    public byte[]? DefaultOptionsFile { get; private set; }

    public void PlaySound(FrontEndSound sound)
    {
        if (_context.SoundEffects is { } sounds)
        {
            if (sounds.EventKernelConfigured)
                sounds.PlayEffect(sounds.Find(FrontEnd.SoundName(sound)), null, _context.Crt, new Sound.SoundPlayback(Volume: 1));
            else sounds.PlayEffect(sounds.Find(FrontEnd.SoundName(sound)), _context.Crt);
        }
        else
        {
            _context.Skip("CFrontEnd::PlaySound");
        }
    }

    public void SaveFromPause() => DefaultOptionsFile = CareerSaveGame.Save(Career, Bindings.SaveConfigurable(), Options);

    public void SetSoundVolume(float volume)
    {
        // Both stores precede channel updates/callbacks, as in SOUND.SetMasterVolume004e04c0.
        Career.Record.SoundVolume = volume;
        if (_context.SoundEffects is { EventKernelConfigured: true } sounds) sounds.SetMasterVolume(volume);
    }
}
