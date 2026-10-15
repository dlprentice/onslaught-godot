// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// A retail Level 1.00 pine has exactly two representations and no third one.
///
/// <para>The swap distance is retail's AUTHORED default, <b>30.0</b>, corrected
/// from 70.0 on 2026-07-27 under GOAL.md's defaults rule (task #137). All bytes
/// below are read from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c> —
/// never the installed <c>BEA.exe</c>, which is deliberately patched.
///
/// The field is the <b>Geometry detail</b> options row. Its setter at
/// <c>0x004DD6B0</c> dispatches to exactly three arms, each a
/// <c>mov dword [0x006321A0], imm32</c> (<c>C7 05 A0 21 63 00</c>):
/// <c>0x004DD737</c> = 10.0 Low, <c>0x004DD6FF</c> = 30.0 Medium,
/// <c>0x004DD6CA</c> = 70.0 High. The image's own static initialisers select one
/// arm uniquely: <c>.data 0x006321A0</c> (file <c>0x231CA0</c>) is
/// <c>00 00 F0 41</c> = 30.0, LOD bias <c>0x00631E88</c> (file <c>0x231988</c>)
/// is 1.0 and quality scale <c>0x00630E0C</c> (file <c>0x230F0C</c>) is 1.0 —
/// arm 1, Medium. Low would be 10.0/3.0/0.1 and High 70.0/0.3/2.0.
///
/// <b>70.0 was this machine's graphics setting, not an authored constant.</b>
/// <c>defaultoptions.bea</c> file offset 0x26CA holds <c>00 00 8C 42</c> = 70.0f
/// with 0.3/2.0 beside it — but <c>proof_defaultoptions.bea</c>, in the same
/// install, holds <c>00 00 F0 41</c> = 30.0f with 1.0/1.0 at the identical
/// offsets. That file is RUN STATE, not shipped defaults: no <c>.bea</c> appears
/// in any of <c>INSTALL.LOG</c>'s 5,773 installed-file lines, and the game
/// rewrites it. OptionsTail +0x0C is bound to <c>0x006321A0</c> by the
/// serializer itself (<c>0x00420E0F</c> <c>A3 A0 21 63 00</c> on load,
/// <c>0x00420BA0</c> <c>8B 0D A0 21 63 00</c> on save), which is exactly why a
/// value taken from that file is a user setting and not a default.
///
/// What these tests also prove: every pine representation the reconstruction
/// draws is GATED on one distance, so no pass can survive at all distances. That
/// structural claim is independent of which number the gate holds.</para>
///
/// Archive evidence for the representation count, unaffected by the above: the
/// level's IMPS/IMPT/VIEW chunk stores exactly six views per pine variant, each
/// a 32x32 atlas cell plus two floats, and those float pairs are the variant's
/// own mesh bounding-box half-extents scaled by exactly 1.05 on all three axes
/// (views 0/2 = X by Z, 1/3 = Y by Z, 4/5 = X by Y). That is the six faces of
/// the mesh's own bounding box, not a camera-facing card: a billboard needs one
/// view and one size pair and would never carry a top-down disc.
///
/// These tests pin the authored 30.0 swap and prove that every pine
/// representation the reconstruction draws is gated on it, so no pass can
/// survive at all distances.
/// </summary>
public sealed class Level100PineRepresentationTests
{
    private static readonly string StaticWorldSource = ReadGodotSource(
        "Level100StaticWorldAsset.cs");

    /// <summary>
    /// 30.0f is exactly <c>00 00 F0 41</c> little-endian, the four bytes the
    /// shipped image statically initialises at <c>.data 0x006321A0</c>, file
    /// offset <c>0x231CA0</c>, in the pristine specimen. See the class remarks.
    /// </summary>
    private const int MeshQualityDistanceBits = 0x41F00000;

    [Fact]
    public void ManifestPinsTheAuthoredSwapDistanceBitExact()
    {
        JsonElement billboards = LoadManifest().GetProperty("pineBillboards");

        float distance = billboards.GetProperty("meshQualityDistance").GetSingle();

        Assert.Equal(MeshQualityDistanceBits, BitConverter.SingleToInt32Bits(distance));
        Assert.Equal(
            new byte[] { 0x00, 0x00, 0xF0, 0x41 },
            BitConverter.GetBytes(distance));
        Assert.Equal(30f, distance);
        // The value this replaced. 70.0f is the "High" arm at 0x004DD6CA and was
        // sourced from this machine's defaultoptions.bea; it must not come back.
        Assert.NotEqual(0x428C0000, BitConverter.SingleToInt32Bits(distance));
    }

