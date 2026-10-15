// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The released <c>CMEMBUFFER</c> read cursor, restricted to a buffer that is
/// already wholly resident in memory.
/// </summary>
/// <remarks>
/// <para>
/// Owner: <c>references/Onslaught/DXMemBuffer.cpp:352-464</c>
/// (<c>CDXMemBuffer::Skip</c> and <c>CDXMemBuffer::Read</c>), reached from
/// <c>membuffer.h:23-26</c>, which is what <c>CMEMBUFFER</c> resolves to on the
/// <c>_DIRECTX</c> target this specimen is.
/// </para>
/// <para>
/// Those two functions are block-streaming loops, but every loop iteration is
/// guarded by <c>mPtr + size &gt; mData + mDataSize</c>. When the whole file is
/// already resident — <c>mLastBlock</c> true and <c>mDataSize</c> the entire
/// length, which is the <c>InitFromMem</c> case — the loop body is unreachable
/// and both collapse to: clamp the request to what remains, raise
/// <c>mEOF</c> if it had to clamp, advance, and return the number of bytes
/// actually consumed. That collapsed form is what this type implements, so it
/// is exact for an in-memory buffer and says nothing about the streaming path.
/// </para>
/// <para>
/// The EOF test is <b>strictly greater than</b>: a request that lands exactly
/// on the last byte consumes the buffer without raising <c>mEOF</c>, and only a
/// request that would run past the end raises it. Reproduced here, and pinned —
/// weakening it to <c>&gt;=</c> is the mutation that breaks the chunk walk at
/// the tail.
/// </para>
/// <para>
/// <c>Skip</c> differs from <c>Read</c> in two ways. It short-circuits
/// <c>if (!size) return 0;</c> ahead of everything else
/// (<c>DXMemBuffer.cpp:354</c>; <c>test ebx, ebx / jne</c> at
/// <c>0x005482DB</c>), which for a resident buffer is an early-out and nothing
/// more — a zero-size request could not have raised EOF or moved <c>mPos</c>
/// anyway, so removing it changes no observable, and this type keeps it only
/// because retail has it. And <c>Skip</c> is the only one of the pair that a
/// caller can hand a negative count — see <see cref="RetailChunkReader.Skip"/>.
/// A negative count falls through both guards and is a no-op that returns 0 and
/// does not raise EOF; that one <b>is</b> observable, and is pinned.
/// </para>
/// </remarks>
public sealed class RetailMemBuffer
{
    private readonly byte[] _data;
    private int _position;
    private bool _endOfFile;
    private bool _closed;

    /// <summary>Wraps a fully resident buffer. The array is not copied or mutated.</summary>
    public RetailMemBuffer(byte[] data) =>
        _data = data ?? throw new ArgumentNullException(nameof(data));

    /// <summary><c>CDXMemBuffer::WhereAmI</c> — <c>mPos</c>, <c>DXMemBuffer.h:51</c>.</summary>
    public int WhereAmI => _position;

    /// <summary><c>CDXMemBuffer::EndOfFile</c> — <c>mEOF</c>, <c>DXMemBuffer.cpp:628-631</c>.</summary>
    public bool EndOfFile => _endOfFile;

    /// <summary>Bytes still ahead of the cursor.</summary>
    public int Remaining => _data.Length - _position;

    /// <summary>
    /// <c>CDXMemBuffer::Read</c> — <c>DXMemBuffer.cpp:394-464</c>. Returns bytes
    /// copied, which is less than <paramref name="size"/> exactly when the read
    /// ran off the end, in which case <c>mEOF</c> is raised and the partial
    /// prefix is still written to <paramref name="destination"/>.
    /// </summary>
    public int Read(Span<byte> destination, int size)
    {
        int available = size;
        if (_position + available > _data.Length)
        {
            _endOfFile = true;
            available = _data.Length - _position;
        }

        int bytesRead = 0;
        if (available > 0)
        {
            _data.AsSpan(_position, available).CopyTo(destination);
            _position += available;
            bytesRead = available;
        }

        return bytesRead;
    }

