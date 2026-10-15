// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;

namespace OnslaughtRebuild.Core;

/// <summary>
/// Internal dispatch envelope that preserves the public five-field impact
/// contract while allowing the owning simulation to retain exact causal round
/// identity. The identity is observation-only: it is never snapshot, replay,
/// serialization, canonical/public equality, or state-hash material.
/// </summary>
internal readonly record struct Level100ActorRoundImpactReceipt(
    Level100ActorRoundImpact Impact,
    int? RoundId,
    bool ContactOnly = false);

/// <summary>
/// Actor-owned weapons and rounds, in the current 20 Hz firing approximation.
///
/// <para><b>Provenance.</b> The bounded laws below use pristine
/// BEA.exe (sha256 <c>74154bfa...7750</c>) instruction evidence and scalar
/// words from <c>data/default physics.dat</c> (sha256 <c>e1fb3ded...ada14</c>)
/// or shipped record defaults. They do not establish the current all-slots
/// loop as the native controller/provider scheduling chain. Relevant bodies:</para>
/// <list type="bullet">
///   <item><c>ProjectileBurst__SpawnFromPercentBucketFallback</c>
///     @<c>0x00506010</c> - the reload gate and burst start.</item>
///   <item><c>CWeapon__HandleFireBurstEvent</c> @<c>0x00506930</c> - the
///     burst continuation.</item>
///   <item><c>ProjectileBurst__SpawnFromCurrentPreset</c> @<c>0x005069f0</c> -
///     the per-projectile inaccuracy scatter.</item>
///   <item>round vtable slot 66 @<c>0x004d8e40</c> - wiggle, the seek gate and
///     the homing law.</item>
/// </list>
/// <para>The firing gate is
/// <c>CUnit__ClassifyTargetRangeBand</c> @<c>0x004fb670</c> plus
/// <c>OID__CanFireAtTarget_BallisticArcA</c> @<c>0x00507ab0</c>.</para>
///
/// <para><b>Not modelled, and why.</b> The target-height window
/// (<c>CWeaponMinTargetHeight</c>/<c>MaxTargetHeight</c>) is a broad but finite
/// strict window, -10 &lt; height &lt; 10000 for these modes. It is still absent
/// from this approximate firing loop; the separately recovered provider selector
/// carries it without claiming controller/fire integration. The B feasibility
/// path at <c>005090ea</c> performs its own world line query for the selected
/// non-seeking Blaster, independently of A's optional trace context. It admits
/// a Unit hit with the target's allegiance; the seeking missile skips this query.
/// That B check is absent here. Unit initialization also supplies GunA/GunB
/// selector 1 to the weapon attachment at <c>004f8858</c>/<c>0044a830</c>.
/// The launch positions below still omit those mounts; an absent
/// <c>CWeaponLaunchSequence</c> does not justify an owner-centred origin.
/// See the September 12 common-controller evidence in CComplexThing.cpp.md.
/// The weapon's attached-target aim transform
/// supplies launch direction, while the unit's own facing independently passes
/// through <c>CWeaponYawTolerance</c>. <c>CWeaponTrack</c> is absent from both
/// modes (shipped default 0); the reconstruction therefore does not invent its
/// separate in-spawn transform rebuild.</para>
/// </summary>
public sealed partial class Level100ActorMechanics
{
    private sealed class ActorWeaponState
    {
        internal required Level100ActorId ActorId { get; init; }
        internal required Level100ActorWeaponKind Weapon { get; init; }
        internal int ReloadBaseTicksRemaining { get; set; }
        internal int BurstShotsRemaining { get; set; }
        internal int BurstDelayBaseTicksRemaining { get; set; }
    }

    private sealed class ActorRoundState
    {
        internal required int Id { get; init; }
        internal required Level100ActorId OwnerActorId { get; init; }
        internal required Level100ActorId TargetActorId { get; init; }
        internal required Level100ActorRoundKind Kind { get; init; }
        internal SimVector3 PositionMillimeters { get; set; }
        internal int YawMicroRadians { get; set; }
        internal int PitchMicroRadians { get; set; }
        internal int RemainingBaseTicks { get; set; }
        internal int ElapsedBaseTicks { get; set; }
        internal bool Locked { get; set; }
        // Set by the life event. The scheduler implies it (a live round whose
        // life event is no longer filed), so the snapshot carries no copy.
        internal bool Dying { get; set; }
    }

    private readonly List<ActorWeaponState> _actorWeapons = [];
    private readonly List<ActorRoundState> _actorRounds = [];
    private readonly List<Level100ActorRoundImpactReceipt> _actorRoundImpacts = [];
    private Level100ReleasedRandom _releasedRandom = new();
    private int _nextActorRoundId = 1;

    /// <summary>
    /// Released actor rounds that reached their designated target since the
    /// last drain. The caller owns applying the damage, because the player's
    /// hull is not owned by this class.
    /// </summary>
    public IReadOnlyList<Level100ActorRoundImpact> DrainActorRoundImpacts()
    {
        IReadOnlyList<Level100ActorRoundImpactReceipt> receipts =
            DrainActorRoundImpactReceipts();
        if (receipts.Count == 0)
        {
            return Array.Empty<Level100ActorRoundImpact>();
        }

        Level100ActorRoundImpact[] drained = receipts
            .Where(receipt => !receipt.ContactOnly)
            .Select(receipt => receipt.Impact)
            .ToArray();
        return drained.Length == 0 ? Array.Empty<Level100ActorRoundImpact>() : Array.AsReadOnly(drained);
    }

    /// <summary>
    /// Internal production drain retaining the exact actor-round identity for
    /// causal dispatch receipts. Public consumers continue to receive the
    /// stable five-field <see cref="Level100ActorRoundImpact"/> contract.
    /// </summary>
    internal IReadOnlyList<Level100ActorRoundImpactReceipt>
        DrainActorRoundImpactReceipts()
    {
        if (_actorRoundImpacts.Count == 0)
        {
            return Array.Empty<Level100ActorRoundImpactReceipt>();
        }

        Level100ActorRoundImpactReceipt[] drained = _actorRoundImpacts.ToArray();
        _actorRoundImpacts.Clear();
        return Array.AsReadOnly(drained);
    }

    /// <summary>
    /// Advances the released global gameplay stream for one projectile and
    /// returns the two inaccuracy offsets in the Euler order consumed by
    /// <c>ProjectileBurst__SpawnFromCurrentPreset</c> @<c>0x005069f0</c>.
    /// </summary>
    internal (int YawMicroRadians, int PitchMicroRadians) NextWeaponInaccuracy(
        int inaccuracyMicroRadians)
    {
        int firstSample =
            _releasedRandom.NextSignedUnitScaled(inaccuracyMicroRadians);
        int secondSample =
            _releasedRandom.NextSignedUnitScaled(inaccuracyMicroRadians);
        return (secondSample, firstSample);
    }

