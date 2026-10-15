// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/squad.h, src/squad.cpp (CSquad: the constructor 004e5da0, SetAIState 004e5e00, Init
// 004e5e70, IncreaseThingCounter 004e6200, Shutdown 004e65b0, HandleEvent 004e65e0, Unknown4e6610, Unknown4e6640,
// Unknown4e6660, Unknown4e6680), src/SquadNormal.cpp (CNormalSquad 004e6870..004e9800 and CNormalSquadMember),
// src/SquadRelaxed.cpp (CRelaxedSquad 004ea440..004ea9f0), src/NormalSquadTargetSelector.cpp (Unknown477cb0) and
// src/WorldPhysicsManager.cpp (UPhysicsManager::SpawnSquad 0050f4b0, IsSquadBehaviour 0050f680). Not in Stuart's
// release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// Every CUnit member the squads, their target selector, the influence map and the fear grid call, by retail name. A
/// unit is a <see cref="ComplexThing"/> that implements this; the thing-level calls (position, type, Teleport,
/// GetRadius, GetAIState, GoToPoint ...) go through the thing itself.
/// </summary>
public interface ISquadUnit : IThreatSource
{
    /// <summary>GetSquad: mSquad.</summary>
    Squad? GetSquad();

    /// <summary>SetSquad (004fe500).</summary>
    void SetSquad(Squad? squad);

    /// <summary>GetOccupancyGrid (004fd380): the navigation grid for the unit's kind, or null.</summary>
    IOccupancyGrid? GetOccupancyGrid();

    /// <summary>Unknown111 (slot 111, 004fe5c0): the squad's speed is this times 0.06.</summary>
    float Unknown111();

    /// <summary>IsAThreat (slot 89, 004fd440).</summary>
    bool IsAThreat();

    /// <summary>GetStealth (slot 91).</summary>
    float GetStealth();

    /// <summary>GetAllegiance: mAllegiance.</summary>
    int GetAllegiance();

    /// <summary>SetAllegiance.</summary>
    void SetAllegiance(int allegiance);

    /// <summary>Unknown4fb3d0: whether the unit may attack the target.</summary>
    bool Unknown4fb3d0(ISquadUnit target);

    /// <summary>Unknown4fb840: picks the weapon or spawner to use on the target.</summary>
    void Unknown4fb840(Thing target);

    /// <summary>GetMinRange (004fb780) for the chosen weapon.</summary>
    float GetMinRange(FVector target);

    /// <summary>GetMaxRange (004fb7e0).</summary>
    float GetMaxRange(FVector target);

    /// <summary>Unknown4fb5a0: a line-of-fire test on the target.</summary>
    bool Unknown4fb5a0(ISquadUnit target);

    /// <summary>AnyWeaponUnknown94 (004fd570).</summary>
    bool AnyWeaponUnknown94();

    /// <summary>GuideUnknown7 (004fcec0).</summary>
    void GuideUnknown7(FVector pos);

    /// <summary><c>if (mAI) mAI-&gt;Unknown4ffdd0(target, FALSE)</c>.</summary>
    void SetAITarget(ISquadUnit? target);

    /// <summary>GetCurrentTarget (slot 81): the AI's target.</summary>
    ISquadUnit? GetCurrentTarget();

    /// <summary>IsOnGround (slot 67).</summary>
    bool IsOnGround();
}

/// <summary>The COccupancyGrid calls a squad makes.</summary>
public interface IOccupancyGrid
{
    /// <summary>UnknownCell: whether the cell under the position is passable.</summary>
    bool UnknownCell(FVector pos);

    /// <summary>Unknown4bc2e0: builds a path from one point to another into the squad's path.</summary>
    void Unknown4bc2e0(FVector from, FVector to, int a, SquadPath path);
}

/// <summary>
/// CNormalSquadUnknownD4: the path a normal squad follows, cells as half-coordinates in two byte arrays (each starting
/// with room for one and doubling).
/// </summary>
public sealed class SquadPath
{
    public int Unknown00 = -1;
    public int Unknown04 = -1;
    public int Unknown08;

    /// <summary>mUnknown0C: the cells in the path.</summary>
    public int Unknown0C;

    public byte[] Unknown10 = new byte[1];
    public byte[] Unknown18 = new byte[1];

    /// <summary>The last cell's centre in world units; with no cells the coordinates are left as they were.</summary>
    public void GetCurrent(ref int x, ref int y)
    {
        if (Unknown0C > 0)
        {
            x = (Unknown10[Unknown0C - 1] * 2) + 1;
            y = (Unknown18[Unknown0C - 1] * 2) + 1;
        }
    }

    /// <summary>AddCell (inlined by COccupancyGrid::BuildPath): a cell by its centre, the arrays doubling when full.</summary>
    public void AddCell(int x, int y)
    {
        if (Unknown0C >= Unknown10.Length)
        {
            Array.Resize(ref Unknown10, Unknown10.Length * 2);
            Array.Resize(ref Unknown18, Unknown18.Length * 2);
        }

        Unknown10[Unknown0C] = unchecked((byte)(x >> 1));
        Unknown18[Unknown0C] = unchecked((byte)(y >> 1));
        Unknown0C++;
    }
}

/// <summary>The squad events.</summary>
public static class SquadEvent
{
    /// <summary>CNormalSquad: choose a target (Unknown4e8100), every 2 to 4 seconds.</summary>
    public const int ChooseTarget = 4000;

    /// <summary>CNormalSquad: drop dead members and reassign places (Unknown4e83b0), every 1 to 2 seconds.</summary>
    public const int CheckMembers = 4001;

    /// <summary>CSquad: the squad's update (slot 66).</summary>
    public const int Update = 4002;
}

/// <summary>
/// CSquad (class id 28): a group of units the world file places as one thing. Its Init spawns the members by the
/// unit name, initialises each and places it with slot 67; the subclass decides the formation and the behaviour.
/// </summary>
public abstract class Squad : ComplexThing, IThreatSource
{
    public const int ClassIdValue = Oid.Squad;

    protected Squad(ThingContext context)
        : base(context, ClassIdValue)
    {
        Assembling = false;
        SpawnedFromUnitRecord = false;
    }

    /// <summary>
    /// UPhysicsManager::SpawnThing for the members (by unit index; null where retail returns NULL). The world wiring
    /// sets it to the unit factory.
    /// </summary>
    public Func<int, Thing?>? SpawnUnit { get; set; }

    /// <summary>mUnknown7C: the squad's allegiance.</summary>
    public int Unknown7C;

    /// <summary>mUnknown80: set while a squad is being assembled (IScript::SpawnThing, Unknown4e91f0's split).</summary>
    public bool Assembling;

    /// <summary>
    /// mUnknown84: the squad stands for one unit record of the world file (CWorld::LoadWorld, world.cpp:548) or a
    /// script's spawn; it follows its unit's AI state and allegiance.
    /// </summary>
    public bool SpawnedFromUnitRecord;

    /// <summary>mUnknown88: until when the squad was shot at (Unknown4e6660: event time + 5).</summary>
    public float Unknown88;

    /// <summary>mUnknown8C: where SetAIState(AI_DEFENSIVE) anchored it.</summary>
    public FVector Unknown8C;

    /// <summary>mUnknown9C: CSquadInitThing::mMode (1 defensive).</summary>
    public int Unknown9C;

