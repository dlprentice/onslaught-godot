// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OnslaughtRebuild.Parity.Platform;

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

/// <summary>A CE tape's WM_CHAR and its key's scan code.</summary>
public sealed record RetailCharacterEvent(long Frame, uint Character, int ScanCode) : RetailTapeEvent(Frame);

/// <summary>A CE tape's posted window-mouse message and the back-buffer size associated with it.</summary>
public sealed record RetailWindowMouseEvent(long Frame, MouseMessage Message, int X, int Y, uint WParam,
    int ClientWidth, int ClientHeight) : RetailTapeEvent(Frame);

/// <summary>
/// A retail replay tape in the target-neutral text format of the parity harness: one event per line,
/// <c>K &lt;frame&gt; &lt;down|up&gt; &lt;vk hex&gt; &lt;scan hex&gt; &lt;name&gt;</c> or
/// <c>M &lt;frame&gt; &lt;dx&gt; &lt;dy&gt; &lt;wheel&gt; &lt;buttons&gt;</c>. Frames count the game's Present calls
/// from process start (the first Present is frame 1) and never decrease; events of one frame apply in file order.
/// CE tape 2 additionally has a header, W_CLIENT sizes, window-mouse messages and WM_CHAR messages. Unknown versions
/// and event kinds are refused; the original recording is never modified.
/// </summary>
public sealed class RetailTape
{
    private readonly RetailTapeEvent[] _events;
    private readonly Dictionary<long, (int Start, int Count)> _byFrame;

    private RetailTape(RetailTapeEvent[] events, string sha256, int version, bool usesTapeCursor)
    {
        _events = events;
        Events = Array.AsReadOnly(events);
        Sha256 = sha256;
        Version = version;
        UsesTapeCursor = usesTapeCursor;
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

    /// <summary>The parsed events in file order, exposed through a read-only collection.</summary>
    public IReadOnlyList<RetailTapeEvent> Events { get; }

    /// <summary>Lowercase hex SHA-256 of the tape's bytes as read.</summary>
    public string Sha256 { get; }

    /// <summary>The declared CE tape version, or 1 for the earlier unheaded format.</summary>
    public int Version { get; }

    /// <summary>Whether the port returns a replay-owned cursor, starting at (0, 0), for this tape.</summary>
    public bool UsesTapeCursor { get; }

    public long FirstFrame => _events.Length == 0 ? 0 : _events[0].Frame;

    public long LastFrame => _events.Length == 0 ? 0 : _events[^1].Frame;

    /// <summary>The events delivered after Present <paramref name="frame"/>, in file order.</summary>
    public ReadOnlySpan<RetailTapeEvent> EventsAt(long frame) =>
        _byFrame.TryGetValue(frame, out var range) ? _events.AsSpan(range.Start, range.Count) : ReadOnlySpan<RetailTapeEvent>.Empty;

    public static RetailTape Load(string path) => Parse(File.ReadAllBytes(path));

    public static RetailTape Parse(byte[] bytes)
    {
        if (bytes.Any(b => b == 0 || b > 0x7f))
            throw new FormatException("A tape must contain only ASCII text, without NUL bytes.");
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string text = Encoding.ASCII.GetString(bytes);
        bool crlf = text.StartsWith("# onslaught tape 2\r\n", StringComparison.Ordinal);
        var events = new List<RetailTapeEvent>();
        long previousFrame = 0;
        int version = 1, clientWidth = 0, clientHeight = 0;
        bool hasWindowMouse = false;
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
                if (!crlf) throw new FormatException($"Tape line {n + 1}: unexpected CR line ending.");
                line = line[..^1];
            }
            else if (crlf)
                throw new FormatException($"Tape line {n + 1}: mixed line endings.");

            // The CE loader treats comments and blank lines as inert. Reject newer declared formats, whose event
            // and cursor rules may differ, instead of silently treating them as version 2.
            if (line.StartsWith('#'))
            {
                const string header = "# onslaught tape ";
                if (line.StartsWith(header, StringComparison.Ordinal))
                {
                    if (!int.TryParse(line[header.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out version)
                        || version is < 1 or > 2)
                        throw new FormatException($"Tape line {n + 1}: unsupported tape version.");
                }
                continue;
            }
            if (line.Length == 0) continue;

            string[] f = line.Split(' ');
            if (f[0] == "W_CLIENT")
            {
                if (f.Length != 3) throw new FormatException($"Tape line {n + 1}: invalid W_CLIENT.");
                clientWidth = ParseDimension(f[1], n);
                clientHeight = ParseDimension(f[2], n);
                continue;
            }
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
                    ParseScanCode(f[4], n),
                    f[5].Length > 0 ? f[5] : throw new FormatException($"Tape line {n + 1}: empty key name.")),
                "M" when f.Length == 6 => new RetailMouseEvent(
                    ParseFrame(f[1], n),
                    ParseInt(f[2], n),
                    ParseInt(f[3], n),
                    ParseInt(f[4], n),
                    ParseButtons(f[5], n)),
                "C" when f.Length == 4 => new RetailCharacterEvent(
                    ParseFrame(f[1], n), ParseHexWord(f[2], n), ParseScanCode(f[3], n)),
                "W" when f.Length == 6 => new RetailWindowMouseEvent(
                    ParseFrame(f[1], n), ParseMouseMessage(f[2], n), ParseInt(f[3], n), ParseInt(f[4], n),
                    ParseHexWord(f[5], n), clientWidth, clientHeight),
                _ => throw new FormatException($"Tape line {n + 1}: unknown or malformed event."),
            };

            if (e.Frame < previousFrame)
            {
                throw new FormatException($"Tape line {n + 1}: frame {e.Frame} is before frame {previousFrame}.");
            }

            previousFrame = e.Frame;
            hasWindowMouse |= e is RetailWindowMouseEvent;
            events.Add(e);
        }

        return new RetailTape(events.ToArray(), sha, version, version >= 2 || hasWindowMouse);
    }

    private static int ParseDimension(string s, int line)
    {
        if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 0)
            throw new FormatException($"Tape line {line + 1}: invalid window dimension.");
        return value;
    }

    private static uint ParseHexWord(string s, int line)
    {
        if (s.Length is < 1 or > 8 || !uint.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value))
            throw new FormatException($"Tape line {line + 1}: invalid hexadecimal value.");
        return value;
    }

    private static MouseMessage ParseMouseMessage(string s, int line) => s switch
    {
        "move" => MouseMessage.Move, "ldown" => MouseMessage.LeftDown, "lup" => MouseMessage.LeftUp,
        "rdown" => MouseMessage.RightDown, "rup" => MouseMessage.RightUp, "mdown" => MouseMessage.MiddleDown,
        "mup" => MouseMessage.MiddleUp, "wheel" => MouseMessage.Wheel,
        _ => throw new FormatException($"Tape line {line + 1}: unknown window-mouse message."),
    };

    private static long ParseFrame(string s, int line)
    {
        if (!long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out long frame)
            || frame < 1 || frame > uint.MaxValue)
        {
            throw new FormatException($"Tape line {line + 1}: frame must be between 1 and 4294967295.");
        }

        return frame;
    }

    /// <summary>A scan code: a hex byte, or with bit 8 set (0x1xx) an extended key, which lParam carries as bit 24.</summary>
    private static int ParseScanCode(string s, int line)
    {
        if (s.Length is < 1 or > 3 || !int.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int v) || v > 0x1ff)
        {
            throw new FormatException($"Tape line {line + 1}: '{s}' is not a scan code (a hex byte, 0x1xx for an extended key).");
        }

        return v;
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
