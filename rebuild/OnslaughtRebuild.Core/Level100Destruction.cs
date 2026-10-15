// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

public enum Level100DestructionEventKind : byte
{
    PulseImpact = 0,
    SegmentDamaged = 1,
    SegmentDetached = 2,
    ActiveSubtreeBelowHalf = 3,
    Terminal = 4,
    VulcanImpact = 5,
    MicroMissileImpact = 6,
}

public enum Level100DestructionEffectKind : byte
{
    None = 0,
    PulseImpact = 1,
    TargetDestroyed = 2,
    FacilityDestroyed = 3,
    DroneDestroyed = 4,
    VulcanImpact = 5,
    MicroMissileImpact = 6,
}

public readonly record struct Level100DestructionEvent(
    Level100DestructionEventKind Kind,
    Level100DestructionEffectKind EffectKind,
    int ActorId,
    int PartIndex,
    uint RemainingHealthBits,
    Level100Vector3 Position);

public sealed class Level100DestructionSnapshot
{
    private readonly uint[] _initialHealthBits;
    private readonly uint[] _currentHealthBits;
    private readonly byte[] _partActivity;

    internal Level100DestructionSnapshot(
        int actorId,
        string definitionName,
        uint currentLifeBits,
        bool terminal,
        bool belowHalfReported,
        uint[] initialHealthBits,
        uint[] currentHealthBits,
        byte[] partActivity)
    {
        ActorId = actorId;
        DefinitionName = definitionName;
        CurrentLifeBits = currentLifeBits;
        Terminal = terminal;
        BelowHalfReported = belowHalfReported;
        _initialHealthBits = initialHealthBits;
        _currentHealthBits = currentHealthBits;
        _partActivity = partActivity;
    }

    public int ActorId { get; }

    public string DefinitionName { get; }

    public uint CurrentLifeBits { get; }

    public bool Terminal { get; }

    public bool BelowHalfReported { get; }

    public ReadOnlyMemory<uint> InitialHealthBits => _initialHealthBits;

    public ReadOnlyMemory<uint> CurrentHealthBits => _currentHealthBits;

    public ReadOnlyMemory<byte> PartActivity => _partActivity;

    internal uint[] CopyInitialHealthBits() => (uint[])_initialHealthBits.Clone();

    internal uint[] CopyCurrentHealthBits() => (uint[])_currentHealthBits.Clone();

    internal byte[] CopyPartActivity() => (byte[])_partActivity.Clone();

}

public sealed record Level100GroundShutdownSnapshot(
    int ActorId, uint AdmissionFrame, uint DeliveryFrame, uint DueTimeBits);

public sealed record Level100DestructionRuntimeSnapshot(
    IReadOnlyList<Level100DestructionSnapshot> Actors)
{
    // FIFO insertion order, including equal-time admissions.
    public IReadOnlyList<Level100GroundShutdownSnapshot> PendingShutdowns { get; init; } = [];
}

