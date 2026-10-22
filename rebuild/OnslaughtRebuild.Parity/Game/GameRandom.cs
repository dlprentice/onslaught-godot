// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/game.h (CGame::Random and CGame::FloatRandom), in Stuart's release.

using System.Runtime.CompilerServices;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// GAME.Random() and GAME.FloatRandom() over the game's stream. Each records its caller (the C# file and member) in the
/// stream's draw log, so a run's draw sites can be compared with a retail or native-port log draw by draw.
/// </summary>
public static class GameRandom
{
    /// <summary>CGame::Random: mRandomStream->Next().</summary>
    public static int Random(this SeedUniformRandomNumberStream stream,
        [CallerFilePath] string file = "", [CallerMemberName] string member = "") =>
        stream.Next(stream.Log is null ? "" : Site(file, member));

    /// <summary>CGame::FloatRandom: <c>((float)(Random() % 65536)) / 65536.0f</c>, a number in [0, 1).</summary>
    public static float FloatRandom(this SeedUniformRandomNumberStream stream,
        [CallerFilePath] string file = "", [CallerMemberName] string member = "") =>
        (float)(stream.Next(stream.Log is null ? "" : Site(file, member)) % 65536) / 65536.0f;

    private static string Site(string file, string member) => $"{Path.GetFileNameWithoutExtension(file)}.{member}";
}
