// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// The x87 model against oracle tables from a VC6 SP5 harness compiled with retail's flags and run at 24-bit precision
/// control (rebuild/tools/x87_oracle). Each table holds the harness's inputs and its outputs bit for bit; the model must
/// reproduce every one. Skipped where the tables have not been generated.
/// </summary>
public sealed class X87OracleTests
{
    [X87OracleTheory]
    [InlineData("add_d")]
    [InlineData("sub_d")]
    [InlineData("mul_d")]
    [InlineData("div_d")]
    [InlineData("sqrt_d")]
    [InlineData("sin_d")]
    [InlineData("cos_d")]
    [InlineData("atan2_d")]
    public void DoubleResultsMatchTheOracle(string op)
    {
        Func<double, double, double> model = op switch
        {
            "add_d" => X87.Add,
            "sub_d" => X87.Sub,
            "mul_d" => X87.Mul,
            "div_d" => X87.Div,
            "sqrt_d" => (a, _) => X87.Sqrt(a),
            "sin_d" => (a, _) => X87.Sin(a),
            "cos_d" => (a, _) => X87.Cos(a),
            "atan2_d" => X87.Atan2,
            _ => throw new ArgumentException(op),
        };
        (double[] a, double[] b, byte[] output) = Inputs(op, 8);
        bool transcendental = op is "sin_d" or "cos_d" or "atan2_d";
        if (!transcendental)
        {
            Check(op, a.Length, i =>
            {
                double expected = BinaryPrimitives.ReadDoubleLittleEndian(output.AsSpan(i * 8));
                double actual = model(a[i], b[i]);
                return (Same(expected, actual), $"{a[i]:R}, {b[i]:R}: oracle {expected:R}, model {actual:R}");
            });
            return;
        }

        // A transcendental's 64-bit register result stored to a double is rounded twice; the model approximates it to
        // double precision. Rounded to a float, the precision the game's float code consumes, every result must match;
        // at double precision the measured share that differs is a ceiling (see X87.Sin).
        int doubleDifferences = 0;
        Check(op, a.Length, i =>
        {
            double expected = BinaryPrimitives.ReadDoubleLittleEndian(output.AsSpan(i * 8));
            double actual = model(a[i], b[i]);
            if (!Same(expected, actual))
            {
                doubleDifferences++;
            }

            return ((float)expected == (float)actual || (double.IsNaN(expected) && double.IsNaN(actual)),
                $"{a[i]:R}, {b[i]:R}: oracle {expected:R}, model {actual:R} (as floats)");
        });
        // The overall ceiling is a property of this table's input mix and seed (about 15% of the sin/cos inputs lie
        // between 2 pi and 1e4); the real statement is per range: about 0.13% differ within +-2 pi and about 10.6% up to
        // 1e4, where fsin and fcos reduce their argument with a 66-bit pi (X87.Sin, PARITY.md).
        double ceiling = op == "atan2_d" ? 0.001 : 0.02;
        Assert.True(doubleDifferences <= ceiling * a.Length,
            $"{op}: {doubleDifferences} of {a.Length} differ at double precision, above the measured ceiling {ceiling:P1}");
    }

