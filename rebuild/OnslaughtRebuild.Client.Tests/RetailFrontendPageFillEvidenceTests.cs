// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the frontend page background as retail's THREE terms — clear, darkener,
/// FEBack128 video — so that a later "the frontend is showing the raw D3D clear
/// colour, make it flat" pass cannot delete the video underlay.
///
/// <para><b>This test exists because that pass was proposed.</b> Task #147
/// measured retail's main menu as 73.50% one flat colour (23,23,48) against our
/// 3.56%, measured our options root's modal as (31,32,63) — bit-identical to the
/// <c>Clear(0x001F1F3F)</c> immediate at <c>CDXFrontEnd__RenderStart</c>
/// VA 0x00540F88 in the pristine specimen <c>BEA.exe.original.backup</c>
/// (74154bfa) — and concluded we were letting the clear show through where
/// retail covers it. Every number in it reproduces. The conclusion does not.</para>
///
/// <para><b>Both retail references are <c>-skipfmv</c> captures.</b> That switch
/// suppresses the FEBack128 video, and the video is what covers the page. Retail
/// captured WITHOUT it
/// (<c>local-lab/retail-reference-pristine/nofmv-frontend-2026-07-26/</c>) is not
/// flat either — over run1's fifteen settled main-menu frames the modal colour
/// holds only 1.36%..3.57% of the frame across 34,795..39,593 distinct colours,
/// and <c>mm-t001020ms.png</c> reads modal (30,31,60) at 3.57% against our own
/// (30,31,60) at 3.56%. Retail's video-on frames also produce modal (31,32,63)
/// in their own right (<c>mm-t006031ms.png</c>, 2.17%), so the options-root
/// coincidence with the clear colour is the video composite's modal, not leaked
/// clear. The 73% flat frame is real and retail does render it — for the ~420 ms
/// before the video becomes visible, which is why retail's own
/// <c>mm-t000014ms.png</c> reads (23,23,48) at 80.08%.</para>
///
/// <para><b>What is asserted is the mechanism, not a pixel score.</b> The
/// #147 fix would have had to do one of exactly three things to make the page
/// flat: change the clear, change the darkener, or stop drawing the strip. One
/// test guards each, plus the gains, because zeroing those is the fourth way to
/// reach the same wrong place.</para>
/// </summary>
public sealed class RetailFrontendPageFillEvidenceTests
{
    private static readonly string FlowSource = ReadGodotSource("RetailFrontendFlow.cs");
    private static readonly string OptionsSource = ReadGodotSource("RetailFrontendFlow.Options.cs");

