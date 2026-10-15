// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// What the Battle Engine's last event-path crosshair line struck, as
/// <c>CWorldLineColReport::mHitType</c> records it: 0 nothing, 1 ground,
/// 3 a thing (<c>CWorld::FindFirstThingToHitLine</c>, <c>0x0050b030</c>).
/// </summary>
public enum Level100CrosshairHitKind : byte
{
    Nothing = 0,
    Ground = 1,
    Thing = 3,
}

/// <summary>
/// The player Battle Engine's targeting state: the crosshair units
/// (<c>+0x4c8</c>, <c>+0x4cc</c>), the retained event-path line report
/// (<c>mWlcr</c>) and the lock sets.
/// </summary>
public sealed record Level100BattleEngineTargetingSnapshot(
    Level100ActorId? CrosshairUnit,
    Level100ActorId? CrosshairUnitRegardlessOfRange,
    Level100CrosshairHitKind CrosshairHitKind,
    int CrosshairHitDistanceMillimeters,
    Level100PlayerLockSetSnapshot Locks)
{
    public static Level100BattleEngineTargetingSnapshot Initial { get; } = new(
        null, null, Level100CrosshairHitKind.Nothing, 0,
        new Level100PlayerLockSetSnapshot([], [], 0, 0));
}

public sealed partial class Simulation
{
    private Level100ActorId? _crosshairUnit;
    private Level100ActorId? _crosshairUnitRegardlessOfRange;
    private Level100CrosshairHitKind _crosshairHitKind;
    private int _crosshairHitDistanceMillimeters;
    private Level100PlayerLocks _playerLocks = null!;
    private readonly RetailBattleEngineShake _shake = new();

    /// <summary>
    /// The tail of <c>CBattleEngine::Init</c> (<c>0x00404dd0</c>,
    /// <c>BattleEngine.cpp:350-352</c>): one draw and event 6002, then
    /// <c>HandleAutoAim(NULL)</c>, which files 6003 with its own draw.
    /// </summary>
    /// <remarks>
    /// Where this Init sits in the level's construction order relative to
    /// the other constructors' draws is the RE lane's open first-flush
    /// question; Core runs it once the level's actors and mechanics exist.
    /// </remarks>
    private void InitializeBattleEngineRefreshEvents(RetailEventScheduler events, Func<int> nextDraw)
    {
        if (events.FrameCount != _retailEventFrameCount)
        {
            throw new InvalidOperationException(
                "The Battle Engine must be constructed on the level event clock.");
        }

        events.AddEvent(
            RetailBattleEngineRefresh.CrosshairEvent,
            Level100ActorMechanics.BattleEngineListener,
            RetailBattleEngineRefresh.CrosshairDueTime(nextDraw(), events.Time),
            RetailEventPriority.StartOfFrame);
        FileAutoAim(events, nextDraw, reuseHandle: -1);
    }

    /// <summary><c>CBattleEngine::HandleEvent</c> for 6002 and 6003 (<c>0x0040c180</c>).</summary>
    private void HandleBattleEngineEvent(RetailEventScheduler events, RetailEventDispatch dispatch)
    {
        if (Level100ActorMechanics.IsScriptListener(dispatch.Listener))
        {
            HandleScriptEvent(dispatch);
            return;
        }

        if (Level100ActorMechanics.IsPlayerRoundListener(dispatch.Listener))
        {
            HandlePlayerRoundEvent(events, dispatch);
            return;
        }

        if (dispatch.Listener == Level100ActorMechanics.MissilePodListener)
        {
            if (dispatch.EventNum != Level100ActorMechanics.WeaponBurstEvent)
            {
                throw new InvalidOperationException(
                    $"Unadmitted Missile Pod event {dispatch.EventNum}.");
            }
            HandleMissilePodBurst(events, dispatch);
            return;
        }

        switch (dispatch.EventNum)
        {
            case RetailBattleEngineRefresh.CrosshairEvent:
            {
                Level100ActorId? unit = CalcUnitOverCrossHair(updateData: true);
                // 0x0040b091: the requeue closes CalcUnitOverCrossHair; HandleEvent
                // then stores its result in +0x4c8.
                events.AddEvent(
                    RetailBattleEngineRefresh.CrosshairEvent,
                    dispatch.Listener,
                    RetailBattleEngineRefresh.CrosshairDueTime(
                        _level100ActorMechanics.NextReleasedRandom(), events.Time),
                    RetailEventPriority.StartOfFrame,
                    reuseHandle: dispatch.Handle);
                _crosshairUnit = unit;
                return;
            }
            case RetailBattleEngineRefresh.AutoAimEvent:
                HandleAutoAim(events, dispatch.Handle);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unadmitted Battle Engine event {dispatch.EventNum}.");
        }
    }

