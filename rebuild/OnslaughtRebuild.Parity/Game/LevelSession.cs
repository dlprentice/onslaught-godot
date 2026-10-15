// SPDX-License-Identifier: GPL-3.0-or-later
// The level start's order from bea-decomp src/game.cpp (CGame::InitRestartLoop's script part, game.cpp:366 and 384;
// CGame::LoadLevel's players after the load, 716; PostLoadProcess's start positions, 783) and src/world.cpp (the load).

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
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
    private LevelSession(ThingContext context, Game game, LevelResources resources)
    {
        Context = context;
        Game = game;
        Resources = resources;
    }

    public ThingContext Context { get; }

    public Game Game { get; }

    public LevelResources Resources { get; }

    /// <summary>Loads level <paramref name="level"/> from the files <paramref name="read"/> returns (name, bytes).</summary>
    public static LevelSession Load(int level, Func<string, (string Name, byte[] Bytes)> read, IMessageVoice? voice = null)
    {
        (string pname, byte[] pbytes) = read(@"data\default physics.dat");
        PhysicsRegistry physics = PhysicsRegistry.Build(PhysicsScript.Read(MemBuffer.FromFile(pname, pbytes)));
        (string bname, byte[] bbytes) = read(@"data\battle engine configurations.dat");
        BattleEngineDataManager battleEngines = BattleEngineDataManager.Load(MemBuffer.FromFile(bname, bbytes));
        (string rname, byte[] rbytes) = read($@"data\Resources\{level}_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, read);

        var clock = new Clock.PlatformClock();
        var events = new EventManager();
        var services = new ScriptGameServices(NullGameServices.Instance);
        var game = new Game(clock, events, services) { CurrentLevel = level };
        game.InitRestartLoop();
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
        scripts.MessageBox = new MessageBox(context, text, voice ?? new UnmodelledVoice());

        var spawner = new ParitySpawner(context) { SpawnBattleEngineThing = () => new BattleEngine(context) };
        scripts.Spawner = spawner;
        scripts.Waypoints = spawner.Waypoints;
        new WorldLoader(context, resources, spawner).Load(level, isBase: false);

        // CGame::LoadLevel's players after the load, then PostLoadProcess's start positions.
        game.CreatePlayers(context, multiplayerWorld: false);
        game.AssignPlayersToStarts(context);
        scripts.Player = n => game.GetPlayer(n) is { } player ? (true, player.BattleEngine) : (false, null);
        return new LevelSession(context, game, resources);
    }
}