    /// <summary>
    /// Causal-probe seam for the Battle Engine damage consumer. The next
    /// normal <see cref="Simulation.Step"/> still owns hit dispatch, shield
    /// damage, directional-flash creation, death handling and event order.
    /// </summary>
    internal void QueueActorRoundImpactForMeasurement(
        Level100ActorRoundImpact impact)
    {
        ArgumentNullException.ThrowIfNull(impact);
        _actorRoundImpacts.Add(new Level100ActorRoundImpactReceipt(impact, null));
    }

    private IReadOnlyList<Level100ActorWeaponSnapshot> SnapshotActorWeapons() =>
        Array.AsReadOnly(_actorWeapons
            .OrderBy(weapon => weapon.ActorId.Value)
            .ThenBy(weapon => (int)weapon.Weapon)
            .Select(weapon => new Level100ActorWeaponSnapshot(
                weapon.ActorId,
                weapon.Weapon,
                weapon.ReloadBaseTicksRemaining,
                weapon.BurstShotsRemaining,
                weapon.BurstDelayBaseTicksRemaining))
            .ToArray());

    private IReadOnlyList<Level100ActorRoundSnapshot> SnapshotActorRounds() =>
        Array.AsReadOnly(_actorRounds
            .OrderBy(round => round.Id)
            .Select(round => new Level100ActorRoundSnapshot(
                round.Id,
                round.OwnerActorId,
                round.TargetActorId,
                round.Kind,
                round.PositionMillimeters,
                round.YawMicroRadians,
                round.PitchMicroRadians,
                round.RemainingBaseTicks,
                round.ElapsedBaseTicks,
                round.Locked))
            .ToArray());

    private void RestoreArmament(Level100ActorMechanicsSnapshot snapshot)
    {
        if (snapshot.NextActorRoundId < 1)
        {
            throw new ArgumentException(
                "Level 100 actor mechanics snapshot has an invalid round id.",
                nameof(snapshot));
        }

        _releasedRandom = new Level100ReleasedRandom(snapshot.ReleasedRandomSeed);
        _nextActorRoundId = snapshot.NextActorRoundId;
        _actorWeapons.Clear();
        foreach (Level100ActorWeaponSnapshot weapon in snapshot.ActorWeapons)
        {
            ArgumentNullException.ThrowIfNull(weapon);
            _actorWeapons.Add(new ActorWeaponState
            {
                ActorId = weapon.ActorId,
                Weapon = weapon.Weapon,
                ReloadBaseTicksRemaining = weapon.ReloadBaseTicksRemaining,
                BurstShotsRemaining = weapon.BurstShotsRemaining,
                BurstDelayBaseTicksRemaining = weapon.BurstDelayBaseTicksRemaining,
            });
        }

        _actorRounds.Clear();
        foreach (Level100ActorRoundSnapshot round in snapshot.ActorRounds)
        {
            ArgumentNullException.ThrowIfNull(round);
            _actorRounds.Add(new ActorRoundState
            {
                Id = round.Id,
                OwnerActorId = round.OwnerActorId,
                TargetActorId = round.TargetActorId,
                Kind = round.Kind,
                PositionMillimeters = round.PositionMillimeters,
                YawMicroRadians = round.YawMicroRadians,
                PitchMicroRadians = round.PitchMicroRadians,
                RemainingBaseTicks = round.RemainingBaseTicks,
                ElapsedBaseTicks = round.ElapsedBaseTicks,
                Locked = round.Locked,
            });
        }
    }

    /// <summary>
    /// Arms an actor's released weapon slots when it is first told to attack.
    /// The slot list is exactly the <c>CUnitUse</c> statements on the shipped
    /// <c>Unit</c> record.
    /// </summary>
    private void ArmActorWeapons(ActorState state)
    {
        Level100ActorSnapshot actor = _actors.GetActor(state.ActorId);
        if (actor.DefinitionName is null)
        {
            return;
        }

        foreach (Level100ActorWeaponKind kind in
                 Level100ActorArmament.Slots(actor.DefinitionName))
        {
            if (_actorWeapons.Any(weapon =>
                    weapon.ActorId == state.ActorId && weapon.Weapon == kind))
            {
                continue;
            }

            _actorWeapons.Add(new ActorWeaponState
            {
                ActorId = state.ActorId,
                Weapon = kind,
            });
        }
    }

    // ------------------------------------------------------------------
    // Weapons
    // ------------------------------------------------------------------

    private void AdvanceActorWeapons()
    {
        if (_actorWeapons.Count == 0)
        {
            return;
        }

        for (int index = _actorWeapons.Count - 1; index >= 0; index--)
        {
            ActorWeaponState weapon = _actorWeapons[index];
            if (weapon.ReloadBaseTicksRemaining > 0)
            {
                weapon.ReloadBaseTicksRemaining--;
            }
            if (weapon.BurstDelayBaseTicksRemaining > 0)
            {
                weapon.BurstDelayBaseTicksRemaining--;
            }

            if (!_states.TryGetValue(weapon.ActorId.Value, out ActorState? state))
            {
                _actorWeapons.RemoveAt(index);
                continue;
            }

            Level100ActorSnapshot owner = _actors.GetActor(weapon.ActorId);
            if (!owner.Active ||
                owner.Lifecycle != Level100ActorLifecycle.Alive)
            {
                weapon.BurstShotsRemaining = 0;
                continue;
            }

            // AI_OFF silences the unit. The released script command was being
            // parsed and stored and then never read by anything - AiState was
            // written at Level100ActorMechanics, copied into the snapshot and
            // hashed, and NOTHING branched on it. Measured consequence: six
            // drones kept firing after the LevelScript posted its abort, which
            // turns off their AI.
            //
            // AI_OFF is 1, from the released `data\MissionScripts\onsldef.msl`
            // lines 2-6 - authored developer text that the source itself
            // #includes as a header (Career.cpp:11, game.cpp:46):
            //     AI_ON 0, AI_OFF 1, AI_NORMAL 2, AI_DEFENSIVE 3, AI_ONF 4
            // The default is 0, so an actor that was never given an AI state
            // reads as AI_ON and is unaffected by this gate.
            //
            // ONLY AI_OFF is gated here. The sibling defect - SetAllegiance
            // being equally unread - is NOT fixed alongside it, because
            // FRIENDLY_ALLIGENCE is 0 and our Allegiance field also defaults to
            // 0, so a naive "friendly units do not attack" gate would silence
            // every actor in the level. That needs each actor's AUTHORED
            // allegiance established first and is tracked separately.
            // A leaving unit's AI only polls (0x004ff340-0x004ff34f): no think,
            // so no firing.
            if (state.AiState == SimulationConstants.ReleasedAiStateOff ||
                state.PlaneGuide?.SpeedMode is 1 or 2)
            {
                weapon.BurstShotsRemaining = 0;
                continue;
            }

            if (state.Intent != Level100ActorCommandIntent.Attacking ||
                state.TargetActorId is not { } targetId)
            {
                weapon.BurstShotsRemaining = 0;
                continue;
            }

            Level100ActorMotionDefinition? motion =
                _definitions.FindMotionDefinition(owner.DefinitionName);
            if (motion?.MotionClass != Level100ActorMotionClass.Plane)
            {
                continue;
            }

            Level100ActorWeaponMode mode = Level100ActorArmament.Mode(weapon.Weapon);

            // Burst continuation: CWeapon__HandleFireBurstEvent @0x00506930
            // spawns while weapon[+0x6c] < mode[+0x44] regardless of the aim
            // gate - only the burst-start path is gated.
            if (weapon.BurstShotsRemaining > 0)
            {
                if (weapon.BurstDelayBaseTicksRemaining == 0)
                {
                    LaunchActorRound(weapon, mode, state, targetId);
                    weapon.BurstShotsRemaining--;
                    weapon.BurstDelayBaseTicksRemaining = mode.BurstDelayBaseTicks;
                }
                continue;
            }

            // The recovered controller exit has not entered normal control
            // yet. This prevents new requests, but is deliberately AFTER
            // burst continuation: exit state does not cancel a queued shot.
            // The older AI/intent cancellation above remains a separate gap.
            if (!PlaneScriptControlAvailable(state)) continue;

            if (weapon.ReloadBaseTicksRemaining > 0)
            {
                continue;
            }

            if (!CanActorWeaponFire(weapon.ActorId, targetId, mode))
            {
                continue;
            }

            // Burst start: reload is charged from the FIRST shot
            // (`weapon[+0x64] = NOW + mode[+0x38]` before the first spawn).
            weapon.ReloadBaseTicksRemaining = mode.ReloadBaseTicks;
            LaunchActorRound(weapon, mode, state, targetId);
            weapon.BurstShotsRemaining = mode.BurstSize - 1;
            weapon.BurstDelayBaseTicksRemaining = mode.BurstDelayBaseTicks;
        }
    }

