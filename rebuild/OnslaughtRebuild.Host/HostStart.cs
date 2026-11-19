// SPDX-License-Identifier: GPL-3.0-only
namespace OnslaughtRebuild.Host;

/// <summary>
/// The Host's start-time inputs, checked before a mode uses them, so a missing input stops the start with one plain
/// message naming what is missing and the variable that names it.
/// </summary>
internal static class HostStart
{
    /// <summary>HOST_TAPE, when set, must name an existing file.</summary>
    public static string? Tape()
    {
        string? tape = System.Environment.GetEnvironmentVariable("HOST_TAPE");
        if (tape is not null && !File.Exists(tape))
        {
            throw new ArgumentException($"HOST_TAPE names a file that does not exist: {tape}");
        }

        return tape;
    }

    /// <summary>An output file variable (HOST_CAPTURE, HOST_TRACE), when set, must be in an existing folder.</summary>
    public static string? OutputFile(string variable)
    {
        string? path = System.Environment.GetEnvironmentVariable(variable);
        if (path is not null)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
            if (!Directory.Exists(folder))
            {
                throw new ArgumentException($"{variable} is in a folder that does not exist: {folder}");
            }
        }

        return path;
    }

    /// <summary>Each HOST_SAVES file must exist.</summary>
    public static void CheckSaves(string saves)
    {
        foreach (string entry in saves.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = entry.Split('=', 2);
            if (pair.Length != 2 || !File.Exists(pair[1]))
            {
                throw new ArgumentException($"HOST_SAVES names a save that does not exist: {entry}");
            }
        }
    }

    /// <summary>The one plain line every start failure prints, whatever the mode.</summary>
    public static string FailureLine(Exception error) =>
        "HOST_START_FAILED: " + error.Message.ReplaceLineEndings(" ");
}
