// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/unit.h (the unit classes' SetThingType and GetMoveMultiplier), src/GroundUnit.cpp
// (CGroundUnit::Init 0047c730, Move 0047c970), src/GroundVehicle.cpp (CGroundVehicle::Init 0047cfd0, Move 0047d1c0),
// src/Cannon.cpp (CCannon::Init 0041b1a0, Move 0041b370), src/Building.cpp (CBuilding::Init 00417190, InitRenderThing 004176c0, SetThingType 00417660),
// src/SimpleBuilding.cpp (CSimpleBuilding::Init 004dfa40, Move 004dfaa0, ClipToGround 004dfcb0, Gravity 004dfcc0,
// SetThingType), src/AirUnit.cpp (CAirUnit::Init 00402ad0),
// src/Plane.cpp (CPlane::Init 004d19d0) and src/Dropship.cpp (CDropship::Init 00446d70). Not in Stuart's release.

using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;

using OnslaughtRebuild.Parity.Numerics;

using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CGroundUnit: a unit on the ground, a full move every fourth frame.</summary>
public abstract class GroundUnit(ThingContext context) : Unit(context)
{
    /// <summary>CGroundUnit::StartDieProcess (0047ce80): CUnit's, and the bob stops.</summary>
    public override bool StartDieProcess()
    {
        if (!base.StartDieProcess())
        {
            return false;
        }

        BobPhase = 0;
        return true;
    }

    /// <summary>CGroundUnit::InitCollisionSeekingThing (0047c8e0): without a shape, a sphere of 0.8 of the bounding radius.</summary>
    public override void InitCollisionSeekingThing(InitCollision init)
    {
        if (init.Shape is null)
        {
            float radius = GetBoundingRadius();
            init.Shape = new Collision.SphereVolume(radius * 0.8f);
        }

        base.InitCollisionSeekingThing(init);
    }

    public float BobPhase;
    public float Bob;
    public float OldBob;
    public int ThrusterLinks;

    /// <summary>
    /// mUnknown258 (+0x258): it bobs like a hovering unit. Nothing writes it for a ground vehicle and the allocator does
    /// not clear memory, so retail reads whatever the block held; it is read as 0 here (fresh heap pages are zero).
    /// OPEN QUESTION, falsifier: the port's movement trace of a vehicle's +0x250/+0x258.
    /// </summary>
    public int BobsLikeHover;

    /// <summary>The int CGroundUnit::Move reads at +0x258 as mUnknown258: a cannon's own field there is mAboveWater.</summary>
    protected virtual bool BobFlagAt258 => BobsLikeHover != 0;

    /// <summary>
    /// CGroundUnit::Move (0047c970, read from the Steam BEA.exe = the pristine specimen 74154bfa): the guide's requested
    /// velocity (mUnknown14C * 0.4) as acceleration with the ground friction when on the ground, in water or not obeying
    /// gravity (and turning), else the air friction; gravity; SINT(multiplier) steps of v = accel + v, v = friction * v;
    /// the speed along the ground held to Unknown111() * 0.05 (the source's / 20.0f compiles as fmul 0.05f), measured
    /// fsqrt(vY*vY + vX*vX), scaled by top / speed; then CUnit::Move; then the bob (fsin at full precision, * 0.05f).
    /// A parachute unit's thrusters (paratroopers) are not ported.
    /// </summary>
    public override void Move()
    {
        float multiplier = GetMoveMultiplier();
        int steps = X87.Fist(multiplier);
        float gravity = 0;
        if (ObeyGravity())
        {
            gravity = Gravity();
        }

        FVector accel = new(0, 0, 0);
        float friction = Unknown73();
        if ((IsOnGround() || IsInWater() || !ObeyGravity()) && Unknown76())
        {
            friction = Unknown72();
            accel = new FVector(Unknown14C.X * 0.4f, Unknown14C.Y * 0.4f, Unknown14C.Z * 0.4f);
        }

        if (!IsDying && UnitData!.Parachute == 1)
        {
            throw new NotSupportedException("CGroundUnit::Move's parachute thrusters are not ported");
        }

        accel.Z = accel.Z + gravity;
        for (int step = 0; step < steps; step++)
        {
            Velocity.X = accel.X + Velocity.X;
            Velocity.Y = accel.Y + Velocity.Y;
            Velocity.Z = accel.Z + Velocity.Z;
            Velocity.X = friction * Velocity.X;
            Velocity.Y = friction * Velocity.Y;
            Velocity.Z = friction * Velocity.Z;
        }

        float top = Unknown111() * 0.05f;
        float speed = (float)X87.Sqrt((Velocity.Y * Velocity.Y) + (Velocity.X * Velocity.X));
        if (speed > top)
        {
            float scale = top / speed;
            float vx = Velocity.X * scale;
            float vy = Velocity.Y * scale;
            Velocity.X = vx;
            Velocity.Y = vy;
        }

        base.Move();

        if (UnitData!.Hover != 0 || BobFlagAt258)
        {
            OldBob = Bob;
            if (IsDying)
            {
                Bob = BobPhase + Bob;
                BobPhase -= multiplier * 0.005f;
                return;
            }

            if (IsDeploying() && BobPhase < 0.1f)
            {
                BobPhase = 0;
                return;
            }

            BobPhase = (multiplier * 0.1f) + BobPhase;
            Bob = (float)X87.Mul(X87.Sin(BobPhase), 0.05f);
            if (BobPhase > 6.28318548f)
            {
                BobPhase -= 6.28318548f;
            }
        }
    }

    public override float GetMoveMultiplier() => 4.0f;

