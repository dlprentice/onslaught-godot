// SPDX-License-Identifier: GPL-3.0-only
// The level start's order from onslaught-decomp src/game.cpp (CGame::InitRestartLoop's script part, game.cpp:366 and 384;
// CGame::LoadLevel's players after the load, 716; PostLoadProcess's start positions, 783) and src/world.cpp (the load).

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Text;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// A level loaded and ready to update, as the parity core runs it: the physics and battle-engine data, the level's
/// resources, the game and its random stream, the script runtime with the English text and message box, the world
/// loaded, and the players on their starts. The host supplies the game's files (by their game-relative paths).
/// </summary>
public sealed class LevelSession
{
    /// <summary>
    /// Where the level's 2D overlays draw (the pause and end menus, the end-of-level screen), when a host draws them; each
    /// frame is presented just before the level's flip. Drawing never changes the simulation.
    /// </summary>
    public Flow.Draw.FrontEndDraw? Draw { get; set; }

    private IEnumerator<int>? _mainLoop;
    private bool _started;
    private bool _startUnitNoisePending;
    private bool _soundReleased;

    private LevelSession(ThingContext context, Game game, LevelResources resources, Clock.PlatformClock clock)
    {
        Context = context;
        Game = game;
        Resources = resources;
        Clock = clock;
    }

    /// <summary>The keyboard, mouse and controllers.</summary>
    public LevelInput Input { get; private init; } = null!;

    private RetailTape? _tape;
    private string? _tapeInputFailure;
    private bool _tapeContinuation;

    /// <summary>
    /// A legacy K/M replay tape. CE cursor, window and character tapes are refused until standalone per-Present
    /// delivery is modeled; use the whole-process <see cref="Flow.GameRun"/>. Legacy input skipped by a multi-Present
    /// advance also fails explicitly rather than being discarded.
    /// </summary>
    public RetailTape? Tape
    {
        get => _tape;
        set
        {
            if (_started && !ReferenceEquals(value, _tape))
                throw new InvalidOperationException("Select the tape before starting a standalone level.");
            if (value is not null && (value.UsesTapeCursor
                || value.Events.Any(e => e is not (RetailKeyEvent or RetailMouseEvent))))
                throw new NotSupportedException("Standalone LevelSession cannot yet replay CE window or character input; use GameRun.");
            _tape = value;
        }
    }

    /// <summary>The platform clock the game reads; each Flip is a Present.</summary>
    public Clock.PlatformClock Clock { get; }

    /// <summary>The voice of a run driven Present by Present: the virtual mixer's (<see cref="VirtualVoice"/>).</summary>
    public static Func<Clock.PlatformClock, IMessageVoice> VirtualVoiceFor(Func<string, (string Name, byte[] Bytes)> read) =>
        clock => new VirtualVoice(clock, read);

    public long PresentCount => Clock.PresentCount;

