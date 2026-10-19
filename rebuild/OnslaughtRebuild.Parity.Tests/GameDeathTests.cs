// SPDX-License-Identifier: GPL-3.0-or-later
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Things;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class GameDeathTests
{
    private static (ParityGame Game, BattleEngine Engine, Services Services) Started()
    {
        var services = new Services();
        var game = new ParityGame(new PlatformClock(), new EventManager(), services) { CurrentLevel = 100 };
        services.Game = game;
        game.InitRestartLoop();
        var pool = new SPtrSetPool(); pool.Init(64);
        var context = new ThingContext(pool, game.Events, new SeedUniformRandomNumberStream(12345), null!,
            new PhysicsRegistry()) { Game = game };
        game.CreatePlayers(context, multiplayerWorld: false);
        var engine = new BattleEngine(context) { Pos = new FVector(10, 20, 30), LastTimeInWater = -100 };
        game.GetPlayer(0)!.AssignBattleEngine(engine);
        game.StartPlayingState();
        // Drop the unrelated pre-run and fear-grid fixture events; death scheduling is observed from time zero.
        game.Events.Init();
        return (game, engine, services);
    }

    [Theory]
    [InlineData(false, ParityGame.GameOverDeath)]
    [InlineData(true, ParityGame.GameOverWater)]
    public void DeathReplacesCameraAndStopsVoiceBeforeDeclaringLoss(bool water, int expectedReason)
    {
        var (game, engine, services) = Started();
        if (water) engine.LastTimeInWater = game.Events.Time;
        var oldCamera = new ThingCamera(engine);
        game.SetCurrentCamera(0, oldCamera);
        var reader = new ActiveReader<Camera>(); reader.SetReader(oldCamera);
        game.DeclarePlayerDead(1);
        Assert.Null(reader.ToRead); // SetCurrentCamera releases the old monitor.
        Assert.Equal(GameState.LevelLost, game.State);
        Assert.Equal(expectedReason, game.LevelLostReason);
        Assert.False(game.Paused);
        Assert.Equal(new[] { "voice:ViewPointCamera:Playing" }, services.Calls);
        var camera = Assert.IsType<ViewPointCamera>(game.GetCurrentCamera(0));
        Assert.Equal(engine.Pos, camera.GetPos());
        Assert.False(camera.GetShowHud());
        Assert.Equal(1, camera.GetZoom());
        Assert.Equal(2, game.Events.TotalEvents);
        Assert.Contains(game.Events.Overflow, e => e.EventNum == (int)GameEvent.PauseGame && e.Time == 15);
    }

    [Fact]
    public void DeathFadesOnFollowingTicksAndPausesOnlyAtScheduledBoundary()
    {
        var (game, _, services) = Started();
        game.DeclarePlayerDead(1);
        Assert.Equal(1f, game.GameSoundsMasterVolume);
        game.Update();
        Assert.Equal(.997f, game.GameSoundsMasterVolume);
        Assert.Equal(.997f, Assert.Single(services.Volumes));
        while (game.Events.Time < 15) game.Update();
        // The scheduler's overflow route is strict: its event is dispatched once the clock exceeds 15.
        Assert.False(game.Paused);
        game.Update();
        Assert.True(game.Paused);
        Assert.True(game.GameSoundsMasterVolume > 0); // pause occurs before 334 fade ticks can elapse
        int count = services.Volumes.Count;
        float stopped = game.Events.Time;
        game.Update();
        Assert.Equal(stopped, game.Events.Time);
        Assert.Equal(count, services.Volumes.Count);
    }

    [Fact]
    public void FadeClampsAndDoesNotRescheduleOnceNegative()
    {
        var (game, _, services) = Started();
        var fade = new Event((int)GameEvent.ContinueFadeOutGameSounds);
        for (int i = 0; i < 333; i++) game.HandleEvent(fade);
        Assert.True(game.GameSoundsMasterVolume > 0);
        int pending = game.Events.TotalEvents;
        game.HandleEvent(fade);
        Assert.Equal(0, game.GameSoundsMasterVolume);
        Assert.Equal(pending, game.Events.TotalEvents);
        Assert.Equal(0, services.Volumes[^1]);
        game.InitRestartLoop();
        Assert.Equal(1, game.GameSoundsMasterVolume);
    }

    [Fact]
    public void ScriptServiceForwardsDeathHooksBeforeRuntimeAttachment()
    {
        var (game, _, services) = Started();
        var wrapped = new ScriptGameServices(services);
        wrapped.StopCurrentMessageSound();
        wrapped.SetGameSoundsMasterVolume(.4f);
        Assert.Single(services.Calls);
        Assert.Equal(.4f, Assert.Single(services.Volumes));
    }

    [Fact]
    public void DeathWithoutAMessageBoxDoesNotCallVoiceService()
    {
        var (game, _, services) = Started();
        services.MessageBox = null;
        game.DeclarePlayerDead(1);
        Assert.Empty(services.Calls);
        Assert.Equal(GameState.LevelLost, game.State);
    }

    [Fact]
    public void AttachedScriptServiceStopsTheRealMessageBoxVoice()
    {
        var (_, engine, services) = Started();
        ScriptRuntime runtime = ScriptRuntime.Of(engine.Context);
        var voice = new Voice();
        runtime.MessageBox = new MessageBox(engine.Context, new Text(), voice);
        var wrapped = new ScriptGameServices(services) { Runtime = runtime };
        wrapped.StopCurrentMessageSound();
        Assert.Equal(1, voice.Killed);
        Assert.Empty(services.Calls); // the runtime owner replaces the fallback
    }

    [Theory]
    [InlineData(2, false, 100)]
    [InlineData(1, true, 100)]
    [InlineData(1, false, 850)]
    public void UnsupportedDeathRoutesRefuseBeforeChangingState(int number, bool freeCamera, int level)
    {
        var (game, _, services) = Started();
        game.FreeCameraOn = freeCamera; game.CurrentLevel = level;
        Camera? before = game.GetCurrentCamera(0);
        Assert.Throws<NotSupportedException>(() => game.DeclarePlayerDead(number));
        Assert.Same(before, game.GetCurrentCamera(0));
        Assert.Equal(GameState.Playing, game.State);
        Assert.Empty(services.Calls);
    }

    [Fact]
    public void UnsupportedBattleEngineDeathDoesNotSetDyingBeforeTheGameRefuses()
    {
        var (game, engine, services) = Started();
        engine.Player = game.GetPlayer(0);
        game.FreeCameraOn = true;
        Assert.Throws<NotSupportedException>(() => engine.StartDieProcess());
        Assert.False(engine.IsDying);
        Assert.Equal(GameState.Playing, game.State);
        Assert.Null(engine.DeathSmoke);
        Assert.Empty(services.Calls);
    }

    [Fact]
    public void CameraFirstUseIsExplicitlyDefinedAndSameTickRetainsPreviousPose()
    {
        var events = new EventManager();
        var camera = new ViewPointCamera(events, new FVector(10, 20, 30), .5f, 0, 6, 3);
        FMatrix first = camera.GetOrientation();
        // Retail with a zero/equal or NaN uninitialized timestamp returns Identity here instead.
        // This intentional deterministic boundary matches retail's nonmatching initial-cache branch.
        Assert.NotEqual(FMatrix.Identity, first);
        Assert.Equal(FMatrix.Identity, camera.GetOldOrientation());
        Assert.Equal(first, camera.GetOrientation());
        Assert.Equal(FMatrix.Identity, camera.GetOldOrientation());
        FVector original = camera.GetPos();
        events.AdvanceTime();
        Assert.Equal(original, camera.GetOldPos()); // old access does not calculate
        FVector next = camera.GetPos();
        Assert.NotEqual(original, next);
        Assert.Equal(original, camera.GetOldPos());
        Assert.Equal(next, camera.GetPos());
        Assert.Equal(original, camera.GetOldPos());
        camera.GetOrientation();
        Assert.Equal(first, camera.GetOldOrientation());
    }

    [RetailOracleFact("viewpoint-camera-20261019")]
    public void ViewPointCameraMatchesIsolatedOriginalBodiesWithNonmatchingInitialCache()
    {
        var lines = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("viewpoint-camera-20261019"))
        {
            Assert.Equal("ViewPoint", function); Assert.Equal(11, w.Length);
            float F(int at) => RetailOracle.F(w[at]);
            Assert.Equal(-2, F(9)); Assert.Equal(-2, F(10));
            var events = new EventManager();
            int startTick = (int)Math.Round(F(7) / EventManager.ClockTick);
            int endTick = (int)Math.Round(F(8) / EventManager.ClockTick);
            while (events.FrameCount < startTick) events.AdvanceTime();
            Assert.Equal(F(7), events.Time);
            var camera = new ViewPointCamera(events, RetailOracle.V(w, 0), F(3), F(4), F(5), F(6));
            while (events.FrameCount < endTick) events.AdvanceTime();
            Assert.Equal(F(8), events.Time);
            FVector position = camera.GetPos(); FMatrix orientation = camera.GetOrientation();
            string Matrix(FMatrix m) => $"{RetailOracle.Hv(m.Row0)} {RetailOracle.Hv(m.Row1)} {RetailOracle.Hv(m.Row2)}";
            string line = $"ViewPoint {RetailOracle.Hv(position)} {Matrix(orientation)} "
                + $"{RetailOracle.Hv(camera.GetOldPos())} {Matrix(camera.GetOldOrientation())} "
                + $"{RetailOracle.H(events.Time)} {RetailOracle.H(events.Time)}";
            lines.Add(line);
            Assert.True(line == expected, $"case {lines.Count - 1}:\nours {line}\nretail {expected}");
            Assert.Equal(position, camera.GetPos()); Assert.Equal(orientation, camera.GetOrientation());
        }
        Assert.Equal(1036, lines.Count);
        RetailOracle.WriteOurs("viewpoint-camera-20261019", lines.ToArray());
    }

    private sealed class Services : IGameServices
    {
        public ParityGame Game { private get; set; } = null!;
        public List<string> Calls { get; } = [];
        public List<float> Volumes { get; } = [];
        public void StopCurrentMessageSound() => Calls.Add($"voice:{Game.GetCurrentCamera(0)?.GetType().Name}:{Game.State}");
        public void SetGameSoundsMasterVolume(float volume) => Volumes.Add(volume);
        public Monitor? MessageBox { get; set; } = new();
        public bool PlayerZeroInControl => false;
        public bool IsMultiplayer => false;
        public bool PlayerOneInControl => false;
        public bool IsMenuUp => false;
        public void AutoConfigTestFrame(int frame) { }
        public void ControllableCameraUpdateFrameCount() { }
        public void MessageBoxPlayPendingSamples() { }
        public bool HasController(int player) => false;
        public bool ProcessReconnect(int player) => false;
        public bool ReconnectOk(int player) => true;
        public void SetGuideUpdateCounter(int value) { }
        public void PollMouseState() { }
        public void UpdateMouseAxisLock(int player) { }
        public void FlushController(int player) { }
        public void UpdateMouseCentre() { }
        public void ClearKeyWasUp() { }
        public void AtmosphericsProcessAll() { }
        public void SetAllSamplesPaused(bool paused) { }
        public void ShowLevelEndMenu() { }
        public void ShowPauseMenu(Controller from) { }
        public void HidePauseMenu() { }
        public void PostScriptEvent(string name) { }
        public void HandleGameEvent(GameEvent gameEvent, Event e) => throw new InvalidOperationException($"unexpected {gameEvent}");
        public void ResetMouseTransientState() { }
        public QuitType PlatformProcess() => QuitType.None;
        public bool InactivityMeansQuitGame() => false;
        public void EnterAttractMode() { }
        public void AfterUpdate() { }
        public void Render(int numRenders) { }
        public void Flip() { }
    }

    private sealed class Text : IGameText
    {
        public string? GetString(uint id) => null;
        public string? GetSoundName(uint id) => null;
        public string LanguageName => "english";
    }

    private sealed class Voice : IMessageVoice
    {
        public int Killed { get; private set; }
        public bool SoundInitialised => true;
        public bool LoaderBusy => false;
        public bool HasLoadedData => false;
        public void StartLoad(string path) { }
        public void PlayLoaded(ScriptMessage? message) { }
        public void KillVoice() => Killed++;
        public void UpdateStatus() { }
    }
}
