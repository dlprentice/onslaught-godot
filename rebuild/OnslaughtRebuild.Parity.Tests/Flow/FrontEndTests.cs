// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Flow;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class FrontEndTests
{
    /// <summary>The shim's clock: time moves 1/30 s per Present, from the first Present on.</summary>
    private sealed class ShimClock : IFlowClock
    {
        public long PresentCount { get; private set; }
        public float Offset { get; set; }
        public float GetSysTimeFloat() => Offset + (float)(PresentCount / 30.0);
        public void Present() => PresentCount++;
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

    private sealed class MessageBox : IFrontEndMessageBox
    {
        public bool IsInteractive { get; set; }
        public List<int> Buttons { get; } = [];
        public int Processed { get; private set; }
        public void ButtonPressed(int button, float value) => Buttons.Add(button);
        public void Process() => Processed++;
    }

    [Fact]
    public void TheFrameStepStartsAtASixtiethThenFiltersTheShimsThirtieths()
    {
        ShimClock clock = new();
        FrontEndTiming timing = new();
        FrontEnd frontEnd = new(clock, timing);
        frontEnd.Enter(FrontEndPage.Main);

        float expected = 0f;
        float last = FrontEndTiming.NoLastTime;
        for (int frame = 0; frame < 200; frame++)
        {
            Assert.True(frontEnd.Step([]));
            float time = (float)((frame) / 30.0);
            // Plain float arithmetic: every operand is a float and every result stays in range, so it equals x87's 24-bit result.
            expected = last != FrontEndTiming.NoLastTime && time - last < 2f ? (time - last) * 0.1f + expected * 0.9f : 1f / 60f;
            last = time;
            Assert.Equal(expected, timing.FrameStep);
        }
        Assert.Equal(200, clock.PresentCount);
        Assert.InRange(timing.FrameStep, 1f / 30f - 1e-5f, 1f / 30f + 1e-5f);
    }

    [Fact]
    public void AGapOfTwoSecondsOrMoreResetsTheStep()
    {
        ShimClock clock = new();
        FrontEndTiming timing = new();
        FrontEnd frontEnd = new(clock, timing);
        frontEnd.Enter(FrontEndPage.Main);
        for (int frame = 0; frame < 30; frame++) frontEnd.Step([]);
        Assert.NotEqual(1f / 60f, timing.FrameStep);
        clock.Offset = 2f;
        frontEnd.Step([]);
        Assert.Equal(1f / 60f, timing.FrameStep);
    }

    [Fact]
    public void TheCounterAdvancesByThePreviousStep()
    {
        ShimClock clock = new();
        FrontEndTiming timing = new();
        FrontEnd frontEnd = new(clock, timing);
        frontEnd.Enter(FrontEndPage.Main);
        frontEnd.Step([]);
        Assert.Equal(0f, frontEnd.Counter);
        frontEnd.Step([]);
        Assert.Equal(1f / 60f * 50f, frontEnd.Counter);
    }

    [Fact]
    public void AnImmediatePageChangeNotifiesInTheGamesOrder()
    {
        List<string> log = [];
        FrontEnd frontEnd = new(new ShimClock(), new FrontEndTiming());
        frontEnd[FrontEndPage.Intro] = new RecordingPage("Intro", log);
        frontEnd[FrontEndPage.Main] = new RecordingPage("Main", log);
        frontEnd.Enter(FrontEndPage.Intro);
        frontEnd.SetPage(FrontEndPage.Main, 0);
        Assert.Equal(["Intro.Transition(Unknown)", "Intro.Active(Unknown)", "Intro.DeActive", "Main.Transition(Intro)", "Main.Active(Intro)"], log);
        Assert.Equal(FrontEndPage.Main, frontEnd.ActivePage);
    }

    [Fact]
    public void ATimedChangeRunsAsATransitionUntilItsTimeIsReached()
    {
        List<string> log = [];
        ShimClock clock = new();
        FrontEnd frontEnd = new(clock, new FrontEndTiming());
        RecordingPage intro = new("Intro", log), main = new("Main", log);
        frontEnd[FrontEndPage.Intro] = intro;
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Enter(FrontEndPage.Intro);
        for (int frame = 0; frame < 10; frame++) frontEnd.Step([]);
        log.Clear();

        frontEnd.SetPage(FrontEndPage.Main, 50);
        Assert.Equal(FrontEndPage.Transition, frontEnd.ActivePage);
        Assert.Equal(["Main.Transition(Intro)"], log);

        int steps = 0;
        float count = 0f;
        while (frontEnd.ActivePage == FrontEndPage.Transition)
        {
            frontEnd.Step([]);
            steps++;
            Assert.True(frontEnd.TransitionCount > count || frontEnd.ActivePage != FrontEndPage.Transition);
            count = frontEnd.TransitionCount;
            Assert.True(steps < 100);
        }
        Assert.True(frontEnd.TransitionCount >= 50f);
        Assert.Equal(FrontEndPage.Main, frontEnd.ActivePage);
        Assert.Equal(["Main.Transition(Intro)", "Intro.DeActive", "Main.Active(Intro)"], log);
        // During the transition the pages were told their roles.
        Assert.Contains(FrontEndPageState.TransitioningFrom, intro.States);
        Assert.Contains(FrontEndPageState.TransitioningTo, main.States);
        Assert.Equal(FrontEndPageState.Active, main.States[^1]);
    }

    [Fact]
    public void ButtonsGoToTheActivePageFromPlayerZeroOnly()
    {
        List<string> log = [];
        FrontEnd frontEnd = new(new ShimClock(), new FrontEndTiming());
        RecordingPage main = new("Main", log);
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Enter(FrontEndPage.Main);

        frontEnd.Step([new FrontEndButton(1, FrontEndButtons.MenuDown, 1f)]);
        Assert.Equal(-1, frontEnd.Player0Port);
        frontEnd.Step([new FrontEndButton(2, FrontEndButtons.MenuSelect, 1f)]);
        Assert.Equal(2, frontEnd.Player0Port);
        frontEnd.Step([new FrontEndButton(1, FrontEndButtons.MenuDown, 1f), new FrontEndButton(2, FrontEndButtons.MenuUp, 1f)]);
        Assert.Equal([(FrontEndButtons.MenuDown, 1f), (FrontEndButtons.MenuSelect, 1f), (FrontEndButtons.MenuUp, 1f)], main.Buttons);
    }

    [Fact]
    public void NoButtonReachesAPageDuringATransitionAndTheMessageBoxTakesThemWhenInteractive()
    {
        List<string> log = [];
        FrontEnd frontEnd = new(new ShimClock(), new FrontEndTiming());
        RecordingPage intro = new("Intro", log), main = new("Main", log);
        frontEnd[FrontEndPage.Intro] = intro;
        frontEnd[FrontEndPage.Main] = main;
        frontEnd.Enter(FrontEndPage.Intro);
        frontEnd.SetPage(FrontEndPage.Main, 50);
        frontEnd.ReceiveButtonAction(new FrontEndButton(0, FrontEndButtons.MenuSelect, 1f));
        Assert.Empty(intro.Buttons);
        Assert.Empty(main.Buttons);

        frontEnd.SetPage(FrontEndPage.Main, 0);
        MessageBox box = new() { IsInteractive = true };
        frontEnd.Step([new FrontEndButton(0, FrontEndButtons.MenuBack, 1f)], messageBox: box);
        Assert.Equal([FrontEndButtons.MenuBack], box.Buttons);
        Assert.Empty(main.Buttons);
        Assert.Equal(1, box.Processed);

        frontEnd.Step([new FrontEndButton(0, FrontEndButtons.Cheat, 1f)], messageBox: box);
        Assert.True(frontEnd.AllLevelsCheatActive);
        Assert.Equal([(FrontEndButtons.Cheat, 1f)], main.Buttons);
    }

    [Fact]
    public void EachRunningIterationPresentsOnceAndAQuitStopsBeforeThePresent()
    {
        ShimClock clock = new();
        FrontEnd frontEnd = new(clock, new FrontEndTiming());
        frontEnd.Enter(FrontEndPage.Main);
        Assert.True(frontEnd.Step([]));
        Assert.True(frontEnd.Step([]));
        Assert.Equal(2, clock.PresentCount);
        Assert.False(frontEnd.Step([], platformQuit: true));
        Assert.Equal(FrontEnd.QuitToSystem, frontEnd.Quit);
        Assert.Equal(2, clock.PresentCount);
    }
}
