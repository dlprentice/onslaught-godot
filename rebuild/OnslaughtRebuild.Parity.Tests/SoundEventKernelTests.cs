// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;
using SoundOwner = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SoundEventKernelTests
{
    // Controlled platform boundary, never a production audio backend. Original-code fixtures intercept precisely
    // these device/camera calls while executing retail event/list/admission/status code unchanged in private memory.
    private sealed class Backend(int channels) : ISoundEventBackend
    {
        public int AvailableChannels => channels;
        public readonly HashSet<int> Buffers = [];
        public readonly List<string> Calls = [];
        public int FinishedSlot = -1;
        public bool PlayReachesPlayingWrite = true;
        public double RequestedDistance;
        public bool HasBuffer(int channel) => Buffers.Contains(channel);
        public void PrepareStatus() { }
        public void UpdateGlobals() { }
        public void PrepareStart(SoundEffects.SoundEvent e) => e.Pan = 0;
        public void UpdatePosition(SoundEffects.SoundEvent e, bool first) { e.Position = new FVector(); e.Velocity = new FVector(); }
        public double Distance(SoundEffects.SoundEvent e) => RequestedDistance;
        public (int, int) EvaluateVolumes(SoundEffects.SoundEvent e, SoundVolumeSite site)
        {
            // Oracle kernel fixtures use unit master gains and exact quarter fades. Full gain/position parity is
            // deliberately outside this test backend. A separate concrete adapter must establish those numerics.
            int v = (int)Math.Round(e.SubVolume * e.MasterVolume * 127);
            v = Math.Max(-10000, (Math.Min(10000, v * 200) - 10000) / 2);
            return (v, v);
        }
        public bool Play(SoundEffects.SoundEvent e) { Calls.Add($"P{e.Slot}:{e.Channel},"); if (PlayReachesPlayingWrite) Buffers.Add(e.Channel); return PlayReachesPlayingWrite; }
        public void Stop(SoundEffects.SoundEvent e) { Calls.Add($"S{e.Slot}:{e.Channel},"); Buffers.Remove(e.Channel); }
        public void Pause(SoundEffects.SoundEvent e) => Calls.Add($"A{e.Slot}:{e.Channel},");
        public void Resume(SoundEffects.SoundEvent e) => Calls.Add($"R{e.Slot}:{e.Channel},");
        public bool Update(SoundEffects.SoundEvent e) { Calls.Add($"U{e.Slot}:{e.Channel},"); return FinishedSlot == e.Slot; }
        public void UpdatesDone() { }
    }

    private static (SoundEffects Sound, Backend Backend) Config(int channels = 12, int initialChannel = -1, Func<float>? clock = null)
    {
        var sounds = new SoundEffects(); var backend = new Backend(channels);
        sounds.ConfigureEventKernel(backend, clock ?? (() => 0), () => false,
            Enumerable.Repeat(new SoundEventSeed(initialChannel, new FVector()), SoundEffects.EventCapacity).ToArray(), 1);
        return (sounds, backend);
    }

    private static readonly SampleMetadata Sample = new("sample", 44100);

    [Fact]
    public void PoolRetainsChannelAcrossDeferredDeletionAndAffectsItsOwnNextAllocation()
    {
        var (sounds, backend) = Config(4);
        var first = sounds.PlaySample(Sample, null, new SoundPlayback(Repeat: true))!;
        Assert.Equal(0, first.Channel);
        sounds.StopSoundEvent(first);
        Assert.Single(sounds.Events); Assert.Equal(0, first.Channel); Assert.Empty(backend.Buffers);
        sounds.UpdateStatus(); Assert.Empty(sounds.Events);
        var reused = sounds.PlaySample(Sample, null, new SoundPlayback(Repeat: true))!;
        Assert.Same(first, reused); Assert.Equal(1, reused.Channel);
    }

    [Fact]
    public void InitialChannelIsAnExplicitProfileNotAnInventedMinusOneDefault()
    {
        var (sounds, _) = Config(4, 0);
        Assert.Equal(1, sounds.PlaySample(Sample, null, new SoundPlayback())!.Channel);
    }

    [Fact]
    public void OrdinaryInsertionIsAfterFirstUnallocatedNodeAndCockpitInsertionIsAfterHead()
    {
        var (sounds, _) = Config(0); var owner = new SoundOwner();
        var a = sounds.PlaySample(Sample, owner, new SoundPlayback())!;
        var b = sounds.PlaySample(Sample, owner, new SoundPlayback())!;
        var c = sounds.PlaySample(Sample, owner, new SoundPlayback())!;
        // all channel-less: ordinary insertion always after head
        Assert.Equal(new[] { a, c, b }, sounds.Events);
        var d = sounds.PlaySample(Sample, null, new SoundPlayback())!;
        Assert.Equal(new[] { a, d, c, b }, sounds.Events);
        a.Channel = 0;
        var e = sounds.PlaySample(Sample, owner, new SoundPlayback())!;
        Assert.Equal(new[] { a, d, e, c, b }, sounds.Events);
    }

    [Fact]
    public void SortIsOneAdjacentPassAndRetainsStoppedAndPausedListPositions()
    {
        var (sounds, backend) = Config(4); var owner = new SoundOwner();
        var es = Enumerable.Range(0, 4).Select(_ => sounds.PlaySample(Sample, owner, new SoundPlayback(Repeat: true))!).ToArray();
        // all allocated -> initial list is append order
        int[] rank = [1, 4, 3, 2]; for (int i = 0; i < 4; i++) es[i].AttenuatedVolume = rank[i];
        sounds.StopSoundEvent(es[2]); sounds.PauseAllSamples(); backend.Calls.Clear();
        sounds.SortEventList();
        Assert.Equal(new[] { es[1], es[0], es[2], es[3] }, sounds.Events);
        Assert.Equal(-1, es[3].Channel); Assert.Equal(2, es[2].Channel);
        Assert.Equal(new[] { "S3:3," }, backend.Calls);
        es[2].Channel = -1; sounds.UnPauseAllSamples(); backend.Calls.Clear();
        sounds.SortEventList();
        Assert.Contains(backend.Calls, c => c.StartsWith("P2:")); // native does not test Playing
        Assert.True(es[2].Playing); // successful CPC PlaySound revives it before deferred deletion
    }

    [Fact]
    public void StopCallbackSeesPlayingOwnedEventAndBlocksOnceBeforeClearingOwner()
    {
        var (sounds, backend) = Config(); var owner = new SoundOwner(); bool callback = false;
        var e = sounds.PlaySample(Sample, owner, new SoundPlayback(InformOwner: true), (o, active) =>
        {
            callback = true; Assert.Same(owner, o); Assert.True(active.Playing); Assert.Same(owner, active.Owner);
            Assert.True(backend.HasBuffer(active.Channel));
            Assert.Null(sounds.PlaySample(Sample, owner, new SoundPlayback(Once: true)));
        })!;
        sounds.StopSoundEvent(e);
        Assert.True(callback); Assert.False(e.Playing); Assert.Null(e.Owner); Assert.Single(sounds.Events);
        Assert.Equal(0, owner.DeletionCount); Assert.Equal(0, e.Channel);
    }

    [Fact]
    public void OnceUsesSampleIdentityAndWildcardStoredOwnerButQueriesUseNameAndExactOwner()
    {
        var (sounds, _) = Config(); var owner = new SoundOwner();
        var a = sounds.PlaySample(Sample, null, new SoundPlayback())!;
        Assert.Null(sounds.PlaySample(Sample, owner, new SoundPlayback(Once: true)));
        var sameName = new SampleMetadata("SAMPLE", 44100);
        var b = sounds.PlaySample(sameName, owner, new SoundPlayback(Once: true));
        Assert.NotNull(b); Assert.Same(a, sounds.FindPlayingSample("SAMPLE", null));
        Assert.Same(b, sounds.FindPlayingSample("sample", owner));
    }

    [Fact]
    public void PauseIsPerEventAndNewlyStartedSoundRemainsUnpaused()
    {
        var (sounds, backend) = Config(); var owner = new SoundOwner();
        var a = sounds.PlaySample(Sample, owner, new SoundPlayback())!;
        sounds.StopSoundEvent(a); sounds.PauseAllSamples();
        Assert.True(a.Paused); Assert.Contains("A0:0,", backend.Calls);
        var b = sounds.PlaySample(Sample, owner, new SoundPlayback())!;
        Assert.False(b.Paused); Assert.True(a.Paused);
        sounds.UnPauseAllSamples(); Assert.False(a.Paused); Assert.False(b.Paused);
    }

    [Fact]
    public void EmptyStatusConsumesTwoClockReadsAndStoresSecondReading()
    {
        var q = new Queue<float>([1, 2, 2.25f, 3, 3.5f]);
        var (sounds, _) = Config(clock: () => q.Dequeue());
        sounds.UpdateStatus(); Assert.Equal(2.25f, sounds.LastProcessTime);
        var e = sounds.PlaySample(Sample, null, new SoundPlayback(Repeat: true))!;
        sounds.UpdateStatus(); Assert.Equal(.75f, e.Time); Assert.Equal(3.5f, sounds.LastProcessTime); Assert.Empty(q);
    }

    [Fact]
    public void FadeUsesStrictOvershootAndFrozenSuppressesDeletionNotTimeOrFade()
    {
        float clock = 0; var (sounds, _) = Config(clock: () => clock);
        var e = sounds.PlaySample(Sample, null, new SoundPlayback(Repeat: true, Fade: -.5f))!;
        sounds.SoundsFrozen = true; clock = .25f;
        sounds.UpdateStatus(); Assert.Equal(.5f, e.SubVolume); Assert.Equal(.25f, e.Time);
        sounds.UpdateStatus(); Assert.Equal(0, e.SubVolume); Assert.True(e.Playing);
        sounds.UpdateStatus(); Assert.False(e.Playing); Assert.Single(sounds.Events);
        sounds.SoundsFrozen = false; sounds.UpdateStatus(); Assert.Empty(sounds.Events);
    }

    [Fact]
    public void NonloopExpiryAndLoopWrappingUseStrictTimeBoundaryEvenWhenPaused()
    {
        var (sounds, _) = Config(0);
        var e = sounds.PlaySample(Sample, null, new SoundPlayback())!;
        e.Time = e.SampleLength; sounds.PauseAllSamples(); sounds.UpdateStatus(); Assert.True(e.Playing);
        e.Time = float.BitIncrement(e.Time); sounds.UpdateStatus(); Assert.False(e.Playing);
        var loop = sounds.PlaySample(Sample, null, new SoundPlayback(Repeat: true))!;
        loop.Time = loop.SampleLength; sounds.PauseAllSamples(); sounds.UpdateStatus(); Assert.Equal(loop.SampleLength, loop.Time);
        loop.Time = 2 * loop.SampleLength; sounds.UpdateStatus(); Assert.Equal(loop.SampleLength, loop.Time);
    }

    [Fact]
    public void PoolCapacityIncludesStoppedFrozenEntriesAndOwnerReaderIsReleasedOnlyByStop()
    {
        var (sounds, _) = Config(0); var owner = new SoundOwner();
        for (int i = 0; i < 256; i++) Assert.NotNull(sounds.PlaySample(Sample, owner, new SoundPlayback(Repeat: true)));
        Assert.Null(sounds.PlaySample(Sample, owner, new SoundPlayback())); Assert.Equal(256, owner.DeletionCount);
        sounds.SoundsFrozen = true; sounds.KillSamplesForThing(owner); sounds.UpdateStatus();
        Assert.Equal(256, sounds.EventCount); Assert.Equal(0, owner.DeletionCount);
        Assert.Null(sounds.PlaySample(Sample, owner, new SoundPlayback()));
        sounds.SoundsFrozen = false; sounds.UpdateStatus(); Assert.Equal(0, sounds.EventCount);
        Assert.NotNull(sounds.PlaySample(Sample, owner, new SoundPlayback()));
    }

    [Fact]
    public void EarlyDistanceRejectionReturnsNodeWithoutStopAndPreservesReaderUntilReuse()
    {
        var (sounds, backend) = Config(); var owner = new SoundOwner(); backend.RequestedDistance = 50;
        Assert.Null(sounds.PlaySample(Sample, owner, new SoundPlayback()));
        Assert.Equal(0, sounds.EventCount); Assert.Equal(1, owner.DeletionCount); Assert.Empty(backend.Calls);
        backend.RequestedDistance = 0;
        Assert.NotNull(sounds.PlaySample(Sample, null, new SoundPlayback())); Assert.Equal(0, owner.DeletionCount);
    }

    [Fact]
    public void OwnerDeathAndIgnorePositionEachStopFollowAndDieButNotFollowDontDie()
    {
        var (sounds, _) = Config(); var owner = new SoundOwner();
        var dies = sounds.PlaySample(Sample, owner, new SoundPlayback(Tracking: SoundTracking.FollowAndDie, Repeat: true))!;
        var stays = sounds.PlaySample(Sample, owner, new SoundPlayback(Tracking: SoundTracking.FollowDontDie, Repeat: true))!;
        owner.Destroy(); sounds.UpdateStatus(); Assert.False(dies.Playing); Assert.True(stays.Playing); Assert.Null(stays.Owner);
        var ignored = sounds.PlaySample(Sample, new SoundOwner(), new SoundPlayback(Tracking: SoundTracking.FollowAndDie, IgnoreOwnerPosition: true))!;
        sounds.UpdateStatus(); Assert.False(ignored.Playing);
    }

    [Fact]
    public void EffectDrawsHappenBeforeAdmissionButNotBeforeInitialization()
    {
        var (sounds, _) = Config(); var rng = new CrtRandStream(); var effect = new SoundEffect("effect", 0, volume: 80);
        sounds.Initialised = false; Assert.Null(sounds.PlayEffect(effect, null, rng, (_, _) => Sample, new SoundPlayback())); Assert.Equal(0, rng.Calls);
        sounds.Initialised = true; sounds.SetMasterVolume(0);
        Assert.Null(sounds.PlayEffect(effect, null, rng, (_, _) => Sample, new SoundPlayback())); Assert.Equal(1, rng.Calls);
        sounds.SetMasterVolume(1);
        var e = sounds.PlayEffect(effect, null, rng, (_, _) => Sample, new SoundPlayback(Volume: .45f))!;
        Assert.Equal(0x3eb851ebu, BitConverter.SingleToUInt32Bits(e.MasterVolume)); // native 80 * .45f * .01f, not / 100
        Assert.False(e.Looping); Assert.Equal(2, rng.Calls);
        Assert.Null(sounds.PlayEffect(effect, null, rng, (_, _) => Sample, new SoundPlayback(Once: true))); Assert.Equal(3, rng.Calls);
    }

    [Fact]
    public void LegacyAndConfiguredOwnersCannotAccidentallyRunTogether()
    {
        var sounds = new SoundEffects(); var rng = new CrtRandStream(); var effect = new SoundEffect("loop", 0);
        sounds.PlayRepeatingEffect(effect, new object(), rng);
        Assert.Throws<InvalidOperationException>(() => sounds.ConfigureEventKernel(new Backend(12), () => 0, () => false,
            Enumerable.Repeat(new SoundEventSeed(-1, new FVector()), 256).ToArray(), 1));
        var (configured, _) = Config();
        Assert.Throws<InvalidOperationException>(() => configured.AdvanceLoopStatus(false));
        Assert.Throws<InvalidOperationException>(() => configured.PlayEffect(effect, rng));
        Assert.Throws<InvalidOperationException>(() => configured.PlayRepeatingEffect(effect, new object(), rng));
    }

    [RetailOracleFact("sound-events")]
    public void KernelMatchesOriginalAdmissionLinksChannelsCallbacksPauseAndStatus()
    {
        var outputs = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("sound-events"))
        {
            Assert.Equal("Kernel", function);
            int channels = (int)w[0], initial = (int)w[1], actions = (int)w[2], n = (int)w[5];
            var clocks = new Queue<float>([0, RetailOracle.F(w[3]), RetailOracle.F(w[4])]);
            var (sounds, backend) = Config(channels, initial, () => clocks.Dequeue());
            backend.FinishedSlot = (int)w[6]; sounds.SoundsFrozen = (actions & 256) != 0;
            SampleMetadata[] samples = [new("sample0", 44100), new("sample1", 44100)];
            SoundOwner?[] owners = [null, new(), new()];
            for (int i = 0; i < n; i++)
            {
                int at = 7 + i * 9, flags = (int)w[at + 2];
                sounds.PlaySample(samples[w[at]], owners[w[at + 1]], new SoundPlayback(Volume: RetailOracle.F(w[at + 3]),
                    Tracking: SoundTracking.None, Once: (flags & 1) != 0, Fade: RetailOracle.F(w[at + 4]),
                    Repeat: (flags & 2) != 0, Pitch: RetailOracle.F(w[at + 5]), InformOwner: (flags & 4) != 0,
                    IgnoreOwnerPosition: (flags & 8) != 0), (owner, e) =>
                    backend.Calls.Add($"C{e.Slot}:{(e.Playing ? 1 : 0)}:{(e.Owner is null ? 0 : 1)}:{e.Channel},"));
            }
            foreach (var e in sounds.Events)
            {
                int at = 7 + e.Slot * 9;
                e.Time = RetailOracle.F(w[at + 6]); e.AttenuatedVolume = (int)w[at + 7];
                if (w[at + 8] != 0x7fffffff) e.Channel = (int)w[at + 8];
            }
            if ((actions & 1) != 0) sounds.SortEventList();
            if ((actions & 2) != 0) sounds.PauseAllSamples();
            if ((actions & 4) != 0 && n > 0) sounds.StopSoundEvent(sounds.Events.Single(e => e.Slot == 0));
            if ((actions & 512) != 0 && n > 0) sounds.Events.Single(e => e.Slot == 0).Channel = -1;
            if ((actions & 8) != 0) sounds.SortEventList();
            if ((actions & 16) != 0) sounds.UnPauseAllSamples();
            if ((actions & 32) != 0) sounds.KillSamplesForThing(owners[1]);
            if ((actions & 64) != 0) sounds.UpdateStatus();
            if ((actions & 128) != 0) sounds.PlaySample(samples[0], owners[2], new SoundPlayback(Volume: 1, Tracking: SoundTracking.None, Repeat: true));
            string text = $"Kernel {sounds.EventCount:x} {(3 - clocks.Count - 1):x} {RetailOracle.H(sounds.LastProcessTime)}";
            foreach (var e in sounds.Events)
                text += $" {e.Slot:x},{e.Channel:x},{(e.Playing ? 1 : 0):x},{(e.Owner is null ? 0 : ReferenceEquals(e.Owner, owners[1]) ? 1 : 2):x},{(e.Paused ? 1 : 0):x},"
                    + $"{RetailOracle.H(e.Time)},{RetailOracle.H(e.SubVolume)},{RetailOracle.H(e.Fade)},{RetailOracle.H(e.Pitch)},{RetailOracle.H(e.FadeDestination)}";
            text += " |" + string.Concat(backend.Calls);
            outputs.Add(text);
            Assert.True(text == expected, $"case {outputs.Count - 1}: {text}\nretail: {expected}");
        }
        Assert.Equal(328, outputs.Count);
        RetailOracle.WriteOurs("sound-events", outputs.ToArray());
    }
    [Fact]
    public void FadeToCanRetargetStoppedNodesAndPitchFadeUsesNativeNearestInteger()
    {
        var (sounds, _) = Config();
        var e = sounds.PlaySample(Sample, null, new SoundPlayback())!;
        sounds.StopSoundEvent(e);
        sounds.FadeTo(Sample, .25f, .125f, null);
        Assert.Equal(.25f, e.FadeDestination); Assert.Equal(-.125f, e.Fade);
        sounds.SetPitch(e, 2, .075f); Assert.Equal(2, e.PitchFadeFrames); // 1.5 -> nearest even, not truncation
        sounds.SetPitch(e, 3, .125f); Assert.Equal(2, e.PitchFadeFrames); // 2.5 -> nearest even
        sounds.Initialised = false;
        sounds.SetPitch(e, 4); Assert.Equal(3, e.DesiredPitch);
    }

    [RetailOracleFact("sound-effect-admission")]
    public void EffectSelectionMatchesNativeFloatMultiplyPitchOnceAndLanguageAdmission()
    {
        var outputs = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("sound-effect-admission"))
        {
            var sounds = new SoundEffects { PitchVarianceEnabled = w[1] != 0, Initialised = w[2] != 0 };
            var rand = new CrtRandStream(); rand.Srand(w[0]);
            int count = (int)w[4], flags = (int)w[5];
            SoundEffect[] effects = Enumerable.Range(0, count).Select(i => new SoundEffect("effect", (int)w[9 + i * 4],
                ((char)('a' + i)).ToString(), w[10 + i * 4] != 0, (int)w[8 + i * 4], w[11 + i * 4] != 0)).ToArray();
            for (int i = 0; i < effects.Length - 1; i++) effects[i].Chained = effects[i + 1];
            var playback = new SoundPlayback(Volume: RetailOracle.F(w[6]), Pitch: RetailOracle.F(w[7]),
                Once: (flags & 1) != 0, Repeat: (flags & 2) != 0, IgnoreOwnerPosition: (flags & 4) != 0);
            int chosen = -1, language = 0; uint[] captured = new uint[6];
            if (sounds.Initialised)
            {
                SoundEffect selected = sounds.ChooseEffect(effects[0], rand, ref playback);
                chosen = Array.IndexOf(effects, selected); language = selected.LanguageDependent ? 1 : 0;
                if (w[3] == 0)
                    captured = [BitConverter.SingleToUInt32Bits(playback.Volume), BitConverter.SingleToUInt32Bits(playback.Pitch),
                        playback.Once ? 1u : 0u, 0, playback.Repeat ? 1u : 0u, playback.IgnoreOwnerPosition ? 1u : 0u];
            }
            string actual = $"Effect {rand.Calls:x} {rand.HoldRand:x8} {chosen:x} {language:x} 0"
                + string.Concat(captured.Select(x => $" {x:x8}"));
            Assert.True(actual == expected, $"case {outputs.Count}: {actual}\nretail: {expected}"); outputs.Add(actual);
        }
        Assert.Equal(640, outputs.Count); RetailOracle.WriteOurs("sound-effect-admission", outputs.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedStoppedNodeRevivesOnlyWhenBackendReachesPlayingWrite(bool reachesWrite)
    {
        var (sounds, backend) = Config(4);
        var e = sounds.PlaySample(Sample, null, new SoundPlayback(Repeat: true))!;
        sounds.StopSoundEvent(e); e.Channel = -1;
        backend.PlayReachesPlayingWrite = reachesWrite;
        sounds.SortEventList();
        Assert.Equal(reachesWrite, e.Playing);
        Assert.Equal(reachesWrite, backend.HasBuffer(e.Channel));
    }

    [Fact]
    public void AuthoredMetadataRetainsVolumeLanguageAndLoopingWithoutImplyingRepeat()
    {
        byte[] bytes = System.Text.Encoding.Latin1.GetBytes("103\n1\nwarning\nsample\nlowpass\n80\n0\n5\n1\n1\ncomment\n");
        var effect = SoundEffects.Load(bytes).Find("warning")!;
        Assert.Equal(80, effect.Volume); Assert.True(effect.LanguageDependent); Assert.True(effect.Looping);
        var (sounds, _) = Config(); var rand = new CrtRandStream(); bool language = false;
        SoundPlayback options = new(Once: true, InformOwner: true);
        var owner = new SoundOwner();
        var e = sounds.PlayEffect(effect, owner, rand, (name, lang) => { Assert.Equal("sample", name); language = lang; return Sample; }, options)!;
        Assert.True(language); Assert.False(e.Looping); Assert.False(e.InformOwner);
        Assert.NotNull(sounds.PlayEffect(effect, owner, rand, (_, _) => Sample, options)); // authored Looping clears once only
    }

    [Fact]
    public void PrerunRejectsNonrepeatAfterEffectDrawButKeepsExplicitRepeat()
    {
        var sounds = new SoundEffects(); var backend = new Backend(4); bool prerun = true;
        sounds.ConfigureEventKernel(backend, () => 0, () => prerun,
            Enumerable.Repeat(new SoundEventSeed(-1, new FVector()), 256).ToArray(), 1);
        var effect = new SoundEffect("effect", 0, looping: true); var rand = new CrtRandStream();
        Assert.Null(sounds.PlayEffect(effect, null, rand, (_, _) => Sample, new SoundPlayback())); Assert.Equal(1, rand.Calls);
        Assert.NotNull(sounds.PlayEffect(effect, null, rand, (_, _) => Sample, new SoundPlayback(Repeat: true))); Assert.Equal(2, rand.Calls);
        prerun = false;
        Assert.NotNull(sounds.PlayEffect(effect, null, rand, (_, _) => Sample, new SoundPlayback())); Assert.Equal(3, rand.Calls);
    }

}
