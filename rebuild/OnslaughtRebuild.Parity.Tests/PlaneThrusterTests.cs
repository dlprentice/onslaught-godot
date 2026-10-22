// SPDX-License-Identifier: GPL-3.0-only
// Matched Plane.cpp Init004d19d0/Move004d1cd0 and Unit.cpp Shutdown004f95d0/~CUnit004f84e0.
// Pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class PlaneThrusterTests
{
    private sealed class TestUnit(ThingContext c) : Unit(c)
    {
        public void DeleteDirectly() => base.Deleted();
    }

    private static ThingContext Context(ParticleSet? set = null)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        var c = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123), null!, new PhysicsRegistry());
        c.Particles = new LevelParticles(c, set ?? new ParticleSet());
        return c;
    }

    private static Plane ActualEmitterPlane()
    {
        var set = new ParticleSet(); Assert.True(set.ReadData(GameData.Read(ParticleSet.InGameFile).Bytes));
        var c = Context(set);
        var plane = new Plane(c)
        {
            UnitData = new UnitData("fixture"u8.ToArray()), Pos = new FVector(10, 20, -30), LaunchTime = 1
        };
        plane.UnitData.EffectNames[8] = "Forseti Afterburner Effect"u8.ToArray();
        var mesh = MeshTests.Level100Meshes(_ => true).Select(x => x.Mesh!)
            .Single(m => m.HeaderName.Equals("FA_F24_training.msh", StringComparison.OrdinalIgnoreCase));
        plane.RenderThing = new RenderThing(c, mesh, plane);
        plane.InitLaunchThrusters();
        Assert.NotEmpty(plane.Thrusters);
        return plane;
    }

    [Fact]
    public void UnresolvedAfterburnerCreatesNoLinksOrRenderLookup()
    {
        var c = Context(); var plane = new Plane(c) { UnitData = new UnitData("fixture"u8.ToArray()) };
        plane.UnitData.EffectNames[8] = "not in the set"u8.ToArray();
        plane.InitLaunchThrusters();
        Assert.Empty(plane.Thrusters); Assert.Equal(0, c.Crt.Calls); Assert.Equal(0, c.Random.Draws);
        Assert.Null(plane.RenderThing); // the missing definition is rejected before any emitter lookup
    }

    [GameDataFact]
    public void RealEngineLinksCreateOnceFollowThePlaneAndStopAtTheExactDeadline()
    {
        Plane plane = ActualEmitterPlane(); var c = plane.Context; var manager = c.Particles!.Manager;
        int links = plane.Thrusters.Count;
        plane.UpdateLaunchThrusters();
        Assert.True(plane.ThrustersActive); Assert.Equal(links, manager.Allocations);
        Assert.Equal(0, c.Crt.Calls); // afterburner root is a timeline; its children draw when processed
        FoR[] frames = plane.Thrusters.Select(l => l.Effect!).ToArray();
        FVector[] positions = frames.Select(f => f.Pos).ToArray();
        for (int i = 0; i < links; i++)
        {
            plane.GetEmitter(23, i + 1, out FVector pos, out _);
            Assert.Equal(pos, frames[i].Pos); Assert.Equal(plane.Orientation, frames[i].Orientation);
            Assert.True(frames[i].HasOrientation);
        }
        plane.Pos += new FVector(3, 4, 5);
        plane.UpdateLaunchThrusters();
        Assert.Equal(links, manager.Allocations);
        for (int i = 0; i < links; i++)
        {
            Assert.Same(frames[i], plane.Thrusters[i].Effect);
            Assert.Equal(positions[i] + new FVector(3, 4, 5), frames[i].Pos);
        }
        plane.LaunchTime = c.Events.Time;
        plane.UpdateLaunchThrusters();
        Assert.False(plane.ThrustersActive); Assert.Equal(links, plane.Thrusters.Count);
        Assert.All(plane.Thrusters, l => Assert.Null(l.Effect));
        Assert.All(frames, f => Assert.Equal((ushort)2, f.State)); // Kill is Stop(false), not immediate destruction
        plane.UpdateLaunchThrusters();
        Assert.Equal(links, manager.Allocations); Assert.Equal(0, c.Crt.Calls);
    }

    [GameDataFact]
    public void ActualAfterburnerTimelineStartsItsChildrenThroughTheSharedRandomStream()
    {
        Plane plane = ActualEmitterPlane(); var c = plane.Context; var manager = c.Particles!.Manager;
        plane.UpdateLaunchThrusters();
        var roots = plane.Thrusters.Select(l => l.Effect!.Particle!).ToArray();
        Assert.Equal(0, c.Crt.Calls);
        foreach (Particle p in roots)
        {
            var timeline = Assert.IsType<PdTimeline>(p.PD);
            Assert.Equal(2, timeline.NumEntries);
            Assert.Equal(new[] { "Forseti Afterburner Trail", "Forseti Engine Emitter" },
                timeline.Entries.Take(2).Select(e => e.Descriptor!.Name));
            Assert.All(timeline.Entries.Take(2), e => { Assert.Equal(0, e.Time); Assert.Equal(1, e.TransmitFoR); });
            Assert.True(timeline.Unknown10(manager, p));
        }
        Assert.Equal(roots.Length, c.Crt.Calls);
        Assert.Equal(0, c.Random.Draws); // this must not consume the separate GAME stream
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnitShutdownAndDestructorStopAndUnlinkOwnedEffects(bool directlyDelete)
    {
        var c = Context(); var owner = new TestUnit(c); var manager = c.Particles!.Manager;
        var tail = new ParticleEffectLink(manager);
        owner.Unknown1AC = new ParticleEffectLink(manager) { Effect = new FoR { InUse = true } };
        owner.Thrusters.Add(new ParticleEffectLink(manager) { Effect = new FoR { InUse = true } });
        owner.DustLinks.Add(new ParticleEffectLink(manager) { Effect = new FoR { InUse = true } });
        owner.VentLinks.Add(new ParticleEffectLink(manager) { Effect = new FoR { InUse = true } });
        FoR[] frames = [owner.Unknown1AC.Effect!, owner.Thrusters[0].Effect!, owner.DustLinks[0].Effect!, owner.VentLinks[0].Effect!];
        var head = new ParticleEffectLink(manager);
        if (directlyDelete) owner.DeleteDirectly(); else owner.Shutdown();
        Assert.Null(owner.Unknown1AC); Assert.Empty(owner.Thrusters); Assert.Empty(owner.DustLinks); Assert.Empty(owner.VentLinks);
        Assert.Same(tail, head.Next); Assert.Null(tail.Next);
        Assert.All(frames, f => { Assert.Equal((ushort)2, f.State); Assert.True(f.InUse); });
        Assert.Equal(0, c.Crt.Calls); Assert.Equal(0, c.Random.Draws);
    }
}
