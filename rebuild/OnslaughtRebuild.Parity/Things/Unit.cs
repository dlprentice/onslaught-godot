// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/unit.h and src/Unit.cpp (CUnit: the constructor 004f7e90, Init 004f86d0, SetThingType
// 004fcdc0, Unknown4fb280 004fb280, Activate 004fd6a0, Deactivate 004fd700). Not in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
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
}

/// <summary>
/// A unit's weapon as far as CUnit::Init sets it up: its definition, mount, and whether the gun's part chain turns a
/// turret (+0x94) or a barrel (+0x98). The weapon itself (CWeapon) is ported with the weapons.
/// </summary>
public sealed class UnitWeaponInstance(int index, int type, int mount)
{
    public int Index { get; } = index;
    public int Type { get; } = type;
    public int Mount { get; } = mount;
    public bool TurnsTurret;
    public bool TurnsBarrel;
}

/// <summary>CUnit: a unit with physics-script data, weapons, AI, life and shields.</summary>
public abstract partial class Unit(ThingContext context, int classId = Oid.Unit) : Actor(context, classId)
{
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

    /// <summary>mUnknown224: one of the weapons turns a barrel.</summary>
    public bool HasBarrel;

    public bool Unknown228Nexus;
    public bool Unknown22CWeakPoint;
    public int DeployState;
    public bool ThrustersActive;
    public readonly List<UnitWeaponInstance> Weapons = [];

    /// <summary>mAI: the unit's AI (set by the subclass's Init; null until then).</summary>
    public object? AI;

    /// <summary>mSquad: the squad the unit belongs to (set before Init by the squad that spawns it).</summary>
    public ComplexThing? Squad;

    /// <summary>mSegments: a building's destructable segments controller (not ported; its presence only).</summary>
    public bool HasSegments;

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
                Context.Skip("UPhysicsManager::SpawnWeapon and CWeapon::Init");
                var weapon = new UnitWeaponInstance(uw.Index, uw.Unknown04, uw.Mount);
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
                            weapon.TurnsTurret = true;
                        }
                        else if (part.Name.StartsWith("barrel", StringComparison.Ordinal)
                                 || part.Name.StartsWith("X1 Barrel", StringComparison.OrdinalIgnoreCase))
                        {
                            HasBarrel = true;
                            weapon.TurnsBarrel = true;
                            Context.Skip("CEulerAngles(FMatrix) pitch of the barrel part (mUnknownF4)");
                        }

                        part = part.Parent;
                    }
                }

                Weapons.Add(weapon);
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

            // The dust and vent links: one per emitter, when the particle set has the effect (stood in for here by the
            // effect being named; the particle set is not loaded yet).
            if (UnitData.EffectNames[6] is not null)
            {
                RenderThing!.CountEmitters("Dust");
                Context.Skip("PARTICLE_SET lookup stood in by the effect name (dust)");
            }

            if (UnitData.EffectNames[7] is not null)
            {
                RenderThing!.CountEmitters("Vent");
                Context.Skip("PARTICLE_SET lookup stood in by the effect name (vent)");
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

        if (HasSegments)
        {
            Context.Skip("CDestructableSegmentsController::Init");
        }

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

        if (AI is not null)
        {
            Context.Skip("CUnit::Unknown4fb280 with an AI target (CalculateDesiredPitch)");
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

    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case UnitEvent.Event4001:
                Unknown4fb280(e as ScheduledEvent);
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }
}
