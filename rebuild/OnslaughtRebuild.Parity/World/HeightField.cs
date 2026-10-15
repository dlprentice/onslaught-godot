// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/heightfield.h and src/HeightField.cpp (CHeightField: GetHeightSample 0047ea20,
// Unknown47eb00 0047eb00, Collide 0047eb80, Normal 0047ec60, Load 0047f750). Not in Stuart's release.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.World;

/// <summary>
/// The level's height field: a raw 0x13dc-byte record (the serialized base of CMap) and 64 x 64 tiles of 9 x 9 signed
/// 16-bit heights, scaled by the record's height scale. Ground height comes from bilinear interpolation in 8.8 fixed
/// point, reached through a float bit trick; the normal from the triangle of the sample square under the position.
/// </summary>
public sealed class HeightField
{
    /// <summary>sizeof(CHeightField): the record Load reads whole.</summary>
    public const int RecordSize = 0x13dc;

    /// <summary>64 * 64 tiles of 81 samples.</summary>
    public const int SampleCount = 64 * 64 * 81;

    private readonly short[] _heights;

    private HeightField(byte[] record, short[] heights)
    {
        Record = record;
        _heights = heights;
    }

    /// <summary>The record as read (pointers and all); fields are read from it at retail's offsets.</summary>
    public byte[] Record { get; }

    /// <summary>mHeightScale (+0x102c).</summary>
    public float HeightScale => BinaryPrimitives.ReadSingleLittleEndian(Record.AsSpan(0x102c));

    /// <summary>mWaterLevel (+0x1034).</summary>
    public float WaterLevel => BinaryPrimitives.ReadSingleLittleEndian(Record.AsSpan(0x1034));

