// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Text;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Text;

public sealed class TextAndFontTests
{
    // A synthetic version-3 text file: entries (id, text offset in UTF-16 units, sample -1), the pool's length, the pool.
    private static byte[] TextFile(params (uint Id, string Text)[] strings)
    {
        using MemoryStream stream = new();
        using BinaryWriter w = new(stream);
        w.Write(0xffffffbbu);
        w.Write(3u);
        w.Write(strings.Length);
        int offset = 0;
        foreach ((uint id, string text) in strings)
        {
            w.Write(id);
            w.Write(offset);
            w.Write(-1);
            offset += text.Length + 1;
        }
        byte[] pool = [.. strings.SelectMany(s => Encoding.Unicode.GetBytes(s.Text + "\0"))];
        w.Write(pool.Length);
        w.Write(pool);
        w.Write(0);
        return stream.ToArray();
    }

    [Fact]
    public void TheTextDatabaseFindsStringsByIdAndFallsBackToTheFirst()
    {
        TextDatabase db = TextDatabase.Load(TextFile((7, "Seven"), (42, "Forty-two é")));
        Assert.Equal(3, db.Format);
        Assert.Equal("Forty-two é", db.GetStringById(42));
        Assert.Equal("Seven", db.GetStringById(99));
        Assert.Equal(@"data\LANGUAGE\german.DAT", TextDatabase.FileName(2, americanText: false));
        Assert.Equal(@"data\LANGUAGE\american.DAT", TextDatabase.FileName(2, americanText: true));
    }

    [Fact]
    public void TheIdsComeFromTheHeadersDefines()
    {
        TextIds ids = TextIds.Read(Encoding.ASCII.GetBytes("// header\n#define\tFETX_NEW_GAME   123\n#define OTHER 4\r\n"));
        Assert.Equal(123u, ids["FETX_NEW_GAME"]);
        Assert.Equal(4u, ids["OTHER"]);
        Assert.Throws<KeyNotFoundException>(() => ids["MISSING"]);
    }

    [Fact]
    public void FrontEndTextReadsTheXKeyOnEveryCall()
    {
        PlatformInput input = new(new PlatformWindow());
        TextIds ids = TextIds.Read(Encoding.ASCII.GetBytes("#define FETX_NEW_GAME 5\n"));
        FrontEndTexts texts = new(TextDatabase.Load(TextFile((5, "New"))), ids, input);
        Assert.Equal("New", texts.GetText(FrontEndText.NewGame));
        input.PostTapeKey(0x58, Dik.X, down: true);
        input.Process(1);
        Assert.Throws<NotSupportedException>(() => texts.GetText(FrontEndText.NewGame));
        Assert.True(texts.ShowEnglish);
    }

    // A synthetic 32-bit DDS of 64 x 16 with 16-pixel cells: the space, then '!' with alpha in columns 3..9 (row 0 only,
    // which the scan reads), then '"' with alpha only in the cell's last row (which the scan leaves out).
    private static byte[] Dds()
    {
        const int width = 64, height = 16;
        byte[] dds = new byte[128 + width * height * 4];
        "DDS "u8.CopyTo(dds);
        BitConverter.GetBytes(124).CopyTo(dds, 4);
        BitConverter.GetBytes(height).CopyTo(dds, 12);
        BitConverter.GetBytes(width).CopyTo(dds, 16);
        BitConverter.GetBytes(32).CopyTo(dds, 88);
        for (int x = 16 + 3; x <= 16 + 9; x++) dds[128 + x * 4 + 3] = 0xff;
        dds[128 + (15 * width + 32 + 8) * 4 + 3] = 0xff;
        // An alpha of 0x10 does not count.
        dds[128 + (2 * width + 16 + 12) * 4 + 3] = 0x10;
        return dds;
    }

    [Fact]
    public void TheFontMeasuresCellsFromTheirAlphaAsRetailDoes()
    {
        BitmapFont font = new(Dds(), 16, charSwapHack: false);
        // The space is half a cell.
        Assert.Equal(8f, font.CellWidth(0));
        // '!': the scan starts at column 16 + 14 (passing the 0x10 alpha at 28), finds column 25 and returns one left of
        // it (24); with the left bound at 15 the width is 24 - 15 + 2.
        Assert.Equal(11f, font.CellWidth(1));
        // '"': its only alpha is in the cell's last row, which the scan leaves out, so the scan runs to the bound (31)
        // and returns 30: 30 - 31 + 2.
        Assert.Equal(1f, font.CellWidth(2));
        // Each character adds its width and one; the height is the cell.
        Assert.Equal((8 + 1 + 11 + 1, 16), font.GetTextExtent(" !"));
        Assert.True(font.CanDisplay('!'));
        Assert.False(font.CanDisplay('一'));
        Assert.Equal(font.RemapChar('\''), font.RemapChar('’'));
    }
}
