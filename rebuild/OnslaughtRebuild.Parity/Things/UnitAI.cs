// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/UnitAI.h and src/UnitAI.cpp (CUnitAI: the constructor 004fe710, Unknown5 004fea30, Unknown8
// 004feac0, Unknown6 004febe0, Unknown9 004fec60, Unknown3 004fef40, HandleEvent 004ff330, Unknown4 004ff4f0, Unknown11
// 004ff710, Unknown7 004ffb60, Unknown10 004ffbb0, Unknown4ffdd0, NewSweep 004ffe00), src/PlaneAI.h and src/PlaneAI.cpp
// (CPlaneAI::Unknown9 004d21c0), src/DropshipAI.cpp (CDropshipAI: Unknown8 00448490, Unknown9 00448580, Unknown3 004487e0)
// and src/RepairPadAI.cpp (CRepairPadAI: Unknown11 004d6d10, CanService 004d6e00). Not in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CUnitAI: a unit's AI. It follows its waypoints (3001), fights (3000) or follows the emitter of the unit that
/// launched it (3002); 3003 restarts whichever it was doing.</summary>
public class UnitAI : Monitor
{
    public const int Fight = 3000;
    public const int FollowWaypoints = 3001;
    public const int FollowEmitter = 3002;
    public const int Restart = 3003;

    public UnitAI(Unit unit, InitThing init)
    {
        ForUnit = unit;
        AIMode = -1;
        Unknown10 = 0;
        Unknown14 = 1;
        CanFireAtTarget = false;
        TargetInReach = false;
        Waypoint.SetReader(init.TargetThing);
        EmitterType = 0;
        EmitterIndex = 1;
        SpawnedBy.SetReader(init.SpawnedBy);
        if (SpawnedBy.ToRead is not null)
        {
            EmitterType = init.WaypointPath;
            Unknown7();
        }
        else
        {
            if (unit.Script is not null)
            {
                Context.Events.AddEvent(ThingEvent.ReadyScript, unit, EventManager.NextFrame);
            }

            if (Waypoint.ToRead is not null)
            {
                Unknown5();
            }
            else
            {
                Unknown6();
            }
        }

        if (unit.UnitData!.Sweeping != 0)
        {
            NewSweep();
        }
    }

    protected ThingContext Context => ForUnit.Context;

    public Unit ForUnit { get; }

    public ActiveReader<Unit> Target { get; } = new();

    public int Unknown10;
    public int Unknown14;
    public bool CanFireAtTarget;
    public bool TargetInReach;

    /// <summary>mAIMode: 0 following waypoints, 1 fighting, 2 following an emitter.</summary>
    public int AIMode;

    public ActiveReader<Thing> Waypoint { get; } = new();
    public ActiveReader<Unit> SpawnedBy { get; } = new();
    public int EmitterType;
    public int EmitterIndex;
    public FVector AimPos;
    public float EmitterReleaseTime;
    public float SweepYawStart, SweepYaw, SweepYawEnd, SweepPitchStart, SweepPitch, SweepPitchEnd;

    /// <summary>Unknown5: starts following its waypoints.</summary>
    public virtual bool Unknown5()
    {
        if (Waypoint.ToRead is not null)
        {
            if (AIMode == 0)
            {
                return false; // "CANT start following waypoints cos it already was"
            }

            AIMode = 0;
            Context.Events.AddEvent(FollowWaypoints, this, Context.Events.Time);
            return true;
        }

        return false;
    }

    /// <summary>Unknown6: starts fighting.</summary>
    public virtual bool Unknown6()
    {
        if (AIMode == 1)
        {
            return false; // "CANT start fighting cos it already was"
        }

        AIMode = 1;
        Context.Events.AddEvent(Fight, this, Context.Events.Time);
        return true;
    }

    /// <summary>Unknown7: starts following its launcher's emitter.</summary>
    public virtual bool Unknown7()
    {
        if (SpawnedBy.ToRead is not null && EmitterType != 0)
        {
            AIMode = 2;
            EmitterReleaseTime = Context.Events.Time + 10.0f;
            Context.Events.AddEvent(FollowEmitter, this, Context.Events.Time);
            return true;
        }

        return false;
    }

