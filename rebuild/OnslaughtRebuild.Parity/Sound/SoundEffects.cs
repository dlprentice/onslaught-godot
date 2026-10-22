// SPDX-License-Identifier: GPL-3.0-only
// Effect metadata/selection and the explicit old-loop adapter. SoundEffects.Events owns event state. Adapted from onslaught-decomp
// src/SoundManager.cpp (CEffect::LoadSFXFile 004e2530, GetEffectByName 004e2a90, CSoundManager::PlayEffect's choice of
// chained effect and pitch, 004e1910..004e19bb). Event lifetime is simulation state; output audio is presentation.

using System.Text;
using OnslaughtRebuild.Parity.Crt;
using SoundOwner = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>CEffect: named sample, authored gain/language/loop flags and a randomly selected effect chain.</summary>
public sealed class SoundEffect(string name, int pitchVariance, string? sampleName = null, bool looping = false, int volume = 100, bool languageDependent = false)
{
    public string Name { get; } = name;

    public string SampleName { get; } = sampleName ?? name;

    public int PitchVariance { get; } = pitchVariance;

    public bool Looping { get; } = looping;

    public int Volume { get; } = volume;

    public bool LanguageDependent { get; } = languageDependent;

    public SoundEffect? Chained;
}

/// <summary>The effects of data\sounds\sounds.sfx, a later effect of an earlier one's name chained after it.</summary>
public sealed partial class SoundEffects
{
    public const string FileName = @"data\sounds\sounds.sfx";

    private readonly List<SoundEffect> _effects = [];

    /// <summary>CPCSoundManager::GetPitchVarianceEnabled: hardware sound only, which the configuration leaves off.</summary>
    public bool PitchVarianceEnabled { get; init; }

    public int Count => _effects.Count;

    /// <summary>
    /// CSoundManager::mGameSoundsMasterVolume: the level-loss fade's separate gain, initially one. This retains
    /// manager state only; native callers separately request UpdateVolumeForAllSoundEvents when required.
    /// </summary>
    public float GameSoundsMasterVolume { get; private set; } = 1.0f;

    public void SetGameSoundsMasterVolume(float value) => GameSoundsMasterVolume = value;

    /// <summary>PC Init004e0210/004e0217; independent console-adjustable gains, not the global volume.</summary>
    public float RadioMessageVolume { get; set; } = .42f;
    public float HudMessageVolume { get; set; } = .45f;

    /// <summary>The process's loaded sample banks, retaining sample object identity and language selection.</summary>
    public Func<string, bool, SampleMetadata?>? SampleLookup { get; set; }

    /// <summary>Configured gameplay caller. Volume is explicit: PlayEffect defaults to 1, PlaySample to .7.</summary>
    public SoundEvent? PlayEffect(SoundEffect? effect, SoundOwner? owner, CrtRandStream rand, SoundPlayback playback) =>
        PlayEffect(effect, owner, rand,
            SampleLookup ?? throw new InvalidOperationException("Bind the process's sample banks before playing effects."), playback);

    /// <summary>LoadSFXFile: lines, skipping '#' comment lines, each with its last character (the line end) dropped.</summary>
    public static SoundEffects Load(byte[] file)
    {
        var effects = new SoundEffects();
        string[] lines = Encoding.Latin1.GetString(file).Split('\n');
        int next = 0;
        string ReadLine()
        {
            string line = "#";
            while (line.Length > 0 && line[0] == '#' && next < lines.Length)
            {
                line = lines[next++];
            }

            return line;
        }

        int version = int.Parse(ReadLine().Trim());
        int count = int.Parse(ReadLine().Trim());
        for (int i = 0; i < count; i++)
        {
            string name = ReadLine();
            string sample = ReadLine();
            ReadLine(); // low-pass sample
            int volume = Atoi(ReadLine());
            ReadLine(); // falloff
            int pitch = Atoi(ReadLine());
            bool looping = version >= 101 && ReadLine().StartsWith('1');

            bool languageDependent = version >= 103 && ReadLine().StartsWith('1');

            ReadLine();
            var e = new SoundEffect(name, pitch, sample, looping, volume, languageDependent);
            SoundEffect? found = effects._effects.FirstOrDefault(c => Particles.ParticleSet.Stricmp(c.Name, name) == 0);
            if (found is null)
            {
                effects._effects.Add(e);
            }
            else
            {
                while (found.Chained is not null)
                {
                    found = found.Chained;
                }

                found.Chained = e;
            }
        }

        return effects;
    }

