// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test that needs the retail replay data of the parity harness, which lives in the canonical checkout's ignored
/// <c>local-data/link-runtime/20261015-f/</c> (or <c>ONSLAUGHT_PARITY_DATA</c>). Skipped, and reported as skipped,
/// where that data is absent.
/// </summary>
public sealed class RetailDataFactAttribute : FactAttribute
{
    public RetailDataFactAttribute()
    {
        if (RetailData.Root is null)
        {
            Skip = "retail parity data not found (set ONSLAUGHT_PARITY_DATA)";
        }
    }
}

internal static class RetailData
{
    public static string? Root { get; } = Find();

    public static string File(string relative) => Path.Combine(Root!, relative);

    private static string? Find()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("ONSLAUGHT_PARITY_DATA");
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return Directory.Exists(fromEnvironment) ? fromEnvironment : null;
        }

        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "local-data", "link-runtime", "20261015-f");
            if (Directory.Exists(Path.Combine(candidate, "golden")))
            {
                return candidate;
            }
        }

        return null;
    }
}
