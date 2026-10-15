// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using OnslaughtRebuild.Parity;
using OnslaughtRebuild.Parity.Clock;

namespace OnslaughtRebuild.Headless;

/// <summary>
/// <c>OnslaughtRebuild.Headless parity</c>: plays a retail replay tape into a parity simulation, records its probe
/// trace and compares it with a retail golden trace. Exit code 0 means 1:1, 2 means a measured difference.
/// </summary>
public static class ParityCommand
{
    private const long MaximumFrames = 1_000_000;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? tapePath = null, goldenPath = null, outPath = null, simulationName = "unimplemented";
        long frames = 10_000, frameOffset = 0, levelStart = 3100;
        long? baseRead = null;
        int level = 100;
        int every = 50;
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--tape": tapePath = Value(args, ref i); break;
                    case "--golden": goldenPath = Value(args, ref i); break;
                    case "--out": outPath = Value(args, ref i); break;
                    case "--sim": simulationName = Value(args, ref i); break;
                    case "--frames": frames = Number(args, ref i, 1, MaximumFrames); break;
                    case "--every": every = (int)Number(args, ref i, 1, 1_000_000); break;
                    case "--frame-offset": frameOffset = Number(args, ref i, -MaximumFrames, MaximumFrames); break;
                    case "--level": level = (int)Number(args, ref i, 0, 999); break;
                    case "--level-start": levelStart = Number(args, ref i, 1, MaximumFrames); break;
                    case "--base-read": baseRead = Number(args, ref i, 1, MaximumFrames); break;
                    case "--help": PrintHelp(output); return 0;
                    default: throw new ArgumentException($"Unknown parity argument: {args[i]}");
                }
            }

            if (tapePath is null)
            {
                throw new ArgumentException("parity needs --tape <retail tape>.");
            }

            RetailTape tape = RetailTape.Load(tapePath);
            IParitySimulation simulation = simulationName switch
            {
                "unimplemented" => new UnimplementedSimulation(),
                "clock" => new ClockSimulation(level, baseRead ?? levelStart, levelStart),
                _ => throw new ArgumentException($"Unknown parity simulation '{simulationName}'."),
            };

            ProbeTrace trace = ParityRunner.Run(simulation, tape, frames, every);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"tape {tapePath} sha256 {tape.Sha256}: {tape.Events.Count} events, frames {tape.FirstFrame}..{tape.LastFrame}"));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"simulation {simulationName}: {frames} Presents, {trace.SampleCount} samples every {every}"));
            if (outPath is not null)
            {
                using var writer = new StreamWriter(outPath);
                trace.Write(writer);
            }

            if (goldenPath is null)
            {
                return 0;
            }

            ProbeTrace golden = ProbeTrace.Load(goldenPath);
            var comparison = TraceComparison.Compare(golden, trace, frameOffset);
            output.WriteLine($"golden {goldenPath}");
            if (TraceComparison.FirstTickSample(golden) is { } g)
            {
                output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"golden first sample with event time: frame {g.Frame} ({g.Ticks} ticks)"));
            }

            output.Write(comparison.Report(golden, trace));
            return comparison.IsOneToOne ? 0 : 2;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            error.WriteLine(e.Message);
            return 1;
        }
    }

    private static string Value(string[] args, ref int i)
    {
        if (++i >= args.Length || args[i].Length == 0)
        {
            throw new ArgumentException($"{args[i - 1]} requires a value.");
        }

        return args[i];
    }

    private static long Number(string[] args, ref int i, long min, long max)
    {
        string option = args[i];
        if (!long.TryParse(Value(args, ref i), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long v) || v < min || v > max)
        {
            throw new ArgumentException($"{option} must be an integer from {min} through {max}.");
        }

        return v;
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("OnslaughtRebuild.Headless parity --tape <retail tape> [--golden <probes>] [--out <trace>]");
        output.WriteLine("  [--frames 10000] [--every 50] [--frame-offset 0] [--sim unimplemented|clock]");
        output.WriteLine("  clock (M2: EVT, FT and LVL only): [--level 100] [--level-start 3100] [--base-read <level start>]");
        output.WriteLine("  Exit 0 = every probe equal at every golden sample with no frame offset (1:1); 2 = differences; 1 = error.");
    }
}