    /// <summary>
    /// A script's INIT_SCRIPT (2001) or <c>ready()</c> (2003), delivered in the
    /// level event manager's flush: a script carrier's runs its level script,
    /// a thing's its own. Their commands settle before the next delivery.
    /// </summary>
    private void HandleScriptEvent(RetailEventDispatch dispatch)
    {
        if (Level100ActorMechanics.IsCarrierScriptListener(dispatch.Listener))
        {
            int row = Level100ActorMechanics.CarrierScriptRow(dispatch.Listener);
            string script = Level100ScriptCarriers.For(_worldNumber).Single(carrier => carrier.LevelRow == row).ScriptName;
            switch (script)
            {
                case "LevelScript":
                    _level100Mission.RunInit();
                    break;
                case "Setup":
                    _level100ActorScripts.RunSetupInit();
                    break;
                default:
                    _level100ActorScripts.RunCarrierInit(script);
                    break;
            }
        }
        else
        {
            Level100ActorId actorId = Level100ActorMechanics.ScriptListenerActor(dispatch.Listener);
            if (dispatch.EventNum == Level100ActorMechanics.InitScriptEvent)
            {
                _level100ActorScripts.RunScriptInit(actorId);
            }
            else
            {
                _level100ActorScripts.DispatchReady(actorId);
            }
        }

        PumpLevel100EventBus();
    }

    /// <summary>
    /// <c>HandleAutoAim</c> (<c>0x0040b6d0</c>, <c>BattleEngine.cpp:2446-2613</c>).
    /// Auto-aim is allowed for the whole level: <c>CGame::InitRestartLoop</c>
    /// sets <c>GAME+0x20</c> to 1 (<c>0x0046c4c2</c>) and only the pause
    /// menu's option changes it, which the rebuild's pause menu does not
    /// offer. The call therefore always ends with one draw and a new 6003.
    /// </summary>
    /// <remarks>
    /// The Smart-weapon candidate search and the offsets it drives are not
    /// implemented yet: <c>mAutoAimTarget</c> stays null, which is exactly
    /// retail for the non-Smart Pulse Cannon Pod and Missile Pod, and not yet
    /// retail for the Smart Vulcans. The draw and requeue are exact.
    /// </remarks>
    private void HandleAutoAim(RetailEventScheduler events, int reuseHandle) =>
        FileAutoAim(events, _level100ActorMechanics.NextReleasedRandom, reuseHandle);

    private static void FileAutoAim(RetailEventScheduler events, Func<int> nextDraw, int reuseHandle) =>
        events.AddEvent(
            RetailBattleEngineRefresh.AutoAimEvent,
            Level100ActorMechanics.BattleEngineListener,
            RetailBattleEngineRefresh.AutoAimDueTime(nextDraw(), events.Time),
            RetailEventPriority.StartOfFrame,
            reuseHandle: reuseHandle);