    public override void SetThingType(uint t) =>
        base.SetThingType(t | ThingType.Unknown40000000 | ThingType.GroundUnit);

    /// <summary>CGroundUnit::Init: shields, a thruster link per emitter, and a hovering unit's bob phase.</summary>
    public override void Init(InitThing init)
    {
        init.InitCst.DoObbForMeshCollision = true;
        base.Init(init);
        MaxShields = Shields = init.Stats!.GroundShields;
        ShieldRegeneration = init.Stats.GroundShieldRegeneration;
        if (UnitData!.EffectNames[3] is not null)
        {
            ThrusterLinks = RenderThing!.CountEmitters("Thruster");
            Context.Skip("PARTICLE_SET lookup stood in by the effect name (thrusters)");
        }

        BobPhase = 0;
        Bob = 0;
        OldBob = 0;
        if (UnitData is not null && UnitData.Hover != 0)
        {
            BobPhase = Context.Random.FloatRandom() * (MathF.PI * 2);
        }

        ThrustersActive = false;
    }
}

/// <summary>CGroundVehicle: tanks and trucks.</summary>
public sealed class GroundVehicle(ThingContext context) : GroundUnit(context)
{
    /// <summary>Unknown112 (slot 112): a vehicle shuts down half a second after its removal.</summary>
    public override float Unknown112() => 0.5f;

    /// <summary>CGroundVehicle::StartDieProcess (0041b590): the ground unit's death, then Remove (its explosion, its script, its shutdown).</summary>
    public override bool StartDieProcess()
    {
        if (!base.StartDieProcess())
        {
            return false;
        }

        Remove();
        return true;
    }

    /// <summary>GetMaxVelocity (slot 15).</summary>
    public override float GetMaxVelocity() => 3.5f;


    public int TiltsWithGround;

    /// <summary>mMotionController when it is a CMCGroundVehicle (Init: a vehicle without wheel motion).</summary>
    public GroundVehicleMotionController? MotionController;

    /// <summary>COfGHeight (slot 48): 0.1.</summary>
    public override float COfGHeight() => 0.1f;

    /// <summary>
    /// CGroundVehicle::Move (0047d1c0, read from the Steam BEA.exe = the pristine specimen 74154bfa): a vehicle that
    /// tilts (obeying gravity, without a buggy controller) takes its target pitch and roll from the ground: forward is
    /// MakeRotationYawF's Y column, (-sin yaw, cos yaw, 0) from fsin/fcos stored as floats; forward ^ normal, then up =
    /// that ^ normal, normalised inline ((Z*Z + Y*Y) + X*X, times the reciprocal); pitch = -up.Z; roll = the first
    /// cross product's Z after FVector::Normalise (out of line, 00406d50: (X*X + Y*Y) + Z*Z). Then CGroundUnit's move.
    /// </summary>
    public override void Move()
    {
        if (ObeyGravity() && TiltsWithGround != 0)
        {
            float yaw = CurrentOrientation.Yaw;
            var forward = new FVector((float)-X87.Sin(yaw), (float)X87.Cos(yaw), 0.0f);
            FVector normal = Context.Map.Normal(Pos);
            forward = FVector.Cross(forward, normal);
            FVector up = FVector.Cross(forward, normal);
            float m = (float)X87.Sqrt((up.Z * up.Z) + (up.Y * up.Y) + (up.X * up.X));
            if (m != 0.0f)
            {
                float r = 1.0f / m;
                up = new FVector(up.X * r, up.Y * r, up.Z * r);
            }

            Unknown120.Pitch = -up.Z;
            FVector side = forward;
            side.Normalise();
            Unknown120.Roll = side.Z;
        }

        base.Move();
    }

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Vehicle);

    public override void Init(InitThing init)
    {
        base.Init(init);
        float turn = init.Stats!.TurnRate;
        Unknown12C = new EulerAngles(turn, turn, turn);
        MaxShields = Shields = init.Stats.GroundShields;
        ShieldRegeneration = init.Stats.GroundShieldRegeneration;
        TiltsWithGround = 1;
        if (AnimModeByName("WheelMotion") != -1)
        {
            Context.Skip("CMCBuggy (wheeled motion controller)");
            TiltsWithGround = 0;
        }
        else
        {
            MotionController = new GroundVehicleMotionController(this);
        }

        Guide = new GroundVehicleGuide(this);
        AI = new UnitAI(this, init);
    }
}

/// <summary>CCannon: a turret.</summary>
public sealed class Cannon(ThingContext context) : GroundUnit(context)
{
    /// <summary>CMCCannon, created by CCannon::Init (0041b1a0) after its guide and AI.</summary>
    public CannonMotionController? MotionController;

    /// <summary>CCannon::StartDieProcess (0041b590): the ground unit's death, then Remove (its explosion, its script, its shutdown).</summary>
    public override bool StartDieProcess()
    {
        if (!base.StartDieProcess())
        {
            return false;
        }

        Remove();
        return true;
    }

    public int State;
    public bool AboveWater;
    public float LastTargetTime;

    /// <summary>
    /// CGroundUnit::Move reads +0x258, which in a cannon is mAboveWater: a cannon above water runs the hover bob on its
    /// +0x250/+0x254/+0x25c (its sit-down amount and neighbours, which are the bob fields here, as CGroundUnit::Init
    /// writes them).
    /// </summary>
    protected override bool BobFlagAt258 => AboveWater;

    /// <summary>Gravity (slot 45, the shared 004bfc60): none.</summary>
    public override float Gravity() => 0.0f;

