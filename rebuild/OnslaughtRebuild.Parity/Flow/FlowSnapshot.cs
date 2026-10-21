// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>Which part of the game a Present belongs to.</summary>
public enum FlowSegment
{
    Boot,
    FrontEnd,
    Level,
}

/// <summary>
/// One Present of a whole run (<see cref="GameRun"/>), captured by the thread that runs it, at the Present's end (after
/// the tape's events for it): an immutable copy, so another thread can read it while the run goes on. It holds the
/// fields the route references compare: rand()'s count and holdrand always; in the front end its probe line
/// (gFrontEndFrameStep, the active page, the transition's pages, count and time, mCounter) and EVENT_MANAGER's time; in a
/// level the event time and processed count, frame time, score, level, game random stream, and the player's position,
/// life and energy. <see cref="Line"/> is its trace line.
/// </summary>
public sealed record FlowSnapshot(long Present, FlowSegment Segment, uint CrtCalls, uint CrtHold, string? FrontEndLine,
    string? FrontEndTime, IReadOnlyDictionary<string, string>? LevelFields)
{
    public static FlowSnapshot Capture(GameRun run)
    {
        long p = run.Clock.PresentCount;
        var crt = run.Crt.MainThread;
        if (run.Input.Window.FrontEndRunning)
        {
            FrontEnd fe = run.FrontEnd;
            var b = new byte[28];
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(0), fe.Timing.FrameStep);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), (int)fe.ActivePage);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8), (int)fe.TransitionFrom);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(12), (int)fe.TransitionTo);
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(16), fe.TransitionCount);
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(20), fe.TransitionTime);
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(24), fe.Counter);
            return new(p, FlowSegment.FrontEnd, (uint)crt.Calls, crt.HoldRand, Hex(b), F32(fe.Events.Time), null);
        }

        if (run.Level is { Ended: false } level)
        {
            return new(p, FlowSegment.Level, (uint)crt.Calls, crt.HoldRand, null, null, LevelFieldsOf(level));
        }

        return new(p, FlowSegment.Boot, (uint)crt.Calls, crt.HoldRand, null, null, null);
    }

    /// <summary>The nine level probe fields the port writes (EVT EVN FT SC LVL RNG POS LIFE, and rand()'s as CRT).</summary>
    public static IReadOnlyDictionary<string, string> LevelFieldsOf(LevelSession session)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["EVT"] = F32(session.Context.Events.Time),
            ["EVN"] = U32(session.Context.Events.TotalEventsProcessed),
            ["FT"] = F32(session.Game.Loop.FrameTime),
            ["SC"] = U32(unchecked((uint)session.Game.Score)),
            ["LVL"] = U32(unchecked((uint)session.Game.CurrentLevel)),
            ["RNG"] = U32(unchecked((uint)session.Context.Random.Value)) + U32(unchecked((uint)session.Context.Random.Seed)),
        };
        if (session.Game.GetPlayer(0)?.BattleEngine is BattleEngine be)
        {
            fields["POS"] = F32(be.Pos.X) + F32(be.Pos.Y) + F32(be.Pos.Z);
            fields["LIFE"] = F32(be.Life) + F32(be.Energy);
        }

        return fields;
    }

    /// <summary>The trace line: the Present, its segment, rand(), and the segment's fields in a fixed order.</summary>
    public string Line()
    {
        string head = $"P {Present} {Segment} CRT {U32(CrtCalls)}{U32(CrtHold)}";
        return Segment switch
        {
            FlowSegment.FrontEnd => $"{head} EVT {FrontEndTime} FE {FrontEndLine}",
            FlowSegment.Level => head + string.Concat(LevelFields!.Select(kv => $" {kv.Key} {kv.Value}")),
            _ => head,
        };
    }

    private static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();

    private static string U32(uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        return Hex(b);
    }

    private static string F32(float v) => U32(BitConverter.SingleToUInt32Bits(v));
}
