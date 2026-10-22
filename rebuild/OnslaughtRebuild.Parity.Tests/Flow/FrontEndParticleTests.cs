// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Particles;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class FrontEndParticleTests
{
    // Authored descriptors isolate the page lifecycle; the admitted-data tests below run the real closure.
    internal static void ConfigureMinimal(FrontEnd frontEnd)
    {
        frontEnd.Crt ??= new CrtRand();
        frontEnd.LoadParticles = fe =>
        {
            var set = new ParticleSet();
            set.Create("Goodie Effect", PdType.Timeline, false);
            set.Create("Goodie Effect 2", PdType.Timeline, false);
            var firework = (PdEmitter)set.Create("Firework Explosion Blue Effect", PdType.Emitter, false)!;
            firework.Life = 0;
            return new FrontEndParticles(fe, set, new Dictionary<string, MeshData>());
        };
    }

    [RetailOracleFact("noise")]
    public void NoiseTableAndOneTimeRandConsumptionAgreeWithUnmodifiedOriginalInstructions()
    {
        var outputs = new List<string>();
        foreach ((string function, uint[] words, string expected) in RetailOracle.Cases("noise"))
        {
            Assert.Equal("Noise", function);
            Assert.Single(words);
            var rand = new CrtRandStream();
            rand.Srand(words[0]);
            var state = new ParticleProcessState();
            state.InitNoiseTable(rand);
            Assert.Equal(1023, rand.Calls);
            Assert.Equal(1024, state.Noise!.Length);
            string result = $"Noise {rand.Calls:x8} {rand.HoldRand:x8} " + string.Join(" ", state.Noise.Select(RetailOracle.H));
            outputs.Add(result);
            Assert.Equal(expected, result);
            float[] table = state.Noise;
            state.InitNoiseTable(rand);
            Assert.Same(table, state.Noise);
            Assert.Equal(1023, rand.Calls);
        }
        Assert.True(outputs.Count >= 8);
        RetailOracle.WriteOurs("noise", outputs.ToArray());
    }

    [Fact]
    public void ProcessTicksParticlesBeforePagesAndDoesNotUseFilteredFrameStep()
    {
        FrontEnd fe = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        ConfigureMinimal(fe);
        var page = new ObservePage(fe);
        fe[FrontEndPage.Main] = page;
        fe.Begin(FrontEndPage.Main);
        for (int i = 0; i < 3; i++) fe.Step();
        Assert.Equal([1, 2, 3], page.Frames);
        Assert.Equal(3f, fe.Particles!.Manager.Time);
        Assert.Equal(1f, fe.Particles.Manager.FrameLength);
        Assert.True(fe.Particles.Manager.FirstFrame);
        fe.ReceiveButtonAction(new FrontEndButton(0, FrontEnd.ButtonMenuSelect, 1));
        fe.Reconnect[0].Reset(ReconnectState.Disconnected);
        fe.Process();
        Assert.Equal(4, fe.Particles.Manager.Frame);
        Assert.Equal([1, 2, 3], page.Frames); // reconnect hold happens after the particle step
    }

    private sealed class ObservePage(FrontEnd fe) : FrontEndPageBase
    {
        public List<int> Frames { get; } = [];
        public override void Process(FrontEndPageState state)
        {
            if (state == FrontEndPageState.Active) Frames.Add(fe.Particles!.Manager.Frame);
        }
    }

    [Fact]
    public void RenderSideNoiseRunsOnlyAfterSuccessfulBeginSceneAndOnlyOncePerProcess()
    {
        FrontEnd fe = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        ConfigureMinimal(fe);
        fe.Begin(FrontEndPage.Main);
        var manager = fe.Particles!.Manager;
        var sprite = (PdSimpleSprite)manager.Set.Create("test sprite", PdType.SimpleSprite, false)!;
        sprite.Life = 100;
        manager.CreateEffect(sprite, null, default, false, false);
        long before = fe.Crt!.MainThread.Calls;
        int attempts = 0;
        fe.BeginScene = () => ++attempts > 1;
        fe.Clock.Presented = () =>
        {
            if (attempts == 1)
            {
                Assert.Null(fe.ParticleState.Noise);
                Assert.Equal(before, fe.Crt.MainThread.Calls);
            }
        };
        fe.Step();
        Assert.Equal(2, attempts);
        Assert.NotNull(fe.ParticleState.Noise);
        Assert.Equal(before + 1023, fe.Crt.MainThread.Calls);
        float[] noise = fe.ParticleState.Noise!;
        int frames = manager.Frame, allocations = manager.Allocations, sprites = fe.ParticleState.SpriteCount;
        fe.Shutdown();
        Assert.Null(fe.Particles);
        Assert.Null(manager.Particles);
        Assert.Null(manager.Set.FirstNode);
        Assert.Equal(0, manager.NumLiveParticles);
        fe.Begin(FrontEndPage.Main);
        Assert.Same(noise, fe.ParticleState.Noise);
        Assert.Equal((frames, allocations, sprites), (fe.Particles!.Manager.Frame, fe.Particles.Manager.Allocations, fe.ParticleState.SpriteCount));
        Assert.Equal(0f, fe.Particles.Manager.Time);
        fe.ParticleState.InitNoiseTable(fe.Crt.MainThread);
        Assert.Equal(before + 1023, fe.Crt.MainThread.Calls);

        FrontEnd other = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        Assert.Null(other.ParticleState.Noise);
        Assert.Equal(0, other.ParticleState.SpriteCount);
    }

    [Fact]
    public void LinkedEffectsInterpolateEveryCoordinateUsingTheLastGameFraction()
    {
        FrontEnd fe = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        ConfigureMinimal(fe);
        fe.Begin(FrontEndPage.Main);
        var link = new ParticleEffectLink(fe.Particles!.Manager);
        fe.Particles.Manager.CreateEffect(fe.Particles.Manager.Set.GetPD("Goodie Effect"), link, default, false, false);
        link.SetPos(new FVector(-3, 0, 1));
        link.SetPos(new FVector(1, 8, 5));
        fe.ParticleState.RenderFraction = .25f;
        fe.Particles.Render();
        Assert.Equal(new FVector(-2, 2, 2), link.Effect!.StartPos);
        fe.ParticleState.RenderFraction = 0;
        fe.Particles.Render();
        Assert.Equal(new FVector(-3, 0, 1), link.Effect.StartPos);
        fe.Shutdown();
        Assert.Null(link.Effect);
        link.Release();
    }

    [GameDataFact]
    public void ShippedGoodiesHaveTheirRealMeshesFullPositionsLinksAndShutdown()
    {
        FrontEnd fe = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        fe.Crt = new CrtRand();
        fe.LoadParticles = f => FrontEndParticles.Load(f, GameData.Read);
        fe.Career.Blank();
        fe.FirstRun = false;
        fe.AutoSave = AutoSaveMode.Pretend;
        fe.EndLevelData.WorldFinished = 100;
        fe.EndLevelData.FinalState = OnslaughtRebuild.Parity.Career.EndLevelState.Won;
        fe.EndLevelData.Ranking = 0.9f;
        for (int i = 0; i < 300; i++) fe.Clock.Present();
        var page = new DebriefingPage(fe) { UnshownPositionX = -1000f };
        fe[FrontEndPage.Debriefing] = page;
        Assert.True(fe.Init(FrontEndEntry.Start));
        var particles = fe.Particles!;
        Assert.Equal(65, particles.Manager.Set.Count);
        Assert.True(particles.MeshRadius("PS2_Normal_Logo3.MSH") > 0);
        fe.Step();
        Assert.Equal(new FVector(-3, 0, 1), page.Position(0));
        Assert.Equal(page.Position(0), page.Link(0)!.Effect!.Pos);
        Assert.Equal(new FVector(0, -2, 0), particles.Camera!.Value.Pos);
        Assert.Equal(default, particles.Manager.Particles!.Pos); // local ZERO, translated by the linked FoR
        for (int i = 0; i < 35; i++) fe.Step();
        Assert.NotNull(fe.ParticleState.Noise);
        Assert.Contains(Enumerate(particles.Manager), p => p.PD is PdMesh);
        Assert.True(fe.Crt.MainThread.Calls > 1023);
        var effect = page.Link(0)!.Effect!;
        for (int i = 0; page.Fade <= .5f && i < 300; i++)
        {
            fe.MessageBox.Kill();
            fe.Step();
        }
        Assert.True(page.Fade > .5f);
        page.ButtonPressed(FrontEndButtons.MenuSelect, 1);
        Assert.Null(page.Link(0)!.Effect);
        Assert.Equal((ushort)2, effect.State);
        fe.Shutdown();
        Assert.Null(particles.Manager.Particles);
        Assert.Null(particles.Manager.Set.FirstNode);
        Assert.Null(particles.Manager.Set.LastNode);
    }

    [GameDataFact]
    public void ShippedFireworkAndFastGoodieClosuresRunAndExpireWithoutASecondSimulator()
    {
        FrontEnd fe = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        fe.Crt = new CrtRand();
        fe.LoadParticles = f => FrontEndParticles.Load(f, GameData.Read);
        fe.Begin(FrontEndPage.Main);
        var manager = fe.Particles!.Manager;
        manager.CreateEffect(manager.Set.GetPD("Firework Explosion Blue Effect"), null, new FVector(0, 0, -5), false, false);
        var link = new ParticleEffectLink(manager);
        manager.CreateEffect(manager.Set.GetPD("Goodie Effect 2"), link, default, false, false);
        link.SetPos(new FVector(-3, 0, 1));
        for (int i = 0; i < 5; i++) fe.Step();
        Assert.True(manager.NumLiveParticles > 20);
        Assert.NotNull(fe.ParticleState.Noise);
        link.Stop(false);
        link.Release();
        for (int i = 0; i < 500; i++) fe.Step();
        Assert.Equal(0, manager.NumLiveParticles);
        Assert.Null(manager.Set.FirstNode);
    }

    private static IEnumerable<Particle> Enumerate(ParticleManager manager)
    {
        for (Particle? p = manager.Particles; p is not null; p = p.Next) yield return p;
    }
}
