// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/Career.cpp (level_structure, UpdateGoodieStates' episode lists) and src/FEPGoodies.cpp
// (the goodies[] thresholds). The two slot numbers are the game's data/MissionScripts/onsldef.msl definitions.

namespace OnslaughtRebuild.Parity.Career;

/// <summary>The fixed tables the career logic reads.</summary>
public static class CareerTables
{
    /// <summary>
    /// <c>level_structure</c>: per campaign node, its world number, the indices of its lower and higher child nodes,
    /// and the worlds whose base-things list is updated when it is won (always, and with every secondary objective).
    /// </summary>
    public static readonly int[,] LevelStructure =
    {
        { 100, 1, -1, 110, -1 }, { 110, 2, -1, -1, -1 }, { 200, 3, 4, 211, 212 }, { 211, 5, 6, 231, 232 },
        { 212, 5, 6, 231, 232 }, { 221, 7, 8, -1, -1 }, { 222, 7, 8, -1, -1 }, { 231, 9, -1, -1, -1 },
        { 232, 9, -1, -1, -1 }, { 300, 10, 11, 311, 312 }, { 311, 12, 13, 321, 322 }, { 312, 12, 13, 321, 322 },
        { 321, 14, 15, -1, -1 }, { 322, 14, 15, -1, -1 }, { 331, 16, -1, -1, -1 }, { 332, 16, -1, -1, -1 },
        { 400, 17, 18, 411, 412 }, { 411, 19, 20, 431, 432 }, { 412, 19, 20, 431, 432 }, { 421, 21, 22, -1, -1 },
        { 422, 21, 22, -1, -1 }, { 431, 23, -1, -1, -1 }, { 432, 23, -1, -1, -1 }, { 500, 24, 25, -1, -1 },
        { 511, 26, 27, -1, -1 }, { 512, 28, 29, -1, -1 }, { 521, 30, -1, -1, -1 }, { 522, 30, -1, -1, -1 },
        { 523, 30, -1, -1, -1 }, { 524, 30, -1, -1, -1 }, { 600, 31, 32, -1, -1 }, { 611, 33, 34, 621, 622 },
        { 612, 33, 34, 621, 622 }, { 621, 35, -1, -1, -1 }, { 622, 35, -1, -1, -1 }, { 700, 36, -1, -1, -1 },
        { 710, 37, -1, 720, -1 }, { 720, 38, 39, 731, 732 }, { 731, 40, -1, -1, -1 }, { 732, 41, -1, -1, -1 },
        { 741, -1, -1, -1, -1 }, { 742, 42, -1, -1, -1 }, { 800, -1, -1, -1, -1 },
    };

    /// <summary>The worlds in campaign-node order, which the concept-art goodies follow.</summary>
    public static readonly int[] ConceptArtWorlds =
    [
        100, 110, 200, 211, 212, 221, 222, 231, 232, 300, 311, 312, 321, 322, 331, 332, 400, 411, 412, 421, 422,
        431, 432, 500, 511, 512, 521, 522, 523, 524, 600, 611, 612, 621, 622, 700, 710, 720, 731, 732, 741, 742, 800,
    ];

    /// <summary><c>SLOT_500_ROCKET</c> and <c>SLOT_500_SUB</c>: which way level 500 was finished.</summary>
    public const int Slot500Rocket = 61;
    public const int Slot500Sub = 62;

    /// <summary>The goodies each episode marks "hint shown" once it is available (episodes 1 to 8).</summary>
    public static readonly int[][] EpisodeInstructions =
    [
        [0, 1, 8, 14, 33, 36, 41, 42, 43],
        [2, 3, 9, 12, 19, 21, 23, 26, 27, 28, 30, 31, 32, 35, 38, 40],
        [4, 5, 13, 17, 22, 24, 29, 34, 39, 43, 45, 47, 53, 54, 55, 56, 57, 58],
        [10, 16, 25, 44, 48, 52, 59],
        [18, 46, 49, 50, 51, 52, 60, 61, 62],
        [6, 15, 20, 37, 63, 64],
        [7, 11, 64, 66, 67, 68, 69, 70, 71, 74, 75, 76, 77, 78],
        [65, 72, 73],
    ];

    // goodies[i].GetNumber() and GetNumber2() for the entries the career rules read; every other entry is -1.
    private static readonly Dictionary<int, (int Number, int Number2)> GoodieNumbers = new()
    {
        [33] = (40, -1), [34] = (160, -1), [35] = (80, -1), [36] = (25, -1), [37] = (100, -1), [38] = (50, -1),
        [39] = (75, -1), [40] = (25, 80), [41] = (50, 100), [42] = (100, -1), [43] = (400, -1), [44] = (300, -1),
        [45] = (200, -1), [47] = (20, -1), [48] = (40, -1), [49] = (80, -1), [50] = (25, -1), [51] = (40, -1),
        [53] = (50, 25), [54] = (50, -1), [55] = (25, -1), [56] = (75, 100), [57] = (25, 25), [63] = (100, -1),
        [66] = (26, -1), [74] = (20, -1), [75] = (40, -1), [76] = (43, -1), [77] = (43, -1),
    };

    public static int GoodieNumber(int goodie) => GoodieNumbers.TryGetValue(goodie, out var numbers) ? numbers.Number : -1;

    public static int GoodieNumber2(int goodie) => GoodieNumbers.TryGetValue(goodie, out var numbers) ? numbers.Number2 : -1;
}
