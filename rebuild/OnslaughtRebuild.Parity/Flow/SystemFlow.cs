// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/System.cpp (CSystem::Run; reconstructed from the retail program, not in Stuart's release).

using OnslaughtRebuild.Parity.Clock;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>What <see cref="SystemFlow"/> runs: the front end, a level, and the attract films.</summary>
public interface ISystemParts
{
    /// <summary><c>FRONTEND.Run(entry, TRUE)</c>: -1 to quit, -3 for the attract films, or the level to play.</summary>
    int RunFrontEnd(FrontEndEntry entry);

    /// <summary><c>GAME.RunLevel(level)</c>.</summary>
    QuitType RunLevel(int level);

    /// <summary>
    /// <c>FMV.PlayFullscreen(name, ...)</c> for one attract film: true when the player skipped it. <see cref="FilmPlayer"/>
    /// gives its Presents (one before, one for each frame but the last, one after).
    /// </summary>
    bool PlayAttractFilm(string name);

    /// <summary>The <c>TWIMTBP</c> tweak: whether the graphics-vendor film plays.</summary>
    bool PlayVendorFilm { get; }
}

/// <summary>
/// <c>CSystem::Run</c> (0x004f0330) without <c>-level</c>: the front end and the levels it starts, in turn, until the
/// front end quits. The first run enters as a start; after the attract films the front end enters from the attract
/// videos; a level the player quit to the title screen returns to the main menu; a level that timed out (the
/// playable demo's inactivity) re-enters the front end as if from the attract videos, without playing them (the films
/// play only when the front end itself returns -3, its title page's timeout).
/// </summary>
/// <remarks>
/// The attract films stop at the first one the player skips: the logo, then (when not skipped) the vendor film if its
/// tweak is on, then (when neither was skipped) the opening film with its splash after 1,934 frames. Not modelled: the
/// stress test (<c>CLIPARAMS.mStressTest</c>, off in play), and the memory manager's logging.
/// </remarks>
public sealed class SystemFlow(ISystemParts parts)
{
    public const int QuitToSystem = -1;
    public const int Attract = -3;
    public const int ToTitleScreen = -69;

    private bool _firstFrontEnd = true;

    /// <summary>The front-end entries and levels in the order they ran, for comparison with retail.</summary>
    public List<string> Trace { get; } = [];

    /// <summary>Runs until the front end quits; returns the last result (-1).</summary>
    public int Run()
    {
        int result = 0;
        while (true)
        {
            FrontEndEntry entry = result != ToTitleScreen
                ? (result == Attract ? FrontEndEntry.FromAttract : FrontEndEntry.Start)
                : FrontEndEntry.TitleScreen;
            if (_firstFrontEnd)
            {
                _firstFrontEnd = false;
                entry = FrontEndEntry.Start;
            }
            Trace.Add($"front end {entry}");
            result = parts.RunFrontEnd(entry);
            if (result == QuitToSystem) break;
            if (result == Attract)
            {
                Trace.Add("attract");
                if (!parts.PlayAttractFilm("ltlogo"))
                {
                    if (!parts.PlayVendorFilm || !parts.PlayAttractFilm("TWIMTBP_GefFX_640x480_Audio"))
                        parts.PlayAttractFilm("openingfmv");
                }
            }
            else
            {
                Trace.Add($"level {result}");
                QuitType quit = parts.RunLevel(result);
                if (quit == QuitType.QuitTimeout) result = Attract;
                else if (quit == QuitType.UserQuitToTitleScreen) result = ToTitleScreen;
            }
        }
        return result;
    }
}
