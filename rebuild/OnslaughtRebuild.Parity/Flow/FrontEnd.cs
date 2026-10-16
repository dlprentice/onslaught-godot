// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/FrontEnd.cpp, src/frontend.h and src/FrontEndPage.h (CFrontEnd, CFrontEndPage).

using OnslaughtRebuild.Parity.Clock;
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

    /// <summary>The page's <c>Shutdown</c>, when the front end ends.</summary>
    public virtual void Shutdown() { }

    /// <summary>
    /// The part of the page's <c>Render</c> that changes state (mouse hovers and clicks, scroll clamps), called in the
    /// render's page order; drawing is the host's. <paramref name="transition"/> is 1 when settled.
    /// </summary>
    public virtual void Render(float transition, FrontEndPage dest) { }
}

/// <summary>A page with no behaviour (<c>CFEPUnknown</c>, and any page not yet ported).</summary>
public sealed class InertPage : FrontEndPageBase
{
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
public sealed class FrontEnd : IButtonReceiver, Menus.IMenuHost
{
    /// <summary><c>NUM_CONTROLLER_PORTS</c>.</summary>
    public const int ControllerPorts = 2;

    public const int QuitRunning = -2;
    public const int QuitToSystem = -1;
    public const int QuitAttract = -3;

    /// <summary><c>BUTTON_FRONTEND_MENU_SELECT</c> and <c>BUTTON_FRONTEND_CHEAT</c> (controller.h).</summary>
    public const int ButtonMenuSelect = FrontEndButtons.MenuSelect;
    public const int ButtonCheat = FrontEndButtons.Cheat;

    private readonly FrontEndPageBase[] _pages = new FrontEndPageBase[(int)FrontEndPage.PageCount];

    public FrontEnd(PlatformClock clock, FrontEndTiming timing, ControllerContext controls)
    {
        Clock = clock;
        Timing = timing;
        Controls = controls;
        MessageBox = new FrontEndMessageBox(this);
        for (int page = 0; page < _pages.Length; page++) _pages[page] = new InertPage();
    }

    public PlatformClock Clock { get; }
    public FrontEndTiming Timing { get; }

    /// <summary>EVENT_MANAGER while the front end runs: Init starts it, each process ticks it.</summary>
    public Events.EventManager Events { get; } = new();
    public ControllerContext Controls { get; }
    public PlatformInput Platform => Controls.Input;

    /// <summary><c>FEMESSBOX</c>.</summary>
    public FrontEndMessageBox MessageBox { get; }

    /// <summary><c>CAREER</c>: the game's one career, shared with the level loop.</summary>
    public OnslaughtRebuild.Parity.Career.Career Career { get; set; } = OnslaughtRebuild.Parity.Career.Career.Constructed();

    /// <summary><c>MEMORYCARD</c>: the PC's save folder.</summary>
    public MemoryCard MemoryCard { get; set; } = new(new InMemorySaveFolder());

    /// <summary><c>mSaveMode</c>: the storage pages are saving (true) or loading.</summary>
    public bool SaveMode { get; set; }

    /// <summary><c>mAutoSave</c>.</summary>
    public AutoSaveMode AutoSave { get; set; } = AutoSaveMode.Not;

    /// <summary><c>mOnSuccessFEP</c> and <c>mFEPTransTime</c>: where a storage operation goes when it succeeds.</summary>
    public FrontEndPage SuccessPage { get; private set; }
    public int SuccessTransitionTime { get; private set; }

    /// <summary><c>mSelectedLevel</c>: the level the front end will start.</summary>
    public int SelectedLevel { get; set; } = -1;

    /// <summary><c>mMemoryCardNumber</c>.</summary>
    public int MemoryCardNumber { get; set; }

    /// <summary>The text language (<c>TEXT.GetLanguage</c>), 0 to 4.</summary>
    public int Language { get; set; }

    /// <summary><c>CLIPARAMS.mAttractMode</c>.</summary>
    public bool AttractMode { get; set; }

