// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace OnslaughtRebuild.Core;

public static class StateHasher
{
    private static readonly byte[] s_magic = Encoding.ASCII.GetBytes("ONSLAUGHT-REBUILD-STATE");

    public static string ComputeHex(WorldSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Convert.ToHexString(SHA256.HashData(GetCanonicalBytes(state))).ToLowerInvariant();
    }

    internal static byte[] GetCanonicalBytes(WorldSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(s_magic);
            if (state.Level100Actors.BaseStates.Any(item => item.State.RetailPlane is null &&
                    (item.State.RetailPoses is not null || item.State.RetailMotion is not null)))
                throw new NotSupportedException("Incomplete retail construction has no admitted hash schema.");
            // World 110's landing craft (Land sets their landing state) have
            // no hash schema yet; its fighters are already refused below.
            if (state.Level100ActorMechanics.Actors.Any(actor => actor.DropshipLandingState != 0))
                throw new NotSupportedException("A landed dropship has no admitted hash schema.");
            // A world built from a partial carry-over (lost base rows and their
            // landscape damage) has no hash schema yet either.
            if (state.Level100Actors.LostBaseRows.Count != 0 ||
                state.Level100ActorMechanics.LandscapeDamageStamps.Count != 0)
                throw new NotSupportedException("A world with lost base rows has no admitted hash schema.");
            int[] rawActors = state.Level100Actors.BaseStates.Where(item => item.State.RetailPlane is not null)
                .Select(item => item.ActorId.Value).Order().ToArray();
            bool usesPlaneMotionSchema = rawActors.Length != 0 ||
                state.Level100ActorMechanics.PlaneEvents is not null ||
                state.Level100ActorMechanics.Actors.Any(actor => actor.PlaneGuide is not null || actor.PlaneSpawnerExit is not null);
            int[] expectedExitOwners = state.Level100Actors.Actors.Where(actor => actor.SpawnOwnerId.HasValue &&
                Array.BinarySearch(rawActors, actor.ActorId.Value) >= 0)
                .Select(actor => actor.ActorId.Value).Order().ToArray();
            int[] actualExitOwners = state.Level100ActorMechanics.Actors.Where(actor => actor.PlaneSpawnerExit is not null)
                .Select(actor => actor.ActorId.Value).Order().ToArray();
            if (!expectedExitOwners.SequenceEqual(actualExitOwners))
                throw new ArgumentException("Spawned aircraft exit ownership is incomplete.", nameof(state));
            bool usesPlaneExitSchema = expectedExitOwners.Length != 0;
            if (usesPlaneMotionSchema && state.Level100Mission.WorldNumber != 100)
                throw new NotSupportedException("Raw Plane motion does not admit incomplete world construction.");
            if (usesPlaneMotionSchema)
            {
                int[] guidedActors = state.Level100ActorMechanics.Actors.Where(actor => actor.PlaneGuide is not null)
                    .Select(actor => actor.ActorId.Value).Order().ToArray();
                if (!rawActors.SequenceEqual(guidedActors) || state.Level100ActorMechanics.PlaneEvents is not { } events ||
                    events.FrameCount != state.RetailEventFrameCount)
                    throw new ArgumentException("Aircraft pose, guide and event clock ownership is incomplete.", nameof(state));
            }
            bool usesGroundShutdownSchema = usesPlaneMotionSchema || state.Level100Destruction.PendingShutdowns.Count != 0 ||
                state.Level100Actors.Actors.Any(actor =>
                    actor.Lifecycle == Level100ActorLifecycle.DiedAwaitingShutdown);
            bool usesEventClockSchema = usesGroundShutdownSchema || state.RetailEventFrameCount !=
                unchecked((uint)state.Level100Mission.Tick);
            bool usesPlayerWeaponSchema = usesEventClockSchema || state.Level100PlayerWeaponState !=
                Level100PlayerWeaponStateSnapshot.Initial;
            bool usesWorldMissionSchema = usesPlayerWeaponSchema ||
                UsesWorldMissionSchema(state.Level100Mission);
            // 49: the Battle Engine's crosshair readers, retained crosshair
            // line report and lock sets. Selected whenever any of them differs
            // from construction; the report and cursors then decide later
            // launches, locks and targets.
            bool usesBattleEngineTargetingSchema =
                !IsInitialTargeting(state.Level100BattleEngineTargeting);
            // 50: the Aquila's six weapon stores, their overheat flags, the
            // walker's shields-recharging flag, the depleted/overheated cue
            // times, the Pulse's last Fire level and the Battle Engine's shake.
            // Selected whenever any differs from construction.
            bool usesStoresAndShakeSchema =
                state.Level100PlayerStores != Level100PlayerStoresSnapshot.Initial ||
                state.Level100BattleEngineShake != Level100BattleEngineShakeSnapshot.Initial;
            // 51: the jet Missile Pod's charge, Fire level, mode flag, burst
            // counter and launch counters, and each seeking round's heading,
            // launch time and bound target. Selected whenever the pod differs
            // from construction or a seeking round is in flight.
            bool usesMissilePodSchema =
                state.Level100MissilePod != Level100MissilePodSnapshot.Initial ||
                state.Projectiles.Any(projectile => projectile.Seeking is not null);
            // 52: every constructed unit's callback state (+0x110, its
            // construction frame and its first full Move). Selected whenever
            // the level was constructed in the retail load order; the
            // callbacks themselves are in the event manager above.
            bool usesUnitCallbackSchema =
                state.Level100ActorMechanics.UnitCallbacks is { Count: > 0 };
            // 48: retained spawning-owner reader, exit selector/deadline and
            // explicit handoff to the existing approximate normal-control
            // bridge. Unspawned scenes retain schema 47 byte-for-byte.
            //
            // 47: admitted raw Plane state, including both poses, movement
            // time, velocity, retained angles/rates, next drive and bank flag,
            // guide cache and ordered event pool. Includes all earlier fields.
            // Other incomplete raw construction still fails closed.
            //
            // 46: ordered ground-unit shutdown admissions, exact due words
            // and delivery frames. Includes all earlier optional fields.
            //
            // 45: event-manager frame count when it cannot be recovered from
            // the already-hashed mission tick. Paused terminal updates can
            // advance the latter while the weapon clock remains frozen. All
            // differing counts select this schema, including zero. A count
            // equal to mission time adds no independent state and retains the
            // old layout. Schema 45 includes all schema-44 fields.
            //
            // 44: raw Pulse charge and all four retained weapon ready times.
            // Any non-constructor word selects this extension, including an
            // expired timestamp or signed-zero charge. Only the true initial
            // state keeps the older layout. Schema 44 includes the complete
            // schema-43 mission fields even for a default world-100 mission.
            //
            // 43: the multi-world mission extension. It records the requested
            // career world and all ten retail secondary-objective slots. The
            // schema is selected for every non-root mission, and for any root
            // snapshot whose secondary state is non-default, so no
            // future-affecting value can hide behind the compatibility path.
            // A default world-100 mission deliberately remains schema 42 and
            // emits byte-for-byte the old canonical layout, preserving every
            // established root-world hash.
            //
            // 42: records the reusable Thing/Actor base-state projection. Old
            // pose drives interpolation, contact timestamps and flags are
            // retained source state, and the source-composed inheritance mask
            // is distinct from Level 100's legacy leaf-bit projection.
            //
            // 41: records the retained directional damage-flash list. Its
            // timestamp and order affect future strict one-per-update expiry,
            // so a per-tick presentation event cannot reconstruct this state.
            //
            // 40: records the Walker travel remainder and rollover count that
            // determine whether a later strict stop requests the released
            // hydraulic-settle cue.
            //
            // 39: records the released round identity carried by each live
            // projectile. Pulse, Walker Vulcan, and Jet Vulcan overlap in
            // remaining lifetime, so their presentation contract cannot be
            // reconstructed from the old projectile fields.
            //
            // 38: records whether SetAllegiance actually executed. Allegiance 0
            // is Friendly, but it is also the default value on actor state that
            // an unrelated AI/waypoint command creates, so the value alone
            // cannot reconstruct the presentation-visible override.
            //
            // 37: added the Walker opposite-flick gesture history and live dash
            // countdown. All seven fields change whether later input starts or
            // remains locked in the released 15-update dash lifecycle.
            //
            // 36: added current and desired zoom. Desired zoom changes future
            // easing even when the current projection is momentarily equal.
            //
            // 35: added the Walker and Jet selected-weapon slots plus the Twin
            // Vulcan reload countdown. Active flags alone cannot reconstruct
            // selection because enabling a weapon does not steal the current
            // slot; the reload countdown changes whether the next Fire acts.
            //
            // 34: added canonical Aquila augment charge/active state and the
            // ordered per-tick player-damage stream. This replaces direct hull
            // subtraction with the released shield/augment damage boundary;
            // every tick gains two fields and an event count even when quiet.
            //
            // 33: the 30 Hz -> 20 Hz Core migration. This bump exists because
            // the hashed BYTE LAYOUT changed, independently of any trajectory:
            // Level100ActorMechanicsSnapshot.RetailBaseTickAccumulatorThirtieths
            // was the 20-of-every-30 base-tick accumulator, and at 20 Hz the
            // accumulator is the identity, so the field and its four bytes are
            // gone from every hashed tick.
            //
            // THREE INDEPENDENT REASONS MOVE EVERY PINNED HASH HERE, and each
            // one alone would be sufficient - do not attribute a moved hash to
            // only one of them: (1) this version int; (2) the removed field;
            // (3) state.Tick is hashed first and every tick count is now
            // two-thirds of what it was; (4) every trajectory is re-integrated
            // against the reconverted constants.
            //
            // 32: added Level100Mission.MessageBoxAllowedTick. The released
            // message-box gate is StartPlayingState + NEXT_FRAME, and
            // BUTTON_SKIP_PANNING can move StartPlayingState to any tick of
            // the opening pan, so the gate is player-controllable state rather
            // than a constant. Every hashed tick gains four bytes plus this
            // version bump, so this moves every pinned hash even for a run
            // that never skips. The simulated trajectory of such a run is
            // unchanged - see the causal isolation in the skip-panning note.
            //
            // 31: added the ordered Level100WeaponFireEvents stream. Every
            // hashed tick gains its four-byte count, so this bump moves every
            // pinned hash regardless of whether a weapon fires.
            writer.Write(usesUnitCallbackSchema ? 52 : usesMissilePodSchema ? 51 : usesStoresAndShakeSchema ? 50 : usesBattleEngineTargetingSchema ? 49 : usesPlaneExitSchema ? 48 : usesPlaneMotionSchema ? 47 : usesGroundShutdownSchema ? 46 : usesEventClockSchema ? 45 : usesPlayerWeaponSchema ? 44 : usesWorldMissionSchema ? 43 : 42);
            writer.Write(state.Tick);
            if (usesEventClockSchema)
            {
                writer.Write(state.RetailEventFrameCount);
            }
            writer.Write(state.Seed);
            writer.Write(state.InitialLevel100TutorialProgress.Introduction);
            writer.Write(state.InitialLevel100TutorialProgress.PulseCannon);
            writer.Write(state.InitialLevel100TutorialProgress.VulcanCannon);
            writer.Write(state.InitialLevel100TutorialProgress.StatusBars);
            writer.Write((int)state.Mode);
            writer.Write((int)state.Transition);
            WriteVector(writer, state.PlayerPosition);
            WriteVector(writer, state.PlayerVelocity);
            writer.Write(state.PlayerGroundElevationMillimeters);
            writer.Write(state.PlayerGroundDeltaMillimeters);
            writer.Write(state.PlayerElevationMillimeters);
            writer.Write(state.PlayerVerticalVelocityMillimetersPerTick);
            writer.Write(state.PlayerOnGround);
            writer.Write(state.PlayerInWater);
            writer.Write(state.PlayerWaterFailure);
            writer.Write(state.PlayerOnSteepSlope);
            writer.Write(state.LandingJetsActive);
            writer.Write(state.GroundImpactSpeedMillimetersPerTick);
            writer.Write(state.AquilaFlightEventLog.Count);
            foreach (AquilaFlightEvent flightEvent in state.AquilaFlightEventLog)
            {
                writer.Write(flightEvent.Tick);
                writer.Write((ushort)flightEvent.Kind);
                writer.Write((int)flightEvent.Mode);
                writer.Write((int)flightEvent.Transition);
                writer.Write((byte)flightEvent.Weapon);
            }
            writer.Write(state.FacingX);
            writer.Write(state.FacingZ);
            writer.Write(state.FacingYawMicroRad);
            writer.Write(state.WalkerYawVelocityMicroRadPerTick);
            writer.Write(state.FacingPitchMicroRad);
            writer.Write(state.WalkerPitchVelocityMicroRadPerTick);
            writer.Write(state.BodyRollMicroRad);
            writer.Write(state.RollVelocityMicroRadPerTick);
            writer.Write(state.WalkerLastMoveXPermille);
            writer.Write(state.WalkerLastMoveZPermille);
            writer.Write(state.WalkerLastHardLeftTimeBits);
            writer.Write(state.WalkerLastHardRightTimeBits);
            writer.Write(state.WalkerLastHardForwardTimeBits);
            writer.Write(state.WalkerLastHardBackwardTimeBits);
            writer.Write(state.WalkerDashTicksRemaining);
            writer.Write(state.WalkerSoundTravelMillimeters);
            writer.Write(state.WalkerSoundRolloverCount);
            writer.Write(state.Energy);
            writer.Write(state.Shield);
            writer.Write(state.Hull);
            writer.Write(state.AugmentCharge);
            writer.Write(state.AugmentActive);
            writer.Write(state.Level100PlayerDamageEvents.Count);
            foreach (Level100PlayerDamageEvent damage in state.Level100PlayerDamageEvents)
            {
                writer.Write(damage.Tick);
                writer.Write((byte)damage.Source);
                writer.Write(damage.IncomingDamageMilliLife);
                writer.Write(damage.ShieldAbsorbedMilliLife);
                writer.Write(damage.LifeDamageMilliLife);
                writer.Write(damage.RequestsDeath);
            }
            writer.Write(state.Level100DamageFlashes.Count);
            foreach (Level100DamageFlashSnapshot flash in state.Level100DamageFlashes)
            {
                writer.Write(flash.RelativeYawMicroRad);
                writer.Write(flash.StartTick);
            }
            writer.Write(state.TransformTicksRemaining);
            writer.Write(state.WalkerToJetUsesTakeoffLift);
            writer.Write(state.WalkerToJetLiftApplied);
            writer.Write(state.TicksSinceGroundContact);
            writer.Write(state.JetTicksSinceTransform);
            writer.Write(state.JetStrafeTicksRemaining);
            writer.Write(state.JetStrafeAccelerationRemainder);
            writer.Write(state.JetEnergyDrainRemainderMicroRetail);
            writer.Write(state.JetThrusterPermille);
            writer.Write(state.JetGroundedSlowTicks);
            writer.Write(state.JetStallTicks);
            writer.Write(state.FireCooldownTicksRemaining);
            writer.Write(state.TwinVulcanReloadTicksRemaining);
            if (usesPlayerWeaponSchema)
            {
                Level100PlayerWeaponStateSnapshot weapons = state.Level100PlayerWeaponState;
                writer.Write(weapons.PulseChargeBits);
                writer.Write(weapons.PulseReadyAtTimeBits);
                writer.Write(weapons.TwinVulcanReadyAtTimeBits);
                writer.Write(weapons.MechVulcanReadyAtTimeBits);
                writer.Write(weapons.MissilePodReadyAtTimeBits);
            }
            writer.Write(state.Level100OpeningTicksRemaining);
            writer.Write(state.Level100PlayerActive);
            writer.Write(state.Level100FlightEnabled);
            writer.Write(state.Level100PulseCannonEnabled);
            writer.Write(state.Level100VulcanCannonEnabled);
            writer.Write(state.Level100MechVulcanCannonEnabled);
            writer.Write(state.Level100MissilePodEnabled);
            writer.Write((int)state.Level100WalkerSelectedWeapon);
            writer.Write((int)state.Level100JetSelectedWeapon);
            writer.Write(state.ZoomPermille);
            writer.Write(state.DesiredZoomPermille);
            writer.Write(state.Level100HudEmphasisMask);
            WriteLevel100Mission(
                writer,
                state.Level100Mission,
                usesWorldMissionSchema);
            WriteLevel100Events(writer, state.Level100MissionEvents);
            WriteLevel100ActorRegistry(writer, state.Level100Actors, usesPlaneMotionSchema);
            WriteLevel100Destruction(
                writer,
                state.Level100Destruction,
                state.Level100DestructionEvents);
            if (usesGroundShutdownSchema)
            {
                writer.Write(state.Level100Destruction.PendingShutdowns.Count);
                foreach (Level100GroundShutdownSnapshot pending in state.Level100Destruction.PendingShutdowns)
                {
                    writer.Write(pending.ActorId);
                    writer.Write(pending.AdmissionFrame);
                    writer.Write(pending.DeliveryFrame);
                    writer.Write(pending.DueTimeBits);
                }
            }
            writer.Write(state.Level100WeaponFireEvents.Count);
            foreach (Level100WeaponFireEvent fireEvent in state.Level100WeaponFireEvents)
            {
                writer.Write(fireEvent.Tick);
                writer.Write((byte)fireEvent.Weapon);
                writer.Write(fireEvent.RoundCount);
            }
            WriteLevel100ActorScripts(writer, state.Level100ActorScripts);
            WriteLevel100ActorScriptCommands(writer, state.Level100ActorScriptCommands);
            WriteLevel100ActorMechanics(writer, state.Level100ActorMechanics, usesPlaneMotionSchema, usesPlaneExitSchema);
            writer.Write(state.NextProjectileId);

            ProjectileSnapshot[] projectiles = state.Projectiles
                .OrderBy(projectile => projectile.Id)
                .ToArray();
            writer.Write(projectiles.Length);
            foreach (ProjectileSnapshot projectile in projectiles)
            {
                writer.Write(projectile.Id);
                writer.Write((byte)projectile.Kind);
                WriteVector(writer, projectile.Position);
                WriteVector(writer, projectile.Velocity);
                writer.Write(projectile.ElevationMillimeters);
                writer.Write(projectile.VerticalVelocityMillimetersPerTick);
                writer.Write(projectile.RemainingTicks);
            }

            WalkerFootContactSnapshot[] walkerFeet = state.WalkerFeet
                .OrderBy(foot => foot.Id)
                .ToArray();
            writer.Write(walkerFeet.Length);
            foreach (WalkerFootContactSnapshot foot in walkerFeet)
            {
                writer.Write(foot.Id);
                WriteVector(writer, foot.Position);
                writer.Write(foot.GroundElevationMillimeters);
                writer.Write(foot.PhaseThirds);
                writer.Write(foot.LiftMillimeters);
            }

            if (usesBattleEngineTargetingSchema || usesStoresAndShakeSchema || usesMissilePodSchema ||
                usesUnitCallbackSchema)
            {
                WriteBattleEngineTargeting(writer, state.Level100BattleEngineTargeting);
            }

            if (usesStoresAndShakeSchema || usesMissilePodSchema || usesUnitCallbackSchema)
            {
                Level100PlayerStoresSnapshot stores = state.Level100PlayerStores;
                writer.Write(stores.Store0Bits);
                writer.Write(stores.Store1Bits);
                writer.Write(stores.Store2Bits);
                writer.Write(stores.Store3Bits);
                writer.Write(stores.Store4Bits);
                writer.Write(stores.Store5Bits);
                writer.Write(stores.OverheatMask);
                writer.Write(stores.ShieldsRecharging);
                writer.Write(stores.AmmoDepletedTimeBits);
                writer.Write(stores.WeaponOverheatedTimeBits);
                writer.Write(stores.PulseModeLevel);
                Level100BattleEngineShakeSnapshot shake = state.Level100BattleEngineShake;
                writer.Write(shake.YawBits);
                writer.Write(shake.PitchBits);
                writer.Write(shake.RollBits);
                writer.Write(shake.PhaseBits);
            }

            if (usesMissilePodSchema || usesUnitCallbackSchema)
            {
                Level100MissilePodSnapshot pod = state.Level100MissilePod;
                writer.Write(pod.ChargeBits);
                writer.Write(pod.ModeLevel);
                writer.Write(pod.HasMode);
                writer.Write(pod.BurstCount);
                writer.Write(pod.SequenceCounter);
                writer.Write(pod.AngleCounter);
                ProjectileSnapshot[] seeking = projectiles
                    .Where(projectile => projectile.Seeking is not null)
                    .ToArray();
                writer.Write(seeking.Length);
                foreach (ProjectileSnapshot projectile in seeking)
                {
                    Level100SeekingRoundSnapshot round = projectile.Seeking!;
                    writer.Write(projectile.Id);
                    writer.Write(round.YawMicroRadians);
                    writer.Write(round.PitchMicroRadians);
                    writer.Write(round.LaunchTimeBits);
                    WriteNullableActorId(writer, round.Target);
                }
            }

            if (usesUnitCallbackSchema)
            {
                Level100UnitCallbackSnapshot[] units = state.Level100ActorMechanics.UnitCallbacks!
                    .OrderBy(unit => unit.ActorId.Value)
                    .ToArray();
                writer.Write(units.Length);
                foreach (Level100UnitCallbackSnapshot unit in units)
                {
                    writer.Write(unit.ActorId.Value);
                    writer.Write((byte)unit.Class);
                    writer.Write(unit.NearCamera);
                    writer.Write(unit.ConstructionFrame);
                    writer.Write(unit.FirstMoveFrame);
                }
            }
        }

