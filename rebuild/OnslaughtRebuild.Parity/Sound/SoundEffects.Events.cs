// SPDX-License-Identifier: GPL-3.0-only
// Specimen 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// CSoundManager PlaySample004e0b30, Start004e0bd0, Stop004e0f70, pool004e0fb0, Sort004e1040,
// Kill004e1130, Pause004e1300, UnPause004e1330 and UpdateStatus004e1b20.
using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using SoundOwner = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Sound;

public enum SoundTracking { None, InitialPosition, FollowAndDie, FollowDontDie }
public enum SoundKind { Menu, Game }
public enum SoundVolumeSite { Start, Fade, TrackedOwner, MasterVolume }

/// <summary>All PlaySample arguments; neither effect.Looping nor global pause replaces the explicit Repeat value.</summary>
public readonly record struct SoundPlayback(
    float Volume = 0.7f, SoundTracking Tracking = SoundTracking.FollowDontDie,
    bool Once = false, float Fade = 0, float From = 0, float To = -1,
    bool Repeat = false, float Pitch = 1, bool InformOwner = false,
    bool IgnoreOwnerPosition = false, SoundKind Kind = SoundKind.Game)
{
    public SoundPlayback() : this(0.7f) { }
}

/// <summary>
/// Uninitialized fields consumed before native Start overwrites them. Supply measured allocator/profile contents;
/// neither zero nor -1 is a proven retail Channel default. Seeds are ordered from the initial pool head.
/// </summary>
public readonly record struct SoundEventSeed(int Channel, FVector Position);

/// <summary>
/// Required boundary to the concrete platform/camera adapter. This is not an empty or duration-only sound backend.
/// The event kernel owns admission, ordering, lifetime and callbacks; the adapter owns actual voice buffers,
/// spatial transforms and the instruction-site-specific gain calculation. No adapter is selected implicitly.
/// </summary>
public interface ISoundEventBackend
{
    int AvailableChannels { get; }
    bool HasBuffer(int channel);
    void PrepareStatus(); // camera position/old position/orientation before sorting
    void UpdateGlobals();
    void PrepareStart(SoundEffects.SoundEvent e); // native pan reads the retained position before UpdatePosition
    void UpdatePosition(SoundEffects.SoundEvent e, bool first);
    double Distance(SoundEffects.SoundEvent e);
    (int Volume, int Attenuated) EvaluateVolumes(SoundEffects.SoundEvent e, SoundVolumeSite site);
    bool Play(SoundEffects.SoundEvent e); // true iff CPC PlaySound reaches its Playing=true write (00517932)
    void Stop(SoundEffects.SoundEvent e);
    void Pause(SoundEffects.SoundEvent e);
    void Resume(SoundEffects.SoundEvent e);
    bool Update(SoundEffects.SoundEvent e); // true exactly when native UpdateSound calls StopSoundEvent
    void UpdatesDone();
}

public sealed partial class SoundEffects
{
    public const int EventCapacity = 256;

    /// <summary>One native pool node. A handle may be reused only after deferred deletion; its Channel is retained.</summary>
    public sealed class SoundEvent
    {
        internal SoundEvent(SoundEffects manager, int slot) { Manager = manager; Slot = slot; }
        internal SoundEffects Manager { get; }
        internal ActiveReader<SoundOwner> OwnerReader { get; } = new();
        internal object? LegacyOwner;
        internal bool Legacy;
        internal SoundEvent? Next, Previous;
        internal bool InActiveList;
        internal RepeatingEffect? LoopHandle;
        internal Action<SoundOwner, SoundEvent>? Completion;
        internal SoundEffect? Effect;
        public int Slot { get; }
        public object? Owner => Legacy ? LegacyOwner : OwnerReader.ToRead;
        public SampleMetadata Sample { get; internal set; } = null!;
        public int Channel { get; internal set; }
        public bool Playing { get; internal set; }
        public bool Paused { get; internal set; }
        public bool Looping { get; internal set; }
        public SoundTracking Tracking { get; internal set; }
        public SoundKind Kind { get; internal set; }
        public float MasterVolume { get; internal set; }
        public float SubVolume { get; internal set; }
        public float Fade { get; internal set; }
        public float FadeDestination { get; internal set; }
        public float Time { get; internal set; }
        public float StartPoint { get; internal set; }
        public float EndPoint { get; internal set; }
        public float Pitch { get; internal set; }
        public float DesiredPitch { get; internal set; }
        public int PitchFadeFrames { get; internal set; }
        public bool InformOwner { get; internal set; }
        public bool IgnoreOwnerPosition { get; internal set; }
        public FVector Position { get; set; }
        public FVector Velocity { get; set; }
        public int Pan { get; set; }
        public int CurrentVolume { get; internal set; }
        public int AttenuatedVolume { get; internal set; }
        public float SampleLength { get; internal set; }
    }

