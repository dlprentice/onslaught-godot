// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/unit.h and src/Unit.cpp (CUnit: the constructor 004f7e90, Init 004f86d0, SetThingType
// 004fcdc0, Unknown4fb280 004fb280, Activate 004fd6a0, Deactivate 004fd700). Not in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>EUnitEvent and the unit's own event numbers.</summary>
public static class UnitEvent
{
    public const int Event4001 = 4001;
    public const int IncomingMissileWarning = 4002;
    public const int Event4003 = 4003;
    public const int Event4004 = 4004;
    public const int Event4005 = 4005;
}

/// <summary>CUnit: a unit with physics-script data, weapons, AI, life and shields.</summary>
public abstract partial class Unit(ThingContext context, int classId = Oid.Unit) : Actor(context, classId)
{
    /// <summary>mUnknown1AC: the weapon's effect link (made at Init).</summary>
    public ParticleEffectLink? Unknown1AC;

    /// <summary>mUnknown1B4: one link per Dust emitter.</summary>
    public readonly List<ParticleEffectLink> DustLinks = [];

    /// <summary>mUnknown1C4: one link per Vent emitter.</summary>
    public readonly List<ParticleEffectLink> VentLinks = [];

    /// <summary>
    /// CUnit::Hit (004fcc30): the squad's script hears of it; touching a unit sets mUnknown248; a falling unit that met
    /// something walkable from above (a contact normal within 0.89 of straight down, or with several contacts the sum of
    /// the first three within 0.69) stands on it.
    /// </summary>
    public override void Hit(Thing other, Collision.CollisionReport? report)
    {
        if (Squad is { } squad && squad.Script is not null && other.IsA(ThingType.ComplexThing))
        {
            squad.Script.Hit((ComplexThing)other);
        }

        if (other.IsA(ThingType.Unit))
        {
            Unknown248 = 1;
        }

        if (other.IsA(ThingType.CanBeWalkedOn) && report is not null && report.Unknown00 != 0 && Gravity() > 0)
        {
            var down = new FVector(0, 0, -1);
            int num = report.NumContacts;
            bool onTop = false;
            for (int i = 0; i < num; i++)
            {
                FVector n = report.Normals[i];
                float alignment = (down.X * n.X) + (down.Y * n.Y) + (down.Z * n.Z);
                if (alignment > 0.89f)
                {
                    onTop = true;
                }
            }

            if (!onTop && num > 1)
            {
                // Retail sums the first three normals whatever the count.
                FVector n = report.Normals[0];
                for (int j = 1; j < 3; j++)
                {
                    n += report.Normals[j];
                }

                n.Normalise();
                if ((down.X * n.X) + (down.Y * n.Y) + (down.Z * n.Z) > 0.69f)
                {
                    onTop = true;
                }
            }

            if (onTop)
            {
                DeclareOnObject(other);
                DeclareOnGround();
            }
        }

        base.Hit(other, report);
    }

    /// <summary>GetMaxVelocity (slot 15, unit.obj's copy 004f84b0).</summary>
    public override float GetMaxVelocity() => 0.2f;


    public UnitData? UnitData;
    public float Life = 1.0f;
    public float Energy;
    public float Shields;
    public float MaxShields;
    public float ShieldRegeneration;
    public bool Active = true;
    public int Allegiance = 2;
    public EulerAngles CurrentOrientation;
    public EulerAngles Unknown120;
    public EulerAngles Unknown12C;
    public FVector Unknown1F8;
    public float Unknown20C;
    public float UnknownEC;
    public float UnknownF4;

    /// <summary>mUnknown220: the barrel part the pitch turns.</summary>
    public MeshPart? Unknown220;

    /// <summary>mUnknown224: one of the weapons turns a barrel.</summary>
    public bool HasBarrel;

    public bool Unknown228Nexus;
    public bool Unknown22CWeakPoint;
    public int DeployState;
    public bool ThrustersActive;

