// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Career.cpp (CCareer::Save, Load, SaveAndMarkInProgress, SizeOfSaveGame, WriteOptions,
// ReadOptions).

using System.Buffers.Binary;

namespace OnslaughtRebuild.Parity.Career;

/// <summary>
/// The PC career save and options file, as <c>CCareer::Save</c> (0x00421350) writes it: a 16-bit version stamp, the
/// career's 9,404 bytes of memory, the 16 configurable key bindings as raw 32-byte records, and the 86 bytes of PC
/// options (<c>CCareer::WriteOptions</c> 0x00420b10). Careers (<c>savegames/*.bes</c>) and
/// <c>defaultoptions.bea</c> share it.
/// </summary>
public static class CareerSaveGame
{
    public const int CareerVersion = 17;
    public const int BindingCount = 16;
    public const int BindingSize = 32;
    public const int BindingsSize = BindingCount * BindingSize;

    /// <summary><c>CCareer::SizeOfSaveGame</c> (0x00421430): 2 + 9,404 + 16 × 32 + 86.</summary>
    public const int Size = 2 + CareerRecord.Size + BindingsSize + CareerOptions.Size;

    private const int CareerOffset = 2;
    private const int BindingsOffset = CareerOffset + CareerRecord.Size;
    private const int OptionsOffset = BindingsOffset + BindingsSize;

    /// <summary><c>current_version_stamp</c>: CAREER_VERSION + (sizeof(CCareer) &lt;&lt; 4), kept as a 16-bit word (0x4BD1).</summary>
    public static short VersionStamp => unchecked((short)(CareerVersion + (CareerRecord.Size << 4)));

    /// <summary><c>CCareer::Save</c>: the stamp, the career memory, the bindings, then the options.</summary>
    public static byte[] Save(Career career, ReadOnlySpan<byte> bindings, CareerOptions options)
    {
        if (bindings.Length != BindingsSize)
            throw new ArgumentException($"The configurable bindings are {BindingsSize} bytes, not {bindings.Length}.", nameof(bindings));
        byte[] file = new byte[Size];
        BinaryPrimitives.WriteInt16LittleEndian(file, VersionStamp);
        career.Record.Bytes.CopyTo(file.AsSpan(CareerOffset));
        bindings.CopyTo(file.AsSpan(BindingsOffset));
        options.Write(file.AsSpan(OptionsOffset, CareerOptions.Size));
        return file;
    }

    /// <summary><c>CCareer::SaveAndMarkInProgress</c> (0x004213c0): a saved career counts as in progress.</summary>
    public static byte[] SaveAndMarkInProgress(Career career, ReadOnlySpan<byte> bindings, CareerOptions options)
    {
        career.Record.CareerInProgress = 1;
        return Save(career, bindings, options);
    }

    /// <summary>
    /// <c>CCareer::Load</c> (0x00421200). A wrong version stamp refuses the load and changes nothing. Otherwise the
    /// career memory is copied in and an out-of-range screen position is reset. A load that suggests the next level
    /// (loading a career to play) keeps the volumes in use and does not read the bindings or options; any other load
    /// (the options file) returns them for the caller to apply, along with the career's volumes.
    /// </summary>
    public static CareerLoadResult Load(Career career, ReadOnlySpan<byte> source, bool suggestNextLevel)
    {
        int needed = suggestNextLevel ? CareerOffset + CareerRecord.Size : Size;
        if (source.Length < needed)
            throw new ArgumentException($"A career load reads {needed} bytes; only {source.Length} were supplied.", nameof(source));
        if (BinaryPrimitives.ReadInt16LittleEndian(source) != VersionStamp) return CareerLoadResult.WrongVersion;

        float musicVolume = career.Record.MusicVolume;
        float soundVolume = career.Record.SoundVolume;
        career.Record.CopyFrom(source.Slice(CareerOffset, CareerRecord.Size));

        int x = career.GetScreenPosX();
        int y = career.GetScreenPosY();
        if (x < -64 || x > 64) x = 0;
        if (y < -64 || y > 64) y = 0;
        career.SetScreenPosX(x);
        career.SetScreenPosY(y);

        if (suggestNextLevel)
        {
            career.Record.MusicVolume = musicVolume;
            career.Record.SoundVolume = soundVolume;
            return new CareerLoadResult(true, null, null);
        }
        return new CareerLoadResult(true, source.Slice(BindingsOffset, BindingsSize).ToArray(),
            CareerOptions.Read(source.Slice(OptionsOffset, CareerOptions.Size)));
    }
}

/// <summary>
/// The outcome of <see cref="CareerSaveGame.Load"/>. <see cref="Bindings"/> and <see cref="Options"/> are present only
/// for a load that reads them (not one that suggests the next level).
/// </summary>
public sealed record CareerLoadResult(bool Loaded, byte[]? Bindings, CareerOptions? Options)
{
    public static readonly CareerLoadResult WrongVersion = new(false, null, null);
}