/// <summary>
/// Segment/contact mechanics attached to actors owned by the native registry.
/// This owner neither creates actors nor advances mission state: it consumes
/// exact identity, mesh, active state and full mutable pose, then reports the
/// released hit/dying/died facts back through the registry.
/// </summary>
public sealed class Level100DestructionRuntime
{
    private readonly Level100ActorRegistry _registry;
    private readonly SortedDictionary<int, Level100DestructionState> _states = [];
    private readonly List<Level100DestructionEvent> _events = [];
    private readonly List<Level100GroundShutdownSnapshot> _pendingShutdowns = [];
    private Level100ContactActor[] _contactActors = [];
    private readonly Level100DestructionEvent[] _hitEvents =
        new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];

    public Level100DestructionRuntime(Level100ActorRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        SynchronizeActors(requireInitialState: true);
    }

    public Level100DestructionRuntime(
        Level100ActorRegistry registry,
        Level100DestructionRuntimeSnapshot snapshot)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Actors);
        if (snapshot.Actors.Any(item => item is null) ||
            snapshot.Actors.Select(item => item.ActorId).Distinct().Count() !=
                snapshot.Actors.Count)
        {
            throw new ArgumentException(
                "The destruction runtime snapshot has invalid actor identities.",
                nameof(snapshot));
        }

        SynchronizeActors(
            requireInitialState: false,
            validateExisting: false);
        foreach (Level100DestructionSnapshot actor in snapshot.Actors)
        {
            if (!_states.TryGetValue(actor.ActorId, out Level100DestructionState? state))
            {
                throw new ArgumentException(
                    "The destruction snapshot refers to a non-destructible actor.",
                    nameof(snapshot));
            }
            state.Restore(actor);
        }
        if (_states.Count != snapshot.Actors.Count)
        {
            throw new ArgumentException(
                "The destruction snapshot omits a registered destructible actor.",
                nameof(snapshot));
        }
        ArgumentNullException.ThrowIfNull(snapshot.PendingShutdowns);
        foreach (Level100GroundShutdownSnapshot pending in snapshot.PendingShutdowns)
        {
            if (pending is null ||
                !_states.TryGetValue(pending.ActorId, out Level100DestructionState? state) ||
                state.Definition.Kind != Level100DefinitionKind.TargetTank ||
                !state.Terminal ||
                _pendingShutdowns.Any(item => item.ActorId == pending.ActorId) ||
                pending != GroundShutdownAt(pending.ActorId, pending.AdmissionFrame))
                throw new ArgumentException("Invalid pending ground-unit shutdown.", nameof(snapshot));
            _pendingShutdowns.Add(pending);
        }
        ValidateRegistryInvariants();
    }

    public Level100DestructionRuntimeSnapshot Snapshot => new(
        Array.AsReadOnly(_states.Values
            .Select(state => state.CaptureSnapshot())
            .ToArray()))
    {
        PendingShutdowns = Array.AsReadOnly(_pendingShutdowns.ToArray()),
    };

    public IReadOnlyList<Level100DestructionEvent> Events =>
        Array.AsReadOnly(_events.ToArray());

    internal void RegisterActor(Level100ActorId actorId)
    {
        Level100ActorSnapshot actor = _registry.GetActor(actorId);
        _ = EnsureState(actor);
    }

    internal void BeginTick()
    {
        _events.Clear();
        SynchronizeActors(requireInitialState: false);
    }

    /// <summary>
    /// Event 4000's delivery for a round that carries <c>CRoundExplode</c>: it
    /// explodes in the air where it is (<c>0x004d9a54</c>, then
    /// <c>0x004d9f30</c> in mode 0) and dies. Only the explosion's effect is
    /// reported; its radius damage is not modelled.
    /// </summary>
    internal void ReportAirBurst(SimVector3 position, Level100DestructionEffectKind effectKind) =>
        _events.Add(new Level100DestructionEvent(
            Level100DestructionState.RoundImpactEventKind(effectKind),
            effectKind,
            0,
            -1,
            0,
            ToContactVector(position)));

    internal void FlushStartOfFrame(uint eventFrameCount)
    {
        // CEventManager drains the selected ring bucket without comparing
        // mTime with each stored due time. Only call when the manager advanced.
        for (int i = 0; i < _pendingShutdowns.Count;)
        {
            Level100GroundShutdownSnapshot pending = _pendingShutdowns[i];
            if (pending.DeliveryFrame != eventFrameCount) { i++; continue; }
            _registry.ShutdownGroundUnit(new Level100ActorId(pending.ActorId));
            _pendingShutdowns.RemoveAt(i);
        }
    }

    private static Level100GroundShutdownSnapshot GroundShutdownAt(int actorId, uint frame)
    {
        // GroundVehicle slot112 returns 0.5f (0x0050e890); 0x004fd11f
        // stores managerTime+delay before AddEvent(2000, unit, ..., START).
        float now = RetailEventScheduler.TimeAtFrameCount(frame);
        float due = RetailEventScheduler.RelativeDueTime(now, 0.5f);
        bool immediate = RetailEventScheduler.IsImmediate(now, due);
        int offset = immediate ? 0 : RetailEventScheduler.DelayBufferOffset(now, due);
        if (!immediate && (due > RetailEventScheduler.MaximumTime ||
            offset >= RetailEventScheduler.OverflowBucketThreshold))
            throw new NotSupportedException("Ground shutdown is outside the admitted ring lifetime.");
        return new(actorId, frame, unchecked(frame + (uint)offset + 1),
            BitConverter.SingleToUInt32Bits(due));
    }

    internal void ValidateExternalFacts(
        IReadOnlyList<Level100SimulationFact>? facts)
    {
        if (facts is null)
        {
            return;
        }

        foreach (Level100SimulationFact fact in facts)
        {
            ArgumentNullException.ThrowIfNull(fact);
            switch (fact)
            {
                case Level100ActorHealthFact health:
                    RejectOwnedExternalMutation(health.ActorId);
                    break;
                case Level100ActorStartedDyingFact startedDying:
                    RejectOwnedExternalMutation(startedDying.ActorId);
                    break;
                case Level100ActorDiedFact died:
                    RejectOwnedExternalMutation(died.ActorId);
                    break;
            }
        }
    }

    internal void SetExternalHealth(Level100ActorId actorId, int health)
    {
        RejectOwnedExternalMutation(actorId);
        _registry.SetHealth(actorId, health);
    }

    internal void ReportExternalStartedDying(Level100ActorId actorId)
    {
        RejectOwnedExternalMutation(actorId);
        _registry.ReportStartedDying(actorId);
    }

    /// <summary>
    /// AirUnit +c8 (00403690) starts dying after a vulnerable aircraft loses
    /// its spawner. This does not destroy its segments or issue Died/Shutdown.
    /// Attached-child and particle-link teardown are not yet represented.
    /// </summary>
    internal void StartPlaneDeathAfterSpawnerLoss(Level100ActorId actorId)
    {
        _registry.ReportPlaneStartedDying(actorId);
    }

    internal void ReportExternalDied(Level100ActorId actorId)
    {
        RejectOwnedExternalMutation(actorId);
        _registry.ReportDied(actorId);
    }

    /// <summary>
    /// Retains the existing Medium pulse damage stages. Large uses
    /// <see cref="TryApplyRoundSweep(SimVector3, SimVector3, int, uint, Level100DestructionEffectKind, out Level100ContactHit, uint)"/>
    /// with its own radius and direct damage while its spatial blast is open.
    /// </summary>
    public bool TryApplyPulseSweep(
        SimVector3 start,
        SimVector3 end,
        out Level100ContactHit hit,
        uint eventFrameCount = 0) =>
        TryApplyRoundSweep(
            start,
            end,
            Level100ContactMechanics.PulseRadiusMillimeters,
            Level100DestructionState.PulseDamageBits,
            Level100DestructionEffectKind.PulseImpact,
            preservePulseDamageStages: true,
            eventFrameCount,
            out hit);

    /// <summary>
    /// Sweeps one round of the given contact radius and applies its damage.
    /// </summary>
    public bool TryApplyRoundSweep(
        SimVector3 start,
        SimVector3 end,
        int contactRadiusMillimeters,
        uint damageBits,
        Level100DestructionEffectKind impactEffectKind,
        out Level100ContactHit hit,
        uint eventFrameCount = 0) =>
        TryApplyRoundSweep(
            start,
            end,
            contactRadiusMillimeters,
            damageBits,
            impactEffectKind,
            preservePulseDamageStages: false,
            eventFrameCount,
            out hit);

    /// <summary>
    /// A dying round's step (the RE lane's "No hit while dying"): the contact
    /// still reaches both things' Hit, but the round's own Hit returns before
    /// damage, its impact explosion and its death. Only the struck thing's
    /// script notification remains, which <see cref="TryApplyRoundSweep(SimVector3, SimVector3, int, uint, Level100DestructionEffectKind, out Level100ContactHit, uint)"/>
    /// reports for the same things. Returns whether the step met anything.
    /// </summary>
    internal bool TryReportDyingRoundContact(SimVector3 start, SimVector3 end, int contactRadiusMillimeters)
    {
        int contactActorCount = GatherContactActors(isCandidate: null, atRest: false);
        if (!Level100ContactMechanics.TrySweepRoundWithTerrain(
                ToContactVector(start),
                ToContactVector(end),
                contactRadiusMillimeters,
                _contactActors.AsSpan(0, contactActorCount),
                out Level100ContactHit hit))
        {
            return false;
        }

        if (hit.ActorId != 0)
        {
            var actorId = new Level100ActorId(hit.ActorId);
            if (_states.ContainsKey(hit.ActorId) || _registry.GetActor(actorId).ScriptName is not null)
            {
                _registry.ReportHit(actorId, otherThingTypeMask: Level100ReleasedThingTypeMasks.Ammunition);
            }
        }
        return true;
    }

    /// <summary>
    /// The crosshair's line query: <c>CWorld::FindFirstThingToHitLine</c>
    /// (<c>0x0050b030</c>) at mesh level, which traces the heightfield first and
    /// takes a thing only when it is not farther than the ground. The line is
    /// instantaneous, so every candidate is presented at rest. The caller
    /// supplies retail's candidate filter (<c>BattleEngine.cpp:2320</c> skips
    /// the Battle Engine, trees and rounds; the query skips dying
    /// non-buildings). The hit's time is parts per million of the line.
    /// </summary>
    internal bool TryFindFirstThingOnLine(
        SimVector3 start,
        SimVector3 end,
        Func<Level100ActorSnapshot, Level100ContactDefinition, bool> isCandidate,
        out Level100ContactHit hit)
    {
        ArgumentNullException.ThrowIfNull(isCandidate);
        int contactActorCount = GatherContactActors(isCandidate, atRest: true);
        return Level100ContactMechanics.TrySweepRoundWithTerrain(
            ToContactVector(start),
            ToContactVector(end),
            contactRadiusMillimeters: 0,
            _contactActors.AsSpan(0, contactActorCount),
            out hit);
    }

    private int GatherContactActors(
        Func<Level100ActorSnapshot, Level100ContactDefinition, bool>? isCandidate,
        bool atRest)
    {
        SynchronizeActors(requireInitialState: false);
        Level100ActorRegistrySnapshot registrySnapshot = _registry.Snapshot;
        if (_contactActors.Length < registrySnapshot.Actors.Count)
        {
            Array.Resize(
                ref _contactActors,
                registrySnapshot.Actors.Count);
        }

        int contactActorCount = 0;
        foreach (Level100ActorSnapshot actor in registrySnapshot.Actors)
        {
            if (!actor.Active ||
                actor.Lifecycle == Level100ActorLifecycle.Destroyed ||
                actor.DefinitionName is null ||
                !Level100ContactCatalog.Instance.TryGetDefinition(
                    actor.DefinitionName,
                    out Level100ContactDefinition? definition) ||
                definition is null)
            {
                continue;
            }
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    actor.MeshBinding,
                    definition.Mesh))
            {
                throw new InvalidDataException(
                    $"Level 100 actor {actor.ActorId.Value} definition/mesh binding changed.");
            }
            if (isCandidate is not null && !isCandidate(actor, definition))
            {
                continue;
            }

            ReadOnlyMemory<byte> partActivity =
                _states.TryGetValue(
                    actor.ActorId.Value,
                    out Level100DestructionState? state)
                    ? state.ContactPartActivity
                    : default;
            _contactActors[contactActorCount++] = new Level100ContactActor(
                actor.ActorId.Value,
                active: true,
                ToContactTransform(actor.Pose),
                atRest
                    ? Level100Vector3.Zero
                    : ToContactVector(actor.Pose.LinearVelocityMillimetersPerTick),
                definition,
                partActivity: partActivity);
        }

        return contactActorCount;
    }

    private bool TryApplyRoundSweep(
        SimVector3 start,
        SimVector3 end,
        int contactRadiusMillimeters,
        uint damageBits,
        Level100DestructionEffectKind impactEffectKind,
        bool preservePulseDamageStages,
        uint eventFrameCount,
        out Level100ContactHit hit)
    {
        int contactActorCount = GatherContactActors(isCandidate: null, atRest: false);
        if (!Level100ContactMechanics.TrySweepRoundWithTerrain(
                ToContactVector(start),
                ToContactVector(end),
                contactRadiusMillimeters,
                _contactActors.AsSpan(0, contactActorCount),
                out hit))
        {
            return false;
        }
        if (hit.ActorId == 0 ||
            !_states.TryGetValue(
                hit.ActorId,
                out Level100DestructionState? destruction))
        {
            _events.Add(Level100DestructionState.CreateRoundImpactEvent(
                hit,
                impactEffectKind));
            if (hit.ActorId != 0 &&
                _registry.GetActor(new Level100ActorId(hit.ActorId))
                    .ScriptName is not null)
            {
                // A struck actor that carries no destruction state is a
                // script-bearing static: the Tank Factory, the Hangar, the
                // Facilities and the four turrets. Released `CThing::Hit`
                // dispatches to the attached script regardless of whether the
                // thing is destructible, and TankFactory.msl / Facilities.msl /
                // Turret.msl all define `hit(otherThing)` with a
                // THING_TYPE_AMMUNITION test. Without this report
                // `Hit Friendly Building` and `Broke Tutorial` can never be
                // posted. No health is touched, because no destruction state
                // exists to touch. Statics with no attached script produce
                // nothing observable, so they are not reported.
                _registry.ReportHit(
                    new Level100ActorId(hit.ActorId),
                    otherThingTypeMask:
                        Level100ReleasedThingTypeMasks.Ammunition);
            }
            return true;
        }

        int eventCount = preservePulseDamageStages
            ? destruction.ApplyPulseHit(hit, _hitEvents)
            : destruction.ApplyRoundHit(
                hit,
                damageBits,
                impactEffectKind,
                _hitEvents);
        for (int index = 0; index < eventCount; index++)
        {
            _events.Add(_hitEvents[index]);
        }

        Level100ActorId actorId = new(hit.ActorId);
        _registry.ReportHit(
            actorId,
            otherThingTypeMask: Level100ReleasedThingTypeMasks.Ammunition);
        _registry.SetHealth(actorId, destruction.RegistryHealth);
        if (destruction.Terminal)
        {
            if (destruction.Definition.Kind == Level100DefinitionKind.TargetTank)
            {
                if (_registry.GetActor(actorId).Lifecycle == Level100ActorLifecycle.Alive)
                {
                    Level100GroundShutdownSnapshot pending = GroundShutdownAt(hit.ActorId, eventFrameCount);
                    if (_registry.ReportGroundUnitDied(actorId)) _pendingShutdowns.Add(pending);
                }
            }
            else
            {
                _registry.ReportStartedDying(actorId);
                _registry.ReportDied(actorId);
            }
        }
        ValidateRegistryInvariant(
            _registry.GetActor(actorId),
            destruction);
        return true;
    }

    private void RejectOwnedExternalMutation(Level100ActorId actorId)
    {
        if (_states.ContainsKey(actorId.Value))
        {
            throw new InvalidOperationException(
                $"Level 100 actor {actorId.Value} health and destruction lifecycle " +
                "are owned by segmented destruction contact.");
        }
    }

    private void SynchronizeActors(
        bool requireInitialState,
        bool validateExisting = true)
    {
        foreach (Level100ActorSnapshot actor in _registry.Snapshot.Actors
            .OrderBy(item => item.ActorId.Value))
        {
            Level100DestructionState? state = EnsureState(
                actor,
                requireInitialState);
            if (state is not null &&
                !requireInitialState &&
                validateExisting)
            {
                ValidateRegistryInvariant(actor, state);
            }
        }
    }

    private Level100DestructionState? EnsureState(
        Level100ActorSnapshot actor,
        bool requireInitialState = true)
    {
        if (actor.DefinitionName is null ||
            !Level100ContactCatalog.Instance.TryGetDefinition(
                actor.DefinitionName,
                out Level100ContactDefinition? definition) ||
            definition is null ||
            definition.Kind is not (
                Level100DefinitionKind.TargetTank or
                Level100DefinitionKind.TargetDrone or
                Level100DefinitionKind.Warehouse))
        {
            return null;
        }
        if (!StringComparer.OrdinalIgnoreCase.Equals(
                actor.MeshBinding,
                definition.Mesh))
        {
            throw new InvalidDataException(
                $"Level 100 actor {actor.ActorId.Value} definition/mesh binding changed.");
        }

        if (_states.TryGetValue(
                actor.ActorId.Value,
                out Level100DestructionState? existing))
        {
            if (!ReferenceEquals(existing.Definition, definition))
            {
                throw new InvalidDataException(
                    "A Level 100 actor changed destruction definition.");
            }
            return existing;
        }

        int expectedHealth = checked((int)MathF.Round(
            definition.MaximumLife * 1_000f,
            MidpointRounding.AwayFromZero));
        if (requireInitialState &&
            (actor.Health != expectedHealth ||
             actor.Lifecycle != Level100ActorLifecycle.Alive))
        {
            throw new InvalidDataException(
                $"Level 100 actor {actor.ActorId.Value} is not in the released " +
                $"{definition.Name} initial state.");
        }
        var state = new Level100DestructionState(actor.ActorId.Value, definition);
        _states.Add(actor.ActorId.Value, state);
        return state;
    }

    private void ValidateRegistryInvariants()
    {
        foreach (Level100DestructionState state in _states.Values)
        {
            ValidateRegistryInvariant(
                _registry.GetActor(new Level100ActorId(state.ActorId)),
                state);
        }
    }

    private void ValidateRegistryInvariant(
        Level100ActorSnapshot actor,
        Level100DestructionState destruction)
    {
        bool pending = _pendingShutdowns.Any(item => item.ActorId == actor.ActorId.Value);
        Level100ActorLifecycle expectedLifecycle = destruction.Terminal
            ? pending ? Level100ActorLifecycle.DiedAwaitingShutdown : Level100ActorLifecycle.Destroyed
            : Level100ActorLifecycle.Alive;
        bool dyingPlane = !destruction.Terminal && actor.Lifecycle == Level100ActorLifecycle.StartedDying &&
            destruction.Definition.Kind == Level100DefinitionKind.TargetDrone &&
            _registry.GetBaseState(actor.ActorId).RetailPlane is not null;
        if (actor.Health != destruction.RegistryHealth ||
            actor.Lifecycle != expectedLifecycle && !dyingPlane ||
            (destruction.Terminal && !pending && actor.Active) ||
            (pending && (!destruction.Terminal ||
                destruction.Definition.Kind != Level100DefinitionKind.TargetTank)))
        {
            throw new InvalidDataException(
                $"Level 100 actor {actor.ActorId.Value} registry and segmented " +
                "destruction state disagree.");
        }
    }

    private static Level100Transform3 ToContactTransform(
        Level100ActorPoseSnapshot pose) =>
        new(ToContactVector(pose.PositionMillimeters), ToContactBasis(pose.BasisFloatBits));

    private static Level100Vector3 ToContactVector(SimVector3 vector) =>
        new(vector.X, vector.Z, checked(-vector.Y));

    private static Level100Basis3 ToContactBasis(Level100FloatBasis3Bits core)
    {
        static int Component(int bits)
        {
            float value = BitConverter.Int32BitsToSingle(bits);
            if (!float.IsFinite(value))
            {
                throw new InvalidDataException(
                    "A Level 100 actor basis contains a non-finite component.");
            }
            return checked((int)MathF.Round(
                value * Level100Basis3.Scale,
                MidpointRounding.AwayFromZero));
        }

        // Core is (retail X, up=-retail Z, retail Y). Contact meshes remain
        // (X,Y,Z-down), so transform the full basis rather than transferring
        // yaw-only source enums or dropping pitch/roll.
        var result = new Level100Basis3(
            Component(core.Row0X),
            Component(core.Row0Z),
            -Component(core.Row0Y),
            Component(core.Row2X),
            Component(core.Row2Z),
            -Component(core.Row2Y),
            -Component(core.Row1X),
            -Component(core.Row1Z),
            Component(core.Row1Y));
        return result.IsOrthonormal
            ? result
            : throw new InvalidDataException(
                "A Level 100 actor basis cannot be represented by contact mechanics.");
    }
}