    [Fact]
    public void EverySourceMeshQualityDistanceIsTheSameAuthoredValue()
    {
        // The close mesh and the far box must gate on one shared value, or a
        // band opens where both discard and the tree disappears.
        MatchCollection matches = Regex.Matches(
            StaticWorldSource,
            @"SingleToInt32Bits\(\s*(\d+(?:\.\d+)?)f\s*\)");

        Assert.Contains(
            matches.Cast<Match>(),
            match => BitConverter.SingleToInt32Bits(
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)) ==
                MeshQualityDistanceBits);
    }

    [Fact]
    public void PineImposterMaterialsCannotBeCreatedWithoutADistanceGate()
    {
        // `meshQualityDistance: null` was what made the removed fast card
        // ungated: it selected an unbounded shader and RenderPriority 1, so the
        // card drew over the mesh at every distance. The parameter is no longer
        // nullable and the factory rejects a non-positive distance.
        Assert.DoesNotContain("meshQualityDistance: null", StaticWorldSource);
        Assert.DoesNotContain("double? meshQualityDistance", StaticWorldSource);
        Assert.Contains(
            "double meshQualityDistance)",
            StaticWorldSource);
        Assert.Contains(
            "mesh-quality distance.\");",
            StaticWorldSource);
        Assert.Contains(
            @"material.SetShaderParameter(""mesh_distance_squared"", distance * distance);",
            StaticWorldSource);
    }

    [Fact]
    public void OnlyTwoPineRepresentationsAreEverAddedToTheWorld()
    {
        // AddPines is the single call site that builds pine geometry.
        string body = Between(
            StaticWorldSource,
            "private static int AddPines(",
            "private static PinePlacement[] BuildPinePlacements(");

        Assert.Contains("AddClosePineMeshes(", body);
        Assert.Contains("AddFarPineImposters(", body);
        Assert.DoesNotContain("AddFastPineImposters", StaticWorldSource);
        Assert.DoesNotContain("PineFastImposterRoot", StaticWorldSource);
        Assert.DoesNotContain("PineFastImposterShaderCode", StaticWorldSource);
        Assert.DoesNotContain("camera_facing", StaticWorldSource);
    }

    [Fact]
    public void TheTwoPineShaderGatesAreComplementaryAboutTheSameDistance()
    {
        // Mesh material discards beyond the distance; the box discards at or
        // inside it. Complementary, hard, no fade — matching the byte-authored
        // hard swap with no cross-dissolve anywhere in the data or shaders.
        Assert.Contains(
            "horizontal_distance_squared > maximum_horizontal_distance_squared",
            StaticWorldSource);
        Assert.Contains(
            "horizontal_distance_squared <= mesh_distance_squared",
            StaticWorldSource);
        Assert.DoesNotContain("smoothstep", StaticWorldSource);
        Assert.DoesNotContain("RenderPriority = far", StaticWorldSource);
    }

    [Fact]
    public void NoPineGeometryIsAddedOutsideAShaderGatedMultiMesh()
    {
        // Any MultiMeshInstance3D named for pines must belong to one of the two
        // gated passes. A third batch would show up here.
        string[] names = Regex.Matches(
                StaticWorldSource,
                @"Name = \$""(RetailPine[A-Za-z0-9{}]*)""")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(
            [
                "RetailPineSnow{variant}CloseMeshInstances",
                "RetailPineSnow{variant}FarSixFaceInstances",
            ],
            names.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The recovered pine seating law: the PIVOT lands on the sampled support
    /// and nothing mesh-derived is added to it.
    ///
    /// <para>The authored pine record is (X, Y, variant) — twelve bytes, no Z —
    /// so the released code owns the whole vertical datum, and
    /// <c>CTree::Init</c> at <c>0x004F6080</c> supplies it in five instructions:
    /// <c>0x004F61B9 MOV EBX,[ESP+0x58C]</c> (the single stack argument, the
    /// <c>CTreeInitThing*</c>), <c>0x004F61C4 LEA EDX,[EBX+4]</c>
    /// (&amp;<c>mPos</c>), <c>0x004F61C7 MOV ECX,0x006FADC8</c> (the MAP
    /// singleton), <c>0x004F61DD CALL 0x0047EB80</c> (the height query — the
    /// same receiver and callee <c>CThing::Init</c> uses at <c>0x004F34F6</c>
    /// and <c>0x004F34FB</c>) and <c>0x004F61F4 FSTP DWORD PTR [EBX+0xC]</c>,
    /// which writes the returned height straight into <c>mPos.Z</c>.
    /// <c>[EBX+0xC]</c> is Z because <c>CThing::Init</c> copies
    /// <c>[EBX+4]</c>..<c>[EBX+0x10]</c> into <c>this+0x1c</c>..<c>this+0x28</c>
    /// with Z at <c>+0x24</c>. <b>No FADD, FSUB or FMUL sits between the CALL
    /// and the FSTP, and no mesh extent is read anywhere in the body</b>, which
    /// ends <c>RET 4</c> at <c>0x004F63AE</c> after
    /// <c>0x004F6363 CALL 0x004F34A0</c> — <c>CThing::Init</c>, whose own two
    /// clamps are equally bare.</para>
    ///
    /// <para>Both draw paths inherit that pivot unchanged, and the mesh path
    /// cannot perturb it even in principle: the matrix a tree hands the
    /// renderer carries <b>no translation at all</b>.
    /// <c>CRTTree::VFuncSlot02_BuildRenderOutputs</c> (<c>0x004DD960</c>) takes
    /// the rotation from <c>CTree::GetMatrix</c> (<c>0x004F6560</c>, secondary
    /// vtable <c>0x005DD960</c> slot <c>+0x04</c>), whose tail
    /// <c>SHL ESI,4 / ADD ESI,0x008406B8 / REP MOVSD</c>
    /// (<c>0x004F65F6</c>..<c>0x004F65FF</c>) copies twelve dwords out of the
    /// Z-rotation table <c>CTree::Init</c> builds at <c>0x008406B8</c>, and
    /// takes the position separately from <c>CTree::GetPos</c>
    /// (<c>0x004040A0</c>, secondary vtable slot <c>+0x00</c>) — four verbatim
    /// dword copies out of <c>this+0x1c</c>.
    /// <c>CDXEngine::RenderImposterBillboardSet</c> (<c>0x00543300</c>) forms
    /// <c>thingPos + M*centerOffset</c> — <c>FADD</c> at <c>0x005434AB</c>,
    /// <c>0x005434C8</c>, <c>0x005434D8</c> against the vector from
    /// <c>CALL 0x00401EC0</c> — where <c>centerOffset</c> is <c>mesh+0x150</c>,
    /// the bounding box CENTRE, already carried inside the imposter geometry,
    /// not its minimum.</para>
    ///
    /// <para><c>CRTTree::Init</c> (<c>0x004DD7B0</c>) writes nothing to the
    /// tree's position; the two mesh scalars it does cache
    /// (<c>mesh+0x164</c> to <c>CRTTree+0x24</c> at <c>0x004DD80A</c>,
    /// <c>mesh+0x168</c> to <c>CRTTree+4</c> at <c>0x004DD813</c>) are read by
    /// neither submit path.</para>
    ///
    /// <para>The decisive negative: retail HAS a per-class vertical
    /// ground-clearance hook at vtable slot <c>+0xC0</c>, used by the
    /// ground-follow routine at <c>0x004017C0</c>, and <c>CTree</c> does not
    /// override it — <c>CTree</c> (<c>0x005DD9D8+0xC0</c>) and <c>CThing</c>
    /// (<c>0x005DF5C8+0xC0</c>) both hold <c>0x004BFC60</c>, which is
    /// <c>FLD DWORD PTR [0x005D856C] / RET</c> with <c>[0x005D856C]</c> =
    /// <c>0.0f</c>. The engine has the exact mechanism a base-clearance term
    /// would need; trees return zero from it, and <c>CThing::Init</c> never
    /// calls it.</para>
    ///
    /// <para><c>CTree</c> also inherits both seating gates unchanged —
    /// <c>ClipToGround</c> (<c>+0xB0</c>) is <c>0x004014A0</c>
    /// <c>MOV EAX,1 / RET</c> and <c>CanGoUnderWater</c> (<c>+0xC4</c>) is
    /// <c>0x00405930</c> <c>XOR EAX,EAX / RET</c> — so the water clamp does run
    /// for pines and <c>BuildPinePlacements</c>' <c>Math.Max</c> against the
    /// water level is byte-confirmed, not merely measured inert.</para>
    ///
    /// <para>All addresses read from the pristine specimen
    /// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
    /// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
    /// — never the installed <c>BEA.exe</c>, which is deliberately patched.</para>
    ///
    /// <para>A <c>-min(vertexZ)</c> lift used to be added to both pine passes.
    /// It was the last surviving instance of the term
    /// <c>NoMeshDerivedTermIsAddedToTheReleasedStaticClamp</c> removed from the
    /// statics, and it was kept because pines are seated by a different owner
    /// that had not been read. It has now been read, and it agrees. This test
    /// exists so the lift cannot come back.</para>
    /// </summary>
    [Fact]
    public void NoMeshDerivedTermIsAddedToThePineSeating()
    {
        string pines = Between(
            StaticWorldSource,
            "private static void AddClosePineMeshes(",
            "private static ArrayMesh CreateFarPineImposterMesh(");

        // Exactly two placements, and both seat the bare sampled origin. The
        // far box only describes the tree while it shares the mesh's transform,
        // so a lift on one pass alone would also step the tree at the swap.
        Assert.Equal(
            2,
            Regex.Matches(pines, @"instances\[index\]\.GroundOrigin\)\);").Count);
        Assert.DoesNotContain("GroundOrigin +", pines);
        Assert.DoesNotContain("Vector3.Up", pines);
        Assert.DoesNotContain("BaseClearance", pines);
    }

    /// <summary>
    /// What the removed lift was, and what burying it costs.
    ///
    /// <para><c>baseClearance</c> is a pure mesh-space quantity — the converted
    /// OBJ's own <c>-min(vertexZ)</c> — so adding it to the world seating could
    /// only move the tree off the height the released code computes. Under the
    /// recovered law each variant buries 12 to 17 of its own vertices, against
    /// the 56 to 69 turret-collar vertices <c>CThing::Init</c> already buries in
    /// <c>EveryTurretSeatsItsPivotOnTheSampledGroundAndBuriesItsCollar</c>.</para>
    ///
    /// <para>The imposter shares that one transform and therefore dips a
    /// further 5 % of the mesh's own half-height, because the authored VIEW
    /// extents are that bounding box inflated by exactly 1.05. That identity is
    /// the check that the shared transform still encloses the mesh.</para>
    /// </summary>
    [Theory]
    [InlineData(0, 674, 12, 0.04363361862488091)]
    [InlineData(1, 411, 13, 0.024035155307501554)]
    [InlineData(2, 586, 17, 0.033704003668390214)]
    [InlineData(3, 598, 13, 0.031834605353651568)]
    public void EveryPineBuriesExactlyItsOwnMeshMinimumAndTheBoxStillEnclosesIt(
        int variant,
        int meshVertexCount,
        int verticesBelowThePivot,
        double expectedBaseClearance)
    {
        JsonElement manifest = LoadManifest();
        double baseClearance = manifest
            .GetProperty("meshes")
            .GetProperty($"pinesnow{variant}")
            .GetProperty("baseClearance")
            .GetDouble();
        double[] vertexZ = MeshVertexZ($"pinesnow{variant}.obj");

        // The removed term is a mesh-space datum and nothing else.
        Assert.Equal(meshVertexCount, vertexZ.Length);
        Assert.Equal(expectedBaseClearance, baseClearance, 12);
        Assert.Equal(baseClearance, -vertexZ.Min(), 12);
        Assert.Equal(
            verticesBelowThePivot,
            vertexZ.Count(value => value < -1e-9));

        JsonElement definition = manifest
            .GetProperty("pineBillboards")
            .GetProperty("variants")[variant];
        // Godot Y for the BEA centre offset is -Z. View 0 is the X-by-Z side
        // elevation, so element 5 is the Z half-extent.
        double centerHeight = -definition.GetProperty("centerOffset")[2].GetDouble();
        double halfHeight = definition.GetProperty("views")[0][5].GetDouble();
        double meshHalfHeight = halfHeight / 1.05;

        // Seated at the sample, the mesh foot is its own minimum below ground
        // and the box bottom is that plus the authored 5 % inflation, so the
        // box encloses the mesh and neither representation floats.
        // Tolerance, not decimal places: the VIEW extents are stored as float32,
        // so the identity holds to ~7e-8 and one variant straddles the 6-place
        // rounding boundary. The lift this replaced was 0.024 to 0.044 — four
        // orders of magnitude outside this — so the check still catches it.
        double boxBottom = centerHeight - halfHeight;
        Assert.Equal(
            -(baseClearance + (0.05 * meshHalfHeight)),
            boxBottom,
            tolerance: 1e-6);
        Assert.True(
            boxBottom < -baseClearance,
            $"variant {variant} box bottom {boxBottom} does not enclose the mesh foot");
    }

    [Fact]
    public void EveryVariantStoresSixViewsMatchingItsOwnBoundingBoxAxes()
    {
        // Six views with three distinct size pairs is what rules a billboard
        // out; a camera-facing card needs one view and one pair.
        JsonElement variants = LoadManifest()
            .GetProperty("pineBillboards")
            .GetProperty("variants");

        Assert.Equal(4, variants.GetArrayLength());
        foreach (JsonElement definition in variants.EnumerateArray())
        {
            JsonElement views = definition.GetProperty("views");
            Assert.Equal(6, views.GetArrayLength());
            double x = views[0][4].GetDouble();
            double y = views[1][4].GetDouble();
            double z = views[0][5].GetDouble();

            // 0 and 2 are X by Z, 1 and 3 are Y by Z, 4 and 5 are X by Y.
            Assert.Equal(x, views[2][4].GetDouble(), 5);
            Assert.Equal(z, views[2][5].GetDouble(), 5);
            Assert.Equal(y, views[3][4].GetDouble(), 5);
            Assert.Equal(z, views[1][5].GetDouble(), 5);
            Assert.Equal(x, views[4][4].GetDouble(), 5);
            Assert.Equal(y, views[4][5].GetDouble(), 5);
            Assert.Equal(x, views[5][4].GetDouble(), 5);
            Assert.Equal(y, views[5][5].GetDouble(), 5);
        }
    }

    [Fact]
    public void TheFarBoxDrawsTheCameraFacingFacesOfAClosedSixFaceBox()
    {
        // The six views are the six faces, so all six are built; ordinary
        // culling of a closed box leaves the three facing the camera, which the
        // shader reproduces by discarding faces whose normal points away.
        Assert.Contains("for (int face = 0; face < 6; face++)", StaticWorldSource);
        Assert.Contains("new Vector3[24]", StaticWorldSource);
        Assert.Contains("new int[36]", StaticWorldSource);
        Assert.Contains("face_alignment <= 0.0", StaticWorldSource);
    }

    [Fact]
    public void ImposterSamplingAndColourTransformAreUnchanged()
    {
        // Settled by measurement: the atlas alpha histogram is binary
        // {0: 147423, 255: 114721}, the atlas carries zero mipmaps, its
        // transparent texels are mid-grey (122, 122, 123) so linear filtering
        // would drag every edge grey, and the doubled opaque mean
        // (26.1, 24.5, 31.5) * 2 matches measured retail foliage
        // (50.8, 59.7, 65.4). This test exists to stop a well-meaning "fix".
        Assert.Contains("filter_nearest", StaticWorldSource);
        Assert.DoesNotContain("atlas : filter_linear", StaticWorldSource);
        Assert.Contains("texel.rgb * 2.0", StaticWorldSource);
        Assert.Contains("texel.a < (8.0 / 255.0)", StaticWorldSource);
    }

    /// <summary>
    /// The authored statics are not pines and are seated by a different
    /// released owner. `CThing__Init` @ 0x004F34A0 stores the authored position
    /// at `this+0x1c..+0x28` (Z-down at `+0x24`) and then clamps it twice:
    /// `MOV ECX,0x006FADC8` / `CALL 0x0047EB80` (0x004F34F6, 0x004F34FB) samples
    /// the height field, `FCOM [ESI+0x24]` (0x004F3500) compares, and
    /// `FSTP [ESP+0x14]` (0x004F3529) writes the sampled height into the copy
    /// handed to `CALL [EAX+0x50]`; then `FLD [0x006FBDFC]` (0x004F3549),
    /// `FCOM [ESI+0x24]` and `FSTP [ESI+0x24]` (0x004F3559) do the same with the
    /// water level. Both stores are bare: the mesh bounding box is never read,
    /// so the pivot — not the lowest vertex — lands on the support. These tests
    /// pin that, because a `-min(vertexZ)` lift had been added on top of it and
    /// floated the docks by the exact length of their own pilings.
    /// </summary>
    [Fact]
    public void NoMeshDerivedTermIsAddedToTheReleasedStaticClamp()
    {
        string placement = Between(
            StaticWorldSource,
            "foreach (WorldObject worldObject in manifest.Objects",
            "int pineCount = AddPines(");

        Assert.Contains("relativeHeight,\n                    -relativeZ),", placement.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("verticalClearance", placement);
        Assert.DoesNotContain("BaseClearance", placement);
        // The per-definition suppression only existed to cancel that lift for
        // one object; with the lift gone the general rule covers all 33.
        Assert.DoesNotContain("SatTurretDefinition", placement);
    }

    /// <summary>
    /// The docks are the object that proves the authored Z is an absolute
    /// elevation in the same space as the water plane, not an offset from the
    /// ground: BSWD ordinal 24 is authored at Z-down -8.870770454406738 and
    /// `CHFD + 0x1034` holds -8.84000015258789, so the pivot is authored
    /// 0.0308 above the water surface, while the terrain under it is the flat
    /// sea-floor plateau 10 units below. The authored term therefore wins both
    /// clamps and the pivot must land at the water line.
    /// </summary>
    [Fact]
    public void TheDocksPivotIsTheAuthoredWaterLineAndTheOldLiftWasItsOwnPilings()
    {
        JsonElement manifest = LoadManifest();
        JsonElement docks = manifest
            .GetProperty("objects")
            .EnumerateArray()
            .Single(item => item.GetProperty("ordinal").GetInt32() == 24);
        Assert.Equal("Forseti Docks", docks.GetProperty("definition").GetString());

        double authoredZ = docks.GetProperty("retailPosition")[2].GetDouble();
        double waterZ = Level100Terrain.Instance.WaterLevel;
        Assert.Equal(-8.870770454406738, authoredZ, 9);
        Assert.Equal(0.030770301818847656, waterZ - authoredZ, 9);

        (double authored, double ground, double water) = Supports(docks);
        Assert.Equal(authored, Math.Max(authored, Math.Max(ground, water)), 6);
        // The sea floor here is the flat plateau: raw 1267 * the HFLD scale.
        Assert.Equal(-1.1600439436733723 - 10.0, ground, 6);
        Assert.Equal(0.030770301818847656, authored - water, 9);

        // The lift that used to be added is exactly the piling span this mesh
        // carries below its own origin, which is why the deck hung 3.2 m up and
        // the columns ended above the water.
        double[] vertexZ = MeshVertexZ("fb-docks.obj");
        Assert.Equal(2809, vertexZ.Length);
        Assert.Equal(173, vertexZ.Count(value => value < -0.001));
        Assert.Equal(
            manifest.GetProperty("meshes").GetProperty("FB_Docks")
                .GetProperty("baseClearance").GetDouble(),
            -vertexZ.Min(),
            9);
        Assert.Equal(3.231837272644043, -vertexZ.Min(), 9);
    }

    /// <summary>
    /// The four turrets are the contrast case: they stand on land, so the
    /// ground clamp wins and the pivot must equal the sampled height exactly.
    /// Each turret mesh carries a base collar below its own origin — 56 to 69
    /// vertices reaching 0.222 to 0.228 down — that retail buries. Lifting by
    /// that span is what raised them out of the ground.
    /// </summary>
    [Theory]
    [InlineData(3, "SAT Turret", "ft_sam", "ft-sam.obj", 56, 0.22822660952806473)]
    [InlineData(10, "Blaster Turret", "ft_blaster", "ft-blaster.obj", 69, 0.22263068734901026)]
    [InlineData(11, "Blaster Turret", "ft_blaster", "ft-blaster.obj", 69, 0.22263068734901026)]
    [InlineData(12, "Pulse Turret", "ft_pulse", "ft-pulse.obj", 64, 0.22567694188910536)]
    public void EveryTurretSeatsItsPivotOnTheSampledGroundAndBuriesItsCollar(
        int ordinal,
        string definition,
        string meshKey,
        string meshFile,
        int collarVertexCount,
        double collarDepth)
    {
        JsonElement manifest = LoadManifest();
        JsonElement turret = manifest
            .GetProperty("objects")
            .EnumerateArray()
            .Single(item => item.GetProperty("ordinal").GetInt32() == ordinal);
        Assert.Equal(definition, turret.GetProperty("definition").GetString());
        Assert.Equal(meshKey, turret.GetProperty("mesh").GetString());

        (double authored, double ground, double water) = Supports(turret);
        Assert.Equal(ground, Math.Max(authored, Math.Max(ground, water)), 6);

        double[] vertexZ = MeshVertexZ(meshFile);
        Assert.Equal(collarVertexCount, vertexZ.Count(value => value < -0.001));
        Assert.Equal(collarDepth, -vertexZ.Min(), 9);
        Assert.Equal(
            collarDepth,
            manifest.GetProperty("meshes").GetProperty(meshKey)
                .GetProperty("baseClearance").GetDouble(),
            9);
    }

    /// <summary>
    /// Returns the three clamp supports for one authored object in up-positive
    /// units relative to the player-start reference elevation, exactly as
    /// <c>Level100StaticWorldAsset.Load</c> forms them.
    /// </summary>
    private static (double Authored, double Ground, double Water) Supports(
        JsonElement worldObject)
    {
        const double referenceElevation =
            Level100Terrain.PlayerStartReferenceElevationMillimeters / 1_000.0;
        JsonElement position = worldObject.GetProperty("retailPosition");
        double retailX = position[0].GetDouble();
        double retailY = position[1].GetDouble();
        Level100Terrain terrain = Level100Terrain.Instance;
        int groundUnits = terrain.SampleHeightUnitsAtFixed(
            (int)Math.Floor(retailX * Level100Terrain.FixedPointUnitsPerRetailUnit),
            (int)Math.Floor(retailY * Level100Terrain.FixedPointUnitsPerRetailUnit));
        return (
            referenceElevation - position[2].GetDouble(),
            referenceElevation - (groundUnits * (double)terrain.HeightScale),
            referenceElevation - terrain.WaterLevel);
    }

    private static double[] MeshVertexZ(string meshFile)
    {
        string path = Path.Combine(
            LocateGodotDirectory(),
            "Assets",
            "Level100",
            "StaticWorld",
            "Meshes",
            meshFile);
        Assert.True(File.Exists(path), $"The static-world mesh is missing: {path}");
        return File.ReadLines(path)
            .Where(line => line.StartsWith("v ", StringComparison.Ordinal))
            .Select(line => double.Parse(
                line.Split(' ')[3],
                CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static JsonElement LoadManifest()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Level100",
            "StaticWorld",
            "level100-static-world.json");
        Assert.True(File.Exists(path), $"The static-world manifest is missing: {path}");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.Clone();
    }

    private static string ReadGodotSource(string fileName) =>
        File.ReadAllText(Path.Combine(LocateGodotDirectory(), fileName));

    private static string LocateGodotDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "OnslaughtRebuild.Godot");
            if (File.Exists(Path.Combine(candidate, "Level100StaticWorldAsset.cs")))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate OnslaughtRebuild.Godot above {AppContext.BaseDirectory}.");
    }

    private static string Between(string source, string start, string end)
    {
        int from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Source no longer contains '{start}'.");
        int to = source.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"Source no longer contains '{end}' after '{start}'.");
        return source[from..to];
    }
}
