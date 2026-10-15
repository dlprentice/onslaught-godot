// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using System.Text.Json;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Headless;

public static class HeadlessApplication
{
    private const long MaximumTapeBytes = 8 * 1024 * 1024;
    private const long MaximumReplaySteps = 100_000;
    private const string BuiltInTapeFileName = "first-flight.v1.json";
    private const string Level100ActorManifestFileName = "level100-static-world.json";

    private sealed record Options(
        string TapePath,
        string? CompareTapePath,
        string? ExpectedTraceHash,
        int RepeatCount);

    private sealed record VerificationExpectation(
        string? TraceHash,
        string? FinalStateHash,
        string Source);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            if (args.Contains("--help", StringComparer.Ordinal))
            {
                PrintHelp(output);
                return 0;
            }

            Options options = ParseOptions(args);
            CommandTape tape = CommandTapeCodec.Deserialize(ReadTape(options.TapePath));
            CommandTape? compareTape = options.CompareTapePath is null
                ? null
                : CommandTapeCodec.Deserialize(ReadTape(options.CompareTapePath));
            ValidateWorkBudget(tape, options.RepeatCount, compareTape);
            VerificationExpectation expectation = ResolveExpectation(options, tape);
            Level100ActorDefinitionSet actorDefinitions = LoadActorDefinitions();

            ReplayComparison? tapeComparison = null;
            var determinism = new ReplayDiff(
                TraceHashMismatch: false,
                BehavioralEventMismatch: false,
                FinalStateMismatch: false,
                FirstDivergence: null);
            ReplayResult first;
            int nextRepeat;
            if (compareTape is not null)
            {
                tapeComparison = ReplayRunner.Compare(
                    tape,
                    compareTape,
                    actorDefinitions);
                first = tapeComparison.Before;
                nextRepeat = 1;
            }
            else if (options.RepeatCount >= 2)
            {
                ReplayComparison repeatedPair = ReplayRunner.Compare(
                    tape,
                    tape,
                    actorDefinitions);
                first = repeatedPair.Before;
                determinism = repeatedPair.Diff;
                nextRepeat = 2;
            }
            else
            {
                first = ReplayRunner.Run(tape, actorDefinitions);
                nextRepeat = 1;
            }

            for (int run = nextRepeat; run < options.RepeatCount; run++)
            {
                ReplayResult repeated = ReplayRunner.Run(tape, actorDefinitions);
                bool traceMismatch = !string.Equals(
                    first.TraceHash,
                    repeated.TraceHash,
                    StringComparison.Ordinal);
                bool finalStateMismatch = !string.Equals(
                    first.FinalStateHash,
                    repeated.FinalStateHash,
                    StringComparison.Ordinal);
                if (traceMismatch || finalStateMismatch)
                {
                    determinism = new ReplayDiff(
                        traceMismatch,
                        BehavioralEventMismatch: false,
                        finalStateMismatch,
                        FirstDivergence: null);
                    break;
                }
            }