/// <summary>
/// Per-actor destruction component. The mission/native registry creates and
/// attaches this state; it does not register, spawn, position, deactivate or
/// advance mission objectives itself.
/// </summary>
public sealed class Level100DestructionState
{
    public const uint PulseDirectDamageBits = 0x3F4CCCCD;
    public const uint PulseExplosionDamageBits = 0x3F800000;
    // Round "Mech Pulse Bolt Large" @0xACDA, field 2 in the same pinned
    // physics.dat as SimulationConstants: 8.0f. CRound::Hit forwards this
    // direct amount from roundData+0x1C. Its separate explosion maximum 4.0
    // must not be added without the actual spatial scan and falloff.
    public const uint LargePulseDirectDamageBits = 0x41000000;

    /// <summary>
    /// Legacy combined Medium amount: direct <c>0.8</c> plus explosion maximum
    /// <c>1.0</c>. The actual explosion uses a separate spatial scan and falloff;
    /// this fallback is not a general retail damage contract.
    /// </summary>
    public const uint PulseDamageBits = 0x3FE66666;
    /// <summary>
    /// The Mech Bullet round's direct damage, 0.08 (field 2 of round
    /// "Mech Bullet", <c>default physics.dat</c>). Its "Mech Bullet Hit"
    /// explosion adds nothing: its damage, 0.001 (<c>0x3a83126f</c>), is at or
    /// below the double 0.0015 that <c>CExplosion::Init</c> compares
    /// (<c>0x0044b9e8-0x0044ba02</c>, pristine <c>74154bfa…</c>), so its
    /// collision mask becomes −1 and the filter at <c>0x00426900</c> never
    /// lets it touch anything.
    /// </summary>
    public const uint MechBulletDamageBits = 0x3DA3D70A;
    // The admitted Warehouse has 28 parts. Reserve one detach per part plus
    // impact, direct damage and the two existing threshold/terminal projections.
    public const int MaximumEventsPerHit = 28 + 4;

