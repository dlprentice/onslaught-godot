// SPDX-License-Identifier: GPL-3.0-or-later
// The level start's order from bea-decomp src/game.cpp (CGame::InitRestartLoop's script part, game.cpp:366 and 384;
// CGame::LoadLevel's players after the load, 716; PostLoadProcess's start positions, 783) and src/world.cpp (the load).

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Script;
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
    private IEnumerator<int>? _mainLoop;
    private bool _started;

    private LevelSession(ThingContext context, Game game, LevelResources resources, Clock.PlatformClock clock)
    {
        Context = context;
        Game = game;
        Resources = resources;
        Clock = clock;
    }

    /// <summary>The keyboard, mouse and controllers.</summary>
    public LevelInput Input { get; private init; } = null!;

    /// <summary>A replay tape: its events are delivered after the Present they follow.</summary>
    public RetailTape? Tape { get; set; }

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
        long before = Clock.PresentCount;
        if (!_started)
        {
            _started = true;
            if (!FromFrontEnd)
            {
                Clock.PresentDevice();
                Clock.GetSysTimeFloat();
                Clock.Present();
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

            // RestartLoopRunLevel after the pre-run and the loading screen's last Present: each unit starts its looping init noise (one draw for the chain).
            foreach (Thing thing in Context.World.UnitNB.ToList())
            {
                if (thing is Unit { UnitData: { } data } && data.EffectNames[11] is { } noise && Context.SoundEffects is { } sounds)
                {
                    sounds.PlayEffect(sounds.Find(System.Text.Encoding.Latin1.GetString(noise)), Context.Crt);
                }
            }
            return;
        }

        if (Tape is not null)
        {
            Input.Deliver(Tape.EventsAt(before));
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

                Context.Skip("CGame::RunLevel's close: the samples killed");
                if (Game.Quit is OnslaughtRebuild.Parity.Clock.QuitType.UserQuitToFrontEnd or OnslaughtRebuild.Parity.Clock.QuitType.RestartLevel
                    or OnslaughtRebuild.Parity.Clock.QuitType.UserQuitToTitleScreen)
                {
                    // CFrontEnd::PlaySound(FES_SELECT) (0046e1b7), then SOUND.UpdateStatus once.
                    if (Context.SoundEffects is { } sounds)
                    {
                        sounds.PlayEffect(sounds.Find("Front End Select"), Context.Crt);
                    }
                    else
                    {
                        Context.Skip("CFrontEnd::PlaySound");
                    }
                }

                Context.Skip("CGame::RunLevel's close: the music stopped, the language bank reloaded");
                Game.FillOutEndLevelData(EndLevelData, Context.World);
                Ended = true;
                return;
            }
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
        var host = new LevelHost(Clock, Input.Input, Context.Bindings!, Input.Shared, Context.Crt, EndLevelData, SlotsOf(career),
            Loading ?? new Flow.LoadingScreen(Clock, () => Input.Input.Process(Clock.QueryPerformanceCounter())), career.SetSlot)
        {
            Career = career,
            MenuHost = Input.PauseMenuHost,
            ParticleNoise = Context.Particles?.Noise,
            ParticleAllocations = Context.Particles?.Manager.Allocations ?? 0,
        };
        LevelSession next = Load(Level, Read, Voice, OtherChunk, host);
        next.Game.Loop.CarryFrameTimes(Game.Loop);
        next.Tape = Tape;
        return next;
    }

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
        Func<uint, Data.ChunkReader, bool>? otherChunk = null, LevelHost? host = null)
    {
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
        scripts.MessageBox = new MessageBox(context, text, voice?.Invoke(clock) ?? new UnmodelledVoice());

        // The sound manager's effects (data\sounds\sounds.sfx).
        context.SoundEffects = Sound.SoundEffects.Load(read(Sound.SoundEffects.FileName).Bytes);

        // CGame::LoadResources's particle set.
        var particleSet = new ParticleSet();
        if (!particleSet.ReadData(read(ParticleSet.InGameFile).Bytes))
        {
            throw new InvalidDataException("the particle set did not load");
        }

        context.Particles = new LevelParticles(context, particleSet) { Noise = host?.ParticleNoise };
        context.Particles.Manager.Allocations = host?.ParticleAllocations ?? 0;

        var spawner = new ParitySpawner(context) { SpawnBattleEngineThing = () => new BattleEngine(context) };
        scripts.Spawner = spawner;
        scripts.Waypoints = spawner.Waypoints;
        new WorldLoader(context, resources, spawner).Load(level, isBase: false);

        // CGame::LoadLevel's players after the load, then PostLoadProcess's start positions, its map-who sort (trees
        // to the back of each sector's list, which orders the collision detectors' scans) and the quick-collision map.
        game.CreatePlayers(context, multiplayerWorld: false);
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
                sounds.PlayEffect(sounds.Find(name), context.Crt);
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
        context.MouseLook.Platform = input.Input.Mouse;
        context.MouseLook.Shared = input.Shared;

        // Retail draws the player's battle engine each render, which asks its motion controller for every part; the
        // particles step and draw after the world.
        input.Rendered = numRenders =>
        {
            // CGame::Render counts each render first (mRenderFrameNumber, 0 until the level's first render).
            context.RenderFrameNumber++;
            foreach (MCBattleEngine mc in context.MotionControllers)
            {
                mc.Rendered();
            }

            context.Particles.Render(numRenders);

            // CDXEngine::PostRender after every frame: the message log, the briefing log, the pause menu.
            input.MessageLog?.Render();
            input.BriefingLog?.Render();
            input.PauseMenu?.Render();
        };
        return new LevelSession(context, game, resources, clock)
        {
            Input = input,
            EndLevelData = host?.EndLevelData ?? new OnslaughtRebuild.Parity.Career.EndLevelData(),
            FromFrontEnd = host is not null,
            Loading = host?.Loading,
            Career = career,
            Level = level,
            Read = read,
            Voice = voice,
            OtherChunk = otherChunk,
        };
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

    /// <summary>CPDSimpleSprite's sNoise once built (sNoiseBuilt is the process's: a restart does not build it again).</summary>
    public float[]? ParticleNoise { get; init; }

    /// <summary>CParticleManager's sAllocations, a process static (its load test's sequence carries on through a restart).</summary>
    public int ParticleAllocations { get; init; }
}
