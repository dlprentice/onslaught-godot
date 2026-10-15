// SPDX-License-Identifier: GPL-3.0-or-later

using System.Linq;
using System.Security.Cryptography;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Levels 1-4 of the Level 100 macro cache are produced entirely by
/// <c>Level100TerrainCompositor.RenderTile</c>, and level 0 - the only
/// hash-pinned macro data - is selected only beyond 128 units. Everything the
/// camera sees inside 128 units therefore comes from this compositor. Running
/// it at level 0 over the whole tile grid produces the same 512x512 RGB565
/// buffer as the pinned root map, so the pinned map is a byte oracle for the
/// compositor itself.
/// </summary>
public sealed class Level100TerrainCompositorTests
{
    private const string RootTextureSha256 =
        "6EB202F450926097930BEDCA440F0163A1886572981E3C69B4EDF9289A68AE2B";

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(11)]
    [InlineData(30)]
    public void RenderTileRefusesAnInvalidLevelBeforeAllocatingItsBlock(int level)
    {
        Level100Terrain terrain = Level100Terrain.Instance;
        Level100TerrainCompositor compositor = Level100TerrainCompositor.Create(
            ReadAsset("Assets/Level100/Source/level100-terrain-hierarchy.bin"),
            terrain.SunColorRgb24,
            terrain.AmbientColorRgb24);
        var destination = new byte[Level100TerrainCompositor.RootTextureLength];

        // Unguarded, level 11 allocates a 512 MiB block before its map lookup
        // fails (shifts past 12 wrap the block size to zero); refuse first.
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<IndexOutOfRangeException>(() => compositor.RenderTile(destination, level, 0, 0, 0, 0));
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1 << 20, "An invalid level allocated its block.");
        Assert.All(destination, value => Assert.Equal(0, value));
    }

    [Fact]
    public void RenderTileAtLevelZeroReproducesThePinnedRootTerrainMap()
    {
        byte[] pinnedRoot = ReadAsset(
            "Assets/Level100/Source/level100-root-terrain.rgb565.bin");
        Assert.Equal(Level100TerrainCompositor.RootTextureLength, pinnedRoot.Length);
        Assert.Equal(
            RootTextureSha256,
            Convert.ToHexString(SHA256.HashData(pinnedRoot)));

        Level100Terrain terrain = Level100Terrain.Instance;
        Level100TerrainCompositor compositor = Level100TerrainCompositor.Create(
            ReadAsset("Assets/Level100/Source/level100-terrain-hierarchy.bin"),
            terrain.SunColorRgb24,
            terrain.AmbientColorRgb24);

        var rendered = new byte[Level100TerrainCompositor.RootTextureLength];
        for (int tileY = 0; tileY < Level100TerrainCompositor.TileCountPerAxis; tileY++)
        {
            for (int tileX = 0; tileX < Level100TerrainCompositor.TileCountPerAxis; tileX++)
            {
                compositor.RenderTile(rendered, 0, tileX, tileY, tileX, tileY);
            }
        }

        int mismatches = 0;
        int firstMismatch = -1;
        for (int index = 0; index < pinnedRoot.Length; index++)
        {
            if (rendered[index] == pinnedRoot[index])
            {
                continue;
            }
            mismatches++;
            if (firstMismatch < 0)
            {
                firstMismatch = index;
            }
        }

        Assert.True(
            mismatches == 0,
            $"Level 100 macro compositor diverged from the pinned root map in " +
            $"{mismatches} of {pinnedRoot.Length} bytes; first at byte {firstMismatch} " +
            $"(texel {firstMismatch / 2}, x {(firstMismatch / 2) % 512}, " +
            $"y {(firstMismatch / 2) / 512}).");
        Assert.Equal(
            RootTextureSha256,
            Convert.ToHexString(SHA256.HashData(rendered)));
    }

    [Fact]
    public void TerrainShaderPinsTheRetailDetailRotationAndCloudScrollConstants()
    {
        string shaderSource = ReadSourceText(
            "rebuild/OnslaughtRebuild.Godot/Level100TerrainAppearanceAsset.cs");

        // Stage 3 (0x005459a4-0x005459fd): the angle is the double at
        // 0x005d87e0, 1.0 (`fld qword`; read as a float32 its low dword is
        // 0.0, which an earlier reading took for the angle). sin is stored as a
        // float32, then both are scaled by 0x005d858c = 0.25: _11 = _22 =
        // 0x3e0a5140, _12 = -_21 = 0x3e576aa4, offset (0.3, 0.3).
        Assert.Equal(0x3e0a5140, BitConverter.SingleToInt32Bits((float)(Math.Cos(1.0) * 0.25)));
        Assert.Equal(0x3e576aa4, BitConverter.SingleToInt32Bits((float)Math.Sin(1.0) * 0.25f));
        Assert.Equal(0x3e0a5140, BitConverter.SingleToInt32Bits(0.13507557f));
        Assert.Equal(0x3e576aa4, BitConverter.SingleToInt32Bits(0.21036774f));
        Assert.Contains(
            "0.13507557 * retail_world_uv.x - 0.21036774 * retail_world_uv.y + 0.3,",
            shaderSource);
        Assert.Contains(
            "0.21036774 * retail_world_uv.x + 0.13507557 * retail_world_uv.y + 0.3);",
            shaderSource);
        Assert.DoesNotContain("(retail_world_uv * 0.25) + vec2(0.3)", shaderSource);

        // The scroll rate is MEASURED, not read from .rdata. 0x005d8580 = 0.001
        // and 0x005e50e4 = 0.0005 are per-advance rates multiplied by
        // [0x008a9e20], whose 26 references are all reads with no absolute
        // writer -- its units are not derivable from the file, so neither is
        // the per-second rate. Reading the live accumulators 0x008c0294/98 at
        // three level times gives du/dt = 0.0199944 and 0.0200088 per second
        // (0.07% apart), with v exactly u/2. Wall time is the stable
        // parameterisation: the per-DRAW rate varies 3.1% over the same
        // intervals because terrain draws many tiles per frame.
        Assert.Contains("CloudScrollRateU = 0.02d;", shaderSource);
        Assert.Contains("CloudScrollRateV = 0.01d;", shaderSource);
        Assert.DoesNotContain("CloudScrollRateU = 0.001d", shaderSource);

        // ORIGIN GUARD. Retail advances 0x008c0294/0x008c0298 at the head of
        // CDXLandscape__RenderTerrain and never resets them, so the phase is
        // time spent DRAWING TERRAIN. Godot's TIME is engine time since launch,
        // which carries the front end's 8.167 s plus any menu time, and that
        // origin error reproduces 96 percent of the measured terrain drift.
        // The shader must therefore take the phase as a uniform and must not
        // reference TIME at all -- this is the assertion that would have caught
        // the original defect, so it is written as an absolute prohibition
        // rather than as a match on one expression.
        // Comment lines are stripped first: the prohibition is on the shader
        // CODE referencing TIME, not on the prose explaining why it must not.
        string shaderBody = string.Join(
            '\n',
            shaderSource[
                shaderSource.IndexOf("TerrainShaderCode", StringComparison.Ordinal)..
                shaderSource.IndexOf("\"\"\";", StringComparison.Ordinal)]
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        Assert.DoesNotContain("TIME", shaderBody);
        Assert.Contains("uniform vec2 terrain_cloud_scroll;", shaderSource);
        Assert.Contains("_cloudScrollU = Fract(_cloudScrollU + (frameDelta * CloudScrollRateU));", shaderSource);
        Assert.Contains("_cloudScrollV = Fract(_cloudScrollV + (frameDelta * CloudScrollRateV));", shaderSource);

        // Stage 2's texture matrix at 0x00545943 sets _11 = _22 = 0x3b800000 =
        // 1/256 and its translation row from the two scroll accumulators, so
        // the cloud coordinate is world_uv/256 + scroll and nothing else. The
        // scroll is the proven source of 96 percent of the reconstruction's
        // terrain temporal drift, so this line must not acquire a
        // time-compensation term: see
        // terrain-chain-temporal-drift-2026-07-26.md.
        Assert.Contains("(retail_world_uv / 256.0) + terrain_cloud_scroll).rgb;", shaderSource);
        Assert.Contains("0x008c0294", shaderSource);
        Assert.Contains("0x008c0298", shaderSource);
    }

    private static byte[] ReadAsset(string relativePath) =>
        File.ReadAllBytes(Locate($"rebuild/OnslaughtRebuild.Godot/{relativePath}"));

    private static string ReadSourceText(string repositoryRelativePath) =>
        File.ReadAllText(Locate(repositoryRelativePath));

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