        return stream.ToArray();
    }

    private static bool IsInitialTargeting(Level100BattleEngineTargetingSnapshot targeting)
    {
        ArgumentNullException.ThrowIfNull(targeting);
        return targeting.CrosshairUnit is null &&
            targeting.CrosshairUnitRegardlessOfRange is null &&
            targeting.CrosshairHitKind == Level100CrosshairHitKind.Nothing &&
            targeting.CrosshairHitDistanceMillimeters == 0 &&
            targeting.Locks.Locks.Count == 0 &&
            targeting.Locks.FiredLocks.Count == 0 &&
            targeting.Locks.RecentLocks == 0 &&
            targeting.Locks.CurrentTarget == 0;
    }

    private static void WriteBattleEngineTargeting(
        BinaryWriter writer,
        Level100BattleEngineTargetingSnapshot targeting)
    {
        WriteNullableActorId(writer, targeting.CrosshairUnit);
        WriteNullableActorId(writer, targeting.CrosshairUnitRegardlessOfRange);
        writer.Write((byte)targeting.CrosshairHitKind);
        writer.Write(targeting.CrosshairHitDistanceMillimeters);
        WriteLocks(targeting.Locks.Locks);
        WriteLocks(targeting.Locks.FiredLocks);
        writer.Write(targeting.Locks.RecentLocks);
        writer.Write(targeting.Locks.CurrentTarget);

        void WriteLocks(IReadOnlyList<Level100PlayerLockSnapshot> locks)
        {
            writer.Write(locks.Count);
            foreach (Level100PlayerLockSnapshot item in locks)
            {
                writer.Write(item.Unit.Value);
                writer.Write(item.StartBits);
                writer.Write(item.FinishBits);
                writer.Write(item.DirectLock);
            }
        }
    }

    private static bool UsesWorldMissionSchema(Level100MissionSnapshot mission)
    {
        ArgumentNullException.ThrowIfNull(mission);
        return mission.WorldNumber != Level100MissionProgram.WorldNumber100 ||
               mission.SecondaryObjectives.Any(objective =>
                   objective.TextId != 0 ||
                   objective.Status != RetailSecondaryObjectiveStatus.NotDefined);
    }

    private static void WriteVector(BinaryWriter writer, SimVector2 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Z);
    }

    private static void WriteLevel100ActorScripts(
        BinaryWriter writer,
        Level100ActorScriptRuntimeSnapshot scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        writer.Write(scripts.Tick);
        writer.Write(scripts.NextSequence);
        writer.Write(scripts.PlayerInJetMode);
        writer.Write(scripts.Instances.Count);
        foreach (Level100ActorScriptInstanceSnapshot instance in scripts.Instances)
        {
            WriteNullableActorId(writer, instance.ActorId);
            writer.Write(instance.ProgramName);
            writer.Write(instance.ProgramSha256);
            writer.Write(instance.Initialized);
            writer.Write(instance.Locals.Count);
            foreach (Level100ScriptLocalSnapshot local in instance.Locals)
            {
                writer.Write(local.Ordinal);
                writer.Write(local.Name);
                WriteLevel100Value(writer, local.Value);
            }
            WriteLevel100Execution(writer, instance.ActiveExecution);
            writer.Write(instance.QueuedEvents.Count);
            foreach (Level100QueuedEventSnapshot queuedEvent in instance.QueuedEvents)
            {
                writer.Write(queuedEvent.Sequence);
                writer.Write(queuedEvent.EventName);
            }
            writer.Write(instance.Continuations.Count);
            foreach (Level100ActorScriptContinuationSnapshot continuation in instance.Continuations)
            {
                writer.Write(continuation.Sequence);
                writer.Write(continuation.DueTick.HasValue);
                if (continuation.DueTick.HasValue)
                {
                    writer.Write(continuation.DueTick.Value);
                }
                writer.Write((int)continuation.WaitKind);
                WriteNullableString(writer, continuation.WaitArgument);
                WriteLevel100Execution(writer, continuation.Execution);
            }
        }
        writer.Write(scripts.PendingPostedEvents.Count);
        foreach (Level100ActorScriptEventPosted posted in scripts.PendingPostedEvents)
        {
            writer.Write(posted.Sequence);
            writer.Write(posted.Tick);
            WriteNullableActorId(writer, posted.ActorId);
            writer.Write(posted.EventName);
        }
        WriteLevel100ActorScriptCommands(writer, scripts.PendingCommands);
    }

    private static void WriteLevel100ActorScriptCommands(
        BinaryWriter writer,
        IReadOnlyList<Level100ActorScriptCommand> commands)
    {
        writer.Write(commands.Count);
        foreach (Level100ActorScriptCommand command in commands)
        {
            writer.Write(command.Sequence);
            writer.Write(command.Tick);
            WriteNullableActorId(writer, command.ActorId);
            writer.Write((int)command.Kind);
            WriteNullableActorId(writer, command.TargetActorId);
            WriteNullableString(writer, command.Argument);
            writer.Write(command.Scalar);
        }
    }

    private static void WriteLevel100ActorMechanics(
        BinaryWriter writer,
        Level100ActorMechanicsSnapshot mechanics,
        bool includePlaneMotion, bool includePlaneExit)
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        ArgumentNullException.ThrowIfNull(mechanics.Actors);
        writer.Write(mechanics.LastConsumedCommandSequence);
        Level100ActorCommandIntentSnapshot[] actors = mechanics.Actors
            .OrderBy(actor => actor.ActorId.Value)
            .ToArray();
        writer.Write(actors.Length);
        foreach (Level100ActorCommandIntentSnapshot actor in actors)
        {
            writer.Write(actor.ActorId.Value);
            writer.Write(actor.AiState);
            writer.Write(actor.Allegiance);
            writer.Write(actor.HasAllegianceOverride);
            writer.Write((int)actor.Intent);
            WriteNullableActorId(writer, actor.TargetActorId);
            WriteNullableString(writer, actor.WaypointPath);
            writer.Write(actor.WaypointNodeIndex ?? -1);
            writer.Write(actor.WaypointCommandScalar);
            writer.Write(actor.WaitForWaypointCompletion);
            writer.Write(actor.GroundFullGuideBaseTickPhase);
            if (includePlaneMotion)
            {
                writer.Write(actor.PlaneGuide is not null);
                if (actor.PlaneGuide is { } guide)
                {
                    WriteRawVector(writer, guide.Destination);
                    writer.Write(guide.Mode);
                    writer.Write(guide.ClearanceFloatBits);
                    writer.Write(guide.ClearanceCellX);
                    writer.Write(guide.ClearanceCellY);
                    writer.Write(guide.ControllerState);
                    writer.Write(guide.SpeedMode);
                }
            }
            if (includePlaneExit)
            {
                writer.Write(actor.PlaneSpawnerExit is not null);
                if (actor.PlaneSpawnerExit is { } exit)
                {
                    WriteNullableActorId(writer, exit.SpawningOwnerId);
                    WriteNullableActorId(writer, exit.CollisionIgnoredActorId);
                    writer.Write(exit.AttachmentTag);
                    writer.Write(exit.Selector);
                    writer.Write(exit.DeadlineFloatBits);
                    writer.Write(exit.ScriptControlResumed);
                }
            }
        }

        // Actor armament. The released gameplay random stream is part of the
        // canonical state because it is part of the released simulation: it is
        // seeded to a constant at level start and stepped by every projectile
        // spawn and every wiggle sample.
        ArgumentNullException.ThrowIfNull(mechanics.ActorWeapons);
        ArgumentNullException.ThrowIfNull(mechanics.ActorRounds);
        writer.Write(mechanics.ReleasedRandomSeed);
        writer.Write(mechanics.NextActorRoundId);
        Level100ActorWeaponSnapshot[] weapons = mechanics.ActorWeapons
            .OrderBy(weapon => weapon.ActorId.Value)
            .ThenBy(weapon => (int)weapon.Weapon)
            .ToArray();
        writer.Write(weapons.Length);
        foreach (Level100ActorWeaponSnapshot weapon in weapons)
        {
            writer.Write(weapon.ActorId.Value);
            writer.Write((int)weapon.Weapon);
            writer.Write(weapon.ReloadBaseTicksRemaining);
            writer.Write(weapon.BurstShotsRemaining);
            writer.Write(weapon.BurstDelayBaseTicksRemaining);
        }

        Level100ActorRoundSnapshot[] rounds = mechanics.ActorRounds
            .OrderBy(round => round.Id)
            .ToArray();
        writer.Write(rounds.Length);
        foreach (Level100ActorRoundSnapshot round in rounds)
        {
            writer.Write(round.Id);
            writer.Write(round.OwnerActorId.Value);
            writer.Write(round.TargetActorId.Value);
            writer.Write((int)round.Kind);
            writer.Write(round.PositionMillimeters.X);
            writer.Write(round.PositionMillimeters.Y);
            writer.Write(round.PositionMillimeters.Z);
            writer.Write(round.YawMicroRadians);
            writer.Write(round.PitchMicroRadians);
            writer.Write(round.RemainingBaseTicks);
            writer.Write(round.ElapsedBaseTicks);
            writer.Write(round.Locked);
        }
        if (includePlaneMotion) WriteAircraftEvents(writer, mechanics.PlaneEvents!);
    }

    private static void WriteAircraftEvents(BinaryWriter writer, RetailEventSchedulerSnapshot events)
    {
        writer.Write(events.Float24Arithmetic);
        writer.Write(events.TimeBits); writer.Write(events.FrameCount);
        writer.Write(events.CurrentBufferNum); writer.Write(events.ReadyToFlushBuffer);
        writer.Write(events.LiveEvents); writer.Write(events.TotalProcessed);
        writer.Write(events.ProcessedThisUpdate); writer.Write(events.Valid); writer.Write(events.FreeList);
        writer.Write(events.Slots.Count);
        foreach (RetailEventSlotSnapshot slot in events.Slots)
        {
            writer.Write(slot.Handle); writer.Write(slot.NextFree); writer.Write(slot.EventNum);
            writer.Write(slot.Listener); writer.Write(slot.Data); writer.Write(slot.TimeBits); writer.Write(slot.Reuse);
        }
        writer.Write(events.Lanes.Count);
        foreach (RetailEventLaneSnapshot lane in events.Lanes)
        {
            writer.Write(lane.LaneIndex); writer.Write(lane.Handles.Count);
            foreach (int handle in lane.Handles) writer.Write(handle);
        }
        writer.Write(events.Overflow.Count);
        foreach (int handle in events.Overflow) writer.Write(handle);
    }

    private static void WriteLevel100ActorRegistry(
        BinaryWriter writer,
        Level100ActorRegistrySnapshot registry,
        bool includePlaneMotion)
    {
        ArgumentNullException.ThrowIfNull(registry);
        writer.Write(registry.DefinitionSetIdentitySha256);
        writer.Write(registry.NextActorId);
        writer.Write(registry.NextFactSequence);

        Level100ActorSnapshot[] actors = registry.Actors
            .OrderBy(actor => actor.ActorId.Value)
            .ToArray();
        writer.Write(actors.Length);
        foreach (Level100ActorSnapshot actor in actors)
        {
            writer.Write(actor.ActorId.Value);
            writer.Write(actor.DefinitionIdentity);
            writer.Write(actor.Name);
            WriteNullableString(writer, actor.DefinitionName);
            WriteNullableString(writer, actor.ScriptName);
            WriteNullableString(writer, actor.MeshBinding);
            writer.Write(actor.ThingTypeMask);
            WriteNullableActorId(writer, actor.SpawnOwnerId);
            WriteNullableString(writer, actor.SpawnerName);
            writer.Write(actor.IsStatic);
            writer.Write(actor.Active);
            writer.Write(actor.IsObjective);
            writer.Write((int)actor.Lifecycle);
            writer.Write(actor.Health);
            ArgumentNullException.ThrowIfNull(actor.Pose);
            WriteVector(writer, actor.Pose.PositionMillimeters);
            WriteBasis(writer, actor.Pose.BasisFloatBits);
            WriteVector(writer, actor.Pose.LinearVelocityMillimetersPerTick);
            WriteVector(writer, actor.Pose.AngularVelocityMicroRadiansPerTick);

            writer.Write((int)actor.TargetGroup);
            writer.Write(actor.TargetOrdinal);
            writer.Write(actor.Trigger.HasValue);
            if (actor.Trigger.HasValue)
            {
                writer.Write((int)actor.Trigger.Value);
            }

            writer.Write(actor.TriggerEntered);
            writer.Write(actor.TriggerEntryJetModeState.HasValue);
            if (actor.TriggerEntryJetModeState.HasValue)
            {
                writer.Write((int)actor.TriggerEntryJetModeState.Value);
            }

            writer.Write(actor.TriggerEventDispatched);
        }

        ArgumentNullException.ThrowIfNull(registry.BaseStates);
        Level100ActorBaseStateSnapshot[] baseStates = registry.BaseStates
            .OrderBy(item => item.ActorId.Value)
            .ToArray();
        writer.Write(baseStates.Length);
        foreach (Level100ActorBaseStateSnapshot item in baseStates)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(item.State);
            ThingActorBaseStateSnapshot state = item.State;
            if ((state.RetailPoses is not null || state.RetailMotion is not null) && state.RetailPlane is null)
                throw new NotSupportedException("Retail actor construction has no admitted canonical hash schema yet.");
            writer.Write(item.ActorId.Value);
            writer.Write((ushort)state.Flags);
            WriteVector(writer, state.CurrentPose.PositionMillimeters);
            WriteBasis(writer, state.CurrentPose.BasisFloatBits);
            WriteVector(writer, state.OldPose.PositionMillimeters);
            WriteBasis(writer, state.OldPose.BasisFloatBits);
            WriteVector(writer, state.Velocity);
            WriteVector(writer, state.AngularVelocity);
            writer.Write(state.ThingTypeMask);
            writer.Write(state.LastTimeOnGroundFloatBits);
            writer.Write(state.LastTimeInWaterFloatBits);
            writer.Write(state.LastTimeOnObjectFloatBits);
            if (includePlaneMotion)
            {
                writer.Write(state.RetailPlane is not null);
                if (state.RetailPlane is { } plane)
                {
                    Level100ActorSnapshot actor = actors.Single(value => value.ActorId == item.ActorId);
                    // Level 100's two planes and its U-17 dropship.
                    if (actor.DefinitionName is not ("Air Trainer" or "Target Drone" or "U-17 Highside Transporter"))
                        throw new NotSupportedException("Raw Plane hashing requires an admitted aircraft definition.");
                    _ = new ThingActorBaseState(state);
                    if (actor.Pose.PositionMillimeters != state.CurrentPose.PositionMillimeters ||
                        actor.Pose.BasisFloatBits != state.CurrentPose.BasisFloatBits ||
                        actor.Pose.LinearVelocityMillimetersPerTick != state.Velocity ||
                        actor.Pose.AngularVelocityMicroRadiansPerTick != state.AngularVelocity)
                        throw new ArgumentException("Aircraft projection conflicts with its raw owner.", nameof(registry));
                    foreach (RetailActorPoseSnapshot pose in new[] { state.RetailPoses!.Current, state.RetailPoses.Old })
                    {
                        WriteRawVector(writer, pose.PositionFloatBits);
                        WriteBasis(writer, pose.BasisFloatBits);
                    }
                    writer.Write(state.RetailMotion!.LastMoveTimeFloatBits);
                    writer.Write(state.RetailMotion.MoveCountdown);
                    WriteRawVector(writer, plane.Velocity);
                    WriteRawVector(writer, plane.Drive);
                    WriteRawVector(writer, plane.CurrentEuler);
                    WriteRawVector(writer, plane.DesiredEuler);
                    WriteRawVector(writer, plane.EulerRates);
                    writer.Write(plane.BankFlagFloatBits);
                }
            }
        }

        Level100ActorFactSnapshot[] pendingFacts = registry.PendingFacts
            .OrderBy(fact => fact.Sequence)
            .ToArray();
        writer.Write(pendingFacts.Length);
        foreach (Level100ActorFactSnapshot fact in pendingFacts)
        {
            writer.Write(fact.Sequence);
            writer.Write((int)fact.Kind);
            writer.Write(fact.ActorId.Value);
            WriteNullableActorId(writer, fact.OtherActorId);
            writer.Write(fact.OtherThingTypeMask);
        }
    }

    private static void WriteRawVector(BinaryWriter writer, Level100FloatVector3Bits value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static void WriteLevel100Destruction(
        BinaryWriter writer,
        Level100DestructionRuntimeSnapshot destruction,
        IReadOnlyList<Level100DestructionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(destruction);
        ArgumentNullException.ThrowIfNull(destruction.Actors);
        Level100DestructionSnapshot[] actors = destruction.Actors
            .OrderBy(actor => actor.ActorId)
            .ToArray();
        writer.Write(actors.Length);
        foreach (Level100DestructionSnapshot actor in actors)
        {
            writer.Write(actor.ActorId);
            writer.Write(actor.DefinitionName);
            writer.Write(actor.CurrentLifeBits);
            writer.Write(actor.Terminal);
            writer.Write(actor.BelowHalfReported);

            ReadOnlySpan<uint> initial = actor.InitialHealthBits.Span;
            ReadOnlySpan<uint> current = actor.CurrentHealthBits.Span;
            ReadOnlySpan<byte> activity = actor.PartActivity.Span;
            if (initial.Length != current.Length || initial.Length != activity.Length)
            {
                throw new InvalidDataException(
                    "A Level 100 destruction snapshot changed part shape.");
            }
            writer.Write(initial.Length);
            for (int index = 0; index < initial.Length; index++)
            {
                writer.Write(initial[index]);
                writer.Write(current[index]);
                writer.Write(activity[index]);
            }
        }

        ArgumentNullException.ThrowIfNull(events);
        writer.Write(events.Count);
        foreach (Level100DestructionEvent item in events)
        {
            writer.Write((byte)item.Kind);
            writer.Write((byte)item.EffectKind);
            writer.Write(item.ActorId);
            writer.Write(item.PartIndex);
            writer.Write(item.RemainingHealthBits);
            WriteContactVector(writer, item.Position);
        }
    }

    private static void WriteContactVector(BinaryWriter writer, Level100Vector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }

    private static void WriteVector(BinaryWriter writer, SimVector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }

    private static void WriteBasis(BinaryWriter writer, Level100FloatBasis3Bits basis)
    {
        writer.Write(basis.Row0X);
        writer.Write(basis.Row0Y);
        writer.Write(basis.Row0Z);
        writer.Write(basis.Row1X);
        writer.Write(basis.Row1Y);
        writer.Write(basis.Row1Z);
        writer.Write(basis.Row2X);
        writer.Write(basis.Row2Y);
        writer.Write(basis.Row2Z);
    }

    private static void WriteNullableActorId(
        BinaryWriter writer,
        Level100ActorId? actorId)
    {
        writer.Write(actorId.HasValue);
        if (actorId.HasValue)
        {
            writer.Write(actorId.Value.Value);
        }
    }

    private static void WriteLevel100Mission(
        BinaryWriter writer,
        Level100MissionSnapshot mission,
        bool usesWorldMissionSchema)
    {
        ArgumentNullException.ThrowIfNull(mission);

        writer.Write(mission.Tick);
        writer.Write(mission.ProgramSha256);
        if (usesWorldMissionSchema)
        {
            writer.Write(mission.WorldNumber);
        }
        writer.Write(mission.InitializerRan);
        writer.Write(mission.IsRunning);
        writer.Write(mission.NextSequence);
        WriteLevel100Execution(writer, mission.ActiveExecution);

        writer.Write(mission.Locals.Count);
        foreach (Level100ScriptLocalSnapshot local in mission.Locals)
        {
            writer.Write(local.Ordinal);
            writer.Write(local.Name);
            WriteLevel100Value(writer, local.Value);
        }

        writer.Write(mission.EventQueue.Count);
        foreach (Level100QueuedEventSnapshot queuedEvent in mission.EventQueue)
        {
            writer.Write(queuedEvent.Sequence);
            writer.Write(queuedEvent.EventName);
        }

        writer.Write(mission.Continuations.Count);
        foreach (Level100ScriptContinuationSnapshot continuation in mission.Continuations)
        {
            writer.Write(continuation.Sequence);
            writer.Write(continuation.DueTick);
            writer.Write((int)continuation.WaitKind);
            writer.Write(continuation.WaitArgument);
            WriteLevel100Execution(writer, continuation.Execution);
        }

        writer.Write((int)mission.Outcome);
        writer.Write((int)mission.TerminalState);
        writer.Write((int)mission.FailureReason);
        writer.Write(mission.FailureTextId);
        writer.Write(mission.TerminalTicksRemaining);
        writer.Write(mission.InitialPlayerHealth);
        writer.Write(mission.LatestPlayerHealth);
        writer.Write(mission.ObservedPlayerHealth);
        writer.Write(mission.PlayerActive);
        writer.Write(mission.FlightModeEnabled);
        writer.Write((int)mission.PulseCannonAvailability);
        writer.Write((int)mission.TwinVulcanAvailability);
        writer.Write((int)mission.MechVulcanAvailability);
        writer.Write((int)mission.MissilePodAvailability);
        WriteNullableString(writer, mission.NavigationObjective);
        writer.Write(mission.Evaded);
        writer.Write(mission.Aborted);
        writer.Write(mission.FriendlyBuildingHits);
        writer.Write(mission.ScoreDelta);
        writer.Write(mission.TutorialProgress.Introduction);
        writer.Write(mission.TutorialProgress.PulseCannon);
        writer.Write(mission.TutorialProgress.VulcanCannon);
        writer.Write(mission.TutorialProgress.StatusBars);

        writer.Write(mission.PrimaryObjectives.Count);
        foreach (Level100PrimaryObjectiveSnapshot objective in mission.PrimaryObjectives)
        {
            writer.Write(objective.Objective);
            writer.Write(objective.TextId);
            writer.Write((int)objective.Status);
        }

        if (usesWorldMissionSchema)
        {
            writer.Write(mission.SecondaryObjectives.Count);
            foreach (RetailSecondaryObjectiveSnapshot objective in mission.SecondaryObjectives)
            {
                writer.Write(objective.Index);
                writer.Write(objective.TextId);
                writer.Write((int)objective.Status);
            }
        }

        WriteLevel100Events(writer, mission.PendingEvents);

        writer.Write(mission.MessageClearTick);
        WriteLevel100Events(writer, mission.PendingMessages);
        writer.Write(mission.MessageBoxAllowedTick);
    }

    private static void WriteLevel100Events(
        BinaryWriter writer,
        IReadOnlyList<Level100MissionEvent> events)
    {
        writer.Write(events.Count);
        foreach (Level100MissionEvent missionEvent in events)
        {
            switch (missionEvent)
            {
                case Level100MessageRequested item:
                    writer.Write((byte)1);
                    writer.Write(item.Tick);
                    writer.Write(item.SpeakerId);
                    writer.Write(item.MessageId);
                    writer.Write(item.ScriptWaitsForDuration);
                    writer.Write(item.ExpectedPlaybackTicks);
                    break;
                case Level100HudEmphasisChanged item:
                    writer.Write((byte)2);
                    writer.Write(item.Tick);
                    writer.Write(item.PartId);
                    writer.Write(item.Emphasized);
                    break;
                case Level100PlayerActivationChanged item:
                    writer.Write((byte)3);
                    writer.Write(item.Tick);
                    writer.Write(item.Active);
                    break;
                case Level100FlightModeAvailabilityChanged item:
                    writer.Write((byte)4);
                    writer.Write(item.Tick);
                    writer.Write(item.Enabled);
                    break;
                case Level100WeaponAvailabilityChanged item:
                    writer.Write((byte)5);
                    writer.Write(item.Tick);
                    writer.Write((int)item.Weapon);
                    writer.Write(item.Enabled);
                    break;
                case Level100NavigationObjectiveChanged item:
                    writer.Write((byte)6);
                    writer.Write(item.Tick);
                    WriteNullableString(writer, item.ThingName);
                    break;
                case Level100ActorCommandRequested item:
                    writer.Write((byte)7);
                    writer.Write(item.Tick);
                    writer.Write(item.ActorId.Value);
                    writer.Write((int)item.Command);
                    break;
                case Level100SpawnThingRequested item:
                    writer.Write((byte)8);
                    writer.Write(item.Tick);
                    writer.Write(item.OwnerActorId.Value);
                    writer.Write(item.DefinitionName);
                    writer.Write(item.SpawnerName);
                    writer.Write(item.Count);
                    writer.Write(item.ScriptName);
                    break;
                case Level100MissionEventPosted item:
                    writer.Write((byte)9);
                    writer.Write(item.Tick);
                    writer.Write(item.EventName);
                    break;
                case Level100HelpRequested item:
                    writer.Write((byte)13);
                    writer.Write(item.Tick);
                    writer.Write(item.HelpMessageId);
                    break;
                case Level100ScoreChanged item:
                    writer.Write((byte)14);
                    writer.Write(item.Tick);
                    writer.Write(item.Delta);
                    writer.Write(item.TotalDelta);
                    break;
                case Level100TutorialSlotSaved item:
                    writer.Write((byte)15);
                    writer.Write(item.Tick);
                    writer.Write(item.Slot);
                    break;
                case Level100PrimaryObjectiveChanged item:
                    writer.Write((byte)16);
                    writer.Write(item.Tick);
                    writer.Write(item.Objective);
                    writer.Write(item.TextId);
                    writer.Write((int)item.Status);
                    break;
                case Level100MissionOutcomeDeclared item:
                    writer.Write((byte)17);
                    writer.Write(item.Tick);
                    writer.Write((int)item.Outcome);
                    writer.Write((int)item.FailureReason);
                    writer.Write(item.FailureTextId);
                    break;
                case Level100TerminalStateChanged item:
                    writer.Write((byte)18);
                    writer.Write(item.Tick);
                    writer.Write((int)item.State);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown Level 100 mission event {missionEvent.GetType().Name}.");
            }
        }
    }

    private static void WriteLevel100Execution(
        BinaryWriter writer,
        Level100ScriptExecutionSnapshot execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        WriteNullableString(writer, execution.EventName);
        writer.Write(execution.InstructionPointer);
        writer.Write(execution.Flags);
        writer.Write(execution.SavedStackSize);
        writer.Write(execution.Abort);
        writer.Write(execution.CallContext.HasValue);
        if (execution.CallContext.HasValue)
        {
            WriteLevel100Value(writer, execution.CallContext.Value);
        }
        writer.Write(execution.Stack.Count);
        foreach (Level100ScriptValueSnapshot value in execution.Stack)
        {
            WriteLevel100Value(writer, value);
        }

        writer.Write(execution.CallFrames.Count);
        foreach (int instructionPointer in execution.CallFrames)
        {
            writer.Write(instructionPointer);
        }
    }

    private static void WriteLevel100Value(
        BinaryWriter writer,
        Level100ScriptValueSnapshot value)
    {
        writer.Write((int)value.Type);
        writer.Write(value.Scalar);
        writer.Write(value.ComponentY);
        writer.Write(value.ComponentZ);
        WriteNullableString(writer, value.Text);
    }

    private static void WriteNullableString(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            writer.Write(value);
        }
    }
}
