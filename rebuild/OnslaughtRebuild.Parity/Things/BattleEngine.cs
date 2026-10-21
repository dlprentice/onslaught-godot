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
/// CBattleEngine: the player's machine. Walker/jet movement, morph transitions, weapons, skimming and death have
/// production owners; special moves and device audio retain explicit boundaries.
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
            Explode();
            AddShutdownEvent();
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
    /// <summary>mCachedRadarWarningReceiver; owned equipment, released before the unit's destructor.</summary>
    public RadarWarningReceiver? RadarWarningReceiver { get; private set; }
    /// <summary>mPlayIncomingMissileSound: the receiver sets it; HandleSounds consumes it.</summary>
    public bool PlayIncomingMissileSound;
    /// <summary>mLastIncomingMissileVoice (+0x610): the next allowed spoken warning time, distinct from the radar beep.</summary>
    public float LastIncomingMissileVoice;
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
        Context.Skip("CBattleEngine::Init: the stores");
        Cockpit = new Cockpit(Context, this, Configuration?.Cockpit is { } cockpit && Context.Meshes.TryGetValue(System.Text.Encoding.ASCII.GetString(cockpit), out Mesh.MeshData? cm) ? cm : null);
        LastIncomingMissileVoice = -20.0f; // Init00405635, before radar initialization.
        // 004055dc constructs Cockpit before 00405710 initializes the radar. Both queue end-of-frame events.
        RadarWarningReceiver = new RadarWarningReceiver(Context, Context.World.Set110, () => PlayIncomingMissileSound = true);
        RadarWarningReceiver.Init(new InitEquipment { AttachedTo = this }, 80.0f, 0.03f);
        Zoom = 1.0f;
        PoweredUp = true;
        ShieldRegeneration = 0;
        InitMovementAfterUnit();
        PutUsInRightMesh();
        Context.Events.AddEvent(BattleEngineEvent.CalcUnitOverCrosshair, this,
            Context.Events.Time + (Context.Random.FloatRandom() * 0.2f) + 0.1f);
        HandleAutoAim(null);
    }

    /// <summary>SetCollisionShape (004063b0): a cylinder of the radius and half the centre-of-gravity height.</summary>
    public void SetCollisionShape() => Seeker!.SetShape(new Collision.CylinderVolume(GetRadius(), COfGHeight() * 0.5f));

    protected override void Deleted()
    {
        // Equipment precedes BE parts; the attached controller/current mesh follow ComplexThing's script/animation.
        RadarWarningReceiver?.Delete();
        RadarWarningReceiver = null;
        DeleteBattleEngineParts();
        base.Deleted();
    }

    /// <summary>mWlcr: the crosshair's last line test (CalcUnitOverCrossHair with its data updated).</summary>
    public World.WorldLineReport Wlcr { get; } = new();

    /// <summary>mCurrentUnitOverCrosshair: the unit under the crosshair within the weapon's range.</summary>
    public ActiveReader<Unit> CurrentUnitOverCrosshair { get; } = new();

    /// <summary>mCurrentUnitOverCrosshairRegardlessOfRange.</summary>
    public ActiveReader<Unit> CurrentUnitOverCrosshairRegardlessOfRange { get; } = new();

    /// <summary>mAutoAimYawOffset / mAutoAimPitchOffset: the aim's turn from the view.</summary>
    public float AutoAimYawOffset, AutoAimPitchOffset;

    /// <summary>GetCurrentWeapon (0040c4d0..): the selected walker or jet part's weapon.</summary>
    public Weapon? GetCurrentWeapon() => State == BattleEngineState.Jet
        ? JetPart?.GetCurrentWeapon()
        : WalkerPart?.GetCurrentWeapon();

    /// <summary>
    /// CBattleEngine::CalcUnitOverCrossHair (0040acc0): a 1000-unit line from the player's view through the aim offsets
    /// (FindFirstThingToHitLine: the map's water flag, mesh collision or the outer spheres, not trees or rounds); a unit
    /// hit (not a dead building) is the result when nearer than the weapon's actual range (1000 without one), and with
    /// the data updated the report and both crosshair units are kept. From the event, the next search in 0.1..0.3 s.
    /// </summary>
    /// <summary>
    /// CalcUnitOverCrossHair's (view * autoAim) * FVector(0, 1000, 0), inlined: only the product's Y column is used, each
    /// element's three terms summed in retail's ranked order, (k2 + k1) + k0 in row 0 and (k0 + k2) + k1 in rows 1 and 2
    /// (0040ada5..0040aec7, B's patch 0024), then scaled by 1000 (005d8c54). The written order ended win1 P4387's line
    /// one ulp short of retail's 44896c02 4447e6c2 42acaf32.
    /// </summary>
    private static FVector CrosshairDirection(FMatrix view, FMatrix autoAim)
    {
        float k0, k1, k2;
        k0 = view.Row0.X * autoAim.Row0.Y;
        k1 = view.Row0.Y * autoAim.Row1.Y;
        k2 = view.Row0.Z * autoAim.Row2.Y;
        float x = (k2 + k1) + k0;
        k0 = view.Row1.X * autoAim.Row0.Y;
        k1 = view.Row1.Y * autoAim.Row1.Y;
        k2 = view.Row1.Z * autoAim.Row2.Y;
        float y = (k0 + k2) + k1;
        k0 = view.Row2.X * autoAim.Row0.Y;
        k1 = view.Row2.Y * autoAim.Row1.Y;
        k2 = view.Row2.Z * autoAim.Row2.Y;
        float z = (k0 + k2) + k1;
        return new FVector(x * 1000.0f, y * 1000.0f, z * 1000.0f);
    }

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

            // FMatrix(yaw, pitch, roll) out of line, then the inline product: win1 P4387's line end (44896c02 4447e6c1
            // 42acaf32, the port = retail on the distance) needs both; the inline constructor's matrix ends one ulp short.
            FMatrix autoAim = FMatrix.MakeRotationOutOfLine(AutoAimYawOffset, AutoAimPitchOffset, 0.0f);
            float range = GetCurrentWeapon()?.GetActualMaxRange(Pos) ?? 0.0f;
            if (range <= 0.0f)
            {
                range = 1000.0f;
            }

            Game.Camera? camera = Context.Game?.GetCurrentCamera(player.Number - 1);
            FVector pos = camera?.GetPos() ?? new FVector(0, 0, 0);
            FVector end = pos + CrosshairDirection(camera?.GetOrientation() ?? FMatrix.Identity, autoAim);
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

    /// <summary>
    /// CBattleEngine::HandleAutoAim (0040b6d0): with a smart weapon, the enemy unit (or lockable enemy feature) within the
    /// weapon's ranges whose direction is nearest the battle engine's heading, within 0.2 rad of yaw plus pitch (0.8 for a
    /// predicting weapon, 0.2 for a target that hardly moves); units the auto-aim may not take (aircraft without
    /// prediction, big, inactive, with a nexus or weak point, not lockable) are left; stealth shortens the range and
    /// being off the scanner halves it. A building is dropped, and a target the line of sight does not reach first. Then
    /// the next search in 0.2..0.3 s.
    /// </summary>
    public void HandleAutoAim(ScheduledEvent? e)
    {
        Weapon? weapon = GetCurrentWeapon();
        FVector forward = Orientation.Transform(new FVector(0, 1, 0));
        float currentYaw = WeaponMaths.Azimuth(Context, forward);
        float currentPitch = WeaponMaths.Elevation(Context, forward);
        float minRange = weapon?.GetMinRange(Pos) ?? 0.0f;
        float maxRange = weapon?.GetMaxRange(Pos) ?? 0.0f;
        AutoAimTarget.SetReader(null);

        // GAME.GetAllowedAutoAim(): TRUE from CGame::InitRestartLoop (game.cpp:316) until a script turns it off; read
        // as TRUE until CGame carries the flag.
        Context.Skip("GAME.GetAllowedAutoAim read as TRUE");
        if (weapon is not null && weapon.IsSmart())
        {
            float range = maxRange;
            if (range <= 0.0f)
            {
                range = 1000.0f;
                maxRange = 1000.0f;
            }

            float bestAngleDiff = weapon.CanPredict() ? 0.8f : 0.2f;
            for (MapWhoEntry? entry = Context.MapWho.GetFirstEntryWithinRadius(Pos, range); entry is not null;
                 entry = Context.MapWho.GetNextEntryWithinRadius())
            {
                Thing thing = entry.Thing;
                FVector targetPos;
                float distSq;
                float tempMaxRange = maxRange;
                if (thing.IsA(ThingType.Unit) && thing is Unit unit)
                {
                    if (!IsTargetAlligence(unit.Allegiance) || unit.IsDying)
                    {
                        continue;
                    }

                    bool canAutoAim = !(unit.IsA(ThingType.AirUnit) && !weapon.CanPredict());
                    if (unit.UnitData is { Big: not 0 } || !unit.Active || unit.Unknown228Nexus || unit.Unknown22CWeakPoint
                        || unit.UnitData is { CanBeLocked: 0 })
                    {
                        canAutoAim = false;
                    }

                    if (!canAutoAim)
                    {
                        continue;
                    }

                    targetPos = unit.GetTargetablePos();
                    FVector diffU = targetPos - Pos;
                    distSq = diffU.MagnitudeSq();
                    // Retail multiplies by its 0.01f constant (005d85fc), not a division by 100.
                    tempMaxRange = maxRange * (1.0f - (unit.GetStealth() * 0.01f));
                }
                else if (thing.IsA(ThingType.Feature) && thing is Feature feature)
                {
                    if (feature.Data?.Lockable is null or 0 || !IsTargetAlligence(feature.Allegiance))
                    {
                        continue;
                    }

                    targetPos = feature.GetCentrePos();
                    FVector d = targetPos - Pos;
                    float zSquared = d.Z * d.Z;
                    distSq = (zSquared + (d.X * d.X)) + (d.Y * d.Y);
                }
                else
                {
                    continue;
                }

                if (!(minRange * minRange <= distSq && tempMaxRange * tempMaxRange >= distSq))
                {
                    continue;
                }

                FVector diff = targetPos - Pos;
                float yawDiff = -Unit.AngleDifference(currentYaw, WeaponMaths.Azimuth(Context, diff));
                float pitchDiff = -Unit.AngleDifference(currentPitch, WeaponMaths.Elevation(Context, diff));
                float angleDiff = MathF.Abs(yawDiff) + MathF.Abs(pitchDiff);
                float tempAngleDiff = bestAngleDiff;
                if (thing.GetVelocity().MagnitudeSq() < 0.1f * 0.1f && tempAngleDiff > 0.2f)
                {
                    tempAngleDiff = 0.2f;
                }

                if (angleDiff < tempAngleDiff)
                {
                    AutoAimTarget.SetReader(thing);
                    bestAngleDiff = angleDiff;
                }
            }
        }

        if (AutoAimTarget.ToRead is { } building && building.IsA(ThingType.Building))
        {
            AutoAimTarget.SetReader(null);
        }

        if (AutoAimTarget.ToRead is { } target)
        {
            FVector targetPos = target is Unit tu && target.IsA(ThingType.Unit) ? tu.GetTargetablePos() : target.GetCentrePos();
            var wlcr = new World.WorldLineReport();
            if (World.WorldLineQuery.FindFirstThingToHitLine(Context, Pos, targetPos, this, wlcr, false, true,
                    (int)CollisionLevel.ApproxGeometryShapes, ThingType.Ammunition, true) == World.LineHitType.Thing)
            {
                if (!ReferenceEquals(wlcr.HitThing, target))
                {
                    AutoAimTarget.SetReader(null);
                }
            }
            else
            {
                AutoAimTarget.SetReader(null);
            }
        }

        Context.Events.AddEvent(BattleEngineEvent.HandleAutoAim, this,
            Context.Events.Time + (Context.Random.FloatRandom() * 0.1f) + 0.2f, EventPriority.StartOfFrame, null, e);
    }

    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case BattleEngineEvent.BecomeJet:
                if (State == BattleEngineState.MorphingIntoJet)
                {
                    TransformStartTime = Context.Events.Time;
                    State = BattleEngineState.Jet;
                    SetCollisionShape();
                }
                break;
            case BattleEngineEvent.BecomeWalker:
                State = BattleEngineState.Walker;
                SetCollisionShape();
                break;
            case UnitEvent.IncomingMissileWarning:
                // 0040c1ea..0040c263: strict last<time, but FCOMP's C0 also admits unordered. Equal times
                // suppress the warning. This event is independent of HandleSounds' incoming-missile beep.
                if (!(LastIncomingMissileVoice >= Context.Events.Time))
                {
                    PlayHudSample("hud_incoming_missile");
                    LastIncomingMissileVoice = (float)Numerics.X87.Add(Context.Events.Time, 2.0f);
                }
                break;
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