    /// <summary><c>GAME.mSupervisorMode</c>, which <c>CFrontEnd::Init</c> sets ("for E3").</summary>
    public bool SupervisorMode { get; set; }

    public void SetSuccessPage(FrontEndPage page, int time)
    {
        SuccessPage = page;
        SuccessTransitionTime = time;
    }

    /// <summary><c>CFrontEnd::SetLanguage</c> (0x00466ab0): the text set changes; the options page's cached text is presentation.</summary>
    public void SetLanguage(int language) => Language = language;

    /// <summary>The process's C runtime random state (one per process, shared with the simulation), from the host.</summary>
    public Crt.CrtRand? Crt { get; set; }

    /// <summary>
    /// The game's live options, which a save writes after the career and bindings (<c>CCareer::WriteOptions</c>: the
    /// display mode, sound and the rest), from the host.
    /// </summary>
    public Func<Career.CareerOptions>? CurrentOptions { get; set; }

    /// <summary>The game's front-end settings (<c>GAME.GetFrontEndSettings()</c>), which outlive the front end; from the host.</summary>
    public FrontEndSettings Settings { get; set; } = new();

    /// <summary>Reads one of the game's data files by its retail path, or null when it is missing; from the host.</summary>
    public Func<string, byte[]?>? ReadDataFile { get; set; }

    /// <summary><c>data\WorldHeaders.dat</c>, read by the configuration page's Init.</summary>
    public WorldHeaders? WorldHeaders { get; set; }

    /// <summary>
    /// A Battle Engine configuration's cockpit mesh (<c>UBattleEngineDataManager::GetConfiguration(name)->mCockpit</c>),
    /// or null for an unknown configuration; from the owner of the Battle Engine data.
    /// </summary>
    public Func<string, string?>? CockpitOf { get; set; }

    /// <summary>
    /// Starts a particle effect by name for a debriefing goodie (<c>PARTICLE_MANAGER.CreateEffect</c>), from the host's
    /// particle system, whose random draws are its own to model.
    /// </summary>
    public Action<string>? CreateGoodieEffect { get; set; }

    /// <summary>
    /// The small font (<c>PLATFORM.Font(FONT_SMALL)</c>, Font13PS) and the front-end texts (<c>FET</c>), from the host's
    /// installation. Render logic that hit-tests laid-out text (the main menu's items) runs only when both are set.
    /// </summary>
    public Text.BitmapFont? SmallFont { get; set; }

    /// <inheritdoc cref="SmallFont"/>
    public Text.FrontEndTexts? Texts { get; set; }

    /// <summary>The normal font (<c>FONT_NORMAL</c>, font22.512), from the host; the message box's buttons are measured in it.</summary>
    public Text.BitmapFont? NormalFont { get; set; }

    /// <summary>
    /// Opens the message box with its layout when the host supplied the fonts and texts (then its render takes mouse
    /// clicks), else without. <paramref name="text"/> is read only when the layout is made, as retail reads it as
    /// Create's argument.
    /// </summary>
    public void ShowMessage(float x, float y, float width, Func<Text.FrontEndTexts, string> text, uint timeout, uint fadeTime,
        MessageBoxOption option, int question)
    {
        if (SmallFont is { } small && NormalFont is { } normal && Texts is { } texts)
            MessageBox.Create(x, y, width, text(texts), small, normal, timeout, fadeTime, false, option, question);
        else
            MessageBox.Create(timeout, fadeTime, false, option, question);
    }

    /// <summary>The front end's video player, from the host.</summary>
    public IFrontEndVideo? Video { get; set; }

    /// <summary><c>gAllCheatsEnabled</c> (00679ec1), which the game sets: every cheat counts as active.</summary>
    public bool AllCheatsEnabled { get; set; }

    /// <summary>The name the save page writes under (<c>mFEPSaveGame.mSaveGameName</c>), set by the name keyboard's Done.</summary>
    public string SaveGameName { get; set; } = "";