    /// <summary>GetMaxVelocity (slot 15, the shared 004bfc60): none.</summary>
    public override float GetMaxVelocity() => 0.0f;

    /// <summary>
    /// CCannon::Move (0041b370): an active cannon with a target plays "Activate" (unless active or activating) and notes
    /// the time; without one for ten seconds it plays "Deactivate" (unless inactive or deactivating); then CGroundUnit's.
    /// </summary>
    public override void Move()
    {
        if (Active && AI!.Target.ToRead is not null)
        {
            if (State != 0 && State != 3)
            {
                SetAnimMode(AnimModeByName("Activate"), true, false);
                State = 3;
            }

            if (AI.Target.ToRead is not null)
            {
                LastTargetTime = Context.Events.Time;
            }
        }
        else if (LastTargetTime + 10.0f < Context.Events.Time)
        {
            if (State != 1 && State != 2)
            {
                SetAnimMode(AnimModeByName("Deactivate"), true, false);
                State = 2;
            }
        }

        base.Move();
    }

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Emplacement | ThingType.Stationary);

    public override void Init(InitThing init)
    {
        init.InitCst.NotSeekCollisionWith |= ThingType.Stationary;
        base.Init(init);
        if (Active)
        {
            SetAnimMode(AnimModeByName("Active"), true, true);
            State = 0;
        }
        else
        {
            SetAnimMode(AnimModeByName("Inactive"), true, true);
            State = 1;
        }

        Guide = new BasicGuide(this, "CTerrainGuide");
        AI = new UnitAI(this, init);
        Unknown12C = new EulerAngles(0, 0, 0);
        MotionController = new CannonMotionController(this);
        UnknownF4 = 0;
        LastTargetTime = 0;
        Occupancy.AddUnit(Context.Occupancy, Context.Map, this);
        AboveWater = !(Context.WaterLevel > Pos.Z);
    }
}

/// <summary>CBuilding: a building, with its own render thing (mesh and rubble) and destructable segments.</summary>
public sealed class Building(ThingContext context) : Unit(context)
{
    /// <summary>
    /// CRTBuilding::Unknown10 (004dba40): the rubble mesh (the unit data's rubble name, else "m-b-rubble") at a random one of
    /// its six levels of detail (main-thread rand), stopping at the chain's end.
    /// </summary>
    public Mesh.MeshData? PickRubble()
    {
        string name = (UnitData?.Rubble is { } r ? System.Text.Encoding.ASCII.GetString(r) : "m-b-rubble") + ".msh";
        if (!Context.Meshes.TryGetValue(name, out Mesh.MeshData? rubble) && !Context.Meshes.TryGetValue("m-b-rubble.msh", out rubble))
        {
            throw new NotSupportedException($"CRTBuilding: the rubble mesh {name} is not in the level resources");
        }

        int n = Context.Crt.Rand() % 6;
        Mesh.MeshData m = rubble;
        for (int i = 0; i < n && m.NextInChain is not null; i++)
        {
            m = m.NextInChain;
        }

        return m;
    }

    /// <summary>
    /// CBuilding::Damage (004179a0): noted as hit; an open door begins to shut; a segmented building, or one not yet dying,
    /// takes CUnit's damage.
    /// </summary>
    public override void Damage(float amount, Thing? byThis, bool damageShields = true, int meshPart = -1)
    {
        Unknown268 = Context.Events.Time;
        if (Unknown260 && Unknown264 == 3)
        {
            SetAnimMode(AnimModeByName("shutting"), true, false);
            Unknown264 = 0;
        }

        if (Segments is not null || !IsDying)
        {
            base.Damage(amount, byThis, damageShields, meshPart);
        }
    }

    /// <summary>
    /// CBuilding::StartDieProcess (00417a40): CUnit's, the children released; an unsegmented building explodes (its
    /// explosion, its life to -1, the explosion's own effect) and throws forty pieces of its rubble (three main-thread
    /// rand draws each, and the rubble pick); a scripted building tells its script next frame (5000).
    /// </summary>
    public override bool StartDieProcess()
    {
        if (!base.StartDieProcess())
        {
            return false;
        }

        if (UnitData?.Components.Count > 0)
        {
            throw new NotSupportedException("CUnit::ReleaseChildren (a unit's components) is not ported");
        }

        if (Segments is null)
        {
            int explosionId = UnitData!.Explosion;
            Physics.ExplosionData? data = explosionId >= 0 && explosionId < Context.Physics.Explosions.Count ? Context.Physics.Explosions[explosionId] : null;
            if (Explosion.Spawn(Context, explosionId) is { } explosion)
            {
                explosion.Init(new InitThing { Behaviour = data, Pos = Pos, Allegiance = Allegiance });
            }

            Life = -1.0f;
            if (Context.Particles is { } particles)
            {
                if (explosionId != -1)
                {
                    particles.Manager.CreateEffect(particles.Manager.Set.GetPD(data!.NameText), null, Pos, false, false);
                }

                Particles.ParticleDescriptor? pd = particles.Manager.Set.GetPD("Generic Mesh");
                for (int i = 0; i < 40; i++)
                {
                    var link = new Particles.ParticleEffectLink(particles.Manager);
                    particles.Manager.CreateEffect(pd, link, new FVector(0, 0, 0), false, false);
                    int a = (Context.Crt.Rand() & 0xff) - 128;
                    int b = (Context.Crt.Rand() & 0xff) - 128;
                    int c = (Context.Crt.Rand() & 0xff) - 128;
                    var vel = new FVector(c * 0.0003125f, b * 0.0003125f, a * 0.0003125f);
                    FVector displacement = vel * 20.0f;
                    FVector pos = displacement + (Pos - new FVector(0, 0, 1.0f));
                    link.SetPos(pos);
                    if (link.Full && link.Effect!.Particle is { } particle)
                    {
                        vel.Z = (vel.Z * 2) - 0.04f;
                        particle.Velocity = vel;
                        if (PickRubble() is { } rubble)
                        {
                            particle.Data = rubble;
                            particle.Radius = rubble.Radius;
                        }
                    }

                    link.Release();
                }
            }
        }

        if (Script is not null)
        {
            Context.Events.AddEvent(5000, this, Events.EventManager.NextFrame);
        }

        return true;
    }

