// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/FrontEnd.cpp, src/frontend.h and src/FrontEndPage.h (CFrontEnd, CFrontEndPage).

using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary><c>EFrontEndPage</c>: the front end's pages, in the game's order.</summary>
public enum FrontEndPage
{
    Main = 0,
    Development,
    Common,
    BeConfig,
    Wingmen,
    Briefing,
    Debriefing,
    LevelSelect,
    Goodies,
    DevSelect,
    LoadGame,
    SaveGame,
    Intro,
    VirtualKeyboard,
    Directory,
    MultiplayerStart,
    Multiplayer,
    Options,
    Credits,
    ScreenPos,
    E3LevelSelect,
    LanguageTest,
    DemoMain,
    Unknown,
    PageCount,

    /// <summary>The active page while changing pages.</summary>
    Transition = -1,

    /// <summary>The destination passed when not changing.</summary>
    None = -2,
}

/// <summary><c>EFEPState</c>: a page's state as <see cref="FrontEnd.Process"/> passes it.</summary>
public enum FrontEndPageState
{
    Active = 0,
    TransitioningTo,
    TransitioningFrom,
    Inactive,
}

/// <summary>
/// <c>CFrontEndPage</c>: a page's hooks. Every hook does nothing by default, as the base class's do; a page overrides
/// what it uses. Rendering belongs to the host and is not modelled here.
/// </summary>
public abstract class FrontEndPageBase
{
    public virtual bool Init() => true;
    public virtual void Process(FrontEndPageState state) { }
    public virtual void ButtonPressed(int button, float value) { }
    public virtual void TransitionNotification(FrontEndPage from) { }
    public virtual void ActiveNotification(FrontEndPage from) { }
    public virtual void DeActiveNotification() { }
}

/// <summary>A page with no behaviour (<c>CFEPUnknown</c>, and any page not yet ported).</summary>
public sealed class InertPage : FrontEndPageBase
{
}

/// <summary>
/// The clock and Present counter the flow runs on. A stand-in for the parity platform clock (rebuild Parity/Clock, M2):
/// <see cref="GetSysTimeFloat"/> is CPCPlatform's float time, <see cref="QueryPerformanceCounter"/> the counter the
/// frame timer reads, and <see cref="Present"/> is the only call that advances the counter and, on the shim's clock, time.
/// </summary>
public interface IFlowClock
{
    float GetSysTimeFloat();
    long QueryPerformanceCounter();
    void Present();
    long PresentCount { get; }
}

/// <summary>A button action delivered to the front end by a controller's flush (<c>CFrontEnd::ReceiveButtonAction</c>).</summary>
public readonly record struct FrontEndButton(int Port, int Button, float Value);

/// <summary>
/// The front end's process-wide statics: <c>gFrontEndFrameStep</c> (00679af8) and the function-static
/// <c>last_time</c> in <c>CFrontEnd::Process</c>. Both persist across front-end runs and level runs.
/// </summary>
public sealed class FrontEndTiming
{
    public const float NoLastTime = -12345f;

    /// <summary><c>gFrontEndFrameStep</c>, zero in static storage until the first front-end process.</summary>
    public float FrameStep { get; set; }

    public float LastTime { get; set; } = NoLastTime;

    /// <summary>
    /// The filter in <c>CFrontEnd::Process</c> (0x00466ba0): with a previous time less than 2 s back, the step becomes
    /// 0.1 of the new frame time plus 0.9 of the old step; otherwise 1/60. x87: the difference, both products and the
    /// sum round to 24 bits in the register; the result is stored to the float global.
    /// </summary>
    public void Update(float time)
    {
        if (LastTime != NoLastTime)
        {
            double elapsed = X87.Sub(time, LastTime);
            if (elapsed < 2.0)
            {
                FrameStep = (float)X87.Add(X87.Mul(elapsed, 0.1f), X87.Mul(FrameStep, 0.9f));
                LastTime = time;
                return;
            }
        }
        FrameStep = 1f / 60f;
        LastTime = time;
    }
}

/// <summary>
/// <c>CFrontEnd</c>'s page machine: the page table, page changes with timed transitions, button routing, the per-frame
/// process with its filtered frame step, and the run loop of one process and one Present per iteration
/// (<c>CFrontEnd::Run</c> 0x004684d0). Pages not yet ported are <see cref="InertPage"/>.
/// </summary>
public sealed class FrontEnd
{
    public const int QuitRunning = -2;
    public const int QuitToSystem = -1;
    public const int QuitAttract = -3;

