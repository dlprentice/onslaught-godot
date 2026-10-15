// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/unit.h and src/Unit.cpp (CUnit: SetSquad 004fe500, GetOccupancyGrid 004fd380, Unknown111
// 004fe5c0, IsAThreat 004fd440, Unknown4fb3d0 004fb3d0, IsLiveUnit 004fd5b0, IsTargetAlligence 004fd3d0, GetThreat and
// GetImportance (slots 79/80), GetCurrentTarget (81), GetStealth (004bfc60), Unknown108). Not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CUnit's side of the squads, the target selector, the influence map and the fear grids (ISquadUnit).</summary>
public abstract partial class Unit : ISquadUnit
{
    public Squad? GetSquad() => Squad;

    /// <summary>SetSquad: a unit in a squad leaves the sides' lists; the squad stands for it there.</summary>
    public void SetSquad(Squad? squad)
    {
        Squad = squad;
        if (squad is not null)
        {
            Context.World.Set30.Remove(this);
            Context.World.Set20.Remove(this);
        }
    }

    /// <summary>GetOccupancyGrid: the world's grid for the unit's navigation map (none for water units).</summary>
    public IOccupancyGrid? GetOccupancyGrid()
    {
        if (UnitData is null)
        {
            return null;
        }

        if (UnitData.NavMap is >= 1 and <= 4)
        {
            Context.Skip("CUnit::GetOccupancyGrid: the world's occupancy grids are not ported (read as none)");
        }

        return null;
    }

    /// <summary>Unknown111: the unit's speed, half again while deploying.</summary>
    public float Unknown111() => DeployState is 1 or 2 ? UnitData!.Velocity * 1.5f : UnitData!.Velocity;

    /// <summary>GetThreat (slot 79): the unit data's threat.</summary>
    public virtual float GetThreat() => UnitData!.Threat;

    /// <summary>GetImportance (slot 80).</summary>
    public virtual float GetImportance() => UnitData!.Importance;

    /// <summary>GetStealth (slot 91): 0 for a unit.</summary>
    public virtual float GetStealth() => 0;

    public int GetAllegiance() => Allegiance;

    public void SetAllegiance(int allegiance) => Allegiance = allegiance;

    /// <summary>Unknown108 (slot 108): TRUE for a unit (FALSE for the classes that override it).</summary>
    public virtual bool Unknown108() => true;

    /// <summary>IsAThreat: a weapon whose mode's round does damage, a spawner, or a live child.</summary>
    public bool IsAThreat()
    {
        if (WeaponsAreAThreat())
        {
            return true;
        }

        if (UnitData is not null && UnitData.Spawners.Count > 0)
        {
            Context.Skip("CUnit::IsAThreat: spawners read from the unit data (the runtime spawners are not ported)");
            return true;
        }

        return false;
    }

    /// <summary>Unknown4fb3d0: a spawner that can make something for the target, or the first weapon that can reach it.</summary>
    public bool Unknown4fb3d0(ISquadUnit target)
    {
        if (target is not Unit unit || !unit.Unknown108())
        {
            return false;
        }

        if (UnitData is not null && UnitData.Spawners.Count > 0)
        {
            throw new NotSupportedException("CUnit::Unknown4fb3d0 with spawners (CSpawner::Unknown4e4480 is not ported)");
        }

        return WeaponCanReach(unit);
    }

    /// <summary>IsLiveUnit: not dying and not deploying.</summary>
    public static bool IsLiveUnit(Unit? unit) =>
        unit is not null && !unit.IsDying && unit.DeployState != 1 && unit.DeployState != 2;

    /// <summary>IsTargetAlligence: whether a unit of that allegiance is an enemy.</summary>
    public bool IsTargetAlligence(int allegiance)
    {
        if (UnitData is not null && UnitData.Indiscriminate != 0 && allegiance == 2)
        {
            return true;
        }

        return allegiance switch
        {
            0 => Allegiance is 1 or 6,
            1 => Allegiance is 0 or 6,
            6 => Allegiance is 1 or 0,
            _ => false,
        };
    }

    /// <summary><c>if (mAI) mAI-&gt;Unknown4ffdd0(target, FALSE)</c>.</summary>
    public void SetAITarget(ISquadUnit? target)
    {
        if (AI is not null)
        {
            throw new NotSupportedException("CUnitAI::Unknown4ffdd0 (the unit AI is not ported yet)");
        }
    }

    /// <summary>GetCurrentTarget (slot 81): the AI's target.</summary>
    public virtual ISquadUnit? GetCurrentTarget()
    {
        if (AI is not null)
        {
            throw new NotSupportedException("CUnit::GetCurrentTarget (the unit AI is not ported yet)");
        }

        return null;
    }

    public virtual bool IsOnGround() => throw new NotSupportedException("IsOnGround (CActor/CGroundUnit) is not ported yet");

    /// <summary>GuideUnknown7 (004fcec0): the guide's slot 7.</summary>
    public void GuideUnknown7(FVector pos) => throw new NotSupportedException("the unit guides are not ported yet");
}