    /// <summary>Unknown8 (3001): a waypoint step.</summary>
    public virtual void Unknown8(Event e)
    {
        Thing? waypoint = Waypoint.ToRead;
        if (waypoint is not null)
        {
            FVector d = waypoint.Pos - ForUnit.Pos;
            Thing? next = (waypoint as Waypoint)?.Next.ToRead;
            if (d.MagnitudeXY() < 4.0f && !ReferenceEquals(next, waypoint))
            {
                Waypoint.SetReader(next);
            }

            waypoint = Waypoint.ToRead;
            if (waypoint is not null)
            {
                ForUnit.GoToPoint(waypoint.Pos, false);
                Unknown4();
                if (Target.ToRead is not null)
                {
                    Unknown6();
                    return;
                }
            }
            else
            {
                ForUnit.Stop();
                ForUnit.Unknown82();
            }
        }

        if (waypoint is not null)
        {
            Context.Events.AddEvent(FollowWaypoints, this, Context.Events.Time + (Context.Random.FloatRandom() + 1.0f),
                EventPriority.StartOfFrame, null, e as ScheduledEvent);
        }
        else
        {
            Unknown6();
        }
    }

    /// <summary>Unknown9 (3000): a fighting step.</summary>
    public virtual void Unknown9(Event e)
    {
        if (ForUnit.IsDying || ForUnit.DeployState is 1 or 2 || AIMode == 0)
        {
            return;
        }

        Squad? squad = ForUnit.Squad;
        Unit? target = Target.ToRead;
        if (squad is null)
        {
            if (target is not null)
            {
                if (ForUnit.Unknown4fb670(target) == 0)
                {
                    ForUnit.GuideUnknown6(target.Pos);
                }
            }
            else
            {
                ForUnit.Stop();
            }
        }
        else if (squad.Unknown83() == 1)
        {
            if (target is not null)
            {
                switch (ForUnit.Unknown4fb670(target))
                {
                    case 0:
                        ForUnit.GuideUnknown6(target.Pos);
                        break;
                    case 1:
                        ForUnit.GuideUnknown7(target.Pos);
                        break;
                    case 2:
                        ForUnit.GuideUnknown5(target.Pos);
                        break;
                }
            }
            else
            {
                ForUnit.Stop();
            }
        }

        bool swept = false;
        if (ForUnit.UnitData!.Sweeping != 0 && ForUnit.AnyWeaponUnknown50a290())
        {
            throw new NotSupportedException("CUnitAI::Unknown9's sweep aim (no sweeping unit in level 100)");
        }

        float delay = Unknown3();
        if (swept)
        {
            delay = 0;
        }

        Context.Events.AddEvent(Fight, this, Context.Events.Time + delay, EventPriority.StartOfFrame, null, e as ScheduledEvent);
    }

    /// <summary>Unknown3: fires on the target when it can; returns the delay to the next step.</summary>
    public virtual float Unknown3()
    {
        Unknown4();
        if (ForUnit.Unknown84() && Target.ToRead is not null)
        {
            Unit target = Target.ToRead;
            float delay;
            if (ForUnit.UnitData!.Sweeping != 0)
            {
                throw new NotSupportedException("CUnitAI::Unknown3's sweep (no sweeping unit in level 100)");
            }

            ForUnit.WeaponUnknown509140(target, AimPos);
            delay = Context.Random.FloatRandom() + 0.5f;
            ForUnit.Unknown74();
            if (ForUnit.UnitData.Indiscriminate != 0)
            {
                Target.SetReader(null);
                ForUnit.Unknown4fb840(null);
                Unknown10 = 0;
            }

            return delay;
        }

        if (CanFireAtTarget && Target.ToRead is { } aimed)
        {
            ForUnit.Unknown4fb840(aimed);
            if (ForUnit.UnitData!.Sweeping == 0 && ForUnit.Unknown168 != 1)
            {
                AimPos = aimed.GetTargetablePos();
                ForUnit.WeaponUnknown509140(aimed, AimPos);
            }

            ForUnit.Unknown86();
            return ForUnit.GetWeaponUnknownA0_88() + 0.1f;
        }

        Unit unit = ForUnit;
        unit.Unknown1EC = 0;
        unit.Unknown1E8 = 0;
        if (unit.UnitData!.Deploy != 0)
        {
            unit.StartUndeploying();
        }

        if (ForUnit.Unknown110 != 0)
        {
            return Context.Random.FloatRandom() + 1.5f;
        }

        return (Context.Random.FloatRandom() * 2.0f) + 3.0f;
    }