    /// <summary>
    /// The next Present, as a <c>-level N -skipfmv</c> start in the retail harness runs it (Clock/ClockSimulation's
    /// level-100 order, matched to C's golden-640 run): Presents 1 and 2 before the level (the boot's first clock read
    /// after Present 1); after Present 2 the base time is read and PreRun runs with no Present; Present 3 is the loading
    /// screen at 100%; MainLoop's frames follow, from Present 4.
    /// </summary>
    public void AdvanceToNextPresent()
    {
        if (_tapeInputFailure is not null) throw new NotSupportedException(_tapeInputFailure);
        long before = Clock.PresentCount;
        if (!_started)
        {
            if (!_tapeContinuation && Tape is { Events.Count: > 0 } tape && tape.FirstFrame < before)
                throw new NotSupportedException("The standalone level was given a tape with already-passed input; use GameRun.");
            if (Tape is not null)
            {
                Input.Input.PrepareTape(Tape);
                Input.Mouse.UseReplayState(Input.Input.DeviceMouse);
            }
            _started = true;
            if (!FromFrontEnd)
            {
                Clock.PresentDevice();
                Clock.GetSysTimeFloat();
                SoundProcess?.InitialiseAfterFirstPresent(Career!.Record.SoundVolume);
                Clock.Present();
            }

            if (IntroFilms is { } films && Flow.FmvLookup.Of(Level, 0) is var intro && intro != -1)
            {
                // RestartLoopRunLevel with an intro (game.cpp 1259): SetLoadingFraction(1.0f) shows the 100% screen and
                // SetLoading(FALSE) releases it, then RunIntroFMV (game.cpp 1070); the later fractions find no screen.
                if (Loading is { } beforeFilm)
                {
                    beforeFilm.Complete();
                    beforeFilm.SetLoading(false);
                }

                PlayCutscene(films, intro);
            }

            Game.StartLevelTiming();
            Game.PreRun();

            if (Loading is { } loading)
            {
                // SetLoadingFraction(1.0f): the 100% screen; RestartLoopRunLevel then releases the loading screen.
                loading.Complete();
                loading.SetLoading(false);
            }
            else
            {
                Clock.Present();
            }

            // Return at the loading Present itself, where the native probe is taken. Startup noises happen only
            // after that Present returns to RestartLoopRunLevel, not before the caller can observe this boundary.
            RefuseSkippedTapeEvents(before, Clock.PresentCount);
            _startUnitNoisePending = true;
            return;
        }

        if (Tape is not null)
        {
            Input.Deliver(Tape.EventsAt(before));
        }

        if (_startUnitNoisePending)
        {
            _startUnitNoisePending = false;
            // RestartLoopRunLevel: each unit starts its init noise after the loading screen's last Present.
            foreach (Thing thing in Context.World.UnitNB.ToList())
            {
                if (thing is Unit { UnitData: { } data } && data.EffectNames[11] is { } noise && Context.SoundEffects is { } sounds)
                {
                    var effect = sounds.Find(System.Text.Encoding.Latin1.GetString(noise));
                    if (sounds.EventKernelConfigured)
                        sounds.PlayEffect(effect, thing, Context.Crt,
                            new SoundPlayback(Volume: .7f, Tracking: SoundTracking.FollowAndDie, Once: true, Repeat: true));
                    else sounds.PlayEffect(effect, Context.Crt);
                }
            }
        }

        if (Ended)
        {
            throw new InvalidOperationException("the level has ended (CGame::RunLevel returned)");
        }

        while (Clock.PresentCount == before)
        {
            _mainLoop ??= Game.MainLoop().GetEnumerator();
            if (!_mainLoop.MoveNext())
            {
                // One CGame::MainLoop done; RunLevel repeats it until mQuit is set. Then RunLevel's close fills the
                // end-of-level data before the world is cleared (game.cpp 1506), and the front end takes it from there.
                _mainLoop = null;
                if (Game.Quit == OnslaughtRebuild.Parity.Clock.QuitType.None)
                {
                    continue;
                }

                Context.SoundEffects?.KillAllSamples();
                if (Game.Quit is OnslaughtRebuild.Parity.Clock.QuitType.UserQuitToFrontEnd or OnslaughtRebuild.Parity.Clock.QuitType.RestartLevel
                    or OnslaughtRebuild.Parity.Clock.QuitType.UserQuitToTitleScreen)
                {
                    // CFrontEnd::PlaySound(FES_SELECT) (0046e1b7), then SOUND.UpdateStatus once.
                    if (Context.SoundEffects is { } sounds)
                    {
                        if (sounds.EventKernelConfigured)
                        {
                            sounds.PlayEffect(sounds.Find("Front End Select"), null, Context.Crt, new SoundPlayback(Volume: 1));
                            sounds.UpdateStatus();
                        }
                        else sounds.PlayEffect(sounds.Find("Front End Select"), Context.Crt);
                    }
                    else
                    {
                        Context.Skip("CFrontEnd::PlaySound");
                    }
                }

                Context.Skip("CGame::RunLevel's close: the music stopped, the language bank reloaded");
                Game.FillOutEndLevelData(EndLevelData, Context.World);
                if (Context.Particles is { } particles) particles.Manager.State.RenderFraction = Game.Loop.Fraction;
                RefuseSkippedTapeEvents(before + 1, Clock.PresentCount);
                Ended = true;
                return;
            }
        }
        RefuseSkippedTapeEvents(before + 1, Clock.PresentCount);
    }

