// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPBEConfig.cpp (CFEPBEConfig::Init and Load, GetCurrentLevel) and src/FEPWingmen.cpp
// (GetNumWingmen) (reconstructed from the retail program; not in Stuart's release).

using System.Text;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>One level's entry in <c>data\WorldHeaders.dat</c> (<c>SBEConfigLevel</c>): its Battle Engine configurations and wingmen.</summary>
public sealed record WorldHeader(int Level, IReadOnlyList<string> Configurations, bool Tara, bool Billy, bool Jason, int Unknown10)
{
    /// <summary><c>CFEPWingmen::GetNumWingmen</c>: Tara, Jason and Billy, each counted when offered.</summary>
    public int NumWingmen => (Tara ? 1 : 0) + (Jason ? 1 : 0) + (Billy ? 1 : 0);
}

/// <summary>
/// <c>data\WorldHeaders.dat</c>, which the configuration and wingmen pages read at front-end start: a version and a count,
/// then each level's version, size (unused), level number, configuration names (a signed length byte, then the
/// characters), and from version 2 the three wingmen flags, from version 3 one more word.
/// </summary>
public sealed class WorldHeaders
{
    public const string FileName = @"data\WorldHeaders.dat";

    private WorldHeaders(IReadOnlyList<WorldHeader> levels) => Levels = levels;

    public IReadOnlyList<WorldHeader> Levels { get; }

    public static WorldHeaders Read(MemBuffer file)
    {
        file.ReadInt32();
        int count = file.ReadInt32();
        List<WorldHeader> levels = [];
        for (int i = 0; i < count; i++)
        {
            int version = file.ReadInt32();
            file.ReadInt32();
            int level = file.ReadInt32();
            int configCount = file.ReadInt32();
            List<string> configs = [];
            for (int c = 0; c < configCount; c++)
            {
                int length = (sbyte)file.ReadByte();
                if (length < 0) throw new NotSupportedException($"A configuration name of length {length} is not supported.");
                byte[] name = new byte[length];
                file.Read(name);
                int end = Array.IndexOf(name, (byte)0);
                configs.Add(Encoding.Latin1.GetString(name, 0, end < 0 ? length : end));
            }
            bool tara = true, billy = false, jason = false;
            int unknown10 = 0;
            if (version >= 2)
            {
                tara = file.ReadInt32() != 0;
                billy = file.ReadInt32() != 0;
                jason = file.ReadInt32() != 0;
            }
            if (version >= 3) unknown10 = file.ReadInt32();
            levels.Add(new WorldHeader(level, configs, tara, billy, jason, unknown10));
        }
        return new WorldHeaders(levels);
    }

    /// <summary><c>GetCurrentLevel</c> (0x00451a40): the first entry for the level, or null.</summary>
    public WorldHeader? ForLevel(int level) => Levels.FirstOrDefault(header => header.Level == level);
}