    private static int Atoi(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i]))
        {
            i++;
        }

        int start = i;
        if (i < s.Length && (s[i] == '-' || s[i] == '+'))
        {
            i++;
        }

        while (i < s.Length && char.IsAsciiDigit(s[i]))
        {
            i++;
        }

        return int.TryParse(s[start..i], out int v) ? v : 0;
    }

    /// <summary>GetEffectByName(name, 0): the first effect of that name, ignoring case (doubled backslashes made single).</summary>
    public SoundEffect? Find(string name)
    {
        var sb = new StringBuilder();
        bool inSlash = false;
        foreach (char c in name)
        {
            if (c == '\\')
            {
                if (!inSlash)
                {
                    sb.Append(c);
                    inSlash = true;
                }
            }
            else
            {
                sb.Append(c);
                inSlash = false;
            }
        }

        string s = sb.ToString();
        return _effects.FirstOrDefault(e => Particles.ParticleSet.Stricmp(e.Name, s) == 0);
    }

    /// <summary>Compatibility view over the ONE event owner, not a separate sound lifetime model.</summary>
    public sealed class RepeatingEffect
    {
        internal RepeatingEffect(SoundEvent e) => Event = e;
        internal SoundEvent Event { get; }
        public SoundEffect Effect => Event.Effect!;
        public object? Owner => Event.Owner;
        public float SubVolume => Event.SubVolume;
        public float Fade => Event.Fade;
        public float FadeDestination => Event.FadeDestination;
    }

    public IReadOnlyList<RepeatingEffect> RepeatingEffects => Events.Where(e => e.Playing && e.Looping && e.Effect is not null)
        .Select(e => e.LoopHandle ??= new RepeatingEffect(e)).ToArray();

    /// <summary>Native KillSamplesForThing: exact-owner playing entries; read Next after callback/stop.</summary>
    public void KillSamplesForThing(object? owner)
    {
        for (SoundEvent? e = _first; e is not null;)
        {
            if (e.Playing && ReferenceEquals(e.Owner, owner)) StopSoundEvent(e);
            SoundEvent? next = e.Next;
            if (e.Legacy && !e.Playing) DeleteSoundEvent(e); // explicit legacy stopped-list projection
            e = next;
        }
    }

    public bool IsRepeatingEffectPlaying(SoundEffect? effect, object owner) => IsEffectPlaying(effect, owner);

    public RepeatingEffect? FindRepeatingSample(string sample, object owner)
    {
        SoundEvent? e = Events.FirstOrDefault(e => e.Playing && e.Looping && ReferenceEquals(e.Owner, owner)
            && StringComparer.OrdinalIgnoreCase.Equals(e.Sample.Name, sample));
        return e is null ? null : e.LoopHandle ??= new RepeatingEffect(e);
    }

    /// <summary>
    /// Existing loop-only projection, on the same event nodes. It deliberately has no device/channel/sample-duration
    /// claim. A configured manager instead delegates to the actual event kernel with explicit native arguments.
    /// </summary>
    public RepeatingEffect? PlayRepeatingEffect(SoundEffect? effect, object owner, CrtRandStream rand, float fade = 0, bool once = false)
    {
        if (EventKernelConfigured)
        {
            if (owner is not SoundOwner audible) throw new ArgumentException("A sound owner must support monitored lifetime.", nameof(owner));
            SoundEvent? active = PlayEffect(effect, audible, rand, new SoundPlayback(Volume: 1, Fade: fade, Once: once, Repeat: true));
            return active is null ? null : active.LoopHandle ??= new RepeatingEffect(active);
        }
        RequireLegacyProjection();
        SoundEffect? chosen = PlayEffect(effect, rand);
        if (chosen is null) return null;
        if (once && !chosen.Looping && FindRepeatingSample(chosen.SampleName, owner) is { } existing) return existing;
        if (!_legacySamples.TryGetValue(chosen.SampleName, out SampleMetadata? sample))
            _legacySamples.Add(chosen.SampleName, sample = new SampleMetadata(chosen.SampleName, 0));
        var e = new SoundEvent(this, -1) { Legacy = true, LegacyOwner = owner, Sample = sample, Effect = chosen,
            Playing = true, Looping = true, Channel = -1, Fade = fade, SubVolume = fade > 0 ? 0 : 1,
            FadeDestination = fade < 0 ? 0 : 1, InActiveList = true };
        SoundEvent? last = _first;
        if (last is null) _first = e;
        else { while (last.Next is not null) last = last.Next; last.Next = e; e.Previous = last; }
        _eventCount++;
        return e.LoopHandle = new RepeatingEffect(e);
    }

    public void StopRepeatingSample(SoundEffect? effect, object owner)
    {
        if (effect is null) return;
        StopSample(effect.SampleName, owner);
        for (SoundEvent? e = _first; e is not null;)
        { var next = e.Next; if (e.Legacy && !e.Playing) DeleteSoundEvent(e); e = next; }
    }

    public void FadeRepeatingSample(SoundEffect? effect, object owner, float destination, float speed)
    {
        if (EventKernelConfigured)
        {
            // BE callers first GetSoundEventForThing(name,owner), then pass that event's sample to FadeTo.
            // A later bank lookup could return a different same-named sample after reloading.
            if (effect is not null && FindPlayingSample(effect.SampleName, owner) is { } active)
                FadeTo(active.Sample, destination, speed, owner);
            return;
        }
        if (effect is null || FindRepeatingSample(effect.SampleName, owner) is not { } handle) return;
        handle.Event.FadeDestination = destination;
        handle.Event.Fade = destination < handle.Event.SubVolume ? -speed : speed;
    }

    public void AdvanceLoopStatus(bool paused)
    {
        RequireLegacyProjection();
        if (paused) return;
        for (SoundEvent? e = _first; e is not null;)
        {
            if (e.Fade != 0) AdvanceFade(e);
            var next = e.Next;
            if (!e.Playing) DeleteSoundEvent(e);
            e = next;
        }
    }

    /// <summary>Compatibility selection-only entry point; the configured kernel requires full playback arguments.</summary>
    public SoundEffect? PlayEffect(SoundEffect? effect, CrtRandStream rand)
    {
        RequireLegacyProjection();
        if (!Initialised || effect is null) return null;
        var playback = new SoundPlayback();
        return ChooseEffect(effect, rand, ref playback);
    }
}
