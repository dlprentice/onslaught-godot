// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client.Tests;

public sealed class FirstFlightLaunchOptionsTests
{
    [Fact]
    public void NormalLaunchAdmitsRepeatedCareerSelectionsAndStartupMediaWithoutReadingThem()
    {
        FirstFlightLaunchOptions options = FirstFlightLaunchOptions.Parse(
            ["--career-save=/missing/first.bes", "--career-save=/missing/second.bes",
             "--startup-media=/missing/media"]);
        Assert.False(options.Smoke);
        Assert.False(options.SkipStartupMedia);
        Assert.False(options.CaptureArgumentsPresent);
        Assert.Null(options.RecordTapePath);
    }

    [Fact]
    public void LaunchKeepsRecordingAndMediaOptionsIndependent()
    {
        string tape = Path.GetFullPath("recording.json");
        FirstFlightLaunchOptions options = FirstFlightLaunchOptions.Parse(
            ["--skipfmv", "--intro", $"--record-tape={tape}", "--capture-plan=mainmenu"]);
        Assert.Equal(tape, options.RecordTapePath);
        Assert.True(options.SkipStartupMedia);
        Assert.True(options.ForceStartupMedia);
        Assert.True(options.CaptureArgumentsPresent);
    }

    [Theory]
    [InlineData("--career-svae=/missing/save.bes")]
    [InlineData("/missing/save.bes")]
    [InlineData("--post-mission-event=Won")]
    public void UnknownArgumentsRemainRefused(string argument) =>
        Assert.Throws<ArgumentException>(() => FirstFlightLaunchOptions.Parse([argument]));

    [Theory]
    [InlineData("--record-tape=relative.json")]
    [InlineData("--record-tape=/tmp/save.bes")]
    [InlineData("--record-tape=")]
    public void RecordingRequiresAnExplicitJsonDestination(string argument) =>
        Assert.Throws<ArgumentException>(() => FirstFlightLaunchOptions.Parse([argument]));

    [Fact]
    public void SmokeRequiresAnAbsoluteReportDestination()
    {
        Assert.Throws<ArgumentException>(() => FirstFlightLaunchOptions.Parse(["--smoke"]));
        Assert.Throws<ArgumentException>(() => FirstFlightLaunchOptions.Parse(
            ["--smoke", "--report=relative.json"]));
        string report = Path.GetFullPath("smoke.json");
        FirstFlightLaunchOptions options = FirstFlightLaunchOptions.Parse(
            ["--smoke", $"--report={report}"]);
        Assert.True(options.Smoke);
        Assert.Equal(report, options.ReportPath);
    }

    [Theory]
    [InlineData("--capture-dir=/missing/capture")]
    [InlineData("--capture-plan=mainmenu")]
    [InlineData("--capture-size=960x720")]
    [InlineData("--capture-offsets-ms=0,100")]
    public void SmokeAndCaptureDriversCannotOwnTheSameRun(string captureArgument)
    {
        string report = $"--report={Path.GetFullPath("smoke.json")}";
        foreach (string[] arguments in new[]
                 {
                     new[] { "--smoke", report, captureArgument },
                     new[] { captureArgument, report, "--smoke" },
                 })
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => FirstFlightLaunchOptions.Parse(arguments));
            Assert.Contains("Smoke mode and capture", error.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("--report=", false)]
    [InlineData("--report=", true)]
    [InlineData("--record-tape=", false)]
    [InlineData("--record-tape=", true)]
    public void OutputDestinationsCannotBeSpecifiedTwice(string prefix, bool samePath)
    {
        string first = Path.GetFullPath("first.json");
        string second = samePath ? first : Path.GetFullPath("second.json");
        var arguments = new List<string> { prefix + first, prefix + second };
        if (prefix == "--report=")
            arguments.Add("--smoke");

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => FirstFlightLaunchOptions.Parse(arguments));
        Assert.Contains(prefix[..^1], error.Message, StringComparison.Ordinal);
        Assert.Contains("only be specified once", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--report=", "")]
    [InlineData("--report=", "relative.json")]
    [InlineData("--record-tape=", "")]
    [InlineData("--record-tape=", "relative.json")]
    public void ASecondDestinationCannotHideAnInvalidFirstValue(string prefix, string invalid)
    {
        var arguments = new List<string>
        {
            prefix + invalid,
            prefix + Path.GetFullPath("valid.json"),
        };
        if (prefix == "--report=")
            arguments.Add("--smoke");

        Assert.Throws<ArgumentException>(() => FirstFlightLaunchOptions.Parse(arguments));
    }

    [Fact]
    public void NullArgumentIsReportedAsAnArgumentError()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => FirstFlightLaunchOptions.Parse(new string[] { null! }));
        Assert.Equal("arguments", error.ParamName);
    }

    [Fact]
    public void RepeatedBooleanMediaFlagsRemainIdempotent()
    {
        FirstFlightLaunchOptions options = FirstFlightLaunchOptions.Parse(
            ["--skipfmv", "--intro", "--skipfmv", "--intro"]);
        Assert.True(options.SkipStartupMedia);
        Assert.True(options.ForceStartupMedia);
        Assert.False(options.Smoke);
        Assert.False(options.CaptureArgumentsPresent);
    }
}
