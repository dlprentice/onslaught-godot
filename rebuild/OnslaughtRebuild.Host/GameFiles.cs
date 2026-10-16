// SPDX-License-Identifier: GPL-3.0-or-later

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

    public (string Name, byte[] Bytes) Read(string gamePath)
    {
        string current = Root;
        foreach (string part in gamePath.Split('\\', '/'))
        {
            string? match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
            current = match ?? throw new FileNotFoundException(gamePath);
        }

        return (Path.GetFileName(current), File.ReadAllBytes(current));
    }
}
