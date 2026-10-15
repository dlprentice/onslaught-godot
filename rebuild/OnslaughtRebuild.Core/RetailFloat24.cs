// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Round admitted geometry, segment-threshold and Unit Euler operations to
/// 24 significand bits, ties to even.
/// The double carrier retains the exponent range needed between the measured
/// float-input geometry operations; a float cast represents a separate store.
/// The segment threshold additionally uses retail's binary64 0.3 coefficient.
/// PC24/RN also matches the bounded September 8 copied-retail Plane observation
/// under WineD3D. Other call sites and rendering backends remain unmeasured.
/// </summary>
internal static class RetailFloat24
{
    public static double Add(double left, double right) => Round(left + right);
    public static double Subtract(double left, double right) => Round(left - right);
    public static double Multiply(double left, double right) => Round(left * right);
    public static double Divide(double left, double right) => Round(left / right);
    public static double Sqrt(double value) => Round(Math.Sqrt(value));

    private static double Round(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Retail arithmetic must remain finite.");
        if (value == 0) return value; // Preserve the arithmetic operation's zero sign.
        int exponent = Math.ILogB(value);
        double significand = Math.ScaleB(value, 23 - exponent);
        return Math.ScaleB(Math.Round(significand, MidpointRounding.ToEven), exponent - 23);
    }
}
