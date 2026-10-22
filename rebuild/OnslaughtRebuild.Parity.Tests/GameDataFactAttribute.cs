// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test that reads the player's own installation of the game (read only, in memory): ONSLAUGHT_GAME_DIR, else the
/// Linux Steam library default. Skipped where no installation is found; nothing from it is written anywhere.
/// </summary>
public sealed class GameDataFactAttribute : FactAttribute
{
    public GameDataFactAttribute()
    {
        if (GameData.Root is null)
        {
            Skip = "game installation not found (set ONSLAUGHT_GAME_DIR)";
        }
    }
}

internal static class GameData
{
    public static string? Root { get; } = Find();

    /// <summary>A file of the installation by its game-relative path, matching names without case as Windows does.</summary>
    public static (string Name, byte[] Bytes) Read(string gamePath)
    {
        string current = Root!;
        foreach (string part in gamePath.Split('\\', '/'))
        {
            string? match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
            current = match ?? throw new FileNotFoundException(gamePath);
        }

        return (gamePath, File.ReadAllBytes(current));
    }

    private static string? Find()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("ONSLAUGHT_GAME_DIR");
        string candidate = !string.IsNullOrEmpty(fromEnvironment)
            ? fromEnvironment
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local/share/Steam/steamapps/common/Battle Engine Aquila");
        return Directory.Exists(Path.Combine(candidate, "data")) ? candidate : null;
    }
}