    /// <summary>CUnitAI::HandleEvent: the AI's events, else a restart in 2 to 4 seconds.</summary>
    public override void HandleEvent(Event e)
    {
        bool ok = ForUnit.DeployState is not (1 or 2);
        if (ForUnit.GetAIState() == AIState.Onf)
        {
            Unknown3();
        }
        else if (ok)
        {
            switch (e.EventNum)
            {
                case FollowWaypoints:
                    if (ForUnit.Active)
                    {
                        Unknown8(e);
                        return;
                    }

                    break;
                case Fight:
                    if (ForUnit.GetAIState() == AIState.On && ForUnit.Active)
                    {
                        Unknown9(e);
                        return;
                    }

                    break;
                case FollowEmitter:
                    Unknown10Step(e);
                    return;
                case Restart:
                    int next = AIMode switch { 1 => Fight, 0 => FollowWaypoints, 2 => FollowEmitter, _ => -1 };
                    if (next != -1)
                    {
                        Context.Events.AddEvent(next, this, EventManager.NextFrame, EventPriority.StartOfFrame, null,
                            e as ScheduledEvent);
                    }

                    return;
            }
        }

        Context.Events.AddEvent(Restart, this, Context.Events.Time + ((Context.Random.FloatRandom() * 2.0f) + 2.0f),
            EventPriority.StartOfFrame, null, e as ScheduledEvent);
    }

    /// <summary>Unknown4: refreshes the target and the fire flags.</summary>
    public virtual void Unknown4()
    {
        if (Target.ToRead is { } t && t.IsDying)
        {
            Target.SetReader(null);
        }

        Squad? squad = ForUnit.Squad;
        if (squad is not null && squad.Unknown83() == 0)
        {
            CanFireAtTarget = false;
            TargetInReach = false;
            if (Target.ToRead is not { } target)
            {
                return;
            }

            if (Unit.IsLiveUnit(target))
            {
                ForUnit.Unknown4fb840(target);
                bool reach = ForUnit.Unknown4fb5a0(target);
                TargetInReach = reach;
                if (reach)
                {
                    CanFireAtTarget = ForUnit.CanFireAt(target, false);
                }
            }
            else
            {
                Target.SetReader(null);
            }

            return;
        }

        if (ForUnit.Unknown98())
        {
            throw new NotSupportedException("CUnitAI::Unknown4's spawner targeting (CSpawner is not ported)");
        }

        if (Target.ToRead is { } kept && Unknown10 != 0)
        {
            ForUnit.Unknown4fb840(kept);
            TargetInReach = ForUnit.Unknown4fb5a0(kept);
            CanFireAtTarget = TargetInReach && ForUnit.CanFireAt(kept, false);
            return;
        }

        Unknown11();
    }