    /// <summary>The save list (<c>mFEPDirectory</c>), once registered.</summary>
    public Pages.DirectoryPage? Directory { get; set; }

    /// <summary>The load page (<c>mFEPLoadGame</c>), once registered.</summary>
    public Pages.LoadGamePage? LoadGame { get; set; }

    /// <summary><c>gDisplayResetState</c> (0082b5b0): set while the display settings are being reset; then a load does not rewrite the options file.</summary>
    public bool DisplayResetState { get; set; }

    /// <summary>
    /// <c>CFEPOptions::WriteDefaultOptionsFile</c> (0x0051f680): writes <c>defaultoptions.bea</c> with these bytes (a
    /// failure is only logged); from the host.
    /// </summary>
    public Action<byte[]>? WriteDefaultOptions { get; set; }

    /// <summary>The storage-device page (<c>mFEPDevSelect</c>), once registered.</summary>
    public Pages.DevSelectPage? DevSelect { get; set; }

    /// <summary>The name keyboard (<c>mFEPVirtualKeyboard</c>), once registered.</summary>
    public INameKeyboard? VirtualKeyboard { get; set; }

    /// <summary>The level select (<c>mFEPLevelSelect</c>), once registered.</summary>
    public ILevelSelect? LevelSelect { get; set; }

    /// <summary>Forgets player 0's port (the intro page does this on entry).</summary>
    public void ForgetPlayer0() => Player0Port = -1;

    /// <summary><c>RECONNECT_INTERFACE</c>: player 0's and player 1's controller-reconnect prompts.</summary>
    public ReconnectInterface[] Reconnect { get; } = [new(), new()];

    /// <summary>The sounds the front end plays (<c>EFrontEndSound</c>), recorded for the host.</summary>
    public List<FrontEndSound> SoundsPlayed { get; } = [];

    /// <summary>
    /// SOUND.PlayEffect for a front-end sound by its effect name (<c>CFrontEnd::GetSoundName</c>: "Front End Move",
    /// "Front End Select", "Front End Back"), which draws on the main thread's rand(); null where no sound manager runs.
    /// </summary>
    public Action<string>? PlayEffect { get; set; }

    /// <summary>Replaces the live options (the options menu's settings); null where they cannot change.</summary>
    public Action<Career.CareerOptions>? SetOptions { get; set; }

    /// <summary>CFEPOptions::WriteDefaultOptionsFile: defaultoptions.bea's new contents.</summary>
    public Action<byte[]>? WriteDefaultOptionsFile { get; set; }

    /// <summary>CPCSoundManager::GetNumDevices: the sound devices the host lists.</summary>
    public int SoundDevices { get; set; } = 1;

    /// <summary>SOUND.m3DSoundMethod: the method the sound manager chose (the options' -1 means it).</summary>
    public int Sound3DMethod { get; set; }

    Platform.ControlBindings Menus.IMenuHost.Bindings => Controls.Bindings;

    float Menus.IMenuHost.MouseSensitivity
    {
        get => Controls.Shared.MouseSensitivity;
        set
        {
            Controls.Shared.MouseSensitivity = value;
            Menus.IMenuHost host = this;
            host.Options = host.Options with { MouseSensitivity = value };
        }
    }

    bool Menus.IMenuHost.IsCheatActive(int cheat) => Pages.SaveGamePage.CheatActive(SaveGameName, AllCheatsEnabled, cheat);

    /// <summary>The display and joysticks the options' display and controls lists read (the port's harness by default).</summary>
    public Menus.MachineDisplay Machine { get; } = new();