    private static string ReadGodotSource(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "godot-pause-source", fileName));

    /// <summary>
    /// Retail's page fill is two draws, and the reconstruction keeps them as two
    /// so that neither input can drift silently behind a baked answer.
    ///
    /// <para>Term 1 is <c>Clear(0x001F1F3F)</c> = RGB(31,31,63). Term 2 is draw 0
    /// of every frontend page: a full-screen quad, diffuse <c>0x3E000000</c>,
    /// SRCALPHA/INVSRCALPHA, whose stage-0 COLOROP is MODULATE(TEXTURE, DIFFUSE)
    /// with BLACK diffuse RGB and whose stage-0 ALPHAOP is DISABLE — so it is
    /// black at a flat 0x3E/255, whatever its texture holds
    /// (<c><maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\inventories\main-menu-settled.csv</c>,
    /// frame 3000, draw 0).</para>
    ///
    /// <para>The arithmetic is the falsifiable part: it has to land on the
    /// (23,23,48) that retail's <c>-skipfmv</c> frames actually read.</para>
    /// </summary>
    [Fact]
    public void PageFillIsRetailsTwoTermsAndComposesToTheMeasuredFlatFill()
    {
        int[] clear = ParseByteColor(FlowSource, "FrontendClearColor");
        Assert.Equal([31, 31, 63], clear);

        int darkenerAlpha = ParseDarkenerAlpha(FlowSource);
        Assert.Equal(0x3E, darkenerAlpha);

        // SRCALPHA/INVSRCALPHA of black over the clear, rounded as the device
        // rounds on the write to an 8-bit target.
        double keep = (255d - darkenerAlpha) / 255d;
        int[] composed = [.. clear.Select(c => (int)Math.Round(c * keep, MidpointRounding.AwayFromZero))];
        Assert.Equal([23, 23, 48], composed);

        // The baked constant the FEBack strip is added to must BE that composite.
        Assert.Equal(composed, ParseByteColor(FlowSource, "MainUnderlayFallback"));
    }

    /// <summary>
    /// Every frontend page renderer composites the underlay. Retail draws the
    /// video on the non-main pages too: its own no-<c>-skipfmv</c> devselect
    /// frames (<c>run1/ds-t003507ms.png</c>) carry the video plainly, at 11,283
    /// to 13,174 distinct colours against a flat page's ~1.
    /// </summary>
    [Theory]
    [InlineData("DrawMainMenu")]
    [InlineData("DrawDevSelect")]
    [InlineData("DrawLevelSelect")]
    [InlineData("DrawOptions")]
    public void EveryFrontendPageRendererCompositesTheUnderlay(string renderer)
    {
        string source = renderer == "DrawOptions" ? OptionsSource : FlowSource;
        Assert.Contains("DrawMainUnderlay(", MethodBody(source, renderer), StringComparison.Ordinal);
    }

    /// <summary>
    /// The underlay draws the strip, not just the flat fill. This is the exact
    /// edit #147 would have produced — keep the two fill rects, drop the video —
    /// and it is the one that has to stay red.
    /// </summary>
    [Fact]
    public void TheUnderlayDrawsTheFeBack128StripOverTheFill()
    {
        string body = MethodBody(FlowSource, "DrawMainUnderlay");

        Assert.Contains("FrontendClearColor", body, StringComparison.Ordinal);
        Assert.Contains("FrontendFillDarkener", body, StringComparison.Ordinal);
        Assert.Contains("DrawTextureRect(", body, StringComparison.Ordinal);
        Assert.Contains("_feBackFrames[frame]", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The additive gains stay inside the band the per-frame least-squares fits
    /// actually produced against retail's video-on main menu (run1 frames
    /// mm-t001020ms..mm-t007027ms, scored on the 222,683 pixels the
    /// <c>-skipfmv</c> capture proves are pure underlay). Zeroing these is the
    /// fourth route to a flat page and would leave every other assertion green.
    /// </summary>
    [Theory]
    [InlineData(0, 0.2523, 0.2687)]
    [InlineData(1, 0.2456, 0.2674)]
    [InlineData(2, 0.2290, 0.2467)]
    public void FeBackUnderlayGainStaysInsideTheMeasuredPerFrameBand(int channel, double low, double high)
    {
        Match match = Regex.Match(
            FlowSource,
            @"FeBackUnderlayGain\s*=\s*\[(?<values>[^\]]+)\]",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        Assert.True(match.Success, "FeBackUnderlayGain declaration was not found.");

        double[] gains =
        [
            .. match.Groups["values"].Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v.TrimEnd('f'), CultureInfo.InvariantCulture)),
        ];

        Assert.Equal(3, gains.Length);
        Assert.InRange(gains[channel], low, high);
    }

    /// <summary>Reads a <c>new(r / 255f, g / 255f, b / 255f, 1f)</c> declaration back as bytes.</summary>
    private static int[] ParseByteColor(string source, string name)
    {
        Match match = Regex.Match(
            source,
            name + @"\s*=\s*new\(\s*(?<r>\d+)f?\s*/\s*255f\s*,\s*(?<g>\d+)f?\s*/\s*255f\s*,\s*(?<b>\d+)f?\s*/\s*255f",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        Assert.True(match.Success, $"{name} was not declared as a /255f byte colour.");

        return
        [
            int.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture),
        ];
    }

    /// <summary>Reads the darkener's hex alpha byte back out of its declaration.</summary>
    private static int ParseDarkenerAlpha(string source)
    {
        Match match = Regex.Match(
            source,
            @"FrontendFillDarkener\s*=\s*new\(\s*0f\s*,\s*0f\s*,\s*0f\s*,\s*0x(?<a>[0-9A-Fa-f]{2})u?\s*/\s*255f\s*\)",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        Assert.True(match.Success, "FrontendFillDarkener was not declared as black at a hex alpha over 255.");

        return int.Parse(match.Groups["a"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>Brace-matched body of a private void method, by name.</summary>
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
