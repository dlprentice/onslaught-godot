// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Headless;

internal static class Program
{
    public static int Main(string[] args)
    {
        return args.Length > 0 && args[0] == "parity"
            ? ParityCommand.Run(args[1..], Console.Out, Console.Error)
            : HeadlessApplication.Run(args, Console.Out, Console.Error);
    }
}