    void Menus.IMenuHost.SaveFromPause()
    {
        OnslaughtRebuild.Parity.Career.CareerOptions options = ((Menus.IMenuHost)this).Options;
        byte[] image = OnslaughtRebuild.Parity.Career.CareerSaveGame.Save(Career, Controls.Bindings.SaveConfigurable(), options);
        if (SaveGameName.Length != 0 && MemoryCard.CreateSave(SaveGameName, true, out _) == MemoryCard.Success)
        {
            MemoryCard.WriteSave(SaveGameName, image);
        }

        WriteDefaultOptionsFile?.Invoke(image);
    }

    Text.TextIds Menus.IMenuHost.Ids => Texts?.Ids ?? throw new InvalidOperationException("the menus need the text ids");

    Career.CareerOptions Menus.IMenuHost.Options
    {
        get => CurrentOptions?.Invoke() ?? OnslaughtRebuild.Parity.Career.CareerOptions.Read(new byte[OnslaughtRebuild.Parity.Career.CareerOptions.Size]);
        set => (SetOptions ?? throw new InvalidOperationException("the options cannot be changed (FrontEnd.SetOptions)"))(value);
    }

    /// <summary>CONSOLE's loading screen, which Init shows while it loads; null where no console runs.</summary>
    public LoadingScreen? Loading { get; set; }

    /// <summary><c>CFrontEnd::PlaySound</c> (0x00468770): <c>SOUND.PlayEffect(SOUND.GetEffectByName(name), NULL)</c>.</summary>
    public void PlaySound(FrontEndSound sound)
    {
        SoundsPlayed.Add(sound);
        PlayEffect?.Invoke(SoundName(sound));
    }

    /// <summary>CFrontEnd::GetSoundName: the sound's effect name.</summary>
    public static string SoundName(FrontEndSound sound) => sound switch
    {
        FrontEndSound.Move => "Front End Move",
        FrontEndSound.Select => "Front End Select",
        _ => "Front End Back",
    };

    /// <summary><c>mControllers</c>: one per port, created when the front end begins.</summary>
    public Controller?[] Controllers { get; } = new Controller?[ControllerPorts];

    bool IButtonReceiver.CanBeControlledWhenInPause => true;
    ControlType IButtonReceiver.ControlType => ControlType.FrontEnd;

    void IButtonReceiver.ReceiveButtonAction(Controller from, int button, float value) =>
        ReceiveButtonAction(new FrontEndButton(PortOf(from), button, value));

    /// <summary><c>GetPortFromController</c>: the controller's port, or 0 if it is not one of the front end's.</summary>
    public int PortOf(Controller controller)
    {
        for (int port = 0; port < ControllerPorts; port++)
        {
            if (ReferenceEquals(Controllers[port], controller)) return port;
        }
        return 0;
    }

    /// <summary>
    /// The part of <c>CFrontEnd::Init</c> (0x004662a0) that sets up the page machine's run: a controller per port with
    /// the front end as the controlled object, the input buffers flushed, the counter, quit and render time reset, and
    /// the start page entered. The caller decides the start page and then runs the loop with the front end marked
    /// running (<c>gInFrontEnd</c>). Not modelled: the reset of <c>GAME.mRenderFrameNumber</c> (a presentation
    /// counter). Returns false, as <c>Init</c> does, when any page's <c>Init</c> failed; every page
    /// still initialises first (<c>ok &amp;= Init()</c>).
    /// </summary>
    public bool Begin(FrontEndPage startPage)
    {
        bool ok = true;
        foreach (FrontEndPageBase page in _pages) ok &= page.Init();
        if (!ok) return false;
        SupervisorMode = true;
        for (int port = 0; port < ControllerPorts; port++) Controllers[port] = new Controller(port, 1, this);
        Platform.FlushInputBuffers();
        Counter = 0f;
        Quit = QuitRunning;
        LastRenderTime = -100f;
        Enter(startPage);
        Platform.Window.FrontEndRunning = true;
        return true;
    }

