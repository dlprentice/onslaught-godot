// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>The films' Bink headers (frames, width, height), read from the player's own installation's data\video.</summary>
public sealed class InstalledFilmHeaders(Func<string, (string Name, byte[] Bytes)> read) : IFilmFiles
{
    public FilmHeader? Probe(string name)
    {
        byte[] bytes;
        try
        {
            bytes = read($@"data\video\{name}.vid").Bytes;
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        return new FilmHeader(BitConverter.ToInt32(bytes, 8), BitConverter.ToInt32(bytes, 28), BitConverter.ToInt32(bytes, 32));
    }
}
