// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/explosion.cpp and explosion.h (CExplosion: Init 0044b930, Hit 0044bf10, Unknown66
// 0044c0f0, HandleEvent 0044c140, GetRadius 0044c160, Unknown67 0044c170, SetThingType 0044c180) and
// src/WorldPhysicsManager.cpp (UPhysicsManager::SpawnExplosion 0050ff10). Not in Stuart's release.

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CExplosion: a blast of a physics-script kind. Above the water's depth it plays its effect for what it went off
/// against (air, ground, water or a thing) and its sound; a small one (radius up to 3) has its full radius at once, a
/// larger damaging one grows by a step each frame. It damages what its sphere touches, more nearer its centre, crediting
/// its originator with the kills; a smart one spares its own side.
/// </summary>
/// <remarks>Not ported yet: volumetric effects scattered over the thing it went off on.</remarks>
public sealed class Explosion(ThingContext context) : ComplexThing(context, Oid.Explosion)
{
    public const int GrowEvent = 3000;

    /// <summary>mCurrentRadius: grows to mRadius.</summary>
    public float CurrentRadius;

    /// <summary>mRadius.</summary>
    public float Radius;

    /// <summary>mBehaviour: the physics script's definition.</summary>
    public ExplosionData? Behaviour;

    /// <summary>mColType: what it went off against (ECollisionType).</summary>
    public int ColType;

    public int Allegiance;

    /// <summary>mOriginator: credited with kills.</summary>
    public ActiveReader<Thing> Originator { get; } = new();

    /// <summary>UPhysicsManager::SpawnExplosion (0050ff10): one for a definition that exists (the heap always has room).</summary>
    public static Explosion? Spawn(ThingContext context, int index)
    {
        context.Skip("UPhysicsManager::SpawnExplosion's thing-heap free-space test (200 KB)");
        return index <= -1 ? null : new Explosion(context);
    }

    public override float GetRadius() => Radius;

    public override bool CanGoUnderWater() => true;

    public override void SetThingType(uint t)
    {
        t |= ThingType.Explosion;
        if (Behaviour is { Damage: > 3.0f })
        {
            t |= ThingType.CanDestroyTrees;
        }

        base.SetThingType(t);
    }

    /// <summary>
    /// Init (0044b930): within 1.5 of the water's surface or above, the explosion sets up its passive collision (none for
    /// no radius or no damage), its radius and side, the effect for its collision type, its sound and its growth (or its
    /// end next frame); deeper, it only joins the world and ends.
    /// </summary>
    public override void Init(InitThing init)
    {
        Originator.SetReader(init.Originator);
        if (Context.WaterLevel + 1.5f > init.Pos.Z)
        {
            InitCollision cst = init.InitCst;
            cst.NotSeekCollisionWith |= ThingType.Explosion;
            cst.IgnoreThing = init.AttachedTo;
            Behaviour = init.Behaviour;
            ExplosionData b = Behaviour!;
            Radius = init.AttachedTo is not null && init.UseAttachedRadius ? init.AttachedTo.GetRadius() : b.Radius;
            Allegiance = init.Allegiance;
            if (Radius <= 0.0f || b.Damage <= 0.0015)
            {
                Flags &= ~ThingFlags.InMapWho;
                cst.NotSeekCollisionWith = ThingType.Everything;
            }

            cst.MaxCollision = CollisionLevel.ApproxGeometryShapes;
            cst.CollisionResponse = 0; // ECR_PASSIVE
            cst.StartCollideOnNextFrame = false;
            cst.DoObbForMeshCollision = true;
            ColType = init.ColType;
            CurrentRadius = Radius <= 3.0f ? Radius : 0;
            base.Init(init);
            byte[]? effect = ColType switch
            {
                0 => b.AirEffectName,
                1 => b.GroundEffectName,
                2 => b.WaterEffectName,
                3 => b.UnitEffectName,
                _ => null,
            };
            if (b.Volumetric > 1 && init.AttachedTo is not null && init.AllowVolumetric)
            {
                throw new NotSupportedException("CExplosion::Init's volumetric effects (GetRandomSurfacePoint) are not ported");
            }

            if (ColType == 2)
            {
                Pos.Z = Context.WaterLevel - 0.15f;
            }

            if (Context.Particles is { } particles)
            {
                var link = new ParticleEffectLink(particles.Manager);
                particles.Manager.CreateEffect(effect is null ? null : Context.FindParticle(effect), link, new FVector(0, 0, 0), false, init.Important);
                link.SetPos(Pos);
                link.SetOrientation(Orientation);
                link.Release();
            }

            // Native unsigned ColType<=3 dispatch0044bdd6 rejects unsupported collision types.
            byte[]? sound = ColType switch { 0 or 1 or 3 => b.SoundName, 2 => b.WaterSoundName, _ => null };
            if (sound is not null && Context.SoundEffects is { } sounds)
            {
                Sound.SoundEffect? selected = sounds.Find(System.Text.Encoding.Latin1.GetString(sound));
                // Pristine74154bfa call0044be28: keep the explosion's initial position after its owner expires.
                if (sounds.EventKernelConfigured)
                    sounds.PlayEffect(selected, this, Context.Crt,
                        new Sound.SoundPlayback(Volume: .7f, Tracking: Sound.SoundTracking.InitialPosition));
                else sounds.PlayEffect(selected, Context.Crt);
            }

            if (b.Light != -1)
            {
                Context.Skip("ENGINE.LogLight (an explosion's light)");
            }

            if (b.Damage > 0 && Radius > 3.0f)
            {
                Grow();
            }
            else
            {
                AddShutdownEvent();
            }
        }
        else
        {
            Context.World.Things.Add(this); // WORLD.AddThing: too deep under water to go off
            AddShutdownEvent();
        }
    }

