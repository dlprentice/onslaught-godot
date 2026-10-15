// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins DrawLoading to the measured full-width bar bbox from
/// <c>local-lab/retail-reference-pristine/loading/07-loading-640x480.png</c>
/// and forbids the rejected DrawBar-cap dest.
///
/// <para>28e6ad93 replaced the measured x78..562 y423..447 overlay with two
/// unmodulated 64x25 <c>DrawTextureRect</c> caps of FrontEnd\BarL/BarR. Those
/// files are CFrontEnd::DrawBar header masks, not this page's sprite:
/// CConsole__RenderLoadingScreen (0x0042C810) does not call DrawBar. The
/// capture is a continuous dark overlay, not two isolated end pieces.</para>
/// </summary>
public sealed class RetailFrontendLoadingBarTests
{
    private static readonly string FlowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

    [Fact]
    public void DrawLoadingSpansTheMeasuredBarBboxAndDoesNotDrawDrawBarCaps()
    {
        string body = MethodBody(FlowSource, "DrawLoading");

        Assert.Contains(
            "new Rect2(LoadingBarLeft, LoadingBarTop, LoadingBarWidth, LoadingBarHeight)",
            body,
            StringComparison.Ordinal);
        Assert.Contains("Colors.Black", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_loadingBarL", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_loadingBarR", body, StringComparison.Ordinal);
        Assert.DoesNotContain("const float capWidth", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawLoadingNamesRenderLoadingScreenAsOwnerAndDoesNotLoadDrawBarMasks()
    {
        Assert.Contains("CConsole__RenderLoadingScreen", FlowSource, StringComparison.Ordinal);
        Assert.Contains("0x0042C810", FlowSource, StringComparison.Ordinal);
        Assert.Contains("07-loading-640x480.png", FlowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadTexture(\"bar-l\"", FlowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadTexture(\"bar-c\"", FlowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadTexture(\"bar-r\"", FlowSource, StringComparison.Ordinal);
    }

    private static string MethodBody(string source, string methodName)
    {
        Match signature = Regex.Match(
            source,
            @"private\s+void\s+" + Regex.Escape(methodName) + @"\s*\([^)]*\)\s*\{",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        Assert.True(signature.Success, $"{methodName} was not found as a private void method.");

        int open = source.IndexOf('{', signature.Index);
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[open..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"{methodName} has an unbalanced body.");
    }
}
