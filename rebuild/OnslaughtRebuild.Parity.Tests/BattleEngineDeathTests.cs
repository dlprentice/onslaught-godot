// SPDX-License-Identifier: GPL-3.0-only
// StartDieProcess0040bfd0, Explode0040dfb0, dying Move0040842a..004084a6, Shutdown00405f80.
// Pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
// State fixtures below exercise reconstructed code; only the optional native oracle executes original instructions.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineDeathTests
{
    private static BattleEngine Engine()
    {
        var pool = new SPtrSetPool(); pool.Init(128);
        var events = new EventManager(); events.Init();
        for (int n = 0; n < 40; n++) events.AdvanceTime();
        var raw = new byte[24 + OnslaughtRebuild.Parity.World.HeightField.RecordSize
            + OnslaughtRebuild.Parity.World.HeightField.SampleCount * 2];
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x102c), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x1034), 100);
        var map = OnslaughtRebuild.Parity.World.HeightField.Load(new OnslaughtRebuild.Parity.Data.ChunkReader(
            OnslaughtRebuild.Parity.Data.MemBuffer.FromFile("flat-map", raw)));
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), map, new PhysicsRegistry());
        return new BattleEngine(context)
        {
            Configuration = new BattleEngineData(), State = BattleEngineState.Jet,
            LastTimeOnGround = -100, LastTimeInWater = -100,
        };
    }

    [RetailOracleFact("be-death-move")]
    public void DyingJetArithmeticMatchesUnchangedOriginalBlockIncludingSpillBoundary()
    {
        // Native harness executes exactly0040842a..004084a6, returning through its original408d75 branch target.
        // No calls/imports/devices; PC24 and authored EBP/stack. PC53 negative control changes38/1024 cases.
        var rows = new List<string>();
        BattleEngine be = Engine();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("be-death-move"))
        {
            Assert.Equal("Move", function); Assert.Equal(6, w.Length);
            be.Velocity = RetailOracle.V(w, 0);
            be.Orientation = new FMatrix(new FVector(0, RetailOracle.F(w[3]), 0),
                new FVector(0, RetailOracle.F(w[4]), 0), new FVector(0, RetailOracle.F(w[5]), 0));
            be.Pitchvel = 17;
            be.MoveDyingBattleEngine();
            string actual = $"Move {RetailOracle.Hv(be.Velocity)} {RetailOracle.H(be.Pitchvel)}";
            Assert.True(actual == expected, $"case {rows.Count}: {actual}\nretail: {expected}");
            rows.Add(actual);
        }
        Assert.Equal(1024, rows.Count);
        RetailOracle.WriteOurs("be-death-move", rows.ToArray());
    }

    [Theory]
    [InlineData(BattleEngineState.Walker)]
    [InlineData(BattleEngineState.MorphingIntoWalker)]
    [InlineData(BattleEngineState.MorphingIntoJet)]
    public void DyingNonJetDoesNotReplaceVelocityOrPitch(BattleEngineState state)
    {
        BattleEngine be = Engine(); be.State = state;
        be.Velocity = new FVector(2, 3, 4); be.Pitchvel = .5f;
        be.MoveDyingBattleEngine();
        Assert.Equal(new FVector(2, 3, 4), be.Velocity); Assert.Equal(.5f, be.Pitchvel);
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 1)]
    public void ExplosionUsesConfigurationAttachedRadiusAndGroundBeforeWater(bool ground, bool water, int colType)
    {
        BattleEngine be = Engine(); be.Allegiance = 1;
        be.Pos = new FVector(5, 6, -2);
        if (ground) be.LastTimeOnGround = be.Context.Events.Time;
        if (water) be.LastTimeInWater = be.Context.Events.Time;
        var definition = new ExplosionData("death-fixture"u8.ToArray()) { Radius = 99, Volumetric = 20 };
        be.Configuration!.Explosion = definition.Name; be.Context.Physics.Explosions.Add(definition);
        be.Explode();
        Explosion explosion = Assert.Single(be.Context.World.Things.ToList().OfType<Explosion>());
        Assert.Same(definition, explosion.Behaviour); Assert.Equal(be.GetRadius(), explosion.Radius);
        Assert.Equal(1, explosion.Allegiance); Assert.Equal(colType, explosion.ColType);
        Assert.Null(explosion.Originator.ToRead); // BE's init leaves originator and volumetric permission at defaults.
        Assert.True((explosion.Flags & ThingFlags.DeclaredShutdown) != 0);
        Assert.False(be.IsDying);
    }

    [Fact]
    public void MissingExplosionDoesNotSpawnAnotherDefinition()
    {
        BattleEngine be = Engine(); be.Configuration!.Explosion = "missing"u8.ToArray();
        be.Context.Physics.Explosions.Add(new ExplosionData("different"u8.ToArray()));
        be.Explode(); Assert.Empty(be.Context.World.Things.ToList());
    }

    [GameDataFact]
    public void DeathDeclaresLossBeforeScriptAndKeepsWreckUntilItsShutdownEvent()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        while (session.PresentCount < 3000) session.AdvanceToNextPresent();
        BattleEngine be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        int[] a = (int[])session.Context.World.CountersA.Clone(), b = (int[])session.Context.World.CountersB.Clone();
        Camera oldCamera = Assert.IsAssignableFrom<Camera>(session.Game.GetCurrentCamera(0));
        var cameraReader = new ActiveReader<Camera>(); cameraReader.SetReader(oldCamera);
        // A sentinel Died handler proves the callback ordering: it prints while Playing, but must be suppressed
        // after DeclarePlayerDead has set LevelLost. Its code must still be deleted and its readers invalidated.
        be.Script?.Delete();
        int print = Array.FindIndex(ScriptNatives.Table, n => n.Name == "Print");
        var code = new ScriptObjectCode
        {
            Instructions = [new(Opcode.Push, 0), new(Opcode.Call, print | (1 << 8)), new(Opcode.RemoveTop, 0), new(Opcode.Return, 0)],
            EventPcs = [-1, -1, -1, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1],
            Symbols = [new Symbol("death-marker"u8.ToArray(), new ScriptValue(DataType.String, 0, 0, "DEATH_MARKER"u8.ToArray(), default), 0, 0, 1)],
            NextSymbolIndex = 1, EventFunctions = [], Unknown60 = 0, Unknown5c = 0,
        };
        var script = IScript.Create(be, code, session.Context); be.Script = script;
        var runtime = ScriptRuntime.Of(session.Context);
        int before = runtime.Log.Count;
        script.Died();
        Assert.Equal("DEATH_MARKER", Assert.Single(runtime.Log.Skip(before)));
        var scriptReader = new ActiveReader<IScript>(); scriptReader.SetReader(script);
        Assert.True(be.StartBattleEngineDeath());
        Assert.True(be.IsDying); Assert.Equal(GameState.LevelLost, session.Game.State);
        Assert.Null(cameraReader.ToRead); Assert.Null(be.Script);
        Assert.True(script.Deleted); Assert.Null(scriptReader.ToRead);
        Assert.Equal(before + 1, runtime.Log.Count);
        Assert.False((be.Flags & ThingFlags.DeclaredShutdown) != 0);
        Assert.Contains(be, session.Context.World.Things.ToList());
        Assert.Equal(a, session.Context.World.CountersA); Assert.Equal(b, session.Context.World.CountersB);
        FoR smoke = Assert.IsType<FoR>(be.DeathSmoke);
        int explosions = session.Context.World.Things.ToList().OfType<Explosion>().Count();
        Assert.False(be.StartBattleEngineDeath());
        Assert.Same(smoke, be.DeathSmoke);
        Assert.Equal(explosions, session.Context.World.Things.ToList().OfType<Explosion>().Count());
        be.Pos += new FVector(1, 2, 3); be.MoveDyingBattleEngine();
        Assert.Equal(be.Pos, smoke.Pos);
        be.AddDamageFlash(be.Pos + new FVector(0, 1, 0));
        be.Shutdown();
        Assert.Empty(be.DamageFlashes); Assert.Null(be.DeathSmoke);
        Assert.DoesNotContain(be, session.Context.World.Things.ToList());
        Assert.Null(session.Game.GetPlayer(0)!.BattleEngine);
        Assert.Null(be.Cockpit); Assert.Null(be.WalkerPart); Assert.Null(be.JetPart);
        Assert.Null(be.MotionController);
    }

    [Fact]
    public void PartsDeletionInvalidatesQueuedRecipientsAndOwnedWeaponReaders()
    {
        BattleEngine be = Engine();
        be.WalkerPart = new BattleEngineWalkerPart(be); be.JetPart = new BattleEngineJetPart(be);
        Weapon Weapon() => new(be.Context, new WeaponData("test"u8.ToArray()), 0);
        be.WalkerPart.Weapons.Add(Weapon()); be.WalkerPart.PrimaryWeapon = Weapon(); be.WalkerPart.AugWeapon = Weapon();
        be.JetPart.Weapons.Add(Weapon()); be.CurrentWeapon = be.WalkerPart.PrimaryWeapon;
        Weapon[] weapons = [..be.WalkerPart.Weapons, be.WalkerPart.PrimaryWeapon, be.WalkerPart.AugWeapon, ..be.JetPart.Weapons];
        var readers = weapons.Select(weapon => { var reader = new ActiveReader<Weapon>(); reader.SetReader(weapon); return reader; }).ToArray();
        be.Cockpit = new Cockpit(be.Context, be, null);
        var cockpitReader = new ActiveReader<Cockpit>(); cockpitReader.SetReader(be.Cockpit);
        be.MotionController = new MCBattleEngine(be.Context, be);
        var motionReader = new ActiveReader<MCBattleEngine>(); motionReader.SetReader(be.MotionController);
        be.Shutdown();
        Assert.All(readers, reader => Assert.Null(reader.ToRead)); Assert.Null(cockpitReader.ToRead); Assert.Null(motionReader.ToRead);
        Assert.Empty(be.Context.MotionControllers); Assert.Null(be.CurrentWeapon);
        Assert.Null(be.Cockpit); Assert.Null(be.WalkerPart); Assert.Null(be.JetPart);
    }
}
