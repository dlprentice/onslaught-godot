// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// CPCSoundManager Play00517790, Resume00517960, Pause00517990, Stop005179b0, Update00517ae0.
// The concrete device contract is bea-decomp port/dsound/dsound.cpp, not arbitrary retail sound hardware.
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>
/// Explicit single-player virtual-port profile. The port's 12 sound channels plus separately owned music/voice
/// streams must remain below its 256-voice device limit. This adapter admits successful secondary-buffer allocation;
/// it does not invent device loss, allocation failures or another owner's Doppler behavior.
/// </summary>
public readonly record struct VirtualSoundBackendProfile(int SoundRate, int Channels, bool ContinuousRate, bool FlipSpeakers);

public readonly record struct SoundCameraState(FVector Position, FVector OldPosition, FMatrix Orientation);
public readonly record struct SoundOwnerSpatialState(FVector Position, FVector Velocity);

/// <summary>A diagnostic observation polls the virtual device, just like DirectSound GetStatus/GetCurrentPosition.</summary>
public readonly record struct VirtualSoundChannelState(uint Status, uint PlayCursor, uint WriteCursor, uint Frequency,
    int Volume, FVector Position, FVector PendingPosition);

/// <summary>
/// Concrete silent backend for the shared retail sound-event owner. Buffer cursors use the existing virtual mix
/// clock; no wall-clock, platform-clock reads, sample-duration timer, audio device or PCM renderer lives here.
/// Camera and original sample-buffer availability come from the host. A spatial owner must explicitly resolve to
/// a proven zero sound velocity: moving objects may qualify, but unknown/nonzero sound velocity is unsupported.
/// </summary>
public sealed partial class VirtualSoundEventBackend : ISoundEventBackend
{
    private sealed class Channel(VirtualSoundBuffer buffer)
    {
        public VirtualSoundBuffer Buffer { get; } = buffer;
        public int Volume;
        public FVector Position, PendingPosition;
        public bool PositionDirty;
    }

    private readonly SoundEffects _sound;
    private readonly VirtualSoundBackendProfile _profile;
    private readonly Func<long> _blocksMixed;
    private readonly Func<SampleMetadata, bool> _sampleHasBuffer;
    private readonly Func<SoundCameraState?> _camera;
    private readonly Func<object, SoundOwnerSpatialState?> _ownerSpatial;
    private readonly Channel?[] _channels;

    public VirtualSoundEventBackend(SoundEffects sound, VirtualSoundBackendProfile profile, Func<long> blocksMixed,
        Func<SampleMetadata, bool> sampleHasBuffer, Func<SoundCameraState?> camera,
        Func<object, SoundOwnerSpatialState?> ownerSpatial)
    {
        ArgumentNullException.ThrowIfNull(sound); ArgumentNullException.ThrowIfNull(blocksMixed);
        ArgumentNullException.ThrowIfNull(sampleHasBuffer); ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(ownerSpatial);
        if (profile.Channels is < 0 or > 64) throw new ArgumentOutOfRangeException(nameof(profile), "CPCSoundManager has 64 channel slots.");
        _sound = sound; _profile = profile; _blocksMixed = blocksMixed; _sampleHasBuffer = sampleHasBuffer;
        _camera = camera; _ownerSpatial = ownerSpatial; _channels = new Channel?[profile.Channels];
    }

    /// <summary>Pass this same SoundRate to ConfigureEventKernel; the event and buffer owners share one format selection.</summary>
    public VirtualSoundBackendProfile Profile => _profile;
    public int AvailableChannels => _channels.Length;
    public SoundCameraState CameraSnapshot { get; private set; } = new(default, default, FMatrix.Identity);
    private uint SampleRate => _profile.SoundRate switch { 0 => 44100, 1 => 22050, _ => 11025 };
    private Channel? GetChannel(int channel)
    {
        if ((uint)channel >= (uint)_channels.Length) throw new ArgumentOutOfRangeException(nameof(channel));
        return _channels[channel];
    }
    private void CheckOwner(SoundEffects.SoundEvent e)
    {
        if (!ReferenceEquals(e.Manager, _sound)) throw new ArgumentException("The event belongs to another manager.", nameof(e));
    }

    public bool HasBuffer(int channel) => GetChannel(channel) is not null;

    public VirtualSoundChannelState? InspectChannel(int channel)
    {
        Channel? c = GetChannel(channel);
        if (c is null) return null;
        uint status = c.Buffer.GetStatus(); var cursor = c.Buffer.GetCurrentPosition();
        return new(status, cursor.Play, cursor.Write, c.Buffer.Frequency, c.Volume, c.Position, c.PendingPosition);
    }

    public void PrepareStatus() => CameraSnapshot = _camera() ?? new(default, default, FMatrix.Identity);