    /// <summary>
    /// <c>CalcUnitOverCrossHair</c> (<c>0x0040acc0</c>,
    /// <c>BattleEngine.cpp:2278-2344</c>) on its event path: clear both
    /// crosshair readers, cast the 1000-unit view line with mesh collision
    /// and retain its report, then return the struck unit when the current
    /// weapon's range exceeds the hit distance.
    /// </summary>
    private Level100ActorId? CalcUnitOverCrossHair(bool updateData)
    {
        if (updateData)
        {
            _crosshairUnit = null;
            _crosshairUnitRegardlessOfRange = null;
        }

        (SimVector3 start, SimVector3 end) = CrosshairLine();
        bool struck = _level100Destruction.TryFindFirstThingOnLine(
            start,
            end,
            IsCrosshairLineCandidate,
            out Level100ContactHit hit);
        Level100CrosshairHitKind kind = !struck
            ? Level100CrosshairHitKind.Nothing
            : hit.SurfaceKind == Level100ContactSurfaceKind.Terrain
                ? Level100CrosshairHitKind.Ground
                : Level100CrosshairHitKind.Thing;
        // The line is 1,000,000 mm long, so its parts-per-million time is the
        // distance in millimetres.
        int distance = struck ? hit.TimePartsPerMillion : 0;
        if (updateData)
        {
            _crosshairHitKind = kind;
            if (struck)
            {
                _crosshairHitDistanceMillimeters = distance;
            }
        }

        if (kind != Level100CrosshairHitKind.Thing)
        {
            return null;
        }

        var unit = new Level100ActorId(hit.ActorId);
        Level100ActorSnapshot actor = _level100Actors.GetActor(unit);
        Level100ThingClass thingClass = Level100ThingClasses.Of(actor.DefinitionName);
        // A lifeless building is not a crosshair unit (BattleEngine.cpp:2327).
        if (!thingClass.IsUnit ||
            (thingClass.IsBuilding && actor.Lifecycle != Level100ActorLifecycle.Alive))
        {
            return null;
        }

        if (updateData)
        {
            _crosshairUnitRegardlessOfRange = unit;
        }

        long range = (long)(CurrentWeaponActualMaxRange() * 1000.0f);
        return range > distance ? unit : null;
    }

    /// <summary>
    /// The candidate filter the crosshair line applies: dying things that are
    /// not buildings are skipped (<c>0x0050b030</c>). The Battle Engine,
    /// trees and rounds are never contact actors here.
    /// </summary>
    private static bool IsCrosshairLineCandidate(
        Level100ActorSnapshot actor,
        Level100ContactDefinition definition) =>
        actor.Lifecycle is not (Level100ActorLifecycle.StartedDying or
            Level100ActorLifecycle.DiedAwaitingShutdown) ||
        Level100ThingClasses.Of(actor.DefinitionName).IsBuilding;

    /// <summary>
    /// The view line: 1000 units from the view point along the view
    /// orientation times the auto-aim matrix. Core has no camera, so the
    /// Battle Engine's position and facing stand in for the view, as the
    /// launch correction has always assumed; the auto-aim offsets are zero
    /// until its search is implemented.
    /// </summary>
    private (SimVector3 Start, SimVector3 End) CrosshairLine()
    {
        const int LineLengthMillimeters = 1_000_000;
        (int yawSin, int yawCos) = FixedSinCos(_facingYawMicroRad);
        (int pitchSin, int pitchCos) = FixedSinCos(_facingPitchMicroRad);
        int horizontalLength = DivideRoundNearest(
            (long)pitchCos * LineLengthMillimeters,
            FixedTrigScale);
        var start = new SimVector3(
            PlayerPosition.X,
            PlayerElevationMillimeters,
            PlayerPosition.Z);
        var end = new SimVector3(
            checked(start.X + DivideRoundNearest(
                -(long)yawSin * horizontalLength,
                FixedTrigScale)),
            checked(start.Y + DivideRoundNearest(
                -(long)pitchSin * LineLengthMillimeters,
                FixedTrigScale)),
            checked(start.Z + DivideRoundNearest(
                (long)yawCos * horizontalLength,
                FixedTrigScale)));
        return (start, end);
    }

    /// <summary>
    /// <c>GetCurrentWeapon</c> (<c>BattleEngine.cpp:2740-2746</c>): the jet
    /// part's weapon only in the jet state, the walker part's otherwise,
    /// including both morphs.
    /// </summary>
    private Level100MissionWeapon RetailCurrentWeapon =>
        _mode == VehicleMode.Jet && _transition == VehicleTransition.None
            ? _level100PlayerWeapons.JetSelectedWeapon
            : _level100PlayerWeapons.WalkerSelectedWeapon;

