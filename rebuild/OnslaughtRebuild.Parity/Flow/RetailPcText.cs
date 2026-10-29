// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>
/// The hosts' PC texts: <c>Localization_GetStringById</c> (0x00524830) read from the player's own BEA.exe, so no
/// program string is carried in source. The function switches on the language (a jump table at 0x00526098), then on the
/// id (a compare of eax with max, branching when above, then jump-table
/// dispatch); each case is a load of string into eax, then return. The code shapes are recognised by SHA-256, so no
/// program bytes are carried in source either.
/// </summary>
public sealed class RetailPcText(byte[] exe, int language)
{
    private const uint LanguageTable = 0x00526098;

    // Positions, from the start of a language's branch, of the six opcode bytes of the bounds compare, the
    // out-of-range branch and the indexed jump-table dispatch. The values between them (the largest id, the branch
    // displacement and the table address) differ per language and are read below, not checked.
    internal static readonly int[] BranchShapeOffsets = [0, 5, 6, 11, 12, 13];

    // SHA-256 of those six opcode bytes, and of the one opcode byte that opens a text case (a load of an immediate
    // into eax). Taken once from the pristine specimen BEA.exe (SHA-256 74154bfa…7750, read-only); the same digest holds
    // for all five languages.
    private static readonly byte[] BranchShapeSha256 =
        Convert.FromHexString("72f2d8a319507ef3ad0610f7046acd8f1fff09cccc1eac9b544b84f2974ec25b");

    private static readonly byte[] CaseShapeSha256 =
        Convert.FromHexString("d3bb0d59e354ea843e790801303a46e880219996c6850ddd4c85a83e08c41d92");

    /// <summary>The file offset of this language's branch in <c>Localization_GetStringById</c>.</summary>
    internal int BranchOffset() => Offset(Dword(LanguageTable + (uint)(language * 4)));

    private static bool HasShape(ReadOnlySpan<byte> opcodes, byte[] sha256) => SHA256.HashData(opcodes).AsSpan().SequenceEqual(sha256);

    /// <summary>True when the branch at file offset <paramref name="at"/> has the six expected opcode bytes.</summary>
    internal bool BranchHasExpectedShape(int at)
    {
        if (at + BranchShapeOffsets[^1] >= exe.Length)
        {
            return false;
        }

        Span<byte> opcodes = stackalloc byte[BranchShapeOffsets.Length];
        for (int i = 0; i < opcodes.Length; i++)
        {
            opcodes[i] = exe[at + BranchShapeOffsets[i]];
        }

        return HasShape(opcodes, BranchShapeSha256);
    }

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
            int at = BranchOffset();
            if (!BranchHasExpectedShape(at))
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
            if (!HasShape(stackalloc byte[] { exe[c] }, CaseShapeSha256))
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
