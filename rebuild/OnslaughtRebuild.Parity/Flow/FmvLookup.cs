// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/fmvlookup.cpp (fmv_lookup at 0063fd70, lookup_FMV 00523120) and src/game.cpp
// (MakeCutsceneName 0046d810). Reconstructed; not in Stuart's release.

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>Which cutscene plays around each level, and the file a cutscene is played from.</summary>
public static class FmvLookup
{
    /// <summary>fmv_lookup: each level's intro, outro and alternative outro cutscene (-1 for none).</summary>
    private static readonly (int Level, int Intro, int Outro, int AltOutro)[] Table =
    [
        (100, 1, 2, -1), (110, 3, 4, -1), (200, 5, 6, -1), (211, -1, -1, -1), (212, -1, -1, -1),
        (221, 7, -1, -1), (222, 7, -1, -1), (231, 8, 9, -1), (232, 8, 9, -1), (300, 10, -1, -1),
        (311, -1, -1, -1), (312, -1, -1, -1), (321, -1, -1, -1), (322, -1, -1, -1), (331, 11, 12, -1),
        (332, 11, 12, -1), (400, -1, -1, -1), (411, -1, -1, -1), (412, -1, -1, -1), (421, 13, -1, -1),
        (422, 13, -1, -1), (431, -1, -1, -1), (432, -1, -1, -1), (500, -1, 14, 15), (511, 16, -1, -1),
        (512, 17, 18, -1), (521, 19, -1, -1), (522, 19, -1, -1), (523, 20, -1, -1), (524, 20, -1, -1),
        (600, 21, -1, -1), (611, 22, -1, -1), (612, 22, -1, -1), (621, 23, 24, -1), (622, 23, 24, -1),
        (700, 25, -1, -1), (710, -1, -1, -1), (720, 26, 28, 27), (731, -1, -1, -1), (732, -1, -1, -1),
        (741, -1, 29, 32), (742, -1, 30, -1), (800, 31, 33, -1),
    ];

    /// <summary>lookup_FMV: <paramref name="which"/> 1 the outro, 2 the alternative outro, anything else the intro.</summary>
    public static int Of(int level, int which)
    {
        foreach ((int l, int intro, int outro, int alt) in Table)
        {
            if (l == level)
            {
                return which switch { 1 => outro, 2 => alt, _ => intro };
            }
        }

        return -1;
    }

    /// <summary>
    /// MakeCutsceneName: <c>cutscenes\NN</c> when <c>data\video\cutscenes\NN.vid</c> opens, else <c>cutscenes\NN_small</c>.
    /// </summary>
    public static string CutsceneName(int fmv, IFilmFiles files)
    {
        string name = $@"cutscenes\{fmv:00}";
        return files.Probe(name) is null ? name + "_small" : name;
    }
}