    /// <summary>mThrusters: launch/boost effect links, owned and released by CUnit.</summary>
    public readonly List<ParticleEffectLink> Thrusters = [];

    /// <summary>mAI: the unit's AI (set by the subclass's Init; null until then).</summary>
    public UnitAI? AI;

    /// <summary>mAIState: AI_ON from the constructor; scripts set it (SetAIState).</summary>
    public AIState CurrentAIState = AIState.On;

    /// <summary>CUnit::GetAIState (slot 53, 00405e50).</summary>
    public override AIState GetAIState() => CurrentAIState;

    /// <summary>CUnit::SetAIState (004fdcb0): turning the AI off drops its target and its emitter step's flag.</summary>
    public override void SetAIState(AIState state)
    {
        if (AI is { } ai && state == AIState.Off)
        {
            ai.Target.SetReader(null);
            ai.Unknown10 = 0;
        }

        CurrentAIState = state;
    }

    /// <summary>mGuide: what steers the unit (set by the subclass's Init).</summary>
    public Guide? Guide;

    /// <summary>mSquad: the squad the unit belongs to (null during its own Init when a squad spawns it; SetSquad after).</summary>
    public Squad? Squad;

    /// <summary>mSegments: a building's destructable segments controller (only a building makes one).</summary>
    public DestructableSegments? Segments;

    /// <summary>The segments' motion controller (mMotionController for a segmented building).</summary>
    public SegmentsMotionController? SegmentsController;

    public bool HasSegments => Segments is not null;

    public override void SetThingType(uint t)
    {
        if (UnitData is not null && UnitData.KnockTrees != 0)
        {
            t |= ThingType.CanDestroyTrees;
        }

        base.SetThingType(t | ThingType.Unit);
    }

    public override void Activate()
    {
        if (Active)
        {
            return;
        }

        Active = true;
        if (Squad is not null)
        {
            Context.Skip("CSquad::Activate from CUnit::Activate");
        }
    }