/// <summary>
/// The 86 bytes of PC options after the bindings, field by field in <c>CCareer::WriteOptions</c>' order. Several
/// game variables are floats saved as their raw bits; they are kept as raw words here so a value round-trips exactly.
/// </summary>
public sealed record CareerOptions(
    float UnknownOption,
    float MouseSensitivity,
    short ControlPreset,
    short Language,
    int TreeMeshDistanceBits,
    float MeshLodBias,
    int TrailDetailBits,
    int BlobShadowFadeDistance,
    int LandscapeLowResGeometry,
    int ScreenShape,
    int DisallowMipmapping,
    uint DisplayModeKey,
    int VSync,
    int LandscapeMaxLevels,
    int TextureResolutionLossShift,
    int TextureAllow32Bit,
    int MultiSampleType,
    int FlipSpeakers,
    int HardwareSound,
    int SoundRate,
    int SoundDevice,
    int Sound3DMethod,
    byte Shadows,
    byte BlobShadows)
{
    /// <summary><c>OPTIONS_SIZE</c>.</summary>
    public const int Size = 0x56;

    /// <summary>The display mode key's width (bits 0-15), height (bits 16-30) and 32-bit flag (bit 31).</summary>
    public int DisplayWidth => (int)(DisplayModeKey & 0xffff);
    public int DisplayHeight => (int)((DisplayModeKey >> 16) & 0x7fff);
    public bool Display32Bit => (DisplayModeKey >> 31) != 0;

    public static CareerOptions Read(ReadOnlySpan<byte> s)
    {
        if (s.Length < Size) throw new ArgumentException($"The options are {Size} bytes.", nameof(s));
        return new CareerOptions(
            BinaryPrimitives.ReadSingleLittleEndian(s[0..]), BinaryPrimitives.ReadSingleLittleEndian(s[4..]),
            BinaryPrimitives.ReadInt16LittleEndian(s[8..]), BinaryPrimitives.ReadInt16LittleEndian(s[10..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[12..]), BinaryPrimitives.ReadSingleLittleEndian(s[16..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[20..]), BinaryPrimitives.ReadInt32LittleEndian(s[24..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[28..]), BinaryPrimitives.ReadInt32LittleEndian(s[32..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[36..]), BinaryPrimitives.ReadUInt32LittleEndian(s[40..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[44..]), BinaryPrimitives.ReadInt32LittleEndian(s[48..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[52..]), BinaryPrimitives.ReadInt32LittleEndian(s[56..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[60..]), BinaryPrimitives.ReadInt32LittleEndian(s[64..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[68..]), BinaryPrimitives.ReadInt32LittleEndian(s[72..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[76..]), BinaryPrimitives.ReadInt32LittleEndian(s[80..]),
            s[84], s[85]);
    }

    public void Write(Span<byte> d)
    {
        if (d.Length < Size) throw new ArgumentException($"The options are {Size} bytes.", nameof(d));
        BinaryPrimitives.WriteSingleLittleEndian(d[0..], UnknownOption);
        BinaryPrimitives.WriteSingleLittleEndian(d[4..], MouseSensitivity);
        BinaryPrimitives.WriteInt16LittleEndian(d[8..], ControlPreset);
        BinaryPrimitives.WriteInt16LittleEndian(d[10..], Language);
        BinaryPrimitives.WriteInt32LittleEndian(d[12..], TreeMeshDistanceBits);
        BinaryPrimitives.WriteSingleLittleEndian(d[16..], MeshLodBias);
        BinaryPrimitives.WriteInt32LittleEndian(d[20..], TrailDetailBits);
        BinaryPrimitives.WriteInt32LittleEndian(d[24..], BlobShadowFadeDistance);
        BinaryPrimitives.WriteInt32LittleEndian(d[28..], LandscapeLowResGeometry);
        BinaryPrimitives.WriteInt32LittleEndian(d[32..], ScreenShape);
        BinaryPrimitives.WriteInt32LittleEndian(d[36..], DisallowMipmapping);
        BinaryPrimitives.WriteUInt32LittleEndian(d[40..], DisplayModeKey);
        BinaryPrimitives.WriteInt32LittleEndian(d[44..], VSync);
        BinaryPrimitives.WriteInt32LittleEndian(d[48..], LandscapeMaxLevels);
        BinaryPrimitives.WriteInt32LittleEndian(d[52..], TextureResolutionLossShift);
        BinaryPrimitives.WriteInt32LittleEndian(d[56..], TextureAllow32Bit);
        BinaryPrimitives.WriteInt32LittleEndian(d[60..], MultiSampleType);
        BinaryPrimitives.WriteInt32LittleEndian(d[64..], FlipSpeakers);
        BinaryPrimitives.WriteInt32LittleEndian(d[68..], HardwareSound);
        BinaryPrimitives.WriteInt32LittleEndian(d[72..], SoundRate);
        BinaryPrimitives.WriteInt32LittleEndian(d[76..], SoundDevice);
        BinaryPrimitives.WriteInt32LittleEndian(d[80..], Sound3DMethod);
        d[84] = Shadows;
        d[85] = BlobShadows;
    }
}
