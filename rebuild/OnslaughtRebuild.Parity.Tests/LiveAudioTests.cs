// SPDX-License-Identifier: GPL-3.0-only
// Authored player observations test production adapter/kernel coupling, not real Godot playback or heard output.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using OnslaughtRebuild.Host;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class LiveAudioTests
{
    private static byte[] Bank(uint size = 8820)
    {
        byte[] bytes = new byte[80 + (size + 3) / 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        "effect"u8.CopyTo(bytes.AsSpan(12)); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), size);
        bytes.AsSpan(80).Fill(0x17); return bytes;
    }
    private static CareerOptions Options(int rate = 1) => CareerOptions.Read(new byte[CareerOptions.Size]) with { SoundRate = rate, Sound3DMethod = -1 };
    private sealed class Player : ILiveAudioPlayer
    {
        private bool _active;
        // The pinned Godot streamed player does not report Playing while StreamPaused is true.
        public bool Playing { get => _active && !Paused; set => _active = value; }
        public float PlaybackPosition { get; set; }
        public bool Paused { get; set; }
        public bool FailPlay, ThrowStop, Disposed, Looping;
        public float Started, Pitch, Db;
        public bool Muted;
        public FVector Position;
        public void Play(float from) { Started = from; Playing = !FailPlay; }
        public void Stop() { Playing = false; if (ThrowStop) throw new InvalidOperationException("authored release failure"); }
        public void SetLooping(bool value) => Looping = value;
        public void SetPitch(float value) => Pitch = value;
        public void SetVolume(float db, bool muted) { Db = db; Muted = muted; }
        public void SetPosition(FVector position) => Position = position;
        public void Dispose() { Disposed = true; Playing = false; }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly LiveSoundSources Sources = new(Bank(), Options());
        public readonly List<Player> Players = [];
        public readonly LiveSoundDevice Device;
        public readonly SoundProcess Process;
        public readonly PlatformClock Clock = new();
        public Fixture()
        {
            Device = new(Sources.Pcm, _ => { var p = new Player(); Players.Add(p); return p; });
            Process = new(new SoundEffects(), Clock, Sources.Options(Device));
            Clock.PresentDevice(); Clock.GetSysTimeFloat(); Process.InitialiseAfterFirstPresent(.8f);
        }
        public SampleMetadata Sample => Process.SampleLookup("effect", false)!;
        public SoundEffects.SoundEvent Play() => Process.Effects.PlaySample(Sample, null, new SoundPlayback())!;
        public ISoundPlaybackBuffer Duplicate(ulong generation = 1, int channel = 0) => Device.Duplicate(Sample,
            Sources.Pcm(Sample).Format, new(channel, generation))!;
        public void Dispose() => Device.Dispose();
    }
    private sealed class LibraryFixture : IDisposable
    {
        public const string VoicePath = @"data\sounds\english\MessageBox\authored.ogg";
        public readonly byte[] Ogg = [1, 2, 3, 4];
        public readonly byte[] Pcm = Enumerable.Range(0, 8820).Select(i => (byte)i).ToArray();
        public readonly string DirectoryPath = Path.Combine(AppContext.BaseDirectory, "local-data", "live-audio", Guid.NewGuid().ToString("N"));
        public JsonObject Manifest;
        public LibraryFixture()
        {
            Directory.CreateDirectory(DirectoryPath); File.WriteAllBytes(Path.Combine(DirectoryPath, "voice.pcm"), Pcm);
            Manifest = new() { ["schema"] = 1, ["contract"] = VoicePcmLibrary.Contract,
                ["decoder"] = new JsonObject { ["control_word"] = "027f", ["executable_sha256"] = Hash([1]),
                    ["library_sha256"] = Hash([2]), ["reader_sha256"] = Hash([3]) },
                ["voices"] = new JsonArray(new JsonObject { ["path"] = VoicePath, ["ogg_sha256"] = Hash(Ogg),
                    ["pcm_file"] = "voice.pcm", ["decoded_bytes"] = Pcm.Length, ["pcm_sha256"] = Hash(Pcm) }) };
        }
        public VoicePcmLibrary Load()
        {
            string manifest = Path.Combine(DirectoryPath, "voices.json"); File.WriteAllText(manifest, Manifest.ToJsonString());
            return new(manifest, _ => ("authored", Ogg));
        }
        public JsonObject Entry => (JsonObject)((JsonArray)Manifest["voices"]!)[0]!;
        public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        public void Dispose() { /* Small authored receipts remain in ignored test output. */ }
    }

    [Fact]
    public void ApprovedHostPolicyIsExplicitAndDoesNotBorrowAReplayProfile()
    {
        using var f = new Fixture(); var options = f.Sources.Options(f.Device);
        Assert.Equal(new VirtualSoundBackendProfile(1, 12, true, false), options.Profile);
        Assert.Equal(256, options.Seeds.Count); Assert.All(options.Seeds, s => Assert.Equal(new SoundEventSeed(-1, default), s));
        Assert.Equal(.8f, f.Process.Effects.MasterVolume); Assert.False(f.Process.Effects.PitchVarianceEnabled);
        Assert.Equal(1, new LiveSoundSources(Bank(), null).SoundRate);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void SelectedRateAndSpeakerFlipAreRetained(int rate)
    {
        var sources = new LiveSoundSources(Bank(), Options(rate) with { FlipSpeakers = 1 });
        Assert.Equal(rate, sources.SoundRate); Assert.True(sources.FlipSpeakers);
    }

    [Theory]
    [InlineData(-1)] [InlineData(3)]
    public void UnknownRateRefusesRatherThanNormalizing(int rate) =>
        Assert.Throws<NotSupportedException>(() => new LiveSoundSources(Bank(), Options(rate)));

    [Fact]
    public void HardwarePitchAndUnmappedDeviceModesRefuseBeforePlayingOrRandomSelection()
    {
        Assert.Throws<NotSupportedException>(() => new LiveSoundSources(Bank(), Options() with { HardwareSound = 1 }));
        Assert.Throws<NotSupportedException>(() => new LiveSoundSources(Bank(), Options() with { SoundDevice = 1 }));
        Assert.Throws<NotSupportedException>(() => new LiveSoundSources(Bank(), Options() with { Sound3DMethod = 0 }));
        using var f = new Fixture(); Assert.Empty(f.Players);
        f.Sources.CheckOptions(Options());
        Assert.Throws<NotSupportedException>(() => f.Sources.CheckOptions(Options(0)));
    }

    [Fact]
    public void RealPlayerObservationControlsCompletionRatherThanVoiceLengthOrPlayRequest()
    {
        using var f = new Fixture(); using var data = new LibraryFixture();
        var voice = new PcmMessageVoice(data.Load(), () => f.Sources); voice.BindSoundProcess(f.Process);
        var message = new ScriptMessage(null, "authored", 1, 0, null, null, 0);
        voice.StartLoad(LibraryFixture.VoicePath); Assert.True(voice.HasLoadedData); voice.PlayLoaded(message);
        Assert.False(message.FinishedPlaying); Assert.Single(f.Process.Effects.Events); Assert.Single(f.Players);
        for (int i = 0; i < 60; i++) { f.Clock.Present(); f.Process.Effects.UpdateStatus(); }
        Assert.False(message.FinishedPlaying); // Many nominal lengths elapsed; the actual player still reports active.
        f.Players[0].Playing = false;
        Assert.False(message.FinishedPlaying); // Device changes do not asynchronously call the owner.
        f.Process.Effects.UpdateStatus(); Assert.True(message.FinishedPlaying);
        Assert.Throws<InvalidOperationException>(() => voice.UpdateStatus());
    }

    [Fact]
    public void ReplacingVoiceKillsPreviousSampleBeforeInstallingTheNewIdentity()
    {
        using var f = new Fixture(); using var data = new LibraryFixture();
        var voice = new PcmMessageVoice(data.Load(), () => f.Sources); voice.BindSoundProcess(f.Process);
        var a = new ScriptMessage(null, "a", 1, 0, null, null, 0);
        voice.StartLoad(LibraryFixture.VoicePath); voice.PlayLoaded(a);
        voice.StartLoad(LibraryFixture.VoicePath); voice.PlayLoaded(null);
        Assert.True(a.FinishedPlaying); Assert.True(f.Players[0].Disposed); Assert.Equal(2, f.Players.Count);
        Assert.Single(f.Process.Effects.Events, e => e.Playing);
        // Native Kill stops now and returns its event to the pool only during the manager's deferred deletion pass.
        voice.KillVoice(); f.Process.Effects.UpdateStatus(); Assert.Empty(f.Process.Effects.Events);
        Assert.All(f.Players, p => Assert.True(p.Disposed));
    }

    [Fact]
    public void FailedActualPlayIsNotForgedIntoDevicePlaying()
    {
        using var f = new Fixture(); var buffer = f.Duplicate(); f.Players[0].FailPlay = true;
        Assert.False(buffer.Play(false)); Assert.Equal(0u, buffer.GetStatus().Flags);
    }

    [Fact]
    public void StopAndResumeUseObservedFrameAlignedCursorAndNoElapsedClock()
    {
        using var f = new Fixture(); var buffer = f.Duplicate();
        buffer.SetCurrentPosition(17); buffer.Play(false); Assert.Equal(8 / 22050f, f.Players[0].Started);
        f.Players[0].PlaybackPosition = .025f; buffer.Stop();
        Assert.Equal(0u, buffer.GetStatus().Flags); buffer.Play(true);
        Assert.Equal(551 / 22050f, f.Players[0].Started); Assert.Equal(5u, buffer.GetStatus().Flags);
        Assert.Throws<NotSupportedException>(() => buffer.GetCurrentPosition());
    }

    [Fact]
    public void PausedActualPlayerIsNotMistakenForPlayingAndFocusResumesBeforeCoreObservations()
    {
        using var f = new Fixture(); var buffer = f.Duplicate(); buffer.Play(true);
        f.Device.SetSuspended(true); Assert.False(f.Players[0].Playing); Assert.Equal(0u, buffer.GetStatus().Flags);
        f.Device.SetSuspended(false); Assert.Equal(5u, buffer.GetStatus().Flags);
    }

    [Fact]
    public void DeferredPositionWaitsForCommitAndNativeDistanceIsNotAppliedTwice()
    {
        using var f = new Fixture(); var buffer = f.Duplicate(); buffer.SetVolume(-500);
        buffer.SetSpatial(new(10, 0, 0), true); Assert.Equal(default, f.Players[0].Position);
        f.Device.CommitDeferredSettings(); Assert.Equal(new FVector(10, 0, 0), f.Players[0].Position);
        Assert.InRange(f.Players[0].Db, -13.411f, -13.410f);
        buffer.SetSpatial(new(51, 0, 0), false); Assert.True(f.Players[0].Muted);
        buffer.SetSpatial(default, false); Assert.False(f.Players[0].Muted);
    }

    [Fact]
    public void InvalidGainFrequencyAndNonfinitePositionNeverBecomePlatformSettings()
    {
        using var f = new Fixture(); var b = f.Duplicate(); Assert.False(b.SetVolume(1)); Assert.False(b.TrySetFrequency(99));
        Assert.True(b.TrySetFrequency(44100)); Assert.Equal(2f, f.Players[0].Pitch);
        Assert.True(b.TrySetFrequency(0)); Assert.Equal(1f, f.Players[0].Pitch);
        Assert.Throws<NotSupportedException>(() => b.SetSpatial(new(float.NaN, 0, 0), false));
    }

    [Fact]
    public void DuplicateGenerationAndChannelOwnershipCannotAliasOrReviveReleasedBuffers()
    {
        using var f = new Fixture(); var old = f.Duplicate();
        Assert.Throws<InvalidDataException>(() => f.Duplicate(2)); old.Release(false);
        Assert.Throws<ObjectDisposedException>(() => old.GetStatus());
        Assert.Throws<InvalidDataException>(() => f.Duplicate(1));
        var current = f.Duplicate(3); Assert.NotEqual(old.Token, current.Token); Assert.Equal(1, f.Device.BufferCount);
        Assert.Throws<InvalidDataException>(() => f.Duplicate(4, 12));
    }

    [Fact]
    public void TeardownReleasesEveryPlayerEvenWhenOneStopFailsAndIsIdempotent()
    {
        using var f = new Fixture(); f.Duplicate(); f.Duplicate(2, 1); f.Players[0].ThrowStop = true;
        Assert.Throws<InvalidOperationException>(() => f.Device.Dispose());
        Assert.All(f.Players, p => Assert.True(p.Disposed)); Assert.Equal(0, f.Device.BufferCount);
        f.Device.Dispose(); Assert.Throws<ObjectDisposedException>(() => f.Duplicate(3));
    }

    [Fact]
    public void ForeignThreadCannotTouchMainThreadDevice()
    {
        using var f = new Fixture(); Exception? error = null;
        var thread = new Thread(() => { try { f.Device.CommitDeferredSettings(); } catch (Exception e) { error = e; } });
        thread.Start(); thread.Join(); Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void GodotPCM8ConversionChangesOnlySignedBiasAndDoesNotMutateNativePCM()
    {
        byte[] raw = new byte[32]; raw.AsSpan(0, 8).Fill(0x80); raw.AsSpan(8, 8).Fill(0x7f);
        var pcm = SamplePcm.ConvertPcm16(raw, 2, 64); byte[] original = pcm.Data.ToArray();
        Assert.Equal(original.Select(b => (byte)(b ^ 128)), LiveAudioEncoding.ForGodot(pcm));
        Assert.Equal(original, pcm.Data.ToArray());
    }

    [Fact]
    public void UnknownOrUnwrittenVoiceBuffersCannotBecomeSilentSuccessfulSamples()
    {
        using var f = new Fixture(); using var data = new LibraryFixture();
        Assert.Throws<NotSupportedException>(() => data.Load().Read(@"data\sounds\missing.ogg"));
        Assert.Throws<ArgumentException>(() => f.Sources.Pcm(new("effect", 8820)));
        var source = new LiveSoundSources(Bank(28), Options(2));
        Assert.Throws<NotSupportedException>(() => source.Pcm(source.Options(f.Device).SampleLookup("effect", false)!));
    }

    [Theory]
    [InlineData("ogg_sha256")] [InlineData("pcm_sha256")]
    public void PreparedPCMRequiresExactActualOggAndPCMBytes(string key)
    {
        using var data = new LibraryFixture(); data.Entry[key] = new string('0', 64);
        Assert.Throws<InvalidDataException>(() => data.Load());
    }

    [Theory]
    [InlineData("../voice.pcm")] [InlineData("sub\\voice.pcm")]
    public void PreparedPCMRefusesPathEscapes(string file)
    {
        using var data = new LibraryFixture(); data.Entry["pcm_file"] = file;
        Assert.Throws<InvalidDataException>(() => data.Load());
    }

    [Fact]
    public void PreparedPCMRefusesEmptyDuplicateWrongSizeOrWrongDecoderContract()
    {
        using var data = new LibraryFixture(); var entries = (JsonArray)data.Manifest["voices"]!;
        entries.Add(data.Entry.DeepClone()); Assert.Throws<InvalidDataException>(() => data.Load()); entries.RemoveAt(1);
        data.Entry["decoded_bytes"] = 4; Assert.Throws<InvalidDataException>(() => data.Load());
        data.Entry["decoded_bytes"] = data.Pcm.Length;
        data.Manifest["contract"] = "header-duration"; Assert.Throws<InvalidDataException>(() => data.Load());
        data.Manifest["contract"] = VoicePcmLibrary.Contract; entries.Clear(); Assert.Throws<InvalidDataException>(() => data.Load());
    }

    [GameDataFact]
    public void ExternalOptionsReceiveTheActualSingleBootLoadWithoutChangingPresentClockOrCRT()
    {
        int reads = 0, factories = 0;
        (string Name, byte[] Bytes) Read(string path)
        {
            if (path == DefaultOptions.FileName) reads++;
            return GameData.Read(path);
        }
        using var f = new Fixture();
        var voice = new BoundVoice();
        var session = LevelSession.Load(100, Read, _ => voice, soundDeviceOptions: options =>
        {
            factories++; Assert.NotNull(options);
            return new SoundDeviceProcessOptions(new(1, 12, true, false), f.Sources.Options(f.Device).Seeds,
                f.Process.SampleLookup, f.Sources.HasBuffer, _ => new(default, default), f.Device);
        });
        Assert.Equal(1, reads); Assert.Equal(1, factories); Assert.Same(session.SoundProcess, voice.Process);
        Assert.False(session.SoundProcess!.Initialised);
        var virtualOptions = new VirtualSoundProcessOptions(new(1, 12, true, false), f.Sources.Options(f.Device).Seeds,
            f.Process.SampleLookup, f.Sources.HasBuffer, _ => new(default, default));
        var control = LevelSession.Load(100, GameData.Read, soundOptions: virtualOptions);
        session.AdvanceToNextPresent(); control.AdvanceToNextPresent();
        Assert.True(session.SoundProcess.Initialised); Assert.Equal(3, session.PresentCount);
        Assert.Equal(control.Clock.PeekSteps, session.Clock.PeekSteps);
        Assert.Equal(control.SoundProcess!.Effects.LastProcessTime, session.SoundProcess.Effects.LastProcessTime);
        Assert.Equal(control.Clock.GetSysTimeFloat(), session.Clock.GetSysTimeFloat());
        Assert.Equal(control.Context.Crt.Calls, session.Context.Crt.Calls);
        Assert.Throws<ArgumentException>(() => LevelSession.Load(100, _ => throw new Exception("must refuse before reads"),
            soundOptions: new(new(1, 12, true, false), f.Sources.Options(f.Device).Seeds, f.Process.SampleLookup,
                f.Sources.HasBuffer, _ => new(default, default)), soundDeviceOptions: _ => throw new Exception()));
    }
    private sealed class BoundVoice : ISoundProcessVoice
    {
        public SoundProcess? Process;
        public void BindSoundProcess(SoundProcess process) => Process = process;
        public bool SoundInitialised => Process?.Initialised == true;
        public bool LoaderBusy => false;
        public bool HasLoadedData => false;
        public void StartLoad(string path) => throw new NotSupportedException();
        public void PlayLoaded(ScriptMessage? message) => throw new NotSupportedException();
        public void KillVoice() { }
        public void UpdateStatus() => throw new InvalidOperationException();
    }
}
