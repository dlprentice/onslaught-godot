// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/unit.h and src/Unit.cpp (CUnit: GoToPoint 004fce00, GuideUnknown5 004fce40, GuideUnknown6
// 004fce80, GuideUnknown7 004fcec0, Stop 004fcf00, Unknown74 004fc080, Unknown87 004fc170, Unknown98 004fda90,
// StartUndeploying 004fde70, Unknown82, GetVulnerable, Unknown97) and src/actor.h (CActor::Stop). Not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CUnit's side of its AI and guide.</summary>
public abstract partial class Unit
{
    /// <summary>mUnknown14C: the velocity the guide asks for.</summary>
    public FVector Unknown14C;

    /// <summary>mUnknown1F0: set by Unknown82 (the AI ran out of waypoints).</summary>
    public int Unknown1F0;

    /// <summary>mVulnerable.</summary>
    public bool Vulnerable = true;

    public override void GoToPoint(FVector point, bool inOverride = false) => Guide?.GoToPoint(point, inOverride);

    public void GuideUnknown5(FVector v) => Guide?.Unknown5(v);

    public void GuideUnknown6(FVector v) => Guide?.Unknown6(v);

    public void GuideUnknown7(FVector v) => Guide?.Unknown7(v);

    /// <summary>CUnit::Stop: no requested velocity, yaw held, CActor's stop (no velocity), then the guide's.</summary>
    public virtual void Stop()
    {
        Unknown14C = new FVector(0, 0, 0);
        Unknown120.Yaw = CurrentOrientation.Yaw;
        Velocity = new FVector(0, 0, 0);
        Guide?.Stop();
    }

    /// <summary>Unknown82 (slot 82).</summary>
    public virtual void Unknown82() => Unknown1F0 = 1;

    public virtual bool GetVulnerable() => Vulnerable;

    /// <summary>Unknown97 (slot 97): TRUE for a unit.</summary>
    public virtual bool Unknown97() => true;

    /// <summary>Unknown4fb670: 0 in range of the current weapon, 1 too far, 2 too near (or nothing to use).</summary>
    public int Unknown4fb670(Thing? target) => RangeBand(target);

    public bool Unknown84() => ShotReady();

    public bool Unknown86() => PrepareShot();

    public void WeaponUnknown509140(Thing? target, FVector pos) => AimWeapon(target, pos);

    /// <summary>CGuide::Unknown7's test: the current weapon turns a turret (+0x94).</summary>
    public bool CurrentWeaponTurnsTurret() => CurrentWeapon is not null && CurrentWeapon.Unknown94 != 0;

    /// <summary>Unknown74: fires the prepared shot when it is ready (spawners are not ported).</summary>
    public virtual bool Unknown74()
    {
        bool fired = false;
        if (CurrentWeapon is not null)
        {
            if (Unknown1E8 != 0 && CurrentWeapon.ReadyToFire())
            {
                fired = true;
                CurrentWeapon.Fire();
                Unknown87();
            }
        }
        else if (UnitData!.Spawners.Count > 0)
        {
            throw new NotSupportedException("CUnit::Unknown74 with a spawner (CSpawner is not ported)");
        }

        Unknown1EC = 0;
        Unknown1E8 = 0;
        return fired;
    }

    /// <summary>Unknown87 (slot 87): after a shot, the sound, the component effects and the reload timer.</summary>
    public virtual void Unknown87()
    {
        if (this is GroundUnit)
        {
            throw new NotSupportedException("CGroundUnit::Unknown87 (0047d420) is not ported yet");
        }

        if (CurrentWeapon?.Mode?.PostFireSoundName is { } name)
        {
            if (Context.SoundEffects is { EventKernelConfigured: true } sounds)
            {
                // Pristine 74154bfa:004fc18a..004fc1ae: sound precedes component effects and state writes.
                sounds.PlayEffect(sounds.Find(System.Text.Encoding.Latin1.GetString(name)), this, Context.Crt,
                    new SoundPlayback(Volume: .7f, Tracking: SoundTracking.InitialPosition));
            }
            else
            {
                Context.Skip("CUnit::Unknown87: the mode's post-fire sound");
            }
        }
        Context.Skip("CUnit::SpawnComponentEffects (post-fire)");
        Unknown1E8 = 0;
        float reload = CurrentWeapon?.Mode?.PostFireDelay ?? 0;
        // FCOMP / TEST AH,40 at 004fc1d4..004fc1e5 rejects both equal and unordered operands.
        if (CurrentWeapon is not null && reload != 0 && !float.IsNaN(reload))
        {
            Unknown16C = Context.Events.Time + reload;
            Unknown168 = 2;
        }
        else
        {
            Unknown168 = 0;
        }
    }

    /// <summary>Unknown98: a spawner that can spawn now (spawners are not ported: none here).</summary>
    public virtual bool Unknown98()
    {
        if (UnitData is not null && UnitData.Spawners.Count > 0)
        {
            throw new NotSupportedException("CUnit::Unknown98 with spawners (CSpawner is not ported)");
        }

        return false;
    }

    /// <summary>StartUndeploying: a deployed unit (state 4) starts undeploying.</summary>
    public void StartUndeploying()
    {
        if (DeployState != 4)
        {
            return;
        }

        DeployState = 5;
        SetAnimMode(AnimModeByName("undeploying"), true, false);
    }
}