    private SoundEvent? _first, _pool;
    private ISoundEventBackend? _eventBackend;
    private Func<float>? _soundClock;
    private Func<bool>? _isPrerunning;
    private readonly Dictionary<string, SampleMetadata> _legacySamples = new(StringComparer.OrdinalIgnoreCase);
    private int _eventCount;
    private int _soundRate;
    public bool EventKernelConfigured => _eventBackend is not null;
    public bool Initialised { get; set; } = true;
    public bool SoundsFrozen { get; set; }
    public float MasterVolume { get; private set; } = 1;
    public float MenuSoundsMasterVolume { get; set; } = 1;
    public float LastProcessTime { get; private set; }
    public int EventCount => _eventCount;
    public IEnumerable<SoundEvent> Events
    {
        get { for (SoundEvent? e = _first; e is not null; e = e.Next) yield return e; }
    }

    /// <summary>
    /// Explicit handover from the old loop-only projection, only with no active entries. Exactly 256 pool seeds,
    /// a real backend, native clock reader and pre-run predicate are mandatory. This consumes Init's one clock read.
    /// Callers must migrate together; legacy draw/loop-only entry points refuse use after configuration.
    /// </summary>
    public void ConfigureEventKernel(ISoundEventBackend backend, Func<float> clock, Func<bool> isPrerunning,
        IReadOnlyList<SoundEventSeed> seeds, int soundRate)
    {
        ArgumentNullException.ThrowIfNull(backend); ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(isPrerunning); ArgumentNullException.ThrowIfNull(seeds);
        if (EventKernelConfigured || _eventCount != 0) throw new InvalidOperationException("Sound event ownership is already active.");
        if (seeds.Count != EventCapacity) throw new ArgumentException("Retail initializes 256 sound events.", nameof(seeds));
        if (backend.AvailableChannels < 0) throw new ArgumentOutOfRangeException(nameof(backend));
        _eventBackend = backend; _soundClock = clock; _isPrerunning = isPrerunning; _soundRate = soundRate;
        for (int i = seeds.Count - 1; i >= 0; --i)
        {
            var e = new SoundEvent(this, i) { Channel = seeds[i].Channel, Position = seeds[i].Position, Next = _pool };
            if (_pool is not null) _pool.Previous = e;
            _pool = e;
        }
        LastProcessTime = clock();
    }

    private ISoundEventBackend Backend => _eventBackend ?? throw new InvalidOperationException("Configure the sound event backend first.");
    private void RequireLegacyProjection()
    {
        if (EventKernelConfigured) throw new InvalidOperationException("Migrate this loop/draw-only caller to the configured sound event kernel.");
    }

    private SoundEvent? GetSoundEvent(bool insertAtTop)
    {
        SoundEvent? e = _pool;
        if (e is null) return null;
        _pool = e.Next;
        if (_pool is not null) _pool.Previous = null;
        e.Next = null;
        if (_first is null) { e.Previous = null; _first = e; }
        else
        {
            SoundEvent current = _first;
            if (!insertAtTop)
                while (current.Next is not null && current.Channel >= 0) current = current.Next;
            e.Next = current.Next; e.Previous = current; current.Next = e;
            if (e.Next is not null) e.Next.Previous = e;
        }
        e.InActiveList = true; ++_eventCount;
        return e;
    }

    private void DeleteSoundEvent(SoundEvent e)
    {
        if (e.Next is not null) e.Next.Previous = e.Previous;
        if (e.Previous is not null) e.Previous.Next = e.Next; else _first = e.Next;
        --_eventCount; e.InActiveList = false;
        if (e.Legacy) { e.Next = e.Previous = null; return; }
        // No Playing, owner or Channel reset: the early-distance rejection also uses this exact pool return.
        e.Next = _pool; e.Previous = null;
        if (_pool is not null) _pool.Previous = e;
        _pool = e;
    }