    /// <summary><c>BUTTON_FRONTEND_MENU_SELECT</c> and <c>BUTTON_FRONTEND_CHEAT</c> (controller.h).</summary>
    public const int ButtonMenuSelect = FrontEndButtons.MenuSelect;
    public const int ButtonCheat = FrontEndButtons.Cheat;

    private readonly FrontEndPageBase[] _pages = new FrontEndPageBase[(int)FrontEndPage.PageCount];

    public FrontEnd(IFlowClock clock, FrontEndTiming timing, PlatformInput platform)
    {
        Clock = clock;
        Timing = timing;
        Platform = platform;
        for (int page = 0; page < _pages.Length; page++) _pages[page] = new InertPage();
    }

    public IFlowClock Clock { get; }
    public FrontEndTiming Timing { get; }
    public PlatformInput Platform { get; }

    /// <summary>
    /// Whether a render's scene starts (<c>PLATFORM.BeginScene</c>). Each render presents whether or not it started, and
    /// the run loop renders until one starts. True unless a test or the host says otherwise.
    /// </summary>
    public Func<bool> BeginScene { get; set; } = () => true;

    /// <summary><c>mLastRenderTime</c>: the clock at the start of the last render.</summary>
    public float LastRenderTime { get; private set; } = -100f;

    public FrontEndPage ActivePage { get; private set; } = FrontEndPage.Unknown;
    public FrontEndPage TransitionFrom { get; private set; }
    public FrontEndPage TransitionTo { get; private set; }

    /// <summary><c>mTransitionCount</c> and <c>mTransitionTime</c>: counted in fiftieths of a second of frame step.</summary>
    public float TransitionCount { get; private set; }
    public float TransitionTime { get; private set; }

    /// <summary><c>mCounter</c>: advanced by the frame step at the start of each process.</summary>
    public float Counter { get; private set; }

    /// <summary><c>mQuit</c>: <see cref="QuitRunning"/> while running.</summary>
    public int Quit { get; set; } = QuitRunning;

    /// <summary><c>mPlayer0ControllerPort</c>: the first port to press select, or -1.</summary>
    public int Player0Port { get; private set; } = -1;

    /// <summary><c>mAllLevelsCheatActive</c>.</summary>
    public bool AllLevelsCheatActive { get; private set; }

    public FrontEndPageBase this[FrontEndPage page]
    {
        get => _pages[(int)page];
        set => _pages[(int)page] = value;
    }

    /// <summary>
    /// <c>CFrontEnd::SetPage</c> (0x00466ae0): with time 0 the change is immediate (deactivate, transition, activate);
    /// otherwise a transition of <paramref name="time"/> fiftieths of a second begins.
    /// </summary>
    public void SetPage(FrontEndPage page, int time)
    {
        if (ActivePage == FrontEndPage.Transition) ActivePage = TransitionFrom;
        if (time == 0)
        {
            this[ActivePage].DeActiveNotification();
            this[page].TransitionNotification(ActivePage);
            this[page].ActiveNotification(ActivePage);
            ActivePage = page;
            return;
        }
        TransitionFrom = ActivePage;
        TransitionTo = page;
        TransitionCount = 0f;
        TransitionTime = time;
        ActivePage = FrontEndPage.Transition;
        this[TransitionTo].TransitionNotification(TransitionFrom);
    }

    /// <summary>Starts on <paramref name="page"/> as <c>CFrontEnd::Init</c> does: from the unknown page, then the entry notifications.</summary>
    public void Enter(FrontEndPage page)
    {
        ActivePage = FrontEndPage.Unknown;
        SetPage(page, 0);
    }

