// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The source <c>CThing::mFlags</c> bits that this deterministic Core seam
/// currently carries. Actor motion remains separate from CThing publication.
/// </summary>
[Flags]
public enum ThingActorFlags : ushort
{
    None = 0,
    DeclaredShutdown = 1 << 0,
    InMapWho = 1 << 1,
    Dying = 1 << 2,
    Invisible = 1 << 4,
    IsBigThing = 1 << 6,
}

/// <summary>
/// Source inheritance bits composed by the CThing/CComplexThing/CActor
/// <c>SetThingType</c> chain. Retail proves the CThing and CActor endpoints;
/// the CComplexThing middle override remains source-only in the W2 receipt.
/// </summary>
public static class ThingActorTypeMasks
{
    public const uint Thing = 0x00000001;
    public const uint Actor = 0x00000002;
    public const uint ComplexThing = 0x80000000;
    public const uint ActorLineage = Thing | ComplexThing | Actor;
}

/// <summary>
/// A deterministic simulation pose. It contains no renderer or engine object.
/// </summary>
public sealed record ThingActorPoseSnapshot(
    SimVector3 PositionMillimeters,
    Level100FloatBasis3Bits BasisFloatBits);

/// <summary>
/// Meaningful retail float words. Unmeasured fourth vector words and matrix
/// padding are not manufactured. These poses belong to the same Actor owner
/// as its compatibility view; they are not a second mutable actor registry.
/// </summary>
public sealed record RetailActorPoseSnapshot(
    Level100FloatVector3Bits PositionFloatBits,
    Level100FloatBasis3Bits BasisFloatBits);

public sealed record RetailActorPosePair(
    RetailActorPoseSnapshot Current, RetailActorPoseSnapshot Old);

public sealed record RetailActorMotionSnapshot(int LastMoveTimeFloatBits, int MoveCountdown);

/// <summary>
/// Immutable presentation-safe projection of the reusable Thing/Actor state.
/// </summary>
public sealed record ThingActorBaseStateSnapshot(
    ThingActorFlags Flags,
    ThingActorPoseSnapshot CurrentPose,
    ThingActorPoseSnapshot OldPose,
    SimVector3 Velocity,
    SimVector3 AngularVelocity,
    uint ThingTypeMask,
    int LastTimeOnGroundFloatBits,
    int LastTimeInWaterFloatBits,
    int LastTimeOnObjectFloatBits)
{
    /// <summary>
    /// Exact state, when present. CurrentPose/OldPose are derived compatibility
    /// views. Only the admitted Plane motion slice supports raw restore/hash;
    /// the separate incomplete world-construction route remains unsupported.
    /// </summary>
    public RetailActorPosePair? RetailPoses { get; init; }
    public RetailActorMotionSnapshot? RetailMotion { get; init; }
    public RetailPlaneMotionSnapshot? RetailPlane { get; init; }

    public bool IsInvisible => (Flags & ThingActorFlags.Invisible) != 0;

    public bool IsDying => (Flags & ThingActorFlags.Dying) != 0;

    public bool IsShuttingDown =>
        (Flags & ThingActorFlags.DeclaredShutdown) != 0;

    /// <summary>
    /// Retail <c>0x00401550</c>, source inline
    /// <c>CActor::GetLocalLastFrameMovement</c>.
    /// </summary>
    public SimVector3 LocalLastFrameMovement => new(
        CurrentPose.PositionMillimeters.X - OldPose.PositionMillimeters.X,
        CurrentPose.PositionMillimeters.Y - OldPose.PositionMillimeters.Y,
        CurrentPose.PositionMillimeters.Z - OldPose.PositionMillimeters.Z);

    public bool IsA(uint typeMask) => (ThingTypeMask & typeMask) != 0;
}