    /// <summary>mUnknownA0: the members' unit definition.</summary>
    public UnitData? UnknownA0;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Unknown20000000);

    /// <summary>CSquad::SetAIState (004e5e00).</summary>
    public override void SetAIState(AIState state)
    {
        if (state == AIState.Defensive)
        {
            Unknown9C = 1;
            Unknown8C = Pos;
        }
        else if (state == AIState.Normal)
        {
            Unknown9C = 0;
        }
    }

    /// <summary>
    /// CSquad::Init: the squad is outside the map-who and collides with nothing; it takes the allegiance, the unit
    /// definition and the position, spawns and initialises its members (each placed by slot 67 at its height), then
    /// CComplexThing::Init and the world's squad list.
    /// </summary>
    public override void Init(InitThing init)
    {
        Unknown88 = 0;
        Unknown9C = init.Mode;
        init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        Flags &= ~ThingFlags.InMapWho;
        Unknown7C = init.Allegiance;
        UnknownA0 = Context.Physics.GetUnitData(init.UnitName);
        Pos = init.Pos;
        InitThing unitInit = MemberInit(init);
        int amount = init.Amount;
        for (int i = 0; i < amount; i++)
        {
            Thing? thing = Spawn(init.UnitName);
            if (thing is not null)
            {
                var memberInit = new InitThing { Kind = InitThingRecord.InitKind.Unit };
                memberInit.Copy(unitInit);
                memberInit.Stats = UnknownA0;
                thing.Init(memberInit);
                FVector pos = Unknown67((ISquadUnit)thing, i, init.Amount);
                pos.Z = thing.Pos.Z;
                thing.Teleport(pos);
            }
        }

        base.Init(init);
        Unknown8C = Pos;
        Context.World.SquadNB.Append(this);
    }

    /// <summary>
    /// The members' CUnitInitThing: the squad's placement; the script and name stay with the squad (cleared on the
    /// members) unless the squad is relaxed, stands for a unit record or attaches scripts to its units, when they go to
    /// the members (and are cleared on the squad's init).
    /// </summary>
    private InitThing MemberInit(InitThing init)
    {
        var unitInit = new InitThing
        {
            Kind = InitThingRecord.InitKind.Unit,
            Pos = init.Pos,
            OrientationType = init.OrientationType,
            Yaw = init.Yaw,
            Pitch = init.Pitch,
            Orientation = init.Orientation,
            Roll = init.Roll,
            MeshNo = init.MeshNo,
            Allegiance = init.Allegiance,
            Target = init.Target,
            TargetThing = init.TargetThing,
            Script = init.Script,
            Name = init.Name,
            SpawnScript = init.SpawnScript,
            Active = init.Active,
            AttachScriptsToUnits = init.AttachScriptsToUnits,
        };
        if (Unknown83() == 0 && !SpawnedFromUnitRecord && init.AttachScriptsToUnits == 0)
        {
            unitInit.Script = [];
            unitInit.Name = [];
        }
        else
        {
            init.Script = [];
            init.Name = [];
        }

        return unitInit;
    }

    private Thing? Spawn(byte[] unitName)
    {
        Func<int, Thing?> spawn = SpawnUnit
            ?? throw new InvalidOperationException("a squad that spawns units needs SpawnUnit (UPhysicsManager::SpawnThing)");
        return spawn(Context.Physics.GetUnitIndex(unitName));
    }

    /// <summary>
    /// CSquad::IncreaseThingCounter: the members are spawned and each counts itself (thing numbers advance), then
    /// they are deleted.
    /// </summary>
    public override void IncreaseThingCounter(InitThing init)
    {
        InitThing unitInit = MemberInit(init);
        int amount = init.Amount;
        for (int i = 0; i < amount; i++)
        {
            Thing? thing = Spawn(init.UnitName);
            if (thing is not null)
            {
                var memberInit = new InitThing { Kind = InitThingRecord.InitKind.Unit };
                memberInit.Copy(unitInit);
                memberInit.Stats = Context.Physics.GetUnitData(init.UnitName);
                thing.IncreaseThingCounter(memberInit);
            }
        }
    }

    /// <summary>CSquad::Shutdown: off the side lists and the squad list, then CComplexThing's.</summary>
    public override void Shutdown()
    {
        Context.World.Set30.Remove(this);
        Context.World.Set20.Remove(this);
        Context.World.SquadNB.Remove(this);
        base.Shutdown();
    }

    /// <summary>CSquad::HandleEvent: 4002 is the update (slot 66).</summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case SquadEvent.Update:
                Unknown66((ScheduledEvent)e);
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>Unknown4e6610: shot at recently and Forseti.</summary>
    public bool Unknown4e6610() => Unknown88 > Context.Events.Time && Unknown7C == 0;

    /// <summary>Unknown4e6640: a member touched something; the squad's script hears of it.</summary>
    public void Unknown4e6640(ISquadUnit unit, Thing other)
    {
        if (Script is not null && other.IsA(ThingType.ComplexThing))
        {
            Context.Skip("IScript::Hit");
        }
    }

    /// <summary>Unknown4e6660: a member was shot at.</summary>
    public void Unknown4e6660(ISquadUnit attacker) => Unknown88 = Context.Events.Time + 5.0f;

    /// <summary>Unknown4e6680: whether the allegiance is this squad's enemy (Forseti 0, Muspell 1, independent 2).</summary>
    public bool Unknown4e6680(int allegiance) => allegiance switch
    {
        0 => Unknown7C is 1 or 2,
        1 => Unknown7C is 0 or 2,
        2 => Unknown7C is 1 or 0,
        _ => false,
    };

    /// <summary>Slot 66: the update (CSquad's does nothing).</summary>
    public virtual void Unknown66(ScheduledEvent? e)
    {
    }

    /// <summary>Slot 67: takes a unit in and returns where it stands (b members, this one the a-th).</summary>
    public abstract FVector Unknown67(ISquadUnit unit, int a, int b);

    /// <summary>Slot 68: copies a unit's properties (CUnit::Init calls it when the unit already has a squad).</summary>
    public abstract void Unknown68(ISquadUnit unit);

    public abstract bool Unknown69();

    /// <summary>Slot 70: whether the squad is finished (no live member, not being assembled).</summary>
    public abstract bool Unknown70();

    /// <summary>Slot 71: the squad's position.</summary>
    public virtual FVector Unknown71() => Pos;

    /// <summary>Slot 72: the members' centre.</summary>
    public abstract FVector Unknown72();

    /// <summary>Slot 73: the first live member.</summary>
    public abstract ISquadUnit? Unknown73();

    public abstract ISquadUnit? Unknown74();

    /// <summary>Slot 75: the live member nearest a point.</summary>
    public abstract ISquadUnit? Unknown75(FVector point);

    public abstract int Unknown76();

    /// <summary>Slot 77: the target.</summary>
    public abstract Thing? Unknown77();

    public abstract float Unknown78();

    /// <summary>Slot 79: the members' threat (the influence map reaches it as CUnit::GetThreat).</summary>
    public abstract float Unknown79();

    /// <summary>Slot 80: the members' importance.</summary>
    public abstract float Unknown80();

    /// <summary>Slot 81: a new set of the live members.</summary>
    public abstract SPtrSet<Thing> GetUnits();

    public abstract bool Unknown82();

    public abstract int Unknown83();

    public abstract float Unknown84();

    public abstract void Unknown85(Thing? target);

    /// <summary>Slot 86: every member shuts down.</summary>
    public abstract void Unknown86();

    float IThreatSource.GetThreat() => Unknown79();

    float IThreatSource.GetImportance() => Unknown80();

    // Maths the squads share.
    internal static float MagnitudeXYSq(FVector v) => (v.X * v.X) + (v.Y * v.Y);

    /// <summary>
    /// FVector::Azimuth: -atan2f(X, Y). The x87 result rounds to 24 bits; here the double atan2 is rounded to float
    /// (not yet checked against an fpatan oracle).
    /// </summary>
    internal static float Azimuth(FVector v) => -(float)Math.Atan2(v.X, v.Y);

    /// <summary>FMatrix(azimuth, 0, 0): with no pitch or roll the rotation's products are exact.</summary>
    internal static FMatrix Heading(float azimuth) => FMatrix.FromYawPitchRoll(azimuth, 0, 0);

    internal static ComplexThing ThingOf(ISquadUnit unit) => (ComplexThing)unit;

    /// <summary>UPhysicsManager::SpawnSquad: the squad a unit behaviour gets, or null for the kinds that go alone.</summary>
    public static Squad? SpawnSquad(ThingContext context, UnitBehaviour behaviour) => behaviour switch
    {
        UnitBehaviour.Mech or UnitBehaviour.Infantry => new RelaxedSquad(context),
        _ when !IsSquadBehaviour(behaviour) || behaviour == UnitBehaviour.SimpleBuilding => null,
        _ => new NormalSquad(context),
    };

    /// <summary>UPhysicsManager::IsSquadBehaviour (the simple building is not on its list, unlike SpawnSquad's).</summary>
    public static bool IsSquadBehaviour(UnitBehaviour behaviour) => behaviour switch
    {
        UnitBehaviour.Turret or UnitBehaviour.Boat or UnitBehaviour.Carrier or UnitBehaviour.Building
            or UnitBehaviour.Fighter or UnitBehaviour.Bomber or UnitBehaviour.GroundAttackAircraft or UnitBehaviour.Jeep
            or UnitBehaviour.Dropship or UnitBehaviour.Mine or UnitBehaviour.HiveBoss or UnitBehaviour.Submarine
            or UnitBehaviour.DiveBomber or UnitBehaviour.ThunderHead or UnitBehaviour.Carver or UnitBehaviour.GillM
            or UnitBehaviour.Sentinel or UnitBehaviour.Fenrir or UnitBehaviour.WarspiteDome or UnitBehaviour.Pod => false,
        _ => true,
    };

    /// <summary>
    /// CNormalSquadTargetSelector::Unknown477cb0 (INFLUENCE_BUFFER_A for Forseti, B for Muspell; the selector keeps no
    /// state): the best enemy unit by distance, attack priority and range, as its squad when it has one.
    /// </summary>
    public static Thing? SelectTarget(Squad squad)
    {
        WorldRegistry world = squad.Context.World;
        SPtrSet<Thing> things = world.Things;
        if (squad.Unknown7C == 1)
        {
            things = world.Set20;
        }
        else if (squad.Unknown7C == 0)
        {
            things = world.Set30;
        }

        ISquadUnit? best = null;
        float bestScore = -1.0f;
        FVector position = squad.Unknown72();
        ISquadUnit? first = squad.Unknown73();
        if (first is not null)
        {
            var it = new ListIterator<Thing>(things);
            for (Thing? thing = it.First(); thing is not null; thing = it.Next())
            {
                ISquadUnit? unit = null;
                if ((thing.ThingTypeBits & ThingType.Unknown20000000) != 0)
                {
                    unit = ((Squad)thing).Unknown75(position);
                }
                else if ((thing.ThingTypeBits & ThingType.Unit) != 0)
                {
                    unit = (ISquadUnit)thing;
                }

                if (unit is null)
                {
                    continue;
                }

                ComplexThing u = ThingOf(unit);
                FVector delta = u.Pos - position;
                float distanceSq = delta.MagnitudeSq();
                bool visible = true;
                UnitData data = squad.UnknownA0 ?? throw new InvalidOperationException("a squad with members has no unit data");
                if (unit.GetStealth() != 0)
                {
                    float range = (1.0f - (unit.GetStealth() / 100.0f)) * data.MaxTargetRange;
                    if (distanceSq > range * range)
                    {
                        visible = false;
                    }
                }

                if (!squad.Unknown4e6680(unit.GetAllegiance()) || !first.Unknown4fb3d0(unit) || !visible)
                {
                    continue;
                }

                float priority = 0;
                uint type = u.ThingTypeBits;
                if ((type & ThingType.Vehicle) != 0)
                {
                    priority = data.AttackPriority[1];
                }
                else if ((type & ThingType.Infantry) != 0)
                {
                    priority = data.AttackPriority[4];
                }
                else if ((type & ThingType.AirUnit) != 0)
                {
                    priority = data.AttackPriority[5];
                }
                else if ((type & ThingType.Component) != 0)
                {
                    priority = data.AttackPriority[6];
                }
                else if ((type & ThingType.Emplacement) != 0)
                {
                    priority = data.AttackPriority[0];
                }
                else if ((type & ThingType.Building) != 0)
                {
                    priority = data.AttackPriority[2];
                }
                else if ((type & ThingType.Naval) != 0)
                {
                    priority = data.AttackPriority[3];
                }

                float distance = MathF.Sqrt(distanceSq);
                float score = 1000.0f - distance + (priority * 10.0f);
                first.Unknown4fb840(u);
                if (first.GetMinRange(u.Pos) <= distance)
                {
                    score += first.GetMaxRange(u.Pos) < distance ? 10000.0f : 100000.0f;
                }

                if (!unit.IsAThreat() && !u.IsObjective())
                {
                    score = 0;
                }

                if (squad.Unknown9C == 1 && MagnitudeXYSq(u.Pos - squad.Unknown8C) > 5625.0f)
                {
                    score = -1.0f;
                }

                // Strict: the first admitted candidate keeps a tie.
                if (score > bestScore)
                {
                    bestScore = score;
                    best = unit;
                }
            }
        }

        if (best is null)
        {
            return null;
        }

        return (Thing?)best.GetSquad() ?? ThingOf(best);
    }
}

