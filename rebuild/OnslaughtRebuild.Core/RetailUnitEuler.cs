// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The finite PC24/RN angle update and matrix operation order of Unit 0x004fa4b0.
/// XYZ are retained retail yaw, pitch and roll words, not angles recovered
/// from a projected matrix. RetailPlaneMotion owns its ordered integration
/// into the living full Move path.
/// </summary>
/// <remarks>
/// The pinned partial source lacks Unit.cpp. The pristine 74154bfa…7750
/// instructions and isolated native oracle are recorded in the existing
/// reverse-engineering/binary-analysis/functions/CComplexThing.cpp.md map.
/// PC24/RN was also observed at twelve actual Plane calls in the private
/// September 8 WineD3D run. This does not establish other rendering backends.
/// BuildBasis uses managed trig; its native comparisons are finite evidence,
/// not a general claim of x87 transcendental equivalence.
/// </remarks>
public static class RetailUnitEuler
{
    private static readonly double HalfPi = Read(0x3fc90fdb);
    private static readonly double Pi = Read(0x40490fdb);
    private static readonly double TwoPi = Read(0x40c90fdb);
    private static readonly double Ease = Read(0x3dcccccd);

    public static Level100FloatVector3Bits Smooth(
        Level100FloatVector3Bits current,
        Level100FloatVector3Bits desired,
        Level100FloatVector3Bits maximumStep,
        float moveMultiplier)
    {
        if (!float.IsFinite(moveMultiplier) || moveMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(moveMultiplier));

        return new(
            SmoothAxis(current.X, desired.X, maximumStep.X, moveMultiplier, wrap: true),
            SmoothAxis(current.Y, desired.Y, maximumStep.Y, moveMultiplier, wrap: false),
            SmoothAxis(current.Z, desired.Z, maximumStep.Z, moveMultiplier, wrap: true));
    }

    /// <summary>
    /// Reconstruct the nine meaningful retail matrix words after smoothing.
    /// The caller must preserve its old basis when Unit's three current/desired
    /// comparisons are all equal: retail then skips the complete routine.
    /// </summary>
    /// <remarks>
    /// Retail float-stores yaw sine/cosine and pitch/roll cosine, retaining the
    /// two other sine results for subsequent PC24 arithmetic. Managed double
    /// trig is provisional: all 31 admitted native cases match, including 11
    /// consecutive live transitions, but arbitrary x87 results and cross-host
    /// transcendental equivalence have not been established.
    /// </remarks>
    public static Level100FloatBasis3Bits BuildBasis(Level100FloatVector3Bits euler)
    {
        double yaw = Read(euler.X), pitch = Read(euler.Y), roll = Read(euler.Z);
        double yawCos = Store(Math.Cos(yaw)), yawSin = Store(Math.Sin(yaw));
        double rollCos = Store(Math.Cos(roll)), rollSin = Math.Sin(roll);
        double pitchCos = Store(Math.Cos(pitch)), pitchSin = Math.Sin(pitch);

        double sineProduct = RetailFloat24.Multiply(pitchSin, rollSin);
        // 0x004fa784 stores this already-PC24 product without popping it.
        // M02 retains its exponent range; M12 reloads the float32 copy.
        double mixedProduct = RetailFloat24.Multiply(pitchSin, rollCos);
        double storedMixedProduct = Store(mixedProduct);
        return new(
            StoreBits(RetailFloat24.Subtract(RetailFloat24.Multiply(rollCos, yawCos),
                RetailFloat24.Multiply(sineProduct, yawSin))),
            StoreBits(-RetailFloat24.Multiply(pitchCos, yawSin)),
            StoreBits(RetailFloat24.Add(RetailFloat24.Multiply(mixedProduct, yawSin),
                RetailFloat24.Multiply(rollSin, yawCos))),
            StoreBits(RetailFloat24.Add(RetailFloat24.Multiply(sineProduct, yawCos),
                RetailFloat24.Multiply(rollCos, yawSin))),
            StoreBits(RetailFloat24.Multiply(pitchCos, yawCos)),
            StoreBits(RetailFloat24.Subtract(RetailFloat24.Multiply(rollSin, yawSin),
                RetailFloat24.Multiply(storedMixedProduct, yawCos))),
            StoreBits(-RetailFloat24.Multiply(pitchCos, rollSin)),
            StoreBits(pitchSin),
            StoreBits(RetailFloat24.Multiply(pitchCos, rollCos)));
    }

    private static int SmoothAxis(int currentBits, int desiredBits, int rateBits,
        double multiplier, bool wrap)
    {
        double current = Read(currentBits);
        double desired = Read(desiredBits);
        // Retail skips the whole axis, including wrapping and stores. In
        // particular, equal -0/+0 must retain the current negative-zero word.
        if (current == desired) return currentBits;

        double rate = Read(rateBits);
        if (rate < 0) throw new ArgumentOutOfRangeException(nameof(rateBits));
        double cap = (float)RetailFloat24.Multiply(multiplier, rate);
        double adjustedDesired = desired;
        if (wrap)
        {
            if (current < -HalfPi && desired > HalfPi)
                adjustedDesired = RetailFloat24.Subtract(desired, TwoPi);
            else if (current > HalfPi && desired < -HalfPi)
                adjustedDesired = RetailFloat24.Add(desired, TwoPi);
        }

        double step = RetailFloat24.Multiply(
            RetailFloat24.Multiply(
                Math.Abs(RetailFloat24.Subtract(current, adjustedDesired)), multiplier), Ease);
        // The cap is a float store; the calculated step stays on the x87
        // stack until the angle update, even when it is below float's range.
        if (step > cap) step = cap;

        bool add = current < desired;
        if (wrap)
        {
            double originalDifference = add
                ? RetailFloat24.Subtract(desired, current)
                : RetailFloat24.Subtract(current, desired);
            if (originalDifference > Pi) add = !add;
        }

        float updated = Store(add
            ? RetailFloat24.Add(current, step)
            : RetailFloat24.Subtract(current, step));
        if (wrap)
        {
            // Exactly one correction, after the first float32 store.
            if (updated > Pi) updated = Store(RetailFloat24.Subtract(updated, TwoPi));
            else if (updated < -Pi) updated = Store(RetailFloat24.Add(updated, TwoPi));
        }
        return BitConverter.SingleToInt32Bits(updated);
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
        if (!float.IsFinite(stored))
            throw new ArgumentOutOfRangeException(nameof(value), "Unit Euler output must remain finite.");
        return stored;
    }

    private static int StoreBits(double value) => BitConverter.SingleToInt32Bits(Store(value));
}