    /// <summary>
    /// <c>CDXMemBuffer::Skip</c> — <c>DXMemBuffer.cpp:352-391</c>. A zero count
    /// returns before the EOF test; a negative count is a silent no-op.
    /// </summary>
    public int Skip(int size)
    {
        if (size == 0)
        {
            return 0;
        }

        int available = size;
        if (_position + available > _data.Length)
        {
            _endOfFile = true;
            available = _data.Length - _position;
        }

        int bytesRead = 0;
        if (available > 0)
        {
            _position += available;
            bytesRead = available;
        }

        return bytesRead;
    }

    /// <summary>
    /// <c>CDXMemBuffer::Close</c> — <c>DXMemBuffer.cpp:574-615</c>. TRUE the
    /// first time (there was data to release), FALSE afterwards, because retail
    /// nulls <c>mData</c> and the second call falls straight to
    /// <c>return FALSE</c>.
    /// </summary>
    public bool Close()
    {
        if (_closed)
        {
            return false;
        }

        _closed = true;
        return true;
    }
}

/// <summary>
/// The released <c>CChunkReader</c>: an <c>id/size</c> chunk walker over a
/// <c>CMEMBUFFER</c>. Deterministic — it is a cursor over bytes the caller
/// already holds.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/chunker.cpp:96-200</c> and
/// <c>chunker.h:33-51</c>. Retail identities in the pristine <c>74154bfa…</c>
/// image, all read at file offset VA - 0x400000:
/// </para>
/// <list type="bullet">
/// <item><c>0x00423870</c> <c>Open(CMEMBUFFER*)</c> — <c>chunker.cpp:108-121</c>.</item>
/// <item><c>0x00423900</c> <c>Close</c> — <c>chunker.cpp:138-148</c>.</item>
/// <item><c>0x00423910</c> <c>GetNext</c> — <c>chunker.cpp:151-178</c>.</item>
/// <item><c>0x00423960</c> <c>Read</c> — <c>chunker.cpp:180-187</c>.</item>
/// <item><c>0x00423990</c> <c>Skip</c> — <c>chunker.cpp:189-195</c>.</item>
/// </list>
/// <para>
/// <b>Source and retail agree</b> on all five, and the field layout the code
/// indexes reproduces <c>chunker.h</c>'s declaration order exactly:
/// <c>Size</c> at <c>+0</c>, <c>File</c> at <c>+4</c>, <c>ReadSinceChunk</c> at
/// <c>+8</c>, <c>mOwnFile</c> at <c>+0xC</c>. <c>Open</c> zeroes <c>+0</c> and
/// <c>+8</c> before it touches the old buffer; <c>Close</c> compiles
/// <c>if(File-&gt;Close()) return 0; else return -1;</c> into
/// <c>neg/sbb/neg/dec</c>, which is the same two-valued function.
/// </para>
/// <para>
/// <b>Two release-build behaviours the source's asserts hide.</b> The
/// <c>ASSERT(ReadSinceChunk &lt;= Size)</c> at <c>chunker.cpp:184</c> is compiled
/// out — <c>0x00423960</c> contains no comparison against <c>Size</c> at all —
/// so over-reading a chunk is silent, and <c>Skip</c> then computes
/// <c>Size - ReadSinceChunk</c> in <c>ULONG</c>, hands the wrap-around to a
/// <c>SINT</c> parameter, and the buffer's own <c>size &gt; 0</c> guard turns it
/// into a no-op. And <c>GetNext</c> zeroes <c>ReadSinceChunk</c> before either
/// read, so a <c>GetNext</c> that fails still resets the accounting while
/// leaving a partially overwritten <c>Size</c> behind: the second
/// <c>File-&gt;Read(&amp;Size, 4)</c> writes however many bytes were available
/// into the field before the shortfall is detected.
/// </para>
/// <para>
/// <b>Not established here.</b> The <c>Open(char*)</c> / <c>InitFromFile</c>
/// path at <c>0x004238C0</c> is deliberately absent — it is filesystem IO and
/// has no place in Core. Neither the writer half (<c>CChunker</c>) nor any
/// specific chunk grammar is modelled; this is the framing layer only.
/// </para>
/// </remarks>
public sealed class RetailChunkReader
{
    private RetailMemBuffer? _file;
    private uint _size;
    private uint _readSinceChunk;

