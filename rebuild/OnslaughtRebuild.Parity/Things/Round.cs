// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Round.cpp and round.h (CRound: GetMaxVelocity 004d82a0, BounceFactor 004d82d0,
// ClipToGround 004d82e0, SetThingType 004d8320, CanGoUnderWater 004d8330, the constructor 004d81e0, Init 004d8410,
// GetRadius 004d8ac0, Hit 004d8ae0, Shutdown 004d8dc0, Move 004d8e40, HandleEvent 004d9910, DeclareOnGround 004d9dd0,
// DeclareInWater 004d9ef0, Unknown4d9f30 004d9f30, SetTarget 004daab0, ReleaseTarget 004dab50, Unknown4dac90 004dac90,
// StartDieProcess 004db130, Gravity 004db600, Unknown4db630 004db630), src/CollisionSeekingRound.cpp
// (CCollisionSeekingRound: Init 00425b50, Unknown8 00425c60, Unknown7 00425e30) and src/WorldPhysicsManager.cpp
// (UPhysicsManager::SpawnRound 0050f7a0). Not in Stuart's release.

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CRound's events.</summary>
public static class RoundEvent
{
    /// <summary>The flight's end: flak and exploding rounds go off, a beam traces its line, others die.</summary>
    public const int Launch = 4000;

    /// <summary>A straight round reaching the ground point found at launch.</summary>
    public const int Land = 4001;

    /// <summary>A jumping round spawning its next.</summary>
    public const int Jump = 4002;

    /// <summary>A seeking round looking for a target again.</summary>
    public const int Retarget = 4003;
}

/// <summary>
/// CRound: a bullet, shell or missile in flight from a round definition. A round with a radius collides as a sphere; one
/// without is a line the length of its travel (CCollisionSeekingRound). It flies straight (its ground point found at
/// launch, landing then), falls under its gravity, hugs the ground or seeks a target. It goes off in the air, on the
/// ground, in water or against a thing: an explosion of its kind and its damage to what it hit.
/// </summary>
/// <remarks>
/// Not ported yet, each throwing where it is reached: the damage a hit does (CThing::Damage and the kill it confirms),
/// explosions (UPhysicsManager::SpawnExplosion), beams (CCSRay), jumping rounds, missiles (CMissile), the battle engine's
/// lock hits. Target lock eligibility, radar membership and the seeking turn have production owners.
/// </remarks>
public partial class Round : Actor
{
    /// <summary>GAME_FR: the game's frames per second.</summary>
    private const float GameFr = 20.0f;

    public Round(ThingContext context, RoundData data)
        : base(context, Oid.Round)
    {
        Data = data;
        Unknown124 = 0;
        StartTime = context.Events.Time;
        Unknown120 = false;
        Unknown12C = false;
        Unknown130 = 1;
    }

    /// <summary>mEffect: the round's particle effect.</summary>
    public ParticleEffectLink? Effect;

    /// <summary>mUnknownE8: the target.</summary>
    public ActiveReader<Thing> Target { get; } = new();

    /// <summary>mUnknownEC: what launched it.</summary>
    public ActiveReader<Thing> Launcher { get; } = new();

    /// <summary>mData.</summary>
    public RoundData Data { get; private set; }

    public float StartTime;

    /// <summary>mBeamStart: a beam's start.</summary>
    public FVector BeamStart;

    /// <summary>mDest: the init's destination, or where the flight meets the ground.</summary>
    public FVector Dest;

    public int JumpsPerformed;

    public int Allegiance;

    /// <summary>mUnknown120: it cannot start dying (a jump is due).</summary>
    public bool Unknown120;

    /// <summary>mUnknown124: inside its own Hit.</summary>
    public int Unknown124;

    /// <summary>mUnknown128: a ground-hugging round has met the ground.</summary>
    public int Unknown128;

    /// <summary>mUnknown12C: a torpedo in the water (no gravity).</summary>
    public bool Unknown12C;

    /// <summary>mUnknown130: it may take a battle engine as its target.</summary>
    public int Unknown130;

