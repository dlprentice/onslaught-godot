// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Read-only audio presentation state consumed by the Level 100 HUD. The audio
/// adapter remains the sole playback owner.
/// </summary>
public readonly record struct Level100MessagePlaybackState(
    int? ActiveSpeakerId,
    int? ActiveMessageId,
    double PositionSeconds,
    double LengthSeconds,
    bool Playing,
    bool Paused);

/// <summary>
/// One wrapped line of an in-level message, plus how many characters of the
/// source string it consumed. <see cref="SourceLength"/> includes the run of
/// whitespace the wrap discarded at the end of the line, so summing it walks
/// the original string exactly - which is what the type-on cursor counts.
/// </summary>
public readonly record struct Level100MessageLine(string Text, int SourceLength);

/// <summary>
/// Pure, presentation-free layout and type-on model for the released in-level
/// message panel. Every number here was measured off the 640x480 retail
/// gameplay captures under
/// local-lab/retail-reference-pristine/level100-gameplay/ - see
/// local-lab/HUD-MESSAGE-PANEL-2026-07-26.md for the frame-by-frame working.
///
/// What retail does, from the captures (not from any code path):
///
///   * The message TYPES ON one character at a time into a THREE-line window.
///     hud-timeline-run1/level100-t020080ms.png holds HUD_06 mid-word at
///     "The circle to the left is / your scanner", and
///     opening-pan-run1/level100-t013011ms.png holds HUD_02 mid-word at
///     "This is the threat / circle. That notch / indi".
///   * When the type-on needs a fourth line the window SCROLLS UP BY EXACTLY
///     ONE LINE; it does not page. HUD_02 runs
///     [1,2,3] (t013269) -> [2,3,4] (t013761) -> [3,4,5] (t014260) in
///     opening-pan-run1, so line 3 is the top line of the next window rather
///     than being discarded with lines 1-2.
///   * The wrap is by CHARACTER COLUMN, not by pixel width. Across six captured
///     messages every observed break is reproduced by a greedy 25-column wrap
///     and by no pixel width: "your scanner. Enemy units" (25 columns, 224px)
///     stays on one line while "This is the threat circle." (26 columns, 214px)
///     does not, so no single pixel width can explain both.
///   * The reveal runs at 40 characters per second, counting the source
///     character each break consumes. Least squares over the seven HUD_02
///     samples gives 39.67 char/s and the two HUD_06 samples give 40.28.
/// </summary>
public static class Level100MessagePanel
{
    /// <summary>
    /// Greedy wrap column count. Pinned from both sides by the captures: four
    /// distinct 25-column lines are rendered unbroken ("The circle to the left
    /// is", "your scanner. Enemy units", "Okay, Hawk? I want you to",
    /// "controls.  One determines") and three distinct 26-column candidates are
    /// broken ("This is the threat circle.", "If you ever need to review",
    /// "check out Aquila's message"), so the limit is exactly 25.
    /// </summary>
    public const int WrapColumns = 25;

    /// <summary>
    /// Lines visible at once. Every captured frame that shows message text
    /// shows at most three lines.
    /// </summary>
    public const int VisibleLines = 3;

    /// <summary>
    /// Baseline-to-baseline pitch in 640x480 design pixels. The three rendered
    /// glyph cells in
    /// opening-pan-run1/level100-t016011ms.png start on rows 412, 427 and 442.
    /// </summary>
    public const float LineHeightPixels = 15f;

    /// <summary>
    /// Pen X of the first glyph on every line, in 640x480 design pixels. Retail
    /// left-aligns: the white ink of the leading glyph sits at x 205 on all
    /// three lines of t016011 and t022080, including the short final line
    /// "units in blue.". The pen is one pixel right of the white ink because
    /// the drop shadow is offset (+1,+1) from the white glyph - measured by
    /// cross-correlating the white and shadow masks of t022080, which peaks at
    /// (dx,dy)=(1,1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// NOT MOVED to the 203.5 the d3d9 draw log of 2026-07-27 reports for
    /// retail's text quads (in-level draws 1211-1218), and the reason is a
    /// measurement rather than a preference. Setting the pen to
    /// (203.5, 411.5) and re-capturing moves this client's rendered glyph ink
    /// two rows up and two to three columns left of retail's, and costs the
    /// message-panel region 48.03 -> 53.45 % material at t029072. At (206, 413)
    /// the rendered rows land on retail's EXACTLY - 415-426 / 430-441 / 445-453
    /// with identical per-row ink counts (420:34, 435:59, 450:12).
    /// </para>
    /// <para>
    /// The log and this constant are therefore not in conflict about retail;
    /// they are quoting different things. The log gives the issued vertex, and
    /// the (+2.5, +1.5) between it and this pen is D3D9's half-pixel rule plus
    /// the shadow-versus-white quad the log does not distinguish. The
    /// rasterised comparison is the stronger test of the two and it is the one
    /// that decides. See
    /// local-lab/agent-notes-2026-07-27/hud-corrections-applied.md.
    /// </para>
    /// </remarks>
    public const float TextPenLeft = 206f;