    private int FindFreeChannel()
    {
        for (int channel = 0; channel < Backend.AvailableChannels; channel++)
        {
            bool held = false;
            for (SoundEvent? e = _first; e is not null; e = e.Next)
                if (e.Channel == channel) { held = true; break; }
            if (!held && !Backend.HasBuffer(channel)) return channel;
        }
        return -1;
    }

    /// <summary>Admit one actual sample. Completion handlers must use their owner argument, not capture it strongly.</summary>
    public SoundEvent? PlaySample(SampleMetadata sample, SoundOwner? owner, SoundPlayback playback,
        Action<SoundOwner, SoundEvent>? completed = null)
    {
        _ = Backend;
        if (!Initialised || MasterVolume == 0 || (_isPrerunning!() && !playback.Repeat)) return null;
        if (playback.Once)
            for (SoundEvent? s = _first; s is not null; s = s.Next)
                if (s.Playing && ReferenceEquals(s.Sample, sample) && (s.Owner is null || ReferenceEquals(s.Owner, owner))) return null;
        if (!SampleBufferFormat.TryCreate(sample.OriginalPcmBytes, _soundRate, out SampleBufferFormat format) || !(format.NominalEventLength > 0))
            throw new NotSupportedException("The event kernel admits only samples with a validated backend format.");
        if (playback.InformOwner && owner is not null && completed is null)
            throw new ArgumentException("Owner completion needs the actual audible-owner callback.", nameof(completed));
        SoundEvent? e = GetSoundEvent(owner is null || playback.IgnoreOwnerPosition);
        if (e is null) return null;
        e.Effect = null; e.LoopHandle = null;
        e.OwnerReader.SetReader(owner); e.Sample = sample; e.MasterVolume = playback.Volume; e.Time = 0;
        // The newly inserted node's retained Channel participates in this lookup.
        e.Channel = FindFreeChannel(); e.Looping = playback.Repeat; e.PitchFadeFrames = 0;
        e.Pitch = e.DesiredPitch = playback.Pitch; e.InformOwner = playback.InformOwner;
        e.IgnoreOwnerPosition = playback.IgnoreOwnerPosition; e.Paused = false; e.Kind = playback.Kind; e.Playing = true;
        e.Tracking = owner is null ? SoundTracking.None : playback.Tracking; e.Completion = completed;
        e.Fade = playback.Fade; e.SubVolume = playback.Fade > 0 ? 0 : 1;
        if (playback.Fade != 0) e.FadeDestination = playback.Fade < 0 ? 0 : 1;
        e.StartPoint = playback.From; e.EndPoint = playback.To; e.SampleLength = format.NominalEventLength;
        Backend.PrepareStart(e); Backend.UpdatePosition(e, true);
        if (Backend.Distance(e) >= 50 && !e.Looping) { DeleteSoundEvent(e); return null; }
        RefreshVolumes(e, SoundVolumeSite.Start);
        if (e.Channel >= 0) PlayOnBackend(e);
        return e;
    }

    /// <summary>Effect selection and pitch precede sample lookup, master-volume, pre-run and once admission.</summary>
    public SoundEvent? PlayEffect(SoundEffect? effect, SoundOwner? owner, CrtRandStream rand,
        Func<string, bool, SampleMetadata?> lookup, SoundPlayback playback)
    {
        _ = Backend;
        if (!Initialised || effect is null) return null;
        SoundEffect chosen = ChooseEffect(effect, rand, ref playback);
        SampleMetadata? sample = lookup(chosen.SampleName, chosen.LanguageDependent);
        if (sample is null) return null;
        var e = PlaySample(sample, owner, playback with { InformOwner = false });
        if (e is not null) e.Effect = chosen;
        return e;
    }

