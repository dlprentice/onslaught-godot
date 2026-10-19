// SPDX-License-Identifier: GPL-3.0-or-later
// The sound effects as the sound manager knows them, for the rand() draws playing one makes. Adapted from bea-decomp
// src/SoundManager.cpp (CEffect::LoadSFXFile 004e2530, GetEffectByName 004e2a90, CSoundManager::PlayEffect's choice of
// chained effect and pitch, 004e1910..004e19bb). The samples themselves are presentation.

using System.Text;
using OnslaughtRebuild.Parity.Crt;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>CEffect: a named effect, its pitch variance and the effects chained after it (one is picked at random).</summary>
public sealed class SoundEffect(string name, int pitchVariance, string? sampleName = null, bool looping = false)
{
    public string Name { get; } = name;

    public string SampleName { get; } = sampleName ?? name;

    public int PitchVariance { get; } = pitchVariance;

    public bool Looping { get; } = looping;

    public SoundEffect? Chained;
}

/// <summary>The effects of data\sounds\sounds.sfx, a later effect of an earlier one's name chained after it.</summary>
public sealed class SoundEffects
{
    public const string FileName = @"data\sounds\sounds.sfx";

    private readonly List<SoundEffect> _effects = [];

    /// <summary>CPCSoundManager::GetPitchVarianceEnabled: hardware sound only, which the configuration leaves off.</summary>
    public bool PitchVarianceEnabled { get; init; }

    public int Count => _effects.Count;

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
            ReadLine();
            ReadLine();
            ReadLine();
            int pitch = Atoi(ReadLine());
            bool looping = version >= 101 && ReadLine().StartsWith('1');

            if (version >= 103)
            {
                ReadLine();
            }

            ReadLine();
            var e = new SoundEffect(name, pitch, sample, looping);
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

    /// <summary>
    /// Silent logical state for explicitly repeating effects only. This does not model channel allocation, device
    /// audibility, nonloop sample durations or callbacks. Its owner must call AdvanceLoopStatus once per MainLoop's
    /// SOUND.UpdateStatus boundary, including the original pause state; never once per simulated movement.
    /// </summary>
    public sealed class RepeatingEffect(SoundEffect effect, object owner, float fade)
    {
        public SoundEffect Effect { get; } = effect;
        public object Owner { get; } = owner;
        public float SubVolume { get; internal set; } = fade > 0 ? 0 : 1;
        public float Fade { get; internal set; } = fade;
        public float FadeDestination { get; internal set; } = fade < 0 ? 0 : 1;
    }

    private readonly List<RepeatingEffect> _repeating = [];
    public IReadOnlyList<RepeatingEffect> RepeatingEffects => _repeating;

    /// <summary>IsEffectPlaying004e1ab0: test every chained sample with the same owner.</summary>
    public bool IsRepeatingEffectPlaying(SoundEffect? effect, object owner)
    {
        for (SoundEffect? current = effect; current is not null; current = current.Chained)
            if (FindRepeatingSample(current.SampleName, owner) is not null) return true;
        return false;
    }

    public RepeatingEffect? FindRepeatingSample(string sample, object owner) => _repeating.FirstOrDefault(e =>
        ReferenceEquals(e.Owner, owner) && StringComparer.OrdinalIgnoreCase.Equals(e.Effect.SampleName, sample));

    /// <summary>
    /// PlayEffect004e1940 chooses the effect and draws pitch through the existing owner. The caller guarantees
    /// an admitted repeating sample. Optional once admission occurs AFTER chain/pitch draws and applies only when
    /// the selected effect is not marked looping (004e1987..004e19b1, 004e0b6c..004e0b94). Device channels, sample
    /// availability and nonloop playback remain outside this loop-only model.
    /// </summary>
    public RepeatingEffect? PlayRepeatingEffect(SoundEffect? effect, object owner, CrtRandStream rand, float fade = 0, bool once = false)
    {
        SoundEffect? chosen = PlayEffect(effect, rand);
        if (chosen is null) return null;
        if (once && !chosen.Looping && FindRepeatingSample(chosen.SampleName, owner) is { } existing) return existing;
        var playing = new RepeatingEffect(chosen, owner, fade);
        _repeating.Add(playing);
        return playing;
    }

    /// <summary>StopSample004e1800: every matching sample of this owner, no random draw.</summary>
    public void StopRepeatingSample(SoundEffect? effect, object owner)
    {
        if (effect is null) return;
        _repeating.RemoveAll(e => ReferenceEquals(e.Owner, owner)
            && StringComparer.OrdinalIgnoreCase.Equals(e.Effect.SampleName, effect.SampleName));
    }

    /// <summary>FadeTo004e1260: the first matching sample, signed speed from its current volume.</summary>
    public void FadeRepeatingSample(SoundEffect? effect, object owner, float destination, float speed)
    {
        if (effect is null || FindRepeatingSample(effect.SampleName, owner) is not { } playing) return;
        playing.FadeDestination = destination;
        playing.Fade = destination < playing.SubVolume ? -speed : speed;
    }

    /// <summary>
    /// UpdateStatus004e1b20: one addition per unpaused status update, then STRICT overshoot comparisons. Reaching zero
    /// exactly does not stop the sample until the next update; replacing this with a <= comparison changes RNG later.
    /// </summary>
    public void AdvanceLoopStatus(bool paused)
    {
        if (paused) return;
        for (int i = 0; i < _repeating.Count;)
        {
            RepeatingEffect effect = _repeating[i];
            if (effect.Fade != 0)
            {
                effect.SubVolume += effect.Fade;
                if ((effect.Fade > 0 && effect.SubVolume > effect.FadeDestination)
                    || (effect.Fade < 0 && effect.SubVolume < effect.FadeDestination))
                {
                    bool stop = effect.Fade < 0 && effect.FadeDestination == 0;
                    effect.SubVolume = effect.FadeDestination;
                    effect.Fade = 0;
                    if (stop) { _repeating.RemoveAt(i); continue; }
                }
            }
            i++;
        }
    }

    /// <summary>PlayEffect's draws: one of the chain at random, then (with pitch variance on) its pitch.</summary>
    public SoundEffect? PlayEffect(SoundEffect? effect, CrtRandStream rand)
    {
        if (effect is null)
        {
            return null;
        }

        int chained = 0;
        for (SoundEffect? c = effect; c is not null; c = c.Chained)
        {
            chained++;
        }

        int pick = rand.Rand() % chained;
        SoundEffect current = effect;
        for (; pick > 0; pick--)
        {
            current = current.Chained!;
        }

        if (current.PitchVariance != 0 && PitchVarianceEnabled)
        {
            rand.Rand();
        }

        return current;
    }
}
