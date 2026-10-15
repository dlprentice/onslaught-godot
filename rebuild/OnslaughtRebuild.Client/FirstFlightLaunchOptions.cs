// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>Launch argument admission, before any media or career files are opened.</summary>
public sealed record FirstFlightLaunchOptions(
    bool Smoke,
    string? ReportPath,
    bool CaptureArgumentsPresent,
    bool SkipStartupMedia,
    bool ForceStartupMedia,
    string? RecordTapePath)
{
    public static FirstFlightLaunchOptions Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        bool smoke = false, capture = false, skip = false, intro = false;
        string? report = null, tape = null;
        foreach (string argument in arguments)
        {
            if (argument is null)
                throw new ArgumentException("First Flight arguments cannot contain null entries.", nameof(arguments));
            if (argument == "--smoke") smoke = true;
            else if (argument == "--skipfmv") skip = true;
            else if (argument == "--intro") intro = true;
            else if (argument.StartsWith("--report=", StringComparison.Ordinal))
                report = ReadSingleDestination(argument, "--report=", report);
            else if (argument.StartsWith("--record-tape=", StringComparison.Ordinal))
                tape = ReadSingleDestination(argument, "--record-tape=", tape);
            else if (argument.StartsWith("--capture-dir=", StringComparison.Ordinal) ||
                     argument.StartsWith("--capture-plan=", StringComparison.Ordinal) ||
                     argument.StartsWith("--capture-size=", StringComparison.Ordinal) ||
                     argument.StartsWith("--capture-offsets-ms=", StringComparison.Ordinal))
                capture = true;
            else if (argument.StartsWith("--startup-media=", StringComparison.Ordinal) ||
                     argument.StartsWith("--career-save=", StringComparison.Ordinal))
            {
                // Their existing adapters own path/content validation and file reads.
            }
            else
                throw new ArgumentException($"Unknown First Flight argument '{argument}'.");
        }

        // FirstFlightGame and FrontendCaptureRig each drive the frontend and
        // request application exit. They cannot own one run simultaneously.
        if (smoke && capture)
            throw new ArgumentException("Smoke mode and capture arguments cannot be used together.");
        if (smoke && (string.IsNullOrWhiteSpace(report) || !Path.IsPathFullyQualified(report)))
            throw new ArgumentException("Smoke mode requires an absolute --report path.");
        if (tape is not null &&
            (!Path.IsPathFullyQualified(tape) ||
             !string.Equals(Path.GetExtension(tape), ".json", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException(
                "--record-tape requires an absolute .json path outside career-save and retail storage.");

        return new(smoke, report, capture, skip, intro, tape);
    }

    private static string ReadSingleDestination(string argument, string prefix, string? previous)
    {
        // Do not silently redirect an explicit output or hide an earlier bad
        // value behind a later one. Repeated career selections remain legal.
        if (previous is not null)
            throw new ArgumentException($"{prefix[..^1]} may only be specified once.");
        return argument[prefix.Length..];
    }
}
