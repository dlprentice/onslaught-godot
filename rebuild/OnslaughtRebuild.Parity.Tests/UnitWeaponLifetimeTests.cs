// SPDX-License-Identifier: GPL-3.0-only
// CUnit::Shutdown004f95d0 deletes each owned weapon; CWeapon destruction releases its effect links and target.
// Pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class UnitWeaponLifetimeTests
{
    private sealed class TestUnit(ThingContext context) : Unit(context);

    private sealed class ParticleWorld : IParticleWorld
    {
        public float EventTime => 0;
        public (FVector Pos, FMatrix Orientation)? Camera => null;
        public float FrameRenderFraction => 0;
        public float MapCollide(FVector pos) => 0;
        public float MeshRadius(string name) => 0;
    }

    private static (ThingContext Context, TestUnit Owner, Weapon Weapon) Fixture()
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123), null!, new PhysicsRegistry());
        var owner = new TestUnit(context);
        var weapon = new Weapon(context, new WeaponData("owned"u8.ToArray()), 0) { AttachedTo = owner };
        owner.AppendWeapon(weapon); owner.CurrentWeapon = weapon;
        return (context, owner, weapon);
    }

    [Fact]
    public void ShutdownReleasesWeaponTargetAndInvalidatesAlreadyQueuedWeaponEvent()
    {
        var (context, owner, weapon) = Fixture();
        var target = new TestUnit(context);
        weapon.Target.SetReader(target);
        var independentReader = new ActiveReader<Unit>(); independentReader.SetReader(target);
        context.Events.AddEvent(Weapon.BurstEvent, weapon, EventManager.NextFrame);
        ScheduledEvent queued = Assert.Single(context.Events.Buffer(0, EventPriority.StartOfFrame));
        Assert.Equal(2, target.DeletionCount);

        owner.Shutdown();

        Assert.Empty(owner.WeaponList); Assert.Null(owner.CurrentWeapon);
        Assert.Null(weapon.Target.ToRead); Assert.Equal(1, target.DeletionCount);
        Assert.Same(target, independentReader.ToRead);
        Assert.Null(queued.ToCall); Assert.Equal(0, weapon.DeletionCount);
        context.Events.Update();
        Assert.Equal(1u, context.Events.TotalEventsProcessed); // the abandoned event is counted, never called
        Assert.Equal(0, context.Events.TotalEvents);
        Assert.Equal(0, context.Random.Draws); Assert.Equal(0, context.Crt.Calls);
    }

    [Fact]
    public void ShutdownUnlinksBothWeaponEffectsWithoutStoppingIndependentParticles()
    {
        var (context, owner, weapon) = Fixture();
        var manager = new ParticleManager(new ParticleSet(), context.Crt, new ParticleWorld());
        var tail = new ParticleEffectLink(manager);
        var firstFrame = new FoR { InUse = true }; var secondFrame = new FoR { InUse = true };
        weapon.Effects[0] = new ParticleEffectLink(manager) { Effect = firstFrame };
        weapon.Effects[1] = new ParticleEffectLink(manager) { Effect = secondFrame };
        var head = new ParticleEffectLink(manager);
        Assert.Same(weapon.Effects[1], head.Next);
        Assert.Same(weapon.Effects[0], head.Next!.Next);
        Assert.Same(tail, head.Next.Next!.Next);

        owner.Shutdown();

        Assert.Same(tail, head.Next); // public next links observe manager unlink, not merely a nulled owner field
        Assert.Null(tail.Next);
        Assert.Equal((ushort)0, firstFrame.State); Assert.Equal((ushort)0, secondFrame.State);
        Assert.True(firstFrame.InUse); Assert.True(secondFrame.InUse); // link destruction is not Stop(false)
        Assert.Equal(0, context.Random.Draws); Assert.Equal(0, context.Crt.Calls);
    }
}
