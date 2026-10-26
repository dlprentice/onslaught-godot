// SPDX-License-Identifier: GPL-3.0-only
// Authored boundaries with the real adapter/shared event owner; not actual Godot/device acceptance.
// Godot 4d10419cacd6d3009976e8f9fd3abd970fd58018: AudioStreamPlayer3D::is_playing delegates to
// AudioStreamPlayerInternal::is_playing, then AudioServer::is_playback_active. For streamed playback,
// PAUSED/FADE_OUT_TO_PAUSE is not PLAYING. Its paused getter distinguishes these from a natural end.
using OnslaughtRebuild.Host;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class LiveAudioSuspensionTests
{
    private sealed class Player : ILiveAudioPlayer
    {
        public bool Active, Disposed;
        public int PlayCalls, Disposals;
        public float Started;
        public bool Playing => Active && !Paused;
        public float PlaybackPosition => .03125f;
        public bool Paused { get; set; }
        public void Play(float fromSeconds) { Started = fromSeconds; Active = true; PlayCalls++; }
        public void Stop() => Active = false;
        public void Dispose() { Active = false; Disposed = true; Disposals++; }
        public void SetLooping(bool looping) { }
        public void SetPitch(float pitch) { }
        public void SetVolume(float decibels, bool muted) { }
        public void SetPosition(FVector position) { }
    }
    private sealed class RecordingDevice(LiveSoundDevice device) : ISoundBufferDevice
    {
        public ISoundPlaybackBuffer Last = null!;
        public ISoundPlaybackBuffer? Duplicate(SampleMetadata sample, SampleBufferFormat format, SoundBufferToken token) =>
            Last = device.Duplicate(sample, format, token)!;
        public void SetListener(SoundListenerParameters parameters) => device.SetListener(parameters);
        public void CommitDeferredSettings() => device.CommitDeferredSettings();
    }
    private sealed class Fixture : IDisposable
    {
        public readonly List<Player> Players = [];
        public readonly LiveSoundDevice Device;
        public readonly RecordingDevice Recording;
        public readonly SoundProcess Process;
        public readonly PlatformClock Clock = new();
        public readonly SampleMetadata Sample = new("authored-zero-PCM", 8820);
        public ScriptMessage Message = null!;
        public int Polls, Callbacks;
        public Fixture()
        {
            var pcm = SamplePcm.ConvertPcm16(new byte[8820], 1, 8820);
            Device = new(_ => pcm, _ => { var player = new Player(); Players.Add(player); return player; });
            Recording = new(Device);
            Process = new(new SoundEffects(), Clock, new SoundDeviceProcessOptions(new(1, 12, true, false),
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), (_, _) => Sample,
                _ => true, _ => new(default, default), Recording));
            Clock.PresentDevice(); Clock.GetSysTimeFloat(); Process.InitialiseAfterFirstPresent(.8f);
        }
        public void Start(bool looping = false)
        {
            Message = new(null, "authored owner", 1, 0, null, null, 0);
            Process.Effects.PlaySample(Sample, Message, new SoundPlayback(Repeat: looping, InformOwner: true, IgnoreOwnerPosition: true),
                (owner, _) => { Callbacks++; ((ScriptMessage)owner).SampleFinishedPlaying(); });
        }
        public Player Player => Players[^1];
        public uint Flags => Recording.Last.GetStatus().Flags;
        public void Poll() { Polls++; Process.Effects.UpdateStatus(); }
        public void Dispose() => Device.Dispose();
    }

    [Theory]
    [InlineData(false, 1u)]
    [InlineData(true, 5u)]
    public void PausedStatusReadPreservesResumeAndDoesNotCompleteOwner(bool looping, uint flags)
    {
        using var f = new Fixture(); f.Start(looping); f.Poll(); f.Poll();
        Assert.Equal(flags, f.Flags); int pollsBefore = f.Polls;
        f.Device.SetSuspended(true);
        Assert.True(f.Player.Active); Assert.True(f.Player.Paused); Assert.False(f.Player.Playing);
        for (int i = 0; i < 3; i++) Assert.Equal(0u, f.Flags);
        Assert.False(f.Message.FinishedPlaying); Assert.Equal(0, f.Callbacks);
        f.Device.SetSuspended(false);
        Assert.Equal(pollsBefore, f.Polls); Assert.False(f.Message.FinishedPlaying);
        Assert.Equal(flags, f.Flags); Assert.Equal(1, f.Player.PlayCalls);
        f.Poll(); Assert.False(f.Message.FinishedPlaying); Assert.Equal(0, f.Callbacks);
    }

    [Fact]
    public void SuspensionWithoutStatusReadKeepsSamePlayback()
    {
        using var f = new Fixture(); f.Start();
        f.Device.SetSuspended(true); f.Device.SetSuspended(false);
        Assert.Equal(1u, f.Flags); Assert.Equal(1, f.Player.PlayCalls);
        f.Poll(); Assert.False(f.Message.FinishedPlaying);
    }

    [Fact]
    public void BufferCreatedDuringSuspensionRetainsItsPlayRequestAfterStatusRead()
    {
        // This authored ILiveAudioPlayer retains Paused before Play. Godot's zero-playback setter
        // does not promise that persistence; this control is not new-player Godot acceptance.
        using var f = new Fixture(); f.Device.SetSuspended(true); f.Start();
        Assert.True(f.Player.Paused); Assert.Equal(0u, f.Flags);
        f.Device.SetSuspended(false); Assert.Equal(1u, f.Flags);
        f.Poll(); Assert.False(f.Message.FinishedPlaying);
    }

    [Fact]
    public void GenuineCompletionStillWaitsForSharedOwnerPollAndCannotResurrect()
    {
        using var f = new Fixture(); f.Start(); f.Player.Active = false;
        Assert.Equal(0u, f.Flags); Assert.False(f.Message.FinishedPlaying);
        f.Poll(); Assert.True(f.Message.FinishedPlaying); Assert.Equal(1, f.Callbacks);
        f.Device.SetSuspended(true); f.Device.SetSuspended(false); f.Poll();
        Assert.Equal(1, f.Callbacks); Assert.Equal(0, f.Device.BufferCount);
    }

    [Fact]
    public void NativePauseStillStopsAndResumesFromObservedCursor()
    {
        using var f = new Fixture(); f.Start(); f.Process.Effects.PauseAllSamples();
        Assert.Equal(0u, f.Flags); Assert.False(f.Player.Active); Assert.False(f.Message.FinishedPlaying);
        f.Poll(); Assert.False(f.Message.FinishedPlaying);
        f.Process.Effects.UnPauseAllSamples(); Assert.Equal(1u, f.Flags);
        Assert.Equal(2, f.Player.PlayCalls); Assert.Equal(689 / 22050f, f.Player.Started);
        f.Poll(); Assert.False(f.Message.FinishedPlaying);
    }

    [Fact]
    public void ExplicitStopIsNotUndoneByFocusResume()
    {
        using var f = new Fixture(); f.Start(); f.Recording.Last.Stop();
        f.Device.SetSuspended(true); Assert.Equal(0u, f.Flags);
        f.Device.SetSuspended(false); Assert.Equal(0u, f.Flags); Assert.False(f.Player.Active);
        f.Poll(); Assert.True(f.Message.FinishedPlaying); Assert.Equal(1, f.Callbacks);
    }

    [Fact]
    public void ReleasedGenerationAndIdempotentTeardownAreUnchanged()
    {
        using var f = new Fixture(); f.Start(); var old = f.Recording.Last;
        f.Process.Effects.KillAllSamples(); f.Poll(); f.Start();
        Assert.True(f.Recording.Last.Token.Generation > old.Token.Generation);
        Assert.Throws<ObjectDisposedException>(() => old.GetStatus()); Assert.Equal(1u, f.Flags);
        f.Device.Dispose(); Assert.Equal(0, f.Device.BufferCount);
        Assert.All(f.Players, player => { Assert.True(player.Disposed); Assert.Equal(1, player.Disposals); });
    }
}
