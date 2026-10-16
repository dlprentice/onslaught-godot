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
public sealed partial class BattleEngine(ThingContext context) : Unit(context, Oid.BattleEngine)
{
    /// <summary>
    /// CBattleEngine::Hit (00407350): a stuck contact leaves the safe place; dying, it explodes on a unit, tree or
    /// walkable thing; CUnit's; then, stuck, it stands on a walkable thing and goes back to its current safe position,
    /// carried along by the other thing's velocity since then.
    /// </summary>
    public override void Hit(Thing other, Collision.CollisionReport? report)
    {
        if (report!.Stuck)
        {
            InSafeCollisionPlace = false;
        }

        if (IsDying && other.IsA(ThingType.Unit | ThingType.Tree | ThingType.CanBeWalkedOn))
        {
            throw new NotSupportedException("CBattleEngine::Hit: Explode (a dying battle engine's crash) is not ported");
        }

        base.Hit(other, report);
        if (report.Stuck)
        {
            throw new NotSupportedException("CBattleEngine::Hit: the stuck return to a safe position (other things' GetVelocity) is not ported");
        }
    }

    /// <summary>GetMaxVelocity (slot 15).</summary>
    public override float GetMaxVelocity() => 35.0f;


    public int ConfigurationId;
    public BattleEngineData? Configuration;
    public BattleEngineState State;
    public bool Visible;
    public RenderThing? OtherMeshRenderThing;

    /// <summary>mCockpit: the cockpit drawn in first-person view.</summary>
    public Cockpit? Cockpit;
    public int ThrusterFrames;
    public int EngineFrames;
    public float Zoom;
    public bool PoweredUp;

    /// <summary>mMotionController: the walker's leg motion (made when the mesh has a LegMotion animation).</summary>
    public MCBattleEngine? MotionController;

