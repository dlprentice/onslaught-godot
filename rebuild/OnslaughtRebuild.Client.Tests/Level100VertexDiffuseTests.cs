// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Retail mesh vertices carry a per-vertex D3DCOLOR DIFFUSE dword and the
/// released renderer consumes it.
///
/// Static evidence, read read-only out of the maintainer Ghidra database
/// (<c>BEA.exe</c>, imported Steam specimen SHA-256
/// <c>74154BFAE14DDC8ECB87A0766F5BC381C7B7F1AB334ED7A753040EDA1E1E7750</c>)
/// with <c>analyzeHeadless.bat ... -readOnly -noanalysis</c>:
///
/// <list type="bullet">
/// <item><description>The mesh vertex profile is FVF <c>0x152</c> =
/// <c>XYZ|NORMAL|DIFFUSE|TEX1</c> at stride 36 = 12 + 12 + 4 + 8, rejected
/// otherwise by the converter and by the walker loader.</description></item>
/// <item><description><c>D3DStateCache__UseDefaultRenderState</c> at
/// <c>0x004EB1E0</c> issues <c>RenderState_SetRaw(0x89, 1)</c>
/// (<c>D3DRS_LIGHTING</c> = TRUE), <c>RenderState_SetRaw(0x91, 1)</c>
/// (<c>D3DRS_DIFFUSEMATERIALSOURCE</c> = <c>D3DMCS_COLOR1</c>) and
/// <c>RenderState_SetRaw(0x93, 1)</c> (<c>D3DRS_AMBIENTMATERIALSOURCE</c> =
/// <c>D3DMCS_COLOR1</c>). <c>D3DRS_COLORVERTEX</c> (<c>0x8D</c>) is written at
/// none of the 490 render-state call sites reachable from the two general setters
/// <c>0x00513BC0</c> (440 sites) and <c>0x00513C20</c> (50 sites), so it keeps its
/// <c>TRUE</c> Direct3D <b>9</b> default. (The binary imports d3d9.dll - two
/// occurrences, zero of d3d8.dll - and every setter resolves on the
/// IDirect3DDevice9 vtable at +0xE4. The default value is the same in both
/// APIs, so only the attribution was wrong.)</description></item>
/// <item><description>The mesh draw keeps lighting on. Statically, <c>0x89</c>
/// is cleared only around the mode 2 and mode 6 passes of
/// <c>CMeshRenderer__RenderMeshCore</c> (<c>0x00549570</c>) and the two overlay
/// passes of <c>CMeshRenderer__RenderMeshWithLayerPasses</c>
/// (<c>0x0054D530</c>), and restored to <c>1</c> immediately. At runtime those
/// mode-2 and mode-6 branches turn out to be dead in Level 100 — their draw
/// calls at <c>0x0054a423</c> and <c>0x0054a466</c> never fired across 4,393
/// observed mesh renders, the mode global <c>[0x00704e48]</c> reading only
/// <c>0</c> or <c>4</c> — and <c>D3DRS_LIGHTING</c>, read from the
/// render-state shadow at <c>[0x00855764]</c>, is <c>1</c> at all 576 world and
/// tree mesh draws of a frame.</description></item>
/// <item><description>The same default block sets stage zero
/// <c>D3DTSS_COLORARG1 = D3DTA_TEXTURE</c> and
/// <c>D3DTSS_COLORARG2 = D3DTA_DIFFUSE</c>
/// (<c>SetStateRaw(0, 2, 2)</c>, <c>SetStateRaw(0, 3, 0)</c>), with
/// <c>D3DTSS_COLOROP</c> driven to <c>MODULATE</c> or <c>MODULATE2X</c> by
/// <c>D3DStateCache__SetSlotMode4or5</c> (<c>0x00513AF0</c>).</description></item>
/// </list>
///
/// So the dword is the diffuse and the ambient material reflectance of the
/// fixed-function lighting equation, and the lit result is stage zero's second
/// colour argument. It is consumed, not authored-and-ignored.
///
/// The terrain is the contrast case and is unaffected: its 20-byte vertex has
/// neither normal nor diffuse, and <c>CDXLandscape__Render</c>
/// (<c>0x00545410</c>) switches <c>0x91</c> to <c>D3DMCS_MATERIAL</c> for its
/// own draw, restoring <c>D3DMCS_COLOR1</c> at <c>0x00545590</c>.
/// </summary>
public sealed class Level100VertexDiffuseTests
{
    private const string MeshDirectory = "Assets/Level100/StaticWorld/Meshes";

    /// <summary>
    /// Measured over the 28 Level 100 static-world meshes as converted from the
    /// retail installation: 37,797 vertices, of which 75 are not opaque white.
    /// </summary>
    private const int StaticWorldVertexCount = 37_797;