    // Start004e0cf1 reads the retained event position BEFORE UpdateSoundPosition replaces it.
    public void PrepareStart(SoundEffects.SoundEvent e)
    {
        CheckOwner(e);
        e.Pan = e.Owner is not null && !e.IgnoreOwnerPosition ? CalculatePan(e.Position) : 0;
    }

    private static int CalculatePan(FVector position)
    {
        float sine = (float)X87.Sin(X87.Atan2(position.X, position.Y));
        double p = X87.Mul(sine, 10); p = X87.Mul(p, p); p = X87.Mul(p, p);
        return X87.Fist(X87.Mul(p, sine > 0 ? 1 : -1));
    }

    /// <summary>UpdateSoundPosition004e1360, single camera; the actual sample type fallback is outside tracking0..3.</summary>
    public void UpdatePosition(SoundEffects.SoundEvent e, bool first)
    {
        CheckOwner(e);
        if (e.Tracking is < SoundTracking.None or > SoundTracking.FollowDontDie)
            throw new NotSupportedException("Only the four declared retail sound-tracking modes are admitted.");
        SoundCameraState camera = _camera() ?? new(default, default, FMatrix.Identity);
        if (e.Tracking != SoundTracking.InitialPosition || first)
        {
            if (e.Owner is { } owner && !e.IgnoreOwnerPosition)
            {
                SoundOwnerSpatialState spatial = _ownerSpatial(owner)
                    ?? throw new NotSupportedException("The audible owner's position/zero-velocity contract is not established.");
                if (spatial.Velocity.X != 0 || spatial.Velocity.Y != 0 || spatial.Velocity.Z != 0)
                    throw new NotSupportedException("Virtual sound cursor parity has not admitted nonzero source Doppler.");
                FVector d = spatial.Position - camera.Position;
                FMatrix t = camera.Orientation.Transpose();
                // This inline copy is (Y*Y + Z*Z) + X*X, rather than FMatrix.Transform's call-site order.
                float Dot(FVector r) => (float)X87.Add(X87.Add(X87.Mul(r.Y, d.Y), X87.Mul(r.Z, d.Z)), X87.Mul(r.X, d.X));
                FVector position = new(Dot(t.Row0), Dot(t.Row1), Dot(t.Row2));
                if (position.X is > 2048 or < -2048 || position.Y is > 2048 or < -2048 || position.Z is > 2048 or < -2048)
                    throw new NotSupportedException("The retail positional assertion would fire outside ±2048 camera-local units.");
                e.Position = position; e.Velocity = spatial.Velocity;
            }
            else if (first || e.Tracking == SoundTracking.None) { e.Position = default; e.Velocity = default; }
        }
        if (_profile.FlipSpeakers) { FVector position = e.Position; position.X = -position.X; e.Position = position; }
        if (e.Owner is not null && !e.IgnoreOwnerPosition && e.Tracking is SoundTracking.FollowAndDie or SoundTracking.FollowDontDie)
            e.Pan = CalculatePan(e.Position);
    }

    // Four original inline copies reassociate the distance and gain products differently. Preserve each site:
    // Start004e0d97, Fade004e1ed1, TrackedOwner004e207e, MasterVolume004e0300.
    private static double Distance(FVector p, SoundVolumeSite site)
    {
        double xx = X87.Mul(p.X, p.X), yy = X87.Mul(p.Y, p.Y), zz = X87.Mul(p.Z, p.Z);
        double sum = site == SoundVolumeSite.Fade ? X87.Add(X87.Add(xx, yy), zz)
            : site == SoundVolumeSite.TrackedOwner ? X87.Add(X87.Add(xx, zz), yy) : X87.Add(X87.Add(zz, yy), xx);
        return X87.Sqrt(sum);
    }
    public double Distance(SoundEffects.SoundEvent e) { CheckOwner(e); return Distance(e.Position, SoundVolumeSite.Start); }

    private static int ConvertGain(double value)
    {
        int scaled = unchecked(X87.Fist(value) * 200);
        if (scaled > 10000) scaled = 10000;
        return Math.Max(unchecked(scaled - 10000) / 2, -10000);
    }

    public (int Volume, int Attenuated) EvaluateVolumes(SoundEffects.SoundEvent e, SoundVolumeSite site)
    {
        CheckOwner(e);
        double distance = Distance(e.Position, site);
        int attenuation = site != SoundVolumeSite.TrackedOwner && e.Tracking == SoundTracking.None ? 127
            : X87.Fist(X87.Mul(X87.Mul(Math.Clamp(X87.Sub(50, distance), 0, 50), 100), .02f));
        double q = X87.Mul(e.MasterVolume, e.SubVolume), global = _sound.MasterVolume;
        double kind = e.Kind == SoundKind.Game ? _sound.GameSoundsMasterVolume : _sound.MenuSoundsMasterVolume;
        double full = site == SoundVolumeSite.Start ? X87.Mul(X87.Mul(X87.Mul(q, kind), global), 127)
            : X87.Mul(X87.Mul(X87.Mul(kind, global), q), 127);
        double attenuated = site == SoundVolumeSite.Start ? X87.Mul(X87.Mul(X87.Mul(attenuation, kind), q), global)
            : X87.Mul(X87.Mul(X87.Mul(attenuation, kind), global), q);
        int volume = ConvertGain(full), attenuatedVolume = ConvertGain(attenuated);
        if (site == SoundVolumeSite.Fade && e.Tracking == SoundTracking.None) attenuatedVolume = volume;
        return (volume, attenuatedVolume);
    }

