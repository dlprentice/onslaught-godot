// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Level 0 of the macro cache is byte-pinned to retail-derived data and is used
/// only beyond 128 units; levels 1-4 are produced by the same compositor and
/// carry every texel inside 128 units. Retail's landscape cache is one atlas
/// algorithm sampled at five resolutions over the same world, so a level-L
/// atlas box-filtered down by 2^L must reproduce the level-0 colours of the
/// same world square. A systematic offset here is a near-field brightness bug
/// that no capture-free gate currently covers.
/// </summary>
public sealed class Level100MacroCacheResolutionTests
{
    private const int MapSize = Level100TerrainCompositor.MapSize;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void MacroCacheLevelMatchesTheRootMapWhenDownsampled(int level)
    {
        byte[] root = ReadAsset("Assets/Level100/Source/level100-root-terrain.rgb565.bin");
        Level100Terrain terrain = Level100Terrain.Instance;
        Level100TerrainCompositor compositor = Level100TerrainCompositor.Create(
            ReadAsset("Assets/Level100/Source/level100-terrain-hierarchy.bin"),
            terrain.SunColorRgb24,
            terrain.AmbientColorRgb24);

        int scale = 1 << level;
        int tilesPerAxis = Level100TerrainCompositor.TileCountPerAxis >> level;
        int coveredSize = MapSize >> level;
        var rendered = new byte[Level100TerrainCompositor.RootTextureLength];
        double[] renderedSum = new double[3];
        double[] rootSum = new double[3];
        double[] absoluteDelta = new double[3];
        int samples = 0;

        // A level-L atlas only spans a 512 >> level world square, so sweep every
        // such window over the whole 512-unit landscape instead of sampling one
        // window that may miss the island entirely.
        for (int windowY = 0; windowY < scale; windowY++)
        {
            for (int windowX = 0; windowX < scale; windowX++)
            {
                int originTileX = windowX * tilesPerAxis;
                int originTileY = windowY * tilesPerAxis;
                for (int slotY = 0; slotY < tilesPerAxis; slotY++)
                {
                    for (int slotX = 0; slotX < tilesPerAxis; slotX++)
                    {
                        compositor.RenderTile(
                            rendered,
                            level,
                            originTileX + slotX,
                            originTileY + slotY,
                            slotX,
                            slotY);
                    }
                }

                // Box-filter the atlas back to one texel per world unit and
                // compare with the same world square of the pinned root map.
                int rootOriginX = originTileX * Level100TerrainCompositor.TileWidth;
                int rootOriginY = originTileY * Level100TerrainCompositor.TileWidth;
                for (int y = 0; y < coveredSize; y++)
                {
                    for (int x = 0; x < coveredSize; x++)
                    {
                        double[] block = new double[3];
                        for (int subY = 0; subY < scale; subY++)
                        {
                            for (int subX = 0; subX < scale; subX++)
                            {
                                (double r, double g, double b) = ReadRgb565(
                                    rendered,
                                    (((y * scale) + subY) * MapSize) + (x * scale) + subX);
                                block[0] += r;
                                block[1] += g;
                                block[2] += b;
                            }
                        }
                        int area = scale * scale;
                        (double rootR, double rootG, double rootB) = ReadRgb565(
                            root,
                            ((rootOriginY + y) * MapSize) + rootOriginX + x);
                        double[] rootPixel = [rootR, rootG, rootB];
                        for (int channel = 0; channel < 3; channel++)
                        {
                            double value = block[channel] / area;
                            renderedSum[channel] += value;
                            rootSum[channel] += rootPixel[channel];
                            absoluteDelta[channel] += Math.Abs(value - rootPixel[channel]);
                        }
                        samples++;
                    }
                }
            }
        }

        string report = string.Join(
            "  ",
            Enumerable.Range(0, 3).Select(channel =>
                $"{"RGB"[channel]}: level{level}={renderedSum[channel] / samples:F2} " +
                $"root={rootSum[channel] / samples:F2} " +
                $"meanAbs={absoluteDelta[channel] / samples:F2}"));

        for (int channel = 0; channel < 3; channel++)
        {
            double delta = (renderedSum[channel] - rootSum[channel]) / samples;
            Assert.True(
                Math.Abs(delta) <= 2.0,
                $"Level 100 macro cache level {level} is offset by {delta:F2} counts in " +
                $"channel {"RGB"[channel]} against the pinned root map. {report}");
        }
    }

    private static (double Red, double Green, double Blue) ReadRgb565(byte[] buffer, int texel)
    {
        int value = buffer[texel * 2] | (buffer[(texel * 2) + 1] << 8);
        return (
            ((value >> 11) & 0x1F) * 255.0 / 31.0,
            ((value >> 5) & 0x3F) * 255.0 / 63.0,
            (value & 0x1F) * 255.0 / 31.0);
    }

    private static byte[] ReadAsset(string relativePath) =>
        File.ReadAllBytes(Locate($"rebuild/OnslaughtRebuild.Godot/{relativePath}"));

    private static string Locate(string repositoryRelativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(
            $"Could not locate '{repositoryRelativePath}'. Run 'npm run prepare:rebuild-assets'.");
    }
}
