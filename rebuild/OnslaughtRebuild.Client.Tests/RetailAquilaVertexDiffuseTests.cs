// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The three retained Aquila specimens carry the same per-vertex D3DCOLOR
/// DIFFUSE dword the static world does, and the released renderer consumes it.
///
/// The render-state evidence is stated once, in
/// <see cref="Level100VertexDiffuseTests"/>: read read-only out of the
/// maintainer Ghidra database, <c>D3DStateCache__UseDefaultRenderState</c> at
/// <c>0x004EB1E0</c> sets <c>D3DRS_LIGHTING</c> TRUE,
/// <c>D3DRS_DIFFUSEMATERIALSOURCE</c> and <c>D3DRS_AMBIENTMATERIALSOURCE</c>
/// both <c>D3DMCS_COLOR1</c>, and stage zero
/// <c>COLORARG1 = D3DTA_TEXTURE</c> / <c>COLORARG2 = D3DTA_DIFFUSE</c>;
/// <c>D3DRS_COLORVERTEX</c> (<c>0x8D</c>) is written at none of the 490 render-state call
/// sites and so keeps its <c>TRUE</c> Direct3D <b>9</b> default; and the base mesh
/// pass runs lit — now read at runtime rather than inferred, with
/// <c>D3DRS_LIGHTING</c> at <c>[0x00855764]</c> equal to <c>1</c> across all
/// 576 world and tree mesh draws of a Level 100 frame, and the mode-2 and
/// mode-6 branches of <c>CMeshRenderer__RenderMeshCore</c> that do clear
/// lighting never executing at all (breakpoints on their draw calls at
/// <c>0x0054a423</c> and <c>0x0054a466</c> did not fire across 4,393 observed
/// mesh renders).
///
/// These tests measure the bytes of the retained specimens themselves rather
/// than restating that. The walker loader is a Godot-project type this test
/// assembly cannot reference, so the vertex stream is re-walked here from the
/// materialized <c>.aya</c> files with an independent reader.
/// </summary>
public sealed class RetailAquilaVertexDiffuseTests
{
    private const string SourceDirectory = "Assets/Aquila/Source";

    /// <summary>
    /// Owned vertices per specimen. A part carrying <c>REFR</c> shares an
    /// earlier part's vertex stream and owns no bytes of its own, so these are
    /// the distinct stride-36 records actually present in the file.
    /// </summary>
    public static TheoryData<string, int, int, int> Specimens => new()
    {
        { "m_f_be1.msh.aya", 5_520, 203, 153 },
        { "m_f_be2.msh.aya", 6_588, 203, 153 },
        { "m_cockpit2.msh.aya", 745, 48, 16 },
    };

    [Theory]
    [MemberData(nameof(Specimens))]
    public void EachSpecimenCarriesTheMeasuredVertexDiffuseDistribution(
        string fileName,
        int expectedVertices,
        int expectedNonWhite,
        int expectedDark)
    {
        uint[] diffuse = ReadOwnedVertexDiffuse(fileName);

        Assert.Equal(expectedVertices, diffuse.Length);
        Assert.Equal(expectedNonWhite, diffuse.Count(color => (color & 0x00FFFFFFu) != 0x00FFFFFFu));
        Assert.Equal(
            expectedDark,
            diffuse.Count(color =>
                (color & 0x00FFFFFFu) != 0x00FFFFFFu &&
                Math.Max(Math.Max((color >> 16) & 0xFF, (color >> 8) & 0xFF), color & 0xFF) < 200));
    }

    /// <summary>
    /// Stage zero also runs <c>D3DTSS_ALPHAOP = MODULATE</c> against
    /// <c>D3DTA_DIFFUSE</c>, so a non-opaque dword would be a real alpha term.
    /// Every vertex of all three specimens is alpha 255, matching the
    /// 278,173-vertex retail corpus, and the loader refuses anything else
    /// rather than silently dropping it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Specimens))]
    public void EveryVertexDiffuseAlphaIsOpaque(
        string fileName,
        int expectedVertices,
        int expectedNonWhite,
        int expectedDark)
    {
        _ = expectedVertices;
        _ = expectedNonWhite;
        _ = expectedDark;

        Assert.All(ReadOwnedVertexDiffuse(fileName), color => Assert.Equal(0xFFu, color >> 24));
    }

    /// <summary>
    /// The walker's 203 coloured vertices are six exact authored D3DCOLOR
    /// values, all neutral greys. Any drift here would mean the reader is
    /// taking the wrong bytes of the stride-36 record.
    /// </summary>
    [Fact]
    public void TheWalkerColouredVerticesAreTheExactAuthoredD3DColors()
    {
        Dictionary<uint, int> counts = ReadOwnedVertexDiffuse("m_f_be1.msh.aya")
            .Where(color => (color & 0x00FFFFFFu) != 0x00FFFFFFu)
            .GroupBy(color => color)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(
            new Dictionary<uint, int>
            {
                [0xFF6A6F75] = 66,
                [0xFFEDEDED] = 50,
                [0xFF363636] = 27,
                [0xFF888888] = 26,
                [0xFF0A0A0A] = 18,
                [0xFF050505] = 16,
            },
            counts);
    }