/// <summary>
/// Pure deterministic state laws shared by source <c>CThing</c> and
/// <c>CActor</c>. Filesystem, clock, render, audio, and engine ownership stay at
/// adapters.
/// </summary>
/// <remarks>
/// <para>
/// Source authority is pinned <c>references/Onslaught</c> commit
/// <c>5352a81cdb838b145a57f7febc5d9fc4b0129ebb</c>:
/// <c>thing.h:41-52, 113-118, 138-146, 156-158, 172-174, 194-199, 237-248,
/// 268-275</c>, <c>thing.cpp:28-37, 183-204, 423-435, 563-571, 742-745</c>,
/// <c>actor.h:13-65</c>, and <c>actor.cpp:15-40, 54-81, 114-149,
/// 326-367</c>.
/// </para>
/// <para>
/// Retail identity is the W2 receipt landed at <c>07fca645</c> (main merge
/// <c>561c2099</c>) plus the promoted CThing/CComplexThing/CActor semantic
/// tables. Relevant exact bodies are visibility <c>0x00401460/0x00401470</c>,
/// actor type <c>0x00401540</c>, last-frame movement <c>0x00401550</c>, stop
/// <c>0x00401580</c>, actor init/move/contact
/// <c>0x004011e0/0x004015e0/0x00402000..0x00402020</c>, CThing type
/// <c>0x004f3470</c>, and shutdown/dying
/// <c>0x004f3760/0x004f37a0</c>.
/// </para>
/// <para>
/// Compiler-folded identities stay explicit: <c>0x004040a0</c> is the one
/// current-position body for render/current/start/end, <c>0x0043e9f0</c> is
/// CThing sound/old position, and <c>0x0043ea20</c> is CComplexThing
/// sound/old orientation. This owner retains one current and one old pose; it
/// does not reintroduce the excluded render/audio virtual defaults.
/// </para>
/// <para>
/// Reuse disposition: <b>REUSED</b> — W2 receipt and sealed
/// <c>pc-layout-thing-complexthing-20260813-v1</c> predecessor
/// <c>e788ffde077c9c861d9163a7526964917cc1747c9c69f6287d9e919a1e399efa</c>;
/// <b>EXTENDED</b> — these existing authorities into a Core state owner and
/// Level-100 adapter; <b>NEW_MEASUREMENT</b> — zero.
/// </para>
/// </remarks>
public sealed class ThingActorBaseState
{
    public static readonly int InitialContactTimeFloatBits =
        BitConverter.SingleToInt32Bits(-100.0f);

    private readonly ThingBaseState _thing;
    private ThingActorPoseSnapshot _currentPose;
    private ThingActorPoseSnapshot _oldPose;
    private RetailActorPosePair? _retailPoses;
    private RetailActorMotionSnapshot? _retailMotion;
    private RetailPlaneMotionSnapshot? _retailPlane;
    private SimVector3 _velocity;
    private SimVector3 _angularVelocity;
    private int _lastTimeOnGroundFloatBits;
    private int _lastTimeInWaterFloatBits;
    private int _lastTimeOnObjectFloatBits;

    public ThingActorBaseState(
        ThingActorPoseSnapshot initialPose,
        SimVector3 velocity,
        SimVector3 angularVelocity,
        uint specificTypeMask)
    {
        ValidatePose(initialPose, nameof(initialPose));
        _currentPose = initialPose;
        _oldPose = initialPose;
        _velocity = velocity;
        _angularVelocity = angularVelocity;
        _thing = new(ThingActorTypeMasks.ActorLineage, specificTypeMask);
        _lastTimeOnGroundFloatBits = InitialContactTimeFloatBits;
        _lastTimeInWaterFloatBits = InitialContactTimeFloatBits;
        _lastTimeOnObjectFloatBits = InitialContactTimeFloatBits;
    }

