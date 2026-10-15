// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/DXFont.cpp (CDXBitmapFont::InitFromTexture, GetTextExtent) and src/Font.cpp (the
// character map, RemapChar, CanDisplay, WordWrap) (reconstructed from the retail program; not in Stuart's release).

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Text;

/// <summary>
/// A bitmap font (<c>CDXBitmapFont</c>) measured as retail measures it: the texture is a grid of square cells, one per
/// character in <see cref="Characters"/> order, and each cell's width is found from the texture's alpha. The PC's normal
/// font is <c>font22.512.tga</c> (32-pixel cells, with the inverted-mark swap) and its small font <c>Font13PS.tga</c>
/// (16-pixel cells); the textures come from <c>data\resources\textures\mustbe_NAME(0)A8R8G8B8.aya</c> as 32-bit DDS.
/// </summary>
/// <remarks>
/// Assumed: the textures load at 32 bits (the 16-bit fallback for a 16-bit display mode would change the alpha the
/// measurement reads; the harness runs 32-bit).
/// </remarks>
public sealed class BitmapFont : IFontMetrics
{
    /// <summary><c>FONT_CHARACTERS</c> (005db5fc): the cells' characters in order; the zero ends the measured cells.</summary>
    public static readonly char[] Characters =
    [
        .. Enumerable.Range(0x20, 0x60).Select(c => (char)c),
        'á', 'à', 'â', 'ä', 'ç', 'é', 'è', 'ê', 'ë', 'í', 'ì', 'î',
        'ï', 'ó', 'ò', 'ô', 'ö', 'œ', 'ñ', 'ú', 'ù', 'û', 'ü', 'Á',
        'À', 'Ä', 'Â', 'Ç', 'É', 'È', 'Í', 'Ì', 'Ñ', 'Ó', 'Ò', 'Ö',
        'Ü', 'Ú', 'Ù', 'Œ', '¡', '¿', '©', '®', '™', '«', '»', 'º',
        'ª', '\u001f', 'ß', 'Ê', 'Ë', 'Î', 'Ï', 'Ô', 'Û', '┐', '┌', '\0',
        '┌', '┐',
    ];

    // FONT_UNMAPPED_CHARACTERS (005db738): cells no character maps to.
    private const string Unmapped = "$@[\\]^_{|}~\u007f";

    private static readonly byte UnknownCell;
    private static readonly byte[] CharMap = new byte[0x100];
    private static readonly (char Char, byte Cell)[] WideChars;

    /// <summary><c>InitCharMap</c> (0x00465c10).</summary>
    static BitmapFont()
    {
        int i;
        for (i = 0; Characters[i] != 0; i++)
        {
            if (Characters[i] != '\u001f') continue;
            UnknownCell = (byte)i;
            break;
        }
        Array.Fill(CharMap, UnknownCell);
        List<(char, byte)> wide = [];
        for (i = 0; Characters[i] != 0; i++)
        {
            char c = Characters[i];
            if (Unmapped.Contains(c)) continue;
            if (c >= 0x100) wide.Add((c, (byte)i));
            else CharMap[c] = (byte)i;
        }
        WideChars = [.. wide];
    }

    private readonly float[,] _texCoords;

