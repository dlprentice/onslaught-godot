// SPDX-License-Identifier: GPL-3.0-only
// Original-code query oracle: pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
// Query 004fd5e0, segment getters 004442d0/00444300 and Unknown107 004fe540 execute unchanged.
// The authored vtable records Unknown107 before invoking it with a nonzero countdown, avoiding scheduler imports.
// These isolated numeric/state cases do not establish full rendering or gameplay parity.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Render;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class HitFlashRenderTests
{
    private sealed class TestUnit(ThingContext c) : Unit(c);
    private sealed class RecordingUnit(ThingContext c) : Unit(c), IRenderSubject
    {
        public int AnimationIndex = -1;
        int IRenderSubject.RealAnimIndex => AnimationIndex;
        public readonly List<int> Queried = [];
        public override int GetShouldDoHitEffect(int partNumber) { Queried.Add(partNumber); return 37; }
    }

    private static ThingContext Context()
    {
        var pool = new SPtrSetPool(); pool.Init(128);
        var events = new EventManager(); events.Init();
        return new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
    }

    private static void Segment(TestUnit unit, float time, float amount)
    {
        var segments = new DestructableSegments(unit, 0, false, 0);
        segments.Segments = [new DestroyableSegment(segments, 0, null, 1, SegmentType.Extra)
            { LastDamageTime = time, LastDamageAmount = amount }];
        unit.Segments = segments;
    }

    [RetailOracleFact("hit-flash")]
    public void BrightnessAndCountdownMatchOriginalInstructionsIncludingFistAndAgeBoundaries()
    {
        var lines = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("hit-flash"))
        {
            Assert.Equal("HitFlash", function); Assert.Equal(6, w.Length);
            ThingContext c = Context();
            int frame = (int)MathF.Round(RetailOracle.F(w[1]) / EventManager.ClockTick);
            for (int i = 0; i < frame; i++) c.Events.AdvanceTime();
            Assert.Equal(w[1], BitConverter.SingleToUInt32Bits(c.Events.Time));
            var unit = new TestUnit(c) { Unknown218 = unchecked((int)w[4]), Unknown21C = unchecked((int)w[5]) };
            if (w[0] != 0)
            {
                Segment(unit, RetailOracle.F(w[2]), RetailOracle.F(w[3]));
                if (w[0] == 2) unit.Segments!.Segments[0] = null;
            }

            int result = unit.GetShouldDoHitEffect(w[0] == 3 ? -1 : 0);
            string line = $"HitFlash {unchecked((uint)result):x8} {unchecked((uint)unit.Unknown218):x8} {unchecked((uint)unit.Unknown21C):x8}";
            // The oracle also records callback count/argument; the core comparison here is the query's three outputs.
            Assert.Equal(string.Join(' ', expected.Split(' ').Take(4)), line);
            lines.Add(line);
        }

        Assert.Equal(569, lines.Count);
        RetailOracle.WriteOurs("hit-flash", lines.ToArray());
    }

    [Theory]
    [InlineData(0.025f, 51)]
    [InlineData(0.075f, 31)]
    [InlineData(0.975f, 0)]
    public void RecentHitUsesNearestEvenAndRefreshesEvenWhenBrightnessIsZero(float age, int expected)
    {
        var unit = new TestUnit(Context()) { Unknown218 = 1, Unknown21C = 7 };
        Segment(unit, -age, .5f);
        Assert.Equal(expected, unit.GetShouldDoHitEffect(0));
        Assert.Equal(51, unit.Unknown218);
        Assert.Equal(10, unit.Unknown21C);
    }

    [Fact]
    public void RecentRenderSchedulesOnlyOneCountdownAndAgeOneDoesNotRefresh()
    {
        ThingContext c = Context(); var unit = new TestUnit(c);
        Segment(unit, 0, .5f);
        Assert.Equal(50, unit.GetShouldDoHitEffect(0));
        Assert.Equal(100, unit.GetShouldDoHitEffect(0));
        Assert.Equal(1, c.Events.TotalEvents);
        c.Events.Update();
        Assert.Equal(90, unit.Unknown218);
        unit.Segments!.Segments[0]!.LastDamageTime = c.Events.Time - 1;
        Assert.Equal(0, unit.GetShouldDoHitEffect(0));
        Assert.Equal(90, unit.Unknown218);
        for (int i = 0; i < 10; i++) c.Events.Update();
        Assert.Equal(0, unit.Unknown218); Assert.Equal(0, c.Events.TotalEvents);
    }

    [GameDataFact]
    public void ActualPartTypesFlagsAndCollisionReferencesControlHitQueries()
    {
        // Read real descriptors; no fake part-number/type fields bypass their parser.
        List<MeshData> meshes = MeshTests.Level100Meshes(_ => true).Select(x => x.Mesh!).ToList();
        MeshPart[] parts = meshes.SelectMany(m => m.Parts).ToArray();
        var c = Context(); var unit = new RecordingUnit(c); var render = new WorldRender(c);
        foreach (int type in new[] { 1, 2, 4, 5, 6 })
        {
            MeshPart part = parts.First(p => p.PartType == type);
            var rt = new RenderThing(c, part.Mesh, unit);
            int queried = unit.Queried.Count;
            render.RunPart(rt, part, unit, new(0, 0, 0), FMatrix.Identity, 0);
            if (type is 2 or 4 or 5) Assert.Equal(queried, unit.Queried.Count);
            else Assert.Equal((type == 6 ? part.ReferenceTo! : part).PartNumber, unit.Queried.Last());
            queried = unit.Queried.Count;
            render.RunPart(rt, part, unit, new(0, 0, 0), FMatrix.Identity, 1);
            Assert.Equal(queried, unit.Queried.Count); // hidden flag wins, including reference parts
            render.RunPart(rt, part, unit, new(0, 0, 0), FMatrix.Identity, 4);
            Assert.Equal(queried + 1, unit.Queried.Count); // debug cuboids still make the native query
        }

        MeshPart animated = parts.First(p => p.PartType == 1 && (p.NumHFrames > 1 || p.NumAFrames > 1));
        var art = new RenderThing(c, animated.Mesh, unit);
        unit.AnimationIndex = 0;
        int before = unit.Queried.Count;
        render.RunPart(art, animated, unit, new(0, 0, 0), FMatrix.Identity, 0x10);
        Assert.Equal(before, unit.Queried.Count);
        unit.AnimationIndex = -1;
        render.RunPart(art, animated, unit, new(0, 0, 0), FMatrix.Identity, 0x10);
        Assert.Equal(before + 1, unit.Queried.Count);
        Assert.All(render.PartLog, p => Assert.Equal(37, p.HitBrightness));
    }
    [GameDataFact]
    public void PoseParentAndHiddenFrameControlWhichPartIsQueriedInArrayOrder()
    {
        MeshData mesh = MeshTests.Level100Meshes(_ => true).Select(x => x.Mesh!)
            .First(m => m.Parts.Count(p => p.PartType == 1) >= 2);
        var c = Context(); var unit = new RecordingUnit(c); var rt = new RenderThing(c, mesh, unit);
        var render = new WorldRender(c); MeshPose pose = rt.Pose!;
        pose.Update(mesh, unit, c.RenderFrameNumber, true);
        int[] admitted = mesh.Parts.Where(p => p.PartType == 1).Take(2).Select(p => p.PartNumber).ToArray();
        Array.Fill(pose.Parents, -1);
        pose.Parents[0] = admitted[1]; pose.Parents[1] = admitted[0];
        pose.Frames[0] = 0; pose.Frames[1] = 1;
        render.RunParts(rt, mesh, unit);
        Assert.Equal(new[] { admitted[1] }, unit.Queried);
        pose.Frames[1] = 0;
        unit.Queried.Clear();
        render.RunParts(rt, mesh, unit);
        Assert.Equal(new[] { admitted[1], admitted[0] }, unit.Queried);
    }

    [GameDataFact]
    public void ReleaseUnlinksCurrentAndPriorHeadsAndStopsLinksWithoutKillingParticles()
    {
        MeshData mesh = MeshTests.Level100Meshes(_ => true).Select(x => x.Mesh!).First(m => m.NumParts > 0);
        var c = Context(); var unit = new RecordingUnit(c); var render = new WorldRender(c);
        var first = new RenderThing(c, mesh, unit);
        var middle = new RenderThing(c, mesh, unit);
        var head = new RenderThing(c, mesh, unit);
        render.RenderMesh(false, first); render.RenderMesh(false, middle); render.RenderMesh(false, head);
        Assert.Same(middle, head.NextRendered); Assert.Same(first, middle.NextRendered);
        render.ReleaseRenderThing(middle);
        Assert.Same(first, head.NextRendered); Assert.Same(head, first.PrevRendered);
        Assert.Null(middle.NextRendered); Assert.Null(middle.PrevRendered);
        render.ReleaseRenderThing(head);
        Assert.Null(first.PrevRendered);
        render.UpdateRenderLists(); // first is now prior-frame head; current head must no longer retain head/middle.

        var particles = new LevelParticles(c, new ParticleSet());
        var effect = new FoR(); var link = new ParticleEffectLink(particles.Manager) { Effect = effect };
        first.Effects = new MeshEffects { Parts = [null], Ids = [0], Types = [null], Links = [link], Active = [true] };
        render.ReleaseRenderThing(first); render.ReleaseRenderThing(first);
        Assert.Equal((ushort)2, effect.State); // Stop(false), not the immediate kill state1.
        Assert.Null(link.Effect); Assert.Null(first.Effects.Links[0]);
        Assert.Null(first.NextRendered); Assert.Null(first.PrevRendered);
        // If either list head retained a released node this second pass would revisit it. Re-admitting another
        // owner must also start with no dangling previous link.
        render.UpdateRenderLists(); render.RenderMesh(false, middle);
        Assert.Null(middle.PrevRendered); Assert.Null(middle.NextRendered);
        render.UpdateRenderLists(); render.ReleaseRenderThing(middle);
    }

    [GameDataFact]
    public void OrdinaryUnitShutdownReleasesItsRenderOwnerAndLeavesNoRetainedListEntry()
    {
        MeshData mesh = MeshTests.Level100Meshes(_ => true).Select(x => x.Mesh!).First(m => m.NumParts > 0);
        var c = Context();
        c.Particles = new LevelParticles(c, new ParticleSet());
        var unit = new RecordingUnit(c);
        var rt = new RenderThing(c, mesh, unit);
        unit.RenderThing = rt;
        var effect = new FoR();
        var link = new ParticleEffectLink(c.Particles.Manager) { Effect = effect };
        rt.Effects = new MeshEffects { Parts = [null], Ids = [0], Types = [null], Links = [link], Active = [true] };
        c.Particles.World.RenderMesh(false, rt);
        c.Particles.World.UpdateRenderLists();

        unit.Shutdown();

        Assert.Null(unit.RenderThing);
        Assert.Null(rt.NextRendered); Assert.Null(rt.PrevRendered);
        Assert.Null(link.Effect); Assert.Null(rt.Effects.Links[0]);
        Assert.Equal((ushort)2, effect.State);
        // Advancing the two render lists must not retain or touch the deleted current owner.
        c.Particles.World.UpdateRenderLists();
        unit.Shutdown();
        Assert.Null(unit.RenderThing);
    }

}