    /// <summary>
    /// The released firing gate, reduced to the terms that are not
    /// unconditional for Level 100. See the class remarks for what is omitted.
    /// </summary>
    private bool CanActorWeaponFire(
        Level100ActorId ownerId,
        Level100ActorId targetId,
        Level100ActorWeaponMode mode)
    {
        Level100ActorSnapshot target = _actors.GetActor(targetId);
        if (!target.Active || target.Lifecycle != Level100ActorLifecycle.Alive)
        {
            return false;
        }

        Level100ActorPoseSnapshot ownerPose = _actors.GetPose(ownerId);
        Level100ActorPoseSnapshot targetPose = _actors.GetPose(targetId);
        long deltaX = (long)targetPose.PositionMillimeters.X -
            ownerPose.PositionMillimeters.X;
        long deltaY = (long)targetPose.PositionMillimeters.Y -
            ownerPose.PositionMillimeters.Y;
        long deltaZ = (long)targetPose.PositionMillimeters.Z -
            ownerPose.PositionMillimeters.Z;

        // CUnit__ClassifyTargetRangeBand @0x004fb670: a plain 3D centre
        // distance against CWeaponMinRange / CWeaponMaxRange, because neither
        // drone weapon uses a ballistic arc.
        long distanceSquared =
            (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
        long minimum = mode.MinimumRangeMillimeters;
        long maximum = mode.MaximumRangeMillimeters;
        if (distanceSquared < minimum * minimum ||
            distanceSquared > maximum * maximum)
        {
            return false;
        }

        (int ownerYaw, int ownerPitch) = ReadPoseYawPitch(ownerPose);
        long horizontal = IntegerSquareRoot((deltaX * deltaX) + (deltaZ * deltaZ));
        int targetYaw = FixedAtan2(-deltaX, deltaZ);
        int targetPitch = FixedAtan2(deltaY, horizontal);

        // |wrapped yaw error| < CWeaponYawTolerance, strict, at
        // `ABS(fVar2) < mode[+0x84]`.
        int yawError = NormalizeMicroRad(targetYaw - ownerYaw);
        if (Math.Abs(yawError) >= mode.YawToleranceMicroRadians)
        {
            return false;
        }

        // The deflection window. The shipped records carry
        // CWeaponMinDeflection = +pi/4 and CWeaponMaxDeflection = -pi/4, i.e.
        // the two names are the wrong way round, which is what makes the
        // shipped comparisons read as inverted. Net effect: a symmetric window.
        int pitchError = NormalizeMicroRad(targetPitch - ownerPitch);
        return Math.Abs(pitchError) < mode.PitchWindowMicroRadians;
    }

    private void LaunchActorRound(
        ActorWeaponState weapon,
        Level100ActorWeaponMode mode,
        ActorState state,
        Level100ActorId targetId)
    {
        Level100ActorPoseSnapshot ownerPose = _actors.GetPose(weapon.ActorId);

        // The launch direction is the WEAPON's aim transform
        // (`burstContext+0x30`), not the unit's facing.
        // `OID__UpdateAimTransformAndAttachTargetReader` @0x00509140 rebuilds
        // that matrix every update from `-atan2(...)` and finite-interior
        // `asin(...)` elevation (0x0055dcb0, despite its retained Acos label) of the
        // vector to the attached target reader, so a weapon that is allowed to
        // fire is already pointing at what it is firing at. The unit's own
        // facing is what CWeaponYawTolerance gates, which is why the two are
        // separate tests. CWeaponTrack (mode +0xac, the branch that instead
        // rebuilds the matrix inside the spawn body) is absent from both drone
        // weapon modes and is not modelled.
        Level100ActorPoseSnapshot aimPose = _actors.GetPose(targetId);
        long aimX = (long)aimPose.PositionMillimeters.X -
            ownerPose.PositionMillimeters.X;
        long aimY = (long)aimPose.PositionMillimeters.Y -
            ownerPose.PositionMillimeters.Y;
        long aimZ = (long)aimPose.PositionMillimeters.Z -
            ownerPose.PositionMillimeters.Z;
        int yaw = FixedAtan2(-aimX, aimZ);
        int pitch = FixedAtan2(
            aimY,
            IntegerSquareRoot((aimX * aimX) + (aimZ * aimZ)));

        // ProjectileBurst__SpawnFromCurrentPreset @0x005069f0 draws exactly two
        // samples per projectile and feeds the second one to the first Euler
        // argument. The draws are taken even when CWeaponInaccuracy is zero in
        // the reconstruction only if the shipped code would take them - it
        // does, unconditionally, so the stream advances by two per projectile
        // regardless of the scatter magnitude.
        (int yawInaccuracy, int pitchInaccuracy) =
            NextWeaponInaccuracy(mode.InaccuracyMicroRadians);
        // The spawner then runs the round's Init: CRound::Init 0x004d8410 calls
        // CActor::Init at 0x004d867b, which takes one more shared draw for the
        // round's Move phase. Every spawned round therefore costs three draws.
        _ = _releasedRandom.Next();

        // The actor rounds keep pitch nose-up; retail's basis is nose-down.
        // Neither drone weapon mode names a CWeaponLaunchAngle.
        (int launchYaw, int launchPitch) = ComposeLaunchDirection(
            yaw, -pitch, DefaultLaunchAngle, (yawInaccuracy, pitchInaccuracy));
        _ = AddActorRound(
            weapon.ActorId,
            targetId,
            mode.Round,
            ownerPose.PositionMillimeters,
            launchYaw,
            NormalizeMicroRad(-launchPitch));
        _ = state;
    }

    /// <summary>
    /// A new actor round in flight from <paramref name="position"/> along
    /// (yaw, nose-up pitch), and <c>CRound::Init</c>'s MOVE and life events.
    /// </summary>
    private int AddActorRound(
        Level100ActorId ownerId,
        Level100ActorId targetId,
        Level100ActorRoundKind kind,
        SimVector3 position,
        int yawMicroRadians,
        int pitchMicroRadians)
    {
        Level100ActorRoundData round = Level100ActorArmament.Round(kind);
        int roundId = _nextActorRoundId++;
        _actorRounds.Add(new ActorRoundState
        {
            Id = roundId,
            OwnerActorId = ownerId,
            TargetActorId = targetId,
            Kind = round.Kind,
            PositionMillimeters = position,
            YawMicroRadians = yawMicroRadians,
            PitchMicroRadians = pitchMicroRadians,
            RemainingBaseTicks = round.LifeSpanBaseTicks,
            ElapsedBaseTicks = 0,
            Locked = round.Seeks,
        });
        FileRoundEvents(ActorRoundListener(roundId), round.LifeSpanSeconds);
        return roundId;
    }

    /// <summary>
    /// Causal-probe seam: one actor round placed in flight exactly as a weapon
    /// launch places it, without the weapon's aim, scatter or draws. No
    /// shipped path calls this; it lets a test put a round's last step where
    /// it wants.
    /// </summary>
    internal int QueueActorRoundForMeasurement(
        Level100ActorId ownerId,
        Level100ActorId targetId,
        Level100ActorRoundKind kind,
        SimVector3 position,
        int yawMicroRadians,
        int pitchMicroRadians) =>
        AddActorRound(ownerId, targetId, kind, position, yawMicroRadians, pitchMicroRadians);

    // ------------------------------------------------------------------
    // Rounds
    // ------------------------------------------------------------------

    /// <summary>
    /// Round listeners on the level event manager: 0x1000_0000 + 2·id for a
    /// Battle Engine round, + 1 for an actor's; below the unit range.
    /// </summary>
    private const int RoundListenerBase = 0x1000_0000;

    /// <summary><c>CActor</c>'s MOVE, which <c>CActor::Init</c> files for the next frame.</summary>
    internal const int RoundMoveEvent = 3000;

    /// <summary>The end of a round's life span, filed by <c>CRound::Init</c> (<c>0x004d86a6</c>).</summary>
    internal const int RoundLifeEvent = 4000;

    internal static int PlayerRoundListener(int roundId) => checked(RoundListenerBase + (roundId * 2));

    internal static int ActorRoundListener(int roundId) => checked(RoundListenerBase + (roundId * 2) + 1);

    private static bool IsRoundListener(int listener) =>
        listener >= RoundListenerBase && listener < ComponentMoveListenerBase;

    internal static bool IsPlayerRoundListener(int listener) =>
        IsRoundListener(listener) && (listener - RoundListenerBase) % 2 == 0;

    internal static int PlayerRoundId(int listener) => (listener - RoundListenerBase) / 2;

    /// <summary>
    /// <c>CRound::Init</c>'s two events (the RE lane's round Frames contract):
    /// <c>CActor::Init</c> files the MOVE at −1 (multiplier 1.0, so never
    /// LF_MOVE), into the current bucket, so the round first moves in the next
    /// frame's flush; then the 4000 at now + the life span, which lands
    /// k = floor((life − 0.001) × 20) buckets later. Each delivered MOVE
    /// re-files itself as it is delivered, so rounds move in the manager's
    /// insertion order: a round made by a controller Fire before the flush goes
    /// ahead of every MOVE that flush re-files, and one made by a 5001 burst
    /// goes in at that point of the flush.
    /// </summary>
    internal void FileRoundEvents(int listener, float lifeSpanSeconds)
    {
        if (!(lifeSpanSeconds > RetailEventScheduler.ImmediateWindow))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifeSpanSeconds), "A round's life must end after its first Move.");
        }

        RetailEventScheduler events = LevelEvents;
        events.AddEvent(RoundMoveEvent, listener, RetailEventScheduler.NextFrame);
        events.AddEventTimeFromNow(lifeSpanSeconds, RoundLifeEvent, listener);
    }

    /// <summary>
    /// Re-files a delivered round MOVE for the next frame on its own record
    /// (<c>CActor::HandleEvent</c>, <c>0x00401b41</c>).
    /// </summary>
    internal static void RefileRoundMove(RetailEventScheduler events, RetailEventDispatch dispatch) =>
        events.AddEvent(
            RoundMoveEvent,
            dispatch.Listener,
            RetailEventScheduler.NextFrame,
            RetailEventPriority.StartOfFrame,
            reuseHandle: dispatch.Handle);

    /// <summary>
    /// An actor round's MOVE or life event. The round starts dying on its life
    /// event; its next MOVE takes one last step and is not re-filed. A ring
    /// life event comes before that frame's MOVE, because it was filed at
    /// launch and the MOVE was re-filed in the frame before, so the round makes
    /// k + 1 Moves. A life of 9.9 s or more files into the overflow list, which
    /// is delivered after the lanes, so the last step falls in the next frame.
    /// The Forseti Missile's <c>CRoundExplode</c> air burst on the life event
    /// (<c>0x004d9a54</c>) is not modelled.
    /// </summary>
    private void DispatchActorRoundEvent(RetailEventScheduler events, RetailEventDispatch dispatch)
    {
        int roundId = (dispatch.Listener - RoundListenerBase - 1) / 2;
        int index = _actorRounds.FindIndex(round => round.Id == roundId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Actor round {roundId} has an event but no round.");
        }

        ActorRoundState round = _actorRounds[index];
        switch (dispatch.EventNum)
        {
            case RoundLifeEvent:
                round.Dying = true;
                return;
            case RoundMoveEvent:
                if (!StepActorRound(round) || round.Dying)
                {
                    _actorRounds.RemoveAt(index);
                    events.ClearListenerEvents(dispatch.Listener);
                    return;
                }

                RefileRoundMove(events, dispatch);
                return;
            default:
                throw new InvalidOperationException($"Unadmitted actor round event {dispatch.EventNum}.");
        }
    }

    /// <summary>
    /// One <c>CRound::Move</c> of an actor round. Returns false when the round
    /// is spent: it reached its target, or it went below the height field.
    /// </summary>
    private bool StepActorRound(ActorRoundState round)
    {
        Level100ActorRoundData data = Level100ActorArmament.Round(round.Kind);

        // Seek, before the move: the shipped body runs the guidance block
        // after CActor__Move, so the round travels one tick on its launch
        // heading before homing can act. That is the same ordering as
        // steering here and integrating below, because the steering result
        // is only used by the next integration.
        if (data.Seeks && round.Locked)
        {
            SteerSeekingRound(round, data);
        }

        (int sin, int cos) = FixedSinCos(round.YawMicroRadians);
        (int pitchSin, int pitchCos) = FixedSinCos(round.PitchMicroRadians);

        // CRoundWiggle: two draws with the same [-1,+1) law as the weapon
        // scatter, applied to the travel direction for this integration
        // step only and then undone. Retail composes it as a matrix on the
        // velocity vector; Core composes the identical pair of Euler
        // samples onto the round's own (yaw, pitch), which leaves the
        // round's stored attitude bit-identical either way and differs only
        // within the single step by O(wiggle * pitch).
        int travelYaw = round.YawMicroRadians;
        int travelPitch = round.PitchMicroRadians;
        if (data.WiggleMicroRadians > 0)
        {
            (int wiggleYaw, int wigglePitch) = NextWiggle(data.WiggleMicroRadians);
            travelYaw = NormalizeMicroRad(travelYaw + wiggleYaw);
            // NextWiggle's pitch is retail's nose-down draw.
            travelPitch = NormalizeMicroRad(travelPitch - wigglePitch);
            (sin, cos) = FixedSinCos(travelYaw);
            (pitchSin, pitchCos) = FixedSinCos(travelPitch);
        }

        int speed = data.SpeedMillimetersPerBaseTick;
        var step = new SimVector3(
            DivideRoundNearest(
                (long)-MultiplyFixed(sin, pitchCos) * speed,
                FixedTrigScale),
            DivideRoundNearest((long)pitchSin * speed, FixedTrigScale),
            DivideRoundNearest(
                (long)MultiplyFixed(cos, pitchCos) * speed,
                FixedTrigScale));

        SimVector3 start = round.PositionMillimeters;
        var end = new SimVector3(
            checked(start.X + step.X),
            checked(start.Y + step.Y),
            checked(start.Z + step.Z));
        round.PositionMillimeters = end;
        round.ElapsedBaseTicks++;
        round.RemainingBaseTicks--;

        if (TryReportActorRoundImpact(round, data, start, end))
        {
            return false;
        }

        // The shipped round collides with the world through
        // CCollisionSeekingRound. The reconstruction models only the
        // terrain half of that: a round below the height field is spent.
        // No damage is produced, which matches a terrain impact.
        return end.Y >=
            Level100Terrain.Instance.SampleGroundElevationMillimeters(
                new SimVector2(end.X, end.Z));
    }

    /// <summary>
    /// The released homing law, round vtable slot 66 @<c>0x004d8e40</c>.
    ///
    /// <para>The gate is <c>CRoundDamage &gt;= 0</c> and
    /// <c>CRoundTurnRate &gt; 0</c> and a bound target and
    /// <c>SeekDelay &lt; age &lt; SeekTerminationTime</c>. Both surviving
    /// Level 100 seekers pass the first two by construction, and
    /// <c>Forseti Missile</c> carries no <c>CRoundSeekTerminationTime</c>, so
    /// the shipped default 1000.0 s (item[0x11] of
    /// <c>CRoundData__CreateAndRegisterByName</c> @<c>0x0042ffa0</c>) never
    /// expires inside its 10 s lifespan and is not represented.</para>
    ///
    /// <para>The direction to the target is taken into the round's own frame;
    /// if its forward component falls below <c>cos(CRoundSeekAngle)</c> the
    /// lock is dropped outright and never re-acquired - <c>CRoundSeek 3</c> is
    /// distinguished from <c>CRoundSeek 1</c> by exactly that, because
    /// <c>== 1</c> in
    /// <c>CRound__SelectBestTargetReaderAndSyncAimState</c> @<c>0x004dac90</c>
    /// is the only self-acquire path in the binary. Nothing in the shipped
    /// image distinguishes 2 from 3.</para>
    ///
    /// <para>The steering is a per-axis clamp to <c>CRoundTurnRate</c> in the
    /// body frame, exactly as the shipped body computes
    /// <c>yawErr = -atan2(localX, localForward)</c> and
    /// <c>pitchErr = atan2(localUpward, hypot(localX, localForward))</c> and
    /// clamps each independently before composing them onto the basis.</para>
    /// </summary>
    private void SteerSeekingRound(
        ActorRoundState round,
        Level100ActorRoundData data)
    {
        if (round.ElapsedBaseTicks <= data.SeekDelayBaseTicks)
        {
            return;
        }

        Level100ActorSnapshot target = _actors.GetActor(round.TargetActorId);
        if (!target.Active || target.Lifecycle != Level100ActorLifecycle.Alive)
        {
            round.Locked = false;
            return;
        }

        int yaw = round.YawMicroRadians;
        int pitch = round.PitchMicroRadians;
        if (SteerTowards(
                ref yaw,
                ref pitch,
                round.PositionMillimeters,
                _actors.GetPose(round.TargetActorId).PositionMillimeters,
                data.TurnRateMicroRadians,
                data.SeekAngleMicroRadians) == SeekSteering.LeftTheCone)
        {
            round.Locked = false;
            return;
        }

        round.YawMicroRadians = yaw;
        round.PitchMicroRadians = pitch;
    }

    /// <summary>
    /// The launch angle of a weapon mode with no <c>CWeaponLaunchAngle</c>
    /// entries: <c>0x004f8140(0, 1, 0)</c>, the integer-angle constructor in
    /// units of 2π/4096, which equals the float constructor at a pitch of
    /// float(2π/4096) (the RE lane's Euler-constructor control, 36 cases).
    /// Retail's z axis points down, so it tips every such round 1,534 µrad
    /// nose-down.
    /// </summary>
    internal static (int Yaw, int Pitch) DefaultLaunchAngle => (0, 1_534);

    /// <summary>
    /// The burst spawner's launch basis, orientation × launch angle × jitter
    /// (<c>0x00506ed1-0x005072d0</c>), reduced to the forward column its
    /// velocity uses. Each factor is <c>FMatrix(yaw, pitch, 0)</c> with
    /// retail's nose-down pitch; the jitter is the scatter pair, yaw from the
    /// second draw and pitch from the first. Shared by the Battle Engine's
    /// rounds and the actors'.
    /// </summary>
    internal static (int YawMicroRadians, int PitchMicroRadians) ComposeLaunchDirection(
        int baseYawMicroRadians,
        int basePitchMicroRadians,
        (int Yaw, int Pitch) launchAngle,
        (int Yaw, int Pitch) jitter)
    {
        // Retail axes: x right, y forward, z down. Core X = x, Z = y, up = -z.
        (long x, long y, long z) = RotateByEuler(jitter.Yaw, jitter.Pitch, 0, FixedTrigScale, 0);
        (x, y, z) = RotateByEuler(launchAngle.Yaw, launchAngle.Pitch, x, y, z);
        (x, y, z) = RotateByEuler(baseYawMicroRadians, basePitchMicroRadians, x, y, z);
        return (
            FixedAtan2(-x, y),
            FixedAtan2(z, IntegerSquareRoot((x * x) + (y * y))));
    }

    /// <summary>
    /// <c>FMatrix(yaw, pitch, 0)</c> times a Q30 vector: columns
    /// (cos y, sin y, 0), (−cos p sin y, cos p cos y, sin p) and
    /// (sin p sin y, −sin p cos y, cos p), the words <c>RetailUnitEuler</c>
    /// builds with zero roll.
    /// </summary>
    private static (long X, long Y, long Z) RotateByEuler(
        int yawMicroRadians,
        int pitchMicroRadians,
        long x,
        long y,
        long z)
    {
        (int yawSin, int yawCos) = FixedSinCos(yawMicroRadians);
        (int pitchSin, int pitchCos) = FixedSinCos(pitchMicroRadians);
        long forwardX = -MultiplyFixed(pitchCos, yawSin);
        long forwardY = MultiplyFixed(pitchCos, yawCos);
        long upX = MultiplyFixed(pitchSin, yawSin);
        long upY = -MultiplyFixed(pitchSin, yawCos);
        return (
            DivideRoundNearest((x * yawCos) + (y * forwardX) + (z * upX), FixedTrigScale),
            DivideRoundNearest((x * yawSin) + (y * forwardY) + (z * upY), FixedTrigScale),
            DivideRoundNearest((y * pitchSin) + (z * pitchCos), FixedTrigScale));
    }

    /// <summary>The outcome of one <see cref="SteerTowards"/> step.</summary>
    internal enum SeekSteering
    {
        Steered,
        NoDirection,
        LeftTheCone,
    }

    /// <summary>
    /// One steering step of <c>CRound::Move</c>'s guidance
    /// (<c>0x004d93cc-0x004d9838</c>) for a round without
    /// <c>CRoundWeirdoSeek</c>: take the aim point into the round's frame; when
    /// its forward component is below <c>cos(CRoundSeekAngle)</c> the target
    /// is dropped; otherwise turn by the yaw error <c>-atan2(x, y)</c> and the
    /// pitch error <c>atan2(z, √(x² + y²))</c>, each clamped to the turn rate.
    /// Shared by the actors' rounds and the Battle Engine's.
    /// </summary>
    internal static SeekSteering SteerTowards(
        ref int yawMicroRadians,
        ref int pitchMicroRadians,
        SimVector3 position,
        SimVector3 aimPoint,
        int turnRateMicroRadians,
        int seekAngleMicroRadians)
    {
        long deltaX = (long)aimPoint.X - position.X;
        long deltaY = (long)aimPoint.Y - position.Y;
        long deltaZ = (long)aimPoint.Z - position.Z;
        long length = IntegerSquareRoot(
            (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        if (length == 0)
        {
            return SeekSteering.NoDirection;
        }

        // The round's own frame, from its (yaw, pitch): forward is the third
        // basis column and up is the second in the Core pose projection.
        (int yawSin, int yawCos) = FixedSinCos(yawMicroRadians);
        (int pitchSin, int pitchCos) = FixedSinCos(pitchMicroRadians);
        long rightX = yawCos;
        long rightY = 0;
        long rightZ = yawSin;
        long upX = MultiplyFixed(yawSin, pitchSin);
        long upY = pitchCos;
        long upZ = -MultiplyFixed(yawCos, pitchSin);
        long forwardX = -MultiplyFixed(yawSin, pitchCos);
        long forwardY = pitchSin;
        long forwardZ = MultiplyFixed(yawCos, pitchCos);

        long localRight =
            ((deltaX * rightX) + (deltaY * rightY) + (deltaZ * rightZ)) / length;
        long localUp =
            ((deltaX * upX) + (deltaY * upY) + (deltaZ * upZ)) / length;
        long localForward =
            ((deltaX * forwardX) + (deltaY * forwardY) + (deltaZ * forwardZ)) /
            length;

        (_, int seekCosine) = FixedSinCos(seekAngleMicroRadians);
        if (localForward < seekCosine)
        {
            return SeekSteering.LeftTheCone;
        }

        int yawError = FixedAtan2(localRight, localForward);
        int pitchError = FixedAtan2(
            localUp,
            IntegerSquareRoot((localRight * localRight) +
                (localForward * localForward)));
        int cappedYaw = Math.Clamp(
            yawError,
            -turnRateMicroRadians,
            turnRateMicroRadians);
        int cappedPitch = Math.Clamp(
            pitchError,
            -turnRateMicroRadians,
            turnRateMicroRadians);

        // New forward = right*sin(cy)cos(cp) + up*sin(cp) + forward*cos(cy)cos(cp).
        // When neither axis clamps this reproduces the direction to the target
        // exactly, because the two errors were defined by that decomposition.
        (int cySin, int cyCos) = FixedSinCos(cappedYaw);
        (int cpSin, int cpCos) = FixedSinCos(cappedPitch);
        int alongRight = MultiplyFixed(cySin, cpCos);
        int alongForward = MultiplyFixed(cyCos, cpCos);
        int nextX =
            MultiplyFixed((int)rightX, alongRight) +
            MultiplyFixed((int)upX, cpSin) +
            MultiplyFixed((int)forwardX, alongForward);
        int nextY =
            MultiplyFixed((int)rightY, alongRight) +
            MultiplyFixed((int)upY, cpSin) +
            MultiplyFixed((int)forwardY, alongForward);
        int nextZ =
            MultiplyFixed((int)rightZ, alongRight) +
            MultiplyFixed((int)upZ, cpSin) +
            MultiplyFixed((int)forwardZ, alongForward);

        yawMicroRadians = FixedAtan2(-nextX, nextZ);
        pitchMicroRadians = FixedAtan2(
            nextY,
            IntegerSquareRoot(((long)nextX * nextX) + ((long)nextZ * nextZ)));
        return SeekSteering.Steered;
    }

    /// <summary>
    /// <c>CRoundWiggle</c>: two shared draws with the scatter law, the first
    /// for pitch and the second for yaw, taken first in every Move of a round
    /// whose wiggle is above zero (<c>0x004d8ffc</c>, <c>0x004d9036</c>).
    /// </summary>
    internal (int YawMicroRadians, int PitchMicroRadians) NextWiggle(int wiggleMicroRadians)
    {
        int first = _releasedRandom.NextSignedUnitScaled(wiggleMicroRadians);
        int second = _releasedRandom.NextSignedUnitScaled(wiggleMicroRadians);
        return (second, first);
    }

    /// <summary>
    /// Tests the round's swept line against the designated Battle Engine's
    /// primary finite cylinder. The shipped round has no <c>CRoundRadius</c>
    /// node (default 0), so the whole envelope is the target cylinder installed
    /// by <c>CBattleEngine::SetCollisionShape</c> at <c>0x004063B0</c>.
    /// </summary>
    private bool TryReportActorRoundImpact(
        ActorRoundState round,
        Level100ActorRoundData data,
        SimVector3 start,
        SimVector3 end)
    {
        Level100ActorSnapshot target = _actors.GetActor(round.TargetActorId);
        if (!target.Active || target.Lifecycle != Level100ActorLifecycle.Alive)
        {
            return false;
        }

        Level100ActorPoseSnapshot targetPose =
            _actors.GetPose(round.TargetActorId);
        if (!TryResolveBattleEngineCylinderContact(
                start,
                end,
                targetPose.PositionMillimeters,
                out SimVector3 selectedPosition))
        {
            return false;
        }

        // A dying round still meets its target, but its own Hit (slot 39,
        // 0x004d8ae0) returns at 0x004d8af7 while the dying bit is set: no
        // damage, no impact explosion, no death. Only the target's script hit
        // notification remains (the RE lane's "No hit while dying").
        _actorRoundImpacts.Add(new Level100ActorRoundImpactReceipt(
            new Level100ActorRoundImpact(
                round.TargetActorId,
                round.OwnerActorId,
                round.Kind,
                selectedPosition,
                data.IncomingDamageMilliLife),
            round.Id,
            ContactOnly: round.Dying));
        return true;
    }

    /// <summary>
    /// Released mode-1 <c>CLine</c>-versus-<c>CCylinder</c> response reduced to
    /// Core's deterministic millimetre boundary. The exact finite-cylinder
    /// overlap is the prefilter. Retail then constructs two clamped candidates
    /// from <c>(|start|-radius)/|delta|</c>, rejects candidates beyond the same
    /// cap, and selects the first. Q32 square roots approximate the released
    /// binary32/x87 spills; they are not asserted bit-identical.
    /// </summary>
    internal static bool TryResolveBattleEngineCylinderContact(
        SimVector3 start,
        SimVector3 end,
        SimVector3 battleEnginePosition,
        out SimVector3 selectedPosition)
    {
        selectedPosition = default;
        long cylinderCenterY = (long)battleEnginePosition.Y -
            SimulationConstants.Level100PlayerCollisionCenterBelowOriginMillimeters;
        long startX = (long)start.X - battleEnginePosition.X;
        long startY = (long)start.Y - cylinderCenterY;
        long startZ = (long)start.Z - battleEnginePosition.Z;
        long deltaX = (long)end.X - start.X;
        long deltaY = (long)end.Y - start.Y;
        long deltaZ = (long)end.Z - start.Z;
        long halfHeight =
            SimulationConstants.Level100PlayerCollisionHalfHeightMillimeters;
        long radius =
            SimulationConstants.Level100PlayerContactRadiusMillimeters;

        bool intersects = FitsFastCylinderArithmetic(
            startX,
            startY,
            startZ,
            deltaX,
            deltaY,
            deltaZ,
            radius,
            halfHeight)
            ? IntersectsFiniteCylinderFast(
                startX,
                startY,
                startZ,
                deltaX,
                deltaY,
                deltaZ,
                radius,
                halfHeight)
            : IntersectsFiniteCylinder(
                startX,
                startY,
                startZ,
                deltaX,
                deltaY,
                deltaZ,
                radius,
                halfHeight);
        if (!intersects)
        {
            return false;
        }

        const int FixedShift = 32;
        BigInteger one = BigInteger.One << FixedShift;
        BigInteger startXBig = startX;
        BigInteger startYBig = startY;
        BigInteger startZBig = startZ;
        BigInteger deltaXBig = deltaX;
        BigInteger deltaYBig = deltaY;
        BigInteger deltaZBig = deltaZ;
        BigInteger startLengthQ = IntegerSquareRootBig(
            ((startXBig * startXBig) +
                (startYBig * startYBig) +
                (startZBig * startZBig)) <<
            (FixedShift * 2));
        BigInteger deltaLengthQ = IntegerSquareRootBig(
            ((deltaXBig * deltaXBig) +
                (deltaYBig * deltaYBig) +
                (deltaZBig * deltaZBig)) <<
            (FixedShift * 2));
        BigInteger firstParameterQ = BigInteger.Zero;
        BigInteger secondParameterQ = BigInteger.Zero;
        if (!deltaLengthQ.IsZero)
        {
            firstParameterQ = ClampUnit(
                ((startLengthQ - ((BigInteger)radius * one)) << FixedShift) /
                deltaLengthQ,
                one);
            secondParameterQ = ClampUnit(
                ((startLengthQ + ((BigInteger)radius * one)) << FixedShift) /
                deltaLengthQ,
                one);
        }

        BigInteger firstAxisQ = (startYBig * one) +
            (deltaYBig * firstParameterQ);
        BigInteger secondAxisQ = (startYBig * one) +
            (deltaYBig * secondParameterQ);
        BigInteger capQ = (BigInteger)halfHeight * one;
        if ((firstAxisQ > capQ && secondAxisQ > capQ) ||
            (firstAxisQ < -capQ && secondAxisQ < -capQ))
        {
            return false;
        }

        selectedPosition = new SimVector3(
            checked((int)((BigInteger)start.X + DivideRoundNearest(
                deltaXBig * firstParameterQ,
                one))),
            checked((int)((BigInteger)start.Y + DivideRoundNearest(
                deltaYBig * firstParameterQ,
                one))),
            checked((int)((BigInteger)start.Z + DivideRoundNearest(
                deltaZBig * firstParameterQ,
                one))));
        return true;

        static bool FitsFastCylinderArithmetic(
            long startX,
            long startY,
            long startZ,
            long deltaX,
            long deltaY,
            long deltaZ,
            long radius,
            long halfHeight)
        {
            // This bound keeps the largest squared rational numerator below
            // signed Int128. Full Int32-domain inputs fall back to BigInteger.
            const long Limit = 1_000_000;
            return Math.Abs(startX) <= Limit &&
                Math.Abs(startY) <= Limit &&
                Math.Abs(startZ) <= Limit &&
                Math.Abs(deltaX) <= Limit &&
                Math.Abs(deltaY) <= Limit &&
                Math.Abs(deltaZ) <= Limit &&
                radius <= Limit &&
                halfHeight <= Limit;
        }

        static bool IntersectsFiniteCylinderFast(
            long startX,
            long startY,
            long startZ,
            long deltaX,
            long deltaY,
            long deltaZ,
            long radius,
            long halfHeight)
        {
            (Int128 Numerator, Int128 Denominator) minimum = (0, 1);
            (Int128 Numerator, Int128 Denominator) maximum = (1, 1);

            if (deltaY == 0)
            {
                if (Math.Abs(startY) > halfHeight)
                {
                    return false;
                }
            }
            else
            {
                (Int128 Numerator, Int128 Denominator) first =
                    NormalizeFastFraction(-halfHeight - startY, deltaY);
                (Int128 Numerator, Int128 Denominator) second =
                    NormalizeFastFraction(halfHeight - startY, deltaY);
                if (CompareFastFractions(first, second) > 0)
                {
                    (first, second) = (second, first);
                }
                if (CompareFastFractions(first, minimum) > 0)
                {
                    minimum = first;
                }
                if (CompareFastFractions(second, maximum) < 0)
                {
                    maximum = second;
                }
                if (CompareFastFractions(minimum, maximum) > 0)
                {
                    return false;
                }
            }

            Int128 radialLengthSquared =
                ((Int128)deltaX * deltaX) + ((Int128)deltaZ * deltaZ);
            (Int128 Numerator, Int128 Denominator) closest = minimum;
            if (radialLengthSquared != 0)
            {
                closest = NormalizeFastFraction(
                    -(((Int128)startX * deltaX) +
                        ((Int128)startZ * deltaZ)),
                    radialLengthSquared);
                if (CompareFastFractions(closest, minimum) < 0)
                {
                    closest = minimum;
                }
                else if (CompareFastFractions(closest, maximum) > 0)
                {
                    closest = maximum;
                }
            }

            Int128 closestX = ((Int128)startX * closest.Denominator) +
                ((Int128)deltaX * closest.Numerator);
            Int128 closestZ = ((Int128)startZ * closest.Denominator) +
                ((Int128)deltaZ * closest.Numerator);
            Int128 radialLimit = (Int128)radius * radius *
                closest.Denominator * closest.Denominator;
            return (closestX * closestX) + (closestZ * closestZ) <=
                radialLimit;
        }

        static bool IntersectsFiniteCylinder(
            long startX,
            long startY,
            long startZ,
            long deltaX,
            long deltaY,
            long deltaZ,
            long radius,
            long halfHeight)
        {
            BigInteger startXBig = startX;
            BigInteger startYBig = startY;
            BigInteger startZBig = startZ;
            BigInteger deltaXBig = deltaX;
            BigInteger deltaYBig = deltaY;
            BigInteger deltaZBig = deltaZ;
            BigInteger radiusBig = radius;
            BigInteger halfHeightBig = halfHeight;
            (BigInteger Numerator, BigInteger Denominator) minimum = (0, 1);
            (BigInteger Numerator, BigInteger Denominator) maximum = (1, 1);

            if (deltaY == 0)
            {
                if (BigInteger.Abs(startYBig) > halfHeightBig)
                {
                    return false;
                }
            }
            else
            {
                (BigInteger Numerator, BigInteger Denominator) first =
                    NormalizeFraction(-halfHeightBig - startYBig, deltaYBig);
                (BigInteger Numerator, BigInteger Denominator) second =
                    NormalizeFraction(halfHeightBig - startYBig, deltaYBig);
                if (CompareFractions(first, second) > 0)
                {
                    (first, second) = (second, first);
                }
                if (CompareFractions(first, minimum) > 0)
                {
                    minimum = first;
                }
                if (CompareFractions(second, maximum) < 0)
                {
                    maximum = second;
                }
                if (CompareFractions(minimum, maximum) > 0)
                {
                    return false;
                }
            }

            BigInteger radialLengthSquared =
                (deltaXBig * deltaXBig) + (deltaZBig * deltaZBig);
            (BigInteger Numerator, BigInteger Denominator) closest = minimum;
            if (!radialLengthSquared.IsZero)
            {
                closest = NormalizeFraction(
                    -((startXBig * deltaXBig) + (startZBig * deltaZBig)),
                    radialLengthSquared);
                if (CompareFractions(closest, minimum) < 0)
                {
                    closest = minimum;
                }
                else if (CompareFractions(closest, maximum) > 0)
                {
                    closest = maximum;
                }
            }

            BigInteger closestX =
                (startXBig * closest.Denominator) +
                (deltaXBig * closest.Numerator);
            BigInteger closestZ =
                (startZBig * closest.Denominator) +
                (deltaZBig * closest.Numerator);
            return (closestX * closestX) + (closestZ * closestZ) <=
                radiusBig * radiusBig *
                closest.Denominator * closest.Denominator;
        }

        static (Int128 Numerator, Int128 Denominator) NormalizeFastFraction(
            Int128 numerator,
            Int128 denominator)
        {
            return denominator < 0
                ? (-numerator, -denominator)
                : (numerator, denominator);
        }

        static int CompareFastFractions(
            (Int128 Numerator, Int128 Denominator) left,
            (Int128 Numerator, Int128 Denominator) right)
        {
            return (left.Numerator * right.Denominator).CompareTo(
                right.Numerator * left.Denominator);
        }

        static (BigInteger Numerator, BigInteger Denominator) NormalizeFraction(
            BigInteger numerator,
            BigInteger denominator)
        {
            return denominator.Sign < 0
                ? (-numerator, -denominator)
                : (numerator, denominator);
        }

        static int CompareFractions(
            (BigInteger Numerator, BigInteger Denominator) left,
            (BigInteger Numerator, BigInteger Denominator) right)
        {
            return (left.Numerator * right.Denominator).CompareTo(
                right.Numerator * left.Denominator);
        }

        static BigInteger ClampUnit(BigInteger value, BigInteger one) =>
            value.Sign < 0 ? BigInteger.Zero : BigInteger.Min(value, one);

        static BigInteger DivideRoundNearest(
            BigInteger numerator,
            BigInteger denominator)
        {
            BigInteger half = denominator >> 1;
            return numerator.Sign >= 0
                ? (numerator + half) / denominator
                : (numerator - half) / denominator;
        }

        static BigInteger IntegerSquareRootBig(BigInteger value)
        {
            if (value.Sign <= 0)
            {
                return BigInteger.Zero;
            }

            int initialShift = checked((int)((value.GetBitLength() + 1) / 2));
            BigInteger root = BigInteger.One << initialShift;
            while (true)
            {
                BigInteger next = (root + (value / root)) >> 1;
                if (next >= root)
                {
                    return root;
                }
                root = next;
            }
        }
    }

    private static (int Yaw, int Pitch) ReadPoseYawPitch(
        Level100ActorPoseSnapshot pose)
    {
        int forwardX = FloatBitsToQ30(pose.BasisFloatBits.Row0Z);
        int forwardY = FloatBitsToQ30(pose.BasisFloatBits.Row1Z);
        int forwardZ = FloatBitsToQ30(pose.BasisFloatBits.Row2Z);
        int horizontal = Q30Hypotenuse(forwardX, forwardZ);
        return (FixedAtan2(-forwardX, forwardZ), FixedAtan2(forwardY, horizontal));
    }

    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }
        long root = 0;
        long bit = 1L << 62;
        while (bit > value)
        {
            bit >>= 2;
        }
        long remainder = value;
        while (bit != 0)
        {
            if (remainder >= root + bit)
            {
                remainder -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }
            bit >>= 2;
        }
        return root;
    }
}