    private float CurrentWeaponActualMaxRange()
    {
        Level100MissionWeapon weapon = RetailCurrentWeapon;
        return RetailBattleEngineRefresh.ActualMaxRange(
            weapon,
            weapon == Level100MissionWeapon.PulseCannonPod &&
                _level100PlayerWeapons.PulseChargeLevel >= 1);
    }

    /// <summary>
    /// <c>CBattleEngine::HandleLocks</c> (<c>0x00406560</c>,
    /// <c>BattleEngine.cpp:586-760</c>), called from <c>Move</c> on every
    /// update while the Battle Engine is not dying. Every lock parameter comes
    /// from the mode the current charge selects; the loss and acquisition cones
    /// use <c>CWeaponLockDeflection</c>, and every Level 100 mode uses direct
    /// lock mode 0 (the RE lane's Q5/Q6 answers and the final-wave contract).
    /// </summary>
    private void HandleLocks()
    {
        if (_level100Actors.GetLifecycle(_level100PlayerActorId) != Level100ActorLifecycle.Alive)
        {
            return;
        }

        bool jetState = _mode == VehicleMode.Jet && _transition == VehicleTransition.None;
        // IsFiring (0x00414b30) asks the jet part in state 3 and the walker
        // part otherwise; of Level 100's weapons only a pod burst counts.
        if (jetState && _level100PlayerWeapons.PodIsFiring)
        {
            return;
        }

        Level100MissionWeapon weapon = RetailCurrentWeapon;
        bool loseLocks = !_level100PlayerWeapons.CanWeaponFire(weapon, walkerPart: !jetState);
        Level100LockParameters parameters = _level100PlayerWeapons.LockParameters(weapon);
        double cosine = MathF.Cos(parameters.LockDeflection);
        _playerLocks.Prune(unit =>
            !(ForwardComponentToward(unit) < cosine || loseLocks || IsDying(unit)));

        if (_playerLocks.CountLocks() >= parameters.MaxLocks || loseLocks)
        {
            return;
        }

        float now = EngineTimeSeconds;
        if (!_level100PlayerWeapons.ReadyToFire(weapon, now) ||
            parameters.LockUnitMask == 0)
        {
            // CanLock ANDs the target type with the mode's lock unit, so a
            // zero mask acquires nothing; the probe below has no side effects.
            return;
        }

        Level100ActorId? unit = _crosshairUnit ?? CalcUnitOverCrossHairOuterSphere();
        if (unit is not { } candidate || _playerLocks.Locked(candidate) ||
            !RetailBattleEngineRefresh.IsTargetAllegiance(0, _level100ActorMechanics.ScriptAllegiance(candidate) ?? -1) ||
            !CanLock(candidate, parameters.LockUnitMask))
        {
            return;
        }

        // GetStealth (slot 91, 0x004bfc60) is 0 for every Level 100 unit, so
        // the range factor 1 - stealth x 0.01 is 1.
        SimVector3 delta = DeltaToward(candidate);
        double distanceSquared = ((double)delta.X * delta.X) + ((double)delta.Y * delta.Y) +
            ((double)delta.Z * delta.Z);
        double range = parameters.LockRange * 1000.0;
        if (distanceSquared < range * range && ForwardComponentToward(candidate) > cosine)
        {
            _playerLocks.StartLock(candidate, parameters.LockTime, directLock: true, now);
        }
    }

    private SimVector3 DeltaToward(Level100ActorId unit)
    {
        SimVector3 target = _level100Actors.GetPose(unit).PositionMillimeters;
        return new SimVector3(
            target.X - PlayerPosition.X,
            target.Y - PlayerElevationMillimeters,
            target.Z - PlayerPosition.Z);
    }

    /// <summary>
    /// The normalised heading's forward component,
    /// <c>(inverse(mOrientation) × (unit.pos − mPos)).y / |…|</c>.
    /// </summary>
    private double ForwardComponentToward(Level100ActorId unit)
    {
        SimVector3 delta = DeltaToward(unit);
        double length = Math.Sqrt(((double)delta.X * delta.X) + ((double)delta.Y * delta.Y) +
            ((double)delta.Z * delta.Z));
        if (length == 0.0)
        {
            return 0.0;
        }

        FixedBodyBasis body = GetBodyBasis();
        double forward = ((double)body.ForwardX * delta.X) + ((double)body.ForwardY * delta.Y) +
            ((double)body.ForwardZ * delta.Z);
        return forward / FixedTrigScale / length;
    }

