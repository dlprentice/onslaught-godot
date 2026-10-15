// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Godot-host boundary for explicitly selected career bytes. It recognizes only
/// <c>--career-save=&lt;path&gt;</c>, reads those named files in argument order, and
/// performs no directory or installed-save discovery. It never writes.
/// </summary>
public static class RetailCareerLoadAdapter
{
    public const string CareerSaveArgumentPrefix = "--career-save=";

    public static IReadOnlyList<RetailCareerDescriptor> ReadExplicitSelections(
        IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var descriptors = new List<RetailCareerDescriptor>();
        foreach (string argument in arguments)
        {
            if (!argument.StartsWith(CareerSaveArgumentPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string selectedPath = argument[CareerSaveArgumentPrefix.Length..];
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                throw new ArgumentException(
                    "--career-save requires an explicitly selected file path.",
                    nameof(arguments));
            }

            string fullPath = Path.GetFullPath(selectedPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            RetailCareerSave career = RetailCareerSaveCodec.Read(bytes);
            descriptors.Add(new RetailCareerDescriptor(
                SlotNumber: null,
                Name: Path.GetFileNameWithoutExtension(fullPath),
                Career: career));
        }

        return descriptors.AsReadOnly();
    }
}