    public override void HandleEvent(Events.Event e)
    {
        if (e.EventNum == 5000 && Script is not null)
        {
            Script.Died();
            Script.Delete();
            Script = null;
        }
        else
        {
            base.HandleEvent(e);
        }
    }

    public override bool CanBeStaticallyShadowed => true;

    /// <summary>GetMaxVelocity (slot 15, the shared 004bfc60).</summary>
    public override float GetMaxVelocity() => 0.0f;


    /// <summary>
    /// CBuilding::StickToGround (004dfd10, shared with CSimpleBuilding by /OPT:ICF): grounded, then lowered to the
    /// water level when it stands above it.
    /// </summary>
    public override void StickToGround()
    {
        base.StickToGround();
        if (Context.Map.WaterLevel < Pos.Z)
        {
            Pos.Z = Context.Map.WaterLevel;
            OldPos.Z = Context.Map.WaterLevel;
        }
    }

    /// <summary>
    /// CBuilding::Move (004178a0): the shot timer (a prepared shot becomes ready, which is how a repair pad fires), the
    /// spawner doors (an open door closes 8 seconds after its spawners fall idle; a busy spawner opens it), and a shut
    /// door reopening 15 seconds after the building was last hit. Spawning is not ported (it throws where it would start),
    /// so no spawner is ever busy here.
    /// </summary>
    public override void Move()
    {
        UpdateTimer();

        // With no spawner busy: an open door closes once its time has passed (a busy one would push the time 8 seconds
        // on, and with the door shut a busy spawner would open it, Unknown115).
        if (Unknown254 == 2 && Unknown25C < Context.Events.Time)
        {
            int anim = AnimModeByName("closing");
            if (anim == -1)
            {
                Unknown254 = 3;
            }
            else
            {
                Unknown254 = 1;
                SetAnimMode(anim, true, false);
            }
        }

        if (Unknown260 && Unknown264 == 1 && Unknown268 + 15.0f < Context.Events.Time)
        {
            SetAnimMode(AnimModeByName("unshutting"), true, false);
            Unknown264 = 2;
        }
    }

    public float Unknown258 = -1.0f;
    public int Unknown254;
    /// <summary>mUnknown25C: when an open spawner door may close.</summary>
    public float Unknown25C;
    public bool Unknown260;
    public int Unknown264;
    public float Unknown268;

    public override void SetThingType(uint t) => base.SetThingType(
        t | ThingType.Unknown40000000 | ThingType.CanBeWalkedOn | ThingType.Building | ThingType.Stationary);

    /// <summary>CreateRepairPadAI (00417390): the repair pad's AI services battle engines; every other building fights.</summary>
    private void CreateRepairPadAI(InitThing init)
    {
        if (string.Equals(UnitData!.NameText, "Forseti Repair Pad", StringComparison.OrdinalIgnoreCase))
        {
            AI = new RepairPadAI(this, init);
            IsRepairPad = true;
        }
        else
        {
            AI = new UnitAI(this, init);
        }
    }

    /// <summary>mUnknown1F4: set for the repair pad.</summary>
    public bool IsRepairPad;

    /// <summary>CBuilding::InitRenderThing: "&lt;mesh&gt;.msh" and its rubble "&lt;rubble&gt;.msh" (RTID 4, CRTBuilding).</summary>
    public override void InitRenderThing(InitThing init) =>
        RenderThing = MakeMeshRenderThing([.. init.Stats!.Mesh!, .. ".msh"u8.ToArray()]);

    public override void Init(InitThing init)
    {
        init.InitCst.MinCollision = CollisionLevel.Mesh;
        init.InitCst.MaxCollision = CollisionLevel.Mesh;
        init.InitCst.NotSeekCollisionWith |= ThingType.Stationary | ThingType.Unknown08000000;
        init.InitCst.UseFixedMeshTransforms = init.Stats!.AnimatedCollision == 0;
        Unknown258 = -1.0f;
        // The segments and their motion controller, kept only when the mesh has a core segment.
        Segments = new DestructableSegments(this, 2.0f, true, 0);
        SegmentsController = new SegmentsMotionController(Segments);
        base.Init(init);
        if (Segments.Cores <= 0)
        {
            Segments = null;
            SegmentsController = null;
        }

        int anim = AnimModeByName("closed");
        if (anim != -1)
        {
            SetAnimMode(anim, true, true);
        }

        Unknown254 = 3;
        Unknown25C = 0;
        anim = AnimModeByName("notshut");
        if (anim != -1)
        {
            SetAnimMode(anim, true, true);
            Unknown260 = true;
        }
        else
        {
            Unknown260 = false;
        }

        Unknown264 = 3;
        Unknown268 = -100.0f;
        StickToGround();
        CreateRepairPadAI(init);
        SetAnimMode(AnimModeByName("Idle"), true, true);
        Occupancy.AddUnit(Context.Occupancy, Context.Map, this);
    }
}