/// <summary>CNormalSquadMember: a member's unit and its place in the formation (relative to the squad).</summary>
public sealed class NormalSquadMember
{
    public NormalSquadMember(ISquadUnit unit, NormalSquad squad)
    {
        Unit.SetReader((ComplexThing)unit);
        Unknown04 = 0;
        Unknown08 = 0;
        Squad = squad;
        Unknown0C = squad.Context.Events.Time;
    }

    /// <summary>mUnit.</summary>
    public ActiveReader<Thing> Unit { get; } = new();

    public ISquadUnit? UnitOrNull => (ISquadUnit?)Unit.ToRead;

    /// <summary>mUnknown04, mUnknown08: the place across and along the formation.</summary>
    public float Unknown04;
    public float Unknown08;

    /// <summary>mUnknown0C: when it joined.</summary>
    public float Unknown0C;

    public NormalSquad Squad { get; }

    /// <summary>The place in world space: the squad's position plus its orientation times (04, 08, 0).</summary>
    public FVector GetPos() => Squad.Pos + Squad.Orientation.Transform(new FVector(Unknown04, Unknown08, 0));

    public void TeleportToPosition()
    {
        if (Unit.ToRead is { } unit)
        {
            unit.Teleport(GetPos());
            FVector p = unit.Pos;
            p.Z = Squad.Context.Map.Collide(p);
            if (p.Z < Squad.Pos.Z)
            {
                unit.Teleport(p);
            }
        }
    }

    public float GetRadius() => Squad.Unknown78();

    public void MoveToPosition()
    {
        if (Unit.ToRead is ComplexThing unit)
        {
            unit.GoToPoint(GetPos());
        }
    }