    public ThingActorBaseState(ThingActorBaseStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if ((snapshot.RetailPoses is not null || snapshot.RetailMotion is not null) &&
            snapshot.RetailPlane is null)
            throw new NotSupportedException("Retail actor construction has no admitted restore contract yet.");
        if (snapshot.RetailPlane is not null)
        {
            if (snapshot.RetailPoses is null || snapshot.RetailMotion is null)
                throw new ArgumentException("Plane motion requires complete Actor poses and movement state.", nameof(snapshot));
            ValidateRetailPose(snapshot.RetailPoses.Current);
            ValidateRetailPose(snapshot.RetailPoses.Old);
            ValidatePlaneMotion(snapshot.RetailPlane);
            _ = ValidateEventTime(snapshot.RetailMotion.LastMoveTimeFloatBits);
            if (snapshot.RetailMotion.MoveCountdown < 0 ||
                ProjectRetailPose(snapshot.RetailPoses.Current) != snapshot.CurrentPose ||
                ProjectRetailPose(snapshot.RetailPoses.Old) != snapshot.OldPose ||
                ProjectRetailVector(snapshot.RetailPlane.Velocity) != snapshot.Velocity)
                throw new ArgumentException("Plane compatibility fields disagree with retained raw state.", nameof(snapshot));
        }
        ThingActorFlags knownFlags =
            ThingActorFlags.DeclaredShutdown |
            ThingActorFlags.Dying |
            ThingActorFlags.Invisible;
        if (snapshot.RetailPlane is not null) knownFlags |= ThingActorFlags.InMapWho;
        if ((snapshot.Flags & ~knownFlags) != 0 ||
            snapshot.CurrentPose is null ||
            snapshot.OldPose is null ||
            !HasFiniteBasis(snapshot.CurrentPose.BasisFloatBits) ||
            !HasFiniteBasis(snapshot.OldPose.BasisFloatBits) ||
            (snapshot.ThingTypeMask & ThingActorTypeMasks.ActorLineage) !=
                ThingActorTypeMasks.ActorLineage ||
            !IsFiniteFloatBits(snapshot.LastTimeOnGroundFloatBits) ||
            !IsFiniteFloatBits(snapshot.LastTimeInWaterFloatBits) ||
            !IsFiniteFloatBits(snapshot.LastTimeOnObjectFloatBits))
        {
            throw new ArgumentException(
                "Thing/Actor base-state snapshot violates the source contract.",
                nameof(snapshot));
        }

        _thing = new(ThingActorTypeMasks.ActorLineage, snapshot.ThingTypeMask, snapshot.Flags);
        _currentPose = snapshot.CurrentPose;
        _oldPose = snapshot.OldPose;
        _velocity = snapshot.Velocity;
        _angularVelocity = snapshot.AngularVelocity;
        _retailPoses = snapshot.RetailPoses;
        _retailMotion = snapshot.RetailMotion;
        _retailPlane = snapshot.RetailPlane;
        _lastTimeOnGroundFloatBits = snapshot.LastTimeOnGroundFloatBits;
        _lastTimeInWaterFloatBits = snapshot.LastTimeInWaterFloatBits;
        _lastTimeOnObjectFloatBits = snapshot.LastTimeOnObjectFloatBits;
    }

    public ThingActorBaseStateSnapshot Snapshot => new(
        _thing.Flags,
        _retailPoses is null ? _currentPose : ProjectRetailPose(_retailPoses.Current),
        _retailPoses is null ? _oldPose : ProjectRetailPose(_retailPoses.Old),
        _retailPlane is null ? _velocity : ProjectRetailVector(_retailPlane.Velocity),
        _angularVelocity,
        _thing.TypeMask,
        _lastTimeOnGroundFloatBits,
        _lastTimeInWaterFloatBits,
        _lastTimeOnObjectFloatBits)
        { RetailPoses = _retailPoses, RetailMotion = _retailMotion, RetailPlane = _retailPlane };

    internal RetailActorPosePair RetailPoses => _retailPoses ??
        throw new InvalidOperationException("Retail actor initialization has not begun.");
    internal bool HasRetailConstruction => _retailPoses is not null;
    internal bool HasRetailPlaneMotion => _retailPlane is not null;

    /// <summary>
    /// Creation admission with complete retained inputs. Does not infer angles
    /// from a matrix or admit any guide cache, controller, event or RNG state.
    /// </summary>
    internal void BeginRetailPlane(RetailActorPoseSnapshot pose,
        RetailPlaneMotionSnapshot motion, int eventTimeFloatBits, uint specificTypeMask)
    {
        ValidatePlaneMotion(motion);
        _ = ProjectRetailVector(motion.Velocity);
        _ = ValidateEventTime(eventTimeFloatBits);
        BeginRetailInitialization(pose, pose, specificTypeMask);
        _retailPlane = motion;
        _retailMotion = new(eventTimeFloatBits, 1);
    }

    /// <summary>
    /// Commit the complete move after its guide/contact phases. All inputs are
    /// checked before either current or old state changes. The pre-move raw
    /// pose becomes old even when the translation is zero.
    /// </summary>
    internal void CommitRetailPlaneMove(RetailActorPoseSnapshot pose,
        RetailPlaneMotionSnapshot motion, int eventTimeFloatBits)
    {
        RetailPlaneMotionSnapshot previous = _retailPlane ??
            throw new InvalidOperationException("Plane motion has not been admitted.");
        ValidateRetailPose(pose);
        ValidatePlaneMotion(motion);
        _ = ProjectRetailVector(motion.Velocity);
        _ = ValidateEventTime(eventTimeFloatBits);
        SimVector3 angular = ProjectPlaneAngularDelta(previous.CurrentEuler, motion.CurrentEuler);
        _retailPoses = new(pose, RetailPoses.Current);
        _retailPlane = motion;
        _retailMotion = new(eventTimeFloatBits, _retailMotion!.MoveCountdown);
        _angularVelocity = angular;
    }

