// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngine.h and src/BattleEngine.cpp (CBattleEngine::Init 00404dd0, UpdateConfiguration
// 0040c650, CalcUnitOverCrossHair 0040acc0, HandleAutoAim 0040b6d0, HandleEvent), in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>EBattleEngineEvent.</summary>
public static class BattleEngineEvent
{
    public const int BecomeJet = 6000;
    public const int BecomeWalker = 6001;
    public const int CalcUnitOverCrosshair = 6002;
    public const int HandleAutoAim = 6003;
}

/// <summary>EBattleEngineState.</summary>
public enum BattleEngineState
{
    MorphingIntoWalker,
    MorphingIntoJet,
    Walker,
    Jet,
}

/// <summary>
/// CBattleEngine: the player's machine. Init and the two targeting events are ported as far as they draw and schedule;
/// the walker and jet parts, weapons, cockpit, sounds and targeting maths are counted as not ported.
/// </summary>
public sealed class BattleEngine(ThingContext context) : Unit(context, Oid.BattleEngine)
{
    public int ConfigurationId;
    public BattleEngineData? Configuration;
    public BattleEngineState State;
    public bool Visible;
    public RenderThing? OtherMeshRenderThing;
    public int ThrusterFrames;
    public int EngineFrames;
    public float Zoom;
    public bool PoweredUp;

    /// <summary>The player controlling it (CBattleEngine::mPlayer), set by CGame::PostLoadProcess.</summary>
    public object? Player;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.BattleEngine | ThingType.GroundUnit
        | ThingType.AirUnit | ThingType.Mech | ThingType.CanDestroyTrees | ThingType.Vehicle);

    /// <summary>CBattleEngine::UpdateConfiguration.</summary>
    public void UpdateConfiguration()
    {
        BattleEngineData? data = Context.BattleEngines?.ForWorld(Context.WorldBattleEngineConfigurations, ConfigurationId);
        if (!ReferenceEquals(Configuration, data))
        {
            Configuration = data ?? throw new InvalidOperationException("no battle engine configuration");
            Energy = Configuration.Energy;
            Life = Configuration.Life;
            Context.Skip("CBattleEngine::UpdateConfiguration: the parts' ResetConfiguration and the stores");
        }
    }

    public override void Init(InitThing init)
    {
        Context.Skip("CBattleEngine::Init: sounds and particle descriptors");
        ConfigurationId = init.ConfigurationId;
        Configuration = null;
        UpdateConfiguration();
        Context.Skip("CBattleEngineWalkerPart and CBattleEngineJetPart");
        Life = Configuration!.Life;
        Energy = Configuration.Energy;
        if (init.PlaneMode != 0)
        {
            State = BattleEngineState.Jet;
            Shields = 0;
        }
        else
        {
            State = BattleEngineState.Walker;
            Shields = Energy;
        }

        init.InitCst.MaxCollision = CollisionLevel.ApproxGeometryShapes; // not multiplayer
        bool muspell = init.Allegiance == 1;
        RenderThing = MakeMeshRenderThing(System.Text.Encoding.ASCII.GetBytes(muspell ? "m_be1" : "f_be1"));
        OtherMeshRenderThing = MakeMeshRenderThing(System.Text.Encoding.ASCII.GetBytes(muspell ? "m_be2" : "f_be2"));
        ThrusterFrames = RenderThing!.CountEmitters("Thruster");
        EngineFrames = OtherMeshRenderThing!.CountEmitters("Engine");
        if (AnimModeByName("LegMotion") != -1)
        {
            Context.Skip("CMCBattleEngine (the walker's motion controller)");
        }

        Visible = false;
        base.Init(init);
        Context.Skip("CBattleEngine::SetCollisionShape, the cockpit, the radar warning receiver and the stores");
        Zoom = 1.0f;
        PoweredUp = true;
        ShieldRegeneration = 0;
        Context.Skip("CBattleEngine::MassiveHackPutUsInRightMesh");
        Context.Events.AddEvent(BattleEngineEvent.CalcUnitOverCrosshair, this,
            Context.Events.Time + (Context.Random.FloatRandom() * 0.2f) + 0.1f);
        HandleAutoAim(null);
    }

    /// <summary>CBattleEngine::CalcUnitOverCrossHair: the target search, then (from the event) the next search.</summary>
    public void CalcUnitOverCrossHair(ScheduledEvent? e)
    {
        if (Player is not null)
        {
            Context.Skip("CBattleEngine::CalcUnitOverCrossHair's line test");
        }

        if (e is not null)
        {
            Context.Events.AddEvent(BattleEngineEvent.CalcUnitOverCrosshair, this,
                Context.Events.Time + (Context.Random.FloatRandom() * 0.2f) + 0.1f, EventPriority.StartOfFrame, null, e);
        }
    }

    /// <summary>CBattleEngine::HandleAutoAim: the auto-aim target search, then the next search.</summary>
    public void HandleAutoAim(ScheduledEvent? e)
    {
        // GAME.GetAllowedAutoAim(): TRUE from CGame::InitRestartLoop (game.cpp:316) until a script turns it off; read
        // as TRUE until CGame carries the flag.
        Context.Skip("GAME.GetAllowedAutoAim read as TRUE");

        Context.Skip("CBattleEngine::HandleAutoAim's target search (weapons and map-who)");
        Context.Events.AddEvent(BattleEngineEvent.HandleAutoAim, this,
            Context.Events.Time + (Context.Random.FloatRandom() * 0.1f) + 0.2f, EventPriority.StartOfFrame, null, e);
    }

    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case BattleEngineEvent.CalcUnitOverCrosshair:
                CalcUnitOverCrossHair(e as ScheduledEvent);
                break;
            case BattleEngineEvent.HandleAutoAim:
                HandleAutoAim(e as ScheduledEvent);
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }
}
