// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OnslaughtRebuild.Parity;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Host;

/// <summary>
/// Explicitly requested, exclusive-create live-host diagnostics. Contains the existing seven ProbeCatalog values
/// plus state that could expose an extra render callback or input delivery. Floats are little-endian bit hex, never
/// rounded decimal strings. This selected-state trace is a falsifier, not a whole-game equivalence certificate.
/// No Godot dependency: the same recorder can compare an ordinary headless LevelSession.
/// </summary>
public sealed class LiveHostTrace : IDisposable
{
    private readonly StreamWriter _writer;
    private long _lastPresent = -1;
    private int _records;
    private bool _disposed;

    public LiveHostTrace(string path, bool drawing, bool replay)
    {
        _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false)) { AutoFlush = true };
        _writer.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "header",
            format = "onslaught-live-host-1",
            level = 100,
            drawing,
            replay,
            silent_virtual_voice = true,
            fixed_present_dt_le = Hex64(unchecked((ulong)BitConverter.DoubleToInt64Bits(PlatformClock.ReplayDt))),
            values = "little-endian bit hex; after core Present and optional host presentation, before next input",
        }));
    }

    public void Write(LevelSession session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (session.PresentCount == _lastPresent)
        {
            return;
        }

        if (session.PresentCount < _lastPresent)
        {
            throw new InvalidOperationException("live trace Presents must increase");
        }

        var probes = new ProbeTrace();
        long present = session.PresentCount;
        void Probe(string name, string hex) => probes.Add(present, name, hex == "null" ? [] : Convert.FromHexString(hex));
        Probe(ProbeCatalog.EventTime, Floats(session.Context.Events.Time));
        Probe(ProbeCatalog.FrameTime, Floats(session.Game.Loop.FrameTime));
        Probe(ProbeCatalog.Score, Hex32(unchecked((uint)session.Game.Score)));
        Probe(ProbeCatalog.Level, Hex32(unchecked((uint)session.Game.CurrentLevel)));
        Probe(ProbeCatalog.Random, Hex32(unchecked((uint)session.Context.Random.Value)) + Hex32(unchecked((uint)session.Context.Random.Seed)));
        BattleEngine? be = session.Game.GetPlayer(0)?.BattleEngine as BattleEngine;
        Probe(ProbeCatalog.Position, be is null ? "null" : Vector(be.Pos));
        Probe(ProbeCatalog.LifeEnergy, be is null ? "null" : Floats(be.Life, be.Energy));
        var particles = session.Context.Particles?.Manager;
        _writer.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "present",
            present,
            probes = probes.At(present)!.ToDictionary(pair => pair.Key,
                pair => pair.Value.Length == 0 ? "null" : Convert.ToHexString(pair.Value).ToLowerInvariant()),
            crt_calls = session.Context.Crt.Calls,
            crt_hold_le = Hex32(session.Context.Crt.HoldRand),
            game_rng_draws = session.Context.Random.Draws,
            be_velocity_le = be is null ? "null" : Vector(be.Velocity),
            be_orientation_le = be is null ? "null" : Matrix(be.Orientation),
            be_mode = be is null ? -1 : (int)be.State,
            be_engine_mode = be?.EngineState ?? -1,
            game_state = (int)session.Game.State,
            quit = (int)session.Game.Quit,
            session.Ended,
            session.Game.Paused,
            game_updates = session.Game.UpdateFrameNumber,
            frame_fraction_le = Floats(session.Game.Loop.Fraction, session.Game.Loop.Fraction2),
            event_frame = session.Context.Events.FrameCount,
            event_pending = session.Context.Events.TotalEvents,
            event_processed = session.Context.Events.TotalEventsProcessed,
            particle_frame = particles?.Frame ?? -1,
            particle_allocations = particles?.Allocations ?? -1,
            particle_live = particles?.NumLiveParticles ?? -1,
            particle_sprites = particles?.State.SpriteCount ?? -1,
            particle_time_le = particles is null ? "null" : Floats(particles.Time, particles.FrameLength),
            particle_noise_initialized = particles?.State.Noise is not null,
            particle_rng_calls = particles?.Rand.Calls ?? -1,
            particle_rng_hold_le = particles is null ? "null" : Hex32(particles.Rand.HoldRand),
        }));
        _lastPresent = present;
        _records++;
    }

    private static string Vector(FVector value) => Floats(value.X, value.Y, value.Z);
    private static string Matrix(FMatrix value) => Vector(value.Row0) + Vector(value.Row1) + Vector(value.Row2);
    private static string Floats(params float[] values) => string.Concat(values.Select(v => Hex32(BitConverter.SingleToUInt32Bits(v))));

    private static string Hex32(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Hex64(ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writer.WriteLine(JsonSerializer.Serialize(new { kind = "closed", records = _records, last_present = _lastPresent }));
        _writer.Dispose();
    }
}