    [X87OracleTheory]
    [InlineData("add_f")]
    [InlineData("sub_f")]
    [InlineData("mul_f")]
    [InlineData("div_f")]
    [InlineData("sqrt_f")]
    [InlineData("sin_f")]
    [InlineData("cos_f")]
    [InlineData("atan2_f")]
    [InlineData("d2f")]
    public void FloatResultsMatchTheOracle(string op)
    {
        Func<double, double, float> model = op switch
        {
            "add_f" => (a, b) => (float)a + (float)b,
            "sub_f" => (a, b) => (float)a - (float)b,
            "mul_f" => (a, b) => (float)a * (float)b,
            "div_f" => (a, b) => (float)a / (float)b,
            "sqrt_f" => (a, _) => (float)X87.Sqrt(a),
            "sin_f" => (a, _) => (float)X87.Sin(a),
            "cos_f" => (a, _) => (float)X87.Cos(a),
            "atan2_f" => (a, b) => (float)X87.Atan2(a, b),
            "d2f" => (a, _) => (float)a,
            _ => throw new ArgumentException(op),
        };
        (double[] a, double[] b, byte[] output) = Inputs(op, 4);
        // The harness's (float) casts of its operands are not stores at /O2: VC6 keeps a double operand at full
        // precision in the register (the oracle shows (float)1e300 * 0 = 0, not NaN). For the float operators only the
        // operands that are already floats test float arithmetic, so the others are left out.
        bool floatOperator = op is "add_f" or "sub_f" or "mul_f" or "div_f";
        Check(op, a.Length, i =>
        {
            if (floatOperator && ((double)(float)a[i] != a[i] || (double)(float)b[i] != b[i]) && !double.IsNaN(a[i]) && !double.IsNaN(b[i]))
            {
                return (true, "");
            }

            float expected = BinaryPrimitives.ReadSingleLittleEndian(output.AsSpan(i * 4));
            float actual = model(a[i], b[i]);
            bool same = BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual)
                || (float.IsNaN(expected) && float.IsNaN(actual));
            return (same, $"{a[i]:R}, {b[i]:R}: oracle {expected:R}, model {actual:R}");
        });
    }

    [X87OracleTheory]
    [InlineData("fist")]
    [InlineData("trunc")]
    public void IntegerConversionsMatchTheOracle(string op)
    {
        Func<double, int> model = op == "fist" ? X87.Fist : X87.Ftol;
        (double[] a, _, byte[] output) = Inputs(op, 4);
        Check(op, a.Length, i =>
        {
            int expected = BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(i * 4));
            int actual = model(a[i]);
            return (expected == actual, $"{a[i]:R}: oracle {expected}, model {actual}");
        });
    }

    private static (double[] A, double[] B, byte[] Output) Inputs(string op, int resultBytes)
    {
        (byte[] input, byte[] output, int n) = X87OracleTable.Read(X87Oracle.Root!, op, resultBytes, minimumCount: 1_000_000);
        var a = new double[n];
        var b = new double[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = BinaryPrimitives.ReadDoubleLittleEndian(input.AsSpan(i * 16));
            b[i] = BinaryPrimitives.ReadDoubleLittleEndian(input.AsSpan(i * 16 + 8));
        }

        return (a, b, output);
    }

    private static bool Same(double expected, double actual) =>
        BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual)
        || (double.IsNaN(expected) && double.IsNaN(actual));

    private static void Check(string op, int count, Func<int, (bool Same, string Detail)> compare)
    {
        int mismatches = 0;
        var examples = new List<string>();
        for (int i = 0; i < count; i++)
        {
            (bool same, string detail) = compare(i);
            if (!same)
            {
                mismatches++;
                if (examples.Count < 8)
                {
                    examples.Add($"#{i}: {detail}");
                }
            }
        }

        Assert.True(mismatches == 0, $"{op}: {mismatches} of {count} differ from the oracle\n{string.Join('\n', examples)}");
    }
}

/// <summary>A theory that needs the x87 oracle tables; skipped where they are absent.</summary>
public sealed class X87OracleTheoryAttribute : TheoryAttribute
{
    public X87OracleTheoryAttribute()
    {
        if (X87Oracle.Root is null)
        {
            Skip = "x87 oracle tables not found (run rebuild/tools/x87_oracle/x87_oracle.py, set X87_ORACLE_OUT)";
        }
    }
}

internal static class X87Oracle
{
    public static string? Root { get; } = Find();

    public static string File(string name) => Path.Combine(Root!, name);

    private static string? Find()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("X87_ORACLE_OUT");
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return System.IO.File.Exists(Path.Combine(fromEnvironment, "manifest.json")) ? fromEnvironment : null;
        }

        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "local-data", "parity-oracle", "x87");
            if (System.IO.File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }
        }

        return null;
    }
}
