// SPDX-License-Identifier: GPL-3.0-or-later

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
public sealed class LevelMenuHost(ThingContext context, PlatformClock clock, OnslaughtRebuild.Parity.Career.Career career,
    Text.TextIds ids, CareerOptions options, Platform.ControlBindings bindings) : IMenuHost
{
    public PlatformClock Clock => clock;

    public OnslaughtRebuild.Parity.Career.Career Career => career;

    public Text.TextIds Ids => ids;

    public CareerOptions Options { get; set; } = options;

    public int SoundDevices => 1;

    public int Sound3DMethod => 0;

    /// <summary>defaultoptions.bea as the pause menu last wrote it, or null.</summary>
    public byte[]? DefaultOptionsFile { get; private set; }

    public void PlaySound(FrontEndSound sound)
    {
        if (context.SoundEffects is { } sounds)
        {
            sounds.PlayEffect(sounds.Find(FrontEnd.SoundName(sound)), context.Crt);
        }
        else
        {
            context.Skip("CFrontEnd::PlaySound");
        }
    }

    public void SaveFromPause() => DefaultOptionsFile = CareerSaveGame.Save(career, bindings.SaveConfigurable(), Options);
}