    /// <summary>
    /// UPhysicsManager::SpawnRound (0050f7a0): a missile for a round definition marked so, else a round (the thing heap
    /// always has 200 KB free here).
    /// </summary>
    public static Round? Spawn(ThingContext context, RoundData? data)
    {
        if (data is null)
        {
            return null;
        }

        context.Skip("UPhysicsManager::SpawnRound's thing-heap free-space test (200 KB)");
        if (data.Missile != 0)
        {
            throw new NotSupportedException("CMissile (a round marked missile) is not ported");
        }

        return new Round(context, data);
    }

    public override float GetMaxVelocity() => Gravity() != 0 ? 160.0f : Data.Speed;

    public override float BounceFactor() => Data.BounceFactor;

    public override bool ClipToGround() => !(Data.BounceFactor == 0 && Data.Gravity == 0 && Data.TurnRate <= 0);

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Ammunition);

    public override bool CanGoUnderWater() => Data.CanGoUnderWater != 0;

    public override int GetSoundMaterial() => Data.SoundMaterial;

    /// <summary>GetRadius (004d8ac0): half a frame's travel plus the round's radius.</summary>
    public override float GetRadius() => (Data.Speed / GameFr * 0.5f) + Data.Radius;

    /// <summary>Gravity (004db600): none for a torpedo in the water, else the data's at a fortieth.</summary>
    public override float Gravity() => Data.Torpedo != 0 && Unknown12C ? 0 : Data.Gravity * 0.025f;

    /// <summary>
    /// Init (004d8410): the destination, jumps, data and side; the collision set-up (mesh collision, approximate shapes,
    /// a sphere of the radius ignoring the launcher, or for no radius a line the round's length); then the actor's Init,
    /// the end-of-flight event at the lifespan, the effect, and for a straight round the landing event where its line
    /// meets the ground. Finally the first target search.
    /// </summary>
    public override void Init(InitThing init)
    {
        Dest = init.Dest;
        JumpsPerformed = init.JumpsPerformed;
        Data = init.RoundData ?? Data;
        Allegiance = init.Allegiance;
        Unknown128 = 0;
        InitCollision cst = init.InitCst;
        cst.DesiredCollision = CollisionLevel.Mesh;
        if (Data.GillMBreath != 0)
        {
            cst.DoObbForMeshCollision = true;
            cst.NotSeekCollisionWith |= ThingType.NamedMesh;
        }

        if (Data.Missile != 0)
        {
            cst.MinCollision = CollisionLevel.Mesh;
            cst.MaxCollision = CollisionLevel.Mesh;
        }
        else
        {
            cst.MinCollision = CollisionLevel.ApproxGeometryShapes;
            cst.MaxCollision = CollisionLevel.ApproxGeometryShapes;
            cst.NotSeekCollisionWith |= ThingType.Ammunition;
        }

        cst.CollisionResponse = 1; // ECR_STATIC
        if (Data.TreeCollision == 2)
        {
            cst.NotSeekCollisionWith |= ThingType.Tree;
        }

        if (Data.Beam != 0)
        {
            throw new NotSupportedException("CRound::Init of a beam (CCSRay) is not ported");
        }

        if (Data.Torpedo == 0)
        {
            cst.NotSeekCollisionWith |= ThingType.Explosion;
        }

        if (Data.Radius > 0)
        {
            cst.Shape = new SphereVolume(Data.Radius);
            cst.IgnoreThing = Launcher.ToRead;
            if (Data.PassiveCollision != 0)
            {
                cst.CollisionResponse = 0; // ECR_PASSIVE
            }
        }
        else
        {
            cst.RoundLength = Data.Length;
        }

        base.Init(init);
        Context.Events.AddEvent(RoundEvent.Launch, this, Context.Events.Time + init.LifeSpan);
        if (Data.EffectName is { } effectName && Context.Particles is { } particles)
        {
            Effect = new ParticleEffectLink(particles.Manager);
            bool important = Launcher.ToRead is Unit launcher ? launcher.UnitData is not { } ud || ud.Fenrir != 0 : false;
            particles.Manager.AddParticle(Context.FindParticle(effectName), Effect, important);
            Effect.SetPos(Pos);
            Effect.SetOrientation(Orientation);
        }

        // A round that flies straight: find where it meets the ground now.
        if (!(Data.TurnRate > 0) && Data.GroundHugging == 0 && Gravity() == 0)
        {
            FVector start = Pos;
            float speed = GetVelocity().Magnitude() * GameFr;
            FVector dir = GetVelocity();
            // CRound::Init 004d8848 groups Y² and Z² before X² here (port patch 0055).
            dir.NormaliseYZX();
            if (Context.Map.LineHits(start, start + (dir * (speed * init.LifeSpan)), out FVector hit, true))
            {
                float t = (start - hit).Magnitude() / speed;
                Context.Events.AddEvent(RoundEvent.Land, this, Context.Events.Time + t, EventPriority.EndOfFrame);
                Dest = hit;
            }
        }

        if (Data.GridOfFear != 0)
        {
            Context.World.Set60.Add(this);
        }

        Unknown4dac90(null);
    }

    /// <summary>The seeker: CCollisionSeekingRound for a round with no radius, else the persistent seeker.</summary>
    public override void InitCollisionSeekingThing(InitCollision init)
    {
        if (Data.Radius > 0)
        {
            base.InitCollisionSeekingThing(init);
            return;
        }

        if (init.Shape is not null)
        {
            Context.Skip("CCollisionSeekingRound::Init: \"WARNING: Collision seeking round will define its own shape\" (a log line)");
        }

        init.Shape = new LineVolume(new FVector(0, 0, 0), new FVector(0, 0, 0));
        init.ForThing = this;
        HasCollisionSeekingThing = true;
        Seeker = new RoundSeeker(this, init);
    }

    /// <summary>
    /// Hit (004d8ae0): unless dying, inside its own hit, a contact report on a non-explosion of an active round, a
    /// target-only round meeting something else or a smart round meeting its own side: the hit's damage, the battle
    /// engine's rearm, the kill the launcher confirms, the impact sound and the round going off against the thing.
    /// </summary>
    public override void Hit(Thing other, CollisionReport? report)
    {
        base.Hit(other, report);
        if (IsDying)
        {
            return;
        }

        if (report is not null && report.TestTriangles == 0 && Data.PassiveCollision == 0 && !other.IsA(ThingType.Explosion))
        {
            return;
        }

        if (Unknown124 != 0)
        {
            return;
        }

        if (Data.Damage < 0 && !ReferenceEquals(other, Target.ToRead))
        {
            return;
        }

        if (Data.Smart != 0 && other is Unit su && su.Allegiance == Allegiance)
        {
            return;
        }

        if (Launcher.ToRead is BattleEngine shooter && other.IsA(ThingType.Unit) && other is Unit hitUnit && hitUnit.Allegiance != shooter.Allegiance)
        {
            (shooter.Player as Game.Player)?.IncStat(Game.PlayerStat.RoundsHit);
        }

        if (Data.Beam != 0)
        {
            throw new NotSupportedException("CRound::Hit of a beam is not ported");
        }

        Unknown124 = 1;
        int part = -1;
        if (report is { Unknown00: not 0, NumContacts: > 0 })
        {
            part = report.ContactParts[0];
        }

        bool wasDying = other.IsDying;
        other.Damage(Data.Damage, this, true, part);
        if (other is BattleEngine rearmed)
        {
            rearmed.Rearm(Data.Rearm);
        }

        if (Launcher.ToRead is Unit killer && killer.IsA(ThingType.Unit) && other.IsA(ThingType.Unit) && other is Unit killed
            && other.IsDying && !wasDying)
        {
            killer.ConfirmedKill(killed);
        }

        PlayImpactSound(this, other);
        GoOff(CollideMode.Thing, report, other);
        if (Data.Fire == 0)
        {
            StartDieProcess();
        }

        Unknown124 = 0;
    }

    /// <summary>Shutdown (004d8dc0): off the fear grid's list, the effect killed, the target released.</summary>
    public override void Shutdown()
    {
        if (Data.GridOfFear != 0)
        {
            Context.World.Set60.Remove(this);
        }

        Effect?.Kill();
        ReleaseTarget();
        base.Shutdown();
    }

    /// <summary>
    /// Move (004d8e40): a stopped round (a jump due) stays; otherwise off inside its proximity of the target, wiggling
    /// (two GAME randoms), falling and hugging the ground, flying, letting go of a target it can no longer lock, turning
    /// toward its target at no more than its turn rate once it may seek, and pointing along its flight while it falls.
    /// </summary>
    public override void Move()
    {
        if (Unknown120)
        {
            Effect?.Kill();
            ReleaseTarget();
            return;
        }

        if (Data.Beam != 0)
        {
            throw new NotSupportedException("CRound::Move of a beam is not ported");
        }

        if (Target.ToRead is { } near)
        {
            FVector d = near.Pos - Pos;
            if ((d.X * d.X) + (d.Y * d.Y) + (d.Z * d.Z) < Data.Proximity * Data.Proximity)
            {
                GoOff(CollideMode.Nothing, null, null);
                StartDieProcess();
            }
        }

        FMatrix unwiggle = FMatrix.Identity;
        if (Data.Wiggle > 0)
        {
            // Both matrices through the out-of-line FMatrix(yaw, pitch, 0) (004062d0, called at 004d908d and 004d90d1); the wiggle
            // product sums each component (X + Y) + Z.
            float pitch = ((Context.Random.FloatRandom() * 2) - 1) * Data.Wiggle;
            float yaw = ((Context.Random.FloatRandom() * 2) - 1) * Data.Wiggle;
            FMatrix wiggle = FMatrix.MakeRotationOutOfLine(yaw, pitch, 0);
            unwiggle = FMatrix.MakeRotationOutOfLine(-yaw, -pitch, 0);
            Velocity = WiggleVelocity(wiggle, Velocity);
        }

        Velocity.Z += Gravity();
        if (Data.GroundHugging != 0)
        {
            if (Context.Map.Collide(Pos) < Pos.Z)
            {
                Unknown128 = 1;
            }

            if (Unknown128 != 0)
            {
                Velocity.Z = Context.Map.Collide(Pos) - Pos.Z;
            }
        }

        base.Move();
        if (Data.Wiggle > 0)
        {
            Velocity = UnwiggleVelocity(unwiggle, Velocity);
        }

        UpdateSeeking();

        if (Gravity() != 0 || Unknown128 != 0)
        {
            Orientation = FMatrix.FromYawPitchRoll(WeaponMaths.Azimuth(Context, Velocity), WeaponMaths.Elevation(Context, Velocity), 0);
        }

        if (Effect?.Effect is not null)
        {
            Effect.SetPos(Pos);
            Effect.SetOrientation(Orientation);
        }
    }

    /// <summary>HandleEvent (004d9910): retargeting, a jump, the landing and the end of the flight.</summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case RoundEvent.Retarget:
                Unknown4dac90(e as ScheduledEvent);
                break;
            case RoundEvent.Jump:
                throw new NotSupportedException("CRound::Unknown4db150 (a jumping round's next) is not ported");
            case RoundEvent.Land:
                if (Data.BounceFactor == 0 && !Unknown120)
                {
                    if (Data.Beam == 0 && e is ScheduledEvent landing)
                    {
                        float dt = Context.Events.Time - landing.Time;
                        Pos -= Velocity * 20.0f * dt;
                    }

                    GoOff(Context.WaterLevel - 0.001f < Pos.Z ? CollideMode.Water : CollideMode.Ground, null, null);
                    if (Data.Fire == 0)
                    {
                        StartDieProcess();
                    }
                }

                break;
            case RoundEvent.Launch:
                if (Data.Flak != 0 || Data.Explode != 0)
                {
                    GoOff(CollideMode.Nothing, null, null);
                }

                StartDieProcess();
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>DeclareOnGround (004d9dd0): a bouncing round bounces; another lands where its last step met the ground.</summary>
    public override void DeclareOnGround()
    {
        if (Data.BounceFactor != 0)
        {
            base.DeclareOnGround();
        }
        else if (Data.Fire == 0)
        {
            if (Context.Map.LineHits(OldPos, Pos, out FVector hit, false))
            {
                Pos = hit;
            }

            GoOff(CollideMode.Ground, null, null);
            StartDieProcess();
        }
    }

    /// <summary>DeclareInWater (004d9ef0): a torpedo arms; a round that cannot go under water goes off.</summary>
    public override void DeclareInWater()
    {
        if (!Unknown12C && Data.Torpedo != 0)
        {
            throw new NotSupportedException("CRound::Unknown4db630 (a torpedo arming) is not ported");
        }

        base.DeclareInWater();
        if (Data.CanGoUnderWater == 0 && Data.Torpedo == 0)
        {
            GoOff(CollideMode.Water, null, null);
            StartDieProcess();
        }
    }

    /// <summary>StartDieProcess (004db130): not while a jump is due.</summary>
    public override bool StartDieProcess() => !Unknown120 && base.StartDieProcess();

    /// <summary>How the round goes off (ECollisionType).</summary>
    public enum CollideMode
    {
        Nothing = 0,
        Ground = 1,
        Water = 2,
        Thing = 3,
    }

    /// <summary>
    /// Unknown4d9f30 (004d9f30): the round goes off. On the ground the landscape takes damage; on the ground, in water or
    /// against a thing its explosion spawns (and a jump is scheduled); in the air only flak and exploding rounds explode.
    /// Then it stops (unless it burns on) and its effect follows it.
    /// </summary>
    private void GoOff(CollideMode mode, CollisionReport? report, Thing? thing)
    {
        var init = new InitThing { Originator = Launcher.ToRead };
        if (init.Originator is not null && init.Originator.IsA(ThingType.BattleEngine))
        {
            init.Important = true;
        }

        int explosionIndex = Data.ExplosionName is { } explosionName ? Context.Physics.GetExplosionIndex(explosionName) : -1;
        if (Data.ExplosionName is not null)
        {
            init.Behaviour = explosionIndex >= 0 ? Context.Physics.Explosions[explosionIndex] : null;
        }

        if (init.Behaviour is { Oriented: not 0 })
        {
            switch (mode)
            {
                case CollideMode.Thing:
                    if (report is not null && report.NumContacts >= 1)
                    {
                        FVector n = report.Normals[0];
                        init.Yaw = WeaponMaths.Azimuth(Context, n);
                        init.Pitch = WeaponMaths.Elevation(Context, n) + (MathF.PI / 2);
                        init.Roll = 0;
                    }
                    else if (thing is not null)
                    {
                        FVector n = Pos - thing.Pos;
                        init.Yaw = WeaponMaths.Azimuth(Context, n);
                        init.Pitch = WeaponMaths.Elevation(Context, n) + (MathF.PI / 2);
                        init.Roll = 0;
                    }
                    else
                    {
                        init.OrientationType = OrientationType.DirectionCosineMatrix;
                        init.Orientation = Orientation.Multiply(FMatrix.FromYawPitchRoll(0, -MathF.PI / 2, 0));
                    }

                    break;
                case CollideMode.Ground:
                {
                    FVector n = Context.Map.Normal(Pos);
                    init.Yaw = WeaponMaths.Azimuth(Context, n);
                    init.Pitch = WeaponMaths.Elevation(Context, n) + (MathF.PI / 2);
                    init.Roll = 0;
                    break;
                }

                case CollideMode.Nothing:
                    init.OrientationType = OrientationType.DirectionCosineMatrix;
                    init.Orientation = Orientation.MultiplyLeftToRight(FMatrix.FromYawPitchRoll(0, MathF.PI / 2, 0)); // src/Round.cpp MulMM
                    break;
            }
        }

        if (mode == CollideMode.Ground)
        {
            Context.Skip("ENGINE.AddDamage (the landscape's damage where a round lands)");
        }

        if (init.Behaviour is not null && (mode != CollideMode.Nothing || Data.Flak != 0 || Data.Explode != 0)
            && Explosion.Spawn(Context, explosionIndex) is { } explosion)
        {
            FVector vel = GetVelocity();
            vel.Normalise();
            init.Pos = Pos - (vel * 0.1f);
            init.ColType = (int)mode;
            init.Allegiance = Launcher.ToRead is Unit lu ? lu.Allegiance : 2;
            explosion.Init(init);
        }

        if (Data.Jumps > JumpsPerformed && !Unknown120)
        {
            throw new NotSupportedException("CRound::Unknown4d9f30: a jumping round's next jump is not ported");
        }

        if (Data.Fire == 0)
        {
            Velocity = new FVector(0, 0, 0);
        }

        if (Effect?.Effect is not null && Data.Beam == 0)
        {
            Effect.SetPos(Pos);
            Effect.SetOrientation(Orientation);
        }
    }

    /// <summary>SetTarget (004daab0): only for a seeking or target-only round; a battle engine target joins mSet110.</summary>
    public void SetTarget(Thing? target, bool release)
    {
        if (target is null)
        {
            return;
        }

        if (Data.Seek == 0 && !(Data.Damage < 0))
        {
            return;
        }

        if (release)
        {
            ReleaseTarget();
        }

        Target.SetReader(target);
        if (target.IsA(ThingType.BattleEngine))
        {
            Context.World.Set110.Add(this);
        }
    }

    /// <summary>ReleaseTarget (004dab50): a battle engine launcher's lock hit, then no target.</summary>
    public void ReleaseTarget()
    {
        if (Launcher.ToRead is { } launcher && launcher.IsA(ThingType.BattleEngine) && Target.ToRead is not null)
        {
            throw new NotSupportedException("CBattleEngine::LockHit is not ported");
        }

        if (Target.ToRead is { } t && t.IsA(ThingType.BattleEngine))
        {
            Context.World.Set110.Remove(this);
        }

        Target.SetReader(null);
    }

    /// <summary>
    /// Unknown4dac90 (004dac90): a seeking round (mode 1) without a target takes the unit its launcher may target nearest
    /// its heading within the seek cone; then it looks again in a quarter to half a second (GAME random).
    /// </summary>
    private void Unknown4dac90(ScheduledEvent? reuse)
    {
        if (Data.Seek != 1)
        {
            return;
        }

        throw new NotSupportedException("CRound::Unknown4dac90 (a seeking round's target search) is not ported (no level-100 round seeks)");
    }
}