    private const uint WarehouseHalfFractionBits = 0x3F000000;
    private const uint WarehouseCoreMultiplierBits = 0x40A00000;

    private readonly Level100ContactDefinition _definition;
    private uint[] _initialHealthBits;
    private uint[] _currentHealthBits;
    private byte[] _partActivity;
    private uint _currentLifeBits;
    private bool _terminal;
    private bool _belowHalfReported;

    public Level100DestructionState(
        int actorId,
        Level100ContactDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (actorId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actorId));
        }
        if (definition.Kind is not (
            Level100DefinitionKind.TargetTank or
            Level100DefinitionKind.TargetDrone or
            Level100DefinitionKind.Warehouse))
        {
            throw new ArgumentException(
                "Only released Level 100 destructible definitions have state.",
                nameof(definition));
        }

        ActorId = actorId;
        _definition = definition;
        _initialHealthBits = new uint[definition.PartCount];
        _currentHealthBits = new uint[definition.PartCount];
        _partActivity = new byte[definition.PartCount];
        Reset();
    }

    public int ActorId { get; }

    public Level100ContactDefinition Definition => _definition;

    public bool Terminal => _terminal;

    public uint CurrentLifeBits => _currentLifeBits;

    public float CurrentLife => FromBits(_currentLifeBits);

    /// <summary>
    /// Canonical registry projection. Target Tanks expose their root life;
    /// Warehouse health is the normalized sum of current segment health.
    /// Terminal state always projects to zero.
    /// </summary>
    public int RegistryHealth
    {
        get
        {
            if (_terminal)
            {
                return 0;
            }

            int maximum = checked((int)MathF.Round(
                _definition.MaximumLife * 1_000f,
                MidpointRounding.AwayFromZero));
            if (IsWholeBodyLife(_definition.Kind))
            {
                return Math.Clamp(
                    checked((int)MathF.Round(
                        MathF.Max(0, CurrentLife) * 1_000f,
                        MidpointRounding.AwayFromZero)),
                    1,
                    maximum);
            }

            float initial = SumInitialHealth(0);
            float current = SumCurrentHealth(0);
            if (!float.IsFinite(initial) ||
                !float.IsFinite(current) ||
                initial <= 0 ||
                current < 0)
            {
                throw new InvalidDataException(
                    "Warehouse segment health cannot be projected to the actor registry.");
            }
            return Math.Clamp(
                checked((int)MathF.Round(
                    maximum * (current / initial),
                    MidpointRounding.AwayFromZero)),
                1,
                maximum);
        }
    }

    public ReadOnlyMemory<byte> ContactPartActivity => _partActivity;

    public uint GetInitialSegmentHealthBits(int partIndex)
    {
        ValidatePartIndex(partIndex);
        return _initialHealthBits[partIndex];
    }

    public uint GetCurrentSegmentHealthBits(int partIndex)
    {
        ValidatePartIndex(partIndex);
        return _currentHealthBits[partIndex];
    }

    public void Reset()
    {
        Array.Clear(_initialHealthBits);
        Array.Clear(_currentHealthBits);
        Array.Fill(_partActivity, (byte)1);
        _terminal = false;
        _belowHalfReported = false;
        _currentLifeBits = _definition.MaximumLifeBits;

        if (_definition.Kind == Level100DefinitionKind.Warehouse)
        {
            InitializeWarehouseHealth();
        }
    }

    /// <summary>
    /// Applies the legacy Medium-pulse approximation to a factual narrowphase hit.
    /// The spatial explosion scan and falloff are not implemented here.
    /// The caller retains actor activation and mission consequence ownership.
    /// </summary>
    public int ApplyPulseHit(
        in Level100ContactHit hit,
        Span<Level100DestructionEvent> events)
    {
        // This preserves old contact behavior until the separate explosion
        // resolver is implemented. The configured direct damage and radial
        // maximum cannot in general be combined or assigned to this receiver.
        // See cround-hit-damage-path-2026-08-10.md, corrected August 28.
        if (!IsWholeBodyLife(_definition.Kind))
        {
            return ApplyRoundHit(
                hit,
                PulseDamageBits,
                Level100DestructionEffectKind.PulseImpact,
                events);
        }

        ValidateRoundHit(hit, events);
        var writer = new EventWriter(events);
        writer.Add(CreateRoundImpactEvent(
            hit,
            Level100DestructionEffectKind.PulseImpact));
        if (_terminal && _definition.Kind != Level100DefinitionKind.TargetTank)
        {
            return writer.Count;
        }

        // TODO: replace this unconditional second call with the released
        // synchronous neighbor scan and per-receiver falloff. Particular
        // retained shots observed 0.8 then 1.0; they do not establish this
        // result for every hit or justify applying the same shortcut to Large.
        ApplyWholeBodyDamage(hit, PulseDirectDamageBits, ref writer);
        ApplyWholeBodyDamage(hit, PulseExplosionDamageBits, ref writer);
        return writer.Count;
    }

    /// <summary>
    /// Applies a named round's damage to a factual narrowphase hit. The caller
    /// retains actor activation and mission consequence ownership.
    /// </summary>
    public int ApplyRoundHit(
        in Level100ContactHit hit,
        uint damageBits,
        Level100DestructionEffectKind impactEffectKind,
        Span<Level100DestructionEvent> events)
    {
        ValidateRoundHit(hit, events);

        var writer = new EventWriter(events);
        writer.Add(CreateRoundImpactEvent(hit, impactEffectKind));

        if (_terminal && _definition.Kind != Level100DefinitionKind.TargetTank)
        {
            return writer.Count;
        }

        if (IsWholeBodyLife(_definition.Kind))
        {
            ApplyWholeBodyDamage(hit, damageBits, ref writer);
        }
        else
        {
            ApplyWarehouseDamage(hit, damageBits, ref writer);
        }
        return writer.Count;
    }

    private void ValidateRoundHit(
        in Level100ContactHit hit,
        Span<Level100DestructionEvent> events)
    {
        if (hit.ActorId != ActorId)
        {
            throw new ArgumentException(
                "The hit actor does not own this destruction component.",
                nameof(hit));
        }
        if ((uint)hit.PartIndex >= (uint)_definition.PartCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hit),
                "The factual hit must identify a decoded actor part.");
        }
        if (events.Length < MaximumEventsPerHit)
        {
            throw new ArgumentException(
                $"At least {MaximumEventsPerHit} event slots are required.",
                nameof(events));
        }
    }

    internal static Level100DestructionEvent CreateRoundImpactEvent(
        in Level100ContactHit hit,
        Level100DestructionEffectKind effectKind) =>
        new(
            RoundImpactEventKind(effectKind),
            effectKind,
            hit.ActorId,
            hit.PartIndex,
            0,
            hit.SurfacePoint);

    internal static Level100DestructionEventKind RoundImpactEventKind(
        Level100DestructionEffectKind effectKind) => effectKind switch
        {
            Level100DestructionEffectKind.PulseImpact =>
                Level100DestructionEventKind.PulseImpact,
            Level100DestructionEffectKind.VulcanImpact =>
                Level100DestructionEventKind.VulcanImpact,
            Level100DestructionEffectKind.MicroMissileImpact =>
                Level100DestructionEventKind.MicroMissileImpact,
            _ => throw new ArgumentOutOfRangeException(
                nameof(effectKind),
                effectKind,
                "A round contact requires a released impact effect."),
        };

    public Level100DestructionSnapshot CaptureSnapshot()
    {
        return new Level100DestructionSnapshot(
            ActorId,
            _definition.Name,
            _currentLifeBits,
            _terminal,
            _belowHalfReported,
            (uint[])_initialHealthBits.Clone(),
            (uint[])_currentHealthBits.Clone(),
            (byte[])_partActivity.Clone());
    }

    public void Restore(Level100DestructionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.ActorId != ActorId ||
            !StringComparer.Ordinal.Equals(
                snapshot.DefinitionName,
                _definition.Name) ||
            snapshot.InitialHealthBits.Length != _definition.PartCount ||
            snapshot.CurrentHealthBits.Length != _definition.PartCount ||
            snapshot.PartActivity.Length != _definition.PartCount)
        {
            throw new ArgumentException(
                "The destruction snapshot does not belong to this component.",
                nameof(snapshot));
        }

        uint[] initial = snapshot.CopyInitialHealthBits();
        uint[] current = snapshot.CopyCurrentHealthBits();
        byte[] activity = snapshot.CopyPartActivity();
        float currentLife = FromBits(snapshot.CurrentLifeBits);
        if (!float.IsFinite(currentLife) ||
            (_definition.Kind == Level100DefinitionKind.Warehouse &&
             currentLife < 0))
        {
            throw new ArgumentException(
                "The destruction snapshot has invalid actor health.",
                nameof(snapshot));
        }
        for (int index = 0; index < activity.Length; index++)
        {
            if (activity[index] is not 0 and not 1 ||
                !float.IsFinite(FromBits(initial[index])) ||
                !float.IsFinite(FromBits(current[index])) ||
                FromBits(initial[index]) < 0 ||
                FromBits(current[index]) < 0)
            {
                throw new ArgumentException(
                    "The destruction snapshot has invalid segment state.",
                    nameof(snapshot));
            }
        }
        _initialHealthBits = initial;
        _currentHealthBits = current;
        _partActivity = activity;
        _currentLifeBits = snapshot.CurrentLifeBits;
        _terminal = snapshot.Terminal;
        _belowHalfReported = snapshot.BelowHalfReported;
    }

    /// <summary>
    /// True for the released classes that carry one whole-body
    /// <c>CUnitLife</c> rather than per-segment health:
    /// <c>Target Tank</c>/<c>Target Truck</c> (behaviour class 3) and
    /// <c>Target Drone</c> (behaviour class 9). The damage arithmetic is
    /// shared before death; shutdown lifetimes differ by subclass. Life bits,
    /// mesh and destruction record are carried per definition.
    /// </summary>
    private static bool IsWholeBodyLife(Level100DefinitionKind kind) =>
        kind is Level100DefinitionKind.TargetTank or
            Level100DefinitionKind.TargetDrone;

    private void ApplyWholeBodyDamage(
        in Level100ContactHit hit,
        uint damageBits,
        ref EventWriter writer)
    {
        // CUnit retains the demonstrated overkill value through its terminal
        // transition (6 -> 4.2 -> 2.4 -> 0.6 -> -1.2).
        float remaining = FromBits(_currentLifeBits) - FromBits(damageBits);
        _currentLifeBits = ToBits(remaining);
        writer.Add(new Level100DestructionEvent(
            Level100DestructionEventKind.SegmentDamaged,
            Level100DestructionEffectKind.None,
            ActorId,
            hit.PartIndex,
            _currentLifeBits,
            hit.SurfacePoint));
        // Retail's CUnit__ApplyDamage compares the stored remainder with
        // +0.0 and enters this lane only when x87 C0 is set; see PROVENANCE.
        if (remaining < 0)
        {
            SetTerminal(hit, ref writer);
        }
    }

    private void ApplyWarehouseDamage(
        in Level100ContactHit hit,
        uint damageBits,
        ref EventWriter writer)
    {
        if ((uint)hit.PartIndex >= (uint)_definition.PartCount ||
            _partActivity[hit.PartIndex] == 0 ||
            _currentHealthBits[hit.PartIndex] == 0)
        {
            return;
        }

        float remaining = MathF.Max(
            0,
            FromBits(_currentHealthBits[hit.PartIndex]) -
                FromBits(damageBits));
        _currentHealthBits[hit.PartIndex] = ToBits(remaining);
        writer.Add(new Level100DestructionEvent(
            Level100DestructionEventKind.SegmentDamaged,
            Level100DestructionEffectKind.None,
            ActorId,
            hit.PartIndex,
            _currentHealthBits[hit.PartIndex],
            hit.SurfacePoint));
        if (remaining <= 0)
        {
            DetachWarehouseSegment(hit.PartIndex, hit, ref writer);
            // In the pinned Warehouse, core2 (part 1) is the only damageable
            // Core and all its direct children are Extra segments. Common
            // Core break invokes those children synchronously, in reverse
            // authored order because construction inserts at the list head.
            // Retail Extras queue their own children; those events remain
            // unimplemented here, so do not walk descendants synchronously.
            // Other meshes' positive-health Core collapse is not modeled here.
            if (hit.PartIndex == 1)
            {
                ReadOnlySpan<int> children = _definition.PartArray[1].FloatGeometry.Children.Span;
                for (int index = children.Length - 1; index >= 0; index--)
                    DetachWarehouseSegment(children[index], hit, ref writer);
            }
        }

        float activeInitial = SumActiveInitialHealth(0);
        float totalInitial = SumInitialHealth(0);
        float halfThreshold = totalInitial * FromBits(WarehouseHalfFractionBits);
        if (!_belowHalfReported && activeInitial < halfThreshold)
        {
            _belowHalfReported = true;
            writer.Add(new Level100DestructionEvent(
                Level100DestructionEventKind.ActiveSubtreeBelowHalf,
                Level100DestructionEffectKind.None,
                ActorId,
                hit.PartIndex,
                ToBits(activeInitial),
                hit.SurfacePoint));
        }

        // Under the admitted PC24 model, the direct controller multiplies its
        // float total by binary64 0.3 and retains a double for comparison.
        double terminalThreshold = RetailFloat24.Multiply(totalInitial, 0.3d);
        if (CoreChildrenDestroyed() || activeInitial < terminalThreshold)
        {
            SetTerminal(hit, ref writer);
        }
    }

    private void DetachWarehouseSegment(
        int partIndex,
        in Level100ContactHit cause,
        ref EventWriter writer)
    {
        if (_partActivity[partIndex] == 0)
            return;
        _currentHealthBits[partIndex] = 0;
        _partActivity[partIndex] = 0;
        // A collateral common break zeros health without another damage call.
        // Position is the cause anchor, not a measured debris emission point.
        writer.Add(new Level100DestructionEvent(
            Level100DestructionEventKind.SegmentDetached,
            Level100DestructionEffectKind.None,
            ActorId,
            partIndex,
            0,
            cause.SurfacePoint));
    }

    private void SetTerminal(
        in Level100ContactHit hit,
        ref EventWriter writer)
    {
        if (_terminal)
        {
            return;
        }
        _terminal = true;
        writer.Add(new Level100DestructionEvent(
            Level100DestructionEventKind.Terminal,
            _definition.Kind switch
            {
                Level100DefinitionKind.TargetDrone =>
                    Level100DestructionEffectKind.DroneDestroyed,
                Level100DefinitionKind.TargetTank =>
                    Level100DestructionEffectKind.TargetDestroyed,
                _ => Level100DestructionEffectKind.FacilityDestroyed,
            },
            ActorId,
            hit.PartIndex,
            0,
            hit.SurfacePoint));
    }

    private void InitializeWarehouseHealth()
    {
        Level100ContactPart[] parts = _definition.PartArray;
        float divisor = 0;
        for (int index = 1; index < parts.Length; index++)
        {
            divisor += parts[index].SegmentValue;
        }
        if (!float.IsFinite(divisor) || divisor <= 0)
        {
            throw new InvalidDataException(
                "Warehouse segment extent divisor is invalid.");
        }

        float scale = _definition.MaximumLife;
        float coreMultiplier = FromBits(WarehouseCoreMultiplierBits);
        for (int index = 1; index < parts.Length; index++)
        {
            float health = (parts[index].SegmentValue / divisor) * scale;
            if (parts[index].Name.StartsWith("core", StringComparison.OrdinalIgnoreCase))
            {
                health *= coreMultiplier;
            }
            uint bits = ToBits(health);
            _initialHealthBits[index] = bits;
            _currentHealthBits[index] = bits;
        }

        // The first core segment is the zero-health hierarchy root in Steam.
        _initialHealthBits[0] = 0;
        _currentHealthBits[0] = 0;
    }

    private float SumInitialHealth(int parent)
    {
        float sum = _initialHealthBits[parent] == 0
            ? 0
            : FromBits(_initialHealthBits[parent]);
        // Segment children were inserted at the list head. Keep the native
        // recursive addition/store order, which differs after partial damage.
        ReadOnlySpan<int> children = _definition.PartArray[parent].FloatGeometry.Children.Span;
        for (int index = children.Length - 1; index >= 0; index--)
            sum += SumInitialHealth(children[index]);
        return sum;
    }

    private float SumCurrentHealth(int parent)
    {
        float sum = _currentHealthBits[parent] == 0
            ? 0
            : FromBits(_currentHealthBits[parent]);
        ReadOnlySpan<int> children = _definition.PartArray[parent].FloatGeometry.Children.Span;
        for (int index = children.Length - 1; index >= 0; index--)
            sum += SumCurrentHealth(children[index]);
        return sum;
    }

    private float SumActiveInitialHealth(int parent)
    {
        float sum = _partActivity[parent] != 0 &&
            _currentHealthBits[parent] != 0
            ? FromBits(_initialHealthBits[parent])
            : 0;
        ReadOnlySpan<int> children = _definition.PartArray[parent].FloatGeometry.Children.Span;
        for (int index = children.Length - 1; index >= 0; index--)
            sum += SumActiveInitialHealth(children[index]);
        return sum;
    }

    private bool CoreChildrenDestroyed()
    {
        bool hasChild = false;
        Level100ContactPart[] parts = _definition.PartArray;
        for (int index = 0; index < parts.Length; index++)
        {
            if (parts[index].Parent != 0)
            {
                continue;
            }
            hasChild = true;
            if (_partActivity[index] != 0 && _currentHealthBits[index] != 0)
            {
                return false;
            }
        }
        return hasChild;
    }

    private void ValidatePartIndex(int partIndex)
    {
        if ((uint)partIndex >= (uint)_definition.PartCount)
        {
            throw new ArgumentOutOfRangeException(nameof(partIndex));
        }
    }

    private static float FromBits(uint bits) =>
        BitConverter.Int32BitsToSingle(unchecked((int)bits));

    private static uint ToBits(float value) =>
        unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private ref struct EventWriter
    {
        private readonly Span<Level100DestructionEvent> _events;

        public EventWriter(Span<Level100DestructionEvent> events)
        {
            _events = events;
            Count = 0;
        }

        public int Count { get; private set; }

        public void Add(Level100DestructionEvent item)
        {
            if ((uint)Count >= (uint)_events.Length)
            {
                throw new InvalidOperationException(
                    "The bounded destruction event capacity was exceeded.");
            }
            _events[Count++] = item;
        }
    }

}