    private void RefuseSkippedTapeEvents(long first, long endExclusive)
    {
        if (Tape is null) return;
        for (long present = first; present < endExclusive; present++)
            if (!Tape.EventsAt(present).IsEmpty)
            {
                _tapeInputFailure = $"Standalone LevelSession skipped tape input at Present {present}; use GameRun.";
                throw new NotSupportedException(_tapeInputFailure);
            }
    }

    /// <summary>The level's main loop returned (it quit to the front end, timed out, restarted or the game quit).</summary>
    public bool Ended { get; private set; }

    /// <summary>END_LEVEL_DATA as RunLevel's close filled it.</summary>
    public OnslaughtRebuild.Parity.Career.EndLevelData EndLevelData { get; private init; } = new();

    /// <summary>Started from the front end (<see cref="LevelHost"/>): no boot Presents before the level.</summary>
    public bool FromFrontEnd { get; private init; }

    /// <summary>CAREER: the front end's, or a -level run's wiped boot career.</summary>
    public OnslaughtRebuild.Parity.Career.Career? Career { get; private init; }

    /// <summary>The level number this session plays.</summary>
    public int Level { get; private init; }

    private Func<string, (string Name, byte[] Bytes)> Read { get; init; } = null!;

    private Func<Clock.PlatformClock, IMessageVoice>? Voice { get; init; }

    /// <summary>The same explicitly admitted sound process across front end, level and restart; null is legacy.</summary>
    public SoundProcess? SoundProcess { get; private init; }

    private Func<uint, Data.ChunkReader, bool>? OtherChunk { get; init; }

    /// <summary>CAREER.GetSlots(), as InitRestartLoop copies them.</summary>
    public static int[] SlotsOf(OnslaughtRebuild.Parity.Career.Career career)
    {
        int[] slots = new int[OnslaughtRebuild.Parity.Career.CareerRecord.SlotWordCount];
        for (int word = 0; word < slots.Length; word++)
        {
            slots[word] = career.Record.SlotWord(word);
        }

        return slots;
    }

    /// <summary>
    /// CGame::RunLevel's restart loop after a QT_RESTART_LEVEL (the level-end or pause menu's Retry): InitRestartLoop and
    /// RestartLoopRunLevel again, on the same process: the clock, input, bindings, rand(), END_LEVEL_DATA, CAREER (whose
    /// slots the level may have set) and the loading screen, which renders once before the reload and again at 100%. The
    /// tape carries on.
    /// </summary>
    public LevelSession Restart()
    {
        if (!Ended || Game.Quit != OnslaughtRebuild.Parity.Clock.QuitType.RestartLevel)
        {
            throw new InvalidOperationException("only a level that ended with a restart restarts");
        }

        OnslaughtRebuild.Parity.Career.Career career = Career ?? throw new InvalidOperationException("a restart needs CAREER");
        ReleaseSoundForRestartOrReturn();
        var host = new LevelHost(Clock, Input.Input, Context.Bindings!, Input.Shared, Context.Crt, EndLevelData, SlotsOf(career),
            Loading ?? new Flow.LoadingScreen(Clock, () => Input.Input.Process(Clock.QueryPerformanceCounter())), career.SetSlot)
        {
            Career = career,
            MenuHost = Input.PauseMenuHost,
            ParticleState = Context.Particles?.Manager.State,
            Sounds = Context.SoundEffects,
            SoundProcess = SoundProcess,
        };
        Context.Particles?.Manager.Shutdown();
        LevelSession next = Load(Level, Read, Voice, OtherChunk, host);
        next.Game.Loop.CarryFrameTimes(Game.Loop);
        // A retry is the same process and tape, whose earlier events were handled by the preceding session.
        next._tape = _tape;
        next._tapeContinuation = _tape is not null;
        // The host's canvas belongs to the running route, not the world being replaced by Retry.
        next.Draw = Draw;
        return next;
    }

