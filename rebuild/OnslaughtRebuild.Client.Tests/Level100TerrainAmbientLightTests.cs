// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Retail's terrain draw runs with fixed-function lighting ACTIVE, through a
/// terrain-only <c>D3DMATERIAL9</c> whose Diffuse is black and whose Ambient is
/// 0.8, with every enabled light's colour promoted into
/// <c>D3DLIGHT9.Ambient</c> and the ambient register zeroed. With no vertex
/// normal the whole surviving term is a constant
/// <c>0.8 x sum(light colour) / 256</c>, which stage 0 doubles under
/// <c>D3DTOP_MODULATE2X</c>.
///
/// Derivation, byte by byte:
/// <c>reverse-engineering/binary-analysis/terrain-ambient-light-material-2026-07-26.md</c>.
/// These tests pin that every coefficient stays a shipped byte: the light
/// colours are read from the HFLD at load, the 0.8 is the material record's
/// ambient, the 1/256 is <c>_DAT_005db060</c>, and the 2 is the stage op and
/// lives in the shader beside the stage it belongs to.
/// </summary>
public sealed class Level100TerrainAmbientLightTests
{
    [Fact]
    public void TheTerrainVertexDiffuseIsPointFiveTimesTheSummedHfldLightColours()
    {
        Level100Terrain terrain = Level100Terrain.Instance;

        // CHFD+0x107C and CHFD+0x1080, the two fields CEngine::SetupLights
        // (0x0044a2d0) copies into cached lights 0 and 1 - the only two it
        // enables. Level 100's shipped values.
        Assert.Equal(0xBDB179u, terrain.SunColorRgb24);
        Assert.Equal(0x232338u, terrain.AntiSunColorRgb24);

        (float red, float green, float blue) = Level100TerrainCompositor.TerrainVertexDiffuse(
            terrain.SunColorRgb24,
            terrain.AntiSunColorRgb24);

        // (189 + 35, 177 + 35, 121 + 56) = (224, 212, 177), x 0.8 / 256.
        Assert.Equal(0.8f * 224f / 256f, red, 6);
        Assert.Equal(0.8f * 212f / 256f, green, 6);
        Assert.Equal(0.8f * 177f / 256f, blue, 6);

        // Doubled by MODULATE2X in the shader, this is the whole predicted
        // terrain factor: (1.400, 1.325, 1.106). It is not written down anywhere
        // as a literal - it is what these HFLD bytes produce.
        Assert.Equal(1.400f, 2f * red, 3);
        Assert.Equal(1.325f, 2f * green, 3);
        Assert.Equal(1.106f, 2f * blue, 3);
    }

    [Fact]
    public void TheCoefficientsAreTheShippedBytesAndTheClampIsDirect3DsOwn()
    {
        // 0x3f4ccccd at 0x0083d28c + 0x10, and 0x3b800000 = _DAT_005db060.
        Assert.Equal(0.8f, Level100TerrainCompositor.TerrainMaterialAmbient);
        Assert.Equal(1f / 256f, Level100TerrainCompositor.LightColorByteScale);

        // Nothing here is tied to Level 100: a level whose lights summed to
        // white would land on the Direct3D vertex-colour clamp, and one with no
        // enabled light colour would land on black.
        (float red, float green, float blue) =
            Level100TerrainCompositor.TerrainVertexDiffuse(0xFFFFFFu, 0xFFFFFFu);
        Assert.Equal(1f, red);
        Assert.Equal(1f, green);
        Assert.Equal(1f, blue);

        (red, green, blue) = Level100TerrainCompositor.TerrainVertexDiffuse(0u, 0u);
        Assert.Equal(0f, red);
        Assert.Equal(0f, green);
        Assert.Equal(0f, blue);
    }

    [Fact]
    public void TheShaderAppliesTheLightingTermAndTheStageOpTogetherAtStageZero()
    {
        string shaderSource = ReadSourceText(
            "rebuild/OnslaughtRebuild.Godot/Level100TerrainAppearanceAsset.cs");

        // Stage 0 is TEXTURE x DIFFUSE at MODULATE2X. The doubling and the
        // lighting term are one expression: applying the 2x alone, without the
        // material that kills the diffuse channel, overshoots.
        Assert.Contains("uniform vec3 terrain_vertex_diffuse;", shaderSource);
        Assert.Contains(
            "vec3 stage_color = min(\n                macro_color * terrain_vertex_diffuse * 2.0,\n"
            + "                vec3(1.0));",
            shaderSource.ReplaceLineEndings("\n"));

        // The gain reaches the shader from the parsed HFLD, never as a literal.
        Assert.Contains(
            "Level100TerrainCompositor.TerrainVertexDiffuse(\n"
            + "            heightField.SunColorRgb24,\n"
            + "            heightField.AntiSunColorRgb24);",
            shaderSource.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("1.400", shaderSource);
        Assert.DoesNotContain("1.325", shaderSource);
        Assert.DoesNotContain("1.106", shaderSource);
        Assert.DoesNotContain("224", shaderSource);
    }

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