    internal SoundEffect ChooseEffect(SoundEffect effect, CrtRandStream rand, ref SoundPlayback playback)
    {
        int count = 0;
        for (SoundEffect? c = effect; c is not null; c = c.Chained) count++;
        int pick = rand.Rand() % count;
        SoundEffect chosen = effect;
        while (pick-- > 0) chosen = chosen.Chained!;
        float volume = (float)X87.Mul(X87.Mul(playback.Volume, chosen.Volume), .01f);
        float pitch = playback.Pitch;
        if (chosen.PitchVariance != 0 && PitchVarianceEnabled)
        {
            float delta = (float)X87.Mul(rand.Rand() % chosen.PitchVariance, .01f);
            pitch = (float)X87.Add(pitch, delta);
            if (pitch < .1f) pitch = .1f;
        }
        playback = playback with { Volume = volume, Pitch = pitch, Once = playback.Once && !chosen.Looping };
        return chosen;
    }

    public bool IsEffectPlaying(SoundEffect? effect, object? owner)
    {
        for (SoundEffect? c = effect; c is not null; c = c.Chained)
            if (FindPlayingSample(c.SampleName, owner) is not null) return true;
        return false;
    }

    public SoundEvent? FindPlayingSample(string name, object? owner)
    {
        if (!Initialised) return null;
        for (SoundEvent? e = _first; e is not null; e = e.Next)
            if (e.Playing && ReferenceEquals(e.Owner, owner) && StringComparer.OrdinalIgnoreCase.Equals(e.Sample.Name, name)) return e;
        return null;
    }

    public void StopSoundEvent(SoundEvent e)
    {
        if (!ReferenceEquals(e.Manager, this)) throw new ArgumentException("The event belongs to another sound manager.", nameof(e));
        if (e.InformOwner && e.OwnerReader.ToRead is { } owner) e.Completion!(owner, e);
        if (!e.Legacy && e.Channel >= 0) Backend.Stop(e);
        e.Playing = false; e.OwnerReader.Release(); e.LegacyOwner = null;
        // Channel and list membership deliberately survive Stop.
    }

    public void StopSample(string name, object? owner)
    {
        if (!Initialised) return;
        for (SoundEvent? e = _first; e is not null; e = e.Next)
            if (e.Playing && ReferenceEquals(e.Owner, owner) && StringComparer.OrdinalIgnoreCase.Equals(e.Sample.Name, name)) StopSoundEvent(e);
    }

    public void FadeTo(SampleMetadata sample, float destination, float speed, object? owner)
    {
        for (SoundEvent? e = _first; e is not null; e = e.Next)
            if (ReferenceEquals(e.Sample, sample) && ReferenceEquals(e.Owner, owner))
            { e.FadeDestination = destination; e.Fade = destination < e.SubVolume ? -speed : speed; return; }
    }

    public void SetPitch(SoundEvent? e, float desiredPitch, float fadeTime = 0)
    {
        if (!Initialised || e is null) return;
        e.DesiredPitch = desiredPitch;
        e.PitchFadeFrames = X87.Fist(X87.Mul(fadeTime, 20));
    }

    public void KillAllSamples()
    {
        for (SoundEvent? e = _first; e is not null; e = e.Next) StopSoundEvent(e);
    }

    public void KillAllInstancesOfSample(SampleMetadata sample)
    {
        // PC StopSound ignores its block-until-stopped argument; logical ordering is unchanged.
        for (SoundEvent? e = _first; e is not null; e = e.Next)
            if (ReferenceEquals(e.Sample, sample)) StopSoundEvent(e);
    }

    public void PauseAllSamples()
    {
        _ = Backend;
        for (SoundEvent? e = _first; e is not null; e = e.Next)
        { if (e.Channel != -1) Backend.Pause(e); e.Paused = true; }
    }

    public void UnPauseAllSamples()
    {
        _ = Backend;
        for (SoundEvent? e = _first; e is not null; e = e.Next)
        { if (e.Channel != -1) Backend.Resume(e); e.Paused = false; }
    }