    /// <summary>
    /// Sound-side ShutdownRestartLoop0046ca70: delete MessageBox, then reset game master gain and detach the camera.
    /// The current host retains the ended world for inspection rather than executing complete WORLD.Shutdown; this
    /// is the corresponding sound-only release boundary, not a claim of complete native teardown. RunLevel close
    /// has already killed every sample before this; native deferred list/pool state survives for the next level.
    /// </summary>
    public void ReleaseSoundForRestartOrReturn()
    {
        if (!Ended) throw new InvalidOperationException("Release a level's sound only after its close has completed.");
        if (_soundReleased) return;
        _soundReleased = true;
        ScriptRuntime.Of(Context).MessageBox?.Delete();
        Context.SoundEffects?.SetGameSoundsMasterVolume(1); // native field-only reset; no status/gain refresh here.
        SoundProcess?.ReleaseLevel(Context);
    }

    /// <summary>
    /// RunLevel's close deleted the players (<c>SAFE_DELETE(mPlayer[i])</c>, game.cpp 1516): GAME's player pointers are
    /// null from then on. This host keeps the ended world for inspection; a probe of the player reads nothing.
    /// </summary>
    public bool PlayersDeleted => Ended;

    /// <summary>ShutdownRestartLoop deleted the game's random stream (<c>SAFE_DELETE(mRandomStream)</c>, game.cpp 567).</summary>
    public bool RandomStreamDeleted { get; private set; }

    /// <summary>
    /// The rest of CGame::RunLevel once its restart loop has ended on anything but a restart (game.cpp 1607-1660):
    /// ShutdownRestartLoop (the sound release, the random stream deleted, the particle manager shut down), then
    /// CGame::Shutdown (game.cpp 417), whose one step with Presents is RunOutroFMV (game.cpp 484): a level won and quit
    /// to the front end plays its outro film on <paramref name="films"/>. Under -skipfmv the film never ends (the
    /// films' <see cref="Flow.UnopenedFilms"/>), so this returns only when the player skips it.
    /// </summary>
    public void Shutdown(Flow.FilmPlayer films)
    {
        if (!Ended || Game.Quit == OnslaughtRebuild.Parity.Clock.QuitType.RestartLevel)
        {
            throw new InvalidOperationException("RunLevel shuts the level down only after its restart loop has ended");
        }

        if (_shutDown) return;
        _shutDown = true;
        ReleaseSoundForRestartOrReturn();
        RandomStreamDeleted = true;
        Context.Particles?.Manager.Shutdown();
        RunOutroFmv(films);
    }

    private bool _shutDown;

    /// <summary>
    /// CGame::RunOutroFMV (0046d9f0, game.cpp 1104): when the level quit to the front end won, its outro from
    /// lookup_FMV (fmvlookup.cpp; level 100's is cutscene 2) is marked new among the goodies (goodie 200 + n, 232 for
    /// cutscene 33) unless it is already new or old, with one more pending extra goodie, and played full screen,
    /// unskippable by select; then the input buffers are flushed.
    /// </summary>
    private void RunOutroFmv(Flow.FilmPlayer films)
    {
        if (Game.Quit != OnslaughtRebuild.Parity.Clock.QuitType.QuitToFrontEnd || EndLevelData.FinalState != OnslaughtRebuild.Parity.Career.EndLevelState.Won)
        {
            return;
        }

        if (Level is 500 or 720 or 741 or 800)
        {
            // Level 500's rocket slot and 720's secondary objectives choose the alternative outro; 741 and 800 roll the
            // credits afterwards. None is modelled (this proof of concept plays level 100 only).
            throw new NotSupportedException($"RunOutroFMV for level {Level}");
        }

        int fmv = Flow.FmvLookup.Of(Level, 1);
        if (fmv == -1)
        {
            return;
        }

        PlayCutscene(films, fmv);
    }

    /// <summary>The step RunIntroFMV and RunOutroFMV share (game.cpp 1078 and 1143): the goodie, the film, the flush.</summary>
    private void PlayCutscene(Flow.FilmPlayer films, int fmv)
    {
        string name = Flow.FmvLookup.CutsceneName(fmv, films.Files);
        int goodie = fmv == 33 ? 232 : fmv + 200;
        OnslaughtRebuild.Parity.Career.Career career = Career ?? throw new InvalidOperationException("a cutscene needs CAREER");
        if (career.Record.GoodieState(goodie) is not (OnslaughtRebuild.Parity.Career.CareerGoodieState.Old or OnslaughtRebuild.Parity.Career.CareerGoodieState.New))
        {
            career.Record.SetGoodieState(goodie, OnslaughtRebuild.Parity.Career.CareerGoodieState.New);
            career.Record.PendingExtraGoodies++;
        }

        films.PlayFullscreen(name, skippable: false);
        Input.Input.FlushInputBuffers();
    }