    /// <summary>mGridShiftX (+0x1038).</summary>
    public int GridShiftX => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0x1038));

    /// <summary>mGridShiftY (+0x103c).</summary>
    public int GridShiftY => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0x103c));

    public ReadOnlySpan<short> Heights => _heights;

    /// <summary>
    /// Load: two chunk headers, the record, one more header, then the heights tile by tile. (Load also rebalances the
    /// sun colour and builds the lighting ramp; those feed rendering only and are not kept here.)
    /// </summary>
    public static HeightField Load(ChunkReader c)
    {
        c.GetNext();
        c.GetNext();
        var record = new byte[RecordSize];
        c.Read(record);
        c.GetNext();
        var heights = new short[SampleCount];
        Span<byte> h = stackalloc byte[2];
        for (int i = 0; i < SampleCount; i++)
        {
            c.Read(h);
            heights[i] = BinaryPrimitives.ReadInt16LittleEndian(h);
        }

        return new HeightField(record, heights);
    }

    /// <summary>GetHeightSample (0047ea20): the raw sample at a sample coordinate, with retail's edge rows.</summary>
    public short GetHeightSample(int x, int y)
    {
        if (((x | y) & 0x3ffe00) == 0)
        {
            return _heights[(((((x >> 3) & 63) * 64) + ((y >> 3) & 63)) * 9 + (y & 7)) * 9 + (x & 7)];
        }

        if ((x & 0x3ffe00) == 512)
        {
            if ((y & 0x3ffe00) == 512)
            {
                return _heights[(64 * 64 * 9 * 9) - 1];
            }

            // Retail shifts y by eight here, unlike the interior tile coordinate.
            return _heights[(63 * 64 * 9 * 9) + 8 + (((((y >> 8) & 63) * 9) + (y & 7)) * 9)];
        }

        if ((y & 0x3ffe00) == 512)
        {
            return _heights[((((x >> 3) & 63) * 9 * 9) * 64) + (x & 7) + (63 * 9 * 9) + (8 * 9)];
        }

        return 0;
    }

    /// <summary>Unknown47eb00 (0047eb00): bilinear height in 8.8 fixed point from packed coordinates.</summary>
    public int InterpolatedHeight(int packedX, int packedY)
    {
        int x = packedX & 255;
        int y = packedY & 255;
        int at = (((((packedX >> 11) & 63) * 64) + ((packedY >> 11) & 63)) * 9 + ((packedY >> 8) & 7)) * 9 + ((packedX >> 8) & 7);
        int top = _heights[at] + (((_heights[at + 1] - _heights[at]) * x) >> 8);
        int bottom = _heights[at + 9] + (((_heights[at + 10] - _heights[at + 9]) * x) >> 8);
        return top + (((bottom - top) * y) >> 8);
    }

    /// <summary>
    /// Collide (0047eb80): the ground height under a position. Adding 49152 puts 8.8 fixed-point coordinates in the low
    /// bits of the float's representation; the bias is 0.49999/256 (bits 3afffeb0).
    /// </summary>
    public float Collide(FVector pos)
    {
        float bias = BitConverter.Int32BitsToSingle(0x3afffeb0);
        float packedX = pos.X - bias + 49152.0f;
        float packedY = pos.Y - bias + 49152.0f;
        int x = BitConverter.SingleToInt32Bits(packedX);
        int y = BitConverter.SingleToInt32Bits(packedY);
        if (((x | y) & 0x3e0000) == 0)
        {
            return InterpolatedHeight(x, y) * HeightScale;
        }

        return 0;
    }

    /// <summary>
    /// Normal (0047ec60): the ground normal under a position, from the triangle of its sample square. As compiled (read
    /// from the instructions of the Steam BEA.exe, SHA-256 74154bfa…, which is the pristine specimen):
    /// <list type="bullet">
    /// <item>floorf goes through the CRT's floor and a 64-bit fistp (/QIfist) keeping the low dword, so a huge
    /// coordinate gives index 0 and carries on instead of saturating;</item>
    /// <item>the cross product of the two edge vectors is folded against their constant 0 and ±1 components: the
    /// near triangle's normal is stored as (dzx, dzy, -1) and the far one's as (dzx * -1.0f, dzy * -1.0f, -1), so no
    /// zero products or subtractions touch the signs of zeros;</item>
    /// <item>Normalise sums (Y*Y + X*X) + 1, takes fsqrt and multiplies by the reciprocal unless the length is 0.</item>
    /// </list>
    /// </summary>
    public FVector Normal(FVector pos)
    {
        float fx = MathF.Floor(pos.X);
        int ix = X87.Fist(Math.Floor((double)pos.X));
        float fy = MathF.Floor(pos.Y);
        int iy = X87.Fist(Math.Floor((double)pos.Y));
        if (ix < 0 || ix + 1 > 511 || iy < 0 || iy + 1 > 511)
        {
            return new FVector(0, 0, 1.0f);
        }

        int cx = ix % 8;
        int cy = iy % 8;
        float d = (pos.Y - fy) + (pos.X - fx);
        int p = (((ix / 8) * 64) + (iy / 8)) * 81;
        float scale = HeightScale;
        float nx;
        float ny;
        if (d <= 1.0f)
        {
            // 0047ed46..0047ed89
            int h = _heights[p + (cy * 9) + cx];
            nx = (float)(_heights[p + (cy * 9) + cx + 1] - h) * scale;
            ny = (float)(_heights[p + ((cy + 1) * 9) + cx] - h) * scale;
        }
        else
        {
            // 0047ee25..0047ee8a: the far corner's differences, each times -1.0f (0x5d8be0)
            int h = _heights[p + (cy * 9) + cx + 10];
            nx = ((float)(_heights[p + ((cy + 1) * 9) + cx] - h) * scale) * -1.0f;
            ny = ((float)(_heights[p + (cy * 9) + cx + 1] - h) * scale) * -1.0f;
        }

        // 0047ed91..0047ee00 and 0047ee8e..0047eeed
        float m = MathF.Sqrt(((ny * ny) + (nx * nx)) + 1.0f);
        if (m == 0)
        {
            return new FVector(nx, ny, -1.0f);
        }

        float r = 1.0f / m;
        return new FVector(nx * r, ny * r, -1.0f * r);
    }
}