    public override void Deactivate()
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        if (Squad is not null)
        {
            Context.Skip("CSquad::Deactivate from CUnit::Deactivate");
        }
    }

    /// <summary>
    /// CRTMesh::Init's CMesh::GetMesh(name): the loaded mesh whose own name matches, ignoring case. Retail loads the mesh
    /// file from disk when the level's resource file lacks it ("not found in level resource file"); that never happens
    /// for a complete resource file, so it throws here.
    /// </summary>
    protected RenderThing? MakeMeshRenderThing(byte[]? meshName)
    {
        if (meshName is null)
        {
            return null;
        }

        string name = Encoding.ASCII.GetString(meshName);
        if (!Context.Meshes.TryGetValue(name, out MeshData? mesh))
        {
            throw new InvalidOperationException($"mesh '{name}' is not in the level resource file");
        }

        return new RenderThing(Context, mesh, this);
    }

    /// <summary>CUnit::Init.</summary>
    public override void Init(InitThing init)
    {
        // ori.MakeRotation(0, 0, 0), then CEulerAngles(ori) into the current orientation and mUnknown12C: the identity
        // gives -0 yaw and roll (Unit.cpp:88..91).
        FMatrix identity = FMatrix.MakeRotation(0, 0, 0);
        CurrentOrientation = EulerAngles.FromMatrix(identity);
        Unknown12C = EulerAngles.FromMatrix(identity);
        init.InitCst.IgnoreThing = init.SpawnedBy;
        UnitData = init.Stats;
        HasBarrel = false;
        if (UnitData is not null)
        {
            if (UnitData.Behaviour != UnitBehaviour.Building)
            {
                RenderThing = MakeMeshRenderThing(UnitData.Mesh);
            }

            foreach (UnitWeaponReference uw in UnitData.Weapons)
            {
                Weapon? weapon = AddWeapon(uw);
                if (weapon is null)
                {
                    continue;
                }

                if (RenderThing is not null && UnitData.TurretTurnRate > 0)
                {
                    if (uw.Mount < 1 || uw.Mount > 9)
                    {
                        throw new NotSupportedException($"mount {uw.Mount}: retail walks an unset part pointer here");
                    }

                    MeshPart? part = RenderThing.Mesh.GetEmitterSubpart("Gun" + (char)('A' + uw.Mount - 1), 1);
                    while (part is not null)
                    {
                        if (part.Name == "turret" || part.Name.StartsWith("X1 Turret", StringComparison.OrdinalIgnoreCase))
                        {
                            weapon.Unknown94 = 1;
                        }
                        else if (part.Name.StartsWith("barrel", StringComparison.Ordinal)
                                 || part.Name.StartsWith("X1 Barrel", StringComparison.OrdinalIgnoreCase))
                        {
                            HasBarrel = true;
                            weapon.Unknown98 = 1;
                            UnknownF4 = EulerAngles.FromMatrix(part.Orientation).Pitch;
                            Unknown220 = part;
                        }

                        part = part.Parent;
                    }
                }

                AppendWeapon(weapon);
            }

            foreach (UnitWeaponReference sr in UnitData.Spawners)
            {
                Context.Skip("UPhysicsManager::SpawnSpawner (the unit's spawners)");
            }

            Life = UnitData.Life;
        }

        base.Init(init);

        if (init.OrientationType == OrientationType.EulerAngles)
        {
            var ori = new EulerAngles(init.Yaw, init.Pitch, init.Roll);
            CurrentOrientation = ori;
            Unknown120 = ori;
        }

        if (UnitData is not null)
        {
            if (UnitData.Components.Count > 0)
            {
                throw new NotSupportedException("unit components (SpawnCharacter) are not ported yet");
            }

            // The weapon's effect link, and the dust and vent links: one per emitter, when the particle set has the effect.
            if (Context.Particles is { } particles)
            {
                Unknown1AC = new ParticleEffectLink(particles.Manager);
                if (Context.FindParticle(UnitData.EffectNames[6]) is not null)
                {
                    for (int n = RenderThing!.CountEmitters("Dust"); n > 0; n--)
                    {
                        DustLinks.Add(new ParticleEffectLink(particles.Manager));
                    }
                }

                if (Context.FindParticle(UnitData.EffectNames[7]) is not null)
                {
                    for (int n = RenderThing!.CountEmitters("Vent"); n > 0; n--)
                    {
                        VentLinks.Add(new ParticleEffectLink(particles.Manager));
                    }
                }
            }
        }

        Context.World.UnitNB.Add(this);
        Allegiance = init.Allegiance;

        MeshData mesh = RenderThing!.Mesh;
        for (int i = 0; i < mesh.NumParts; i++)
        {
            if (mesh.Parts[i]!.Name == "turret")
            {
                Unknown1F8 = mesh.Parts[2]!.OffsetPos;
            }
        }

        if (Squad is not null)
        {
            Context.Skip("CSquad::Unknown68 (the squad takes its unit)");
        }

        Segments?.Init();

        if (UnitData is not null)
        {
            Context.Skip("CWorldMeshList::MarkUsed");
            int behaviour = (int)UnitData.Behaviour;
            if (Allegiance == 0)
            {
                Context.World.CountersA[behaviour]++;
            }
            else if (Allegiance == 1)
            {
                Context.World.CountersB[behaviour]++;
            }
        }

        Unknown4fb280(null);

        Unknown228Nexus = false;
        Unknown22CWeakPoint = false;
        for (int i = 0; i < mesh.NumParts; i++)
        {
            MeshPart? part = mesh.Parts[i];
            if (part is not null)
            {
                if (string.Equals(part.Name, "nexus", StringComparison.OrdinalIgnoreCase))
                {
                    Unknown228Nexus = true;
                }
                else if (string.Equals(part.Name, "weakpoint", StringComparison.OrdinalIgnoreCase))
                {
                    Unknown22CWeakPoint = true;
                }
            }
        }

        DeployState = 0;
        if (Squad is null)
        {
            if (Allegiance is 1 or 6)
            {
                Context.World.Set30.Append(this);
            }

            if (Allegiance is 0 or 6)
            {
                Context.World.Set20.Append(this);
            }
        }

        Context.Events.AddEvent(UnitEvent.Event4003, this, EventManager.NextFrame);
    }

    /// <summary>
    /// CUnit::Unknown4fb280 (UNIT_EVENT_4001): for a unit with a barrel, its pitch towards the AI's target (or back to
    /// rest), clamped, and the next check within a tenth of a second.
    /// </summary>
    public void Unknown4fb280(ScheduledEvent? e)
    {
        if (!HasBarrel || IsDying)
        {
            return;
        }

        if (AI?.Target.ToRead is { } target)
        {
            if (CurrentWeapon?.Mode?.Round is not null)
            {
                throw new NotSupportedException("CUnit::Unknown4fb280's aim pitch (CWeapon::CalculateDesiredPitch) is not ported");
            }

            Unknown20C = Context.Events.Time + 10.0f;
        }
        else if (Unknown20C < Context.Events.Time)
        {
            UnknownEC = UnknownF4;
        }

        if (UnknownEC > MathF.PI)
        {
            UnknownEC = MathF.PI;
        }
        else if (UnknownEC < -MathF.PI / 2)
        {
            UnknownEC = -MathF.PI / 2;
        }

        Context.Events.AddEvent(UnitEvent.Event4001, this,
            (Context.Random.FloatRandom() * 0.1f) + Context.Events.Time, EventPriority.StartOfFrame, null, e);
    }

    /// <summary>mUnknown218 / mUnknown21C: a countdown UNIT_EVENT_4005 runs down by a step each frame.</summary>
    public int Unknown218;

    public int Unknown21C = 10;

    /// <summary>CUnit::HandleEvent (004f9820).</summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case UnitEvent.Event4001:
                Unknown4fb280(e as ScheduledEvent);
                break;
            case UnitEvent.Event4005:
                if (Unknown218 > 0)
                {
                    Unknown218 -= Unknown21C;
                    Context.Events.AddEvent(UnitEvent.Event4005, this, EventManager.NextFrame, EventPriority.StartOfFrame,
                        null, e as ScheduledEvent);
                }
                else
                {
                    Unknown218 = 0;
                }

                break;
            case UnitEvent.Event4003:
            {
                // mUnknown110: a player's camera is within 50. Compiled order (004f98d5..004f98f6): d = mPos - camera,
                // then (d.Z*d.Z + d.X*d.X) + d.Y*d.Y < 2500, each operation at 24 bits.
                Unknown110 = 0;
                for (int i = 0; i < 2; i++)
                {
                    if (Context.Game?.GetPlayer(i) is not null && Context.Game.GetCurrentCamera(i) is { } camera)
                    {
                        FVector c = camera.GetPos();
                        float dx = Pos.X - c.X;
                        float dy = Pos.Y - c.Y;
                        float dz = Pos.Z - c.Z;
                        if ((dz * dz) + (dx * dx) + (dy * dy) < 2500.0f)
                        {
                            Unknown110 = 1;
                        }
                    }
                }

                if (Context.Game is null)
                {
                    Context.Skip("CUnit UNIT_EVENT_4003: no game, so no players' cameras");
                }

                float delay = Context.Random.FloatRandom() + 3.0f;
                Context.Events.AddEventIn(delay, UnitEvent.Event4003, this, EventPriority.StartOfFrame, null,
                    e as ScheduledEvent);
                break;
            }

            case UnitEvent.Event4004:
                Context.Skip("CUnit::SpawnExplosion");
                AddShutdownEvent();
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }
}
