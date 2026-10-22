// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/ltshell.cpp (WinMain's options load, after CLIPARAMS.GetParams; DEFAULT_OPTIONS_NAME
// 0063db18), in Stuart's release.

using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Career;

/// <summary>
/// The saved options at boot: WinMain reads <c>defaultoptions.bea</c> (a career-shaped file; <c>-defaultoptionsname</c>
/// renames it) and, when the whole file reads, loads it as a career with its bindings and options. Then the career is
/// wiped (<c>CAREER.Blank()</c>), loaded or not.
/// </summary>
public static class DefaultOptions
{
    /// <summary><c>DEFAULT_OPTIONS_NAME</c>'s initial value.</summary>
    public const string FileName = "defaultoptions.bea";

    /// <summary>
    /// The boot load: <paramref name="file"/> is the file's bytes (null when it does not open). A short read or a wrong
    /// version leaves the bindings and options as they were. Returns the loaded options (<c>OPTIONS_LOADED</c>), or null.
    /// </summary>
    public static CareerOptions? Load(Career career, ControlBindings bindings, byte[]? file)
    {
        CareerOptions? loaded = null;
        if (file is not null && file.Length >= CareerSaveGame.Size)
        {
            CareerLoadResult result = CareerSaveGame.Load(career, file.AsSpan(0, CareerSaveGame.Size), suggestNextLevel: false);
            if (result.Loaded)
            {
                bindings.LoadConfigurable(result.Bindings!);
                loaded = result.Options;
                // CCareer::ReadOptions (00420d70) applies the saved preset after CCareer::Load copied the raw
                // bindings. With the current host's no-enabled-joystick state, zero preserves those bytes and
                // every signed nonzero value applies the keyboard table. An ordinary career selection uses
                // suggestNextLevel and never reads/applies these options.
                bindings.ApplyPreset(loaded!.ControlPreset);
            }
        }

        career.Blank();
        return loaded;
    }
}
