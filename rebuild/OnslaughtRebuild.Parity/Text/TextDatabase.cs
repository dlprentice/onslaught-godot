// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/text.cpp and src/text.h (CText; reconstructed from the retail program, not in Stuart's
// release).

using System.Buffers.Binary;
using System.Text;

namespace OnslaughtRebuild.Parity.Text;

/// <summary>
/// The loaded language file (<c>CText</c>, <c>TEXT</c>): <c>data\LANGUAGE\&lt;language&gt;.DAT</c>, strings looked up
/// by hashed id. The retail files are version 3 (a 0xffffffbb marker, the version, the entry count, then per entry the
/// id, the text's offset in UTF-16 units and the sample's offset, then the text pool's length and the pool).
/// </summary>
public sealed class TextDatabase
{
    private readonly byte[] _file;
    private readonly int _entries;
    private readonly int _stride;
    private readonly int _textStart;

    private TextDatabase(byte[] file, int format, int entries, int textStart)
    {
        _file = file;
        Format = format;
        _entries = entries;
        _textStart = textStart;
        _stride = format == 1 ? 2 : 3;
    }

    /// <summary><c>mFormat</c>: 1, 2 or 3 (format 0, the unversioned 8-bit table, is not used by the PC's files).</summary>
    public int Format { get; }

    /// <summary><c>CText::GetLanguageName</c>: the file's name for a language (0 English, 1 French, 2 German, 3 Spanish, 4 Italian).</summary>
    public static string FileName(int language, bool americanText) => americanText
        ? @"data\LANGUAGE\american.DAT"
        : $@"data\LANGUAGE\{language switch { 1 => "french", 2 => "german", 3 => "spanish", 4 => "italian", _ => "english" }}.DAT";

    /// <summary><c>CText::Load</c> (0x004f21f0) for one file's bytes.</summary>
    public static TextDatabase Load(byte[] file)
    {
        uint marker = Word(file, 0);
        if (marker != 0xffffffbb) throw new NotSupportedException("The unversioned (format 0) text file is not supported.");
        uint version = Word(file, 1) & 0x7fffffff;
        int entries = (int)Word(file, 2);
        return version switch
        {
            1 => new TextDatabase(file, 1, entries, (entries * 2 + 3) * 4),
            2 or 3 => new TextDatabase(file, (int)version, entries, (entries * 3 + 4) * 4),
            _ => throw new NotSupportedException($"Unknown text file version {version}."),
        };
    }

    private static uint Word(byte[] file, int index) => BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(index * 4));

    /// <summary>
    /// <c>CText::GetStringById</c> (0x004f2580): the first entry with the id; an unknown id gives the pool's first string.
    /// </summary>
    public string GetStringById(uint id)
    {
        for (int entry = 0; entry < _entries; entry++)
        {
            int at = 3 + entry * _stride;
            if (Word(_file, at) == id) return StringAt((int)Word(_file, at + 1));
        }
        return StringAt(0);
    }

    private string StringAt(int units)
    {
        int start = _textStart + units * 2;
        int end = start;
        while (end + 1 < _file.Length && (_file[end] | _file[end + 1]) != 0) end += 2;
        return Encoding.Unicode.GetString(_file, start, end - start);
    }
}

/// <summary>
/// The game's text ids by name, from <c>data\MissionScripts\text\text.stf</c>: the header the game's code includes
/// (<c>#define NAME id</c>, ids in decimal). Read from the player's installation, never copied.
/// </summary>
public sealed class TextIds
{
    public const string FileName = @"data\MissionScripts\text\text.stf";

    private readonly Dictionary<string, uint> _ids;

    private TextIds(Dictionary<string, uint> ids) => _ids = ids;

    public static TextIds Read(byte[] file)
    {
        Dictionary<string, uint> ids = new(StringComparer.Ordinal);
        foreach (string line in Encoding.Latin1.GetString(file).Split('\n'))
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0] == "#define" && uint.TryParse(parts[2], out uint id)) ids.TryAdd(parts[1], id);
        }
        return new TextIds(ids);
    }

    public uint this[string name] =>
        _ids.TryGetValue(name, out uint id) ? id : throw new KeyNotFoundException($"{name} is not in {FileName}.");
}
