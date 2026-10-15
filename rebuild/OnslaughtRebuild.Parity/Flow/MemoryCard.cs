// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/PCMemoryCard.cpp (CPCMemoryCard).

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>
/// The save folder (<c>savegames\</c>) as the PC memory card reads it: the careers' names (without <c>.bes</c>) in the
/// order the file system lists them (<c>_findfirst</c>), and their bytes. The host supplies a real folder; tests use
/// <see cref="InMemorySaveFolder"/>.
/// </summary>
public interface ISaveFolder
{
    /// <summary>The saves' names, in listing order.</summary>
    IReadOnlyList<string> Names { get; }

    bool Exists(string name);
    byte[] Read(string name);

    /// <summary>Creates or replaces a save with these bytes (an empty file when <paramref name="bytes"/> is empty).</summary>
    void Write(string name, ReadOnlySpan<byte> bytes);

    bool Delete(string name);
}

/// <summary>A save folder held in memory, listing saves in the order they were first created.</summary>
public sealed class InMemorySaveFolder : ISaveFolder
{
    private readonly List<string> _names = [];
    // Windows file names are case-insensitive (beyond ASCII too), so the stand-in folder is.
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Names => _names;

    public bool Exists(string name) => _files.ContainsKey(name);

    public byte[] Read(string name) => _files[name].ToArray();

    public void Write(string name, ReadOnlySpan<byte> bytes)
    {
        if (!_files.ContainsKey(name)) _names.Add(name);
        _files[name] = bytes.ToArray();
    }

    public bool Delete(string name)
    {
        if (!_files.Remove(name)) return false;
        _names.RemoveAll(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
        return true;
    }
}

/// <summary>
/// <c>CPCMemoryCard</c>: the PC has one "card", always present and formatted with all the space it wants; its saves are
/// the save folder's files. Return codes are the game's: <see cref="Success"/>, <see cref="FileExists"/>, or 1 for a
/// failure.
/// </summary>
public sealed class MemoryCard(ISaveFolder folder)
{
    public const int Success = 0;
    public const int Failure = 1;

    /// <summary><c>MCE_FILEEXISTS</c>.</summary>
    public const int FileExists = 6;

    public ISaveFolder Folder { get; } = folder;

    /// <summary><c>Update</c>: the PC's cards never change.</summary>
    public bool Update() => false;

    /// <summary><c>IsHDDAvailable</c>: false on PC.</summary>
    public bool IsHddAvailable => false;

    /// <summary><c>GetNumCards</c> (0x00514950): one.</summary>
    public int NumCards => 1;

    /// <summary><c>GetCardInfo</c> (0x00514960): present, formatted, 0x7fffffff bytes free of 0x7fffffff.</summary>
    public (bool Present, bool Formatted, int BytesFree, int BytesTotal) GetCardInfo(int card) => (true, true, 0x7fffffff, 0x7fffffff);

    /// <summary><c>GetNumSaves</c> (0x005149c0).</summary>
    public int NumSaves => Folder.Names.Count;

    /// <summary><c>GetSaveName</c> (0x00514a80): the save's name, or null past the end.</summary>
    public string? GetSaveName(int save) => save >= 0 && save < Folder.Names.Count ? Folder.Names[save] : null;

    /// <summary><c>GetSaveSize</c> (0x00515190): a save takes exactly its data.</summary>
    public int GetSaveSize(int dataSize) => dataSize;

    /// <summary>
    /// <c>CreateSave</c> (0x00514be0): unless overwriting is allowed, an existing save refuses with
    /// <see cref="FileExists"/>; otherwise the file is created empty and its index among the saves found
    /// (as <c>_wcsicmp</c> compares).
    /// </summary>
    public int CreateSave(string name, bool allowOverwrite, out int save)
    {
        save = -1;
        if (!allowOverwrite && Folder.Exists(name)) return FileExists;
        Folder.Write(name, ReadOnlySpan<byte>.Empty);
        for (int index = 0; index < Folder.Names.Count; index++)
        {
            if (WcsIEquals(Folder.Names[index], name))
            {
                save = index;
                return Success;
            }
        }
        return Failure;
    }

    /// <summary>
    /// <c>_wcsicmp</c>'s equality in the C locale (the game never sets another): only ASCII A-Z fold to lower case;
    /// every other character compares exactly.
    /// </summary>
    public static bool WcsIEquals(string? a, string? b)
    {
        if (a is null || b is null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (Fold(a[i]) != Fold(b[i])) return false;
        }
        return true;

        static char Fold(char c) => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
    }

    /// <summary><c>WriteSave</c> (0x00514f80).</summary>
    public int WriteSave(string name, ReadOnlySpan<byte> data)
    {
        Folder.Write(name, data);
        return Success;
    }

    /// <summary><c>ReadSave</c> (0x00515080): succeeds only when exactly <paramref name="dataSize"/> bytes were read.</summary>
    public int ReadSave(string name, int dataSize, out byte[] data)
    {
        data = [];
        if (!Folder.Exists(name)) return Failure;
        byte[] all = Folder.Read(name);
        data = all.Length > dataSize ? all[..dataSize] : all;
        return data.Length == dataSize ? Success : Failure;
    }

    /// <summary><c>DeleteSave</c> (0x00514ec0).</summary>
    public int DeleteSave(string name) => Folder.Delete(name) ? Success : Failure;
}
