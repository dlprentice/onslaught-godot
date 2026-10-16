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
            Clock.Present();
            Clock.GetSysTimeFloat();
            Clock.Present();
            Game.StartLevelTiming();
            Game.PreRun();

            Clock.Present();

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
    public Career.EndLevelData EndLevelData { get; } = new();

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
        Func<uint, Data.ChunkReader, bool>? otherChunk = null)
    {
        (string pname, byte[] pbytes) = read(@"data\default physics.dat");
        PhysicsRegistry physics = PhysicsRegistry.Build(PhysicsScript.Read(MemBuffer.FromFile(pname, pbytes)));
        (string bname, byte[] bbytes) = read(@"data\battle engine configurations.dat");
        BattleEngineDataManager battleEngines = BattleEngineDataManager.Load(MemBuffer.FromFile(bname, bbytes));
        (string rname, byte[] rbytes) = read($@"data\Resources\{level}_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, read, otherChunk: otherChunk);

        var clock = new Clock.PlatformClock();
        var events = new EventManager();
        // WinMain's boot load of the saved options (defaultoptions.bea): the bindings and gMouseSensitivity.
        Platform.ControlBindings bindings = Platform.ControlBindings.Defaults();
        Career.CareerOptions? options = null;
        try
        {
            options = Career.DefaultOptions.Load(Career.Career.Constructed(), bindings, read(Career.DefaultOptions.FileName).Bytes);
        }
        catch (FileNotFoundException)
        {
        }

        var input = new LevelInput(clock, bindings);
        if (options is not null)
        {
            input.Shared.MouseSensitivity = options.MouseSensitivity;
        }
        var services = new ScriptGameServices(input);
        var game = new Game(clock, events, services) { CurrentLevel = level };
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

        context.Particles = new LevelParticles(context, particleSet);

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
        context.MouseLook.Platform = input.Input.Mouse;
        context.MouseLook.Sensitivity = input.Shared.MouseSensitivity;

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
        };
        return new LevelSession(context, game, resources, clock) { Input = input };
    }
}
