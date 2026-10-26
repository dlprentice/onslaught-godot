// SPDX-License-Identifier: GPL-3.0-only
// Authored synchronous device controls. Buffers have actual authored PCM and explicit status inputs, not duration
// projections, hardware-playback observations, or ordinary-player allocator defaults.
using System.Reflection;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;
using SoundOwner = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SoundBufferDeviceTests
{
    private sealed class Device : ISoundBufferDevice
    {
        public readonly Dictionary<SampleMetadata, SamplePcmData> Sources = new(ReferenceEqualityComparer.Instance);
        public readonly List<Playback> Buffers = [];
        public readonly List<string> Calls = [];
        public bool FailDuplicate, FailPlay;
        public SoundBufferToken? WrongDuplicateToken;
        public SoundListenerParameters? Listener;
        public ISoundPlaybackBuffer? Duplicate(SampleMetadata sample, SampleBufferFormat format, SoundBufferToken token)
        {
            Calls.Add($"duplicate:{token.Generation}");
            if (FailDuplicate || !Sources.TryGetValue(sample, out var source) || !source.FullyInitialized || source.Format != format)
                return null;
            var buffer = new Playback(this, format, WrongDuplicateToken ?? token) { FailPlay = FailPlay };
            Buffers.Add(buffer); return buffer;
        }
        public void SetListener(SoundListenerParameters parameters) { Listener = parameters; Calls.Add("listener"); }
        public void CommitDeferredSettings()
        {
            Calls.Add("commit");
            foreach (var buffer in Buffers.Where(b => !b.Released)) buffer.Position = buffer.Pending;
        }
    }

    private sealed class Playback(Device owner, SampleBufferFormat format, SoundBufferToken token) : ISoundPlaybackBuffer
    {
        public SoundBufferToken Token { get; set; } = token; // Mutable only to inject a broken-device counterexample.
        public SoundBufferToken? StatusToken, CursorToken;
        public uint Flags, Cursor;
        public uint Frequency { get; private set; } = format.SampleRate;
        public bool Released, FailPlay;
        public FVector Position, Pending;
        public SoundBufferObservation GetStatus()
        { owner.Calls.Add($"status:{Token.Generation}"); return new(StatusToken ?? Token, Flags); }
        public SoundBufferCursor GetCurrentPosition() => new(CursorToken ?? Token, Cursor, Cursor);
        public bool SetVolume(int volume) { owner.Calls.Add($"volume:{volume}"); return volume is >= -10000 and <= 0; }
        public bool SetSpatial(FVector position, bool deferred)
        { owner.Calls.Add(deferred ? "spatial:deferred" : "spatial:immediate"); Pending = position; if (!deferred) Position = position; return true; }
        public bool TrySetFrequency(uint frequency)
        {
            if (frequency != 0 && frequency is < 100 or > 200000) return false;
            Frequency = frequency == 0 ? format.SampleRate : frequency; return true;
        }
        public void SetCurrentPosition(uint byteOffset) { Cursor = byteOffset % format.BufferBytes; owner.Calls.Add("cursor"); }
        public bool Play(bool looping)
        { owner.Calls.Add("play"); if (!FailPlay) Flags = looping ? 5u : 1u; return !FailPlay; }
        public void Stop() { owner.Calls.Add("stop"); Flags = 0; }
        public void Release(bool stopFirst)
        { if (stopFirst) Stop(); owner.Calls.Add("release"); Flags = 0; Released = true; }
        public void Finish() => Flags = 0; // Device observation only; never calls the event owner.
    }

    private sealed class Fixture
    {
        public readonly Device Device = new();
        public readonly SoundEffects Sounds = new();
        public readonly PlatformClock Clock = new();
        public readonly SampleMetadata Sample = new("authored", 6944);
        public readonly SoundProcess Process;
        public Fixture(bool addPcm = true, int channels = 12)
        {
            if (addPcm) Device.Sources.Add(Sample, SamplePcm.ConvertPcm16(new byte[6944], 1, 6944));
            Process = new(Sounds, Clock, new SoundDeviceProcessOptions(new(1, channels, true, false),
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), (_, _) => Sample,
                sample => Device.Sources.ContainsKey(sample), _ => new(default, default), Device));
            Clock.PresentDevice(); Clock.GetSysTimeFloat(); Process.InitialiseAfterFirstPresent(.8f);
            Device.Calls.Clear();
        }
        public SoundEffects.SoundEvent Play(SoundOwner? owner = null, Action<SoundOwner, SoundEffects.SoundEvent>? completed = null) =>
            Sounds.PlaySample(Sample, owner, new SoundPlayback(IgnoreOwnerPosition: true, InformOwner: completed is not null), completed)!;
        public Playback Last => Device.Buffers[^1];
    }

    [Fact]
    public void ExternalAdmissionNeverReturnsAFakeVirtualBackendOrAcceptsHeaderOnlyVoice()
    {
        var f = new Fixture();
        Assert.False(f.Process.UsesVirtualBuffers); Assert.Equal(1, f.Process.SoundRate);
        Assert.Throws<InvalidOperationException>(() => f.Process.Backend);
        Assert.IsType<PcSoundEventBackend>(f.Process.DeviceBackend);
        Assert.Throws<NotSupportedException>(() => new VirtualVoice(f.Clock, _ => throw new Exception()).BindSoundProcess(f.Process));
        Assert.Empty(f.Device.Buffers); Assert.Empty(f.Sounds.Events);
        f.Clock.Present(); Assert.Equal(0x3d088880u, BitConverter.SingleToUInt32Bits(f.Clock.GetSysTimeFloat()));
    }

    [Fact]
    public void ConfiguredProcessStillRequiresEveryExplicitPoolSeedAndUnusedOwner()
    {
        var f = new Fixture();
        var options = new SoundDeviceProcessOptions(new(1, 12, true, false), [], (_, _) => null, _ => false, _ => null, f.Device);
        Assert.Throws<ArgumentException>(() => new SoundProcess(new(), new(), options));
        Assert.Throws<InvalidOperationException>(() => new SoundProcess(f.Sounds, f.Clock,
            options with { Seeds = Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray() }));
        Assert.Empty(f.Device.Buffers);
    }

    [Fact]
    public void SamePresentStartAndStoppedStatusUseTheExistingCompletionOwner()
    {
        var f = new Fixture(); var owner = new SoundOwner(); int completions = 0;
        var e = f.Play(owner, (_, _) => completions++);
        Assert.True(e.Playing); Assert.Equal(1u, f.Last.Flags); Assert.Equal(1, f.Clock.PresentCount);
        f.Sounds.UpdateStatus(); Assert.True(e.Playing); Assert.Equal(0, completions);
        f.Last.Finish(); Assert.True(e.Playing); Assert.Equal(0, completions);
        f.Sounds.UpdateStatus(); Assert.False(e.Playing); Assert.Equal(1, completions);
        Assert.Empty(f.Sounds.Events); Assert.True(f.Last.Released); Assert.Equal(1, f.Clock.PresentCount);
    }

    [Fact]
    public void MissingPcmAndDuplicateFailureAreNotAdmittedPlaybackEvenThoughNativeLogicalFlagSurvives()
    {
        var missing = new Fixture(addPcm: false); var a = missing.Play();
        Assert.True(a.Playing); // Start's native logical write precedes platform admission; not audible proof.
        Assert.False(missing.Process.DeviceBackend.HasBuffer(a.Channel)); Assert.Empty(missing.Device.Buffers);
        Assert.DoesNotContain(missing.Device.Calls, c => c.StartsWith("duplicate:"));
        var refused = new Fixture(); refused.Device.FailDuplicate = true; var b = refused.Play();
        Assert.True(b.Playing); Assert.False(refused.Process.DeviceBackend.HasBuffer(b.Channel));
        Assert.Empty(refused.Device.Buffers); Assert.DoesNotContain("play", refused.Device.Calls);
    }

    [Fact]
    public void NativeIgnoresPlayHresultButNextStatusCanCompleteTheEvent()
    {
        var f = new Fixture(); f.Device.FailPlay = true;
        var e = f.Play(); Assert.True(e.Playing); Assert.Equal(0u, f.Last.Flags);
        Assert.True(f.Process.DeviceBackend.HasBuffer(e.Channel));
        f.Sounds.UpdateStatus(); Assert.False(e.Playing); Assert.Empty(f.Sounds.Events);
    }

    [Fact]
    public void PauseKeepsBufferAndCursorAndCompletionPrecedesExplicitStopAndRelease()
    {
        var f = new Fixture(); var owner = new SoundOwner(); SoundEffects.SoundEvent? observed = null;
        var e = f.Play(owner, (actual, current) =>
        {
            Assert.Same(owner, actual); Assert.True(current.Playing); Assert.Same(owner, current.Owner);
            Assert.False(f.Last.Released); Assert.True(f.Process.DeviceBackend.HasBuffer(current.Channel));
            observed = current; f.Device.Calls.Add("owner-completed");
        });
        f.Last.SetCurrentPosition(1234); f.Sounds.PauseAllSamples();
        f.Sounds.UpdateStatus(); Assert.True(e.Playing); Assert.True(e.Paused); Assert.Null(observed);
        Assert.Equal(1234u, f.Last.GetCurrentPosition().Play); Assert.False(f.Last.Released);
        f.Sounds.UnPauseAllSamples(); Assert.False(e.Paused); Assert.Equal(1u, f.Last.Flags);
        f.Device.Calls.Clear(); f.Sounds.StopSoundEvent(e);
        Assert.Equal(new[] { "owner-completed", "stop", "release" }, f.Device.Calls);
        Assert.Same(e, observed); Assert.Null(e.Owner); Assert.False(e.Playing);
    }

    [Fact]
    public void ReplacementOwnsANewGenerationAndStaleStatusCannotReclaimIt()
    {
        var f = new Fixture(); var e = f.Play(); var old = f.Last;
        Assert.True(f.Process.DeviceBackend.Play(e)); var current = f.Last;
        Assert.True(old.Released); Assert.NotEqual(old.Token, current.Token);
        old.Finish(); Assert.False(f.Process.DeviceBackend.Update(e)); Assert.True(e.Playing);
        current.StatusToken = old.Token;
        Assert.Throws<InvalidDataException>(() => f.Sounds.UpdateStatus());
        Assert.True(e.Playing); Assert.False(current.Released); Assert.True(f.Process.DeviceBackend.HasBuffer(e.Channel));
        current.StatusToken = null; current.Finish(); f.Sounds.UpdateStatus(); Assert.False(e.Playing);
    }

    [Fact]
    public void StaleCursorAndMutatedBufferIdentityAreRefusedInsteadOfBeingMisattributed()
    {
        var f = new Fixture(); var e = f.Play(); var token = f.Last.Token;
        f.Last.CursorToken = token with { Generation = token.Generation + 1 };
        Assert.Throws<InvalidDataException>(() => f.Process.DeviceBackend.InspectChannel(e.Channel));
        f.Last.CursorToken = null; f.Last.Token = token with { Generation = token.Generation + 1 };
        Assert.Throws<InvalidDataException>(() => f.Sounds.UpdateStatus());
        Assert.True(e.Playing); Assert.False(f.Last.Released);
    }

    [Fact]
    public void DuplicateTokenMismatchAndGenerationExhaustionFailBeforeAnotherVoiceIsReused()
    {
        var wrong = new Fixture(); wrong.Device.WrongDuplicateToken = new(0, 777);
        Assert.Throws<InvalidDataException>(() => wrong.Play());
        Assert.False(wrong.Process.DeviceBackend.HasBuffer(0));
        var f = new Fixture(); var e = f.Play(); var old = f.Last;
        typeof(PcSoundEventBackend).GetField("_nextGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(f.Process.DeviceBackend, ulong.MaxValue);
        Assert.Throws<InvalidOperationException>(() => f.Process.DeviceBackend.Play(e));
        Assert.False(old.Released); Assert.Single(f.Device.Buffers); Assert.True(f.Process.DeviceBackend.HasBuffer(e.Channel));
    }

    [Fact]
    public void DeviceReceivesNativeDeferredPositionListenerAndGainValues()
    {
        var f = new Fixture(); var e = f.Play(); var backend = f.Process.DeviceBackend;
        e.Position = new(1, 2, 3); e.CurrentVolume = -6000;
        backend.Update(e); var pending = backend.InspectChannel(e.Channel)!.Value;
        Assert.Equal(new FVector(1, 2, 3), pending.PendingPosition); Assert.Equal(default, pending.Position);
        Assert.Equal(-10000, pending.Volume); Assert.Equal(default, f.Last.Position);
        backend.UpdateGlobals(); backend.UpdatesDone();
        Assert.Equal(new FVector(1, 2, 3), f.Last.Position); Assert.Equal(new FVector(1, 2, 3), backend.InspectChannel(e.Channel)!.Value.Position);
        Assert.Equal(new SoundListenerParameters(default, default, new(0, 1, 0), new(0, 0, -1), .7f, 1, 1), f.Device.Listener);
    }

    [Fact]
    public void InjectedDeviceDoesNotChangeNativeEffectRngAdmissionOrder()
    {
        var f = new Fixture(); var random = new CrtRandStream(); var effect = new SoundEffect("warning", 0, "authored");
        var owner = new SoundOwner();
        f.Sounds.PlayEffect(effect, owner, random, new SoundPlayback(IgnoreOwnerPosition: true, Once: true));
        long calls = random.Calls; Assert.Equal(1, calls);
        f.Sounds.PlayEffect(effect, owner, random, new SoundPlayback(IgnoreOwnerPosition: true, Once: true));
        Assert.Equal(calls + 1, random.Calls); Assert.Single(f.Device.Buffers);
    }

    private sealed class BoundVoice : ISoundProcessVoice
    {
        public SoundProcess? Bound;
        public bool SoundInitialised => Bound?.Initialised == true;
        public bool LoaderBusy => false;
        public bool HasLoadedData => false;
        public void BindSoundProcess(SoundProcess process) => Bound = process;
        public void StartLoad(string path) => throw new NotSupportedException("This caller fixture supplies no voice PCM.");
        public void PlayLoaded(ScriptMessage? message) => throw new NotSupportedException("This caller fixture plays no voice.");
        public void KillVoice() { }
        public void UpdateStatus() => throw new InvalidOperationException("The shared manager owns completion.");
    }

    [GameDataFact]
    public void ActualLevelLoaderBindsAnExplicitDeviceVoiceThroughTheSharedContract()
    {
        var f = new Fixture(); var input = new PlatformInput(new PlatformWindow());
        var loading = new LoadingScreen(f.Clock, () => input.Process(f.Clock.QueryPerformanceCounter()));
        var career = OnslaughtRebuild.Parity.Career.Career.Constructed();
        var host = new LevelHost(f.Clock, input, ControlBindings.Defaults(), new ControllerShared(), new CrtRandStream(),
            new OnslaughtRebuild.Parity.Career.EndLevelData(), LevelSession.SlotsOf(career), loading)
            { Career = career, Sounds = f.Sounds, SoundProcess = f.Process };
        var voice = new BoundVoice();
        LevelSession session = LevelSession.Load(100, GameData.Read, _ => voice, host: host);
        Assert.Same(f.Process, voice.Bound); Assert.Same(f.Process, session.SoundProcess);
        Assert.Same(f.Sounds, session.Context.SoundEffects); Assert.True(voice.SoundInitialised);
        Assert.False(f.Process.UsesVirtualBuffers);
        // This is the genuine Load binding, not a playback/complete-level claim. Detach this fixture's camera owner.
        f.Process.ReleaseLevel(session.Context);
    }
}