            bool traceHashChecked = expectation.TraceHash is not null;
            bool? traceHashVerified = traceHashChecked
                ? string.Equals(first.TraceHash, expectation.TraceHash, StringComparison.OrdinalIgnoreCase)
                : null;
            bool finalStateHashChecked = expectation.FinalStateHash is not null;
            bool? finalStateHashVerified = finalStateHashChecked
                ? string.Equals(
                    first.FinalStateHash,
                    expectation.FinalStateHash,
                    StringComparison.OrdinalIgnoreCase)
                : null;
            var summary = new
            {
                schemaVersion = "onslaught-rebuild-headless-result.v2",
                tape = tape.Name,
                ticks = first.FinalState.Tick,
                repeats = options.RepeatCount,
                traceHash = first.TraceHash,
                finalStateHash = first.FinalStateHash,
                expectedTraceHash = expectation.TraceHash,
                expectedFinalStateHash = expectation.FinalStateHash,
                verificationSource = expectation.Source,
                traceHashChecked,
                traceHashVerified,
                finalStateHashChecked,
                finalStateHashVerified,
                mode = first.FinalState.Mode.ToString(),
                energy = first.FinalState.Energy,
                shield = first.FinalState.Shield,
                hull = first.FinalState.Hull,
                targetsDestroyed = first.FinalState.TargetsDestroyed,
                activeProjectiles = first.FinalState.Projectiles.Count,
                determinism,
                comparisonTape = compareTape?.Name,
                comparisonTraceHash = tapeComparison?.After.TraceHash,
                comparisonFinalStateHash = tapeComparison?.After.FinalStateHash,
                comparison = tapeComparison?.Diff,
            };
            output.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));

            if (determinism.TraceHashMismatch ||
                determinism.BehavioralEventMismatch ||
                determinism.FinalStateMismatch)
            {
                error.WriteLine(
                    "Determinism failure: repeated replay diverged; inspect the determinism receipt.");
                return 3;
            }

            if (traceHashVerified == false)
            {
                error.WriteLine("Replay trace hash did not match the expected value.");
                return 2;
            }

            if (finalStateHashVerified == false)
            {
                error.WriteLine("Final state hash did not match the expected value.");
                return 2;
            }

            if (tapeComparison is not null &&
                (tapeComparison.Diff.TraceHashMismatch ||
                    tapeComparison.Diff.BehavioralEventMismatch ||
                    tapeComparison.Diff.FinalStateMismatch))
            {
                error.WriteLine(
                    "Replay comparison diverged; inspect the comparison receipt.");
                return 4;
            }

            return 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static Options ParseOptions(string[] args)
    {
        string tapePath = ResolveDefaultTapePath();
        string? compareTapePath = null;
        string? expectedTraceHash = null;
        int repeatCount = 2;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--tape":
                    tapePath = RequireValue(args, ref index, "--tape");
                    break;
                case "--compare-tape":
                    compareTapePath = RequireValue(args, ref index, "--compare-tape");
                    break;
                case "--expect":
                    expectedTraceHash = RequireValue(args, ref index, "--expect");
                    ValidateHashArgument(expectedTraceHash);
                    break;
                case "--repeat":
                    string repeatText = RequireValue(args, ref index, "--repeat");
                    if (!int.TryParse(repeatText, out repeatCount) || repeatCount is < 1 or > 1_000)
                    {
                        throw new ArgumentException("--repeat must be an integer from 1 through 1000.");
                    }
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        return new Options(tapePath, compareTapePath, expectedTraceHash, repeatCount);
    }

    private static VerificationExpectation ResolveExpectation(Options options, CommandTape tape)
    {
        if (options.ExpectedTraceHash is not null)
        {
            return new VerificationExpectation(
                options.ExpectedTraceHash,
                tape.ExpectedFinalStateHash,
                "command-line");
        }

        if (tape.ExpectedTraceHash is not null || tape.ExpectedFinalStateHash is not null)
        {
            return new VerificationExpectation(
                tape.ExpectedTraceHash,
                tape.ExpectedFinalStateHash,
                "command-tape");
        }

        return new VerificationExpectation(null, null, "none");
    }

    private static void ValidateWorkBudget(
        CommandTape tape,
        int repeatCount,
        CommandTape? compareTape)
    {
        long requestedSteps = checked(
            ((long)tape.DurationTicks * repeatCount) +
            (compareTape?.DurationTicks ?? 0));
        if (requestedSteps > MaximumReplaySteps)
        {
            throw new ArgumentException(
                $"Replay request exceeds the {MaximumReplaySteps.ToString("N0", CultureInfo.InvariantCulture)} total-step limit.");
        }
    }

    private static void ValidateHashArgument(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "--expect must be a 64-character SHA-256 replay trace hash.");
        }
    }

    private static string ReadTape(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4_096,
            FileOptions.SequentialScan);
        if (stream.Length > MaximumTapeBytes)
        {
            throw new InvalidDataException("Command tape exceeds the 8 MiB input limit.");
        }

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4_096,
            leaveOpen: false);
        return reader.ReadToEnd();
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        index++;
        if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[index];
    }

    private static string ResolveDefaultTapePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "scenarios", BuiltInTapeFileName);
    }

    private static Level100ActorDefinitionSet LoadActorDefinitions()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Level100",
            "StaticWorld",
            Level100ActorManifestFileName);
        return Level100ActorDefinitionManifest.Decode(File.ReadAllBytes(path));
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("OnslaughtRebuild.Headless");
        output.WriteLine("  --tape <path>   Command tape (default: packaged first-flight scenario)");
        output.WriteLine("  --compare-tape <path>  Optional second tape for first-divergence receipt");
        output.WriteLine("  --expect <hex>  Optional expected SHA-256 replay trace hash");
        output.WriteLine("  --repeat <n>    Replay count (default: 2; 100,000 total-step limit)");
    }
}
