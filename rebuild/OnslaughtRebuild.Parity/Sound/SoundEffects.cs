// SPDX-License-Identifier: GPL-3.0-or-later
// The sound effects as the sound manager knows them, for the rand() draws playing one makes. Adapted from bea-decomp
// src/SoundManager.cpp (CEffect::LoadSFXFile 004e2530, GetEffectByName 004e2a90, CSoundManager::PlayEffect's choice of
// chained effect and pitch, 004e1910..004e19bb). The samples themselves are presentation.

using System.Text;
using OnslaughtRebuild.Parity.Crt;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>CEffect: a named effect, its pitch variance and the effects chained after it (one is picked at random).</summary>
public sealed class SoundEffect(string name, int pitchVariance)
{
    public string Name { get; } = name;

    public int PitchVariance { get; } = pitchVariance;

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
            ReadLine();
            ReadLine();
            ReadLine();
            ReadLine();
            int pitch = Atoi(ReadLine());
            if (version >= 101)
            {
                ReadLine();
            }

            if (version >= 103)
            {
                ReadLine();
            }

            ReadLine();
            var e = new SoundEffect(name, pitch);
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