    // CUnit Stop through guide 47e3d0 clears drive, not Air velocity.
    internal void ClearRetailPlaneDrive()
    {
        RetailPlaneMotionSnapshot motion = _retailPlane ??
            throw new InvalidOperationException("Plane motion has not been admitted.");
        _retailPlane = motion with { Drive = default };
    }

    /// <summary>Begins the supported stationary Actor initialization route.</summary>
    internal void BeginRetailInitialization(RetailActorPoseSnapshot current,
        RetailActorPoseSnapshot old, uint specificTypeMask)
    {
        if (_retailPoses is not null || _velocity != SimVector3.Zero || _angularVelocity != SimVector3.Zero)
            throw new InvalidOperationException("Retail initialization requires a fresh stationary Actor allocation.");
        ValidateRetailPose(current);
        ValidateRetailPose(old);
        _retailPoses = new(current, old);
        _thing.SetThingType(specificTypeMask);
        _thing.AddFlags(ThingActorFlags.InMapWho);
    }

    internal void SetRetailPosition(Level100FloatVector3Bits position)
    {
        ValidateRetailPosition(position);
        RetailActorPosePair poses = RetailPoses;
        _retailPoses = poses with { Current = poses.Current with { PositionFloatBits = position } };
    }

    internal void TeleportRetailPosition(Level100FloatVector3Bits position)
    {
        ValidateRetailPosition(position);
        RetailActorPosePair poses = RetailPoses;
        _retailPoses = new(poses.Current with { PositionFloatBits = position },
            poses.Old with { PositionFloatBits = position });
    }

    // CActor::StickToGround copies position only; old orientation survives.
    internal void CopyRetailPositionToOld()
    {
        RetailActorPosePair poses = RetailPoses;
        _retailPoses = poses with
        {
            Old = poses.Old with { PositionFloatBits = poses.Current.PositionFloatBits }
        };
    }

    internal void AddPublishedType(uint mask) => _thing.AddType(mask);
    internal void AddPublicationFlags(ThingActorFlags flags) => _thing.AddFlags(flags);

    internal void SetRetailMotion(int lastMoveTimeFloatBits, int moveCountdown)
    {
        _ = RetailPoses;
        _retailMotion = new(ValidateEventTime(lastMoveTimeFloatBits), moveCountdown);
    }

    /// <summary>
    /// Retail <c>0x00401470</c>, source inline
    /// <c>CThing::MakeInvisible</c>: set <c>TF_INVISIBLE</c>.
    /// </summary>
    public void MakeInvisible() => _thing.MakeInvisible();

    /// <summary>
    /// Retail <c>0x00401460</c>, source inline
    /// <c>CThing::MakeVisible</c>: clear <c>TF_INVISIBLE</c>.
    /// </summary>
    public void MakeVisible() => _thing.MakeVisible();

    /// <summary>
    /// Retail <c>0x004f3760</c>, source <c>CThing::AddShutdownEvent</c>:
    /// set <c>TF_DECLARED_SHUTDOWN</c> once. The caller owns scheduling.
    /// </summary>
    public bool DeclareShutdown() => _thing.DeclareShutdown();

    /// <summary>
    /// Retail <c>0x004f37a0</c>, source <c>CThing::StartDieProcess</c>:
    /// set <c>TF_DYING</c> once, then declare shutdown.
    /// </summary>
    public bool StartDieProcess() => _thing.StartDieProcess();

    // CUnit's override at 0x004fd140 marks TF_DYING without calling the
    // CThing default AddShutdownEvent. Its subclass owns eventual shutdown.
    internal bool MarkUnitDying() => _thing.MarkDying();

    /// <summary>
    /// The source/retail <c>CActor::Move</c> ordering: capture current pose as
    /// old before publishing the next current pose.
    /// </summary>
    public void AdvancePose(ThingActorPoseSnapshot pose)
    {
        RequireMillimeterMode();
        ValidatePose(pose, nameof(pose));
        _oldPose = _currentPose;
        _currentPose = pose;
    }

