// SPDX-License-Identifier: GPL-3.0-only
// CSystem::Run's parts on one process (onslaught-decomp src/System.cpp, src/FrontEnd.cpp, src/game.cpp): the boot films, the
// front end and the levels it starts share the platform clock, the window's input, the bindings, the C runtime's rand()
// and END_LEVEL_DATA.

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>
/// The game from boot to quit as the parity core runs it, Present by Present on one clock: CSystem::Init's films, then
/// <see cref="SystemFlow"/> with the front end and each level it starts (<see cref="LevelSession"/> on a
/// <see cref="LevelHost"/>). Tape events are delivered after the Present they follow: keys, characters and window-mouse
/// events as messages, and device mouse movement into DirectInput while a level runs.
/// </summary>
public sealed class GameRun : ISystemParts
{
    private readonly Func<string, (string Name, byte[] Bytes)> _read;
    private readonly Func<PlatformClock, IMessageVoice>? _voice;
    private readonly FilmPlayer _films;
    private readonly bool _skipFmv;

    public GameRun(Func<string, (string Name, byte[] Bytes)> read, Func<int, string>? pcText, IFilmFiles films,
        bool skipFmv, Func<PlatformClock, IMessageVoice>? voice = null, VirtualSoundProcessOptions? soundOptions = null)
    {
        _read = read;
        _voice = voice;
        _skipFmv = skipFmv;
        Input = new PlatformInput(new PlatformWindow());
        Bindings = ControlBindings.Defaults();
        Controls = new ControllerContext(Bindings, Input, Shared, new FrontEndGame(), Clock.GetSysTimeFloat);
        _films = new FilmPlayer(Clock, Input, Controls, skipFmv ? new UnopenedFilms(films) : films, new HarnessFilmPacing(Clock));
        FrontEnd = FrontEndSetup.Create(Clock, Controls, Bindings, read, pcText, out Career.CareerOptions? options);
        if (options is not null)
        {
            Shared.MouseSensitivity = options.MouseSensitivity;
        }

        FrontEnd.Crt = Crt;
        Loading = new LoadingScreen(Clock, () => Input.Process(Clock.QueryPerformanceCounter()));
        FrontEnd.Loading = Loading;
        FrontEnd.WriteDefaultOptionsFile = bytes => DefaultOptionsFile = bytes;
        Sounds = SoundEffects.Load(read(SoundEffects.FileName).Bytes);
        SoundProcess = soundOptions is null ? null : new SoundProcess(Sounds, Clock, soundOptions);
        FrontEnd.SoundProcess = SoundProcess;
        FrontEnd.PlayEffect = name =>
        {
            if (Sounds.EventKernelConfigured)
                Sounds.PlayEffect(Sounds.Find(name), null, Crt.MainThread, new SoundPlayback(Volume: 1));
            else Sounds.PlayEffect(Sounds.Find(name), Crt.MainThread);
        };
        Clock.Presented = Presented;
    }

    public PlatformClock Clock { get; } = new();

    public SoundEffects Sounds { get; }

    /// <summary>Null means the explicitly legacy draw/voice projection, not complete sound parity.</summary>
    public SoundProcess? SoundProcess { get; }

    public PlatformInput Input { get; }

    public ControlBindings Bindings { get; }

    public ControllerShared Shared { get; } = new();

    public ControllerContext Controls { get; }

    /// <summary>The process's C runtime (its main thread's rand() is the one the front end and the levels draw from).</summary>
    public Crt.CrtRand Crt { get; } = new();

    public FrontEnd FrontEnd { get; }

    /// <summary>defaultoptions.bea as the options page last wrote it, or null.</summary>
    public byte[]? DefaultOptionsFile { get; private set; }

    /// <summary>CONSOLE's loading screen.</summary>
    public LoadingScreen Loading { get; }

    /// <summary>The level whose load is under way (its loading screen's Presents come before <see cref="Level"/> is set).</summary>
    public int? LoadingLevel { get; private set; }

    /// <summary>The level running now, or the last one.</summary>
    public LevelSession? Level { get; private set; }

    private RetailTape? _tape;

    /// <summary>A replay tape, by Present since boot. It must be selected before the first Present.</summary>
    public RetailTape? Tape
    {
        get => _tape;
        set
        {
            if (Clock.PresentCount != 0 && !ReferenceEquals(value, _tape))
                throw new InvalidOperationException("Select the tape before the first Present.");
            if (value is not null && Clock.TapeOwner is { } owner && !ReferenceEquals(owner, this))
                throw new InvalidOperationException("This clock already has a tape owner.");
            if (value is not null) Clock.TapeOwner = this;
            else if (ReferenceEquals(Clock.TapeOwner, this)) Clock.TapeOwner = null;
            _tape = value;
        }
    }

    /// <summary>Called after each Present, before its tape events, like the port's probe sampling.</summary>
    public Action? BeforeTapeInput { get; set; }

    /// <summary>Called after each Present, after the tape's events for it are delivered.</summary>
    public Action? OnPresented { get; set; }

    /// <summary>A safety stop: the run throws once the clock passes this Present.</summary>
    public long LastPresent { get; set; } = long.MaxValue;

    public SystemFlow? Flow { get; private set; }

