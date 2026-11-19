// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Host;

/// <summary>
/// The player's own installation, read by game-relative path with names matched without case, as Windows does. The
/// directory is ONSLAUGHT_GAME_DIR, or the Linux Steam library's default.
/// </summary>
public sealed class GameFiles(string root)
{
    public string Root { get; } = root;

    public static GameFiles? Find()
    {
        string? dir = System.Environment.GetEnvironmentVariable("ONSLAUGHT_GAME_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".local/share/Steam/steamapps/common/Battle Engine Aquila");
        }

        return Directory.Exists(dir) ? new GameFiles(dir) : null;
    }

    /// <summary>The installation, or a start failure naming the folder looked in and ONSLAUGHT_GAME_DIR.</summary>
    public static GameFiles Require()
    {
        if (Find() is { } files)
        {
            return files;
        }

        string? dir = System.Environment.GetEnvironmentVariable("ONSLAUGHT_GAME_DIR");
        throw new InvalidOperationException(string.IsNullOrEmpty(dir)
            ? "the game installation was not found in the Steam library's default folder; set ONSLAUGHT_GAME_DIR to the game's folder"
            : $"ONSLAUGHT_GAME_DIR names a folder that does not exist: {dir}");
    }

    public (string Name, byte[] Bytes) Read(string gamePath)
    {
        string current = Root;
        foreach (string part in gamePath.Split('\\', '/'))
        {
            string? match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
            current = match ?? throw new FileNotFoundException($"{gamePath} is missing from the game installation at {Root} (ONSLAUGHT_GAME_DIR)", gamePath);
        }

        return (Path.GetFileName(current), File.ReadAllBytes(current));
    }
}