    /// <summary>Unknown11: picks a target among the other side's units (squads stand for their members).</summary>
    public virtual Unit? Unknown11()
    {
        if (Target.ToRead is { } current && current.GetStealth() == 0 && Unknown14 != 0 && Unit.IsLiveUnit(current))
        {
            ForUnit.Unknown4fb840(current);
            TargetInReach = ForUnit.Unknown4fb5a0(current);
            CanFireAtTarget = TargetInReach && ForUnit.CanFireAt(current, false);
            return current;
        }

        CanFireAtTarget = false;
        TargetInReach = false;
        Containers.SPtrSet<Thing> things = Context.World.Things;
        if (ForUnit.Allegiance == 1)
        {
            things = Context.World.Set20;
        }
        else if (ForUnit.Allegiance == 0)
        {
            things = Context.World.Set30;
        }

        Unit? best = null;
        float bestScore = 0;
        float bestPriority = -999999.0f;
        var it = new Containers.ListIterator<Thing>(things);
        for (Thing? thing = it.First(); thing is not null; thing = it.Next())
        {
            Unit? unit;
            if ((thing.ThingTypeBits & ThingType.Unknown20000000) != 0)
            {
                unit = ((Squad)thing).Unknown74() as Unit;
            }
            else if ((thing.ThingTypeBits & ThingType.Unit) != 0)
            {
                unit = (Unit)thing;
            }
            else
            {
                continue;
            }

            if (unit is null || !Unit.IsLiveUnit(unit) || !ForUnit.IsTargetAlligence(unit.GetAllegiance())
                || !ForUnit.Unknown4fb3d0(unit))
            {
                continue;
            }

            // As compiled at 004ff854..004ff8a2 (Steam BEA.exe, SHA-256 74154bfa…, the pristine specimen): the squared
            // distance sums Z*Z + Y*Y, then X*X, and is stored; stealth / 100.0f is a multiply by 0.01f (0x5d85fc).
            FVector d = thing.Pos - ForUnit.Pos;
            float distanceSq = ((d.Z * d.Z) + (d.Y * d.Y)) + (d.X * d.X);
            float range = (1.0f - (unit.GetStealth() * 0.01f)) * ForUnit.UnitData!.MaxTargetRange;
            if (!(distanceSq < range * range))
            {
                continue;
            }

            float priority = 0;
            Physics.UnitData data = ForUnit.UnitData;
            if (data.Indiscriminate != 0)
            {
                priority = Context.Random.FloatRandom() * 8.0f;
            }
            else if (unit.IsAThreat() || data.IgnoreThreats != 0)
            {
                uint type = unit.ThingTypeBits;
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

                if ((type & ThingType.Component) != 0 && priority < data.AttackPriority[6])
                {
                    priority = data.AttackPriority[6];
                }
            }

            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestScore = 0;
            }
            else if (priority != bestPriority)
            {
                continue;
            }

            float distance = MathF.Sqrt(distanceSq);
            float score = 1000.0f - distance;
            ForUnit.Unknown4fb840(unit);
            if (ForUnit.GetMinRange(unit.Pos) <= distance)
            {
                score += ForUnit.GetMaxRange(unit.Pos) < distance ? 10000.0f : 1000000.0f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = unit;
            }
        }

        if (best is not null)
        {
            Unknown4ffdd0(best, false);
            ForUnit.Unknown4fb840(Target.ToRead);
            if (Unit.IsLiveUnit(Target.ToRead))
            {
                TargetInReach = ForUnit.Unknown4fb5a0(Target.ToRead!);
                if (TargetInReach)
                {
                    CanFireAtTarget = ForUnit.CanFireAt(Target.ToRead!, false);
                }
            }
        }
        else
        {
            Target.SetReader(null);
        }

        Unknown10 = 0;
        return Target.ToRead;
    }

    /// <summary>Unknown10 (3002): an emitter step: it follows the launcher's emitter until released, then fights.</summary>
    public virtual void Unknown10Step(Event e)
    {
        Unit? launcher = SpawnedBy.ToRead;
        if (launcher is null || launcher.IsDying)
        {
            if (ForUnit.GetVulnerable())
            {
                ForUnit.StartDieProcess();
                return;
            }
        }

        bool follow = false;
        if (launcher is not null && launcher.Unknown97() && EmitterType != 0 && EmitterReleaseTime > Context.Events.Time)
        {
            launcher.GetEmitter(EmitterType, EmitterIndex, out FVector pos, out _);
            if (pos.X != 0 || pos.Y != 0 || pos.Z != 0)
            {
                float h = Context.Map.Collide(pos) - 0.1f;
                if (pos.Z > h)
                {
                    pos.Z = h;
                }

                FVector d = pos - ForUnit.Pos;
                float limit = ForUnit.IsA(ThingType.AirUnit) ? 6.25f : 0.5625f;
                if (d.MagnitudeSq() < limit)
                {
                    EmitterIndex++;
                }

                ForUnit.GoToPoint(pos, true);
                follow = true;
            }
        }

        if (!follow)
        {
            Context.Skip("CUnitAI::Unknown10: the collision-seeking thing's ignore pointer is cleared");
            Unknown6();
            if (ForUnit.Script is not null)
            {
                Context.Events.AddEvent(ThingEvent.ReadyScript, ForUnit, EventManager.NextFrame);
            }
        }
        else
        {
            Context.Events.AddEvent(FollowEmitter, this,
                (Context.Random.FloatRandom() * 0.1f) + Context.Events.Time + 0.1f, EventPriority.StartOfFrame, null,
                e as ScheduledEvent);
        }
    }

    /// <summary>Unknown4ffdd0: a new target, with the weapon for it.</summary>
    public void Unknown4ffdd0(Unit? unit, bool a)
    {
        Target.SetReader(unit);
        ForUnit.Unknown4fb840(unit);
        Unknown10 = a ? 1 : 0;
    }

    /// <summary>NewSweep: four draws for a sweeping unit's aim swing.</summary>
    public void NewSweep()
    {
        SweepYawStart = (Context.Random.FloatRandom() * 0.04f) + 0.04f;
        SweepPitchStart = (Context.Random.FloatRandom() * 0.04f) + 0.04f;
        if (Context.Random.FloatRandom() > 0.5f)
        {
            SweepYawStart = -SweepYawStart;
        }

        if (Context.Random.FloatRandom() > 0.5f)
        {
            SweepPitchStart = -SweepPitchStart;
        }

        SweepYawEnd = -SweepYawStart;
        SweepYaw = SweepYawStart;
        SweepPitchEnd = -SweepPitchStart;
        SweepPitch = SweepPitchStart;
    }
}