/// <summary>CSimpleBuilding: a building without segments or AI.</summary>
public sealed class SimpleBuilding(ThingContext context) : Unit(context)
{
    public override bool CanBeStaticallyShadowed => true;

    /// <summary>GetMaxVelocity (slot 15, the shared 004bfc60).</summary>
    public override float GetMaxVelocity() => 0.0f;


    /// <summary>
    /// CBuilding::StickToGround (004dfd10, shared with CSimpleBuilding by /OPT:ICF): grounded, then lowered to the
    /// water level when it stands above it.
    /// </summary>
    public override void StickToGround()
    {
        base.StickToGround();
        if (Context.Map.WaterLevel < Pos.Z)
        {
            Pos.Z = Context.Map.WaterLevel;
            OldPos.Z = Context.Map.WaterLevel;
        }
    }

    /// <summary>CSimpleBuilding::Move (004dfaa0): a standing building does nothing; a dying one sinks with explosions (not ported).</summary>
    public override void Move()
    {
        if (!IsDying)
        {
            return;
        }

        throw new NotSupportedException("CSimpleBuilding::Move's dying explosions are not ported");
    }

    /// <summary>CSimpleBuilding::ClipToGround (004dfcb0): not while dying.</summary>
    public override bool ClipToGround() => !IsDying;

    /// <summary>CSimpleBuilding::Gravity (004dfcc0): 0.002 while dying, else none.</summary>
    public override float Gravity() => IsDying ? 0.002f : 0.0f;

    public override void SetThingType(uint t) => base.SetThingType(
        t | ThingType.Unknown40000000 | ThingType.CanBeWalkedOn | ThingType.Building | ThingType.Stationary);

    public override void Init(InitThing init)
    {
        init.InitCst.MinCollision = CollisionLevel.Mesh;
        init.InitCst.MaxCollision = CollisionLevel.Mesh;
        init.InitCst.NotSeekCollisionWith |= ThingType.Stationary | ThingType.Unknown08000000;
        init.InitCst.UseFixedMeshTransforms = true;
        base.Init(init);
        StickToGround();
        AI = null;
        Occupancy.AddUnit(Context.Occupancy, Context.Map, this);
    }
}

/// <summary>CAirUnit: a flying unit.</summary>
public abstract class AirUnit(ThingContext context) : Unit(context)
{
    /// <summary>
    /// CAirUnit::StartDieProcess (00403690): CUnit's death, its children released (killed, as it is dying), and its trails'
    /// and engines' links stopped and deleted.
    /// </summary>
    public override bool StartDieProcess()
    {
        if (!base.StartDieProcess())
        {
            return false;
        }

        if (UnitData?.Components.Count > 0)
        {
            throw new NotSupportedException("CUnit::ReleaseChildren (a unit's components) is not ported");
        }

        foreach (List<ParticleEffectLink> links in new[] { Trails, Engines })
        {
            foreach (ParticleEffectLink link in links)
            {
                link.Stop(false);
                link.Release();
            }

            links.Clear();
        }

        return true;
    }

    /// <summary>mUnknown258 (+0x258): the "Smoke" emitter a dying aircraft trails smoke from (0: none yet).</summary>
    public int Unknown258;

    /// <summary>
    /// CAirUnit::Damage (004037a0): CUnit's, then once it is dying (and not yet shut down) its smoke starts at one of its
    /// "Smoke" emitters: the only one, or one drawn from the rest by GAME.Random(); the player's shots make the effect an
    /// important one.
    /// </summary>
    public override void Damage(float amount, Thing? byThis, bool damageShields = true, int meshPart = -1)
    {
        base.Damage(amount, byThis, damageShields, meshPart);
        if (Life < 0 && (Flags & ThingFlags.DeclaredShutdown) == 0 && Unknown258 == 0)
        {
            Unknown258 = 0;
            var smoke = Context.FindParticle(UnitData!.EffectNames[4]);
            if (smoke is not null)
            {
                bool important = byThis is Round round && round.Launcher.ToRead is { } launcher && launcher.IsA(ThingType.BattleEngine);
                int n = 1;
                GetEmitter(25, n, out FVector pos, out FMatrix _);
                while (pos.X != 0 || pos.Y != 0 || pos.Z != 0)
                {
                    GetEmitter(25, ++n, out pos, out FMatrix _);
                }

                n--;
                if (n != 0)
                {
                    Unknown258 = n == 1 ? n : (Context.Random.Random() % (n - 1)) + 1;
                    GetEmitter(25, Unknown258, out pos, out FMatrix _);
                    if ((pos.X != 0 || pos.Y != 0 || pos.Z != 0) && Effect is not null && Context.Particles is { } particles)
                    {
                        Effect.Effect = null;
                        particles.Manager.CreateEffect(smoke, Effect, new FVector(0, 0, 0), false, important);
                        Effect.SetPos(pos);
                    }
                }
            }
        }
    }

    /// <summary>CAirUnit::DeclareOnGround (00403730): a dying aircraft explodes where it lands (unless mUnknown11C).</summary>
    public override void DeclareOnGround()
    {
        base.DeclareOnGround();
        if (IsDying && UnitData!.Retreat == 0)
        {
            SpawnUnitExplosion();
            AddShutdownEvent();
        }
    }

    /// <summary>CAirUnit::DeclareInWater (00403760): a dying aircraft explodes in the water (unless mUnknown11C or big).</summary>
    public override void DeclareInWater()
    {
        base.DeclareInWater();
        if (IsDying && UnitData!.Retreat == 0 && UnitData.Big == 0)
        {
            SpawnUnitExplosion();
            AddShutdownEvent();
        }
    }

