// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Headless;

internal static class Program
{
    public static int Main(string[] args)
    {
        return HeadlessApplication.Run(args, Console.Out, Console.Error);
    }
}