    /// <summary>
    /// <c>CFrontEnd::ReceiveButtonAction</c> (0x004669a0): the first port to press select becomes player 0 and other
    /// ports are then ignored (except on the multiplayer start page). Nothing reaches a page during a transition.
    /// </summary>
    public void ReceiveButtonAction(FrontEndButton action, IFrontEndMessageBox? messageBox = null)
    {
        if (Player0Port == -1 && action.Button == ButtonMenuSelect)
        {
            Player0Port = action.Port;
        }
        else if (Player0Port != -1)
        {
            if (ActivePage != FrontEndPage.MultiplayerStart && action.Port != Player0Port) return;
        }

        if (action.Button == ButtonCheat && ActivePage != FrontEndPage.Transition)
        {
            AllLevelsCheatActive = !AllLevelsCheatActive;
            this[ActivePage].ButtonPressed(action.Button, action.Value);
        }
        else if (messageBox is { IsInteractive: true })
        {
            messageBox.ButtonPressed(action.Button, action.Value);
        }
        else if (ActivePage != FrontEndPage.Transition)
        {
            this[ActivePage].ButtonPressed(action.Button, action.Value);
        }
    }

    /// <summary>
    /// <c>CFrontEnd::Process</c> (0x00466ba0): the mouse's transient state and the key-up flags clear, the counter
    /// advances by the previous step, the platform handles the window's messages (a quit request ends the front end),
    /// the controllers deliver their button actions, the frame step is filtered from the clock, a transition in progress
    /// advances (and completes once it reaches its time), and every page processes in page order.
    /// </summary>
    public void Process(IReadOnlyList<FrontEndButton> buttons, bool platformQuit = false, IFrontEndMessageBox? messageBox = null)
    {
        Platform.Mouse.ResetTransientState();
        Platform.ClearKeyWasUp();
        // x87: the product and the sum round to 24 bits; the counter is a float member.
        Counter = (float)X87.Add(Counter, X87.Mul(Timing.FrameStep, 50f));
        Platform.Process(Clock.QueryPerformanceCounter());
        if (platformQuit) Quit = QuitToSystem;

        foreach (FrontEndButton button in buttons) ReceiveButtonAction(button, messageBox);

        Timing.Update(Clock.GetSysTimeFloat());

        if (ActivePage == FrontEndPage.Transition)
        {
            TransitionCount = (float)X87.Add(TransitionCount, X87.Mul(Timing.FrameStep, 50f));
            if (TransitionCount >= TransitionTime)
            {
                this[TransitionFrom].DeActiveNotification();
                ActivePage = TransitionTo;
                this[ActivePage].ActiveNotification(TransitionFrom);
            }
        }

        for (int page = 0; page < _pages.Length; page++)
        {
            FrontEndPageState state = FrontEndPageState.Inactive;
            if ((int)ActivePage == page)
            {
                state = FrontEndPageState.Active;
            }
            else if (ActivePage == FrontEndPage.Transition)
            {
                if (page == (int)TransitionFrom) state = FrontEndPageState.TransitioningFrom;
                if (page == (int)TransitionTo) state = FrontEndPageState.TransitioningTo;
            }
            _pages[page].Process(state);
        }

        messageBox?.Process();
    }

    /// <summary>
    /// One iteration of <c>CFrontEnd::Run</c>'s loop: a process, then (while still running) renders until one starts its
    /// scene. Every render presents (<c>CFrontEnd::RenderEnd</c> flips whether or not the scene started), so a frame
    /// usually costs one Present. Returns false once the front end has quit.
    /// </summary>
    public bool Step(IReadOnlyList<FrontEndButton> buttons, bool platformQuit = false, IFrontEndMessageBox? messageBox = null)
    {
        Process(buttons, platformQuit, messageBox);
        if (Quit != QuitRunning) return false;
        while (!Render()) { }
        return true;
    }

    /// <summary><c>CFrontEnd::Render</c> (0x00468200), as far as the flow sees it: the render time, then one Present.</summary>
    private bool Render()
    {
        LastRenderTime = Clock.GetSysTimeFloat();
        bool started = BeginScene();
        Clock.Present();
        return started;
    }
}

/// <summary>The front end's message box (<c>FEMESSBOX</c>), which takes the buttons while it is interactive.</summary>
public interface IFrontEndMessageBox
{
    bool IsInteractive { get; }
    void ButtonPressed(int button, float value);
    void Process();
}

/// <summary>The front-end button ids (Controller.h <c>BUTTON_FRONTEND_*</c>).</summary>
public static class FrontEndButtons
{
    public const int MenuUp = 42;
    public const int MenuDown = 43;
    public const int MenuSelect = 44;
    public const int Cheat = 45;
    public const int MenuBack = 46;
    public const int MenuLeft = 54;
    public const int MenuRight = 55;
    public const int MenuSkip = 60;
    public const int MenuSpecial = 63;
}