    private const int StaticWorldNonWhiteVertexCount = 75;

    [Fact]
    public void EveryStaticWorldMeshCarriesThePerVertexDiffuseExtension()
    {
        int vertices = 0;
        foreach (string path in MeshPaths())
        {
            foreach (string[] fields in VertexRecords(path))
            {
                Assert.Equal(7, fields.Length);
                vertices++;
            }
        }

        Assert.Equal(28, MeshPaths().Count);
        Assert.Equal(StaticWorldVertexCount, vertices);
    }

    [Fact]
    public void OnlyTheHealthPadAndTankFactoryCarryNonWhiteVertexDiffuse()
    {
        var nonWhiteByMesh = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (string path in MeshPaths())
        {
            int nonWhite = VertexRecords(path).Count(fields => ToRgb8(fields) != (255, 255, 255));
            if (nonWhite != 0)
            {
                nonWhiteByMesh.Add(Path.GetFileNameWithoutExtension(path), nonWhite);
            }
        }

        Assert.Equal(
            new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["fb-health-pad"] = 42,
                ["fb-tank-factory"] = 33,
            },
            nonWhiteByMesh);
        Assert.Equal(StaticWorldNonWhiteVertexCount, nonWhiteByMesh.Values.Sum());
    }

    /// <summary>
    /// The repair pad's 42 coloured vertices are a single exact authored tint,
    /// D3DCOLOR <c>0xFF3EF6FD</c>. Its alpha is opaque, which matters because
    /// stage zero also runs <c>D3DTSS_ALPHAOP = MODULATE</c> against
    /// <c>D3DTA_DIFFUSE</c>: no vertex in the 194-mesh retail corpus
    /// (278,173 vertices) has a diffuse alpha other than 255, so the OBJ
    /// colour extension loses nothing by carrying RGB only.
    /// </summary>
    [Fact]
    public void TheHealthPadTintIsTheExactAuthoredD3DColor()
    {
        var distinct = VertexRecords(MeshPath("fb-health-pad.obj"))
            .Select(ToRgb8)
            .Distinct()
            .OrderBy(color => color)
            .ToArray();

        Assert.Equal([(0x3E, 0xF6, 0xFD), (0xFF, 0xFF, 0xFF)], distinct);
    }

    /// <summary>
    /// Every emitted channel must survive the OBJ text round trip back to the
    /// exact retail byte, because Godot quantizes the colour array to eight
    /// bits per channel and any drift would change the rendered product.
    /// </summary>
    [Fact]
    public void EveryEmittedChannelRoundTripsToItsExactRetailByte()
    {
        foreach (string path in MeshPaths())
        {
            foreach (string[] fields in VertexRecords(path))
            {
                foreach (string channel in fields[4..7])
                {
                    float value = float.Parse(channel, CultureInfo.InvariantCulture);
                    Assert.InRange(value, 0f, 1f);
                    int quantized = (int)Math.Round(value * 255.0, MidpointRounding.AwayFromZero);
                    Assert.Equal(value, quantized / 255f, 6);
                }
            }
        }
    }

    [Fact]
    public void TheLoaderBindsTheColorArrayAndTheShaderModulatesTheLitDiffuseByIt()
    {
        string loader = ReadGodotSource("CuratedObjMeshLoader.cs");
        Assert.Contains("arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();", loader);

        string staticWorld = ReadGodotSource("Level100StaticWorldAsset.cs");
        Assert.Contains("vertex_light_color *= COLOR.rgb;", staticWorld);
    }

    /// <summary>
    /// The stage-zero gain is per draw, not a constant folded into the shader.
    ///
    /// Retail's stage-zero <c>D3DTSS_COLOROP</c> was read out of the running
    /// safe copy from the texture-stage-state shadow at <c>0x008557f4</c> — the
    /// caching setter at <c>0x00513820</c> indexes that array as
    /// <c>(type + stage*0n30)*4 + 0x008557f0</c> — at the entry to every
    /// <c>CMeshRenderer::RenderMeshCore</c> call (<c>0x00549570</c>, the only
    /// mesh-render call site in the image) of one whole Level 100 frame. It is
    /// <c>5</c> = <c>D3DTOP_MODULATE2X</c> on the 134 mode-0 static-world draws
    /// and the 442 <c>CRTTree</c> mesh draws, and <c>4</c> =
    /// <c>D3DTOP_MODULATE</c> on all seven cockpit batches and on the 19 mode-4
    /// static-world draws of that same frame. One hardcoded <c>* 2.0</c> cannot
    /// be right for both, so the multiplier is a uniform.
    /// </summary>
    [Fact]
    public void TheStageZeroColourOperationIsAPerDrawUniformAndNotAHardcodedDoubling()
    {
        string staticWorld = ReadGodotSource("Level100StaticWorldAsset.cs");

        Assert.Contains("uniform float stage_zero_gain;", staticWorld);
        Assert.DoesNotContain("vertex_light_color * 2.0", staticWorld);
        Assert.Equal(2, Occurrences(staticWorld, "* vertex_light_color * stage_zero_gain,"));

        // The enumerant values are the Direct3D D3DTOP ones, so the source and
        // the runtime dump read alike, and the gain is exactly 1 and exactly 2.
        Assert.Contains("Modulate = 4,", staticWorld);
        Assert.Contains("Modulate2X = 5,", staticWorld);
        Assert.Contains("RetailStageZeroColorOperation.Modulate => 1f,", staticWorld);
        Assert.Contains("RetailStageZeroColorOperation.Modulate2X => 2f,", staticWorld);
        Assert.Contains("material.SetShaderParameter(\"stage_zero_gain\", stageZeroGain);", staticWorld);
    }

    /// <summary>
    /// The world and tree draws must keep the doubling. This pins the
    /// conditional against silently becoming unconditional in the other
    /// direction — a blanket removal of the <c>* 2.0</c> would regress the 134
    /// mode-0 static-world draws and the 442 tree draws that were measured at
    /// <c>MODULATE2X</c>, and the sky control band's retail/build ratio of
    /// 1.006/1.005/1.001 over 13,644 px says the rest of the frame is not 2x
    /// off.
    /// </summary>
    [Fact]
    public void TheStaticWorldMeshesStateTheMeasuredModulate2XAtTheirCallSite()
    {
        string staticWorld = Normalize(ReadGodotSource("Level100StaticWorldAsset.cs"));

        Assert.Contains(
            "        return RetailFixedFunctionMaterial.Create(\n" +
            "            layers,\n" +
            "            terrain,\n" +
            "            maximumHorizontalDistance,\n" +
            "            maximumHorizontalDistance > 0f ? 8f / 255f : 0.5f,\n" +
            "            RetailStageZeroColorOperation.Modulate2X,\n" +
            "            lightRig);",
            staticWorld);

        // The shared default is MODULATE2X too, so a call site that says
        // nothing keeps the majority-measured behaviour rather than losing it.
        Assert.Contains(
            "        RetailStageZeroColorOperation stageZeroColorOperation =\n" +
            "            RetailStageZeroColorOperation.Modulate2X,\n" +
            "        RetailMeshLightRig? lightRig = null)",
            staticWorld);
    }

    /// <summary>
    /// The reflection layer stays lit, and no per-draw unlit flag exists.
    ///
    /// <c>[0x00704e48]</c>, the <c>CMeshRenderer::RenderMeshCore</c> mode
    /// global, was read at every one of 4,393 mesh renders across three
    /// launches: it is <c>0</c> on 4,314 and <c>4</c> on 79, never 2, never 6
    /// and never 8, and breakpoints on the mode-2 and mode-6 draw calls at
    /// <c>0x0054a423</c> and <c>0x0054a466</c> — the branches that bracket
    /// their draw with <c>D3DRS_LIGHTING := 0</c> — never fired. Meanwhile
    /// <c>D3DRS_LIGHTING</c> at <c>[0x00855764]</c> is <c>1</c> across all 576
    /// world and tree draws. So retail's fixed-function pipeline is lit for
    /// these meshes and substituting <c>COLOR.rgb</c> for
    /// <c>vertex_light_color</c> here would be wrong.
    /// </summary>
    [Fact]
    public void TheReflectionLayerStaysLitAndNoPerDrawUnlitFlagWasAdded()
    {
        string staticWorld = ReadGodotSource("Level100StaticWorldAsset.cs");

        Assert.Contains(
            "reflection_color.rgb * vertex_light_color * stage_zero_gain,",
            staticWorld);
        Assert.DoesNotContain("unlit", staticWorld);

        // The reflection layer's own mesh mode is 0, not 2. The superseded
        // comment identified it as mode 2.
        Assert.DoesNotContain("Mode 2 is another stage-zero world draw", staticWorld);
        Assert.Contains("it is a MODE-0 one", staticWorld);
    }

    /// <summary>
    /// The world path's normal space is the same map the cockpit's was proved
    /// to be, and it is proved here the same way: by carrying the shipped bytes
    /// through both chains numerically rather than by arguing about them.
    ///
    /// The reconstruction reaches Godot world space for a static-world mesh in
    /// two steps, not one. <c>emit_obj</c>
    /// (<c>rebuild/tools/cmsh_static_preview.py</c>, positions at line 775 and
    /// normals at line 738) negates the third component of the BEA model-space
    /// triple, i.e. applies <c>F = diag(1, 1, -1)</c>; then
    /// <see cref="Level100StaticWorldAsset"/> hangs the mesh under a
    /// <c>MeshInstance3D</c> with <c>RotationDegrees = (-90, 0, 0)</c>. The
    /// composite <c>Rx(-90) * F</c> is asserted below to be exactly
    /// <c>RetailAquilaWalkerAsset.MapVector</c>, <c>(x, y, z) -> (x, -z, -y)</c>
    /// — the same orthogonal, symmetric, self-inverse map with determinant -1
    /// the cockpit uses — so the world path is not a different normal space at
    /// all, only a different factorisation of the same one.
    ///
    /// The object yaw is the only part the cockpit chain does not have.
    /// Retail yaws a static about the BEA Z (down) axis; the reconstruction
    /// yaws its object root about Godot's Y. Those agree because
    /// <c>M * Rz(phi) * M = Ry(phi)</c> for every angle, asserted below, so the
    /// yaw conjugates through <c>M</c> exactly like the cockpit's part bases do.
    /// Since <c>Level100HeightFieldAsset</c> maps the sunlight direction by that
    /// same <c>M</c> and <c>M</c> is orthogonal, <c>N.L</c> is preserved.
    /// </summary>
    [Fact]
    public void TheWorldPathNormalSpaceIsMapVectorAndPreservesTheSunDotProduct()
    {
        double[,] rotateXMinus90 = RotationX(-Math.PI / 2.0);
        double[,] emitObjMap = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, -1 } };
        double[,] composite = Multiply(rotateXMinus90, emitObjMap);

        // MapVector: (x, y, z) -> (x, -z, -y).
        double[,] mapVector = { { 1, 0, 0 }, { 0, 0, -1 }, { 0, -1, 0 } };
        Assert.True(MaximumDifference(composite, mapVector) < 1e-15);
        Assert.Equal(-1.0, Determinant(mapVector));

        // The yaw conjugation identity the cockpit proof relied on, on the
        // world path's own axes: BEA yaw about Z (down) is Godot yaw about Y.
        for (int degrees = 0; degrees < 360; degrees++)
        {
            double phi = degrees * Math.PI / 180.0;
            double[,] conjugated = Multiply(mapVector, Multiply(RotationZ(phi), mapVector));
            Assert.True(MaximumDifference(conjugated, RotationY(phi)) < 1e-14);
        }

        // Now the shipped bytes. Every normal of every static-world OBJ, carried
        // through the retail chain (BEA model normal, yawed about Z, dotted with
        // the runtime-observed toward-sun vector) and through the reconstruction
        // chain (OBJ normal, Rx(-90), yawed about Y, dotted with the mapped
        // sunlight direction the shader uses).
        //
        // The sun vector is light 0's direction of travel, read live out of the
        // running safe copy at 0x009c65c0 + 0x14 and byte-identical across five
        // observations in two launches.
        double[] sunTravel = { -0.03407396, -0.90863329, +0.41620260 };
        double[] towardSunBea = { -sunTravel[0], -sunTravel[1], -sunTravel[2] };
        double[] godotSunlightDirection = Apply(mapVector, sunTravel);
        double[] towardSunGodot =
        {
            -godotSunlightDirection[0],
            -godotSunlightDirection[1],
            -godotSunlightDirection[2],
        };

        int normals = 0;
        double worst = 0.0;
        foreach (string path in MeshPaths())
        {
            foreach (double[] objNormal in NormalRecords(path))
            {
                // A yaw is authored per object, not per mesh, so sweep a dense
                // set rather than pick one: the identity must hold at all of
                // them, and the manifest's 33 authored yaws are a subset.
                for (int degrees = 0; degrees < 360; degrees += 60)
                {
                    double phi = degrees * Math.PI / 180.0;
                    double[] beaNormal = Normalize(Apply(emitObjMap, objNormal));
                    double retail = Dot(Apply(RotationZ(phi), beaNormal), towardSunBea);

                    double[] godotNormal = Normalize(
                        Apply(Multiply(RotationY(phi), rotateXMinus90), objNormal));
                    double reconstruction = Dot(godotNormal, towardSunGodot);

                    worst = Math.Max(worst, Math.Abs(retail - reconstruction));
                }
                normals++;
            }
        }

        Assert.Equal(StaticWorldVertexCount, normals);
        Assert.True(
            worst < 1e-12,
            $"world-path N.L deviates from retail by {worst:E3} over {normals} normals");
    }

    private static List<double[]> NormalRecords(string path) => File
        .ReadAllLines(path)
        .Where(line => line.StartsWith("vn ", StringComparison.Ordinal))
        .Select(line => line
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture))
            .ToArray())
        .ToList();

    private static double[,] RotationX(double theta) => new[,]
    {
        { 1.0, 0.0, 0.0 },
        { 0.0, Math.Cos(theta), -Math.Sin(theta) },
        { 0.0, Math.Sin(theta), Math.Cos(theta) },
    };

    private static double[,] RotationY(double theta) => new[,]
    {
        { Math.Cos(theta), 0.0, Math.Sin(theta) },
        { 0.0, 1.0, 0.0 },
        { -Math.Sin(theta), 0.0, Math.Cos(theta) },
    };

    private static double[,] RotationZ(double theta) => new[,]
    {
        { Math.Cos(theta), -Math.Sin(theta), 0.0 },
        { Math.Sin(theta), Math.Cos(theta), 0.0 },
        { 0.0, 0.0, 1.0 },
    };

    private static double[,] Multiply(double[,] left, double[,] right)
    {
        var result = new double[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                double sum = 0.0;
                for (int k = 0; k < 3; k++)
                {
                    sum += left[row, k] * right[k, column];
                }
                result[row, column] = sum;
            }
        }
        return result;
    }

    private static double[] Apply(double[,] matrix, double[] vector) =>
    [
        (matrix[0, 0] * vector[0]) + (matrix[0, 1] * vector[1]) + (matrix[0, 2] * vector[2]),
        (matrix[1, 0] * vector[0]) + (matrix[1, 1] * vector[1]) + (matrix[1, 2] * vector[2]),
        (matrix[2, 0] * vector[0]) + (matrix[2, 1] * vector[1]) + (matrix[2, 2] * vector[2]),
    ];

    private static double[] Normalize(double[] vector)
    {
        double length = Math.Sqrt(Dot(vector, vector));
        return length == 0.0
            ? vector
            : [vector[0] / length, vector[1] / length, vector[2] / length];
    }

    private static double Dot(double[] left, double[] right) =>
        (left[0] * right[0]) + (left[1] * right[1]) + (left[2] * right[2]);

    private static double Determinant(double[,] m) =>
        (m[0, 0] * ((m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1])))
        - (m[0, 1] * ((m[1, 0] * m[2, 2]) - (m[1, 2] * m[2, 0])))
        + (m[0, 2] * ((m[1, 0] * m[2, 1]) - (m[1, 1] * m[2, 0])));

    private static double MaximumDifference(double[,] left, double[,] right)
    {
        double worst = 0.0;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                worst = Math.Max(worst, Math.Abs(left[row, column] - right[row, column]));
            }
        }
        return worst;
    }

    /// <summary>
    /// The proof above is only about the chain the asset actually builds, so
    /// pin that chain's two load-bearing lines. If either changes, the numeric
    /// result above stops describing the shipped code and must be re-derived.
    /// </summary>
    [Fact]
    public void TheWorldMeshChainStillUsesTheProvenRotationAndNormalTransform()
    {
        string staticWorld = Normalize(ReadGodotSource("Level100StaticWorldAsset.cs"));

        Assert.Contains("RotationDegrees = new Vector3(-90f, 0f, 0f),", staticWorld);
        Assert.Contains("new Basis(Vector3.Right, -Mathf.Pi / 2f)", staticWorld);
        Assert.Contains(
            "vec3 world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);",
            staticWorld);
    }

    /// <summary>
    /// The close pines carry no vertex tint, so the device-measured light rig
    /// predicts their shading directly.
    ///
    /// This is the caveat <c>LIT-MESH-LIGHT-STATE-2026-07-26.md</c> section 7
    /// left open: <c>D3DRS_AMBIENTMATERIALSOURCE</c> and
    /// <c>D3DRS_DIFFUSEMATERIALSOURCE</c> are both <c>D3DMCS_COLOR1</c> at the
    /// 442 <c>CRTTree</c> draws, so a per-vertex colour other than white would
    /// scale the whole predicted result and the session that measured the rig
    /// did not read <c>COLOR1</c>. Read here off the shipped bytes instead:
    /// all 2,269 vertices of the four <c>pinesnow</c> meshes carry exactly
    /// <c>1.0 1.0 1.0</c> in the OBJ colour extension, i.e. D3DCOLOR
    /// <c>0xFFFFFFFF</c>. The two non-white meshes in the level
    /// (<c>fb-health-pad</c>, <c>fb-tank-factory</c>) are buildings and neither
    /// is a pine. So <c>vertex_light_color *= COLOR.rgb</c> is the identity on
    /// every tree card and the predicted gains stand unmodified.
    /// </summary>
    [Fact]
    public void EveryClosePineVertexIsOpaqueWhiteSoTheMeasuredRigPredictsDirectly()
    {
        var byMesh = new SortedDictionary<string, (int Total, int NonWhite)>(
            StringComparer.Ordinal);
        foreach (string path in MeshPaths())
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("pinesnow", StringComparison.Ordinal))
            {
                continue;
            }
            List<string[]> records = VertexRecords(path);
            byMesh.Add(
                name,
                (records.Count, records.Count(fields => ToRgb8(fields) != (255, 255, 255))));
        }

        Assert.Equal(
            new SortedDictionary<string, (int Total, int NonWhite)>(StringComparer.Ordinal)
            {
                ["pinesnow0"] = (674, 0),
                ["pinesnow1"] = (411, 0),
                ["pinesnow2"] = (586, 0),
                ["pinesnow3"] = (598, 0),
            },
            byMesh);
        Assert.Equal(2_269, byMesh.Values.Sum(item => item.Total));
    }

    /// <summary>
    /// The close pines run their own measured rig, and it is built from shipped
    /// values plus exactly one measured register.
    ///
    /// Read at the <c>IDirect3DDevice9</c> calls — <c>SetLight</c> at
    /// <c>0x005512e1</c>, <c>LightEnable</c> at <c>0x00551101</c> — over two
    /// independent launches, all 442 <c>CRTTree</c> payloads per frame
    /// byte-identical: <c>D3DRS_AMBIENT = 0x0039293e</c>, light 0
    /// <c>Diffuse = (0.07382812, 0.06914063, 0.04726563)</c> with
    /// <c>Direction = (0, 0, +1)</c>, light 1 <c>Diffuse = (0.13671875,
    /// 0.13671875, 0.21875)</c> with <c>Direction = (0, 0, -1)</c>. Light 0 is
    /// the height field's sun colour times the shipped
    /// <c>[0x005d85c0] = 0.10000000149011612</c> that
    /// <c>CRTTree::BuildRenderOutputs</c> applies at
    /// <c>0x004ddcb8</c>–<c>0x004ddd2f</c>; the anti-sun block at
    /// <c>0x004ddd59</c> has no such factor.
    /// </summary>
    [Fact]
    public void TheClosePineDrawsCarryTheMeasuredTreeRigAndNotTheStaticWorldOne()
    {
        string staticWorld = Normalize(ReadGodotSource("Level100StaticWorldAsset.cs"));

        // The one register with no established shipped source, and the shipped
        // scale that is not a fitted constant.
        Assert.Contains("public const uint ClosePineAmbientRgb24 = 0x0039293Eu;", staticWorld);
        Assert.Contains("public const float ClosePineKeyLightScale = 0.1f;", staticWorld);

        // The rig itself: ambient from the measured register, key light from the
        // shipped sun colour scaled by the shipped 0.1, fill light from the
        // shipped anti-sun unscaled, and the vertical axis.
        Assert.Contains(
            "    public static RetailMeshLightRig ClosePine(Level100HeightFieldAsset terrain) => new(\n" +
            "        ToColorVector(ClosePineAmbientRgb24, 255f),\n" +
            "        ToColorVector(terrain.SunColorRgb24, 256f) * ClosePineKeyLightScale,\n" +
            "        ToColorVector(terrain.AntiSunColorRgb24, 256f),",
            staticWorld);
        Assert.Contains("        new Vector3(0f, -1f, 0f));", staticWorld);

        // And that the pines, and only the pines, are given it.
        Assert.Contains(
            "            bool isClosePine = key.StartsWith(\"pinesnow\", StringComparison.Ordinal);",
            staticWorld);
        Assert.Contains(
            "                    isClosePine\n" +
            "                        ? RetailMeshLightRig.ClosePine(terrain)\n" +
            "                        : RetailMeshLightRig.StaticWorld(terrain)),",
            staticWorld);
        Assert.Equal(1, Occurrences(staticWorld, "RetailMeshLightRig.ClosePine(terrain)"));
    }

    /// <summary>
    /// The static-world rig must NOT acquire the tree's.
    ///
    /// The same device-level session measured the 130 mode-0 <c>CRTMesh</c>
    /// draws at <c>D3DRS_AMBIENT = 0x000d0f2b</c> with the unscaled sun and
    /// anti-sun on the height field's own sun axis, and confirmed the
    /// reconstruction already reproduces that channel for channel including the
    /// 3.32x up/under step. Buildings are correct; nothing about them changes.
    /// The default on every call site that says nothing is the static-world rig.
    /// </summary>
    [Fact]
    public void TheStaticWorldRigKeepsTheHeightFieldAmbientAndTheUnscaledSun()
    {
        string staticWorld = Normalize(ReadGodotSource("Level100StaticWorldAsset.cs"));

        Assert.Contains(
            "    public static RetailMeshLightRig StaticWorld(Level100HeightFieldAsset terrain) => new(\n" +
            "        ToColorVector(terrain.AmbientColorRgb24, 255f),\n" +
            "        ToColorVector(terrain.SunColorRgb24, 256f),\n" +
            "        ToColorVector(terrain.AntiSunColorRgb24, 256f),\n" +
            "        terrain.SunlightDirection);",
            staticWorld);

        // The tree scale and the tree ambient appear nowhere in the world rig.
        Assert.DoesNotContain(
            "ToColorVector(terrain.AmbientColorRgb24, 255f) * ClosePineKeyLightScale",
            staticWorld);
        Assert.Equal(1, Occurrences(staticWorld, "ClosePineKeyLightScale,"));
        Assert.Equal(1, Occurrences(staticWorld, "ToColorVector(ClosePineAmbientRgb24, 255f)"));

        // A call site that states no rig gets the world one.
        Assert.Contains("        RetailMeshLightRig? lightRig = null)", staticWorld);
        Assert.Contains(
            "        RetailMeshLightRig rig = lightRig ?? RetailMeshLightRig.StaticWorld(terrain);",
            staticWorld);
        Assert.Contains("material.SetShaderParameter(\"ambient_color\", rig.AmbientColor);", staticWorld);
        Assert.Contains("material.SetShaderParameter(\"sun_color\", rig.KeyLightColor);", staticWorld);
        Assert.Contains("material.SetShaderParameter(\"anti_sun_color\", rig.FillLightColor);", staticWorld);
        Assert.Contains(
            "material.SetShaderParameter(\"sunlight_direction\", rig.KeyLightDirection);",
            staticWorld);
    }

    /// <summary>
    /// The arithmetic the two rigs produce, from the shipped height field bytes
    /// and the measured register, against the numbers measured off retail's own
    /// pixels. Nothing here is fitted: every operand is either a byte
    /// <c>Level100Terrain</c> parses out of the shipped height field or a value
    /// read at the device.
    ///
    /// Tree rig, both lights axis-aligned so <c>N.L</c> is exactly 1 on a
    /// horizontal card: <c>2 x (ambient + light)</c> is
    /// <c>(0.59472, 0.45985, 0.58081)</c> up and
    /// <c>(0.72050, 0.59501, 0.92377)</c> down. Retail's measured median step at
    /// 353 matched pixel pairs is <c>1.144</c>; this rig predicts <c>1.304</c>
    /// and the static-world rig applied to the same cards predicts
    /// <c>2.714</c>. The sign matters as much as the size: retail's foliage
    /// undersides are brighter and bluer than its tops, and the static-world rig
    /// has that backwards.
    /// </summary>
    [Fact]
    public void TheTreeRigPredictsRetailsNearFlatFoliageStepAndTheWorldRigDoesNot()
    {
        Level100Terrain terrain = Level100Terrain.Instance;
        Assert.Equal(0xBDB179u, terrain.SunColorRgb24);
        Assert.Equal(0x232338u, terrain.AntiSunColorRgb24);
        Assert.Equal(0x0D0F2Bu, terrain.AmbientColorRgb24);

        double[] sun = Channels(terrain.SunColorRgb24, 256.0);
        double[] antiSun = Channels(terrain.AntiSunColorRgb24, 256.0);

        // The device-side light-0 Diffuse at the tree draws, divided by the
        // shipped sun colour, is the shipped 0.1 at 0x005d85c0 on all three
        // channels. Assert the shipped bytes reproduce the uploaded payload.
        double[] treeKey = [sun[0] * 0.1, sun[1] * 0.1, sun[2] * 0.1];
        Assert.Equal(0.07382812, treeKey[0], 7);
        Assert.Equal(0.06914063, treeKey[1], 7);
        Assert.Equal(0.04726563, treeKey[2], 7);

        // D3DRS_AMBIENT at the 442 CRTTree draws. Measured, not shipped: a
        // whole-image operand scan finds zero occurrences of 0x0039293e.
        double[] treeAmbient = Channels(0x0039293Eu, 255.0);
        double[] treeUp = Doubled(treeAmbient, treeKey);
        double[] treeDown = Doubled(treeAmbient, antiSun);

        AssertChannels([0.59472, 0.45985, 0.58081], treeUp, 5);
        AssertChannels([0.72050, 0.59501, 0.92377], treeDown, 5);
        AssertChannels(
            [1.2115, 1.2939, 1.5905],
            [treeDown[0] / treeUp[0], treeDown[1] / treeUp[1], treeDown[2] / treeUp[2]],
            4);

        // The static-world rig on the same horizontal card, for contrast. Its
        // N.L is the shipped sun elevation, not 1: the sun vector is
        // CHFD + 0x10A4 and the reconstruction reads it at load.
        double[] worldAmbient = Channels(terrain.AmbientColorRgb24, 255.0);
        double sunDot =
            Math.Abs((double)terrain.SunPositionZ) / SunPositionLength(terrain);
        Assert.Equal(0.41620260, sunDot, 7);

        double[] worldUp = Doubled(worldAmbient, Scale(sun, sunDot));
        double[] worldDown = Doubled(worldAmbient, Scale(antiSun, sunDot));
        AssertChannels([0.71651, 0.69318, 0.73070], worldUp, 5);
        AssertChannels([0.21577, 0.23145, 0.51934], worldDown, 5);

        // Rec. 601 luminance, the same weighting the 353-pair pixel measurement
        // used, and the same brighter-over-darker orientation that measurement
        // reported, so 1.0 means a flat card either way up. Retail's median
        // there is 1.144.
        const double RetailMedianStep = 1.144;
        double treeStep = Step(treeUp, treeDown);
        double worldStep = Step(worldUp, worldDown);
        Assert.Equal(1.304, treeStep, 3);
        Assert.Equal(2.714, worldStep, 3);

        // The load-bearing comparison, stated as an inequality so it cannot be
        // satisfied by tuning: the measured tree rig lands nearer retail's
        // measured step than the static-world rig does, and on the same side.
        Assert.True(
            Math.Abs(treeStep - RetailMedianStep) < Math.Abs(worldStep - RetailMedianStep),
            $"tree step {treeStep:F3} is no closer to retail's {RetailMedianStep} " +
            $"than the world rig's {worldStep:F3}");
        Assert.True(treeDown[2] > treeUp[2], "retail's foliage undersides are brighter in blue");
        Assert.True(worldDown[2] < worldUp[2], "the static-world rig darkens undersides");
    }

    private static double SunPositionLength(Level100Terrain terrain) => Math.Sqrt(
        ((double)terrain.SunPositionX * terrain.SunPositionX) +
        ((double)terrain.SunPositionY * terrain.SunPositionY) +
        ((double)terrain.SunPositionZ * terrain.SunPositionZ));

    private static double[] Channels(uint rgb, double divisor) =>
    [
        ((rgb >> 16) & 0xFF) / divisor,
        ((rgb >> 8) & 0xFF) / divisor,
        (rgb & 0xFF) / divisor,
    ];

    private static double[] Scale(double[] color, double factor) =>
        [color[0] * factor, color[1] * factor, color[2] * factor];

    /// <summary>Stage-zero <c>D3DTOP_MODULATE2X</c> over ambient plus light.</summary>
    private static double[] Doubled(double[] ambient, double[] light) =>
    [
        2.0 * (ambient[0] + light[0]),
        2.0 * (ambient[1] + light[1]),
        2.0 * (ambient[2] + light[2]),
    ];

    /// <summary>
    /// The luminance step across one card's crease, brighter over darker, which
    /// is how the 353 matched retail pixel pairs were scored.
    /// </summary>
    private static double Step(double[] up, double[] down)
    {
        double first = Luminance(up);
        double second = Luminance(down);
        return Math.Max(first, second) / Math.Min(first, second);
    }

    private static double Luminance(double[] color) =>
        (0.299 * color[0]) + (0.587 * color[1]) + (0.114 * color[2]);

    private static void AssertChannels(double[] expected, double[] actual, int precision)
    {
        for (int channel = 0; channel < 3; channel++)
        {
            Assert.Equal(expected[channel], actual[channel], precision);
        }
    }

    private static int Occurrences(string text, string value)
    {
        int count = 0;
        for (int at = text.IndexOf(value, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static (int R, int G, int B) ToRgb8(string[] fields) => (
        Channel(fields[4]),
        Channel(fields[5]),
        Channel(fields[6]));

    private static int Channel(string value) => (int)Math.Round(
        float.Parse(value, CultureInfo.InvariantCulture) * 255.0,
        MidpointRounding.AwayFromZero);

    private static List<string[]> VertexRecords(string path) => File
        .ReadAllLines(path)
        .Where(line => line.StartsWith("v ", StringComparison.Ordinal))
        .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .ToList();

    private static string MeshPath(string fileName) =>
        Path.Combine(LocateGodotDirectory(), MeshDirectory, fileName);

    private static List<string> MeshPaths() => Directory
        .GetFiles(Path.Combine(LocateGodotDirectory(), MeshDirectory), "*.obj")
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    private static string ReadGodotSource(string fileName) =>
        File.ReadAllText(Path.Combine(LocateGodotDirectory(), fileName));

    private static string LocateGodotDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "OnslaughtRebuild.Godot");
            if (Directory.Exists(Path.Combine(candidate, MeshDirectory)))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate OnslaughtRebuild.Godot/{MeshDirectory} above {AppContext.BaseDirectory}.");
    }
}
