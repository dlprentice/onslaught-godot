// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailChunkReader"/> against
/// <c>references/Onslaught/chunker.cpp:96-200</c> and the pristine
/// <c>74154bfa…</c> bytes at <c>0x00423870</c>, <c>0x00423900</c>,
/// <c>0x00423910</c>, <c>0x00423960</c> and <c>0x00423990</c>.
/// </summary>
public sealed class RetailChunkReaderTests
{
    private static readonly byte[] TwoChunks = BuildTwoChunkBuffer();

    // Pins the header framing of GetNext (chunker.cpp:151-178 / 0x00423910):
    // two little-endian DWORDs, id then payload length, and WhereAmI advances by
    // exactly 8 per header. MKID (membuffer.h:6) packs the id first byte in the
    // low octet, so "DATA" is 0x41544144 and any big-endian read fails. Does not
    // pin any chunk grammar - these ids are a fixture, not evidence.
    [Fact]
    public void GetNext_ReadsLittleEndianIdThenSizeAndAdvancesEightBytes()
    {
        var buffer = new RetailMemBuffer(TwoChunks);
        var reader = new RetailChunkReader();
        reader.OpenExistingBuffer(buffer);

        Assert.Equal(0u, reader.Size);
        Assert.Equal(0, reader.WhereAmI);

        Assert.Equal(0x41544144u, reader.GetNext());
        Assert.Equal(6u, reader.Size);
        Assert.Equal(0u, reader.ReadSinceChunk);
        Assert.Equal(8, reader.WhereAmI);
    }

