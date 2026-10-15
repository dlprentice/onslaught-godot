// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The test host's PC texts: <c>Localization_GetStringById</c> (0x00524830) read from the player's own BEA.exe, so no
/// program string is carried in source. The function switches on the language (a jump table at 0x00526098), then on the
/// id (<c>cmp eax, max; ja; jmp [eax*4 + table]</c>); each case is <c>mov eax, string; ret</c>.
/// </summary>
internal sealed class RetailPcText(byte[] exe, int language)
{
    private const uint LanguageTable = 0x00526098;

    private int Offset(uint va)
    {
        int pe = BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(0x3c));
        int sections = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe + 6));
        int optional = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe + 20));
        uint imageBase = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(pe + 24 + 28));
        uint rva = va - imageBase;
        for (int i = 0; i < sections; i++)
        {
            int h = pe + 24 + optional + (i * 40);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(h + 8));
            uint start = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(h + 12));
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(h + 20));
            if (rva >= start && rva < start + size)
            {
                return (int)(raw + (rva - start));
            }
        }

        throw new InvalidDataException($"address {va:x8} is in no section");
    }

    private uint Dword(uint va) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Offset(va)));

    public string this[int id]
    {
        get
        {
            uint branch = Dword(LanguageTable + (uint)(language * 4));
            int at = Offset(branch);
            // cmp eax, imm32 (3d); ja rel32 (0f 87); jmp [eax*4 + disp32] (ff 24 85)
            if (exe[at] != 0x3d || exe[at + 5] != 0x0f || exe[at + 6] != 0x87 || exe[at + 11] != 0xff || exe[at + 12] != 0x24
                || exe[at + 13] != 0x85)
            {
                throw new InvalidDataException("Localization_GetStringById's language branch is not in the expected form");
            }

            uint max = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at + 1));
            uint table = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at + 14));
            if ((uint)id > max)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            int c = Offset(Dword(table + (uint)(id * 4)));
            if (exe[c] != 0xb8)
            {
                throw new InvalidDataException($"text {id:x}'s case is not a string");
            }

            int s = Offset(BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(c + 1)));
            int end = s;
            while (exe[end] != 0 || exe[end + 1] != 0)
            {
                end += 2;
            }

            return Encoding.Unicode.GetString(exe, s, end - s);
        }
    }
}