    /// <summary>
    /// CAirUnit::Move (00402fa0): a dying aircraft tumbles; the guide's thrust and gravity join the velocity; a big unit
    /// in the water slows (0.95, sinking when dying), any other takes its drag (Unknown73); the speed is held to
    /// Unknown111 / 20; then CUnit::Move. The trails' and engines' particles are presentation (counted).
    /// </summary>
    public override void Move()
    {
        if (IsDying)
        {
            // `/ 3.0f` compiles as a multiply by the float 1/3 (0x3eaaaaab at 005d8608, 00402fc5).
            float turn = UnitData!.TurnRate * 0.33333334f;
            Unknown12C = new EulerAngles(turn, turn, turn);
        }

        Velocity = Velocity + Unknown14C;
        Velocity.Z += Gravity();
        if (UnitData!.Big != 0 && Pos.Z + (GetRadius() * 0.2f) > Context.Map.WaterLevel)
        {
            Velocity = Velocity * 0.95f;
            if (IsDying)
            {
                throw new NotSupportedException("CUnit::Unknown96 (a big aircraft sinking) is not ported");
            }
        }
        else
        {
            Velocity = Velocity * Unknown73();
        }

        // Read from the instructions (Steam BEA.exe = the pristine specimen): X*X + Y*Y, then + Z*Z, fsqrt (004030d7..
        // 004030f6); the cap's `/ 20.0f` is a multiply by 0.05f (005d8584, 0040311a), then max / speed scales each axis.
        float speed = (float)X87.Sqrt((Velocity.X * Velocity.X) + (Velocity.Y * Velocity.Y) + (Velocity.Z * Velocity.Z));
        if (speed > 0)
        {
            float max = Unknown111() * 0.05f;
            if (speed > max)
            {
                Velocity = Velocity * (max / speed);
            }
        }

        base.Move();

        if (Unknown170 + 0.1f < Unknown174)
        {
            Unknown170 += 0.1f;
        }
        else if (Unknown170 - 0.1f > Unknown174)
        {
            Unknown170 -= 0.1f;
        }
        else
        {
            Unknown170 = Unknown174;
        }

        Unknown170 *= 1.0f - (GetStealth() * 0.01f); // retail multiplies by its 0.01f constant (005d85fc)
        // The trails (each wide as Unknown170) and, while it moves, the engines.
        if (Context.Particles is { } particles)
        {
            int n = 1;
            foreach (ParticleEffectLink link in Trails)
            {
                if (Unknown170 > 0 && link.Effect is null)
                {
                    particles.Manager.CreateEffect(Context.FindParticle(UnitData!.EffectNames[2]), link, new FVector(0, 0, 0), false, false);
                }

                GetEmitter(24, n, out FVector pos, out FMatrix _);
                link.SetPos(pos);
                link.SetOrientation(Orientation);
                if (link.Effect?.Particle is { } p)
                {
                    p.Radius = Unknown170;
                }

                n++;
            }

            n = 1;
            foreach (ParticleEffectLink link in Engines)
            {
                if (Unknown117())
                {
                    if (link.Effect is null)
                    {
                        particles.Manager.CreateEffect(Context.FindParticle(UnitData!.EffectNames[5]), link, new FVector(0, 0, 0), false, false);
                    }

                    GetEmitter(23, n, out FVector pos, out FMatrix ori);
                    link.SetPos(pos);
                    link.SetOrientation(ori);
                }
                else
                {
                    link.Stop(false);
                }

                n++;
            }
        }

        // Dying: it explodes once it has taken its unit data's life again in damage, and trails smoke meanwhile.
        if (Life < 0)
        {
            if (-UnitData!.Life > Life && UnitData.Retreat == 0)
            {
                SpawnUnitExplosion();
                AddShutdownEvent();
            }

            if (Unknown258 != 0 && (Flags & ThingFlags.DeclaredShutdown) == 0)
            {
                GetEmitter(25, Unknown258, out FVector smokePos, out FMatrix _);
                Effect?.SetPos(smokePos);
            }
        }
    }

    /// <summary>CAirUnit::Shutdown (00402d30): off the air units' list (Set50), its effects stopped, then CUnit's.</summary>
    public override void Shutdown()
    {
        Context.World.Set50.Remove(this);
        Effect?.Stop(false);
        foreach (List<ParticleEffectLink> links in new[] { Trails, Engines })
        {
            foreach (ParticleEffectLink link in links)
            {
                link.Stop(false);
                link.Release();
            }

            links.Clear();
        }

        base.Shutdown();
    }

    /// <summary>GetRenderImposterBias: an aircraft keeps its mesh to twice the distance.</summary>
    public override float GetRenderImposterBias() => 0.5f;

    /// <summary>GetMaxVelocity: 13 for an air unit.</summary>
    public override float GetMaxVelocity() => 13.0f;

    /// <summary>Gravity (00403b60): a dying aircraft falls faster the smaller it is.</summary>
    public override float Gravity()
    {
        if (IsDying)
        {
            float r = GetRadius();
            if (r > 39.0f)
            {
                r = 39.0f;
            }

            return (40.0f - r) * 0.0004f;
        }

        return 0.0f;
    }

    /// <summary>Unknown117 (00403a50): it moved, and is not dying.</summary>
    public bool Unknown117() => (OldPos.X != Pos.X || OldPos.Y != Pos.Y || OldPos.Z != Pos.Z) && !IsDying;

