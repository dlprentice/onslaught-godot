// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Host;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class LiveAudioOutputStartupTests
{
    private sealed class Driver
    {
        internal string Name = "PulseAudio";
        internal string[] Outputs = ["Default", "owned-output"];
        internal string Current = "Default";
        internal ulong Milliseconds;
        internal int Requests;
        internal int Starts;
        internal Action? DuringStart;
        internal LiveAudioOutputStartup Begin() => new("owned-output", () => Name, () => Outputs,
            () => Current, device => { Assert.Equal("owned-output", device); Requests++; },
            () => Milliseconds, () => { Starts++; DuringStart?.Invoke(); });
    }

    [Fact]
    public void RequestYieldsFramesWithoutStartingUntilTheAsynchronousGetterAcknowledges()
    {
        var driver = new Driver();
        var startup = driver.Begin();
        Assert.Equal(1, driver.Requests);
        Assert.Equal(0, driver.Starts);
        Assert.True(startup.Pending);
        Assert.False(startup.Pump(0));
        Assert.False(startup.Pump(1.0 / 60));
        Assert.Equal(0, driver.Starts);
        driver.Milliseconds = 143;
        driver.Current = "owned-output";
        Assert.True(startup.Pump(1.0 / 60));
        Assert.False(startup.Pending);
        Assert.False(startup.Pump(1.0 / 60));
        startup.Cancel(); startup.Cancel();
        Assert.False(startup.Pump(1.0 / 60));
        Assert.Equal(1, driver.Starts);
        Assert.Equal(1, driver.Requests);
    }

    [Fact]
    public void AnAlreadySelectedGetterStillDoesNotStartInsideTheRequest()
    {
        var driver = new Driver { Current = "owned-output" };
        var startup = driver.Begin();
        Assert.Equal(0, driver.Starts);
        Assert.True(startup.Pump(0));
        Assert.Equal(1, driver.Starts);
    }

    [Theory]
    [InlineData("Dummy", true)]
    [InlineData("PulseAudio", false)]
    public void InvalidInitialDriverOrOutputCannotQueueARequest(string name, bool available)
    {
        var driver = new Driver { Name = name, Outputs = available ? ["owned-output"] : ["Default"] };
        Assert.Throws<NotSupportedException>(() => driver.Begin());
        Assert.Equal(0, driver.Requests);
        Assert.Equal(0, driver.Starts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PendingDriverOrEnumerationLossRefusesEvenIfGetterLooksAcknowledged(bool dummy)
    {
        var driver = new Driver(); var startup = driver.Begin();
        driver.Current = "owned-output";
        if (dummy) driver.Name = "Dummy";
        else driver.Outputs = ["Default"];
        Assert.Throws<NotSupportedException>(() => startup.Pump(0));
        Assert.False(startup.Pending);
        Assert.False(startup.Pump(0));
        Assert.Equal(0, driver.Starts);
        Assert.Equal(1, driver.Requests);
    }

    [Fact]
    public void ExactlyThreeHundredUnacknowledgedFramesAreTerminalWithoutASecondRequest()
    {
        var driver = new Driver(); var startup = driver.Begin();
        for (int i = 1; i < LiveAudioOutputStartup.MaxFrames; i++) Assert.False(startup.Pump(0));
        Assert.Throws<TimeoutException>(() => startup.Pump(0));
        driver.Current = "owned-output";
        Assert.False(startup.Pump(0));
        Assert.False(startup.Pending);
        Assert.Equal(0, driver.Starts);
        Assert.Equal(1, driver.Requests);
    }

    [Fact]
    public void AcknowledgementOnTheLastAdmittedFrameStartsExactlyOnce()
    {
        var driver = new Driver(); var startup = driver.Begin();
        for (int i = 1; i < LiveAudioOutputStartup.MaxFrames; i++) Assert.False(startup.Pump(0));
        driver.Current = "owned-output";
        Assert.True(startup.Pump(0));
        Assert.Equal(1, driver.Starts);
    }

    [Theory]
    [InlineData(4999, true)]
    [InlineData(5000, false)]
    [InlineData(5001, false)]
    public void LogicalAcknowledgementMustArriveBeforeTheWallTimeDeadline(ulong elapsed, bool admitted)
    {
        var driver = new Driver { Milliseconds = 100 };
        var startup = driver.Begin();
        driver.Milliseconds += elapsed;
        driver.Current = "owned-output";
        if (admitted) Assert.True(startup.Pump(0));
        else Assert.Throws<TimeoutException>(() => startup.Pump(0));
        Assert.Equal(admitted ? 1 : 0, driver.Starts);
        Assert.False(startup.Pending);
    }

    [Fact]
    public void DefaultGetterWhileTheRequestIsPendingIsNotAdmissionOrAnImmediateFailure()
    {
        var driver = new Driver(); var startup = driver.Begin();
        driver.Milliseconds = 4_999;
        Assert.False(startup.Pump(0));
        Assert.Equal(0, driver.Starts);
        driver.Milliseconds = 5_000;
        Assert.Throws<TimeoutException>(() => startup.Pump(0));
        Assert.Equal(0, driver.Starts);
    }

    [Fact]
    public void ABackwardsStartupClockCancelsRatherThanExtendingTheDeadline()
    {
        var driver = new Driver { Milliseconds = 100 }; var startup = driver.Begin();
        driver.Milliseconds = 99; driver.Current = "owned-output";
        Assert.Throws<InvalidOperationException>(() => startup.Pump(0));
        Assert.False(startup.Pending);
        Assert.Equal(0, driver.Starts);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidFrameDeltaCannotStartOrRemainPending(double delta)
    {
        var driver = new Driver { Current = "owned-output" }; var startup = driver.Begin();
        Assert.Throws<ArgumentOutOfRangeException>(() => startup.Pump(delta));
        Assert.False(startup.Pending);
        Assert.Equal(0, driver.Starts);
    }

    [Fact]
    public void ExitOrFailureCancellationDiscardsTheContinuationPermanently()
    {
        var driver = new Driver(); var startup = driver.Begin();
        startup.Cancel(); startup.Cancel();
        driver.Current = "owned-output";
        Assert.False(startup.Pump(0));
        Assert.False(startup.Pending);
        Assert.Equal(0, driver.Starts);
    }

    [Fact]
    public void AThrowingStartupCannotBeRetriedByAFrameOrExitCallback()
    {
        var driver = new Driver { Current = "owned-output", DuringStart = () => throw new InvalidDataException("authored startup failure") };
        var startup = driver.Begin();
        Assert.Throws<InvalidDataException>(() => startup.Pump(0));
        Assert.False(startup.Pending);
        startup.Cancel();
        Assert.False(startup.Pump(0));
        Assert.Equal(1, driver.Starts);
    }
}
