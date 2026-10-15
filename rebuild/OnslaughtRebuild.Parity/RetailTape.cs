// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OnslaughtRebuild.Parity;

/// <summary>One input event of a retail replay tape, delivered after the Present call numbered <see cref="Frame"/>.</summary>
public abstract record RetailTapeEvent(long Frame);

/// <summary>
/// A keyboard event. Retail receives it as WM_KEYDOWN/WM_KEYUP with wParam = virtual key and
/// lParam = 1 | (scan &lt;&lt; 16) (| 0xC0000000 for up), and takes the key from the scan byte.
/// </summary>
public sealed record RetailKeyEvent(long Frame, bool Down, int VirtualKey, int ScanCode, string Name) : RetailTapeEvent(Frame);

/// <summary>
/// A mouse event: relative movement in device counts and the new button level (bit 0 left, bit 1 right,
/// bit 2 middle), which holds until the next mouse event changes it.
/// </summary>
public sealed record RetailMouseEvent(long Frame, int DeltaX, int DeltaY, int Wheel, int Buttons) : RetailTapeEvent(Frame);

/// <summary>
/// A retail replay tape in the target-neutral text format of the parity harness: one event per line,
/// <c>K &lt;frame&gt; &lt;down|up&gt; &lt;vk hex&gt; &lt;scan hex&gt; &lt;name&gt;</c> or
/// <c>M &lt;frame&gt; &lt;dx&gt; &lt;dy&gt; &lt;wheel&gt; &lt;buttons&gt;</c>. Frames count the game's Present calls
/// from process start (the first Present is frame 1) and never decrease; events of one frame apply in file order.
/// </summary>
public sealed class RetailTape
{
    private readonly RetailTapeEvent[] _events;
    private readonly Dictionary<long, (int Start, int Count)> _byFrame;

    private RetailTape(RetailTapeEvent[] events, string sha256)
    {
        _events = events;
        Sha256 = sha256;
        _byFrame = new Dictionary<long, (int, int)>();
        for (int i = 0; i < events.Length;)
        {
            int start = i;
            long frame = events[i].Frame;
            while (i < events.Length && events[i].Frame == frame)
            {
                i++;
            }

            _byFrame[frame] = (start, i - start);
        }
    }

    public IReadOnlyList<RetailTapeEvent> Events => _events;

    /// <summary>Lowercase hex SHA-256 of the tape's bytes as read.</summary>
    public string Sha256 { get; }

    public long FirstFrame => _events.Length == 0 ? 0 : _events[0].Frame;

    public long LastFrame => _events.Length == 0 ? 0 : _events[^1].Frame;

    /// <summary>The events delivered after Present <paramref name="frame"/>, in file order.</summary>
    public ReadOnlySpan<RetailTapeEvent> EventsAt(long frame) =>
        _byFrame.TryGetValue(frame, out var range) ? _events.AsSpan(range.Start, range.Count) : ReadOnlySpan<RetailTapeEvent>.Empty;

    public static RetailTape Load(string path) => Parse(File.ReadAllBytes(path));

    public static RetailTape Parse(byte[] bytes)
    {
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string text = Encoding.ASCII.GetString(bytes);
        var events = new List<RetailTapeEvent>();
        long previousFrame = 0;
        string[] lines = text.Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n];
            if (line.Length == 0 && n == lines.Length - 1)
            {
                break;
            }

            if (line.EndsWith('\r'))
            {
                throw new FormatException($"Tape line {n + 1}: CR line endings are not part of the format.");
            }

            string[] f = line.Split(' ');
            RetailTapeEvent e = f[0] switch
            {
                "K" when f.Length == 6 => new RetailKeyEvent(
                    ParseFrame(f[1], n),
                    f[2] switch
                    {
                        "down" => true,
                        "up" => false,
                        _ => throw new FormatException($"Tape line {n + 1}: key transition must be down or up."),
                    },
                    ParseHexByte(f[3], n),
                    ParseHexByte(f[4], n),
                    f[5].Length > 0 ? f[5] : throw new FormatException($"Tape line {n + 1}: empty key name.")),
                "M" when f.Length == 6 => new RetailMouseEvent(
                    ParseFrame(f[1], n),
                    ParseInt(f[2], n),
                    ParseInt(f[3], n),
                    ParseInt(f[4], n),
                    ParseButtons(f[5], n)),
                _ => throw new FormatException($"Tape line {n + 1}: not a K or M event: '{line}'."),
            };

            if (e.Frame < previousFrame)
            {
                throw new FormatException($"Tape line {n + 1}: frame {e.Frame} is before frame {previousFrame}.");
            }

            previousFrame = e.Frame;
            events.Add(e);
        }

        return new RetailTape(events.ToArray(), sha);
    }

    private static long ParseFrame(string s, int line)
    {
        if (!long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out long frame) || frame < 1)
        {
            throw new FormatException($"Tape line {line + 1}: frame must be a positive integer.");
        }

        return frame;
    }

    private static int ParseHexByte(string s, int line)
    {
        if (s.Length is < 1 or > 2 || !int.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int v))
        {
            throw new FormatException($"Tape line {line + 1}: '{s}' is not a hex byte.");
        }

        return v;
    }

    private static int ParseInt(string s, int line)
    {
        if (!int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v))
        {
            throw new FormatException($"Tape line {line + 1}: '{s}' is not an integer.");
        }

        return v;
    }

    private static int ParseButtons(string s, int line)
    {
        int v = ParseInt(s, line);
        if (v is < 0 or > 7)
        {
            throw new FormatException($"Tape line {line + 1}: buttons must be a 3-bit mask.");
        }

        return v;
    }
}