    /// <summary>GoToPoint (00403a90): no lower than its minimum altitude above the ground or the sea.</summary>
    public override void GoToPoint(FVector point, bool inOverride = false)
    {
        if (Guide is null)
        {
            return;
        }

        World.HeightField map = Context.Map;
        float h = map.HeightScale * map.GetHeightSample(X87.Fist(point.X), X87.Fist(point.Y));
        float water = map.WaterLevel;
        if (h > water)
        {
            h = water;
        }

        h -= UnitData!.MinAltitude;
        if (point.Z > h)
        {
            point.Z = h;
        }

        Guide.GoToPoint(point, inOverride);
    }

    /// <summary>mTrails: one link per Trail emitter.</summary>
    public readonly List<ParticleEffectLink> Trails = [];

    /// <summary>mEngines: one link per Engine emitter.</summary>
    public readonly List<ParticleEffectLink> Engines = [];

    /// <summary>mEffect: the smoke's link (a member, so on the manager's list from construction).</summary>
    public ParticleEffectLink? Effect;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Unknown40000000 | ThingType.AirUnit);

    public override void Init(InitThing init)
    {
        base.Init(init);
        float turn = init.Stats!.TurnRate;
        Unknown12C = new EulerAngles(turn, turn, turn);
        Shields = init.Stats.AirShields;
        MaxShields = Shields;
        ShieldRegeneration = init.Stats.AirShieldRegeneration;
        if (Context.Particles is { } particles)
        {
            Effect = new ParticleEffectLink(particles.Manager);
            if (Context.FindParticle(UnitData!.EffectNames[2]) is not null)
            {
                for (int n = RenderThing!.CountEmitters("Trail"); n > 0; n--)
                {
                    Trails.Add(new ParticleEffectLink(particles.Manager));
                }
            }

            if (Context.FindParticle(UnitData.EffectNames[5]) is not null)
            {
                for (int n = RenderThing!.CountEmitters("Engine"); n > 0; n--)
                {
                    Engines.Add(new ParticleEffectLink(particles.Manager));
                }
            }
        }

        Context.World.Set50.Add(this);
    }
}

/// <summary>CPlane (a CSmallAirUnit): fighters and trainers.</summary>
public sealed partial class Plane(ThingContext context) : AirUnit(context)
{
    /// <summary>
    /// CPlane::Hit (004d1f10), then CSmallAirUnit::Hit (00403ba0), then CUnit's: unless its unit data's mUnknown11C is
    /// set, a plane that flies into a unit (not a friendly aircraft) or a tree or walkable thing explodes, and so does a
    /// dying one.
    /// </summary>
    public override void Hit(Thing other, Collision.CollisionReport? report)
    {
        if (UnitData!.Retreat == 0)
        {
            if ((!other.IsA(ThingType.AirUnit) || other.IsA(ThingType.BattleEngine) || ((Unit)other).Allegiance != Allegiance)
                && (other.IsA(ThingType.Unit) || other.IsA(ThingType.Tree | ThingType.CanBeWalkedOn)))
            {
                if (other is Unit victim)
                {
                    victim.ConfirmedKill(this);
                }

                SpawnUnitExplosion();
                AddShutdownEvent();
            }

            // CSmallAirUnit::Hit: a dying aircraft explodes on what it flies into (after CPlane's, so both can explode).
            if (IsDying && (other.IsA(ThingType.Unit) || other.IsA(ThingType.Tree | ThingType.CanBeWalkedOn)))
            {
                SpawnUnitExplosion();
                AddShutdownEvent();
            }
        }

        base.Hit(other, report);
    }

    public int WingState;
    public float LaunchTime;
    public float DeathRoll;
    public int ThrusterLinks => Thrusters.Count;

    public override void Init(InitThing init)
    {
        init.InitCst.MaxCollision = CollisionLevel.ApproxGeometryShapes;
        base.Init(init);
        Guide = new AirGuide(this);
        AI = new PlaneAI(this, init);
        SetAnimMode(AnimModeByName("launch"), true, true);
        if (AI.AIMode == 2)
        {
            WingState = 0;
            LaunchTime = Context.Events.Time + 2.5f;
        }
        else
        {
            WingState = 1;
            LaunchTime = 0;
        }
        InitLaunchThrusters();

        DeathRoll = (float)(Context.Random.Random() % 65536) / 65536.0f > 0.5f ? 0.8f : -0.8f;
    }

    public override float GetMaxVelocity() => 15.0f;

    public override float Gravity() => IsDying ? 0.025f : 0.0f;

    /// <summary>CPlane::DeclareOnGround (004d20a0): CAirUnit's, then without "Retreat" any plane that touches the ground dies.</summary>
    public override void DeclareOnGround()
    {
        base.DeclareOnGround();
        if (UnitData!.Retreat == 0)
        {
            SpawnUnitExplosion();
            AddShutdownEvent();
        }
    }

    /// <summary>CPlane::DeclareInWater (0047bf60, folded): CAirUnit's, then a plane in the water dies (unless retreating or big).</summary>
    public override void DeclareInWater()
    {
        base.DeclareInWater();
        if (UnitData!.Retreat == 0 && UnitData.Big == 0)
        {
            SpawnUnitExplosion();
            AddShutdownEvent();
        }
    }

    /// <summary>CPlane::Stop (00422750): only the guide stops (the plane keeps flying).</summary>
    public override void Stop() => Guide?.Stop();

