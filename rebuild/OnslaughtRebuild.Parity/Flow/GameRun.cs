// SPDX-License-Identifier: GPL-3.0-or-later
// CSystem::Run's parts on one process (bea-decomp src/System.cpp, src/FrontEnd.cpp, src/game.cpp): the boot films, the
// front end and the levels it starts share the platform clock, the window's input, the bindings, the C runtime's rand()
// and END_LEVEL_DATA.

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Script;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>
/// The game from boot to quit as the parity core runs it, Present by Present on one clock: CSystem::Init's films, then
/// <see cref="SystemFlow"/> with the front end and each level it starts (<see cref="LevelSession"/> on a
/// <see cref="LevelHost"/>). A tape's events are delivered after the Present they follow: keys as window messages, mouse
/// movement into DirectInput while a level runs.
/// </summary>
public sealed class GameRun : ISystemParts
{
    private readonly Func<string, (string Name, byte[] Bytes)> _read;
    private readonly Func<PlatformClock, IMessageVoice>? _voice;
    private readonly FilmPlayer _films;
    private readonly bool _skipFmv;

    public GameRun(Func<string, (string Name, byte[] Bytes)> read, Func<int, string>? pcText, IFilmFiles films,
        bool skipFmv, Func<PlatformClock, IMessageVoice>? voice = null)
    {
        _read = read;
        _voice = voice;
        _skipFmv = skipFmv;
        Input = new PlatformInput(new PlatformWindow());
        Bindings = ControlBindings.Defaults();
        Controls = new ControllerContext(Bindings, Input, Shared, new FrontEndGame(), Clock.GetSysTimeFloat);
        _films = new FilmPlayer(Clock, Input, Controls, skipFmv ? new UnopenedFilms(films) : films, new HarnessFilmPacing());
        FrontEnd = FrontEndSetup.Create(Clock, Controls, Bindings, read, pcText, out Career.CareerOptions? options);
        if (options is not null)
        {
            Shared.MouseSensitivity = options.MouseSensitivity;
        }

        FrontEnd.Crt = Crt;
        Loading = new LoadingScreen(Clock, () => Input.Process(Clock.QueryPerformanceCounter()));
        FrontEnd.Loading = Loading;
        FrontEnd.WriteDefaultOptionsFile = bytes => DefaultOptionsFile = bytes;
        Sound.SoundEffects sounds = Sound.SoundEffects.Load(read(Sound.SoundEffects.FileName).Bytes);
        FrontEnd.PlayEffect = name => sounds.PlayEffect(sounds.Find(name), Crt.MainThread);
        Clock.Presented = Presented;
    }

    public PlatformClock Clock { get; } = new();

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

    /// <summary>The level running now, or the last one.</summary>
    public LevelSession? Level { get; private set; }

    /// <summary>A replay tape, by Present since boot.</summary>
    public RetailTape? Tape { get; set; }

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
        Input.FlushInputBuffers();
        // The device makes Present 1 before CSystem::Init. Its first call with a non-zero QPC frequency then
        // establishes GetSysTimeFloat's origin, before the films (port clock traces, 2026-10-17). Setting the origin
        // before this Present shifts the full-boot clock by 333,333 ticks and changes the front-end filter at P2289.
        Clock.Present();
        Clock.GetSysTimeFloat();
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

        Input.Window.FrontEndRunning = false;
        return FrontEnd.Quit;
    }

    public QuitType RunLevel(int level)
    {
        int[] slots = new int[Career.CareerRecord.SlotWordCount];
        for (int word = 0; word < slots.Length; word++)
        {
            slots[word] = FrontEnd.Career.Record.SlotWord(word);
        }

        Level = LevelSession.Load(level, _read, _voice,
            host: new LevelHost(Clock, Input, Bindings, Shared, Crt.MainThread, FrontEnd.EndLevelData, slots, Loading, FrontEnd.Career.SetSlot)
            {
                Career = FrontEnd.Career,
                MenuHost = FrontEnd,
            });
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

        if (Tape is not null)
        {
            foreach (RetailTapeEvent e in Tape.EventsAt(p))
            {
                switch (e)
                {
                    case RetailKeyEvent key:
                        Input.PostTapeKey(key.VirtualKey, key.ScanCode, key.Down);
                        break;
                    case RetailMouseEvent mouse when Level is { Ended: false } level:
                        level.Input.Mouse.AddMouseDelta(mouse.DeltaX, mouse.DeltaY, mouse.Wheel, (byte)mouse.Buttons);
                        break;
                    case RetailMouseEvent:
                        throw new NotSupportedException($"a tape's mouse event at Present {p} outside a level (the front end reads the window's cursor)");
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
