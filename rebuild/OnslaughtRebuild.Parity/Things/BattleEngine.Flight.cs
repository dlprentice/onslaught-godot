// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngine.cpp: Morph0040a580, HandleEvent0040c180, mesh switch00406460,
// EnableFlightMode0040dcb0, DisableFlightMode0040dcc0 and ZoomIn/Out00409e90..00409ec0.
using OnslaughtRebuild.Parity.Events;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class BattleEngine
{
    public bool FlightModeActive = true;
    public bool Stalling;
    public float StallTime = -99999.0f;
    public bool RenderThingIsWalker = true;
    private MCBattleEngine? _backupMotionController;

    public float LastTimeInHostileEnvironment;
    internal void HostileEnvironment()
    {
        if (Context.Events.Time - LastTimeInHostileEnvironment > 5.0f) PlayHudSample("hud_hostile_environment");
        LastTimeInHostileEnvironment = Context.Events.Time;
    }

    public void EnableFlightMode() => FlightModeActive = true;
    public void DisableFlightMode()
    {
        FlightModeActive = false;
        if (State == BattleEngineState.Jet) Morph();
    }

    public void ZoomIn() { if (GetCurrentWeapon()?.Data.ZoomMode == 1) DesiredZoom = 0.4f; }
    public void ZoomOut() { if (GetCurrentWeapon()?.Data.ZoomMode == 1) DesiredZoom = 1.0f; }

    /// <summary>
    /// Morph0040a580: permission first; then slow movement cleared before transition/special-move guards. Both charges
    /// are lost and zoom reset BEFORE the energy test. The ground launch is completed by Move's repeated delayed
    /// BecomeJet events, not a fixed timer started here. The air launch additionally queues its original 0.1s event.
    /// </summary>
    public void Morph()
    {
        if (!FlightModeActive && State == BattleEngineState.Walker) return;
        SlowMovement = false;
        if (State is BattleEngineState.MorphingIntoJet or BattleEngineState.MorphingIntoWalker) return;
        if (JetPart!.IsDoingSpecialAirMove() || WalkerPart!.DoingDashCount > 0) return;
        WalkerPart.LoseWeaponCharge();
        JetPart.LoseWeaponCharge();
        DesiredZoom = 1.0f;
        var sounds = Context.SoundEffects;
        if (State == BattleEngineState.Jet)
        {
            Context.Events.AddEvent(BattleEngineEvent.BecomeWalker, this, Context.Events.Time + 0.5f, EventPriority.StartOfFrame);
            TransformStartTime = IsOnGround() ? Context.Events.Time : 0.0f;
            Cockpit?.MorphIntoWalkerCockpit();
            State = BattleEngineState.MorphingIntoWalker;
            SetAnimMode(AnimModeByName("flytowalk"), true, false);
            sounds?.FadeRepeatingSample(sounds.Find("BE Engines(in-flight)"), this, 0, 0.02f);
            sounds?.PlayEffect(sounds.Find("BE Engines(land)"), Context.Crt);
            Stalling = false;
        }
        else if (Energy >= Configuration!.MinTransformEnergy)
        {
            if (GetThreat() > 0) Context.InfluenceMap!.AddResidual(Pos.X, Pos.Y, GetThreat(), Allegiance);
            Stalling = false;
            Cockpit?.MorphIntoJetCockpit();
            State = BattleEngineState.MorphingIntoJet;
            PutUsInRightMesh();
            SetAnimMode(AnimModeByName("walktofly"), true, false);
            if (Context.Events.Time - LastTimeOnGround < 0.6f) TakeOffHeight = Pos.Z;
            else
            {
                TakeOffHeight = 99999.0f;
                Context.Events.AddEvent(BattleEngineEvent.BecomeJet, this, Context.Events.Time + 0.1f, EventPriority.StartOfFrame);
            }
            TakeOffTime = Context.Events.Time;
            sounds?.PlayEffect(sounds.Find("BE Engines(takeoff)"), Context.Crt);
            if (sounds?.Find("BE Engines(in-flight)") is { } inflight)
            {
                if (!sounds.IsRepeatingEffectPlaying(inflight, this)) sounds.PlayRepeatingEffect(inflight, this, Context.Crt, 0.02f);
                else sounds.FadeRepeatingSample(inflight, this, 1.0f, 0.02f);
            }
        }
    }

    /// <summary>
    /// Single-player switch00406460: swap the two retained meshes, detach the walker controller, then reattach that
    /// SAME controller on return to Walker. MorphingIntoWalker continues drawing the jet mesh. The cylinder remains
    /// the single-player collision owner; retail updates mesh collision only in the multiplayer branch.
    /// </summary>
    public void PutUsInRightMesh()
    {
        if (State is BattleEngineState.MorphingIntoJet or BattleEngineState.Jet && RenderThingIsWalker)
        {
            (RenderThing, OtherMeshRenderThing) = (OtherMeshRenderThing, RenderThing);
            RenderThingIsWalker = false;
            _backupMotionController = MotionController;
            _backupMotionController?.SetAttached(false);
            MotionController = null;
        }
        else if (State == BattleEngineState.Walker && !RenderThingIsWalker)
        {
            (RenderThing, OtherMeshRenderThing) = (OtherMeshRenderThing, RenderThing);
            RenderThingIsWalker = true;
            MotionController = _backupMotionController;
            MotionController?.SetAttached(true);
            _backupMotionController = null;
        }
    }

    /// <summary>Move004081c0 takeoff branch: repeated scheduled events are intentional; only the first changes state.</summary>
    internal void AdvanceTakeoff()
    {
        if (CurrentOrientation.Pitch > -0.03f) Pitchvel -= 0.015f;
        if (Pos.Z > TakeOffHeight - 0.1f && Context.Events.Time < TakeOffTime + 2.0f) Velocity.Z -= 0.7f;
        else Context.Events.AddEvent(BattleEngineEvent.BecomeJet, this, Context.Events.Time + 0.5f, EventPriority.StartOfFrame);
    }
}