    /// <summary>
    /// CPlane::Move (004d1cd0): a dying plane rolls and noses down; otherwise the launch thrusters burn until the launch
    /// time and the wings open once out of launch mode. After CAirUnit::Move the velocity is turned onto
    /// the plane's heading at the same speed.
    /// </summary>
    public override void Move()
    {
        if (IsDying)
        {
            // The death roll: rolling one way (DeathRoll) and nosing straight down. Retail wraps with `roll < PI`, not
            // `-PI`, so any roll up to PI gains 2 PI.
            const float Pi = 3.14159265f;
            Unknown12C.Roll = 0.2f;
            float roll = DeathRoll + CurrentOrientation.Roll;
            if (roll > Pi)
            {
                roll -= Pi * 2;
            }
            else if (roll < Pi)
            {
                roll += Pi * 2;
            }

            Unknown120.Roll = roll;
            Unknown120.Pitch = Pi / 2;
        }
        else
        {
            UpdateLaunchThrusters();

            if (AI!.AIMode != 2 && WingState == 0)
            {
                WingState = 1;
            }
        }

        base.Move();

        Context.Skip("CPlane::Move's speed magnitude (its sum order is unchecked)");
        // As compiled (004d1e91..004d1eb0): X*X + Y*Y, then + Z*Z, fsqrt, used from the register for each product.
        float speed = (float)X87.Sqrt((Velocity.X * Velocity.X) + (Velocity.Y * Velocity.Y) + (Velocity.Z * Velocity.Z));
        Velocity = new FVector(speed * Orientation.Row0.Y, speed * Orientation.Row1.Y, speed * Orientation.Row2.Y);
    }
}

/// <summary>CDropship (a CBigAirUnit): the U-17 transporter.</summary>
public sealed class Dropship(ThingContext context) : AirUnit(context)
{
    /// <summary>CMCDropship, created by CDropship::Init (00446e96) before its guide and AI.</summary>
    public DropshipMotionController? MotionController;

    /// <summary>CBigAirUnit::Hit (00403bf0), then CUnit's: a dying one explodes on a walkable thing (unless mUnknown11C).</summary>
    public override void Hit(Thing other, Collision.CollisionReport? report)
    {
        if (UnitData!.Retreat == 0 && IsDying && other.IsA(ThingType.CanBeWalkedOn))
        {
            throw new NotSupportedException("CBigAirUnit::Hit: a dying aircraft's crash (SpawnExplosion) is not ported");
        }

        base.Hit(other, report);
    }

    public bool HasAnchor;
    public bool AnchorFixed;
    public int DoorState;

    public override void SetThingType(uint t) => base.SetThingType(
        t | ThingType.CanDestroyTrees | ThingType.CanBeWalkedOn | ThingType.Vehicle | ThingType.Animal);

    public override void Init(InitThing init)
    {
        HasAnchor = false;
        AnchorFixed = false;
        init.InitCst.MinCollision = CollisionLevel.Mesh;
        init.InitCst.NotSeekCollisionWith |= ThingType.Animal | ThingType.Building | ThingType.Feature | ThingType.Tree
            | ThingType.Unknown08000000;
        init.InitCst.MaxCollision = CollisionLevel.Mesh;
        base.Init(init);
        float turn = UnitData!.TurnRate;
        Unknown12C = new EulerAngles(turn, turn * 0.25f, turn * 0.25f);
        if (Context.Map.Collide(Pos) <= init.Pos.Z)
        {
            DoorState = 6;
            SetAnimMode(AnimModeByName("doorclosed"), true, true);
        }
        else
        {
            DoorState = 0;
            SetAnimMode(AnimModeByName("wingflat"), true, true);
        }

        MotionController = new DropshipMotionController(this);
        Guide = new DropshipGuide(this);
        AI = new DropshipAI(this, init);
        if (UnitData.EffectNames[3] is not null)
        {
            Context.Skip("the dropship's thruster count (through its motion controller's pose)");
        }
    }

    /// <summary>mYawDelta: the yaw it lands at.</summary>
    public float YawDelta;

    /// <summary>mUnknown2A4: the door animation's place; mUnknown2A8 last frame's.</summary>
    public float Unknown2A4;
    public float Unknown2A8;

    public override float Unknown73() => 0.99f;

    /// <summary>Stop: nothing (slot 64).</summary>
    public override void Stop()
    {
    }

    /// <summary>Gravity (00448360): a dying dropship falls faster the smaller it is.</summary>
    public override float Gravity()
    {
        if (IsDying)
        {
            float r = GetRadius();
            if (r > 39.0f)
            {
                r = 39.0f;
            }

            return (40.0f - r) * 0.0001f;
        }

        return 0.0f;
    }

    public override bool ClipToGround() => !IsDying;

    /// <summary>DeclareOnGround (004dfc60): CActor's directly, a dying one explodes, and it stops.</summary>
    public override void DeclareOnGround()
    {
        LastTimeOnGround = Context.Events.Time;
        if (IsDying)
        {
            throw new NotSupportedException("CDropship::DeclareOnGround's dying explosion is not ported");
        }

        Velocity = new FVector(0, 0, 0);
    }

    /// <summary>
    /// CDropship::Move (00447120), as far as level 100 reaches: in flight with its wings flat (door state 0) it is
    /// CAirUnit's flight, with the door animation's place reset. The landing, the doors, the dying fall and the
    /// thrusters' particles are not ported (the first three throw).
    /// </summary>
    public override void Move()
    {
        if (Unknown168 == 1)
        {
            Unknown168 = 0;
            Unknown1E8 = 1;
        }

        if (IsDying)
        {
            throw new NotSupportedException("CDropship::Move's dying fall is not ported");
        }

        if (DoorState != 0)
        {
            throw new NotSupportedException($"CDropship::Move in door state {DoorState} (landing and the doors) is not ported");
        }

        base.Move();
        Unknown2A8 = Unknown2A4;
        Unknown2A4 = 0;
    }
}