    public void SortEventList()
    {
        _ = Backend;
        for (SoundEvent? s = _first; s is not null; s = s.Next)
        {
            SoundEvent? n = s.Next;
            if (n is null || n.AttenuatedVolume <= s.AttenuatedVolume) continue;
            if (s.Previous is null) _first = n; else s.Previous.Next = n;
            if (n.Next is not null) n.Next.Previous = s;
            s.Next = n.Next; n.Previous = s.Previous; s.Previous = n; n.Next = s;
            // The loop advances old s->Next, skipping s as the next comparison's left operand.
        }
        int retained = Backend.AvailableChannels * 3 / 4;
        SoundEvent? e = _first;
        for (int i = 0; i < retained && e is not null; i++) e = e.Next;
        for (; e is not null; e = e.Next)
            if (e.Channel != -1) { Backend.Stop(e); e.Channel = -1; }
        e = _first;
        for (int i = 0; i < retained && e is not null; i++, e = e.Next)
            if (e.Channel == -1 && !e.Paused)
            { e.Channel = FindFreeChannel(); if (e.Channel < 0) break; PlayOnBackend(e); }
    }

    private void PlayOnBackend(SoundEvent e)
    { if (Backend.Play(e)) e.Playing = true; }

    private void RefreshVolumes(SoundEvent e, SoundVolumeSite site)
    { (e.CurrentVolume, e.AttenuatedVolume) = Backend.EvaluateVolumes(e, site); }

    public void SetMasterVolume(float value)
    {
        _ = Backend; MasterVolume = value;
        UpdateVolumeForAllSoundEvents();
        // CAREER persistence and LOG reporting belong to the caller, not this event-state kernel.
    }

    /// <summary>004e0300; separate from the inline game/menu gain setters, as in native shutdown and fade callers.</summary>
    public void UpdateVolumeForAllSoundEvents()
    {
        _ = Backend;
        for (SoundEvent? e = _first; e is not null; e = e.Next)
        { RefreshVolumes(e, SoundVolumeSite.MasterVolume); if (e.Playing && e.Channel >= 0 && Backend.Update(e)) StopSoundEvent(e); }
    }

    private void AdvanceFade(SoundEvent e)
    {
        e.SubVolume += e.Fade;
        if ((e.Fade > 0 && e.SubVolume > e.FadeDestination) || (e.Fade < 0 && e.SubVolume < e.FadeDestination))
        {
            bool stop = e.Fade < 0 && e.FadeDestination == 0;
            e.SubVolume = e.FadeDestination; e.Fade = 0;
            if (stop) StopSoundEvent(e);
        }
    }

    public void UpdateStatus()
    {
        Backend.PrepareStatus(); SortEventList(); Backend.UpdateGlobals();
        float delta = _soundClock!() - LastProcessTime;
        LastProcessTime = _soundClock(); // A distinct native read, including an empty active list.
        for (SoundEvent? e = _first; e is not null;)
        {
            if (!e.Paused) e.Time += delta;
            if (!SoundsFrozen) Backend.UpdatePosition(e, false);
            if (e.Time > 5 && !e.Playing) { e.OwnerReader.Release(); e.LegacyOwner = null; }
            else if (!e.Paused)
            {
                if (e.PitchFadeFrames > 0)
                { e.Pitch = (float)X87.Add(e.Pitch, X87.Div(X87.Sub(e.DesiredPitch, e.Pitch), e.PitchFadeFrames)); e.PitchFadeFrames--; }
                else e.Pitch = e.DesiredPitch;
                if (e.Fade != 0)
                { AdvanceFade(e); if (e.SubVolume != 0) RefreshVolumes(e, SoundVolumeSite.Fade); }
                if (e.Tracking is SoundTracking.FollowAndDie or SoundTracking.FollowDontDie or SoundTracking.InitialPosition)
                {
                    if (e.Owner is null || e.IgnoreOwnerPosition)
                    { if (e.Tracking == SoundTracking.FollowAndDie) StopSoundEvent(e); }
                    else RefreshVolumes(e, SoundVolumeSite.TrackedOwner);
                }
            }
            if (e.Playing && e.Channel >= 0 && Backend.Update(e)) StopSoundEvent(e);
            if (!e.Looping)
            {
                float end = e.EndPoint == -1 ? e.SampleLength : e.EndPoint;
                if ((e.Channel < 0 || e.EndPoint != -1) && e.Time > end) StopSoundEvent(e);
            }
            else
                while (e.Time > e.SampleLength) e.Time -= e.SampleLength;
            SoundEvent? next = e.Next;
            if (!e.Playing && !SoundsFrozen) DeleteSoundEvent(e);
            e = next;
        }
        Backend.UpdatesDone();
    }
}