    [Fact]
    public void TheWalkerLoaderReadsTheDiffuseDwordAndBindsTheColorArray()
    {
        string loader = ReadGodotSource("RetailAquilaWalkerAsset.cs");

        Assert.Contains("colors[index] = ReadDiffuse(data, offset + 24);", loader);
        Assert.Contains("arrays[(int)Mesh.ArrayType.Color] = geometry.Colors;", loader);
        Assert.Contains(
            "throw new InvalidDataException(\"The retained Aquila walker has a non-opaque vertex diffuse alpha.\");",
            loader);

        // The walker, jet and cockpit all draw through the shared fixed-function
        // material, whose lit vertex colour is already modulated by COLOR.rgb.
        // The stage-zero colour operation travels with the profile, because
        // retail's differs between them.
        Assert.Contains(
            "material = RetailFixedFunctionMaterial.Create(\n" +
            "                        layers,\n" +
            "                        terrain,\n" +
            "                        stageZeroColorOperation: stageZeroColorOperation);",
            loader.Replace("\r\n", "\n"));
        Assert.Contains("profile.StageZeroColorOperation);", loader);
        Assert.Contains("vertex_light_color *= COLOR.rgb;", ReadGodotSource("Level100StaticWorldAsset.cs"));
    }

    /// <summary>
    /// Only the cockpit runs at <c>D3DTOP_MODULATE</c>, and it runs there on
    /// two independent measurements.
    ///
    /// Bytes: stage-zero <c>D3DTSS_COLOROP</c>, read from the
    /// texture-stage-state shadow at <c>0x008557f4</c> inside the cockpit
    /// render wrapper (<c>0x0053bb50</c> to its one return at
    /// <c>0x0053ec6f</c>), is <c>4</c> = <c>D3DTOP_MODULATE</c> on all seven
    /// draw batches. It is read 16 times inside that window — the block dump at
    /// entry, the seven <c>SetTransform</c> batch markers at <c>0x00551043</c>,
    /// the seven <c>CMeshRenderer::RenderMeshCore</c> entries and the block dump
    /// at exit — and is <c>4</c> every time, with zero stage-zero
    /// <c>COLOROP</c> transitions while inside; the last change before entry is
    /// <c>5 -&gt; 4</c> written from <c>0x0055ae02</c>.
    ///
    /// Pixels: over the 31,546-pixel geometric intersection mask on which the
    /// same cockpit surface point is visible in both images, multiplying the
    /// build by 0.5 takes clean-mask <c>meanD</c> from 42.51 to 11.42 and
    /// <c>compare_capture</c>'s material fraction from 98.93% to 25.35%, with
    /// 69.9% of pixels then within 8/255 on every channel. Exact 0.5 beats the
    /// least-squares scale 0.5249 and the free per-channel scales
    /// (0.478, 0.500, 0.557).
    ///
    /// The walker and jet profiles are <b>not</b> measured — the census was
    /// taken from a first-person frame in which the exterior Aquila is not
    /// drawn — so they keep <c>MODULATE2X</c> unchanged. Asserting all three
    /// together pins the change as conditional in both directions.
    /// </summary>
    [Fact]
    public void OnlyTheCockpitProfileCarriesTheMeasuredModulateStageZeroOperation()
    {
        Assert.Equal(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["s_walkerProfile"] = "Modulate2X",
                ["s_jetProfile"] = "Modulate2X",
                ["s_cockpitProfile"] = "Modulate",
            },
            ReadProfileStageZeroColorOperations());
    }

    /// <summary>
    /// <c>Lsidebit02</c> must not be special-cased. The name appears in the
    /// loader only as recorded prose and never as a code path.
    ///
    /// The clean-mask fit found that part — batch 3, 366 px, 1.2% of the mask
    /// and the brightest surface in it — already correct at scale 1.0
    /// (0.920/0.969/1.049) and a factor of ~1.9 too dark at 0.5, i.e. it read
    /// as wanting the doubling kept while the three parts carrying 97.2% of
    /// the mask (<c>hood</c>, <c>Object03</c>, <c>Object01</c>) land within
    /// 2-12% of retail once halved. Stage-zero <c>COLOROP</c> does not explain
    /// it: the runtime read is <c>4</c> on all seven batches with no per-batch
    /// variation at all.
    ///
    /// That residual is now CLOSED, and the answer is that the 366 samples are
    /// not cockpit pixels in either image — see
    /// <c>local-lab/COCKPIT-SIDEBIT-RESIDUAL-2026-07-26.md</c> and
    /// <see cref="TheTwoMirroredBatchSamplesAreRecordedAsOverlayContaminationNotShading"/>.
    /// So the not-fitted property this test pins is not merely prudent, it is
    /// correct: there is nothing to fit.
    /// </summary>
    [Fact]
    public void TheLsidebit02ResidualIsRecordedAsOpenAndNotFitted()
    {
        string[] lines = ReadGodotSource("RetailAquilaWalkerAsset.cs")
            .Replace("\r\n", "\n")
            .Split('\n');
        string[] mentions = lines
            .Where(line => line.Contains("Lsidebit02", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(mentions);
        Assert.All(mentions, line => Assert.StartsWith("//", line.TrimStart(), StringComparison.Ordinal));

        // The batch-to-part mapping the cockpit reading rests on, captured
        // through SetTransform(D3DTS_WORLDMATRIX(0)) at 0x00551043.
        Assert.Contains(
            lines,
            line => line.Contains(
                "0=hood, 1=Rsidebit01, 2=Rsidebit02, 3=Lsidebit02, 4=Lsidebit01,",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Both negative-determinant batches — <c>Rsidebit01</c> (batch 1) and
    /// <c>Lsidebit02</c> (batch 3) — are recorded as measurement
    /// contamination, not as parts needing their own shading, and neither
    /// name is ever a code path.
    ///
    /// The measurement is a differential one and needs no assumption about
    /// what is on top: halving this material's stage-zero gain moved all five
    /// positive-determinant batches' own footprints by 1.79-2.24, and moved
    /// these two by 1.000/1.002/1.001 and 0.998/0.993/0.981 respectively. A
    /// pixel whose level is invariant under a change to a material is not
    /// written by that material, so those samples cannot report its shading in
    /// either direction. This pins the closure so that a later session cannot
    /// quietly reintroduce a per-part factor by citing the retired residual.
    /// </summary>
    [Fact]
    public void TheTwoMirroredBatchSamplesAreRecordedAsOverlayContaminationNotShading()
    {
        string loader = ReadGodotSource("RetailAquilaWalkerAsset.cs");
        string[] lines = loader.Replace("\r\n", "\n").Split('\n');

        foreach (string part in new[] { "Rsidebit01", "Lsidebit02" })
        {
            string[] mentions = lines
                .Where(line => line.Contains(part, StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(mentions);
            Assert.All(
                mentions,
                line => Assert.StartsWith("//", line.TrimStart(), StringComparison.Ordinal));
        }

        // The differential measurement that closes the residual, and the note
        // that carries it. Both must survive for the closure to be citable.
        Assert.Contains("1.000/1.002/1.001", loader, StringComparison.Ordinal);
        Assert.Contains("0.998/0.993/0.981", loader, StringComparison.Ordinal);
        Assert.Contains(
            "local-lab/COCKPIT-SIDEBIT-RESIDUAL-2026-07-26.md",
            loader,
            StringComparison.Ordinal);

        // And the batch-to-part mapping the whole reading rests on, so the two
        // names above cannot drift onto different batches.
        Assert.Contains(
            lines,
            line => line.Contains(
                "0=hood, 1=Rsidebit01, 2=Rsidebit02, 3=Lsidebit02, 4=Lsidebit01,",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Each <c>AssetProfile</c> declaration in the loader, mapped to the
    /// <c>RetailStageZeroColorOperation</c> member it passes. Read out of the
    /// source because the profiles are private to a Godot-project type this
    /// test assembly cannot reference.
    /// </summary>
    private static Dictionary<string, string> ReadProfileStageZeroColorOperations()
    {
        const string declaration = "private static readonly AssetProfile ";
        const string member = "RetailStageZeroColorOperation.";
        string loader = ReadGodotSource("RetailAquilaWalkerAsset.cs");
        var operations = new Dictionary<string, string>(StringComparer.Ordinal);
        int at = loader.IndexOf(declaration, StringComparison.Ordinal);
        while (at >= 0)
        {
            int nameEnd = loader.IndexOf(" = new(", at, StringComparison.Ordinal);
            Assert.True(nameEnd > at);
            string name = loader[(at + declaration.Length)..nameEnd];
            int next = loader.IndexOf(declaration, nameEnd, StringComparison.Ordinal);
            string body = next < 0 ? loader[nameEnd..] : loader[nameEnd..next];

            int operation = body.IndexOf(member, StringComparison.Ordinal);
            Assert.True(operation >= 0, $"{name} states no stage-zero colour operation.");
            int end = body.IndexOf(')', operation);
            Assert.True(end > operation);
            operations.Add(name, body[(operation + member.Length)..end]);
            at = next;
        }
        return operations;
    }

    /// <summary>
    /// Two of the seven cockpit draw batches carry a negative-determinant
    /// (mirrored) world matrix. Captured live at the Level 100 cockpit draw
    /// through <c>IDirect3DDevice9::SetTransform(D3DTS_WORLDMATRIX(0))</c> at
    /// <c>0x00551043</c>, the seven batches are, in order, <c>hood</c>,
    /// <c>Rsidebit01</c>, <c>Rsidebit02</c>, <c>Lsidebit02</c>,
    /// <c>Lsidebit01</c>, <c>Object03</c>, <c>Object01</c> — matched by
    /// composed translation to within 2.0e-5 units and by rotation to within
    /// 2.0e-8 elementwise. Batches 1 and 3 are the mirrored pair.
    ///
    /// The mirror is authored in the shipped <c>HORI</c> bytes, so the importer
    /// reproduces it simply by reading them: <c>ToGodotTransform</c> conjugates
    /// by <c>MapVector</c>, which is an involution, so the composed Godot basis
    /// keeps the part's determinant.
    /// </summary>
    [Fact]
    public void ExactlyTheTwoMirroredCockpitPartsCarryANegativeDeterminant()
    {
        Assert.Equal(
            new[] { "Rsidebit01", "Lsidebit02" },
            ReadCockpitParts()
                .Where(part => part.Groups.Count > 0 && part.Determinant < 0f)
                .Select(part => part.Name)
                .ToArray());
    }

    /// <summary>
    /// The authoring tool emits a mirrored instance with its triangle-strip
    /// index order already reversed, so a part's local winding agrees with its
    /// own stored normals if and only if the part's transform has positive
    /// determinant. That is why nothing in the import path needs a
    /// determinant-conditional winding or normal fix-up: the mirror in the
    /// transform and the pre-reversal in the indices cancel, in Direct3D and in
    /// Godot alike.
    ///
    /// The two mirrored parts disagree on every single oriented triangle and
    /// the five unmirrored ones agree on all but 50 of <c>Object03</c>'s 499
    /// (shared by <c>Object01</c>, which references its stream). Those 50 sit
    /// in the strip's smoothed vertex normals at cosines between -0.002 and
    /// -0.664, which is ordinary crease behaviour, not a mirror: the same walk
    /// over <c>m_m_warehouse.msh.aya</c>'s twenty-eight drawing parts is
    /// exceptionless, with its four negative-determinant parts (<c>Box11</c>
    /// and three <c>X2powerchimneybackleft</c> instances) disagreeing on all of
    /// their triangles and the other twenty-four agreeing on all of theirs.
    ///
    /// The exact counts are asserted so that a future change that reverses
    /// winding unconditionally, or negates the imported normals, fails here.
    /// </summary>
    [Fact]
    public void CockpitLocalWindingAgreesWithTheStoredNormalsExactlyWhenTheTransformIsNotMirrored()
    {
        var measured = new Dictionary<string, (int Agree, int Disagree)>(StringComparer.Ordinal);
        foreach (CockpitPart part in ReadCockpitParts().Where(part => part.Groups.Count > 0))
        {
            int agree = 0;
            int disagree = 0;
            foreach (int[] strip in part.Groups)
            {
                for (int ordinal = 0; ordinal + 2 < strip.Length; ordinal++)
                {
                    int a = strip[ordinal];
                    int b = strip[ordinal + 1];
                    int c = strip[ordinal + 2];
                    if (a == b || b == c || a == c)
                    {
                        continue;
                    }
                    if ((ordinal & 1) != 0)
                    {
                        (a, b) = (b, a);
                    }

                    float[] pa = part.Positions[a];
                    float[] pb = part.Positions[b];
                    float[] pc = part.Positions[c];
                    float[] u = [pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2]];
                    float[] v = [pc[0] - pa[0], pc[1] - pa[1], pc[2] - pa[2]];
                    float[] face =
                    [
                        (u[1] * v[2]) - (u[2] * v[1]),
                        (u[2] * v[0]) - (u[0] * v[2]),
                        (u[0] * v[1]) - (u[1] * v[0]),
                    ];
                    float projection = 0f;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        projection += face[axis] *
                            (part.Normals[a][axis] + part.Normals[b][axis] + part.Normals[c][axis]);
                    }
                    if (projection == 0f)
                    {
                        continue;
                    }
                    if (projection > 0f)
                    {
                        agree++;
                    }
                    else
                    {
                        disagree++;
                    }
                }
            }

            measured.Add(part.Name, (agree, disagree));
            if (part.Determinant < 0f)
            {
                Assert.Equal(0, agree);
            }
        }

        Assert.Equal(
            new Dictionary<string, (int Agree, int Disagree)>(StringComparer.Ordinal)
            {
                ["hood"] = (160, 0),
                ["Rsidebit01"] = (0, 46),
                ["Rsidebit02"] = (46, 0),
                ["Lsidebit02"] = (0, 46),
                ["Lsidebit01"] = (46, 0),
                ["Object03"] = (454, 50),
                ["Object01"] = (454, 50),
            },
            measured);
    }

    /// <summary>
    /// The imported normal must travel through <c>MapVector</c> unnegated.
    ///
    /// <c>MapVector</c> is <c>(x, -z, -y)</c>: orthogonal, symmetric, its own
    /// inverse, determinant -1. <c>ToGodotTransform</c> conjugates each part
    /// basis by it. Carrying the shipped mesh bytes and the captured cockpit
    /// root through both, and the shipped HFLD sun through
    /// <c>Level100HeightFieldAsset</c>'s identical mapping, reproduces retail's
    /// <c>N.L</c> at every one of the cockpit's 1,340 vertices with a maximum
    /// absolute difference of exactly zero. Negating the normals here — or
    /// applying the determinant-carrying cofactor <c>det(M) M^-T = -M</c>
    /// instead of <c>M</c> — would break that identity, not restore it.
    /// </summary>
    [Fact]
    public void TheWalkerLoaderMapsNormalsWithoutNegatingThem()
    {
        string loader = ReadGodotSource("RetailAquilaWalkerAsset.cs");

        Assert.Contains("Vector3 normal = ReadVector3(data, offset + 12);", loader);
        Assert.Contains("normals[index] = MapVector(normal).Normalized();", loader);
        Assert.Contains(
            "private static Vector3 MapVector(Vector3 value) => new(value.X, -value.Z, -value.Y);",
            loader);
        Assert.Contains(
            "vec3 world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);",
            ReadGodotSource("Level100StaticWorldAsset.cs"));
    }

    /// <summary>
    /// Retail <c>CMeshPart__InterpolateSegmentTransform</c> at
    /// <c>0x004b0d00</c> interpolates all nine stored <c>HORI</c> components
    /// independently. It does not convert them to quaternions, which would
    /// discard authored scale/shear terms and change fractional poses.
    /// </summary>
    [Fact]
    public void WalkerTransformInterpolationUsesTheRetailComponentwiseMatrixLaw()
    {
        string loader = ReadGodotSource("RetailAquilaWalkerAsset.cs");

        Assert.Contains(
            "Matrix3.Lerp(first.Rotation, second.Rotation, weight)",
            loader,
            StringComparison.Ordinal);
        Assert.Contains(
            "first.Position.Lerp(second.Position, weight)",
            loader,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Slerp(", loader, StringComparison.Ordinal);

        foreach (string component in new[]
                 {
                     "M00", "M01", "M02",
                     "M10", "M11", "M12",
                     "M20", "M21", "M22",
                 })
        {
            Assert.Contains(
                $"first.{component} + ((second.{component} - first.{component}) * weight)",
                loader,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The cockpit's part records, sequentially walked: name, the local
    /// <c>HORI</c> transform selected by <c>VHFM</c> at virtual frame 25 (the
    /// profile's initial frame), its determinant, and each group's index strip
    /// alongside the owned stride-36 positions and normals.
    /// </summary>
    private static List<CockpitPart> ReadCockpitParts()
    {
        byte[] data = Inflate(File.ReadAllBytes(
            Path.Combine(LocateGodotDirectory(), SourceDirectory, "m_cockpit2.msh.aya")));
        int cursor = 0;
        int Length(int at) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 4, 4));
        float Single(int at) => BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at, 4)));

        Assert.True(Tag(data, cursor, "CMSH"));
        int partCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(cursor + 8 + 0x15C, 4));
        cursor += 8 + Length(cursor);
        Assert.True(Tag(data, cursor, "CMST"));
        cursor += 8 + Length(cursor);
        while (Tag(data, cursor, "MSHT"))
        {
            cursor += 8 + Length(cursor);
        }

        var parts = new List<CockpitPart>(partCount);
        for (int index = 0; index < partCount; index++)
        {
            Assert.True(Tag(data, cursor, "MESP"));
            int end = cursor + 8 + Length(cursor);
            int inner = cursor + 8;
            Assert.True(Tag(data, inner, "CMSP"));
            int cmsp = inner + 8;
            int virtualFrames = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(cmsp + 0xB8, 4));
            int storedFrames = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(cmsp + 0xBC, 4));
            string name = Encoding.ASCII.GetString(
                data.AsSpan(cmsp + 0xDC, 32).ToArray()).Split('\0')[0];
            inner += 8 + Length(inner);

            byte[] frameMap = [];
            int orientations = -1;
            int reference = -1;
            var groups = new List<int[]>();
            var positions = new List<float[]>();
            var normals = new List<float[]>();
            while (inner < end)
            {
                string tag = Encoding.ASCII.GetString(data, inner, 4);
                int payload = inner + 8;
                int length = Length(inner);
                switch (tag)
                {
                    case "VHFM":
                        frameMap = data.AsSpan(payload, length).ToArray();
                        break;
                    case "HORI":
                        orientations = payload;
                        break;
                    case "REFR":
                        reference = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(payload, 4));
                        break;
                    case "PMVB":
                        ReadGeometry(data, payload, payload + length, groups, positions, normals);
                        break;
                }
                inner = payload + length;
            }

            // A REFR part owns no vertex stream; it draws an earlier part's
            // geometry. Lsidebit02 references Rsidebit01 and Lsidebit01
            // references Rsidebit02, so the shared geometry is paired by
            // determinant and the left/right mirror comes from the transform.
            if (reference >= 0 && groups.Count == 0)
            {
                CockpitPart source = parts[reference];
                groups.AddRange(source.Groups);
                positions.AddRange(source.Positions);
                normals.AddRange(source.Normals);
            }

            Assert.Equal(virtualFrames, frameMap.Length);
            Assert.True(orientations >= 0);
            int frame = frameMap[Math.Min(25, virtualFrames - 1)];
            Assert.True(frame < storedFrames);
            int matrix = orientations + (frame * 48);
            float[] rows = new float[9];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    rows[(row * 3) + column] = Single(matrix + (row * 16) + (column * 4));
                }
            }
            float determinant =
                (rows[0] * ((rows[4] * rows[8]) - (rows[5] * rows[7]))) -
                (rows[1] * ((rows[3] * rows[8]) - (rows[5] * rows[6]))) +
                (rows[2] * ((rows[3] * rows[7]) - (rows[4] * rows[6])));
            parts.Add(new CockpitPart(name, determinant, groups, positions, normals));
            cursor = end;
        }
        return parts;
    }

    private static void ReadGeometry(
        byte[] data,
        int cursor,
        int end,
        List<int[]> groups,
        List<float[]> positions,
        List<float[]> normals)
    {
        int Length(int at) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 4, 4));
        float Single(int at) => BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at, 4)));

        Assert.True(Tag(data, cursor, "CMVB"));
        cursor += 8 + Length(cursor);
        while (cursor < end && Tag(data, cursor, "MMPT"))
        {
            int mmpt = cursor + 8;
            int indexCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(mmpt + 8, 4));
            int vertexCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(mmpt + 12, 4));
            cursor += 8 + Length(cursor);

            Assert.True(Tag(data, cursor, "IBUF"));
            int ibuf = cursor + 8;
            int[] strip = new int[indexCount];
            for (int index = 0; index < indexCount; index++)
            {
                strip[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                    data.AsSpan(ibuf + (index * 2), 2));
            }
            groups.Add(strip);
            cursor += 8 + Length(cursor);

            Assert.True(Tag(data, cursor, "VBUF"));
            if (Length(cursor) == vertexCount * 36 && positions.Count == 0)
            {
                int vbuf = cursor + 8;
                for (int index = 0; index < vertexCount; index++)
                {
                    int record = vbuf + (index * 36);
                    positions.Add([Single(record), Single(record + 4), Single(record + 8)]);
                    normals.Add([Single(record + 12), Single(record + 16), Single(record + 20)]);
                }
            }
            cursor += 8 + Length(cursor);

            Assert.True(Tag(data, cursor, "TEXR"));
            cursor += 8 + Length(cursor);
        }
    }

    private sealed record CockpitPart(
        string Name,
        float Determinant,
        List<int[]> Groups,
        List<float[]> Positions,
        List<float[]> Normals);

    /// <summary>
    /// Walks the inflated CMSH stream for group-zero <c>MMPT</c>/<c>IBUF</c>/
    /// <c>VBUF</c> triples and returns the DIFFUSE dword at offset 24 of every
    /// owned stride-36 record. The declared vertex bytes, index bytes,
    /// primitive count and active flag all have to agree before a run is
    /// accepted, and secondary groups declare a zero-length <c>VBUF</c> because
    /// they reuse the owner's stream.
    /// </summary>
    private static uint[] ReadOwnedVertexDiffuse(string fileName)
    {
        byte[] data = Inflate(File.ReadAllBytes(
            Path.Combine(LocateGodotDirectory(), SourceDirectory, fileName)));
        var diffuse = new List<uint>();
        int position = 0;
        while (position + 8 <= data.Length)
        {
            if (!Tag(data, position, "MMPT") ||
                BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position + 4, 4)) != 24 ||
                position + 32 > data.Length)
            {
                position++;
                continue;
            }

            uint vertexBytes = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 8, 4));
            uint indexBytes = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 12, 4));
            uint indexCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 16, 4));
            uint vertexCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 20, 4));
            uint primitiveCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 24, 4));
            uint active = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 28, 4));
            int index = position + 32;
            if (active != 1 || indexCount < 3 || primitiveCount != indexCount - 2 ||
                indexBytes != indexCount * 2 || vertexBytes != vertexCount * 36 ||
                !Tag(data, index, "IBUF") ||
                BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index + 4, 4)) != indexBytes)
            {
                position++;
                continue;
            }

            int vertex = index + 8 + (int)indexBytes;
            if (vertex + 8 > data.Length || !Tag(data, vertex, "VBUF"))
            {
                position++;
                continue;
            }

            uint declared = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(vertex + 4, 4));
            int payload = vertex + 8;
            if (declared == vertexBytes && vertexBytes > 0 && payload + vertexBytes <= data.Length)
            {
                for (int record = 0; record < vertexCount; record++)
                {
                    diffuse.Add(BinaryPrimitives.ReadUInt32LittleEndian(
                        data.AsSpan(payload + (record * 36) + 24, 4)));
                }
            }
            position = payload + (int)declared;
        }
        return diffuse.ToArray();
    }

    private static bool Tag(byte[] data, int offset, string expected) =>
        offset >= 0 && offset + 4 <= data.Length &&
        Encoding.ASCII.GetString(data, offset, 4) == expected;

    private static byte[] Inflate(byte[] source)
    {
        using var output = new MemoryStream();
        int position = 0;
        while (position < source.Length)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(position, 4));
            position += 4;
            using var compressed = new MemoryStream(source, position, length, writable: false);
            using (var inflater = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true))
            {
                inflater.CopyTo(output);
            }
            position += length;
        }
        return output.ToArray();
    }

    /// <summary>
    /// The cockpit is not drawn in the camera's own basis. Retail's cockpit
    /// render thing takes its orientation from the virtual at
    /// <c>0x004254f0</c>, which composes the 3x4 matrix at
    /// <c>CCockpit+0x2c</c> onto <c>battleEngine+0x3c</c> - the same
    /// orientation <c>CThingCamera::GetOrientation</c> hands the camera. A
    /// controlled read of a copied target at the Level 100 cockpit draw
    /// (window armed at <c>0x0053bb50</c>, disarmed at <c>0x0053ec6f</c>,
    /// dumped at <c>0x0053bb56</c>) found <c>CCockpit+0x2c</c> holding a
    /// 2.877224-degree rotation while <c>battleEngine+0x3c</c> was an exact
    /// pure yaw. These tests pin the arithmetic that carries that matrix into
    /// the renderer, so the shipped constant cannot drift away from the read
    /// that produced it. Evidence:
    /// <c>local-lab/COMPOSITION-RESIDUAL-2026-07-26.md</c>.
    /// </summary>
    private static readonly double[,] RuntimeCockpitShakeMatrix =
    {
        { +0.99880058, +0.02999347, +0.03870098 },
        { -0.03041990, +0.99948227, +0.01047720 },
        { -0.03836670, -0.01164191, +0.99919593 },
    };

    /// <summary>
    /// The composed cockpit-local to D3D-camera basis captured independently
    /// through <c>SetTransform(D3DTS_WORLDMATRIX(0))</c> and
    /// <c>D3DTS_VIEW</c>, i.e. <c>W_0 . R_view</c>.
    /// </summary>
    private static readonly double[,] CapturedCockpitToCameraBasis =
    {
        { +0.99880052, +0.03836669, -0.03041992 },
        { +0.02999346, +0.01164222, +0.99948223 },
        { +0.03870098, -0.99919581, +0.01047750 },
    };

    [Fact]
    public void TheRuntimeCockpitShakeMatrixIsAProperRotationOfTheMeasuredAngle()
    {
        double[,] shake = RuntimeCockpitShakeMatrix;
        Assert.Equal(1.0, Determinant(shake), 6);
        Assert.Equal(0.0, OrthonormalityError(shake), 6);
        double trace = shake[0, 0] + shake[1, 1] + shake[2, 2];
        double degrees = Math.Acos(Math.Clamp((trace - 1.0) / 2.0, -1.0, 1.0)) * 180.0 / Math.PI;
        Assert.Equal(2.877224, degrees, 4);
    }

    [Fact]
    public void TheRuntimeShakeMatrixExplainsTheWholeCapturedCompositionResidual()
    {
        // C = S^T . P, where P is the exact axis map x->x, y->z, z->-y that the
        // renderer realises. This identity is what makes the 2.8774-degree
        // residual the cockpit's own matrix rather than a reconstruction error.
        double[,] axisMap = { { 1, 0, 0 }, { 0, 0, 1 }, { 0, -1, 0 } };
        double[,] predicted = Multiply(Transpose(RuntimeCockpitShakeMatrix), axisMap);
        double worst = 0.0;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                worst = Math.Max(
                    worst,
                    Math.Abs(predicted[row, column] - CapturedCockpitToCameraBasis[row, column]));
            }
        }
        Assert.True(worst < 1e-6, $"S^T . P differs from the captured basis by {worst}.");
    }

    [Fact]
    public void TheGodotCockpitBasisIsTheRuntimeShakeMatrixAndNothingElse()
    {
        // Godot camera space negates D3D's forward axis, and the mesh importer
        // already realises P through MapVector(x, -z, -y). The Basis the
        // renderer must carry is the shake matrix expressed in that frame;
        // derive it here rather than restating the literals, so the shipped
        // constant and the runtime read can only agree if the algebra holds.
        double[,] forwardFlip = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, -1 } };
        double[,] importMap = { { 1, 0, 0 }, { 0, 0, -1 }, { 0, -1, 0 } };
        double[,] axisMap = { { 1, 0, 0 }, { 0, 0, 1 }, { 0, -1, 0 } };
        double[,] expected = Multiply(
            Multiply(
                forwardFlip,
                Transpose(Multiply(Transpose(RuntimeCockpitShakeMatrix), axisMap))),
            importMap);

        double[,] shipped = ParseGodotBasis(
            ReadGodotSource("FirstFlightWorldView.cs"),
            "RetailLevel100CockpitOrientationOffset");
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                Assert.Equal(expected[row, column], shipped[row, column], 6);
            }
        }
    }

    [Fact]
    public void TheCockpitOrientationOffsetDefaultsToIdentityAndKeepsItsProvenance()
    {
        string view = ReadGodotSource("FirstFlightWorldView.cs");

        // The "not modelled" default must stay identity. The measured value is
        // identified, not derived - the shake updater's own recompute produces
        // identity from the observed inputs - so it must not become a global
        // cockpit mount by drifting into the default.
        Assert.Contains(
            "RetailNoCockpitOrientationOffset = Basis.Identity",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "_cockpitAsset.Root.Basis = CockpitOrientationOffset;",
            view,
            StringComparison.Ordinal);

        // The provenance has to name the address, the observation window and
        // the note, so the constant can never be read as a fitted angle.
        string[] required =
        [
            "CCockpit+0x2c",
            "0x004254f0",
            "battleEngine+0x3c",
            "0x0053bb50",
            "0x0053ec6f",
            "MEASURED AND IDENTIFIED, not DERIVED",
            "COMPOSITION-RESIDUAL-2026-07-26.md",
        ];
        foreach (string token in required)
        {
            Assert.Contains(token, view, StringComparison.Ordinal);
        }
    }

    private static double[,] ParseGodotBasis(string source, string name)
    {
        int at = source.IndexOf(name + " = new(", StringComparison.Ordinal);
        Assert.True(at >= 0, $"{name} is not declared as a three-Vector3 Basis.");
        var values = new double[3, 3];
        int cursor = at;
        for (int column = 0; column < 3; column++)
        {
            int open = source.IndexOf("new Vector3(", cursor, StringComparison.Ordinal);
            Assert.True(open >= 0, $"{name} is missing axis {column}.");
            int close = source.IndexOf(')', open);
            string[] parts = source[(open + "new Vector3(".Length)..close].Split(',');
            Assert.Equal(3, parts.Length);
            for (int row = 0; row < 3; row++)
            {
                values[row, column] = double.Parse(
                    parts[row].Trim().TrimEnd('f'),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            cursor = close;
        }
        return values;
    }

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

    private static double[,] Transpose(double[,] source)
    {
        var result = new double[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                result[row, column] = source[column, row];
            }
        }
        return result;
    }

    private static double Determinant(double[,] m) =>
        (m[0, 0] * ((m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1]))) -
        (m[0, 1] * ((m[1, 0] * m[2, 2]) - (m[1, 2] * m[2, 0]))) +
        (m[0, 2] * ((m[1, 0] * m[2, 1]) - (m[1, 1] * m[2, 0])));

    private static double OrthonormalityError(double[,] m)
    {
        double[,] product = Multiply(m, Transpose(m));
        double worst = 0.0;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                worst = Math.Max(worst, Math.Abs(product[row, column] - (row == column ? 1.0 : 0.0)));
            }
        }
        return worst;
    }

    private static string ReadGodotSource(string fileName) =>
        File.ReadAllText(Path.Combine(LocateGodotDirectory(), fileName));

    private static string LocateGodotDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "OnslaughtRebuild.Godot");
            if (Directory.Exists(Path.Combine(candidate, SourceDirectory)))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate OnslaughtRebuild.Godot/{SourceDirectory} above {AppContext.BaseDirectory}.");
    }
}