    /// <summary>
    /// Hit (0044bf10): while it has a radius, and sparing a smart explosion's own side, what it touches within its current
    /// radius takes damage falling off to its edge (a large one's spread over its growth frames); its originator
    /// confirms a kill it caused.
    /// </summary>
    public override void Hit(Thing other, CollisionReport? report)
    {
        if (CurrentRadius <= 0)
        {
            return;
        }

        ExplosionData b = Behaviour!;
        if (b.Smart != 0 && other.IsA(ThingType.Unit) && other is Unit su && su.Allegiance == Allegiance)
        {
            return;
        }

        base.Hit(other, report);
        int n = 0;
        // 0044bf65..0044bf9b: this inlined norm adds (Z²+Y²)+X² and retains the x87
        // exponent through the differences, products and root; only the magnitude is stored.
        double dx = X87.Sub(Pos.X, other.Pos.X);
        double dy = X87.Sub(Pos.Y, other.Pos.Y);
        double dz = X87.Sub(Pos.Z, other.Pos.Z);
        float magnitude = (float)X87.Sqrt(X87.Add(X87.Add(X87.Mul(dz, dz), X87.Mul(dy, dy)), X87.Mul(dx, dx)));
        float dist = (float)X87.Sub(magnitude, other.GetRadius());
        bool parts = other.IsA(ThingType.Unit) && other is Unit { HasSegments: true } && report is { Unknown00: not 0, NumContacts: > 0 };
        do
        {
            int part;
            if (parts)
            {
                part = report!.ContactParts[n];
                dist = Radius + report.ContactDistances[n];
            }
            else
            {
                part = -1;
            }

            if (dist < 0)
            {
                dist = 0;
            }

            if (dist <= CurrentRadius)
            {
                bool wasDying = other.IsDying;
                if (Radius > 3.0f)
                {
                    other.Damage(b.Damage / b.Time * (Radius - dist) / Radius, this, true, part);
                }
                else
                {
                    other.Damage((Radius - dist) * b.Damage / Radius, this, true, part);
                }

                if (Originator.ToRead is Unit originator && originator.IsA(ThingType.Unit) && other is Unit victim
                    && other.IsA(ThingType.Unit) && other.IsDying && !wasDying)
                {
                    originator.ConfirmedKill(victim);
                }
            }

            n++;
        }
        while (parts && report!.NumContacts > n);
    }

    /// <summary>Unknown66 (0044c0f0): a step of growth, then the next frame's or its end.</summary>
    private void Grow()
    {
        CurrentRadius += Radius / Behaviour!.Time;
        if (CurrentRadius >= Radius)
        {
            AddShutdownEvent();
        }
        else
        {
            Context.Events.AddEvent(GrowEvent, this, EventManager.NextFrame);
        }
    }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == GrowEvent)
        {
            Grow();
        }
        else
        {
            base.HandleEvent(e);
        }
    }
}