    /// <summary><c>InitFromTexture</c> (0x0053f880): each cell's width from the texture's alpha.</summary>
    public BitmapFont(byte[] dds, int cellSize, bool charSwapHack)
    {
        CellSize = cellSize;
        CharSwapHack = charSwapHack;
        if (BinaryPrimitives.ReadUInt32LittleEndian(dds) != 0x20534444) throw new InvalidDataException("Not a DDS texture.");
        TexHeight = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(12));
        TexWidth = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(16));
        int bits = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(88));
        if (bits != 32 || dds.Length < 128 + TexWidth * TexHeight * 4)
            throw new InvalidDataException("The font texture is not 32-bit uncompressed.");
        _texCoords = new float[Characters.Length, 4];

        float u = 0f, v = 0f;
        int i = 0;
        do
        {
            // x87: the products are exact (u and v are multiples of the cell over the texture's size); /QIfist rounds.
            int sx = X87.Fist(X87.Mul(TexWidth, u));
            int x = sx + cellSize - 1;
            int sy = X87.Fist(X87.Mul(TexHeight, v));
            sx--;
            x--;
            if (sx < 0) sx = 0;
            x = RightmostColumn(dds, sx, x, sy, cellSize);
            int width = i == 0 ? cellSize >> 1 : x - sx + 2;
            _texCoords[i, 0] = u;
            _texCoords[i, 1] = v;
            _texCoords[i, 2] = (float)X87.Add(X87.Div(width, TexWidth), u);
            _texCoords[i, 3] = (float)X87.Add(X87.Div(cellSize, TexHeight), v);
            u = (float)X87.Add(u, X87.Div(cellSize, TexWidth));
            if (u >= 1f)
            {
                u = 0f;
                v = (float)X87.Add(v, X87.Div(cellSize, TexHeight));
            }
            i++;
        } while (Characters[i] != 0 && v < 1f);
    }

    public int CellSize { get; }
    public int TexWidth { get; }
    public int TexHeight { get; }

    /// <summary><c>mCharSwapHack</c>: the normal font swaps the inverted question and exclamation marks.</summary>
    public bool CharSwapHack { get; }

    /// <summary>The cell's measured width in pixels (<c>(u2 - u1) * width</c>, as GetTextExtent forms it).</summary>
    /// <summary><c>mTexCoords[cell][k]</c>: a cell's left, top, right and bottom in texture units.</summary>
    public float TexCoord(int cell, int k) => _texCoords[cell, k];

    public float CellWidth(int cell) => (float)X87.Mul(X87.Sub(_texCoords[cell, 2], _texCoords[cell, 0]), TexWidth);

    // The rightmost column at or left of x with alpha above 0x10 in rows sy..sy+h-2, minus one (its own x-- after the test).
    private int RightmostColumn(byte[] dds, int sx, int x, int sy, int h)
    {
        bool found = false;
        while (x >= sx && !found)
        {
            int bound = sy + h - 1;
            for (int y = sy; y < bound; y++)
            {
                if (dds[128 + (y * TexWidth + x) * 4 + 3] > 0x10) found = true;
            }
            x--;
        }
        return x;
    }

    /// <summary><c>RemapChar</c> (0x00465cf0): a character's cell; characters the font lacks get the unknown cell.</summary>
    public int RemapChar(char c)
    {
        if (CharSwapHack)
        {
            if (c == '¿') c = '¡';
            else if (c == '¡') c = '¿';
        }
        if (c < 0x100) return CharMap[c];
        foreach ((char wide, byte cell) in WideChars)
        {
            if (wide == c) return cell;
        }
        if (c == '’') return RemapChar('\'');
        if (c == '–') return RemapChar('-');
        return UnknownCell;
    }

    /// <summary><c>CanDisplay</c> (0x00465dd0).</summary>
    public bool CanDisplay(char character) => RemapChar(character) != UnknownCell;

    /// <summary>
    /// <c>GetTextExtent</c> (0x00540680): each character adds its cell's width and one pixel; a newline starts a row (and
    /// still adds the unknown cell's width); the size rounds to integers as /QIfist does.
    /// </summary>
    public (int Width, int Height) GetTextExtent(string text)
    {
        float rowHeight = (float)X87.Mul(X87.Sub(_texCoords[0, 3], _texCoords[0, 1]), TexHeight);
        float rowWidth = 0f, width = 0f, height = rowHeight;
        foreach (char c in text)
        {
            if (c == '\0') break;
            if (c == '\n')
            {
                rowWidth = 0f;
                height = (float)X87.Add(height, rowHeight);
            }
            else if (c == '\r')
            {
                continue;
            }
            int cell = RemapChar(c);
            rowWidth = (float)X87.Add(rowWidth, X87.Mul(X87.Sub(_texCoords[cell, 2], _texCoords[cell, 0]), TexWidth));
            rowWidth = (float)X87.Add(rowWidth, 1f);
            if (rowWidth > width) width = rowWidth;
        }
        return (X87.Fist(width), X87.Fist(height));
    }

    public int TextWidth(string text) => GetTextExtent(text).Width;

    /// <summary><c>GetHeight</c>: the cell size.</summary>
    public int Height => CellSize;

    /// <summary>
    /// <c>WordWrap</c> (0x00465a20): for each character, the line so far plus the whole word starting there is measured
    /// (a space followed by one of "!?;:" stays in the word; a word is at most 99 characters); once that is at least
    /// <paramref name="width"/> wide the line ends (trailing spaces dropped) and a new one starts. A space never starts a
    /// line. At most 40 lines; past that the last line is reused.
    /// </summary>
    public List<string> WordWrap(string text, float width)
    {
        const string punctuation = "!?;:";
        int len = text.IndexOf('\0') is var z && z >= 0 ? z : text.Length;
        char[][] fill = [.. Enumerable.Range(0, 40).Select(_ => new char[len + 2])];
        int col = 0, line = 0;
        for (int pos = 0; pos < len; pos++)
        {
            int w = 0;
            while (true)
            {
                if (pos + w == len) break;
                if (text[pos + w] == ' ' && pos + w + 1 < len && !punctuation.Contains(text[pos + w + 1])) break;
                if (w > 98) break;
                w++;
            }
            string tmp = new string(fill[line], 0, col) + text.Substring(pos, w);
            if (GetTextExtent(tmp).Width >= width)
            {
                while (col > 0 && fill[line][col - 1] == ' ') col--;
                fill[line][col] = '\0';
                col = 0;
                line++;
                if (line >= 40) line--;
            }
            char ch = text[pos];
            if (ch != ' ' || col != 0) fill[line][col++] = ch;
        }
        fill[line][col] = '\0';
        List<string> lines = [];
        for (int l = 0; l <= line; l++)
        {
            int end = Array.IndexOf(fill[l], '\0');
            lines.Add(new string(fill[l], 0, end < 0 ? fill[l].Length : end));
        }
        return lines;
    }
}