    /// <summary>
    /// <c>CFrontEnd::ProcessMouseClickInRect</c> (0x00469390): nothing while the message box is interactive; otherwise a
    /// left click inside raises <paramref name="button"/> from controller 0. Returns whether the cursor is inside.
    /// </summary>
    public bool ProcessMouseClickInRect(float left, float top, float right, float bottom, int button)
    {
        if (MessageBox.IsInteractive) return false;
        return Platform.Mouse.DispatchClickInRect(left, top, right, bottom, Platform.KeyTrap,
            () => ReceiveButtonAction(new FrontEndButton(0, button, 1f)));
    }

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
    /// <summary>Called at each <see cref="SetPage"/> with its page and time (a trace for comparison with retail's).</summary>
    public Action<FrontEndPage, int>? SetPageTrace { get; set; }

    public void SetPage(FrontEndPage page, int time)
    {
        SetPageTrace?.Invoke(page, time);
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

    /// <summary><c>mFirstRun</c>: true until the first front-end start has entered the title page.</summary>
    public bool FirstRun { get; set; } = true;

    /// <summary>
    /// <c>performing_stress_test</c> (0083d454), the process's stress-test mode, shared with the game; 0 (off) in play.
    /// A stress test skips the autosave after a level, and <see cref="Run"/> quits to the attract videos once the
    /// counter passes 60 (mode 3) or 2 (other modes).
    /// </summary>
    public int StressTest { get; set; }

    /// <summary><c>CLIPARAMS.mDeveloperMode</c>: off in a retail start.</summary>
    public bool DeveloperMode { get; set; }

    /// <summary>
    /// The game's <c>END_LEVEL_DATA</c>, which the last level filled and <c>CAREER.Update()</c> reads at each front-end
    /// start; from the host (shared with the game).
    /// </summary>
    public Career.EndLevelData EndLevelData { get; set; } = new();

    /// <summary>
    /// <c>CFrontEnd::Init</c> (0x004662a0) for <paramref name="entry"/>, as far as the flow sees it:
    /// <list type="number">
    /// <item>the career takes the last level's result (<c>CAREER.Update()</c>);</item>
    /// <item>the message box resets;</item>
    /// <item>every page initialises (any failure fails the start, after all have run);</item>
    /// <item>supervisor mode is set; the controllers are made and the input flushed; the counter, quit and render
    /// time reset;</item>
    /// <item>the start page is chosen (see below) and gets its transition and active notifications again, from the
    /// common page;</item>
    /// <item>the selected level goes back to 100.</item>
    /// </list>
    /// The first start, or one from the attract videos, goes to the title page. A start after a level goes to the
    /// debriefing, through the save page (an autosave, success to the debriefing at 50) unless the autosave is pretend,
    /// a stress test is running, or all cheats are on (the auto-configuration test, which also skips it, is off in
    /// play: <c>CLIPARAMS.mAutoConfigTest</c> is set only by its switch). Race levels (901-905) go to the
    /// goodies instead, and multiplayer levels (850-879) to the multiplayer page.
    /// Not modelled: loading the front end's data, text, video, sound and music (presentation, the host's), the event
    /// manager, and the Xbox dashboard return (<c>mGoStraightToDeviceSelectScreen</c>, never set on PC).
    /// </summary>
    public bool Init(FrontEndEntry entry)
    {
        // CONSOLE.SetLoading(TRUE, TRUE, FALSE) and the loaded system's range (CSystem::Run passes inLoadedSystem): the
        // loading screen renders if its last render is 0.05 s old, which after a level it is.
        Loading?.SetLoading(true);
        Loading?.SetLoadingRange(50f, 62.5f);
        Events.Init();
        Career.Update(EndLevelData);
        MessageBox.Init();
        bool ok = true;
        foreach (FrontEndPageBase page in _pages) ok &= page.Init();
        if (!ok) return false;
        SupervisorMode = true;
        for (int port = 0; port < ControllerPorts; port++) Controllers[port] = new Controller(port, 1, this);
        Platform.FlushInputBuffers();
        Counter = 0f;
        Quit = QuitRunning;
        LastRenderTime = -100f;

        if (FirstRun || entry == FrontEndEntry.FromAttract)
        {
            FirstRun = false;
            ActivePage = FrontEndPage.Unknown;
            SetPage(DeveloperMode && SupervisorMode ? FrontEndPage.Main : FrontEndPage.Intro, 0);
        }
        else if (entry == FrontEndEntry.TitleScreen)
        {
            ActivePage = FrontEndPage.Unknown;
            SetPage(FrontEndPage.Main, 0);
        }
        else
        {
            bool save = AutoSave != AutoSaveMode.Pretend && StressTest == 0 && !AllCheatsEnabled;
            if (SelectedLevel > 900 && SelectedLevel < 906)
                StartWithAutosave(save, FrontEndPage.Goodies);
            else if (SelectedLevel >= 850 && SelectedLevel < 880)
                ActivePage = FrontEndPage.Multiplayer;
            else
                StartWithAutosave(save, FrontEndPage.Debriefing);
        }

        if (ActivePage != FrontEndPage.Transition)
        {
            this[ActivePage].TransitionNotification(FrontEndPage.Common);
            this[ActivePage].ActiveNotification(FrontEndPage.Common);
        }
        else
        {
            this[TransitionTo].TransitionNotification(FrontEndPage.Common);
        }

        SelectedLevel = 100;
        Loading?.SetLoading(false);
        return true;
    }

    private void StartWithAutosave(bool save, FrontEndPage after)
    {
        if (save)
        {
            SaveMode = true;
            ActivePage = FrontEndPage.Unknown;
            SetPage(FrontEndPage.SaveGame, 0);
            SetSuccessPage(after, 50);
        }
        else
        {
            ActivePage = after;
        }
    }

    /// <summary>
    /// <c>CFrontEnd::Shutdown</c> (0x00466880), as far as the flow sees it: the last deactivation notifications (both pages
    /// in a transition), every page's shutdown, and the controllers removed.
    /// </summary>
    public void Shutdown()
    {
        if (ActivePage == FrontEndPage.Transition)
        {
            this[TransitionFrom].DeActiveNotification();
            this[TransitionTo].DeActiveNotification();
        }
        else
        {
            this[ActivePage].DeActiveNotification();
        }
        foreach (FrontEndPageBase page in _pages) page.Shutdown();
        for (int port = 0; port < ControllerPorts; port++) Controllers[port] = null;
        Events.Shutdown();
    }

    /// <summary>
    /// <c>CFrontEnd::Run</c> (0x004684d0): nothing once the game has been told to quit (-1); otherwise start, process and
    /// render until a page sets the quit value, shut down, and return it (-1 quit, -3 attract videos, or a level to play).
    /// A stress test ends the run after each iteration once the counter is past its limit.
    /// </summary>
    public int Run(FrontEndEntry entry)
    {
        if (Quit == QuitToSystem) return QuitToSystem;
        if (!Init(entry)) return QuitToSystem;
        Platform.Window.FrontEndRunning = true;
        while (Quit == QuitRunning)
        {
            Step();
            if (StressTest > 0 && Counter > (StressTest == 3 ? 60f : 2f)) Quit = QuitAttract;
        }
        Shutdown();
        Platform.Window.FrontEndRunning = false;
        return Quit;
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
    public void ReceiveButtonAction(FrontEndButton action)
    {
        IFrontEndMessageBox messageBox = MessageBox;
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
        else if (messageBox.IsInteractive)
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
    /// the controllers flush and deliver their button actions, the frame step is filtered from the clock, a transition
    /// in progress advances (and completes once it reaches its time), every page processes in page order, and the
    /// message box processes. The event manager ticks (EVENT_MANAGER.Update). Not modelled: particles, sound, music and video updates
    /// (presentation), the inactivity quit (playable demo only), and the second player's reconnect check on the
    /// multiplayer start page (not ported).
    /// </summary>
    public void Process(bool platformQuit = false)
    {
        Platform.Mouse.ResetTransientState();
        Platform.ClearKeyWasUp();
        // x87: the product and the sum round to 24 bits; the counter is a float member.
        Counter = (float)X87.Add(Counter, X87.Mul(Timing.FrameStep, 50f));
        Platform.Process(Clock.QueryPerformanceCounter());
        if (platformQuit) Quit = QuitToSystem;

        // EVENT_MANAGER.Update: a tick on (nothing the front end runs schedules events).
        Events.Update();

        foreach (Controller? controller in Controllers) controller?.Flush(Controls);

        // A disconnected player 0 holds the front end until the controller is back.
        if (Player0Port != -1 && !Reconnect[0].Process() && Reconnect[0].State != ReconnectState.Ok) return;

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

        MessageBox.Process();
    }

    /// <summary>
    /// One iteration of <c>CFrontEnd::Run</c>'s loop: a process, then (while still running) renders until one starts its
    /// scene. Every render presents (<c>CFrontEnd::RenderEnd</c> flips whether or not the scene started), so a frame
    /// usually costs one Present. Returns false once the front end has quit, whether in the process or in the render (a
    /// page's render click can set the quit value, and the loop's test then ends the run before another process).
    /// </summary>
    public bool Step(bool platformQuit = false)
    {
        Process(platformQuit);
        if (Quit != QuitRunning) return false;
        while (!Render()) { }
        return Quit == QuitRunning;
    }

    /// <summary>
    /// <c>CFrontEnd::Render</c> (0x00468200), as far as the flow sees it: the render time; when the scene starts, the
    /// pages' render logic (in a transition the higher-numbered page first, the arriving page at the transition's
    /// fraction and the leaving one at its complement; otherwise the active page at 1), then the message box's (when the
    /// host supplied the normal font and texts); then one Present. The common page's render is drawing only.
    /// </summary>
    private bool Render()
    {
        LastRenderTime = Clock.GetSysTimeFloat();
        bool started = BeginScene();
        if (started)
        {
            // x87: the quotient is stored to a float; 0/0 (an instant change) is NaN and passes both clamps.
            float trans = (float)X87.Div(TransitionCount, TransitionTime);
            if (trans > 1f) trans = 1f;
            else if (trans < 0f) trans = 0f;
            if (ActivePage == FrontEndPage.Transition)
            {
                float back = (float)X87.Sub(1f, trans);
                if (TransitionTo > TransitionFrom)
                {
                    this[TransitionTo].Render(trans, TransitionFrom);
                    this[TransitionFrom].Render(back, TransitionTo);
                }
                else
                {
                    this[TransitionFrom].Render(back, TransitionTo);
                    this[TransitionTo].Render(trans, TransitionFrom);
                }
            }
            else
            {
                this[ActivePage].Render(1f, FrontEndPage.None);
            }
            if (NormalFont is { } normal && Texts is { } texts) MessageBox.Render(normal, texts);
        }
        Clock.Present();
        return started;
    }

    /// <summary><c>CFrontEnd::GetCursorStateInRect</c> (0x004693d0): the cursor moved this frame and is inside.</summary>
    public bool GetCursorStateInRect(float left, float top, float right, float bottom) =>
        !MessageBox.IsInteractive && Platform.Mouse.InRect(left, top, right, bottom, Platform.KeyTrap is not null);

    /// <summary><c>CFrontEnd::MouseClickedInRect</c> (0x00469400): a left click inside, consumed.</summary>
    public bool MouseClickedInRect(float left, float top, float right, float bottom) =>
        !MessageBox.IsInteractive && Platform.Mouse.ClickedInRect(left, top, right, bottom, Platform.KeyTrap);

    /// <summary><c>CFrontEnd::MouseOverRect</c> (0x00469430): the cursor moved and is inside; the move is consumed.</summary>
    public bool MouseOverRect(float left, float top, float right, float bottom) =>
        !MessageBox.IsInteractive && Platform.Mouse.OverRect(left, top, right, bottom, Platform.KeyTrap);

    /// <summary>
    /// The click on the help line's arrow from <c>CFEPCommon::RenderHelpText</c> (0x00453140): help 1, 2, 3 and 6 show
    /// a back arrow at the left, the others a select arrow at the right; both slide in with the transition. Nothing
    /// while a reconnect prompt is up or the message box is interactive. The help text itself is drawing.
    /// </summary>
    public void RenderHelpArrow(int help, float transition)
    {
        if (Reconnect[0].State != ReconnectState.Ok) return;
        if (Reconnect[1].State != ReconnectState.Ok && ActivePage == FrontEndPage.MultiplayerStart) return;
        bool back = help is 1 or 2 or 3 or 6;
        if (MessageBox.IsInteractive) return;
        // x87: each step is stored to a float.
        float ofs = (float)X87.Sub(1f, transition);
        ofs = (float)X87.Mul(X87.Mul(ofs, ofs), 3000f);
        float x = back ? (float)X87.Add(X87.Sub(6f, ofs), 16f) : (float)X87.Add(ofs, 618f);
        ProcessMouseClickInRect((float)X87.Sub(x, 40f), 415f, (float)X87.Add(x, 40f), 495f,
            back ? FrontEndButtons.MenuBack : FrontEndButtons.MenuSelect);
    }
}

/// <summary>
/// The front end's video player (<c>FEV</c>, <c>CDXFrontEndVideo</c>), which the host provides: the pages say which
/// video to open; decoding and drawing are the host's.
/// </summary>
public interface IFrontEndVideo
{
    /// <summary>Whether a video file exists (the briefing page probes with <c>fopen</c>).</summary>
    bool Exists(string path);

    /// <summary><c>FEV.Open(name, width, height, 0, TRUE, 0)</c>: the video at that size, looping.</summary>
    void Open(string path, int width, int height);
}

/// <summary><c>EFrontEndEntry</c>: how <c>CFrontEnd::Run</c> was entered.</summary>
public enum FrontEndEntry
{
    Start = 0,
    FromAttract = 1,
    TitleScreen = 2,
}

/// <summary>
/// The part of the game's <c>SFrontEndSettings</c> the single-player flow sets: player 1's configuration and its cockpit
/// mesh (<c>mPlayer1CockpitMesh</c>, 50 characters in retail).
/// </summary>
public sealed class FrontEndSettings
{
    public int Player1Configuration { get; set; }
    public string Player1CockpitMesh { get; set; } = "";
}

/// <summary>What other pages call on the name keyboard (<c>CFEPVirtualKeyboard</c>).</summary>
public interface INameKeyboard
{
    /// <summary><c>SetDefaultName</c> (0x0051fff0).</summary>
    void SetDefaultName();

    /// <summary>A save has used the name, so it is no longer the fresh default (<c>m_Fresh = false</c>, from the save page).</summary>
    void NameUsed();

    /// <summary>The load page copies the loaded save's name in, with the cursor at its end (freshness unchanged).</summary>
    void SetName(string name);
}

/// <summary>What other pages call on the level select (<c>CFEPLevelSelect</c>).</summary>
public interface ILevelSelect
{
    /// <summary><c>SyncToSelectedLevel</c> (0x00460310).</summary>
    void SyncToSelectedLevel();
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

/// <summary><c>CFrontEnd::AutoSaveMode</c>.</summary>
public enum AutoSaveMode
{
    /// <summary>An explicit save: the first save of a career.</summary>
    Not,

    /// <summary>The usual: the career is saved when it changes, at the end of levels.</summary>
    Normal,

    /// <summary>As normal, but nothing is written.</summary>
    Pretend,
}

/// <summary><c>EFrontEndSound</c>, as the pages use them.</summary>
public enum FrontEndSound
{
    Move,
    Select,
    Back,
}