    /// <summary>
    /// Source <c>CActor::LowFidelityMove</c> (actor.cpp:53-70): capture only
    /// old position. Old orientation stays at the preceding full update.
    /// The caller supplies the translated, conditionally clipped position.
    /// </summary>
    public void AdvanceLowFidelityPosition(SimVector3 position)
    {
        RequireMillimeterMode();
        _oldPose = _oldPose with { PositionMillimeters = _currentPose.PositionMillimeters };
        _currentPose = _currentPose with { PositionMillimeters = position };
    }

    /// <summary>
    /// Update current pose without starting another movement transition. A
    /// later phase of the same tick uses this after old pose was captured.
    /// </summary>
    public void UpdateCurrentPose(ThingActorPoseSnapshot pose)
    {
        RequireMillimeterMode();
        ValidatePose(pose, nameof(pose));
        _currentPose = pose;
    }

    /// <summary>
    /// Teleport/reset control: collapse old and current pose so presentation
    /// cannot interpolate through an out-of-band relocation.
    /// </summary>
    public void ResetPose(ThingActorPoseSnapshot pose)
    {
        RequireMillimeterMode();
        ValidatePose(pose, nameof(pose));
        _currentPose = pose;
        _oldPose = pose;
    }

    public void SetVelocity(SimVector3 velocity)
    {
        RequireMillimeterMode();
        _velocity = velocity;
    }

    public void AddVelocity(SimVector3 velocity)
    {
        RequireMillimeterMode();
        _velocity = new SimVector3(checked(_velocity.X + velocity.X),
            checked(_velocity.Y + velocity.Y), checked(_velocity.Z + velocity.Z));
    }

    /// <summary>
    /// Retail <c>0x00401580</c>, source inline <c>CActor::Stop</c>. Only the
    /// linear <c>mVelocity</c> vector is zeroed.
    /// </summary>
    public void Stop()
    {
        RequireMillimeterMode();
        _velocity = SimVector3.Zero;
    }

    public void SetAngularVelocity(SimVector3 angularVelocity)
    {
        RequireMillimeterMode();
        _angularVelocity = angularVelocity;
    }

    /// <summary>
    /// Retail <c>0x004f3470</c> (CThing), source-only CComplexThing lineage
    /// <c>0x80000001</c>, and retail <c>0x00401540</c> (CActor): replace the
    /// stored mask with the supplied subclass bits OR the full actor lineage.
    /// </summary>
    public void SetThingType(uint specificTypeMask) =>
        _thing.SetThingType(specificTypeMask);

    /// <summary>
    /// Retail <c>0x00402000</c>, source <c>CActor::DeclareOnGround</c>.
    /// Event time is supplied as exact single-precision bits; Core owns no
    /// clock.
    /// </summary>
    public void DeclareOnGround(int eventTimeFloatBits) =>
        _lastTimeOnGroundFloatBits = ValidateEventTime(eventTimeFloatBits);

    /// <summary>Retail <c>0x00402010</c>, CActor water-contact timestamp.</summary>
    public void DeclareInWater(int eventTimeFloatBits) =>
        _lastTimeInWaterFloatBits = ValidateEventTime(eventTimeFloatBits);

    /// <summary>Retail <c>0x00402020</c>, CActor object-contact timestamp.</summary>
    public void DeclareOnObject(int eventTimeFloatBits) =>
        _lastTimeOnObjectFloatBits = ValidateEventTime(eventTimeFloatBits);

    private static int ValidateEventTime(int eventTimeFloatBits)
    {
        if (!IsFiniteFloatBits(eventTimeFloatBits))
        {
            throw new ArgumentOutOfRangeException(nameof(eventTimeFloatBits));
        }

        return eventTimeFloatBits;
    }

    private static bool IsFiniteFloatBits(int bits) =>
        float.IsFinite(BitConverter.Int32BitsToSingle(bits));

    private static void ValidatePlaneMotion(RetailPlaneMotionSnapshot motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        static bool Finite(Level100FloatVector3Bits v) =>
            IsFiniteFloatBits(v.X) && IsFiniteFloatBits(v.Y) && IsFiniteFloatBits(v.Z);
        if (!Finite(motion.Velocity) || !Finite(motion.Drive) ||
            !Finite(motion.CurrentEuler) || !Finite(motion.DesiredEuler) || !Finite(motion.EulerRates) ||
            BitConverter.Int32BitsToSingle(motion.EulerRates.X) < 0 ||
            BitConverter.Int32BitsToSingle(motion.EulerRates.Y) < 0 ||
            BitConverter.Int32BitsToSingle(motion.EulerRates.Z) < 0 ||
            motion.BankFlagFloatBits is not (0 or 0x3f800000))
            throw new ArgumentException("Plane motion requires finite words, nonnegative rates and a boolean bank word.", nameof(motion));
    }

