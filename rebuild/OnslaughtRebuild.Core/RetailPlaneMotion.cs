// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Retained living-Plane fields. Position and both bases remain in the Actor
/// owner; these words cannot be recovered from its presentation projection.
/// </summary>
public sealed record RetailPlaneMotionSnapshot(
    Level100FloatVector3Bits Velocity,
    Level100FloatVector3Bits Drive,
    Level100FloatVector3Bits CurrentEuler,
    Level100FloatVector3Bits DesiredEuler,
    Level100FloatVector3Bits EulerRates,
    int BankFlagFloatBits);

/// <summary>Inputs retained by the guide/controller before Unit Move.</summary>
public sealed record RetailPlaneGuideInput(
    Level100FloatVector3Bits Destination,
    int Mode,
    int ClearanceFloatBits,
    int ControllerState,
    int SpeedMode,
    Level100FloatVector3Bits? AvoidancePosition);

public sealed record RetailPlaneGuideOutput(
    Level100FloatVector3Bits DesiredEuler,
    Level100FloatVector3Bits Drive,
    int BankFlagFloatBits);

/// <summary>
/// Finite living-Plane arithmetic from pristine 74154bfa…7750. The Air,
/// Guide, Actor, Unit and Plane bodies are recorded in CComplexThing.cpp.md.
/// Each operation follows PC24/RN and each Store is a distinct float spill.
/// Guide scheduling, controller decisions, contacts and effects are callers'
/// responsibilities. Managed trig has bounded native comparisons only.
/// </summary>
public static class RetailPlaneMotion
{
    private static readonly double Pi = Read(0x40490fdb);
    private static readonly double HalfPi = Read(0x3fc90fdb);
    private static readonly double TwoPi = Read(0x40c90fdb);
    private static readonly double Tick = Read(0x3d4ccccd);

    /// <summary>The selected living CPlane initializer starts at rest.</summary>
    public static RetailPlaneMotionSnapshot CreateInitial(RetailActorPoseSnapshot pose,
        Level100FloatVector3Bits euler, int turnRateFloatBits) =>
        // The Euler rates start at the type's air turn rate (unit +0xb8).
        CreateInitial(pose, euler,
            new Level100FloatVector3Bits(turnRateFloatBits, turnRateFloatBits, turnRateFloatBits));

    /// <summary>
    /// An air unit at rest with its own yaw, pitch and roll rates:
    /// <c>CAirUnit::Init</c> sets all three to the air turn rate
    /// (<c>0x00402b0c-0x00402b32</c>), and <c>CDropship::Init</c> then quarters
    /// the pitch and roll rates (<c>0x00446dc2-0x00446e0c</c>).
    /// </summary>
    public static RetailPlaneMotionSnapshot CreateInitial(RetailActorPoseSnapshot pose,
        Level100FloatVector3Bits euler, Level100FloatVector3Bits rates)
    {
        ArgumentNullException.ThrowIfNull(pose);
        _ = Read(euler.X); _ = Read(euler.Y); _ = Read(euler.Z);
        _ = Read(rates.X); _ = Read(rates.Y); _ = Read(rates.Z);
        return new(default, default, euler, euler, rates, 0);
    }

    /// <summary>
    /// Script SpawnThing 536ddb..536e68: yaw uses the retained scaled second
    /// column; pitch uses its stored XYZ and stored norm. Roll remains +0.
    /// This consumes an already selected WORLD emitter pose, not an authored
    /// local matrix or a presentation projection.
    /// </summary>
    public static Level100FloatVector3Bits EulerFromSpawnerBasis(Level100FloatBasis3Bits basis)
    {
        double x = RetailFloat24.Multiply(Read(basis.Row0Y), 100);
        double y = RetailFloat24.Multiply(Read(basis.Row1Y), 100);
        double z = Store(RetailFloat24.Multiply(Read(basis.Row2Y), 100));
        int yaw = Bits(-Math.Atan2(x, y));
        double norm = Magnitude(Store(x), Store(y), z);
        int pitch = norm > 0 ? Bits(Math.Asin(RetailFloat24.Divide(z, Store(norm)))) : 0;
        return new(yaw, pitch, 0);
    }