    public bool IsOutOfPosition()
    {
        if (Unit.ToRead is { } unit)
        {
            FVector delta = unit.Pos - GetPos();
            float dist = delta.MagnitudeXY();
            if (GetRadius() * 1.5f < dist)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasWeapon() => UnitOrNull is { } unit && unit.AnyWeaponUnknown94();

    public void SetTarget(ISquadUnit? target)
    {
        if (UnitOrNull is { } unit)
        {
            if (target is not null)
            {
                unit.GuideUnknown7(((ComplexThing)target).Pos);
            }

            unit.SetAITarget(target);
        }
    }

    /// <summary>Unknown4e97e0: swaps units with another member when that brings both closer to their places.</summary>
    public bool Unknown4e97e0(NormalSquadMember other)
    {
        Thing mine = Unit.ToRead!;
        Thing theirs = other.Unit.ToRead!;
        FVector from = GetPos() - mine.Pos;
        FVector otherFrom = other.GetPos() - theirs.Pos;
        FVector to = GetPos() - theirs.Pos;
        FVector otherTo = other.GetPos() - mine.Pos;
        float a = from.MagnitudeXY() + 1.0f;
        float b = otherFrom.MagnitudeXY() + 1.0f;
        float c = to.MagnitudeXY() + 1.0f;
        float d = otherTo.MagnitudeXY() + 1.0f;
        if ((c * c) + (d * d) < (a * a) + (b * b))
        {
            Unit.SetReader(theirs);
            other.Unit.SetReader(mine);
            return true;
        }

        return false;
    }

    /// <summary>IsPositionOpen: a missing unit or grid passes; otherwise the place's cell must be passable.</summary>
    public bool IsPositionOpen()
    {
        if (Unit.ToRead is not null && Squad.UnknownD0 is { } grid && !grid.UnknownCell(GetPos()))
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// CNormalSquad: a formation that picks a target every few seconds, moves as one along an occupancy-grid path, keeps
/// clear of other squads and of the side's fear grid, and merges with or splits from others.
/// </summary>
public sealed class NormalSquad : Squad
{
    public NormalSquad(ThingContext context)
        : base(context)
    {
        Members = new SPtrSet<NormalSquadMember>(context.Pool);
        UnknownB4 = 0;
        Unknown108 = 0;
        UnknownBC = 0;
        UnknownD0 = null;
        Unknown118 = 0;
        Unknown114 = true;
    }

    /// <summary>mMembers.</summary>
    public SPtrSet<NormalSquadMember> Members { get; }

    /// <summary>mUnknownB4: the member count slot 67 and the removals keep.</summary>
    public int UnknownB4;

    /// <summary>mUnknownB8: the authored amount.</summary>
    public int UnknownB8;

    /// <summary>mUnknownBC: the formation (0 none, 1 marching, 2 attacking).</summary>
    public int UnknownBC;

    /// <summary>mUnknownC0: a member's thing type.</summary>
    public uint UnknownC0;

    /// <summary>mUnknownC4: the target, a unit or another squad.</summary>
    public ActiveReader<Thing> UnknownC4 { get; } = new();

    /// <summary>mUnknownC8: the squad it last moved clear of.</summary>
    public ActiveReader<Thing> UnknownC8 { get; } = new();

    /// <summary>mUnknownCC: the target was set from outside (slot 85) and is kept.</summary>
    public int UnknownCC;

    /// <summary>mUnknownD0: the members' occupancy grid.</summary>
    public IOccupancyGrid? UnknownD0;

    /// <summary>mUnknownD4: the path.</summary>
    public SquadPath UnknownD4 { get; } = new();

    /// <summary>mUnknownF4: where it is going.</summary>
    public FVector UnknownF4;

    /// <summary>mUnknown104: a member's radius.</summary>
    public float Unknown104;

    /// <summary>mUnknown108: the speed this update.</summary>
    public float Unknown108;

    /// <summary>mUnknown10C: the members' speed (Unknown111 * 0.06).</summary>
    public float Unknown10C;

    /// <summary>mUnknown110: moving this update.</summary>
    public bool Unknown110;

    /// <summary>mUnknown114: active.</summary>
    public bool Unknown114;

    /// <summary>mUnknown118: attack until this time.</summary>
    public float Unknown118;

    /// <summary>mUnknown11C: the last time the members were in place.</summary>
    public float Unknown11C;

    /// <summary>mUnknown120: the AI state.</summary>
    public AIState Unknown120;

    /// <summary>mUnknown124: the members' centre.</summary>
    public FVector Unknown124;

    /// <summary>mUnknown134: the position at the last update.</summary>
    public FVector Unknown134;

    public override AIState GetAIState() => Unknown120;

    public override void SetAIState(AIState state)
    {
        if (state == AIState.Off)
        {
            UnknownC4.SetReader(null);
            Unknown120 = AIState.Off;
        }
        else if (state == AIState.Defensive)
        {
            Unknown9C = 1;
            Unknown8C = Pos;
            UnknownF4 = Pos;
        }
        else if (state == AIState.Normal)
        {
            Unknown9C = 0;
        }
        else
        {
            Unknown120 = state;
        }
    }

    /// <summary>
    /// CNormalSquad::Init: the amount, the position on the ground, AI_ON, then CSquad::Init (which spawns the members);
    /// the side lists; then the first target choice, member check and update, each scheduling its event (three draws).
    /// </summary>
    public override void Init(InitThing init)
    {
        UnknownB8 = init.Amount;
        Unknown104 = 0;
        Unknown10C = 0;
        UnknownC0 = 0;
        UnknownBC = 0;
        Pos = init.Pos;
        Pos.Z = Context.Map.Collide(Pos);
        Unknown120 = AIState.On;
        Unknown134 = Pos;
        Unknown124 = Pos;
        base.Init(init);
        if (Unknown7C is 1 or 6)
        {
            Context.World.Set30.Append(this);
        }

        if (Unknown7C is 0 or 6)
        {
            Context.World.Set20.Append(this);
        }

        UnknownCC = 0;
        UnknownF4 = Pos;
        Unknown11C = Context.Events.Time;
        Unknown4e8100();
        Unknown4e83b0(true);
        Unknown66(null);
    }

    public override void Shutdown()
    {
        NormalSquadMember? member;
        while ((member = Members.First()) is not null)
        {
            Members.Remove(member);
            UnknownB4--;
        }

        UnknownD0 = null;
        base.Shutdown();
    }

    public override void Unknown86()
    {
        NormalSquadMember? member;
        while ((member = Members.First()) is not null)
        {
            member.Unit.ToRead?.Shutdown();
            Members.Remove(member);
            UnknownB4--;
        }
    }

    /// <summary>
    /// Slot 67: a unit with no squad joins. The squad takes its radius, grid, speed and type; the place is the a-th of
    /// b in a square block (or the next one, for b = 0), centred across; then the formation is redone.
    /// </summary>
    public override FVector Unknown67(ISquadUnit unit, int a, int b)
    {
        if (unit.GetSquad() is null)
        {
            ComplexThing thing = ThingOf(unit);
            Unknown104 = thing.GetRadius();
            UnknownD0 = unit.GetOccupancyGrid();
            Unknown10C = unit.Unknown111() * 0.06000000238418579f;
            UnknownC0 = thing.ThingTypeBits;
            if (b == 0)
            {
                a = UnknownB4;
                b = UnknownB4 + 1;
            }

            int width = (int)Math.Ceiling(Math.Sqrt(b));
            int row = a / width;
            int column = a - (row * width);
            float x = ((float)(width / 2) * Unknown104 * 2.0f) - ((float)column * Unknown104 * 2.0f);
            float y = (float)row * Unknown104 * -2.0f;
            var member = new NormalSquadMember(unit, this);
            UnknownB4++;
            Members.Add(member);
            unit.SetSquad(this);
            Unknown4e8730();
            return new FVector(Pos.X + x, Pos.Y + y, Pos.Z);
        }

        return new FVector(0, 0, 0);
    }

    /// <summary>Unknown4e6f70: a member of this squad leaves it.</summary>
    public void Unknown4e6f70(ISquadUnit? unit)
    {
        if (unit is not null && unit.GetSquad() == this)
        {
            var it = new ListIterator<NormalSquadMember>(Members);
            for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
            {
                if (ReferenceEquals(member.Unit.ToRead, unit))
                {
                    Members.Remove(member);
                    unit.SetSquad(null);
                    UnknownB4--;
                    return;
                }
            }
        }
    }

    public override void Unknown68(ISquadUnit unit)
    {
        ComplexThing thing = ThingOf(unit);
        Unknown104 = thing.GetBoundingRadius();
        UnknownD0 = unit.GetOccupancyGrid();
        Unknown10C = unit.Unknown111() * 0.06000000238418579f;
        UnknownC0 = thing.ThingTypeBits;
    }

    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case SquadEvent.ChooseTarget:
                Unknown4e8100();
                break;
            case SquadEvent.CheckMembers:
                Unknown4e83b0(true);
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>
    /// Slot 66 (004e7070): the update; next frame when it reports a stop, otherwise FloatRandom() * 0.01 + 0.99 seconds
    /// on (a draw), re-using the event; a finished squad shuts down.
    /// </summary>
    public override void Unknown66(ScheduledEvent? e)
    {
        if (Unknown4e7110(e))
        {
            Context.Events.AddEvent(SquadEvent.Update, this, EventManager.NextFrame, EventPriority.StartOfFrame, null, e);
        }
        else
        {
            Context.Events.AddEvent(SquadEvent.Update, this,
                Context.Events.Time + ((Context.Random.FloatRandom() * 0.01f) + 0.99f), EventPriority.StartOfFrame, null, e);
        }

        if (Unknown70())
        {
            AddShutdownEvent();
        }
    }

    private FearGrid? FearGridFor(int allegiance)
    {
        if (Context.Game is null)
        {
            Context.Skip("CNormalSquad: GAME's fear grids (no game attached)");
            return null;
        }

        return allegiance switch
        {
            0 => Context.Game.ForsetiFearGrid,
            1 => Context.Game.MuspellFearGrid,
            _ => null,
        };
    }

    private static ISquadUnit? TargetUnit(Thing? target, FVector near)
    {
        if (target is null)
        {
            return null;
        }

        if ((target.ThingTypeBits & ThingType.Unknown20000000) != 0)
        {
            return ((Squad)target).Unknown75(near);
        }

        if ((target.ThingTypeBits & ThingType.Unit) != 0)
        {
            return (ISquadUnit)target;
        }

        return null;
    }

    /// <summary>Unknown4e7110: the squad's movement and formation for this update; TRUE to run again next frame.</summary>
    public bool Unknown4e7110(ScheduledEvent? e)
    {
        bool result = false;
        if (SpawnedFromUnitRecord && Unknown73() is { } leader)
        {
            Unknown120 = ThingOf(leader).GetAIState();
            Unknown7C = leader.GetAllegiance();
        }

        if (SpawnedFromUnitRecord && Unknown120 == AIState.Off)
        {
            if (Unknown73() is { } unit)
            {
                Pos = ThingOf(unit).Pos;
            }
        }
        else if (Unknown114)
        {
            FVector destination;
            Unknown110 = false;
            Unknown134 = Pos;
            if (UnknownC4.ToRead is { } target && Unknown120 == AIState.On)
            {
                int mode = Unknown4e81d0();
                if (mode == 2)
                {
                    destination = Pos - (target.Pos - Pos);
                    Unknown110 = true;
                    FVector delta = destination - Pos;
                    if (MagnitudeXYSq(delta) > 1.0f)
                    {
                        Orientation = Heading(Azimuth(delta));
                    }
                }
                else if (mode == 1)
                {
                    destination = target.Pos;
                    Unknown110 = true;
                    FVector delta = target.Pos - Pos;
                    if (MagnitudeXYSq(delta) > 1.0f)
                    {
                        Orientation = Heading(Azimuth(delta));
                    }
                }
                else
                {
                    destination = Pos;
                    FVector delta = target.Pos - Pos;
                    if (MagnitudeXYSq(delta) > 1.0f)
                    {
                        Orientation = Heading(Azimuth(delta));
                    }
                }

                UnknownF4 = Pos;
            }
            else
            {
                Unknown110 = true;
                destination = UnknownF4;
                FVector delta = destination - Pos;
                if (MagnitudeXYSq(delta) > 1.0f)
                {
                    Orientation = Heading(Azimuth(delta));
                }
            }

            FMatrix movementOrientation = Heading(Azimuth(destination - Pos));
            FearGrid? fear = Unknown7C switch
            {
                0 => FearGridFor(0),
                1 => FearGridFor(1),
                _ => null,
            };
            if (fear is not null)
            {
                if (Unknown110)
                {
                    if (fear.InDanger(destination))
                    {
                        fear.Unknown44c810(ref destination);
                    }
                }
                else if (fear.InDanger(Pos))
                {
                    destination = Pos;
                    fear.Unknown44c810(ref destination);
                    Unknown110 = true;
                }
            }

            FVector moveDelta = destination - Pos;
            if (Unknown110)
            {
                float distanceSq = MagnitudeXYSq(moveDelta);
                if (distanceSq > 1.0f && UnknownD0 is not null)
                {
                    int radius = X87.Fist(Math.Ceiling(GetRadius())) * 2;
                    if (radius < 2)
                    {
                        radius = 2;
                    }

                    if (UnknownD4.Unknown08 == 0
                        || Math.Abs(UnknownD4.Unknown00 - X87.Fist(destination.X)) > radius
                        || Math.Abs(UnknownD4.Unknown04 - X87.Fist(destination.Y)) > radius)
                    {
                        UnknownD0.Unknown4bc2e0(Pos, destination, 0, UnknownD4);
                    }
                }

                bool advance = distanceSq > 1.0f;
                if (UnknownD4.Unknown08 != 0)
                {
                    int x = X87.Fist(Pos.X);
                    int y = X87.Fist(Pos.Y);
                    UnknownD4.GetCurrent(ref x, ref y);
                    moveDelta = new FVector(x, y, 0) - Pos;
                    movementOrientation = Heading(Azimuth(moveDelta));
                    advance = true;
                    while (moveDelta.MagnitudeXY() < Unknown108 * 20.0f)
                    {
                        if (--UnknownD4.Unknown0C <= 0)
                        {
                            advance = false;
                            break;
                        }

                        UnknownD4.GetCurrent(ref x, ref y);
                        moveDelta = new FVector(x, y, 0) - Pos;
                        movementOrientation = Heading(Azimuth(moveDelta));
                    }
                }
                else
                {
                    Unknown108 = 0;
                }

                UnknownC8.SetReader(null);
                if (advance)
                {
                    if (Unknown4e7cf0())
                    {
                        float scale = Unknown108 * 20.0f;
                        moveDelta = new FVector(movementOrientation.Row0.Y * scale, movementOrientation.Row1.Y * scale,
                            movementOrientation.Row2.Y * scale);
                        if (UnknownD0 is not null)
                        {
                            if (UnknownD0.UnknownCell(Pos + moveDelta))
                            {
                                Pos += moveDelta;
                            }
                            else if (UnknownD4.Unknown0C > 0)
                            {
                                Pos.X = (UnknownD4.Unknown10[UnknownD4.Unknown0C - 1] * 2) + 1;
                                Pos.Y = (UnknownD4.Unknown18[UnknownD4.Unknown0C - 1] * 2) + 1;
                            }
                        }
                    }
                    else if (Unknown4e7f40())
                    {
                        result = true;
                    }
                }

                if ((!advance || Unknown9C == 1) && Unknown4e7cf0() && Unknown104 > 1.0f)
                {
                    var it = new ListIterator<Thing>(Context.World.SquadNB);
                    for (Thing? t = it.First(); t is not null; t = it.Next())
                    {
                        var other = (Squad)t;
                        if (other != this && other.Unknown82() && other.Unknown78() > 1.0f)
                        {
                            FVector separation = Pos - other.Pos;
                            float distance = separation.MagnitudeXY();
                            float overlap = GetRadius() + other.GetRadius() - distance;
                            if (distance > 0 && overlap > 0)
                            {
                                separation.Z = 0;
                                separation *= overlap / distance;
                                if (UnknownD0!.UnknownCell(Pos + separation))
                                {
                                    Pos += separation;
                                }

                                UnknownC8.SetReader(other);
                            }
                        }
                    }
                }
            }

            bool attack = UnknownC4.ToRead is { } aim && MagnitudeXYSq(aim.Pos - Unknown72()) < 15625.0f;
            if (attack || Unknown118 > Context.Events.Time)
            {
                Unknown4e8930();
            }
            else if (Unknown110 || Assembling)
            {
                Unknown4e8730();
            }

            if (attack)
            {
                Unknown118 = Context.Events.Time + 15.0f;
            }

            Pos.Z = Context.Map.Collide(Pos);
            Unknown4e91f0();
            Unknown4e8f80(false);
        }

        int count = 0;
        Unknown124 = new FVector(0, 0, 0);
        var mit = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = mit.First(); member is not null; member = mit.Next())
        {
            if (member.Unit.ToRead is { } unit)
            {
                Unknown124 += unit.Pos;
                count++;
            }
        }

        if (count != 0)
        {
            float n = count;
            Unknown124 = new FVector(Unknown124.X / n, Unknown124.Y / n, Unknown124.Z / n);
        }

        return result;
    }

    /// <summary>
    /// Unknown4e7cf0: whether the squad may advance; the speed is cut by the share of members out of place (those whose
    /// place is open), with a five-second allowance.
    /// </summary>
    public bool Unknown4e7cf0()
    {
        Unknown108 = Unknown10C;
        if (Unknown120 == AIState.Onf)
        {
            return true;
        }

        if (Assembling)
        {
            return false;
        }

        if (Unknown11C + 5.0f < Context.Events.Time)
        {
            Unknown11C = Context.Events.Time;
            return true;
        }

        int count = 0;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.Unit.ToRead is { } unit)
            {
                FVector delta = unit.Pos - member.GetPos();
                if (member.GetRadius() * 1.5f < delta.MagnitudeXY() && member.IsPositionOpen())
                {
                    count++;
                }
            }
        }

        if (UnknownB4 != 0)
        {
            float scale = 1.0f - ((float)count / UnknownB4 * 1.5f);
            if (scale < 0)
            {
                scale = 0;
            }

            Unknown108 *= scale;
            if (scale > 0.5f)
            {
                Unknown11C = Context.Events.Time;
                return true;
            }
        }

        return false;
    }

    /// <summary>Unknown4e7f40: whether the members' centre is within a step of the squad.</summary>
    public bool Unknown4e7f40()
    {
        var centre = new FVector(0, 0, 0);
        int count = 0;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.Unit.ToRead is { } unit)
            {
                centre += unit.Pos;
                count++;
            }
        }