    private static SimVector3 ProjectPlaneAngularDelta(
        Level100FloatVector3Bits before, Level100FloatVector3Bits after)
    {
        static int Delta(int oldBits, int newBits, bool wrap)
        {
            double delta = (double)BitConverter.Int32BitsToSingle(newBits) - BitConverter.Int32BitsToSingle(oldBits);
            double pi = BitConverter.Int32BitsToSingle(0x40490fdb);
            if (wrap && delta > pi) delta -= pi * 2;
            else if (wrap && delta < -pi) delta += pi * 2;
            return checked((int)Math.Round(delta * 1_000_000, MidpointRounding.AwayFromZero));
        }
        return new(checked(-Delta(before.Y, after.Y, false)),
            Delta(before.X, after.X, true), Delta(before.Z, after.Z, true));
    }

    private void RequireMillimeterMode()
    {
        if (_retailPoses is not null)
            throw new NotSupportedException("Millimetre motion cannot overwrite authoritative retail float poses.");
    }

    private static void ValidateRetailPosition(Level100FloatVector3Bits position)
    {
        if (!IsFiniteFloatBits(position.X) || !IsFiniteFloatBits(position.Y) || !IsFiniteFloatBits(position.Z))
            throw new ArgumentException("Retail position must contain finite float words.", nameof(position));
        // Every admitted position must remain readable through the existing
        // registry view. Check before mutating either authoritative pose.
        _ = ProjectRetailPosition(position);
    }

    private static void ValidateRetailPose(RetailActorPoseSnapshot pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ValidateRetailPosition(pose.PositionFloatBits);
        if (!HasFiniteBasis(pose.BasisFloatBits))
            throw new ArgumentException("Retail basis must contain finite float words.", nameof(pose));
    }

    private static ThingActorPoseSnapshot ProjectRetailPose(RetailActorPoseSnapshot pose)
    {
        Level100FloatBasis3Bits b = pose.BasisFloatBits;
        // Positions and local vectors use Q(x,y,z)=(x,-z,y), so a basis must
        // use Q*B*inverse(Q). Flip words directly to retain signed zeros.
        return new(ProjectRetailPosition(pose.PositionFloatBits),
            new(b.Row0X, b.Row0Z ^ int.MinValue, b.Row0Y,
                b.Row2X ^ int.MinValue, b.Row2Z, b.Row2Y ^ int.MinValue,
                b.Row1X, b.Row1Z ^ int.MinValue, b.Row1Y));
    }

    private static SimVector3 ProjectRetailPosition(Level100FloatVector3Bits p)
    {
        static int Millimeters(double value) => checked((int)Math.Round(value * 1000,
            MidpointRounding.AwayFromZero));
        return new(Millimeters((double)BitConverter.Int32BitsToSingle(p.X) - 288.6875),
            Millimeters(-10.0 - BitConverter.Int32BitsToSingle(p.Z)),
            Millimeters((double)BitConverter.Int32BitsToSingle(p.Y) - 243.25));
    }

    private static SimVector3 ProjectRetailVector(Level100FloatVector3Bits v)
    {
        static int Millimeters(int bits, int sign = 1) => checked((int)Math.Round(
            (double)BitConverter.Int32BitsToSingle(bits) * sign * 1000, MidpointRounding.AwayFromZero));
        return new(Millimeters(v.X), Millimeters(v.Z, -1), Millimeters(v.Y));
    }

    private static bool HasFiniteBasis(Level100FloatBasis3Bits basis) =>
        new[]
        {
            basis.Row0X, basis.Row0Y, basis.Row0Z,
            basis.Row1X, basis.Row1Y, basis.Row1Z,
            basis.Row2X, basis.Row2Y, basis.Row2Z,
        }.All(IsFiniteFloatBits);

    private static void ValidatePose(ThingActorPoseSnapshot? pose, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(pose, parameterName);
        if (!HasFiniteBasis(pose.BasisFloatBits))
        {
            throw new ArgumentException(
                "Thing/Actor pose basis must contain finite values.",
                parameterName);
        }
    }
}
