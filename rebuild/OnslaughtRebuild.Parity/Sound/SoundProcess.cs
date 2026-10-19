// SPDX-License-Identifier: GPL-3.0-only
// Process binding for CSoundManager::Init004e00d0 / System.Init004efb10, not a second event owner.
// Pristine specimen 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>
/// Explicit virtual-device admission. Pool seeds and source-buffer availability are measured host inputs, never
/// universal retail defaults. The spatial resolver must prove the owner's native sound velocity, not substitute its
/// movement velocity or assume zero. No retail profile or sample payload is embedded here.
/// </summary>
public sealed record VirtualSoundProcessOptions(VirtualSoundBackendProfile Profile,
    IReadOnlyList<SoundEventSeed> Seeds, Func<string, bool, SampleMetadata?> SampleLookup,
    Func<SampleMetadata, bool> SampleHasBuffer, Func<object, SoundOwnerSpatialState?> OwnerSpatial);

/// <summary>One process's configured sound owner, shared by front end, levels and restarts.</summary>
public sealed class SoundProcess
{
    private readonly SoundEventSeed[] _seeds;
    private readonly HashSet<SampleMetadata> _voiceSamples = new(ReferenceEqualityComparer.Instance);
    private ThingContext? _context;

    public SoundProcess(SoundEffects effects, PlatformClock clock, VirtualSoundProcessOptions options)
    {
        ArgumentNullException.ThrowIfNull(effects); ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(options.Seeds);
        ArgumentNullException.ThrowIfNull(options.SampleLookup); ArgumentNullException.ThrowIfNull(options.SampleHasBuffer);
        ArgumentNullException.ThrowIfNull(options.OwnerSpatial);
        if (options.Seeds.Count != SoundEffects.EventCapacity)
            throw new ArgumentException("A measured profile must supply all 256 initial pool seeds.", nameof(options));
        if (effects.EventKernelConfigured || effects.EventCount != 0)
            throw new InvalidOperationException("The process must bind an unused sound owner.");
        Effects = effects; Clock = clock; _seeds = options.Seeds.ToArray();
        SampleLookup = options.SampleLookup;
        Effects.SampleLookup = SampleLookup;
        Backend = new(effects, options.Profile, BlocksMixed,
            sample => _voiceSamples.Contains(sample) || options.SampleHasBuffer(sample), Camera,
            options.OwnerSpatial);
    }

    public SoundEffects Effects { get; }
    public PlatformClock Clock { get; }
    public VirtualSoundEventBackend Backend { get; }
    public Func<string, bool, SampleMetadata?> SampleLookup { get; }
    public bool Initialised => Effects.EventKernelConfigured;

    /// <summary>
    /// After device Present1 and the existing GetSysTimeFloat origin read, before films or Present2. Configure consumes
    /// Init's one further clock read. Validation and construction above make no clock reads. Never repeat on a restart.
    /// </summary>
    public void InitialiseAfterFirstPresent(float masterVolume)
    {
        if (Initialised) throw new InvalidOperationException("Sound has already been initialized for this process.");
        if (Clock.PresentCount != 1) throw new InvalidOperationException("Sound initialization belongs after device Present1.");
        Effects.ConfigureEventKernel(Backend, Clock.GetSysTimeFloat,
            () => _context?.Game?.State == GameState.PreRunning, _seeds, Backend.Profile.SoundRate);
        Effects.SetMasterVolume(masterVolume);
    }

    public void BindLevel(ThingContext context)
    {
        if (context.Game is null || !ReferenceEquals(context.Game.Clock, Clock))
            throw new ArgumentException("A sound process and its active level must share the platform clock.", nameof(context));
        if (!ReferenceEquals(context.SoundEffects, Effects))
            throw new ArgumentException("The level must use this process's event owner.", nameof(context));
        if (_context is not null && !ReferenceEquals(_context, context))
            throw new InvalidOperationException("Release the old level's sound binding before attaching another.");
        _context = context;
    }

    public void ReleaseLevel(ThingContext context)
    {
        if (!ReferenceEquals(_context, context)) throw new InvalidOperationException("This is not the active sound level.");
        _context = null;
    }

    private SoundCameraState? Camera() => _context?.Game?.GetCurrentCamera(0) is { } c
        ? new(c.GetPos(), c.GetOldPos(), c.GetOrientation()) : null;

    public long BlocksMixed() => VirtualVoice.BlocksMixed(Clock);

    internal void RegisterVoiceSample(SampleMetadata sample) => _voiceSamples.Add(sample);
    internal void ReleaseVoiceSample(SampleMetadata sample) => _voiceSamples.Remove(sample);
}
