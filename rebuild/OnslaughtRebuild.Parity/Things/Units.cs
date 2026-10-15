// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/unit.h (the unit classes' SetThingType and GetMoveMultiplier), src/GroundUnit.cpp
// (CGroundUnit::Init 0047c730), src/GroundVehicle.cpp (CGroundVehicle::Init 0047cfd0), src/Cannon.cpp (CCannon::Init
// 0041b1a0), src/Building.cpp (CBuilding::Init 00417190, InitRenderThing 004176c0, SetThingType 00417660),
// src/SimpleBuilding.cpp (CSimpleBuilding::Init 004dfa40, SetThingType), src/AirUnit.cpp (CAirUnit::Init 00402ad0),
// src/Plane.cpp (CPlane::Init 004d19d0) and src/Dropship.cpp (CDropship::Init 00446d70). Not in Stuart's release.

using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CGroundUnit: a unit on the ground, a full move every fourth frame.</summary>
public abstract class GroundUnit(ThingContext context) : Unit(context)
{
    public float BobPhase;
    public float Bob;
    public float OldBob;
    public int ThrusterLinks;

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

        Context.Skip("CGroundVehicleGuide");
        Context.Skip("CUnitAI constructor (ground vehicle)");
    }
}

/// <summary>CCannon: a turret.</summary>
public sealed class Cannon(ThingContext context) : GroundUnit(context)
{
    public int State;
    public bool AboveWater;
    public float LastTargetTime;

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

        Context.Skip("CTerrainGuide");
        Context.Skip("CUnitAI constructor (cannon)");
        Unknown12C = new EulerAngles(0, 0, 0);
        Context.Skip("CMCCannon (motion controller)");
        UnknownF4 = 0;
        LastTargetTime = 0;
        Context.Skip("CWorld::AddToOccupancy");
        AboveWater = !(Context.WaterLevel > Pos.Z);
    }
}

/// <summary>CBuilding: a building, with its own render thing (mesh and rubble) and destructable segments.</summary>
public sealed class Building(ThingContext context) : Unit(context)
{
    public float Unknown258 = -1.0f;
    public int Unknown254;
    public int Unknown25C;
    public bool Unknown260;
    public int Unknown264;
    public float Unknown268;

    public override void SetThingType(uint t) => base.SetThingType(
        t | ThingType.Unknown40000000 | ThingType.CanBeWalkedOn | ThingType.Building | ThingType.Stationary);

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
        Context.Skip("CBuilding::CreateRepairPadAI");
        SetAnimMode(AnimModeByName("Idle"), true, true);
        Context.Skip("CWorld::AddToOccupancy");
    }
}

/// <summary>CSimpleBuilding: a building without segments or AI.</summary>
public sealed class SimpleBuilding(ThingContext context) : Unit(context)
{
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
        Context.Skip("CWorld::AddToOccupancy");
    }
}

/// <summary>CAirUnit: a flying unit.</summary>
public abstract class AirUnit(ThingContext context) : Unit(context)
{
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
        Context.Skip("CAirGuide");
        // CPlaneAI's mode is 2 (launched) only for a plane a unit spawned with an emitter to follow (CUnitAI::Unknown7);
        // every plane the world file places starts in mode 0 or 1. Spawned planes take the launch branch once spawners
        // and the AI are ported.
        Context.Skip("CPlaneAI constructor (a placed plane: not launched)");
        SetAnimMode(AnimModeByName("launch"), true, true);
        WingState = 1;
        LaunchTime = 0;
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

        Context.Skip("CMCDropship, CDropshipGuide, CDropshipAI");
        if (UnitData.EffectNames[3] is not null)
        {
            Context.Skip("the dropship's thruster count (through its motion controller's pose)");
        }
    }
}
