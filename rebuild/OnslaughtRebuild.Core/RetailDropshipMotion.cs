// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// A living <c>CDropship</c>'s flight arithmetic, from the pristine specimen
/// (<c>74154bfa…7750</c>) and the RE lane's contract
/// (<c>reverse-engineering/game-mechanics/dropship-flight.md</c>). Retail z
/// points down. Every operation is PC24 arithmetic and each named store is a
/// float32 store, as <see cref="RetailPlaneMotion"/> does for the plane.
/// Scheduling, move orders and the unit's state belong to the caller.
/// </summary>
public static class RetailDropshipMotion
{
    /// <summary>Slot 73 (<c>0x0050eb60</c>): 0.99 (<c>0x005d8cc4</c>).</summary>
    public const int DampingFloatBits = 0x3f7d70a4;

    /// <summary>A Big unit in the water damps by 0.95 (<c>0x005d8600</c>).</summary>
    public const int WaterDampingFloatBits = 0x3f733333;

    /// <summary>The run-out's arrival distance, 4.0 (<c>0x005d85bc</c>).</summary>
    public const float RunOutDistance = 4f;

    /// <summary>The run-out's reach along the nose, 300.0 (<c>0x005db520</c>).</summary>
    public const float RunOutReach = 300f;

    private static readonly double Pi = Read(0x40490fdb);
    private static readonly double HalfPi = Read(0x3fc90fdb);
    private static readonly double TwoPi = Read(0x40c90fdb);
    private static readonly double MaximumRoll = Read(0x3ec90fdb);   // π/8, 0x005d9440
    private static readonly double BankThreshold = Read(0x3f060a92); // π/6, 0x005d8df0
    private static readonly double Thrust = Read(0x3a83126f);        // 0.001, 0x005d8580

    /// <summary>
    /// The air step's damping (<c>0x00403038-0x004030d1</c>): a Big unit
    /// (profile <c>+0x124</c>) whose z plus its render radius (slot 16) times
    /// 0.2 (<c>0x005d8604</c>) is below the water level damps by 0.95;
    /// otherwise slot 73 applies.
    /// </summary>
    public static int SelectDamping(bool big, int meshRadiusFloatBits, int positionZFloatBits, float waterLevel)
    {
        if (!big) return DampingFloatBits;
        double reach = RetailFloat24.Add(
            RetailFloat24.Multiply(Read(meshRadiusFloatBits), Read(0x3e4ccccd)), Read(positionZFloatBits));
        return reach > waterLevel ? WaterDampingFloatBits : DampingFloatBits;
    }

    /// <summary>
    /// <c>CDropshipGuide</c>'s update (<c>0x00448930</c>) in landing states 0
    /// and 1 (<c>0x00448a64-0x00448c3d</c>), from the pose before this tick's
    /// Move. <paramref name="floor"/> is the higher of the ground and the water
    /// (the smaller z). The drive is 0.001 times the nose (matrix column 1)
    /// whatever the distance, and the yaw target follows the guide's mode.
    /// </summary>
    public static RetailPlaneGuideOutput UpdateCruise(
        RetailActorPoseSnapshot pose,
        Level100FloatVector3Bits currentEuler,
        Level100FloatVector3Bits goal,
        int mode,
        float floor,
        int minimumAltitudeFloatBits)
    {
        ArgumentNullException.ThrowIfNull(pose);
        double x = Read(pose.PositionFloatBits.X), y = Read(pose.PositionFloatBits.Y);
        double z = Read(pose.PositionFloatBits.Z);
        double height = Store(RetailFloat24.Subtract(floor, z));
        double dz = Store(RetailFloat24.Subtract(Read(goal.Z), z));
        double dy = Store(RetailFloat24.Subtract(Read(goal.Y), y));
        double dx = Store(RetailFloat24.Subtract(Read(goal.X), x));
        double current = Read(currentEuler.X);
        double yaw = Store(-Math.Atan2(dx, dy));
        int pitchBits = 0;
        if (mode == 0) yaw = Store(RetailFloat24.Add(current, HalfPi));
        else if (mode == 2) yaw = Store(RetailFloat24.Subtract(yaw, Pi));
        // Below MinAltitude the nose comes up and the heading holds.
        if (height < Read(minimumAltitudeFloatBits))
        {
            yaw = current;
            pitchBits = unchecked((int)0xbf20d97c);
        }
        if (yaw > Pi) yaw = Store(RetailFloat24.Subtract(yaw, TwoPi));
        if (yaw < -Pi) yaw = Store(RetailFloat24.Add(yaw, TwoPi));

        // The turn still to make, the short way round, at most π/8.
        double adjusted = yaw;
        if (current < -HalfPi && yaw > HalfPi) adjusted = RetailFloat24.Subtract(yaw, TwoPi);
        else if (current > HalfPi && yaw < -HalfPi) adjusted = RetailFloat24.Add(yaw, TwoPi);
        double turn = Math.Abs(RetailFloat24.Subtract(current, adjusted));
        if (turn > MaximumRoll) turn = MaximumRoll;

        // Roll by that much, signed by the goal's side of the right axis
        // (matrix column 0; 0x00448b78-0x00448bbd).
        Level100FloatBasis3Bits b = pose.BasisFloatBits;
        double side = RetailFloat24.Add(RetailFloat24.Add(
            RetailFloat24.Multiply(Read(b.Row1X), dy),
            RetailFloat24.Multiply(Read(b.Row2X), dz)),
            RetailFloat24.Multiply(Read(b.Row0X), dx));
        double roll = side < 0 ? turn : -turn;

        return new(
            new(Bits(yaw), pitchBits, Bits(roll)),
            new(Bits(RetailFloat24.Multiply(Read(b.Row0Y), Thrust)),
                Bits(RetailFloat24.Multiply(Read(b.Row1Y), Thrust)),
                Bits(RetailFloat24.Multiply(Read(b.Row2Y), Thrust))),
            turn > BankThreshold ? 0x3f800000 : 0);
    }

    /// <summary>
    /// The unit step's run-out test (<c>0x004fb070-0x004fb0bd</c>): the 2D
    /// distance from the moved position to the goal, <c>√(dy² + dx²)</c>, is
    /// below 4.0.
    /// </summary>
    public static bool ReachedRunOut(Level100FloatVector3Bits position, Level100FloatVector3Bits goal)
    {
        double dx = RetailFloat24.Subtract(Read(position.X), Read(goal.X));
        double dy = RetailFloat24.Subtract(Read(position.Y), Read(goal.Y));
        return RetailFloat24.Sqrt(RetailFloat24.Add(
            RetailFloat24.Multiply(dy, dy), RetailFloat24.Multiply(dx, dx))) < RunOutDistance;
    }

    /// <summary>
    /// The run-out order's target (<c>0x004fb0c7-0x004fb111</c>): 300 along
    /// the nose in x and y from the moved position, at its height.
    /// </summary>
    public static Level100FloatVector3Bits RunOutTarget(RetailActorPoseSnapshot pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Level100FloatBasis3Bits b = pose.BasisFloatBits;
        double reachY = Store(RetailFloat24.Multiply(Read(b.Row1Y), RunOutReach));
        return new(
            Bits(RetailFloat24.Add(RetailFloat24.Multiply(Read(b.Row0Y), RunOutReach),
                Read(pose.PositionFloatBits.X))),
            Bits(RetailFloat24.Add(reachY, Read(pose.PositionFloatBits.Y))),
            pose.PositionFloatBits.Z);
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
