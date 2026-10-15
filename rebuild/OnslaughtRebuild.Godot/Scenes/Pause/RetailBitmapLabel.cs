// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// A text control for the retained bitmap-font law. It reads a curated atlas
/// only; no simulation, input handling or file mutation occurs.
/// </summary>
public sealed partial class RetailBitmapLabel : Control
{
    private string _text = string.Empty;
    private Color _textColor = Colors.White;
    private bool _shadow;
    private RetailBitmapFont? _font;

    public string Text
    {
        get => _text;
        set { _text = value; QueueRedraw(); }
    }

    public Color TextColor
    {
        get => _textColor;
        set { _textColor = value; QueueRedraw(); }
    }

    public bool Shadow
    {
        get => _shadow;
        set { _shadow = value; QueueRedraw(); }
    }

    public void SetFont(RetailBitmapFont font)
    {
        _font = font;
        QueueRedraw();
    }

    public float Measure(string text) => _font?.Measure(text) ?? 0f;

    public override void _Draw() => _font?.DrawCentered(
        this, Text, Size.X * 0.5f, 0f, TextColor, Shadow);
}

/// <summary>
/// The pause renderer's existing atlas measurement/drawing law, extracted
/// unchanged for the pause text controls.
/// </summary>
public sealed class RetailBitmapFont
{
    private const int FirstGlyph = 32;
    private const int GlyphColumns = 16;
    private const int GlyphCount = 96;
    private readonly Texture2D _atlas;
    private readonly int _cellSize;
    private readonly int[] _widths;

    public RetailBitmapFont(Texture2D atlas, int cellSize)
    {
        _atlas = atlas;
        _cellSize = cellSize;
        _widths = MeasureGlyphWidths(atlas.GetImage(), cellSize);
    }

    public float Measure(string text)
    {
        float width = 0f;
        foreach (char character in text)
        {
            int code = character is >= (char)FirstGlyph and < (char)(FirstGlyph + GlyphCount)
                ? character
                : '?';
            width += _widths[code - FirstGlyph] + 1f;
        }
        return Math.Max(0f, width - 1f);
    }

    public void DrawCentered(Control surface, string text, float centerX, float y, Color color, bool shadow)
    {
        float x = centerX - (Measure(text) * 0.5f);
        foreach (char character in text)
        {
            int code = character is >= (char)FirstGlyph and < (char)(FirstGlyph + GlyphCount)
                ? character
                : '?';
            int glyph = code - FirstGlyph;
            int width = _widths[glyph];
            if (shadow)
                DrawGlyph(surface, glyph, x + 1f, y + 1f, width, Colors.Black);
            DrawGlyph(surface, glyph, x, y, width, color);
            x += width + 1f;
        }
    }

    private void DrawGlyph(Control surface, int glyph, float x, float y, int width, Color color)
    {
        var source = new Rect2(
            (glyph % GlyphColumns) * _cellSize,
            (glyph / GlyphColumns) * _cellSize,
            width,
            _cellSize);
        surface.DrawTextureRectRegion(
            _atlas,
            new Rect2(new Vector2(x, y), new Vector2(width, _cellSize)),
            source,
            color);
    }

    private static int[] MeasureGlyphWidths(Image image, int cellSize)
    {
        var widths = new int[GlyphCount];
        widths[0] = cellSize / 2;
        for (int glyph = 1; glyph < widths.Length; glyph++)
        {
            int cellX = (glyph % GlyphColumns) * cellSize;
            int cellY = (glyph / GlyphColumns) * cellSize;
            int rightmost = cellX;
            for (int x = cellX + cellSize - 2; x >= cellX; x--)
            {
                bool occupied = false;
                for (int y = cellY; y < cellY + cellSize - 1; y++)
                {
                    if (image.GetPixel(x, y).A > (16f / 255f))
                    {
                        occupied = true;
                        break;
                    }
                }
                if (occupied)
                {
                    rightmost = x;
                    break;
                }
            }
            widths[glyph] = (rightmost - cellX) + 2;
        }
        return widths;
    }
}