    /// <summary>
    /// The boot (CSystem::Init's films, none with -skipfmv, and the Presents before the front end), then CSystem::Run
    /// until the front end quits.
    /// </summary>
    public int Run()
    {
        if (Tape is not null) Input.PrepareTape(Tape);
        Input.FlushInputBuffers();
        // The device makes Present 1 before CSystem::Init. Its first call with a non-zero QPC frequency then
        // establishes GetSysTimeFloat's origin, before the films (port clock traces, 2026-10-17). Setting the origin
        // before this Present shifts the full-boot clock by 333,333 ticks and changes the front-end filter at P2289.
        Clock.PresentDevice();
        Clock.GetSysTimeFloat();
        SoundProcess?.InitialiseAfterFirstPresent(FrontEnd.Career.Record.SoundVolume);
        if (!_skipFmv)
        {
            BootFilms.Play(_films, new BootFilms.Settings(LevelGiven: false, StraightToDeviceSelect: false, SkipFmv: false, Attract: false));
        }

        for (int i = 0; i < BootFilms.PresentsAfterFilms; i++)
        {
            Clock.Present();
        }

        // The boot's last Present is the loading screen's: the front end's first Init comes within its 0.05 s throttle
        // and does not render (the port's full-boot run: CFrontEnd::Init and FEP_INTRO at 2288, with no Present between).
        Loading.RenderedByBoot();

        Flow = new SystemFlow(this);
        return Flow.Run();
    }

    public int RunFrontEnd(FrontEndEntry entry)
    {
        if (!FrontEnd.Init(entry))
        {
            throw new InvalidOperationException("the front end did not initialise");
        }

        Input.Window.FrontEndRunning = true;
        while (FrontEnd.Step())
        {
        }

        FrontEnd.Shutdown();
        Input.Window.FrontEndRunning = false;
        return FrontEnd.Quit;
    }

    /// <summary>
    /// For each level, a reader of the resource file's chunks the simulation does not use (its textures and surf), from the
    /// host that draws it; called on the run's thread as the level loads. It never changes the simulation.
    /// </summary>
    public Func<int, Func<uint, Data.ChunkReader, bool>?>? LevelChunks { get; set; }

    /// <summary>For each level, where its 2D overlays draw (<see cref="LevelSession.Draw"/>), from the host; null draws nothing.</summary>
    public Func<Draw.FrontEndDraw?>? LevelDraw { get; set; }

    private readonly Game.GameRenderStatics _renderStatics = new();

    public QuitType RunLevel(int level)
    {
        int[] slots = new int[Career.CareerRecord.SlotWordCount];
        for (int word = 0; word < slots.Length; word++)
        {
            slots[word] = FrontEnd.Career.Record.SlotWord(word);
        }

        LoadingLevel = level;
        Level = LevelSession.Load(level, _read, _voice, LevelChunks?.Invoke(level),
            host: new LevelHost(Clock, Input, Bindings, Shared, Crt.MainThread, FrontEnd.EndLevelData, slots, Loading, FrontEnd.Career.SetSlot)
            {
                Career = FrontEnd.Career,
                MenuHost = FrontEnd,
                ParticleState = FrontEnd.ParticleState,
                Sounds = Sounds,
                SoundProcess = SoundProcess,
                RenderStatics = _renderStatics,
                IntroFilms = _skipFmv ? null : _films,
            });
        LoadingLevel = null;
        Level.Draw = LevelDraw?.Invoke();
        while (true)
        {
            while (!Level.Ended)
            {
                Level.AdvanceToNextPresent();
            }

            if (Level.Game.Quit != QuitType.RestartLevel)
            {
                break;
            }

            // RunLevel's restart loop: the level again on the same process.
            Level = Level.Restart();
        }

        // ShutdownRestartLoop and CGame::Shutdown, with the outro of a level won.
        Level.Shutdown(_films);
        return Level.Game.Quit;
    }

    public bool PlayAttractFilm(string name) => _films.PlayAttractFilm(name);

    public bool PlayVendorFilm => false;

    private void Presented()
    {
        long p = Clock.PresentCount;
        if (p > LastPresent)
        {
            throw new InvalidOperationException($"the run passed Present {LastPresent}");
        }

        BeforeTapeInput?.Invoke();
        if (Tape is not null)
        {
            foreach (RetailTapeEvent e in Tape.EventsAt(p))
            {
                switch (e)
                {
                    case RetailMouseEvent mouse:
                        // The replay buffer belongs to the process. Reads consume motion, not the held button level,
                        // so the same state survives shutdown, front-end Presents and later level devices.
                        Input.AddTapeMouse(mouse);
                        break;
                    default:
                        Input.PostTapeMessage(e);
                        break;
                }
            }
        }

        OnPresented?.Invoke();
    }
}

/// <summary>
/// The films under -skipfmv, where CBinkOpenThread::Run skips BinkOpen: a film that exists plays but never reaches its
/// last frame, so the attract films present until a skip (retail's idle -skipfmv front end hangs in the logo film from
/// Present 905, B's measurement against the port's R2 reference).
/// </summary>
public sealed class UnopenedFilms(IFilmFiles files) : IFilmFiles
{
    public FilmHeader? Probe(string name) =>
        files.Probe(name) is { } film ? film with { Frames = int.MaxValue } : null;
}