/// <summary>
/// CCollisionSeekingRound: a round with no radius collides as a line from its last position, the round's length long,
/// and reaches as far as its top speed's frame plus that length; it never meets its launcher, and a straight round only
/// meets a stationary thing or infantry whose sphere its flight line passes through.
/// </summary>
public sealed class RoundSeeker : CollisionSeekingThing
{
    private readonly float _roundLength;

    public RoundSeeker(Round round, InitCollision init)
        : base(round, init)
    {
        _roundLength = init.RoundLength;
        Radius = (round.GetMaxVelocity() / 20.0f) + _roundLength;
    }

    /// <summary>Unknown8 (00425c60).</summary>
    public override bool Seeks(CollisionSeekingThing other)
    {
        bool result = base.Seeks(other);
        if (result)
        {
            var round = (Round)ForThing;
            if (ReferenceEquals(other.ForThing, round.Launcher.ToRead))
            {
                return false;
            }

            RoundData d = round.Data;
            if (d.BounceFactor == 0 && d.Gravity == 0 && d.TurnRate <= 0 && d.Torpedo == 0
                && (other.ForThing.IsA(ThingType.Stationary) || other.ForThing.IsA(ThingType.Infantry)))
            {
                FVector centre = other.ForThing.GetCentrePos();
                float radius = other.Radius;
                FVector start = ForThing.Pos - centre;
                FVector end = start + ForThing.GetVelocity();
                FVector delta = start - end;
                float fraction = ((delta.X * start.X) + (delta.Y * start.Y) + (delta.Z * start.Z)) / delta.MagnitudeSq();
                FVector closest = start + ((end - start) * fraction);
                // 00425e01..00425e0e rejects x87 C0: radius squared is less than the distance OR unordered.
                // A stopped round produces 0/0 above; it must not schedule collision checks from that NaN.
                if (!(closest.MagnitudeSq() <= radius * radius))
                {
                    return false;
                }
            }
        }

        return result;
    }

    /// <summary>Unknown7 (00425e30): the line from the last position, along the travel for the round's length.</summary>
    public override GeneralVolume GetVolume()
    {
        var round = (Round)ForThing;
        FVector oldPos = round.OldPos;
        FVector pos = round.Pos;
        if (_roundLength > 0.001f)
        {
            FVector direction = pos - oldPos;
            direction.Normalise();
            return new LineVolume(oldPos - pos, direction * _roundLength);
        }

        return new LineVolume(oldPos - pos, new FVector(0, 0, 0));
    }
}