/// <summary>CPlaneAI: escorts the player when its unit data asks, otherwise attacks with its wings open.</summary>
public sealed partial class PlaneAI : UnitAI
{
    public int Unknown60;

    public PlaneAI(Unit unit, InitThing init)
        : base(unit, init)
    {
        Unknown60 = 0;
        Unknown14 = 0;
    }

}

/// <summary>CDropshipAI: the transporter's AI (doors, anchor, spawners).</summary>
public sealed class DropshipAI : UnitAI
{
    public int Unknown60;

    public DropshipAI(Unit unit, InitThing init)
        : base(unit, init) => Unknown60 = 0;

    public override void Unknown8(Event e) =>
        throw new NotSupportedException("CDropshipAI::Unknown8 (the dropship's waypoints are not ported yet)");

    public override void Unknown9(Event e) =>
        throw new NotSupportedException("CDropshipAI::Unknown9 (the dropship's doors and anchor are not ported yet)");

    public override float Unknown3()
    {
        ForUnit.Unknown86();
        if (ForUnit.Unknown84())
        {
            ForUnit.Unknown74();
        }

        return Context.Random.FloatRandom() + 0.5f;
    }
}

/// <summary>CRepairPadAI: its target is the battle engine standing on the pad that needs repair or rearming.</summary>
public sealed class RepairPadAI(Unit unit, InitThing init) : UnitAI(unit, init)
{
    /// <summary>
    /// Unknown11 (004d6d10): each battle engine within 8 of the pad that it can service and that stands within 3 across
    /// and up to 5 below it becomes the target, which it can fire at and reach (the last one found wins).
    /// </summary>
    public override Unit? Unknown11()
    {
        Target.SetReader(null);
        CanFireAtTarget = false;
        TargetInReach = false;
        MapWho map = Context.MapWho;
        for (MapWhoEntry? e = map.GetFirstEntryWithinRadius(ForUnit.Pos, 8.0f); e is not null; e = map.GetNextEntryWithinRadius())
        {
            if (e.Thing is BattleEngine be && CanService(be)
                && (ForUnit.Pos - be.Pos).MagnitudeXY() < 3.0f && ForUnit.Pos.Z > be.Pos.Z && be.Pos.Z + 5.0f > ForUnit.Pos.Z)
            {
                Target.SetReader(be);
                CanFireAtTarget = true;
                TargetInReach = true;
            }
        }

        Unknown10 = 0;
        return Target.ToRead;
    }

    /// <summary>CanService: a battle engine that needs health or ammunition, on a side the pad serves.</summary>
    public bool CanService(BattleEngine be)
    {
        if (be.FullHealth() && !be.NeedsRearm())
        {
            return false;
        }

        return be.Allegiance switch
        {
            0 => ForUnit.Allegiance is 0 or 6,
            1 => ForUnit.Allegiance is 1 or 6,
            6 => ForUnit.Allegiance is 1 or 0,
            _ => false,
        };
    }
}