    private bool IsDying(Level100ActorId unit) =>
        _level100Actors.GetLifecycle(unit) is Level100ActorLifecycle.StartedDying or
            Level100ActorLifecycle.DiedAwaitingShutdown;

    /// <summary>
    /// <c>CWeapon::CanLock</c> (<c>0x005061f0</c>) for Level 100's things: the
    /// target must be active; no Level 100 mesh has a <c>nexus</c> or
    /// <c>weakpoint</c> part and every profile is lockable (the RE lane's Q16
    /// answer), which leaves the thing type against the mode's lock unit.
    /// </summary>
    private bool CanLock(Level100ActorId unit, uint lockUnitMask) =>
        _level100Actors.IsActive(unit) &&
        (Level100ThingClasses.Of(_level100Actors.GetActor(unit).DefinitionName).TypeMask & lockUnitMask) != 0;

    /// <summary>
    /// <c>CalcUnitOverCrossHair(NULL, FALSE, FALSE)</c>, the lock fallback: the
    /// same view line tested at <c>ECL_OUTER_SPHERE</c>. Terrain is traced
    /// first; a candidate counts when the line meets its collision sphere
    /// (centre offset from the mesh bounding box, radius R, or 0.8R for a
    /// ground vehicle) and its distance <c>|centre − start| − R</c> is strictly
    /// below the ground's. It updates nothing and draws nothing (the RE lane's
    /// Q8 and Q14 answers).
    /// </summary>
    /// <remarks>
    /// Only the four target definitions carry their bounding box in the
    /// materialized contact asset, so the base-world statics do not yet take
    /// part in this probe; the event-path line above does include them.
    /// </remarks>
    private Level100ActorId? CalcUnitOverCrossHairOuterSphere()
    {
        (SimVector3 start, SimVector3 end) = CrosshairLine();
        double groundDistance = Level100ContactMechanics.TrySweepRoundAgainstTerrain(
                new Level100Vector3(start.X, start.Z, -start.Y),
                new Level100Vector3(end.X, end.Z, -end.Y),
                0,
                out Level100ContactHit terrain)
            ? terrain.TimePartsPerMillion
            : double.PositiveInfinity;

        double lineX = end.X - (double)start.X;
        double lineY = end.Y - (double)start.Y;
        double lineZ = end.Z - (double)start.Z;
        double lineLengthSquared = (lineX * lineX) + (lineY * lineY) + (lineZ * lineZ);
        Level100ActorId? best = null;
        double bestDistance = double.PositiveInfinity;
        foreach (Level100ActorSnapshot actor in _level100Actors.Snapshot.Actors)
        {
            if (!actor.Active || actor.Lifecycle == Level100ActorLifecycle.Destroyed ||
                actor.DefinitionName is null || actor.ActorId == _level100PlayerActorId ||
                !Level100ContactCatalog.Instance.TryGetDefinition(actor.DefinitionName, out Level100ContactDefinition? definition) ||
                definition?.FloatGeometry is not { } geometry ||
                !IsCrosshairLineCandidate(actor, definition))
            {
                continue;
            }

            (double centreX, double centreY, double centreZ) = SphereCentre(actor, definition, geometry);
            double radius = BitConverter.Int32BitsToSingle(unchecked((int)geometry.BoundingBoxRadiusFloatBits)) * 1000.0;
            double testRadius = radius * BitConverter.Int32BitsToSingle(unchecked((int)geometry.PrimaryRadiusScaleFloatBits));
            double toCentreX = centreX - start.X;
            double toCentreY = centreY - start.Y;
            double toCentreZ = centreZ - start.Z;
            double along = Math.Clamp(
                ((toCentreX * lineX) + (toCentreY * lineY) + (toCentreZ * lineZ)) / lineLengthSquared,
                0.0,
                1.0);
            double offX = toCentreX - (lineX * along);
            double offY = toCentreY - (lineY * along);
            double offZ = toCentreZ - (lineZ * along);
            if ((offX * offX) + (offY * offY) + (offZ * offZ) > testRadius * testRadius)
            {
                continue;
            }

            double distance = Math.Sqrt((toCentreX * toCentreX) + (toCentreY * toCentreY) +
                (toCentreZ * toCentreZ)) - radius;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = actor.ActorId;
            }
        }

