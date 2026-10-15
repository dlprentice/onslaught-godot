// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test that compares the parity core with the native source port's gdb logs of retail's code (draw sites, events),
/// kept in the ignored data root (ONSLAUGHT_PORT_LOGS, else the canonical checkout's local-data/parity-oracle/port-logs)
/// and the player's installation. Skipped, visibly, where either is missing.
/// </summary>
public sealed class PortLogFactAttribute : FactAttribute
{
    public PortLogFactAttribute()
    {
        if (PortLogs.Root is null)
        {
            Skip = "native-port logs not found (set ONSLAUGHT_PORT_LOGS)";
        }
        else if (GameData.Root is null)
        {
            Skip = "game installation not found (set ONSLAUGHT_GAME_DIR)";
        }
    }
}

internal static class PortLogs
{
    public static string? Root { get; } = Find();

    public static string File(string name) => Path.Combine(Root!, name);

    private static string? Find()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("ONSLAUGHT_PORT_LOGS");
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return Directory.Exists(fromEnvironment) ? fromEnvironment : null;
        }

        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "local-data", "parity-oracle", "port-logs");
            if (System.IO.File.Exists(Path.Combine(candidate, "draws-l100-load.txt")))
            {
                return candidate;
            }

            // A worktree under .worktrees/ reads the canonical checkout's data root.
            if (d.Name == ".worktrees" && d.Parent is not null)
            {
                candidate = Path.Combine(d.Parent.FullName, "local-data", "parity-oracle", "port-logs");
                if (System.IO.File.Exists(Path.Combine(candidate, "draws-l100-load.txt")))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