        if (count != 0)
        {
            float n = count;
            centre = new FVector(centre.X / n, centre.Y / n, centre.Z / n);
            if ((Pos - centre).MagnitudeXY() < Unknown10C * 20.0f)
            {
                return true;
            }
        }

        return false;
    }

    public override bool Unknown70()
    {
        if (Assembling)
        {
            return false;
        }

        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.Unit.ToRead is { } unit && !unit.IsDying)
            {
                return false;
            }
        }

        return true;
    }

    public override float Unknown79()
    {
        float threat = 0;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.UnitOrNull is { } unit)
            {
                threat += unit.GetThreat();
            }
        }

        return threat;
    }

    public override float Unknown80()
    {
        float importance = 0;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.UnitOrNull is { } unit)
            {
                importance += unit.GetImportance();
            }
        }

        return importance;
    }

    /// <summary>
    /// Unknown4e8100 (event 4000): an active squad without a kept target chooses one; again FloatRandom() * 2 + 2
    /// seconds on (a draw).
    /// </summary>
    public void Unknown4e8100()
    {
        if (Unknown114 && Unknown120 != AIState.Off)
        {
            if (UnknownC4.ToRead is null)
            {
                UnknownCC = 0;
            }

            if (UnknownCC == 0)
            {
                Thing? target = Unknown7C is 0 or 1 ? SelectTarget(this) : null;
                UnknownC4.SetReader(target);
                UnknownCC = 0;
            }
        }

        Context.Events.AddEvent(SquadEvent.ChooseTarget, this,
            Context.Events.Time + ((Context.Random.FloatRandom() * 2.0f) + 2.0f), EventPriority.StartOfFrame, null, null);
    }

    /// <summary>Unknown4e81d0: 2 to back off (too close), 0 to hold (in range and clear), 1 to close in.</summary>
    public int Unknown4e81d0()
    {
        if (UnknownC4.ToRead is { } target)
        {
            ISquadUnit? unit = Unknown75(target.Pos);
            if (unit is not null)
            {
                ISquadUnit? aim = TargetUnit(target, ThingOf(unit).Pos);
                if (aim is null)
                {
                    return 0;
                }

                unit.Unknown4fb840(ThingOf(aim));
                FVector delta = target.Pos - Unknown72();
                float distance = delta.Magnitude();
                FVector aimPos = ThingOf(aim).Pos;
                if (unit.GetMinRange(aimPos) * 1.05f > distance)
                {
                    return 2;
                }

                if (unit.GetMaxRange(aimPos) * 0.95f >= distance && unit.Unknown4fb5a0(aim))
                {
                    return 0;
                }
            }
        }

        return 1;
    }

    /// <summary>Slot 85: the target set from outside, kept until it goes.</summary>
    public override void Unknown85(Thing? target)
    {
        UnknownC4.SetReader(target);
        UnknownCC = 1;
    }

    public override bool Unknown69() => Unknown73() is { } unit && unit.IsAThreat();

    /// <summary>
    /// Unknown4e83b0 (event 4001): members with no unit or a dying one leave (the formation is redone unless the squad
    /// is attacking with armed members and not moving), places are swapped until settled; then again FloatRandom() + 1
    /// seconds on (a draw) when rescheduling.
    /// </summary>
    public void Unknown4e83b0(bool reschedule)
    {
        var it = new ListIterator<NormalSquadMember>(Members);
        NormalSquadMember? member = it.First();
        ISquadUnit? unit = Unknown73();
        while (member is not null)
        {
            NormalSquadMember current = member;
            member = it.Next();
            if (current.Unit.ToRead is not { } u || u.IsDying)
            {
                Members.Remove(current);
                UnknownB4--;
                if (UnknownBC != 2 || Unknown110 || (unit is not null && unit.AnyWeaponUnknown94()))
                {
                    UnknownBC = 0;
                }
            }
        }

        Unknown4e84e0();
        if (reschedule)
        {
            Context.Events.AddEvent(SquadEvent.CheckMembers, this,
                Context.Events.Time + (Context.Random.FloatRandom() + 1.0f), EventPriority.StartOfFrame, null, null);
        }
    }

    /// <summary>Unknown4e84e0: one pass of place swaps; TRUE when none was made.</summary>
    public bool Unknown4e84e0()
    {
        bool done = true;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            float distance = (member.GetPos() - member.Unit.ToRead!.Pos).MagnitudeXY();
            var candidates = new ListIterator<NormalSquadMember>(Members);
            for (NormalSquadMember? candidate = candidates.First(); candidate is not null; candidate = candidates.Next())
            {
                if ((member.GetPos() - candidate.Unit.ToRead!.Pos).MagnitudeXY() < distance && member.Unknown4e97e0(candidate))
                {
                    distance = (member.GetPos() - member.Unit.ToRead!.Pos).MagnitudeXY();
                    done = false;
                }
            }
        }

        return done;
    }

    /// <summary>Unknown4e8730: the marching formation (a square block), settled, each member sent to its place.</summary>
    public void Unknown4e8730()
    {
        Unknown4e83b0(false);
        if (Unknown104 != 0)
        {
            int columns = (int)Math.Ceiling(Math.Sqrt((float)UnknownB4));
            if (columns < 1)
            {
                columns = 1;
            }

            Place(columns);
            UnknownBC = 1;
        }

        while (!Unknown4e84e0())
        {
        }

        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.MoveToPosition();
        }
    }

    /// <summary>The places across a formation of the given width: alternate sides of the centre, rows behind.</summary>
    private void Place(int columns)
    {
        int index = 0;
        float offset = 0;
        if (columns % 2 == 0)
        {
            offset = Unknown104;
        }

        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            int row = index / columns;
            int column = index - (row * columns);
            if (column % 2 == 1)
            {
                member.Unknown04 = ((-1 - column) * Unknown104) + offset;
            }
            else
            {
                member.Unknown04 = (column * Unknown104) + offset;
            }

            member.Unknown08 = row * Unknown104 * -2.0f;
            index++;
        }
    }

    /// <summary>
    /// Unknown4e8930: the attacking formation (a line of six or half the squad), settled; members in place aim, the
    /// others aim if armed and go to their places.
    /// </summary>
    public void Unknown4e8930()
    {
        Unknown4e83b0(false);
        if (Unknown104 != 0 && UnknownBC != 2)
        {
            int columns = UnknownB4 / 2;
            if (columns < 6)
            {
                columns = 6;
            }

            if (columns > UnknownB4)
            {
                columns = UnknownB4;
            }

            Place(columns);
            UnknownBC = 2;
        }

        while (!Unknown4e84e0())
        {
        }

        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.Unit.ToRead is { } unit)
            {
                ISquadUnit? target = UnknownC4.ToRead is { } aim ? TargetUnit(aim, unit.Pos) : null;
                if (!member.IsOutOfPosition() && !Unknown110)
                {
                    member.SetTarget(target);
                }
                else
                {
                    member.SetTarget(member.HasWeapon() ? target : null);
                    member.MoveToPosition();
                }
            }
        }
    }

    public override ISquadUnit? Unknown73()
    {
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.UnitOrNull is { } unit)
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>Slot 74: the member furthest forward (least mUnknown08).</summary>
    public override ISquadUnit? Unknown74()
    {
        float minimum = 99999.0f;
        ISquadUnit? unit = null;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.UnitOrNull is { } u && minimum > member.Unknown08)
            {
                minimum = member.Unknown08;
                unit = u;
            }
        }

        return unit;
    }

    public override ISquadUnit? Unknown75(FVector point)
    {
        ISquadUnit? nearest = null;
        float nearestDistance = 999999.0f;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            if (member.Unit.ToRead is { } unit && !unit.IsDying)
            {
                float distance = (point - unit.Pos).MagnitudeXY();
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = (ISquadUnit)unit;
                }
            }
        }

        return nearest;
    }

    public override bool Unknown82() =>
        Unknown74() is { } unit && ThingOf(unit).GetMaxVelocity() > 0 && Unknown9C == 0;

    /// <summary>GetRadius: the furthest place from the squad, plus a member's radius / 2.5.</summary>
    public override float GetRadius()
    {
        float radius = 0;
        var it = new ListIterator<NormalSquadMember>(Members);
        NormalSquadMember? member = it.First();
        while (member is not null)
        {
            float distance = (member.GetPos() - Pos).MagnitudeXY();
            if (distance > radius)
            {
                radius = distance;
            }

            member = it.Next();
        }

        return radius + (Unknown104 / 2.5f);
    }

    public override SPtrSet<Thing> GetUnits()
    {
        var units = new SPtrSet<Thing>(Context.Pool);
        for (NormalSquadMember? member = Members.First(); member is not null; member = Members.Next())
        {
            units.Add(member.Unit.ToRead!);
        }

        return units;
    }

    /// <summary>CanMerge: has a speed, is not a unit record or being assembled, is active and on the ground.</summary>
    public bool CanMerge()
    {
        if (Unknown10C > 0 && !SpawnedFromUnitRecord && Unknown114 && !Assembling && (Flags & ThingFlags.DeclaredShutdown) == 0)
        {
            var it = new ListIterator<NormalSquadMember>(Members);
            for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
            {
                if (member.UnitOrNull is { } unit && !unit.IsOnGround())
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Unknown4e8f80: a squad with no target, short of its amount (or forced), merges into a matching squad within 40
    /// units that has room.
    /// </summary>
    public void Unknown4e8f80(bool force)
    {
        if (UnknownC4.ToRead is not null || !CanMerge())
        {
            return;
        }

        if (UnknownB4 < UnknownB8 || force)
        {
            var it = new ListIterator<Thing>(Context.World.SquadNB);
            for (Thing? t = it.First(); t is not null; t = it.Next())
            {
                // Retail casts every squad on the list to CNormalSquad here.
                var other = (NormalSquad)t;
                if (other.Unknown7C == Unknown7C && other.Unknown83() == 0
                    && (Pos - other.Pos).MagnitudeXY() < 40.0f && other != this
                    && other.UnknownA0 == UnknownA0 && other.Unknown9C == Unknown9C)
                {
                    if (other.CanMerge() && (UnknownB8 >= UnknownB4 + other.Members.Size || force))
                    {
                        NormalSquadMember? member;
                        while ((member = Members.First()) is not null)
                        {
                            if (member.UnitOrNull is { } unit)
                            {
                                Unknown4e6f70(unit);
                                other.Unknown67(unit, 0, 0);
                            }
                            else
                            {
                                Members.Remove(member);
                            }

                            other.UnknownBC = 0;
                        }

                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Unknown4e91f0: members more than 5 under the ground leave for squads of their own (assembled, amount 0); a squad of
    /// one follows its member down.
    /// </summary>
    public void Unknown4e91f0()
    {
        if (UnknownB4 >= 2)
        {
            var it = new ListIterator<NormalSquadMember>(Members);
            for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
            {
                if (member.Unit.ToRead is { } unit && unit.Pos.Z - Context.Map.Collide(unit.Pos) < -5.0f)
                {
                    var squad = new NormalSquad(Context) { SpawnUnit = SpawnUnit };
                    var init = new InitThing
                    {
                        Kind = InitThingRecord.InitKind.Squad,
                        Pos = unit.Pos,
                        Orientation = Orientation,
                        Allegiance = Unknown7C,
                        UnitName = UnknownA0!.Name,
                    };
                    squad.Assembling = true;
                    squad.Init(init);
                    Unknown4e6f70(member.UnitOrNull);
                    // Retail reloads the member's reader after removal (004e936d).
                    squad.Unknown67(member.UnitOrNull!, 0, 0);
                    squad.Assembling = false;
                    UnknownBC = 0;
                    if (it.First() is null)
                    {
                        break;
                    }
                }
            }
        }

        if (UnknownB4 == 1 && Unknown73() is { } one)
        {
            Thing unit = ThingOf(one);
            if (unit.Pos.Z - Context.Map.Collide(unit.Pos) < -5.0f)
            {
                Pos = unit.Pos;
                Pos.Z = Context.Map.Collide(Pos);
            }
        }
    }

    public override void GoToPoint(FVector point, bool inOverride = false) => UnknownF4 = point;

    public override void Activate()
    {
        Unknown114 = true;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.Unit.ToRead?.Activate();
        }
    }

    public override void Deactivate()
    {
        Unknown114 = false;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.Unit.ToRead?.Deactivate();
        }
    }

    public override void MakeVisible()
    {
        Flags &= ~ThingFlags.Invisible;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.Unit.ToRead?.MakeVisible();
        }
    }

    public override void MakeInvisible()
    {
        Flags |= ThingFlags.Invisible;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.Unit.ToRead?.MakeInvisible();
        }
    }

    /// <summary>Slot 87: a new allegiance for the squad and its members, and its side lists again.</summary>
    public void Unknown87(int allegiance)
    {
        Unknown7C = allegiance;
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.UnitOrNull?.SetAllegiance(allegiance);
        }

        Context.World.Set30.Remove(this);
        Context.World.Set20.Remove(this);
        if (Unknown7C is 1 or 6)
        {
            Context.World.Set30.Append(this);
        }

        if (Unknown7C is 0 or 6)
        {
            Context.World.Set20.Append(this);
        }
    }

    public override void Teleport(FVector pos)
    {
        base.Teleport(pos);
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.TeleportToPosition();
        }
    }

    public override void TeleportOrientation(FMatrix orientation)
    {
        base.TeleportOrientation(orientation);
        var it = new ListIterator<NormalSquadMember>(Members);
        for (NormalSquadMember? member = it.First(); member is not null; member = it.Next())
        {
            member.TeleportToPosition();
            member.Unit.ToRead?.TeleportOrientation(orientation);
        }
    }

    public override FVector Unknown71() => UnknownF4;

    public override FVector Unknown72() => Unknown124;

    public override int Unknown76() => unchecked((int)UnknownC0);

    public override Thing? Unknown77() => UnknownC4.ToRead;

    public override float Unknown78() => Unknown104;

    public override int Unknown83() => 0;

    public override float Unknown84() => 0;
}

/// <summary>
/// CRelaxedSquad: mechs and infantry; it drifts 45 units towards a target every five to ten seconds and sends idle
/// members after it.
/// </summary>
public sealed class RelaxedSquad : Squad
{
    public RelaxedSquad(ThingContext context)
        : base(context)
    {
        Members = new SPtrSet<ActiveReader<Thing>>(context.Pool);
    }

    /// <summary>mMembers: readers of its units.</summary>
    public SPtrSet<ActiveReader<Thing>> Members { get; }

    public override void Init(InitThing init)
    {
        base.Init(init);
        Unknown66(null);
    }

    public override void Shutdown()
    {
        Members.RemoveAll();
        base.Shutdown();
    }

    public override void Unknown66(ScheduledEvent? e)
    {
        base.Unknown66(e);
        var members = new ListIterator<ActiveReader<Thing>>(Members);
        ActiveReader<Thing>? next = members.First();
        while (next is not null)
        {
            ActiveReader<Thing> member = next;
            next = members.Next();
            if (member.ToRead is null)
            {
                Members.Remove(member);
            }
        }

        Pos = Unknown72();
        Thing? target = Unknown7C is 0 or 1 ? SelectTarget(this) : null;
        if (target is not null)
        {
            FVector d = target.Pos - Pos;
            float m = d.Magnitude();
            if (m != 0)
            {
                float inv = 1.0f / m;
                d.X *= inv;
                d.Y *= inv;
                d.Z *= inv;
            }

            d *= 45.0f;
            Pos += d;
        }

        var it = new ListIterator<ActiveReader<Thing>>(Members);
        for (ActiveReader<Thing>? r = it.First(); r is not null; r = it.Next())
        {
            if (r.ToRead is ComplexThing unit && ((ISquadUnit)unit).GetCurrentTarget() is null)
            {
                if (MagnitudeXYSq(unit.Pos - Pos) < 400.0f)
                {
                    unit.GoToPoint(unit.Pos, false);
                }
                else
                {
                    unit.GoToPoint(Pos, false);
                }
            }
        }

        if (Unknown70())
        {
            AddShutdownEvent();
        }
        else
        {
            Context.Events.AddEvent(SquadEvent.Update, this,
                Context.Events.Time + ((Context.Random.FloatRandom() * 5.0f) + 5.0f), EventPriority.StartOfFrame, null, e);
        }
    }

    /// <summary>Slot 67: a reader of the unit; it stands 1.5 radii per earlier member along the squad's X axis.</summary>
    public override FVector Unknown67(ISquadUnit unit, int a, int b)
    {
        if (unit.GetSquad() is null)
        {
            ComplexThing thing = ThingOf(unit);
            var member = new ActiveReader<Thing>();
            member.SetReader(thing);
            Members.Add(member);
            float offset = thing.GetRadius() * (Members.Size - 1) * 1.5f;
            var side = new FVector(offset * Orientation.Row0.X, offset * Orientation.Row1.X, offset * Orientation.Row2.X);
            return side + thing.Pos;
        }

        return new FVector(0, 0, 0);
    }

    public override void Unknown68(ISquadUnit unit)
    {
    }

    public override bool Unknown69() => false;

    public override bool Unknown70()
    {
        if (Assembling)
        {
            return false;
        }

        var it = new ListIterator<ActiveReader<Thing>>(Members);
        for (ActiveReader<Thing>? r = it.First(); r is not null; r = it.Next())
        {
            if (r.ToRead is not null)
            {
                return false;
            }
        }

        return true;
    }

    public override void Unknown86()
    {
        ActiveReader<Thing>? member;
        while ((member = Members.First()) is not null)
        {
            member.ToRead?.Shutdown();
            Members.Remove(member);
        }
    }

    public override SPtrSet<Thing> GetUnits()
    {
        var units = new SPtrSet<Thing>(Context.Pool);
        var it = new ListIterator<ActiveReader<Thing>>(Members);
        for (ActiveReader<Thing>? r = it.First(); r is not null; r = it.Next())
        {
            if (r.ToRead is { } unit)
            {
                units.Add(unit);
            }
        }

        return units;
    }

    /// <summary>Slot 72: the members' centre, or the squad's position with none.</summary>
    public override FVector Unknown72()
    {
        var centre = new FVector(0, 0, 0);
        int count = 0;
        var it = new ListIterator<ActiveReader<Thing>>(Members);
        for (ActiveReader<Thing>? r = it.First(); r is not null; r = it.Next())
        {
            if (r.ToRead is { } unit)
            {
                centre += unit.Pos;
                count++;
            }
        }

        if (count != 0)
        {
            float n = count;
            return new FVector(centre.X / n, centre.Y / n, centre.Z / n);
        }

        return Pos;
    }

    public override float GetRadius() => 0;

    public override ISquadUnit? Unknown73()
    {
        var it = new ListIterator<ActiveReader<Thing>>(Members);
        for (ActiveReader<Thing>? r = it.First(); r is not null; r = it.Next())
        {
            if (r.ToRead is { } unit)
            {
                return (ISquadUnit)unit;
            }
        }

        return null;
    }

    public override ISquadUnit? Unknown74() => Unknown73();

    public override ISquadUnit? Unknown75(FVector point) => null;

    public override int Unknown76() => 0;

    public override Thing? Unknown77() => null;

    public override float Unknown78() => 0;

    public override float Unknown79() => 0;

    public override float Unknown80() => 0;

    public override bool Unknown82() => false;

    public override int Unknown83() => 2;

    public override float Unknown84() => 1;

    public override void Unknown85(Thing? target)
    {
    }
}