        if (best is not { } hit || !(bestDistance < groundDistance))
        {
            return null;
        }

        Level100ActorSnapshot thing = _level100Actors.GetActor(hit);
        Level100ThingClass thingClass = Level100ThingClasses.Of(thing.DefinitionName);
        if (!thingClass.IsUnit ||
            (thingClass.IsBuilding && thing.Lifecycle != Level100ActorLifecycle.Alive))
        {
            return null;
        }

        return CurrentWeaponActualMaxRange() * 1000.0f > bestDistance ? hit : null;
    }

    /// <summary>
    /// <c>CThing::Init</c>'s centre offset (<c>0x00426218-0x0042626a</c>):
    /// <c>(0, 0, bbox z)</c> for planes, vehicles and cannons, the orientation
    /// times the whole bounding-box origin for buildings.
    /// </summary>
    private static (double X, double Y, double Z) SphereCentre(
        Level100ActorSnapshot actor,
        Level100ContactDefinition definition,
        Level100ContactFloatGeometry geometry)
    {
        static double Word(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
        SimVector3 position = actor.Pose.PositionMillimeters;
        double originX = Word(geometry.BoundingBoxOriginXFloatBits) * 1000.0;
        double originY = Word(geometry.BoundingBoxOriginYFloatBits) * 1000.0;
        double originZ = Word(geometry.BoundingBoxOriginZFloatBits) * 1000.0;
        if (!Level100ThingClasses.Of(definition.Name).IsBuilding)
        {
            // Retail z is down; Core y is up.
            return (position.X, position.Y - originZ, position.Z);
        }

        // Retail (x, y, z) is Core (x, -y, z) swapped: Core x = retail x,
        // Core y = -retail z, Core z = retail y. The pose basis rows are Core.
        Level100FloatBasis3Bits basis = actor.Pose.BasisFloatBits;
        static double B(int bits) => BitConverter.Int32BitsToSingle(bits);
        double coreX = originX;
        double coreY = -originZ;
        double coreZ = originY;
        return (
            position.X + (B(basis.Row0X) * coreX) + (B(basis.Row0Y) * coreY) + (B(basis.Row0Z) * coreZ),
            position.Y + (B(basis.Row1X) * coreX) + (B(basis.Row1Y) * coreY) + (B(basis.Row1Z) * coreZ),
            position.Z + (B(basis.Row2X) * coreX) + (B(basis.Row2Y) * coreY) + (B(basis.Row2Z) * coreZ));
    }

    /// <summary>
    /// The Missile Pod's half of <c>CWeapon::Fire</c> (<c>0x00506010</c>) once
    /// the reload check has passed: stamp the reload from the burst's start,
    /// zero the burst counter and the Battle Engine's target cursor, spawn the
    /// first burst event, then set the counter to 1 and file event 5001 for
    /// the rest (the RE lane's stores contract).
    /// </summary>
    private void FireMissilePod()
    {
        float now = EngineTimeSeconds;
        _level100PlayerWeapons.StampReadyAt(Level100MissionWeapon.MissilePod, now);
        _level100PlayerWeapons.PodBurstCount = 0;
        _playerLocks.ResetCurrentTarget();
        SpawnPodBurst();
        _level100PlayerWeapons.PodBurstCount = 1;
        _level100ActorMechanics.LevelEvents.AddEventTimeFromNow(
            _level100PlayerWeapons.PodMode.BurstDelay,
            Level100ActorMechanics.WeaponBurstEvent,
            Level100ActorMechanics.MissilePodListener);
    }

    /// <summary>
    /// <c>CWeapon__HandleFireBurstEvent</c> (<c>0x00506930</c>): nothing while
    /// the owner is dying; otherwise, while the counter is below the burst size
    /// of the mode <c>+0x68</c> names, spawn, count and re-file.
    /// </summary>
    private void HandleMissilePodBurst(RetailEventScheduler events, RetailEventDispatch dispatch)
    {
        if (_level100Actors.GetLifecycle(_level100PlayerActorId) != Level100ActorLifecycle.Alive)
        {
            return;
        }

        Level100MissilePodMode mode = _level100PlayerWeapons.PodMode;
        if (_level100PlayerWeapons.PodBurstCount >= mode.BurstSize)
        {
            return;
        }

        SpawnPodBurst();
        _level100PlayerWeapons.PodBurstCount++;
        events.AddEventTimeFromNow(
            mode.BurstDelay,
            Level100ActorMechanics.WeaponBurstEvent,
            dispatch.Listener,
            reuseHandle: dispatch.Handle);
    }

    /// <summary>
    /// One Missile Pod burst event of <c>ProjectileBurst__SpawnFromCurrentPreset</c>
    /// in the RE lane's order: <c>WeaponFired</c> (store 3), the launch sound, then
    /// the single round's launch counters, scatter draws, target, <c>FireLock</c>,
    /// target binding, <c>CRound::Init</c> draw and recoil.
    /// </summary>
    private void SpawnPodBurst()
    {
        float now = EngineTimeSeconds;
        if (!_level100PlayerWeapons.WeaponFired(Level100MissionWeapon.MissilePod, jetPart: true, now))
        {
            return;
        }

        EmitWeaponFireEvent(Level100PlayerWeapon.MissilePod, 1);
        (int gun, int angleSlot) = _level100PlayerWeapons.AdvancePodLaunchCounters();
        (int yawInaccuracy, int pitchInaccuracy) = _level100ActorMechanics.NextWeaponInaccuracy(0);
        Level100ActorId? target = _playerLocks.GetCurrentTarget(now);
        if (RetailCurrentWeapon == Level100MissionWeapon.MissilePod)
        {
            _playerLocks.FireLock(target, now);
        }
        _ = _level100ActorMechanics.NextReleasedRandom();
        LaunchWalkerRound(
            Level100ProjectileKind.MicroMissile,
            Level100MissilePod.SpeedMillimetersPerTick,
            Level100MissilePod.LifetimeTicks,
            yawInaccuracy,
            pitchInaccuracy,
            gun,
            target,
            BitConverter.SingleToUInt32Bits(now),
            (FloatBitsToMicroRadians(Level100MissilePod.LaunchAngleYawBits[angleSlot]),
                FloatBitsToMicroRadians(Level100MissilePod.LaunchAnglePitchBits[angleSlot])));
        _shake.Add(_level100PlayerWeapons.PodMode.Power, _level100ActorMechanics.NextReleasedRandom);
    }

    private static int FloatBitsToMicroRadians(uint bits) =>
        (int)Math.Round(BitConverter.UInt32BitsToSingle(bits) * 1_000_000.0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// One <c>CRound::Move</c> guidance step for a seeking player round
    /// (<c>0x004d8e40</c>): release a target that is dying or deleted, then,
    /// once the age is past the seek delay, steer or drop the target at the
    /// cone. Every release by a Battle Engine's round calls <c>LockHit</c>.
    /// </summary>
    private void SteerSeekingPlayerRound(MutableProjectile round)
    {
        if (round.SeekTarget is not { } target)
        {
            return;
        }

        Level100ActorLifecycle lifecycle = _level100Actors.GetLifecycle(target);
        if (lifecycle is Level100ActorLifecycle.Destroyed or Level100ActorLifecycle.StartedDying or
            Level100ActorLifecycle.DiedAwaitingShutdown)
        {
            ReleaseSeekTarget(round);
            return;
        }

        // mTime − +0xf4 on the x87 stack against CRoundSeekDelay, strictly.
        double age = RetailFloat24.Subtract(EngineTimeSeconds, BitConverter.UInt32BitsToSingle(round.LaunchTimeBits));
        if (!(Level100MissilePod.SeekDelay < age) || !(age < 1000.0))
        {
            return;
        }

        // The shared steering law measures pitch nose-up; the Battle Engine's
        // rounds keep retail's nose-down pitch.
        int yaw = round.YawMicroRad;
        int pitch = -round.PitchMicroRad;
        if (Level100ActorMechanics.SteerTowards(
                ref yaw,
                ref pitch,
                new SimVector3(round.Position.X, round.ElevationMillimeters, round.Position.Z),
                SeekAimPoint(target),
                Level100MissilePod.TurnRateMicroRadians,
                Level100MissilePod.SeekAngleMicroRadians) == Level100ActorMechanics.SeekSteering.LeftTheCone)
        {
            ReleaseSeekTarget(round);
            return;
        }

        round.YawMicroRad = yaw;
        round.PitchMicroRad = -pitch;
    }

    private void ReleaseSeekTarget(MutableProjectile round)
    {
        _playerLocks.LockHit(round.SeekTarget);
        round.SeekTarget = null;
    }

    /// <summary>
    /// The target's aim point (slot 90): for a plane or vehicle its position
    /// plus the bounding box's height offset, as <c>GetCentrePos</c> gives it.
    /// </summary>
    private SimVector3 SeekAimPoint(Level100ActorId target)
    {
        Level100ActorSnapshot actor = _level100Actors.GetActor(target);
        SimVector3 position = actor.Pose.PositionMillimeters;
        if (actor.DefinitionName is { } name &&
            Level100ContactCatalog.Instance.TryGetDefinition(name, out Level100ContactDefinition? definition) &&
            definition?.FloatGeometry is { } geometry)
        {
            (double x, double y, double z) = SphereCentre(actor, definition, geometry);
            return new SimVector3(
                (int)Math.Round(x, MidpointRounding.AwayFromZero),
                (int)Math.Round(y, MidpointRounding.AwayFromZero),
                (int)Math.Round(z, MidpointRounding.AwayFromZero));
        }
        return position;
    }

    /// <summary>
    /// Causal-probe seam for the crosshair line: sets the Battle Engine's
    /// current yaw and pitch, which Core also uses as the view orientation.
    /// No shipped path calls this; the next <see cref="Step"/> still owns the
    /// event delivery, the line query and every later use of the report.
    /// </summary>
    internal void SetFacingForMeasurement(int yawMicroRad, int pitchMicroRad)
    {
        _facingYawMicroRad = NormalizeMicroRad(yawMicroRad);
        _facingPitchMicroRad = NormalizeMicroRad(pitchMicroRad);
        QuantizeFacingFromYaw();
    }

    /// <summary>
    /// Causal-probe seam: the launch orientation a round from this emitter
    /// would take now, from <see cref="ReticleAdjustedLaunchAngles"/>.
    /// </summary>
    internal (int YawMicroRad, int PitchMicroRad) LaunchAnglesForMeasurement(SimVector3 emitter) =>
        ReticleAdjustedLaunchAngles(emitter);

    private Level100BattleEngineTargetingSnapshot TargetingSnapshot => new(
        _crosshairUnit,
        _crosshairUnitRegardlessOfRange,
        _crosshairHitKind,
        _crosshairHitDistanceMillimeters,
        _playerLocks.Snapshot);

    private void ResetBattleEngineTargeting()
    {
        _crosshairUnit = null;
        _crosshairUnitRegardlessOfRange = null;
        _crosshairHitKind = Level100CrosshairHitKind.Nothing;
        _crosshairHitDistanceMillimeters = 0;
        _playerLocks = new Level100PlayerLocks(id => _level100Actors.GetLifecycle(id));
        _shake.Reset();
    }

    /// <summary>
    /// The tail of <c>CBattleEngine::Move</c> after the part moves:
    /// <c>UpdateRotation</c>'s shake decay (<c>BattleEngine.cpp:1222-1233</c>)
    /// and the heat-store cooling loop (<c>:1708-1718</c>). Both run on every
    /// Move, dying or not, for as long as the Battle Engine exists.
    /// </summary>
    private void AdvanceBattleEngineRotationTail()
    {
        if (_level100Actors.GetLifecycle(_level100PlayerActorId) == Level100ActorLifecycle.Destroyed)
        {
            return;
        }

        _shake.Decay();
        _level100PlayerWeapons.CoolStores();
    }
}