    /// <summary>
    /// Pen Y of the first visible line, in 640x480 design pixels. The 'i' of
    /// "indicates" in t016011 puts its dot on row 415 and its stem on rows
    /// 418-423; font-13ps carries 'i' ink on cell rows 3..12, so the white cell
    /// top is row 412 and the pen (shadow) top is 413. The resulting three-line
    /// block, cell rows 412..458, is centred in the measured panel body
    /// (405.5..464.5, centre 435) to the half pixel.
    /// </summary>
    /// <remarks>
    /// The device log's 411.5 / 426.5 / 441.5 line tops confirm the 15.0 pitch
    /// exactly and are NOT adopted as this constant; see
    /// <see cref="TextPenLeft"/> for the rasterised measurement that decided it.
    /// </remarks>
    public const float FirstLinePenTop = 413f;

    /// <summary>Type-on speed, in source characters per second.</summary>
    public const double CharactersPerSecond = 40d;

    // Measured panel body extent, carried here so the text clip can be stated
    // against the art rather than against the text. Identical to the numbers
    // already pinned by Level100HudDesignSpaceTests.
    public const float PanelBodyLeft = 186.5f;
    public const float PanelBodyTop = 405.5f;
    public const float PanelBodyRight = 496.5f;
    public const float PanelBodyBottom = 464.5f;

    /// <summary>
    /// Wraps a released message into retail's 25-column lines. Whitespace runs
    /// inside a line are preserved (HUD text really does contain a double space
    /// in "controls.  One"); the whitespace run a break lands on is discarded
    /// but still counted in <see cref="Level100MessageLine.SourceLength"/>.
    /// </summary>
    public static IReadOnlyList<Level100MessageLine> Wrap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<Level100MessageLine>();
        string normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] paragraphs = normalized.Split('\n');
        for (int paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            string paragraph = paragraphs[paragraphIndex];
            // The '\n' that ended the paragraph is a source character too.
            int paragraphTerminator = paragraphIndex < paragraphs.Length - 1 ? 1 : 0;
            string current = string.Empty;
            int pendingSeparator = 0;
            int position = 0;
            while (position < paragraph.Length)
            {
                int separatorStart = position;
                while (position < paragraph.Length && char.IsWhiteSpace(paragraph[position]))
                {
                    position++;
                }
                string separator = paragraph[separatorStart..position];

                int wordStart = position;
                while (position < paragraph.Length && !char.IsWhiteSpace(paragraph[position]))
                {
                    position++;
                }
                if (wordStart == position)
                {
                    // Trailing whitespace closes the paragraph; it belongs to
                    // the line that is already open.
                    pendingSeparator += separator.Length;
                    break;
                }

                string word = paragraph[wordStart..position];
                if (current.Length == 0)
                {
                    // Leading whitespace before the first word of a line is
                    // discarded but still consumed.
                    pendingSeparator += separator.Length;
                }
                else if (current.Length + separator.Length + word.Length <= WrapColumns)
                {
                    current += separator + word;
                    continue;
                }
                else
                {
                    lines.Add(new Level100MessageLine(
                        current,
                        current.Length + separator.Length));
                    current = string.Empty;
                    pendingSeparator = 0;
                }

                string remaining = word;
                while (remaining.Length > WrapColumns)
                {
                    lines.Add(new Level100MessageLine(
                        remaining[..WrapColumns],
                        WrapColumns + pendingSeparator));
                    pendingSeparator = 0;
                    remaining = remaining[WrapColumns..];
                }
                current = remaining;
            }

            if (current.Length > 0)
            {
                lines.Add(new Level100MessageLine(
                    current,
                    current.Length + pendingSeparator + paragraphTerminator));
            }
            else if (paragraph.Length == 0 || pendingSeparator > 0)
            {
                lines.Add(new Level100MessageLine(
                    string.Empty,
                    pendingSeparator + paragraphTerminator));
            }
        }

        return lines;
    }

    /// <summary>Total source characters the wrap consumed.</summary>
    public static int SourceLength(IReadOnlyList<Level100MessageLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        int total = 0;
        foreach (Level100MessageLine line in lines)
        {
            total += line.SourceLength;
        }
        return total;
    }

    /// <summary>
    /// Source characters revealed after <paramref name="elapsedSeconds"/> of a
    /// message, at retail's measured 40 char/s. Never negative.
    /// </summary>
    public static int RevealedCharacters(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0d)
        {
            return 0;
        }
        double revealed = elapsedSeconds * CharactersPerSecond;
        return revealed >= int.MaxValue ? int.MaxValue : (int)revealed;
    }

    /// <summary>
    /// The at-most-three lines retail would have on screen once
    /// <paramref name="revealedCharacters"/> source characters have typed on.
    /// The window ends on the line the cursor is in, so it scrolls up one line
    /// at a time, and that line is truncated to the revealed prefix.
    /// </summary>
    public static IReadOnlyList<string> Window(
        IReadOnlyList<Level100MessageLine> lines,
        int revealedCharacters)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            return Array.Empty<string>();
        }

        int cursor = Math.Max(revealedCharacters, 0);
        int activeLine = lines.Count - 1;
        int revealedInLine = lines[activeLine].Text.Length;
        int consumed = 0;
        for (int index = 0; index < lines.Count; index++)
        {
            int next = consumed + lines[index].SourceLength;
            if (cursor < next || index == lines.Count - 1)
            {
                activeLine = index;
                revealedInLine = Math.Clamp(cursor - consumed, 0, lines[index].Text.Length);
                break;
            }
            consumed = next;
        }

        int first = Math.Max(0, activeLine - VisibleLines + 1);
        var window = new List<string>(VisibleLines);
        for (int index = first; index < activeLine; index++)
        {
            window.Add(lines[index].Text);
        }
        window.Add(lines[activeLine].Text[..revealedInLine]);
        return window;
    }
}