    /// <summary>
    /// Living full Move with no intervening contact response. Air consumes
    /// last drive; Guide writes next drive; Actor translates and retains old
    /// pose; Unit smooths; Plane aligns velocity. Collision/lifecycle owners
    /// must supply their own response before claiming complete Plane Move.
    /// </summary>
    internal static void AdvanceFreeFlight(ThingActorBaseState actor,
        RetailPlaneGuideInput guide, int airSpeedFloatBits, int eventTimeFloatBits)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ThingActorBaseStateSnapshot before = actor.Snapshot;
        RetailPlaneMotionSnapshot motion = before.RetailPlane ??
            throw new InvalidOperationException("Plane creation inputs were not admitted.");
        RetailActorPoseSnapshot pose = before.RetailPoses!.Current;
        Level100FloatVector3Bits velocity = IntegrateVelocity(motion.Velocity,
            motion.Drive, airSpeedFloatBits, guide.SpeedMode);
        RetailPlaneGuideOutput next = UpdateGuide(pose, velocity, guide);
        Level100FloatVector3Bits position = Translate(pose.PositionFloatBits, velocity);
        Level100FloatVector3Bits euler = RetailUnitEuler.Smooth(motion.CurrentEuler,
            next.DesiredEuler, motion.EulerRates, 1f);
        bool skipsEuler = Read(motion.CurrentEuler.X) == Read(next.DesiredEuler.X) &&
            Read(motion.CurrentEuler.Y) == Read(next.DesiredEuler.Y) &&
            Read(motion.CurrentEuler.Z) == Read(next.DesiredEuler.Z);
        Level100FloatBasis3Bits basis = skipsEuler ? pose.BasisFloatBits : RetailUnitEuler.BuildBasis(euler);
        actor.CommitRetailPlaneMove(new(position, basis), motion with
        {
            Velocity = AlignVelocity(velocity, basis), Drive = next.Drive,
            CurrentEuler = euler, DesiredEuler = next.DesiredEuler,
            BankFlagFloatBits = next.BankFlagFloatBits,
        }, eventTimeFloatBits);
    }

    /// <summary>
    /// Recompute the 9-by-9 clearance sample only when its caller dispatches
    /// the cache event and accepts a changed rounded cell. This method does
    /// not initialize unwritten cache coordinates or decide event timing.
    /// </summary>
    public static int ComputeClearance(Level100Terrain terrain,
        Level100FloatVector3Bits position)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        int x = checked((int)Math.Round(Read(position.X), MidpointRounding.ToEven));
        int y = checked((int)Math.Round(Read(position.Y), MidpointRounding.ToEven));
        double height = -Read(position.Z);
        double minimum = Read(0x497423f0); // 402939 writes the finite ceiling, not FLT_MAX.
        for (int offsetY = -20; offsetY <= 20; offsetY += 5)
        {
            for (int offsetX = -20; offsetX <= 20; offsetX += 5)
            {
                int sample = terrain.SampleAirGuideHeightUnits(checked(x + offsetX), checked(y + offsetY));
                double ground = -RetailFloat24.Multiply(sample, terrain.HeightScale);
                double candidate = RetailFloat24.Subtract(height, Math.Max(ground, -terrain.WaterLevel));
                if (candidate < minimum) minimum = Store(candidate);
            }
        }
        return Bits(minimum);
    }

    /// <summary>
    /// Air consumes the previous guide drive, adds living gravity, applies
    /// ordinary friction, and only then applies its strict stored-norm cap.
    /// </summary>
    public static Level100FloatVector3Bits IntegrateVelocity(
        Level100FloatVector3Bits velocity,
        Level100FloatVector3Bits drive,
        int airSpeedFloatBits,
        int speedMode) =>
        IntegrateAirVelocity(velocity, drive, 0x3f7ae148, airSpeedFloatBits, speedMode);

    /// <summary>
    /// The living air-unit step (<c>0x00402fa0</c>) before its unit step: add
    /// the drive, add the unit's gravity (slot 45, zero for a living plane or
    /// dropship), damp by <paramref name="dampingFloatBits"/> (slot 73, or
    /// 0.95 for a Big unit in the water), then the strict stored-norm cap
    /// <c>+0xb4</c> × 0.05, the air velocity times 1.5 while retreating
    /// (slot 111, <c>0x004fe5c0</c>).
    /// </summary>
    public static Level100FloatVector3Bits IntegrateAirVelocity(
        Level100FloatVector3Bits velocity,
        Level100FloatVector3Bits drive,
        int dampingFloatBits,
        int airSpeedFloatBits,
        int speedMode)
    {
        double x = Store(RetailFloat24.Add(Read(velocity.X), Read(drive.X)));
        double y = Store(RetailFloat24.Add(Read(velocity.Y), Read(drive.Y)));
        double z = Store(RetailFloat24.Add(Read(velocity.Z), Read(drive.Z)));
        z = Store(RetailFloat24.Add(z, 0.0));
        double friction = Read(dampingFloatBits);
        x = Store(RetailFloat24.Multiply(x, friction));
        y = Store(RetailFloat24.Multiply(y, friction));
        z = Store(RetailFloat24.Multiply(z, friction));
        double norm = Magnitude(x, y, z);
        double storedNorm = Store(norm);
        double speed = Read(airSpeedFloatBits);
        if (speed < 0) throw new ArgumentOutOfRangeException(nameof(airSpeedFloatBits));
        if (speedMode is 1 or 2) speed = RetailFloat24.Multiply(speed, 1.5);
        double limit = RetailFloat24.Multiply(speed, Tick);
        if (norm > 0 && storedNorm > limit)
        {
            double ratio = RetailFloat24.Divide(limit, storedNorm);
            x = RetailFloat24.Multiply(x, ratio);
            y = RetailFloat24.Multiply(y, ratio);
            z = RetailFloat24.Multiply(z, ratio);
        }
        return new(Bits(x), Bits(y), Bits(z));
    }

    /// <summary>
    /// Guide Update (402280). It reads the velocity already integrated by
    /// Air and the pre-translation pose, and produces the NEXT update's drive.
    /// Clearance and avoidance are supplied cached inputs, not sampled here.
    /// </summary>
    public static RetailPlaneGuideOutput UpdateGuide(
        RetailActorPoseSnapshot pose,
        Level100FloatVector3Bits integratedVelocity,
        RetailPlaneGuideInput guide)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(guide);
        double x = Read(pose.PositionFloatBits.X), y = Read(pose.PositionFloatBits.Y);
        double z = Read(pose.PositionFloatBits.Z);
        double dx = Store(RetailFloat24.Subtract(Read(guide.Destination.X), x));
        double dy = Store(RetailFloat24.Subtract(Read(guide.Destination.Y), y));
        double dz = Store(RetailFloat24.Subtract(Read(guide.Destination.Z), z));
        double heading = Store(-Math.Atan2(dx, dy));
        double horizontal = RetailFloat24.Sqrt(RetailFloat24.Add(
            RetailFloat24.Multiply(dy, dy), RetailFloat24.Multiply(dx, dx)));
        double pitch = Store(Math.Atan2(dz, horizontal));
        double velocityHeading = Store(-Math.Atan2(
            Read(integratedVelocity.X), Read(integratedVelocity.Y)));
        if (guide.Mode == 0)
        {
            heading = RetailFloat24.Add(velocityHeading, HalfPi);
            pitch = 0;
        }
        else if (guide.Mode == 2) heading = RetailFloat24.Subtract(heading, Pi);
        heading = Wrap(heading);
        pitch = Store(Wrap(pitch));

        if (guide.ControllerState != 2)
        {
            if (guide.AvoidancePosition is { } avoidance)
            {
                pitch = RetailFloat24.Subtract(Read(avoidance.Z), z) > 0
                    ? -Read(0x3f860a92) : Read(0x3f860a92);
            }
            double clearance = Read(guide.ClearanceFloatBits);
            if (clearance < 5) pitch = -Read(0x3f490fdb);
            else if (clearance < 15 && pitch > 0) pitch = 0;
            else if (clearance > 50) pitch = Read(0x3f490fdb);
            if (guide.SpeedMode is not (1 or 2))
            {
                if (x < 10) heading = -HalfPi;
                else if (x > 502) heading = HalfPi;
                else if (y < 10) heading = 0;
                else if (y > 502) heading = Pi;
            }
        }

        double adjustedHeading = heading;
        if (velocityHeading < -HalfPi && heading > HalfPi)
            adjustedHeading = RetailFloat24.Subtract(heading, TwoPi);
        else if (velocityHeading > HalfPi && heading < -HalfPi)
            adjustedHeading = RetailFloat24.Add(heading, TwoPi);
        double bank = Math.Min(Math.Abs(RetailFloat24.Subtract(
            velocityHeading, adjustedHeading)), HalfPi);
        Level100FloatBasis3Bits b = pose.BasisFloatBits;
        double side = RetailFloat24.Add(RetailFloat24.Add(
            RetailFloat24.Multiply(Read(b.Row2X), dz),
            RetailFloat24.Multiply(Read(b.Row1X), dy)),
            RetailFloat24.Multiply(Read(b.Row0X), dx));
        bank = Store(side < 0 ? bank : -bank);
        double scale = RetailFloat24.Multiply(RetailFloat24.Multiply(15.0, Tick), 4.0);
        return new(new(Bits(heading), Bits(pitch), Bits(bank)),
            new(Bits(RetailFloat24.Multiply(Read(b.Row0Y), scale)),
                Bits(RetailFloat24.Multiply(Read(b.Row1Y), scale)),
                Bits(RetailFloat24.Multiply(Read(b.Row2Y), scale))),
            Math.Abs(bank) > Read(0x3dcccccd) ? 0x3f800000 : 0);
    }

    /// <summary>Actor adds the per-move velocity without another time scale.</summary>
    public static Level100FloatVector3Bits Translate(
        Level100FloatVector3Bits position, Level100FloatVector3Bits velocity) => new(
        Bits(RetailFloat24.Add(Read(position.X), Read(velocity.X))),
        Bits(RetailFloat24.Add(Read(position.Y), Read(velocity.Y))),
        Bits(RetailFloat24.Add(Read(position.Z), Read(velocity.Z))));

    /// <summary>Plane realigns post-contact velocity to the new forward column.</summary>
    public static Level100FloatVector3Bits AlignVelocity(
        Level100FloatVector3Bits velocity, Level100FloatBasis3Bits basis)
    {
        double magnitude = Magnitude(Read(velocity.X), Read(velocity.Y), Read(velocity.Z));
        return new(Bits(RetailFloat24.Multiply(magnitude, Read(basis.Row0Y))),
            Bits(RetailFloat24.Multiply(magnitude, Read(basis.Row1Y))),
            Bits(RetailFloat24.Multiply(magnitude, Read(basis.Row2Y))));
    }

    private static double Magnitude(double x, double y, double z) =>
        RetailFloat24.Sqrt(RetailFloat24.Add(RetailFloat24.Add(
            RetailFloat24.Multiply(x, x), RetailFloat24.Multiply(y, y)),
            RetailFloat24.Multiply(z, z)));

    private static double Wrap(double value)
    {
        if (value > Pi) value = RetailFloat24.Subtract(value, TwoPi);
        if (value < -Pi) value = RetailFloat24.Add(value, TwoPi);
        return value;
    }

    private static double Read(int bits)
    {
        float value = BitConverter.Int32BitsToSingle(bits);
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(bits));
        return value;
    }

    private static float Store(double value)
    {
        float stored = (float)value;
        if (!float.IsFinite(stored)) throw new ArgumentOutOfRangeException(nameof(value));
        return stored;
    }

    private static int Bits(double value) => BitConverter.SingleToInt32Bits(Store(value));
}
