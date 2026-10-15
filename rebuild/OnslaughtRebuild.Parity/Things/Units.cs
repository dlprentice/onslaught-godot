// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/unit.h (the unit classes' SetThingType and GetMoveMultiplier), src/GroundUnit.cpp
// (CGroundUnit::Init 0047c730, Move 0047c970), src/GroundVehicle.cpp (CGroundVehicle::Init 0047cfd0, Move 0047d1c0),
// src/Cannon.cpp (CCannon::Init 0041b1a0, Move 0041b370), src/Building.cpp (CBuilding::Init 00417190, InitRenderThing 004176c0, SetThingType 00417660),
// src/SimpleBuilding.cpp (CSimpleBuilding::Init 004dfa40, Move 004dfaa0, ClipToGround 004dfcb0, Gravity 004dfcc0,
// SetThingType), src/AirUnit.cpp (CAirUnit::Init 00402ad0),
// src/Plane.cpp (CPlane::Init 004d19d0) and src/Dropship.cpp (CDropship::Init 00446d70). Not in Stuart's release.

using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;

using OnslaughtRebuild.Parity.Numerics;

using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CGroundUnit: a unit on the ground, a full move every fourth frame.</summary>
public abstract class GroundUnit(ThingContext context) : Unit(context)
{
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
    public int TiltsWithGround;

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
            Context.Skip("CMCGroundVehicle (motion controller)");
        }

        Guide = new BasicGuide(this, "CGroundVehicleGuide");
        AI = new UnitAI(this, init);
    }
}

/// <summary>CCannon: a turret.</summary>
public sealed class Cannon(ThingContext context) : GroundUnit(context)
{
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
        Context.Skip("CMCCannon (motion controller)");
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

    /// <summary>CBuilding::Move (004178a0): not ported yet (counted; the unit does not move).</summary>
    public override void Move() => Context.Skip("CBuilding::Move (004178a0) (the movement is not ported yet)");

    public float Unknown258 = -1.0f;
    public int Unknown254;
    public int Unknown25C;
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
        Context.Skip("CDestructableSegmentsController (made, then kept only when the mesh has segments)");
        base.Init(init);

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
    /// <summary>CAirUnit::Move (00402fa0): not ported yet (counted; the unit does not move).</summary>
    public override void Move() => Context.Skip("CAirUnit::Move (00402fa0) (the movement is not ported yet)");

    public int TrailLinks;
    public int EngineLinks;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Unknown40000000 | ThingType.AirUnit);

    public override void Init(InitThing init)
    {
        base.Init(init);
        float turn = init.Stats!.TurnRate;
        Unknown12C = new EulerAngles(turn, turn, turn);
        Shields = init.Stats.AirShields;
        MaxShields = Shields;
        ShieldRegeneration = init.Stats.AirShieldRegeneration;
        if (UnitData!.EffectNames[2] is not null)
        {
            TrailLinks = RenderThing!.CountEmitters("Trail");
            Context.Skip("PARTICLE_SET lookup stood in by the effect name (trails)");
        }

        if (UnitData.EffectNames[5] is not null)
        {
            EngineLinks = RenderThing!.CountEmitters("Engine");
            Context.Skip("PARTICLE_SET lookup stood in by the effect name (engines)");
        }

        Context.World.Set50.Add(this);
    }
}

/// <summary>CPlane (a CSmallAirUnit): fighters and trainers.</summary>
public sealed class Plane(ThingContext context) : AirUnit(context)
{
    public int WingState;
    public float LaunchTime;
    public float DeathRoll;
    public int ThrusterLinks;

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
        if (UnitData!.EffectNames[8] is not null)
        {
            ThrusterLinks = RenderThing!.CountEmitters("Engine");
            Context.Skip("PARTICLE_SET lookup stood in by the effect name (afterburner)");
        }

        DeathRoll = (float)(Context.Random.Random() % 65536) / 65536.0f > 0.5f ? 0.8f : -0.8f;
    }
}

/// <summary>CDropship (a CBigAirUnit): the U-17 transporter.</summary>
public sealed class Dropship(ThingContext context) : AirUnit(context)
{
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

        Context.Skip("CMCDropship (motion controller)");
        Guide = new BasicGuide(this, "CDropshipGuide");
        AI = new DropshipAI(this, init);
        if (UnitData.EffectNames[3] is not null)
        {
            Context.Skip("the dropship's thruster count (through its motion controller's pose)");
        }
    }
}