    /// <summary>
    /// <c>CChunkReader::Open(CMEMBUFFER*)</c> — <c>chunker.cpp:108-121</c>.
    /// Adopts a buffer the caller owns and resets both counters.
    /// </summary>
    public RetailMemBuffer OpenExistingBuffer(RetailMemBuffer existingBuffer)
    {
        _size = 0;
        _readSinceChunk = 0;
        _file = existingBuffer ?? throw new ArgumentNullException(nameof(existingBuffer));
        return _file;
    }

    /// <summary><c>CChunkReader::GetSize</c> — the current chunk's payload length.</summary>
    public uint Size => _size;

    /// <summary><c>ReadSinceChunk</c> — bytes charged against the current chunk.</summary>
    public uint ReadSinceChunk => _readSinceChunk;

    /// <summary><c>CChunkReader::WhereAmI</c> — forwards to the buffer.</summary>
    public int WhereAmI => RequireFile().WhereAmI;

    /// <summary>
    /// <c>CChunkReader::GetNext</c> — <c>chunker.cpp:151-178</c>. Returns the
    /// little-endian chunk id, or 0 when either 4-byte header read came up
    /// short. Zero is not a usable chunk name, which is what the header comment
    /// at <c>chunker.h:45</c> means by "0=dodgy".
    /// </summary>
    public uint GetNext()
    {
        RetailMemBuffer file = RequireFile();
        _readSinceChunk = 0;

        Span<byte> header = stackalloc byte[4];
        header.Clear();
        if (file.Read(header, 4) < 4)
        {
            return 0;
        }

        uint chunk = BitConverter.ToUInt32(header);

        // File->Read(&Size, 4) writes into the live field, so a short read
        // leaves Size partially overwritten even though GetNext reports failure.
        Span<byte> sizeBytes = stackalloc byte[4];
        BitConverter.TryWriteBytes(sizeBytes, _size);
        int got = file.Read(sizeBytes, 4);
        _size = BitConverter.ToUInt32(sizeBytes);
        return got < 4 ? 0u : chunk;
    }

    /// <summary>
    /// <c>CChunkReader::Read</c> — <c>chunker.cpp:180-187</c>. Charges
    /// <c>size * count</c> against the chunk <b>before</b> reading, so the
    /// accounting advances even when the buffer comes up short, and returns
    /// whether the full amount arrived.
    /// </summary>
    public bool Read(Span<byte> destination, uint size, uint count)
    {
        RetailMemBuffer file = RequireFile();
        int requested = unchecked((int)(size * count));
        _readSinceChunk = unchecked(_readSinceChunk + (uint)requested);
        return file.Read(destination, requested) == requested;
    }

    /// <summary>
    /// <c>CChunkReader::Skip</c> — <c>chunker.cpp:189-195</c>. Discards the rest
    /// of the current chunk and returns the number of bytes the buffer actually
    /// moved.
    /// </summary>
    /// <remarks>
    /// <c>skipsize</c> is <c>ULONG</c>, so after an over-read
    /// (<c>ReadSinceChunk &gt; Size</c>) it wraps to a value near 2^32, arrives
    /// at <c>CDXMemBuffer::Skip</c> as a negative <c>SINT</c>, and is dropped by
    /// that function's <c>size &gt; 0</c> guard. The reader's accounting still
    /// resets to <c>Size</c>, so the reader believes it is chunk-aligned while
    /// the buffer cursor is wherever the over-read left it. Retail ships this;
    /// the <c>ASSERT</c> that would have caught it is debug-only.
    /// </remarks>
    public int Skip()
    {
        RetailMemBuffer file = RequireFile();
        uint skipSize = unchecked(_size - _readSinceChunk);
        _readSinceChunk = _size;
        return file.Skip(unchecked((int)skipSize));
    }

    /// <summary>
    /// <c>CChunkReader::Close</c> — <c>chunker.cpp:138-148</c>. 0 when the
    /// buffer reported success, -1 when it did not.
    /// </summary>
    public int Close() => RequireFile().Close() ? 0 : -1;

    private RetailMemBuffer RequireFile() =>
        _file ?? throw new InvalidOperationException(
            "The chunk reader has no buffer; call OpenExistingBuffer first.");
}
