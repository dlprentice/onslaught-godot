// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/SeedUniformRandomNumberStream.{h,cpp} (CSeedUniformRandomNumberStream, 004de8c0 and
// 004de8d0).

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// The game's seeded random stream: a Park-Miller generator by Schrage's method with retail's modulus 214783647, a digit
/// short of 2^31 - 1. With that modulus Schrage's bound no longer holds (r = 25968 exceeds q = 4449), so r * hi can
/// overflow 32 bits; retail's int arithmetic wraps, and so does this (unchecked), which is how the stream's value can be
/// negative. Next returns the value's magnitude by hand, so int.MinValue stays int.MinValue.
/// </summary>
public sealed class SeedUniformRandomNumberStream
{
    /// <summary>RANDOM_MULTIPLIER (retail 006321f0).</summary>
    public const int Multiplier = 48271;

    /// <summary>RANDOM_MODULUS (retail 006321f4).</summary>
    public const int Modulus = 214783647;

    private const int Q = Modulus / Multiplier;
    private const int R = Modulus % Multiplier;

    public SeedUniformRandomNumberStream(int seed)
    {
        Value = Seed = seed;
    }

    /// <summary>mValue (+0x00): the last value drawn.</summary>
    public int Value { get; internal set; }

    /// <summary>mSeed (+0x04): the seed the stream was made with.</summary>
    public int Seed { get; }

    /// <summary>Draws made since the stream was created (retail keeps no count; it localises divergences).</summary>
    public long Draws { get; private set; }

    /// <summary>When set, each draw appends (draw number from 1, the caller's site) for comparison with a retail log.</summary>
    public List<(long Draw, string Site)>? Log { get; set; }

    /// <summary>Draws the next value and returns its magnitude.</summary>
    public int Next() => Next("");

    /// <summary>Next, recording <paramref name="site"/> in <see cref="Log"/> when it is set.</summary>
    public int Next(string site)
    {
        Draws++;
        Log?.Add((Draws, site));
        unchecked
        {
            int hi = Value / Q;
            int lo = Value % Q;
            int t = (Multiplier * lo) - (R * hi);
            Value = t > 0 ? t : t + Modulus;
            return Value < 0 ? -Value : Value;
        }
    }
}
