// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace OnslaughtRebuild.Parity;

/// <summary>How one probe compares between a reference (retail) trace and a candidate (rebuild) trace.</summary>
public sealed record ProbeComparison(
    string Name,
    int ReferenceSamples,
    int ComparedSamples,
    int MatchingSamples,
    int MissingSamples,
    long? FirstDivergenceFrame,
    int ReferenceChanges)
{
    /// <summary>Every reference sample is present in the candidate and equal byte for byte.</summary>
    public bool Exact => MissingSamples == 0 && MatchingSamples == ReferenceSamples;
}

/// <summary>
/// Byte comparison of a candidate probe trace against a reference trace. The candidate's frames can be shifted by
/// a frame offset (candidate frame + offset = reference frame); a non-zero offset means the two runs did not
/// start gameplay at the same Present, so such a result is never a 1:1 match.
/// </summary>
public sealed class TraceComparison
{
    private TraceComparison(IReadOnlyList<ProbeComparison> probes, long frameOffset, long? firstDivergence)
    {
        Probes = probes;
        FrameOffset = frameOffset;
        FirstDivergenceFrame = firstDivergence;
    }

    public IReadOnlyList<ProbeComparison> Probes { get; }

    public long FrameOffset { get; }

    /// <summary>The earliest reference frame at which any probe differs or is missing, if any.</summary>
    public long? FirstDivergenceFrame { get; }

    /// <summary>All probes exact at every reference sample, with no frame offset.</summary>
    public bool IsOneToOne => FrameOffset == 0 && Probes.All(p => p.Exact);

    public static TraceComparison Compare(ProbeTrace reference, ProbeTrace candidate, long frameOffset = 0)
    {
        var results = new List<ProbeComparison>();
        long? overall = null;
        long[] frames = reference.Frames.ToArray();
        foreach (string name in ProbeCatalog.Names)
        {
            int referenceSamples = 0, compared = 0, matching = 0, missing = 0, changes = 0;
            long? first = null;
            byte[]? previous = null;
            foreach (long frame in frames)
            {
                if (!reference.TryGet(frame, name, out byte[] expected))
                {
                    continue;
                }

                referenceSamples++;
                if (previous is not null && !previous.AsSpan().SequenceEqual(expected))
                {
                    changes++;
                }

                previous = expected;
                if (!candidate.TryGet(frame - frameOffset, name, out byte[] actual))
                {
                    missing++;
                    first ??= frame;
                    continue;
                }

                compared++;
                if (actual.AsSpan().SequenceEqual(expected))
                {
                    matching++;
                }
                else
                {
                    first ??= frame;
                }
            }

            if (referenceSamples == 0)
            {
                continue;
            }

            results.Add(new ProbeComparison(name, referenceSamples, compared, matching, missing, first, changes));
            if (first is long f && (overall is null || f < overall))
            {
                overall = f;
            }
        }

        return new TraceComparison(results, frameOffset, overall);
    }

    /// <summary>
    /// The first sample frame at which the trace's event time is non-zero, with the event-manager tick count it
    /// implies (event time = ticks × 0.05). Gives the approximate Present at which gameplay ticks began.
    /// </summary>
    public static (long Frame, int Ticks)? FirstTickSample(ProbeTrace trace)
    {
        foreach (long frame in trace.Frames)
        {
            if (trace.TryGet(frame, ProbeCatalog.EventTime, out byte[] evt))
            {
                float seconds = BinaryPrimitives.ReadSingleLittleEndian(evt);
                if (seconds != 0f)
                {
                    return (frame, (int)Math.Round(seconds / 0.05, MidpointRounding.ToEven));
                }
            }
        }

        return null;
    }

    /// <summary>A plain-text report: one row per probe, then the verdict and the first differing values.</summary>
    public string Report(ProbeTrace reference, ProbeTrace candidate)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"frame offset (candidate + offset = reference): {FrameOffset}\n");
        sb.Append("probe | reference samples | compared | matching | missing | first divergence | reference changes\n");
        foreach (ProbeComparison p in Probes)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"{p.Name} | {p.ReferenceSamples} | {p.ComparedSamples} | {p.MatchingSamples} | {p.MissingSamples} | {(p.FirstDivergenceFrame?.ToString(CultureInfo.InvariantCulture) ?? "none")} | {p.ReferenceChanges}\n");
        }

        sb.Append(IsOneToOne
            ? "verdict: 1:1 (every probe equal at every reference sample, no frame offset)\n"
            : FrameOffset != 0 && Probes.All(p => p.Exact)
                ? "verdict: NOT 1:1 (probes equal only under a non-zero frame offset)\n"
                : string.Create(CultureInfo.InvariantCulture, $"verdict: NOT 1:1 (first divergence at reference frame {FirstDivergenceFrame})\n"));

        foreach (ProbeComparison p in Probes)
        {
            if (p.FirstDivergenceFrame is not long frame)
            {
                continue;
            }

            reference.TryGet(frame, p.Name, out byte[] expected);
            string actual = candidate.TryGet(frame - FrameOffset, p.Name, out byte[] got)
                ? ProbeCatalog.Describe(p.Name, got)
                : "(missing)";
            sb.Append(CultureInfo.InvariantCulture,
                $"  {p.Name} @ {frame}: reference {ProbeCatalog.Describe(p.Name, expected)}, candidate {actual}\n");
        }

        return sb.ToString();
    }
}