    // The native listener is stationary at the camera-local origin. Its orientation/rolloff affect mixed amplitude,
    // not these zero-Doppler cursors. SetAllParameters and CommitDeferredSettings both catch up BEFORE applying.
    private void CatchUpAll()
    { foreach (Channel? c in _channels) if (c is not null) _ = c.Buffer.GetStatus(); }
    public void UpdateGlobals() { if (_sound.Initialised) CatchUpAll(); }
    public void UpdatesDone()
    {
        if (!_sound.Initialised) return;
        CatchUpAll();
        foreach (Channel? c in _channels)
            if (c is { PositionDirty: true }) { c.Position = c.PendingPosition; c.PositionDirty = false; }
    }

    public bool Play(SoundEffects.SoundEvent e)
    {
        CheckOwner(e);
        int from = unchecked(X87.Fist(X87.Mul(X87.Add(e.StartPoint, e.Time), SampleRate)) * 2);
        int to = e.EndPoint == -1 ? unchecked((int)e.Sample.OriginalPcmBytes)
            : unchecked(X87.Fist(X87.Mul(SampleRate, e.EndPoint)) * 2);
        // These early returns retain an old channel buffer and do not change the event's Playing flag.
        if (from >= to || e.Sample.OriginalPcmBytes == 0 || !_sampleHasBuffer(e.Sample)) return false;
        if (!SampleBufferFormat.TryCreate(e.Sample.OriginalPcmBytes, _profile.SoundRate, out var format))
            throw new NotSupportedException("The admitted source sample has no supported virtual-port buffer format.");
        Channel? old = GetChannel(e.Channel);
        if (old is not null) old.Buffer.Stop(); // Release of the last duplicate reference halts it in the port.
        var channel = new Channel(new VirtualSoundBuffer(format, _blocksMixed));
        _channels[e.Channel] = channel;
        SetVolume(channel, 0);
        // QueryInterface/initial min/max-distance changes are admitted by this port profile. Their catch-up has no
        // source-time effect while this freshly duplicated buffer is stopped; no other event ownership is changed.
        _ = channel.Buffer.GetStatus();
        UpdatePosition(e, false);
        _ = Update(e, first: true);
        channel.Buffer.SetCurrentPosition(unchecked((uint)from));
        channel.Buffer.Play(e.Looping);
        return true; // Native writes Playing=true even if the device's Play HRESULT reports failure.
    }

    public void Stop(SoundEffects.SoundEvent e)
    {
        CheckOwner(e);
        Channel? c = GetChannel(e.Channel);
        if (c is not null) { c.Buffer.Stop(); _channels[e.Channel] = null; }
    }
    public void Pause(SoundEffects.SoundEvent e) { CheckOwner(e); GetChannel(e.Channel)?.Buffer.Stop(); }
    public void Resume(SoundEffects.SoundEvent e) { CheckOwner(e); GetChannel(e.Channel)?.Buffer.Play(e.Looping); }
    public bool Update(SoundEffects.SoundEvent e) => Update(e, first: false);

    private static void SetVolume(Channel c, int volume)
    {
        _ = c.Buffer.GetStatus(); // Port SetVolume catches up even when validation then refuses the value.
        if (volume is >= -10000 and <= 0) c.Volume = volume;
    }

    private bool Update(SoundEffects.SoundEvent e, bool first)
    {
        CheckOwner(e);
        Channel? c = GetChannel(e.Channel);
        if (c is null) return false;
        if (e.Velocity.X != 0 || e.Velocity.Y != 0 || e.Velocity.Z != 0)
            throw new NotSupportedException("Virtual sound cursor parity has not admitted nonzero source Doppler.");
        _ = c.Buffer.GetStatus(); // SetPosition/SetVelocity/SetMaxDistance each catch up before changing parameters.
        c.PendingPosition = e.Position;
        if (first) c.Position = e.Position; else c.PositionDirty = true;
        int volume = e.CurrentVolume;
        if (volume < -4000) volume = unchecked(volume + (volume + 4000) * 2);
        SetVolume(c, volume);
        if (_profile.ContinuousRate)
            _ = c.Buffer.TrySetFrequency(unchecked((uint)X87.Fist(X87.Mul(SampleRate, e.Pitch))));
        return !first && !e.Paused && (c.Buffer.GetStatus() & 1) == 0;
    }
}