    // Pins Read (chunker.cpp:180-187 / 0x00423960): size * count charged to
    // ReadSinceChunk BEFORE the buffer read, TRUE only when the full amount
    // arrived, and the exact payload bytes. The release build has no bounds
    // check - the ASSERT at chunker.cpp:184 is compiled out, and 0x00423960
    // contains no comparison against Size - which the over-read test below
    // depends on.
    [Fact]
    public void Read_ChargesTheChunkThenCopiesTheExactPayload()
    {
        RetailChunkReader reader = OpenFixture();
        Assert.Equal(0x41544144u, reader.GetNext());

        var payload = new byte[6];
        Assert.True(reader.Read(payload, 2, 3));

        Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 }, payload);
        Assert.Equal(6u, reader.ReadSinceChunk);
        Assert.Equal(14, reader.WhereAmI);
    }

    // Pins Skip (chunker.cpp:189-195 / 0x00423990): it discards exactly
    // Size - ReadSinceChunk, so a partly consumed chunk lands on the next
    // header and the following GetNext succeeds. A Skip that used Size alone
    // would overshoot by 4 here and the second GetNext would return garbage.
    [Fact]
    public void Skip_DiscardsOnlyTheUnreadRemainderOfTheChunk()
    {
        RetailChunkReader reader = OpenFixture();
        Assert.Equal(0x41544144u, reader.GetNext());

        var head = new byte[4];
        Assert.True(reader.Read(head, 4, 1));
        Assert.Equal(4u, reader.ReadSinceChunk);

        Assert.Equal(2, reader.Skip());
        Assert.Equal(6u, reader.ReadSinceChunk);
        Assert.Equal(14, reader.WhereAmI);

        Assert.Equal(0x4C494154u, reader.GetNext());
        Assert.Equal(2u, reader.Size);
        Assert.Equal(0u, reader.ReadSinceChunk);
    }

    // Pins the zero-remainder case and the STRICT EOF test: skipping a fully
    // consumed chunk returns 0 and does not raise mEOF, even with the cursor
    // sitting exactly on the last byte, because DXMemBuffer.cpp:360 tests
    // mPtr + size > mData + mDataSize. Weakening that to >= is the mutation
    // that fails here - it declares EOF at the tail of a well-formed file and
    // would poison any caller that polls EndOfFile between chunks.
    // The if (!size) return 0 short-circuit at DXMemBuffer.cpp:354 is NOT what
    // this pins: for a resident buffer a zero-size request cannot raise EOF or
    // move mPos either way, so removing it is an equivalent mutation. It is
    // reproduced because retail has it, not because it is observable.
    [Fact]
    public void Skip_OfAFullyConsumedChunkReturnsZeroAndDoesNotRaiseEndOfFile()
    {
        var buffer = new RetailMemBuffer(TwoChunks);
        var reader = new RetailChunkReader();
        reader.OpenExistingBuffer(buffer);

        Assert.Equal(0x41544144u, reader.GetNext());
        Assert.True(reader.Read(new byte[6], 6, 1));
        Assert.Equal(0, reader.Skip());
        Assert.False(buffer.EndOfFile);

        Assert.Equal(0x4C494154u, reader.GetNext());
        Assert.True(reader.Read(new byte[2], 2, 1));
        Assert.Equal(TwoChunks.Length, reader.WhereAmI);
        Assert.Equal(0, reader.Skip());
        Assert.False(buffer.EndOfFile);
    }

    // Pins the shipped over-read behaviour. ReadSinceChunk is ULONG
    // (chunker.h:37), so Size - ReadSinceChunk wraps to ~2^32, arrives at
    // CDXMemBuffer::Skip as a negative SINT, and that function's size > 0 guard
    // drops it: the cursor does not move and does not rewind. The reader still
    // believes it is chunk-aligned. A rebuild that clamped the subtraction at
    // zero would agree on WhereAmI by accident, but one that rewound - the
    // "obviously intended" behaviour - moves the cursor and fails.
    [Fact]
    public void Skip_AfterOverReadingAChunkIsASilentNoOpRatherThanARewind()
    {
        RetailChunkReader reader = OpenFixture();
        Assert.Equal(0x41544144u, reader.GetNext());

        // Six bytes of payload, ten bytes read: straight through the next header.
        Assert.True(reader.Read(new byte[10], 10, 1));
        Assert.Equal(10u, reader.ReadSinceChunk);
        Assert.Equal(6u, reader.Size);
        Assert.Equal(18, reader.WhereAmI);

        // NON-VACUITY: the over-read state this test exists to describe is proved
        // to have been reached - more bytes charged to the chunk than the chunk
        // holds - and the unsigned difference is proved to have wrapped past the
        // whole 32-bit range rather than clamping. A rebuild that refused or
        // clamped the over-read never reaches this line.
        Assert.True(reader.ReadSinceChunk > reader.Size);
        Assert.Equal(0xFFFFFFFCu, unchecked(reader.Size - reader.ReadSinceChunk));

        Assert.Equal(0, reader.Skip());
        Assert.Equal(18, reader.WhereAmI);
        Assert.Equal(6u, reader.ReadSinceChunk);
    }

    // Pins the truncated-header behaviour of GetNext. Retail resets
    // ReadSinceChunk before either read, returns 0 when the first 4-byte read
    // comes up short, and - the part the source's control flow hides - the
    // SECOND read writes into the live Size field before the shortfall is
    // detected, so a truncated 6-byte tail leaves Size with its low two bytes
    // overwritten. Setting Size to 0 on failure, or reading the header
    // atomically, fails the last assertion.
    [Fact]
    public void GetNext_ReturnsZeroOnATruncatedHeaderButStillPartlyOverwritesSize()
    {
        var buffer = new RetailMemBuffer([0x44, 0x41, 0x54, 0x41, 0xAA, 0xBB]);
        var reader = new RetailChunkReader();
        reader.OpenExistingBuffer(buffer);

        Assert.Equal(0u, reader.GetNext());
        Assert.True(buffer.EndOfFile);
        Assert.Equal(0u, reader.ReadSinceChunk);
        Assert.Equal(0x0000BBAAu, reader.Size);

        // No header bytes left at all: the first read fails and Size is untouched.
        Assert.Equal(0u, reader.GetNext());
        Assert.Equal(0x0000BBAAu, reader.Size);
    }

    // Pins Open(CMEMBUFFER*) (chunker.cpp:108-121 / 0x00423870): adopting a
    // buffer resets Size and ReadSinceChunk to zero and does NOT rewind the
    // buffer - retail never touches mPos there. A rebuild that reset the cursor
    // reads the first chunk again and fails.
    [Fact]
    public void OpenExistingBuffer_ResetsChunkStateButNotTheBufferCursor()
    {
        var buffer = new RetailMemBuffer(TwoChunks);
        var first = new RetailChunkReader();
        first.OpenExistingBuffer(buffer);
        Assert.Equal(0x41544144u, first.GetNext());
        Assert.True(first.Read(new byte[6], 6, 1));

        var second = new RetailChunkReader();
        Assert.Same(buffer, second.OpenExistingBuffer(buffer));
        Assert.Equal(0u, second.Size);
        Assert.Equal(0u, second.ReadSinceChunk);
        Assert.Equal(14, second.WhereAmI);
        Assert.Equal(0x4C494154u, second.GetNext());
    }

    // Pins Close (chunker.cpp:138-148 / 0x00423900): 0 for success, -1
    // otherwise - the inverted convention the neg/sbb/neg/dec sequence encodes.
    // The second call returns -1 because CDXMemBuffer::Close nulls mData and
    // then falls to return FALSE (DXMemBuffer.cpp:614).
    [Fact]
    public void Close_ReturnsZeroOnceThenMinusOne()
    {
        RetailChunkReader reader = OpenFixture();
        Assert.Equal(0, reader.Close());
        Assert.Equal(-1, reader.Close());
    }

    private static RetailChunkReader OpenFixture()
    {
        var reader = new RetailChunkReader();
        reader.OpenExistingBuffer(new RetailMemBuffer(TwoChunks));
        return reader;
    }

    private static byte[] BuildTwoChunkBuffer()
    {
        var bytes = new List<byte>();
        bytes.AddRange("DATA"u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes(6u));
        bytes.AddRange([(byte)0x10, 0x20, 0x30, 0x40, 0x50, 0x60]);
        bytes.AddRange("TAIL"u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes(2u));
        bytes.AddRange([(byte)0x70, 0x80]);
        return [.. bytes];
    }
}