    /// <summary>The player controlling it (CBattleEngine::mPlayer), set by CGame::PostLoadProcess.</summary>
    public object? Player;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.BattleEngine | ThingType.GroundUnit
        | ThingType.AirUnit | ThingType.Mech | ThingType.CanDestroyTrees | ThingType.Vehicle);

    /// <summary>FullHealth: energy and life at their configuration's values.</summary>
    public bool FullHealth() => !(Energy < Configuration!.Energy || Life < Configuration.Life);

    /// <summary>NeedsRearm: a store below its configuration's value (the stores are not ported: counted, none).</summary>
    public bool NeedsRearm()
    {
        Context.Skip("CBattleEngine::NeedsRearm: the stores are not ported (read as full)");
        return false;
    }

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
        InitMovement();
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
        RenderThing = MakeMeshRenderThing(System.Text.Encoding.ASCII.GetBytes(muspell ? "m_be1.msh" : "f_be1.msh"));
        OtherMeshRenderThing = MakeMeshRenderThing(System.Text.Encoding.ASCII.GetBytes(muspell ? "m_be2.msh" : "f_be2.msh"));
        ThrusterFrames = RenderThing!.CountEmitters("Thruster");
        EngineFrames = OtherMeshRenderThing!.CountEmitters("Engine");
        if (AnimModeByName("LegMotion") != -1)
        {
            MotionController = new MCBattleEngine(Context, this);
            MotionController.SetMechParameters(0.0f, 400.0f, 1.0f, 0.4f, 0.9f, 0.05f, 2);
            if (Context.Game?.CurrentLevel is 231 or 232 or 331 or 221 or 222 or 524 or 523 or 332)
            {
                MotionController.AnimationBlend = 1.0f; // "SRG massive hack": legs through the carrier on those levels
            }
        }

        Visible = false;
        base.Init(init);
        SetCollisionShape();
        Context.Skip("CBattleEngine::Init: the radar warning receiver and the stores");
        Cockpit = new Cockpit(Context, this, Configuration?.Cockpit is { } cockpit && Context.Meshes.TryGetValue(System.Text.Encoding.ASCII.GetString(cockpit), out Mesh.MeshData? cm) ? cm : null);
        Zoom = 1.0f;
        PoweredUp = true;
        ShieldRegeneration = 0;
        InitMovementAfterUnit();
        Context.Skip("CBattleEngine::MassiveHackPutUsInRightMesh");
        Context.Events.AddEvent(BattleEngineEvent.CalcUnitOverCrosshair, this,
            Context.Events.Time + (Context.Random.FloatRandom() * 0.2f) + 0.1f);
        HandleAutoAim(null);
    }

    /// <summary>SetCollisionShape (004063b0): a cylinder of the radius and half the centre-of-gravity height.</summary>
    public void SetCollisionShape() => Seeker!.SetShape(new Collision.CylinderVolume(GetRadius(), COfGHeight() * 0.5f));

    /// <summary>mWlcr: the crosshair's last line test (CalcUnitOverCrossHair with its data updated).</summary>
    public World.WorldLineReport Wlcr { get; } = new();

    /// <summary>mCurrentUnitOverCrosshair: the unit under the crosshair within the weapon's range.</summary>
    public ActiveReader<Unit> CurrentUnitOverCrosshair { get; } = new();

    /// <summary>mCurrentUnitOverCrosshairRegardlessOfRange.</summary>
    public ActiveReader<Unit> CurrentUnitOverCrosshairRegardlessOfRange { get; } = new();

    /// <summary>mAutoAimYawOffset / mAutoAimPitchOffset: the aim's turn from the view.</summary>
    public float AutoAimYawOffset, AutoAimPitchOffset;

    /// <summary>GetCurrentWeapon (0040c4d0..): the walker part's, or the jet part's (not ported).</summary>
    public Weapon? GetCurrentWeapon() => State == BattleEngineState.Jet
        ? throw new NotSupportedException("CBattleEngineJetPart::GetCurrentWeapon (the jet part is not ported)")
        : WalkerPart?.GetCurrentWeapon();

    /// <summary>
    /// CBattleEngine::CalcUnitOverCrossHair (0040acc0): a 1000-unit line from the player's view through the aim offsets
    /// (FindFirstThingToHitLine: the map's water flag, mesh collision or the outer spheres, not trees or rounds); a unit
    /// hit (not a dead building) is the result when nearer than the weapon's actual range (1000 without one), and with
    /// the data updated the report and both crosshair units are kept. From the event, the next search in 0.1..0.3 s.
    /// </summary>
    public Unit? CalcUnitOverCrossHair(ScheduledEvent? e, bool meshCollision, bool updateData)
    {
        Unit? result = null;
        if (Player is Game.Player player)
        {
            if (updateData)
            {
                CurrentUnitOverCrosshair.SetReader(null);
                CurrentUnitOverCrosshairRegardlessOfRange.SetReader(null);
            }

            FMatrix autoAim = FMatrix.FromYawPitchRoll(AutoAimYawOffset, AutoAimPitchOffset, 0.0f);
            float range = GetCurrentWeapon()?.GetActualMaxRange(Pos) ?? 0.0f;
            if (range <= 0.0f)
            {
                range = 1000.0f;
            }

            Game.Camera? camera = Context.Game?.GetCurrentCamera(player.Number - 1);
            FVector pos = camera?.GetPos() ?? new FVector(0, 0, 0);
            FMatrix ori = (camera?.GetOrientation() ?? FMatrix.Identity).Multiply(autoAim);
            FVector end = pos + ori.Transform(new FVector(0.0f, 1000.0f, 0.0f));
            World.WorldLineReport wlcr = updateData ? Wlcr : new World.WorldLineReport();
            if (World.WorldLineQuery.FindFirstThingToHitLine(Context, pos, end, this, wlcr, false, meshCollision,
                    meshCollision ? (int)CollisionLevel.Mesh : (int)CollisionLevel.OuterSphere, ThingType.Tree + ThingType.Ammunition, true)
                == World.LineHitType.Thing)
            {
                if (wlcr.HitThing is Unit unit && unit.IsA(ThingType.Unit) && (!unit.IsA(ThingType.Building) || unit.Life > 0.0f))
                {
                    if (range > wlcr.DistToImpact)
                    {
                        result = unit;
                    }

                    if (updateData)
                    {
                        CurrentUnitOverCrosshairRegardlessOfRange.SetReader(unit);
                    }
                }
            }
        }

        if (e is not null)
        {
            Context.Events.AddEvent(BattleEngineEvent.CalcUnitOverCrosshair, this,
                Context.Events.Time + (Context.Random.FloatRandom() * 0.2f) + 0.1f, EventPriority.StartOfFrame, null, e);
        }

        return result;
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
                CurrentUnitOverCrosshair.SetReader(CalcUnitOverCrossHair(e as ScheduledEvent, true, true));
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