    /// <summary>The level's intro film player (<see cref="LevelHost.IntroFilms"/>), for its first time round.</summary>
    public Flow.FilmPlayer? IntroFilms { get; private init; }

    /// <summary>The process's loading screen, for a level from the front end.</summary>
    public Flow.LoadingScreen? Loading { get; private init; }

    public ThingContext Context { get; }

    public Game Game { get; }

    public LevelResources Resources { get; }

    /// <summary>
    /// Loads level <paramref name="level"/> from the files <paramref name="read"/> returns (name, bytes). A renderer
    /// reads the resource file's other chunks (its textures) through <paramref name="otherChunk"/>. The voice is made on
    /// the session's clock by <paramref name="voice"/>; without one, a voice stops the run (<see cref="UnmodelledVoice"/>).
    /// A run driven Present by Present plays voices as the virtual mixer does: <see cref="VirtualVoiceFor"/>.
    /// </summary>
    public static LevelSession Load(int level, Func<string, (string Name, byte[] Bytes)> read,
        Func<Clock.PlatformClock, IMessageVoice>? voice = null,
        Func<uint, Data.ChunkReader, bool>? otherChunk = null, LevelHost? host = null,
        VirtualSoundProcessOptions? soundOptions = null,
        Func<OnslaughtRebuild.Parity.Career.CareerOptions?, SoundDeviceProcessOptions>? soundDeviceOptions = null)
    {
        if (host is not null && (soundOptions is not null || soundDeviceOptions is not null))
            throw new ArgumentException("A hosted level inherits its process sound profile.", nameof(soundOptions));
        if (soundOptions is not null && soundDeviceOptions is not null)
            throw new ArgumentException("Choose one process sound device.", nameof(soundDeviceOptions));
        (string pname, byte[] pbytes) = read(@"data\default physics.dat");
        PhysicsRegistry physics = PhysicsRegistry.Build(PhysicsScript.Read(MemBuffer.FromFile(pname, pbytes)));
        (string bname, byte[] bbytes) = read(@"data\battle engine configurations.dat");
        BattleEngineDataManager battleEngines = BattleEngineDataManager.Load(MemBuffer.FromFile(bname, bbytes));
        (string rname, byte[] rbytes) = read($@"data\Resources\{level}_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, read, otherChunk: otherChunk);

        var clock = host?.Clock ?? new Clock.PlatformClock();
        if (host is not null)
        {
            // From the front end, RunLevel's CONSOLE.SetLoading(TRUE) and RenderLoadingScreen: the screen's first render
            // of the load, before the load draws anything (the port's front-end run); the load's later fractions fall
            // in the same instant and do not render.
            host.Loading.SetLoading(true);
            host.Loading.Render();
        }

        var events = new EventManager();
        LevelInput input;
        Platform.ControlBindings bindings;
        OnslaughtRebuild.Parity.Career.Career? bootCareer = null;
        OnslaughtRebuild.Parity.Career.CareerOptions? bootOptions = null;
        if (host is not null)
        {
            // Started from the front end: the process's window, input, bindings and controller state carry on.
            bindings = host.Bindings;
            input = new LevelInput(clock, bindings, host.Input, host.Shared);
        }
        else
        {
            // WinMain's boot load of the saved options (defaultoptions.bea): the bindings and gMouseSensitivity, and the
            // career wiped (CAREER, which a -level run keeps for the level's slots).
            bindings = Platform.ControlBindings.Defaults();
            OnslaughtRebuild.Parity.Career.CareerOptions? options = null;
            bootCareer = OnslaughtRebuild.Parity.Career.Career.Constructed();
            try
            {
                options = OnslaughtRebuild.Parity.Career.DefaultOptions.Load(bootCareer, bindings, read(OnslaughtRebuild.Parity.Career.DefaultOptions.FileName).Bytes);
            }
            catch (FileNotFoundException)
            {
            }

            input = new LevelInput(clock, bindings);
            bootOptions = options;
            if (options is not null)
            {
                input.Shared.MouseSensitivity = options.MouseSensitivity;
            }
        }
        var services = new ScriptGameServices(input);
        var game = new Game(clock, events, services) { CurrentLevel = level };
        OnslaughtRebuild.Parity.Career.Career? career = host?.Career ?? bootCareer;
        if (host is not null)
        {
            game.CareerSlots = host.CareerSlots;
            game.CareerSetSlot = host.CareerSetSlot;
        }
        else if (career is not null)
        {
            game.CareerSlots = SlotsOf(career);
            game.CareerSetSlot = career.SetSlot;
        }

        game.InitRestartLoop();
        input.InitMouse();
        var pool = new SPtrSetPool();
        pool.Init(SPtrSetPool.RetailBlockSize);
        SeedUniformRandomNumberStream random = game.RandomStream!;
        random.Log = [];
        var context = new ThingContext(pool, events, random, resources.HeightField, physics)
        {
            BattleEngines = battleEngines,
            Game = game,
        };
        if (host is not null)
        {
            // The process's C runtime: rand()'s state carries on from the front end.
            context.Crt = host.Crt;
        }

        foreach ((string name, Mesh.MeshData mesh) in resources.Meshes)
        {
            context.Meshes[name] = mesh;
        }

        // CGame::InitRestartLoop's script part: SCRIPT_EVENT_NB.Init and the message box, before the load; TEXT is the
        // English language file.
        ScriptRuntime scripts = ScriptRuntime.Of(context);
        scripts.EventNB.Init();
        services.Runtime = scripts;
        TextDatabase text = TextDatabase.Load(read(TextDatabase.FileName(0, americanText: false)).Bytes);
        scripts.Text = text;
        // One manager per process. The explicit backend profile is configured only after device Present1 below.
        context.SoundEffects = host?.SoundProcess?.Effects ?? host?.Sounds ?? SoundEffects.Load(read(SoundEffects.FileName).Bytes);
        // The main-thread host receives the options this boot actually loaded, rather than reading them twice or
        // pretending a standalone -level run came through the front end. Construction still reads no clock/RNG.
        SoundProcess? soundProcess = host?.SoundProcess ?? (soundDeviceOptions is not null
            ? new(context.SoundEffects, clock, soundDeviceOptions(bootOptions))
            : soundOptions is null ? null : new(context.SoundEffects, clock, soundOptions));
        if (host?.Sounds is not null && !ReferenceEquals(host.Sounds, context.SoundEffects))
            throw new ArgumentException("The host sound owner and configured process disagree.", nameof(host));
        IMessageVoice messageVoice = voice?.Invoke(clock) ?? (soundProcess is null ? new UnmodelledVoice() : new VirtualVoice(clock, read));
        if (soundProcess is not null)
        {
            if (messageVoice is not ISoundProcessVoice processVoice)
                throw new NotSupportedException("The admitted process requires a voice bound to its shared event owner.");
            processVoice.BindSoundProcess(soundProcess);
            soundProcess.BindLevel(context);
        }
        scripts.MessageBox = new MessageBox(context, text, messageVoice);

        // CGame::LoadResources's particle set.
        var particleSet = new ParticleSet();
        if (!particleSet.ReadData(read(ParticleSet.InGameFile).Bytes))
        {
            throw new InvalidDataException("the particle set did not load");
        }

        context.Particles = new LevelParticles(context, particleSet, host?.ParticleState);

        var spawner = new ParitySpawner(context) { SpawnBattleEngineThing = () => new BattleEngine(context) };
        scripts.Spawner = spawner;
        scripts.Waypoints = spawner.Waypoints;
        new WorldLoader(context, resources, spawner).Load(level, isBase: false);

        // CGame::LoadLevel's players after the load, then PostLoadProcess's start positions, its map-who sort (trees
        // to the back of each sector's list, which orders the collision detectors' scans) and the quick-collision map.
        game.CreatePlayers(context, multiplayerWorld: false, career);
        // PostLoadProcess's atmospherics, before the start positions: CSnow::Init (DXSnow.cpp, 00555020) places its
        // thousand flakes (x, y, z, time) and fifty offsets (x, y) with rand(), with vertex shaders enabled as here.
        for (int i = 0; i < (1000 * 4) + (50 * 2); i++)
        {
            context.Crt.Rand();
        }

        game.AssignPlayersToStarts(context);
        context.MapWho.SortEntries();
        context.Map.InitQuickCollisionMap();
        scripts.Player = n => game.GetPlayer(n) is { } player ? (true, player.BattleEngine) : (false, null);
        input.AttachPlayers(game, name =>
        {
            // CFrontEnd::PlaySound in a level: SOUND.PlayEffect(SOUND.GetEffectByName(name), NULL).
            if (context.SoundEffects is { } sounds)
            {
                if (sounds.EventKernelConfigured)
                    sounds.PlayEffect(sounds.Find(name), null, context.Crt, new SoundPlayback(Volume: 1));
                else sounds.PlayEffect(sounds.Find(name), context.Crt);
            }
            else
            {
                context.Skip("CFrontEnd::PlaySound");
            }
        });
        context.Bindings = bindings;

        // InitRestartLoop's pause menu, on the front end's menus when the level came from it.
        input.CreatePauseMenu(host?.MenuHost ?? new LevelMenuHost(context, clock, career!,
            Text.TextIds.Read(read(Text.TextIds.FileName).Bytes),
            bootOptions ?? OnslaughtRebuild.Parity.Career.CareerOptions.Read(new byte[OnslaughtRebuild.Parity.Career.CareerOptions.Size]), bindings),
            () => scripts.MessageBox!.Shown);
        // MESH_LOD_BIAS and TREE_MESH_DISTANCE follow the loaded options (the career load and SetQualityLevel set them);
        // without an options file retail keeps the globals' initial values, which the render falls back to.
        if (host is not null || bootOptions is not null)
        {
            context.RenderOptions = () => input.PauseMenuHost!.Options;
        }

        context.MouseLook.Platform = input.Input.Mouse;
        context.MouseLook.Shared = input.Shared;

        GameRenderStatics renderStatics = host?.RenderStatics ?? new GameRenderStatics();
        var frontEnd = host?.MenuHost as Flow.FrontEnd;
        LevelSession? made = null;

        // Retail draws the player's battle engine each render, which asks its motion controller for every part; the
        // particles step and draw after the world. Behind an opaque end-of-level screen (alpha 255) the world is not
        // rendered at all (CGame::Render 0046e5c8).
        input.Rendered = numRenders =>
        {
            // CGame::Render counts each render first (mRenderFrameNumber, 0 until the level's first render).
            context.RenderFrameNumber++;
            context.Particles.StartRender();
            if (game.WinLoseScreenAlpha != 255)
            {
                foreach (MCBattleEngine mc in context.MotionControllers)
                {
                    mc.Rendered();
                }

                context.Particles.Render(numRenders);
            }

            // CDXEngine::PostRender after every frame: the message log, the briefing log, the pause menu (with the front
            // end's fonts and mouse tests when the level runs in the whole game), then CGame::DrawGameStuff's end screen.
            Flow.Draw.FrontEndDraw? draw = made?.Draw;
            draw?.BeginOverlay();
            // HUD.Render (CDXEngine::PostRender 0053ed01) first: the text its render wrote (the weapon's name, the warnings,
            // the ammunition count), over the HUD's sprites, which the host draws.
            if (draw is not null && frontEnd is { NormalFont: { } normal, Texts: { } texts })
            {
                context.Particles.World.DrawHudText(draw, normal, texts);
            }

            // HUD.RenderBattleline (00487d10) while the player's camera shows the HUD: the message box's panel and text,
            // then its portrait in the battle line's circle (HUD.mViewportRight - 137, the viewport's bottom). Before the
            // portrait the circle's mask writes depth alone (blend zero/one, depth ALWAYS, clamped, z 0.005), so the
            // portrait's layers (z 0.007, LESSEQUAL) show only inside the circle.
            if (game.GetPlayer(0) is not null && game.GetCurrentCamera(0)?.GetShowHud() == true && scripts.MessageBox is { } messages)
            {
                messages.RenderOverlay(draw, frontEnd?.SmallFont);
                if (draw is not null)
                {
                    draw.Blend = Flow.Draw.CanvasBlend.Keep;
                    draw.ZAlways = true;
                    draw.DrawNamedSprite(@"hud\v2\CircleMask.tga", 640f - 137f + 64f, 480f - 64f, 0.005f, 0xffffffff, 1.5f, 1.5f,
                        Flow.Draw.SpriteAlign.Centre, Flow.Draw.SpriteType.None, 1f, 0f, -0.25f, 1.25f, -0.25f, 1.25f, clamp: true,
                        format: Flow.Draw.FrontEndTextures.AnyFormat);
                    draw.Blend = Flow.Draw.CanvasBlend.Modulate;
                    draw.ZAlways = false;
                }

                messages.RenderBattleLinePulse(draw, 640f - 137f, 480f, 7, clock.FramesPerSec);
            }

            input.MessageLog?.Render();
            input.BriefingLog?.Render();
            if (input.PauseMenu is { } pause)
            {
                if (frontEnd is not null && Flow.Menus.MenuDrawing.Of(frontEnd) is { } drawing)
                    pause.Render(drawing with { Draw = draw, OnOptionsPage = false }, new Flow.Menus.FrontEndMenuMouse(frontEnd));
                else
                    pause.Render();
            }

            EndLevelOverlay.Render(game, clock, renderStatics, draw, frontEnd?.SmallFont, frontEnd?.NormalFont, frontEnd?.Texts);
            input.RenderGameInterface(draw, frontEnd?.SmallFont, frontEnd?.NormalFont, frontEnd?.Texts);
        };
        input.BeforeFlip = () => made?.Draw?.Present(clock.PresentCount + 1);
        made = new LevelSession(context, game, resources, clock)
        {
            Input = input,
            EndLevelData = host?.EndLevelData ?? new OnslaughtRebuild.Parity.Career.EndLevelData(),
            FromFrontEnd = host is not null,
            Loading = host?.Loading,
            IntroFilms = host?.IntroFilms,
            Career = career,
            Level = level,
            Read = read,
            Voice = voice,
            SoundProcess = soundProcess,
            OtherChunk = otherChunk,
        };
        return made;
    }
}

/// <summary>
/// What a level started from the front end shares with the process (CSystem::Run's GAME.RunLevel): the platform clock,
/// the window's input, the bindings and controller state, the C runtime's main-thread rand(), END_LEVEL_DATA, which
/// the level fills as it ends and the front end reads, the career's slots (CAREER.GetSlots, which InitRestartLoop
/// copies) and CAREER.SetSlot (the script's SetSlotSave).
/// </summary>
public sealed record LevelHost(Clock.PlatformClock Clock, Platform.PlatformInput Input, Platform.ControlBindings Bindings,
    Platform.ControllerShared Shared, Crt.CrtRandStream Crt, OnslaughtRebuild.Parity.Career.EndLevelData EndLevelData, int[] CareerSlots,
    Flow.LoadingScreen Loading, Action<int, int>? CareerSetSlot = null)
{
    /// <summary>CAREER itself, kept for a restart's slots.</summary>
    public OnslaughtRebuild.Parity.Career.Career? Career { get; init; }

    /// <summary>The menus' host (the front end's), for the pause menu.</summary>
    public Flow.Menus.IMenuHost? MenuHost { get; init; }

    /// <summary>Particle globals shared with the front end and with subsequent levels/restarts.</summary>
    public ParticleProcessState? ParticleState { get; init; }

    /// <summary>The game render's process statics (<see cref="GameRenderStatics"/>), kept across levels.</summary>
    public GameRenderStatics? RenderStatics { get; init; }

    /// <summary>The process's one effect/event owner, retained even by explicitly legacy fixtures.</summary>
    public SoundEffects? Sounds { get; init; }

    /// <summary>Explicit process backend/profile binding, never constructed anew for a hosted level.</summary>
    public SoundProcess? SoundProcess { get; init; }

    /// <summary>
    /// The film player for the level's intro: null when GetIntroFMV gives none (-skipfmv, or a restart, which is not
    /// the first time round).
    /// </summary>
    public Flow.FilmPlayer? IntroFilms { get; init; }
}
