// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class FrontEndTests
{
    /// <summary>
    /// The replay clock as a full boot leaves it: the platform's start makes one Present and then the first
    /// GetSysTimeFloat call, which fixes its zero (B's level-100 golden fit; the boot flow will make these calls itself).
    /// </summary>
    internal static PlatformClock BootedClock()
    {
        PlatformClock clock = new();
        clock.Present();
        clock.GetSysTimeFloat();
        return clock;
    }

    /// <summary>The replay clock's time after <paramref name="presentsSinceBoot"/> more Presents: k * 333,333 ticks, at 24 bits.</summary>
    internal static float TimeAfter(long presentsSinceBoot) =>
        (float)X87.Div(presentsSinceBoot * (double)PlatformClock.QpcTicksPerPresent, X87.Mul(PlatformClock.QpcFrequency, 1.0f));

    internal sealed class NoGame : IControllerGame
    {
        public bool IsPaused => false;
        public bool IsMultiplayer => false;
        public void ReceiveButtonAction(Controller from, int button, float value) { }
    }

    internal static FrontEnd NewFrontEnd(PlatformClock clock, FrontEndTiming? timing = null)
    {
        PlatformInput input = new(new PlatformWindow());
        ControllerContext controls = new(ControlBindings.Defaults(), input, new ControllerShared(), new NoGame(), clock.GetSysTimeFloat);
        return new FrontEnd(clock, timing ?? new FrontEndTiming(), controls);
    }

    private sealed class RecordingPage(string name, List<string> log) : FrontEndPageBase
    {
        public List<(int Button, float Value)> Buttons { get; } = [];
        public List<FrontEndPageState> States { get; } = [];
        public override void ButtonPressed(int button, float value) => Buttons.Add((button, value));
        public override void Process(FrontEndPageState state) => States.Add(state);
        public override void TransitionNotification(FrontEndPage from) => log.Add($"{name}.Transition({from})");
        public override void ActiveNotification(FrontEndPage from) => log.Add($"{name}.Active({from})");
        public override void DeActiveNotification() => log.Add($"{name}.DeActive");
    }

    [Fact]
    public void TheFrameStepStartsAtASixtiethThenFiltersTheShimsThirtieths()
    {
        PlatformClock clock = BootedClock();
        FrontEndTiming timing = new();
        FrontEnd frontEnd = NewFrontEnd(clock, timing);
        frontEnd.Begin(FrontEndPage.Main);

        float expected = 0f;
        float last = FrontEndTiming.NoLastTime;
        for (int frame = 0; frame < 200; frame++)
        {
            Assert.True(frontEnd.Step());
            float time = TimeAfter(frame);
            // Plain float arithmetic: every operand is a float and every result stays in range, so it equals x87's 24-bit result.
            expected = last != FrontEndTiming.NoLastTime && time - last < 2f ? (time - last) * 0.1f + expected * 0.9f : 1f / 60f;
            last = time;
            Assert.Equal(expected, timing.FrameStep);
        }
        Assert.Equal(201, clock.PresentCount);
        Assert.InRange(timing.FrameStep, 1f / 30f - 1e-5f, 1f / 30f + 1e-5f);
    }

    [Fact]
    public void AGapOfTwoSecondsOrMoreResetsTheStep()
    {
        PlatformClock clock = BootedClock();
        FrontEndTiming timing = new();
        FrontEnd frontEnd = NewFrontEnd(clock, timing);
        frontEnd.Begin(FrontEndPage.Main);
        for (int frame = 0; frame < 30; frame++) frontEnd.Step();
        Assert.NotEqual(1f / 60f, timing.FrameStep);
        // Two seconds of Presents outside the front end (a loading screen, say).
        for (int i = 0; i < 60; i++) clock.Present();
        frontEnd.Step();
        Assert.Equal(1f / 60f, timing.FrameStep);
    }

    [Fact]
    public void TheCounterAdvancesByThePreviousStep()
    {
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        frontEnd.Begin(FrontEndPage.Main);
        frontEnd.Step();
        Assert.Equal(0f, frontEnd.Counter);
        frontEnd.Step();
        Assert.Equal(1f / 60f * 50f, frontEnd.Counter);
    }

    [Fact]
    public void AnImmediatePageChangeNotifiesInTheGamesOrder()
    {
        List<string> log = [];
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        frontEnd[FrontEndPage.Intro] = new RecordingPage("Intro", log);
        frontEnd[FrontEndPage.Main] = new RecordingPage("Main", log);
        frontEnd.Begin(FrontEndPage.Intro);
        frontEnd.SetPage(FrontEndPage.Main, 0);
        Assert.Equal(["Intro.Transition(Unknown)", "Intro.Active(Unknown)", "Intro.DeActive", "Main.Transition(Intro)", "Main.Active(Intro)"], log);
        Assert.Equal(FrontEndPage.Main, frontEnd.ActivePage);
    }

    [Fact]
    public void ATimedChangeRunsAsATransitionUntilItsTimeIsReached()
    {
        List<string> log = [];
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        RecordingPage intro = new("Intro", log), main = new("Main", log);
        frontEnd[FrontEndPage.Intro] = intro;
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Begin(FrontEndPage.Intro);
        for (int frame = 0; frame < 10; frame++) frontEnd.Step();
        log.Clear();

        frontEnd.SetPage(FrontEndPage.Main, 50);
        Assert.Equal(FrontEndPage.Transition, frontEnd.ActivePage);
        Assert.Equal(["Main.Transition(Intro)"], log);

        int steps = 0;
        while (frontEnd.ActivePage == FrontEndPage.Transition)
        {
            frontEnd.Step();
            Assert.True(++steps < 100);
        }
        Assert.True(frontEnd.TransitionCount >= 50f);
        Assert.Equal(FrontEndPage.Main, frontEnd.ActivePage);
        Assert.Equal(["Main.Transition(Intro)", "Intro.DeActive", "Main.Active(Intro)"], log);
        Assert.Contains(FrontEndPageState.TransitioningFrom, intro.States);
        Assert.Contains(FrontEndPageState.TransitioningTo, main.States);
        Assert.Equal(FrontEndPageState.Active, main.States[^1]);
    }

    [Fact]
    public void KeysReachTheActivePageThroughController0AndMakeItPlayer0()
    {
        List<string> log = [];
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        RecordingPage main = new("Main", log);
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Begin(FrontEndPage.Main);

        frontEnd.Platform.PostTapeKey(0x28, Dik.Down, down: true);
        frontEnd.Step();
        Assert.Equal(-1, frontEnd.Player0Port);
        frontEnd.Platform.PostTapeKey(0x0D, Dik.Return, down: true);
        frontEnd.Step();
        Assert.Equal(0, frontEnd.Player0Port);
        // Every binding of a key reaches the controlled object, in table order; the front end's pages ignore the ones
        // they do not use. The held down arrow also sends the camera's pitch-down (-1) every frame.
        Assert.Equal([(FrontEndButtons.MenuDown, 1f), (50, 1f), (35, -1f),
            (7, 1f), (58, 1f), (FrontEndButtons.MenuSelect, 1f), (51, 1f), (35, -1f)], main.Buttons);
    }

    [Fact]
    public void ButtonsFromAnotherPortAreIgnoredOncePlayer0IsKnown()
    {
        List<string> log = [];
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        RecordingPage main = new("Main", log);
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Begin(FrontEndPage.Main);
        frontEnd.ReceiveButtonAction(new FrontEndButton(1, FrontEndButtons.MenuSelect, 1f));
        frontEnd.ReceiveButtonAction(new FrontEndButton(0, FrontEndButtons.MenuUp, 1f));
        Assert.Equal(1, frontEnd.Player0Port);
        Assert.Equal([(FrontEndButtons.MenuSelect, 1f)], main.Buttons);
    }

    [Fact]
    public void NoButtonReachesAPageDuringATransitionAndTheMessageBoxTakesThemWhenInteractive()
    {
        List<string> log = [];
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        RecordingPage intro = new("Intro", log), main = new("Main", log);
        frontEnd[FrontEndPage.Intro] = intro;
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Begin(FrontEndPage.Intro);
        frontEnd.SetPage(FrontEndPage.Main, 50);
        frontEnd.ReceiveButtonAction(new FrontEndButton(0, FrontEndButtons.MenuSelect, 1f));
        Assert.Empty(intro.Buttons);
        Assert.Empty(main.Buttons);

        frontEnd.SetPage(FrontEndPage.Main, 0);
        frontEnd.MessageBox.Create(0, 0, false, MessageBoxOption.YesNo, MessageBoxQuestion.Quit);
        frontEnd.ReceiveButtonAction(new FrontEndButton(0, FrontEndButtons.MenuUp, 1f));
        Assert.Empty(main.Buttons);
        Assert.Equal(1, frontEnd.MessageBox.Cursor);

        frontEnd.ReceiveButtonAction(new FrontEndButton(0, FrontEndButtons.Cheat, 1f));
        Assert.True(frontEnd.AllLevelsCheatActive);
        Assert.Equal([(FrontEndButtons.Cheat, 1f)], main.Buttons);
    }

    [Fact]
    public void AClickRaisesSelectFromController0UnlessTheMessageBoxIsInteractive()
    {
        List<string> log = [];
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        RecordingPage main = new("Main", log);
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Begin(FrontEndPage.Main);
        frontEnd.Platform.PostMouseMessage(MouseMessage.Move, 100, 100, 0);
        frontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, 100, 100, 0x1);
        frontEnd.Platform.PostMouseMessage(MouseMessage.LeftUp, 100, 100, 0);
        frontEnd.Step();
        Assert.True(frontEnd.ProcessMouseClickInRect(0, 0, 640, 480, FrontEndButtons.MenuSelect));
        // The release also raises the gun pod's fire binding (18) at the controller flush, before the page's click test.
        Assert.Equal([(18, 1f), (FrontEndButtons.MenuSelect, 1f)], main.Buttons);

        frontEnd.MessageBox.Create(0, 0, false, MessageBoxOption.Ok, 0);
        Assert.False(frontEnd.ProcessMouseClickInRect(0, 0, 640, 480, FrontEndButtons.MenuSelect));
    }

    [Fact]
    public void ProcessMouseClickInRectFollowsTheCpp()
    {
        // CFrontEnd::ProcessMouseClickInRect (0x00469390): false while the message box is interactive, else
        // MouseDispatchClickInRect: a release inside raises the button from controller 0 (consumed once), and the result
        // is whether the cursor (moved this frame) is inside.
        List<string> log = [];
        FrontEnd frontEnd = NewFrontEnd(BootedClock());
        RecordingPage main = new("Main", log);
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Begin(FrontEndPage.Main);
        frontEnd.Platform.PostMouseMessage(MouseMessage.Move, 50, 60, 0);
        frontEnd.Step();
        Assert.True(frontEnd.ProcessMouseClickInRect(0, 0, 100, 100, FrontEndButtons.MenuSelect));
        Assert.False(frontEnd.ProcessMouseClickInRect(50, 61, 100, 100, FrontEndButtons.MenuSelect));
        Assert.Empty(main.Buttons);

        frontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, 50, 60, 0x1);
        frontEnd.Platform.PostMouseMessage(MouseMessage.LeftUp, 50, 60, 0);
        frontEnd.Step();
        main.Buttons.Clear();
        // Not moved this frame: no hover, but the release still clicks; the right and bottom edges are exclusive.
        Assert.False(frontEnd.ProcessMouseClickInRect(0, 0, 50, 61, FrontEndButtons.MenuSelect));
        Assert.Empty(main.Buttons);
        Assert.False(frontEnd.ProcessMouseClickInRect(0, 0, 51, 61, FrontEndButtons.MenuBack));
        Assert.Equal([(FrontEndButtons.MenuBack, 1f)], main.Buttons);
        Assert.False(frontEnd.ProcessMouseClickInRect(0, 0, 51, 61, FrontEndButtons.MenuBack));
        Assert.Single(main.Buttons);
    }

    [Fact]
    public void AFrameRendersUntilASceneStartsAndEveryRenderPresents()
    {
        PlatformClock clock = BootedClock();
        FrontEnd frontEnd = NewFrontEnd(clock);
        frontEnd.Begin(FrontEndPage.Main);
        int attempts = 0;
        frontEnd.BeginScene = () => ++attempts >= 3;
        Assert.True(frontEnd.Step());
        Assert.Equal(1 + 3, clock.PresentCount);
    }

    [Fact]
    public void EachRunningIterationPresentsOnceAndAQuitStopsBeforeThePresent()
    {
        PlatformClock clock = BootedClock();
        FrontEnd frontEnd = NewFrontEnd(clock);
        frontEnd.Begin(FrontEndPage.Main);
        Assert.True(frontEnd.Step());
        Assert.True(frontEnd.Step());
        Assert.Equal(1 + 2, clock.PresentCount);
        Assert.False(frontEnd.Step(platformQuit: true));
        Assert.Equal(FrontEnd.QuitToSystem, frontEnd.Quit);
        Assert.Equal(1 + 2, clock.PresentCount);
    }
}
