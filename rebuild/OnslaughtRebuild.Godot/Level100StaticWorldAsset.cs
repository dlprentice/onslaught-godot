// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

internal sealed partial class Level100StaticWorldAsset
{
    private const string ManifestPath =
        "res://Assets/Level100/StaticWorld/level100-static-world.json";
    private const string AnimationManifestPath =
        "res://Assets/Level100/StaticWorld/level100-static-world-animation.json";
    private const string ManifestSha256 =
        Level100ActorDefinitionManifest.ExpectedManifestSha256;
    private const string SourceArchiveSha256 =
        "ED6350C0E214D00AB1BF6A7BD137FBA3E77D0AFE19A6DC4C0607F56AC037496A";
    private const string SatTurretDefinition = "SAT Turret";
    private const string SatTurretMesh = "ft_sam";
    private const string StaticResourcePrefix = "res://Assets/Level100/StaticWorld/";
    private const string PineFarImposterShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled;

        uniform sampler2D atlas : filter_nearest_mipmap, repeat_enable;
        uniform vec3 fog_color;
        uniform float fog_density;
        uniform float mesh_distance_squared;
        varying float face_alignment;
        varying float horizontal_distance_squared;
        varying float view_depth;

        vec3 retail_output(vec3 color) {
            if (OUTPUT_IS_SRGB) {
                return color;
            }
            vec3 low = color / 12.92;
            vec3 high = pow((color + vec3(0.055)) / 1.055, vec3(2.4));
            return mix(low, high, step(vec3(0.04045), color));
        }

        void vertex() {
            vec3 world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            vec3 world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
            face_alignment = dot(
                world_normal,
                CAMERA_POSITION_WORLD - world_position);
            vec2 horizontal_offset =
                MODEL_MATRIX[3].xz - CAMERA_POSITION_WORLD.xz;
            horizontal_distance_squared = dot(
                horizontal_offset,
                horizontal_offset);
            vec3 camera_position = (VIEW_MATRIX * vec4(world_position, 1.0)).xyz;
            view_depth = max(-camera_position.z, 0.0);
        }

        void fragment() {
            if (horizontal_distance_squared <= mesh_distance_squared ||
                face_alignment <= 0.0) {
                discard;
            }
            vec4 texel = texture(atlas, UV);
            if (texel.a < (8.0 / 255.0)) {
                discard;
            }
            float visibility = clamp(
                exp(-fog_density * view_depth),
                0.0,
                1.0);
            vec3 tree_color = min(texel.rgb * 2.0, vec3(1.0));
            ALBEDO = retail_output(mix(fog_color, tree_color, visibility));
        }
        """;

    private static Shader? _pineFarImposterShader;

    private Level100StaticWorldAsset(
        Node3D root,
        IReadOnlyList<MeshInstance3D> objects,
        int surfaceCount,
        int pineInstanceCount,
        Level100WaterAsset water,
        Level100StaticWorldAnimationDriver animation)
    {
        Root = root;
        Objects = objects;
        SurfaceCount = surfaceCount;
        PineInstanceCount = pineInstanceCount;
        Water = water;
        Animation = animation;
    }

    public Node3D Root { get; }

    public IReadOnlyList<MeshInstance3D> Objects { get; }

    public int SurfaceCount { get; }

    public int PineInstanceCount { get; }

    public Level100WaterAsset Water { get; }

    /// <summary>
    /// The released base-world hierarchy idle loops. Presentation only: it
    /// advances from frame delta, mutates nothing but node transforms, and is
    /// never observed by Core or the mission.
    /// </summary>
    public Level100StaticWorldAnimationDriver Animation { get; }

    public static Level100ActorDefinitionSet LoadActorDefinitions() =>
        Level100ActorDefinitionManifest.Decode(LoadManifestBytes());

    /// <summary>
    /// The authored WRES allegiance of each base-world object, for the released
    /// scanner's colour partition. Presentation input; it never reaches Core.
    /// </summary>
    public static IReadOnlyDictionary<string, int> LoadAuthoredAllegiance() =>
        Level100ActorDefinitionManifest.DecodeAuthoredAllegiance(LoadManifestBytes());

    public static Level100StaticWorldAsset Load(Level100HeightFieldAsset terrain)
    {
        Manifest manifest = LoadManifest();
        ValidateManifest(manifest, terrain);
        Level100StaticWorldAnimationSet animation =
            Level100StaticWorldAnimationManifest.Decode(LoadAnimationManifestBytes());
        float pineMeshDistance = checked((float)manifest.PineBillboards.MeshQualityDistance);

        var root = new Node3D { Name = "RetailLevel100StaticWorld" };
        var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        foreach ((string key, TextureDefinition definition) in manifest.Textures)
        {
            textures.Add(key, LoadTexture(definition));
        }

        var meshes = new Dictionary<string, ArrayMesh>(StringComparer.Ordinal);
        var partitioned = new Dictionary<string, PartitionedObjMesh>(StringComparer.Ordinal);
        foreach ((string key, MeshDefinition definition) in manifest.Meshes)
        {
            // The four `pinesnow` meshes are the only meshes in the manifest no
            // authored world object references (the 33 objects use 24 of the 28
            // mesh keys), and `AddClosePineMeshes` is their only consumer. So
            // this key test selects exactly retail's `CRTTree` close-pine draws
            // and nothing else, which is what the measured rig split needs.
            bool isClosePine = key.StartsWith("pinesnow", StringComparison.Ordinal);
            var surfaceMaterials = definition.Materials.ToDictionary(
                pair => pair.Key,
                pair => (Material)CreateMaterial(
                    pair.Value,
                    textures,
                    manifest.Textures,
                    terrain,
                    isClosePine ? pineMeshDistance : 0f,
                    isClosePine
                        ? RetailMeshLightRig.ClosePine(terrain)
                        : RetailMeshLightRig.StaticWorld(terrain)),
                StringComparer.Ordinal);
            // Only the meshes the released data says actually loop are split by
            // hierarchy part. The three one-shot turret hierarchies hold their
            // authored rest pose, which is virtual frame 0 and therefore exactly
            // the merged mesh already emitted, so splitting them would fragment
            // their material surfaces for no visual difference.
            if (animation.Meshes.TryGetValue(key, out Level100StaticWorldMeshAnimation? meshAnimation) &&
                meshAnimation.Playback == Level100StaticWorldPlayback.CyclicLoop)
            {
                partitioned.Add(key, CuratedObjMeshLoader.LoadPartitioned(
                    definition.ResourcePath,
                    surfaceMaterials,
                    [.. meshAnimation.Parts.Select(part => new ObjPartRange(
                        part.Part,
                        part.ObjVertexStart,
                        part.ObjVertexCount))]));
                continue;
            }

            ArrayMesh mesh = CuratedObjMeshLoader.Load(definition.ResourcePath, surfaceMaterials);
            meshes.Add(key, mesh);
        }

        var objects = new List<MeshInstance3D>(manifest.Objects.Length);
        var animatedParts = new List<Level100StaticWorldAnimationBinding>();
        foreach (WorldObject worldObject in manifest.Objects.OrderBy(item => item.Ordinal))
        {
            float retailX = checked((float)worldObject.RetailPosition[0]);
            float retailY = checked((float)worldObject.RetailPosition[1]);
            float retailZ = checked((float)worldObject.RetailPosition[2]);
            float relativeX = retailX - Level100HeightFieldAsset.PlayerStartX;
            float relativeZ = retailY - Level100HeightFieldAsset.PlayerStartZ;
            // `CThing__Init` @ 0x004F34A0 is the whole vertical placement the
            // released game applies to an authored static. It copies the
            // authored position into `this+0x1c..+0x28` (Z-down at `+0x24`),
            // then runs two clamps and nothing else:
            //
            //   [retail instruction listing removed]
            //   [retail instruction listing removed]
            //   [retail instruction listing removed]
            //   [retail instruction listing removed]
            //   [retail instruction listing removed]
            //   [retail instruction listing removed]
            //   [retail instruction listing removed]
            //   [retail instruction listing removed]
            //
            // Both `FSTP`s write the support height straight into the pivot.
            // Z is down-positive, so the pair is `min(authored, support)` there
            // and the `Math.Max` below in up-positive units. No mesh extent is
            // read anywhere on that path: retail seats the *pivot*, never the
            // bounding box. A `-min(vertexZ)` clearance term therefore has no
            // retail counterpart and is not applied. It measured +3.2318 on
            // `FB_Docks` — exactly that mesh's own 173-vertex piling span below
            // its origin, which is why the docks hung in the air with their
            // legs ending above the water — and +0.2226/+0.2257/+0.2282 on the
            // three turret meshes, each exactly the buried base collar those
            // meshes carry below z = 0.
            float relativeHeight = Math.Max(
                Level100HeightFieldAsset.PlayerStartElevation - retailZ,
                Math.Max(
                    terrain.SampleRelativeHeight(relativeX, relativeZ),
                    terrain.WaterRelativeHeight));

            var objectRoot = new Node3D
            {
                Name = $"RetailWorldObject{worldObject.Ordinal:D2}",
                Position = new Vector3(
                    relativeX,
                    relativeHeight,
                    -relativeZ),
                Rotation = new Vector3(0f, checked((float)worldObject.Yaw), 0f),
            };
            bool isPartitioned = partitioned.TryGetValue(
                worldObject.Mesh,
                out PartitionedObjMesh? split);
            var geometry = new MeshInstance3D
            {
                Name = $"{worldObject.Name}Geometry",
                Mesh = isPartitioned ? split!.Remainder : meshes[worldObject.Mesh],
                RotationDegrees = new Vector3(-90f, 0f, 0f),
            };

            if (isPartitioned)
            {
                // `geometry`'s -90 degree X rotation is the only thing between
                // the OBJ vertices and this object's space, so `geometry`'s own
                // local frame IS the OBJ space the released deltas are expressed
                // in. Its children therefore take those deltas verbatim, with no
                // further conversion.
                Level100StaticWorldMeshAnimation meshAnimation = animation.Meshes[worldObject.Mesh];
                foreach (Level100StaticWorldAnimatedPart part in meshAnimation.Parts)
                {
                    var partNode = new MeshInstance3D
                    {
                        Name = $"Part{part.Part:D2}-{SanitizeNodeName(part.Name)}",
                        Mesh = split!.Parts[part.Part],
                        Transform = Level100StaticWorldAnimationDriver.ToObjSpaceTransform(
                            part.Frames[0]),
                    };
                    geometry.AddChild(partNode);
                    animatedParts.Add(new Level100StaticWorldAnimationBinding(
                        meshAnimation,
                        part,
                        partNode));
                }
            }

            objectRoot.AddChild(geometry);
            root.AddChild(objectRoot);
            objects.Add(geometry);
        }

        // Every part the manifest says moves on a looping mesh must have reached
        // a node. Without this, a mesh key drifting out of the placed set would
        // silently go back to being frozen scenery - which is exactly the defect
        // this whole path exists to fix.
        int expectedBindings = manifest.Objects
            .Where(item => partitioned.ContainsKey(item.Mesh))
            .Sum(item => animation.Meshes[item.Mesh].Parts.Count);
        if (animatedParts.Count != expectedBindings || animatedParts.Count == 0)
        {
            throw new InvalidDataException(
                "The Level 100 static-world animated parts were not all bound at load.");
        }

        int pineCount = AddPines(root, manifest, terrain, meshes, textures);
        Level100WaterAsset water = AddWater(root, manifest, terrain, textures);
        int surfaceCount =
            objects.Sum(item => item.Mesh?.GetSurfaceCount() ?? 0) +
            animatedParts.Sum(item => item.Node.Mesh?.GetSurfaceCount() ?? 0);
        return new Level100StaticWorldAsset(
            root,
            objects,
            surfaceCount,
            pineCount,
            water,
            new Level100StaticWorldAnimationDriver(animation.FramesPerSecond, animatedParts));
    }

    private static string SanitizeNodeName(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            buffer[index] = char.IsAsciiLetterOrDigit(character) ? character : '_';
        }

        return buffer.IsEmpty ? "Part" : new string(buffer);
    }

    private static int AddPines(
        Node3D root,
        Manifest manifest,
        Level100HeightFieldAsset terrain,
        IReadOnlyDictionary<string, ArrayMesh> meshes,
        IReadOnlyDictionary<string, Texture2D> textures)
    {
        // A retail Level 1.00 pine has exactly two representations: the
        // pinesnow mesh at or inside the authored mesh-quality distance, and
        // the six-face box imposter outside it. That distance is retail's
        // authored default 30.0, read from the pristine specimen
        // (local-lab/safe-copy-bea-pristine/BEA.exe.original.backup, sha256
        // 74154bfa...): the "Geometry detail:" setter at 0x004DD6B0 has three
        // arms writing 10.0 / 30.0 / 70.0 to 0x006321A0, and the image's own
        // static initialisers — 0x006321A0 = 30.0, bias 0x00631E88 = 1.0, scale
        // 0x00630E0C = 1.0 — are uniquely the middle arm. It is NOT read from
        // `defaultoptions.bea`, whose 0x26CA is one machine's "High" setting;
        // see GOAL.md's defaults rule and materialize_retail_assets.py's
        // PINE_MESH_QUALITY_DISTANCE for the full byte evidence.
        //
        // The archive's IMPS/IMPT/VIEW chunk carries exactly six views per
        // variant whose (width, height) pairs are that variant's own mesh
        // bounding-box half-extents inflated by 1.05 on all three axes. The two
        // shader gates below are complementary about the same single value, so
        // every pine is covered at every distance and neither pass is ungated.
        PinePlacement[] placements = BuildPinePlacements(manifest.Pines, terrain);
        AddClosePineMeshes(root, meshes, placements);

        Texture2D atlas = textures[manifest.PineBillboards.Texture];
        AddFarPineImposters(root, manifest, terrain, atlas, placements);
        return placements.Length;
    }

    /// <summary>
    /// Seats a pine's PIVOT on the sampled support and adds nothing else.
    ///
    /// <para>Read from the pristine specimen
    /// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
    /// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
    /// — never the installed <c>BEA.exe</c>, which is deliberately patched.
    /// The authored pine record is (X, Y, variant) with no Z at all, so the
    /// entire vertical datum is whatever the released code computes.</para>
    ///
    /// <para><c>CTree::Init</c> at <c>0x004F6080</c> (its single stack argument
    /// is the <c>CTreeInitThing*</c>, loaded at <c>0x004F61B9</c>
    /// <c>MOV EBX,[ESP+0x58C]</c>; the body ends <c>RET 4</c> at
    /// <c>0x004F63AE</c>) computes the pine's Z in five instructions and stores
    /// it back into the init record before seating:
    /// <c>0x004F61C4 LEA EDX,[EBX+4]</c> (&amp;mPos) /
    /// <c>0x004F61C7 MOV ECX,0x006FADC8</c> (the MAP singleton) /
    /// <c>0x004F61DD CALL 0x0047EB80</c> (the height query — the same receiver
    /// and callee <c>CThing::Init</c> uses at <c>0x004F34F6</c>/<c>0x004F34FB</c>)
    /// / <c>0x004F61F4 FSTP DWORD PTR [EBX+0xC]</c>. <c>[EBX+0xC]</c> is
    /// <c>mPos.Z</c>: <c>CThing::Init</c> copies <c>[EBX+4]</c>..<c>[EBX+0x10]</c>
    /// into <c>this+0x1c</c>..<c>this+0x28</c> with Z at <c>+0x24</c>. There is
    /// <b>no FADD, FSUB or FMUL between the CALL and the FSTP</b>, and no mesh
    /// extent is read anywhere in the body — the sampled height becomes the
    /// pivot verbatim. <c>0x004F6363 CALL 0x004F34A0</c> then runs
    /// <c>CThing::Init</c>, whose terrain clamp is inert (Z already equals the
    /// sample) and whose water clamp is the <c>Math.Max</c> below.</para>
    ///
    /// <para>Both draw paths inherit that pivot unchanged, and the mesh path
    /// cannot perturb it even in principle: the matrix a tree hands the
    /// renderer carries <b>no translation at all</b>.
    /// <c>CRTTree::VFuncSlot02_BuildRenderOutputs</c> (<c>0x004DD960</c>) takes
    /// the rotation from <c>CTree::GetMatrix</c> (<c>0x004F6560</c>, secondary
    /// vtable <c>0x005DD960</c> slot <c>+0x04</c>), whose tail
    /// <c>SHL ESI,4 / ADD ESI,0x008406B8 / REP MOVSD</c> at
    /// <c>0x004F65F6</c>..<c>0x004F65FF</c> copies twelve dwords straight out of
    /// the 32-entry Z-rotation table <c>CTree::Init</c> builds at
    /// <c>0x008406B8</c>, and takes the position separately from
    /// <c>CTree::GetPos</c> (<c>0x004040A0</c>, secondary vtable slot
    /// <c>+0x00</c>), which is four verbatim dword copies out of
    /// <c>this+0x1c</c>. <c>CDXEngine::RenderImposterBillboardSet</c>
    /// (<c>0x00543300</c>) then forms <c>thingPos + M*centerOffset</c> —
    /// <c>FADD</c> at <c>0x005434AB</c>, <c>0x005434C8</c> and
    /// <c>0x005434D8</c> against the vector returned by
    /// <c>CALL 0x00401EC0</c> — where <c>centerOffset</c> is
    /// <c>mesh+0x150</c>, the bounding box CENTRE (already carried in the
    /// imposter geometry below), not its minimum.</para>
    ///
    /// <para><c>CRTTree::Init</c> (<c>0x004DD7B0</c>) writes nothing to the
    /// tree's position. It does cache two mesh scalars —
    /// <c>FLD [EAX+0x164] / FSTP [ESI+0x24]</c> at <c>0x004DD80A</c> and
    /// <c>FLD [EAX+0x168] / FSTP [ESI+4]</c> at <c>0x004DD813</c> — but neither
    /// <c>CRTTree+0x24</c> nor <c>CRTTree+4</c> is read by either submit path,
    /// and <c>mesh+0x164</c> is a scalar radius, not a Z extent.</para>
    ///
    /// <para>The decisive negative: retail HAS a per-class vertical
    /// ground-clearance hook, at vtable slot <c>+0xC0</c>, used by the
    /// ground-follow routine at <c>0x004017C0</c>. <c>CTree</c> does not
    /// override it — both <c>CTree</c> (<c>0x005DD9D8+0xC0</c>) and
    /// <c>CThing</c> (<c>0x005DF5C8+0xC0</c>) hold <c>0x004BFC60</c>, which is
    /// <c>FLD DWORD PTR [0x005D856C] / RET</c> and <c>[0x005D856C]</c> is
    /// <c>0.0f</c>. The engine has exactly the mechanism a base-clearance term
    /// would need, trees return zero from it, and <c>CThing::Init</c> does not
    /// call it at all.</para>
    ///
    /// <para>So a <c>-min(vertexZ)</c> "base clearance" lift, which used to be
    /// added to both passes here, has no retail counterpart on any of those
    /// paths. It is gone; a pine's lowest vertex is buried by exactly its own
    /// mesh minimum, the same way <c>CThing::Init</c> buries the 56-69 vertex
    /// collars the three turret meshes carry 0.222-0.228 below their own
    /// origins.</para>
    /// </summary>
    private static PinePlacement[] BuildPinePlacements(
        IReadOnlyList<double[]> pines,
        Level100HeightFieldAsset terrain)
    {
        var placements = new PinePlacement[pines.Count];
        for (int ordinal = 0; ordinal < pines.Count; ordinal++)
        {
            double[] pine = pines[ordinal];
            float retailX = checked((float)pine[0]);
            float retailY = checked((float)pine[1]);
            float relativeX = retailX - Level100HeightFieldAsset.PlayerStartX;
            float relativeZ = retailY - Level100HeightFieldAsset.PlayerStartZ;
            float height = Math.Max(
                terrain.SampleRelativeHeight(relativeX, relativeZ),
                terrain.WaterRelativeHeight);
            placements[ordinal] = new PinePlacement(
                checked((int)pine[2]),
                new Vector3(relativeX, height, -relativeZ));
        }
        return placements;
    }

    private static void AddClosePineMeshes(
        Node3D root,
        IReadOnlyDictionary<string, ArrayMesh> meshes,
        IReadOnlyList<PinePlacement> placements)
    {
        for (int variant = 0; variant < 4; variant++)
        {
            PinePlacement[] instances = placements
                .Where(item => item.Variant == variant)
                .ToArray();
            string meshKey = $"pinesnow{variant}";
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = meshes[meshKey],
                InstanceCount = instances.Length,
            };
            var meshBasis = new Basis(Vector3.Right, -Mathf.Pi / 2f);
            for (int index = 0; index < instances.Length; index++)
            {
                // The pivot, not the lowest vertex. See BuildPinePlacements:
                // CTree::Init @0x004F6080 stores the height query's result
                // straight into mPos.Z (FSTP [EBX+0xC] @0x004F61F4) with no
                // mesh extent read anywhere on the path.
                multiMesh.SetInstanceTransform(
                    index,
                    new Transform3D(meshBasis, instances[index].GroundOrigin));
            }
            root.AddChild(new MultiMeshInstance3D
            {
                Name = $"RetailPineSnow{variant}CloseMeshInstances",
                Multimesh = multiMesh,
            });
        }
    }

    private static void AddFarPineImposters(
        Node3D root,
        Manifest manifest,
        Level100HeightFieldAsset terrain,
        Texture2D atlas,
        IReadOnlyList<PinePlacement> placements)
    {
        ShaderMaterial material = CreatePineImposterMaterial(
            atlas,
            terrain,
            meshQualityDistance: manifest.PineBillboards.MeshQualityDistance);
        for (int variant = 0; variant < 4; variant++)
        {
            PinePlacement[] instances = placements
                .Where(item => item.Variant == variant)
                .ToArray();
            ArrayMesh mesh = CreateFarPineImposterMesh(
                manifest.PineBillboards.Variants[variant],
                material);
            // The stored VIEW half-extents are this variant's own mesh
            // bounding box scaled by 1.05, and `centerOffset` is that mesh
            // bounding box centre, already applied inside the geometry below.
            // The box therefore only describes the tree when it sits on the
            // SAME transform the mesh does, so this must stay identical to
            // AddClosePineMeshes or the tree steps vertically at the swap.
            // Retail forms it the same way and for the same reason:
            // CDXEngine::RenderImposterBillboardSet @0x00543300 adds the
            // rotated centre to the owner's position (FADD @0x005434AB /
            // 0x005434C8 / 0x005434D8) and adds nothing else.
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = instances.Length,
            };
            for (int index = 0; index < instances.Length; index++)
            {
                multiMesh.SetInstanceTransform(
                    index,
                    new Transform3D(Basis.Identity, instances[index].GroundOrigin));
            }
            root.AddChild(new MultiMeshInstance3D
            {
                Name = $"RetailPineSnow{variant}FarSixFaceInstances",
                Multimesh = multiMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }

    private static ArrayMesh CreateFarPineImposterMesh(
        PineBillboardVariant definition,
        Material material)
    {
        Vector3[] rawRights =
        [
            Vector3.Right,
            new Vector3(0f, -1f, 0f),
            Vector3.Left,
            new Vector3(0f, 1f, 0f),
            Vector3.Right,
            Vector3.Right,
        ];
        Vector3[] rawUps =
        [
            Vector3.Back,
            Vector3.Back,
            Vector3.Back,
            Vector3.Back,
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, 1f, 0f),
        ];
        Vector3 center = ToGodotVector(definition.CenterOffset);
        var vertices = new Vector3[24];
        var normals = new Vector3[24];
        var textureCoordinates = new Vector2[24];
        var indices = new int[36];
        for (int face = 0; face < 6; face++)
        {
            double[] view = definition.Views[face];
            Vector3 right = ToGodotVector(rawRights[face]) *
                checked((float)(view[4] * 0.99));
            Vector3 up = ToGodotVector(rawUps[face]) *
                checked((float)(view[5] * 0.99));
            Vector3 normal = ToGodotVector(rawUps[face].Cross(rawRights[face])).Normalized();
            AddQuad(
                vertices,
                normals,
                textureCoordinates,
                indices,
                face,
                center,
                right,
                up,
                normal,
                view);
        }
        return CreateArrayMesh(vertices, normals, textureCoordinates, indices, material);
    }

    private static void AddQuad(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] textureCoordinates,
        int[] indices,
        int quadIndex,
        Vector3 center,
        Vector3 right,
        Vector3 up,
        Vector3 normal,
        double[] view)
    {
        int vertex = quadIndex * 4;
        vertices[vertex] = center - right - up;
        vertices[vertex + 1] = center + right - up;
        vertices[vertex + 2] = center + right + up;
        vertices[vertex + 3] = center - right + up;
        for (int index = 0; index < 4; index++)
        {
            normals[vertex + index] = normal;
        }
        float u0 = checked((float)view[0]);
        float u1 = checked((float)view[1]);
        float v0 = checked((float)view[2]);
        float v1 = checked((float)view[3]);
        textureCoordinates[vertex] = new Vector2(u0, v0);
        textureCoordinates[vertex + 1] = new Vector2(u1, v0);
        textureCoordinates[vertex + 2] = new Vector2(u1, v1);
        textureCoordinates[vertex + 3] = new Vector2(u0, v1);
        int triangle = quadIndex * 6;
        indices[triangle] = vertex;
        indices[triangle + 1] = vertex + 1;
        indices[triangle + 2] = vertex + 2;
        indices[triangle + 3] = vertex + 2;
        indices[triangle + 4] = vertex + 3;
        indices[triangle + 5] = vertex;
    }

    private static ArrayMesh CreateArrayMesh(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] textureCoordinates,
        int[] indices,
        Material material)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Godot.Mesh.ArrayType.Normal] = normals;
        arrays[(int)Godot.Mesh.ArrayType.TexUV] = textureCoordinates;
        arrays[(int)Godot.Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private static ShaderMaterial CreatePineImposterMaterial(
        Texture2D atlas,
        Level100HeightFieldAsset terrain,
        double meshQualityDistance)
    {
        // Every imposter material is distance-gated. There is no ungated
        // overload: the box may only appear where the mesh has been discarded.
        float distance = checked((float)meshQualityDistance);
        if (!float.IsFinite(distance) || distance <= 0f)
        {
            throw new InvalidDataException(
                "Level 100 pine imposters require a positive mesh-quality distance.");
        }
        var material = new ShaderMaterial
        {
            Shader = _pineFarImposterShader ??= new Shader { Code = PineFarImposterShaderCode },
            RenderPriority = 0,
        };
        material.SetShaderParameter("atlas", atlas);
        material.SetShaderParameter("fog_color", new Vector3(
            terrain.FogColor.R,
            terrain.FogColor.G,
            terrain.FogColor.B));
        material.SetShaderParameter("fog_density", terrain.FogDensity);
        material.SetShaderParameter("mesh_distance_squared", distance * distance);
        return material;
    }

    private static Vector3 ToGodotVector(double[] beaVector) => new(
        checked((float)beaVector[0]),
        -checked((float)beaVector[2]),
        -checked((float)beaVector[1]));

    private static Vector3 ToGodotVector(Vector3 beaVector) => new(
        beaVector.X,
        -beaVector.Z,
        -beaVector.Y);

    private static Level100WaterAsset AddWater(
        Node3D root,
        Manifest manifest,
        Level100HeightFieldAsset terrain,
        IReadOnlyDictionary<string, Texture2D> textures)
    {
        Level100WaterAsset water = Level100WaterAsset.Create(
            terrain,
            textures[manifest.Water.ReflectionTexture],
            textures[manifest.Water.CausticTexture],
            textures[manifest.Water.WavesTexture],
            textures[manifest.Water.SunBlobTexture],
            textures[manifest.Water.SunReflectionTexture],
            manifest.Water.SurfaceResourcePath,
            manifest.Water.SurfaceSha256);
        root.AddChild(water.Root);
        return water;
    }

    private static Texture2D LoadTexture(TextureDefinition definition)
    {
        if (!Enum.TryParse(
                definition.Compression,
                ignoreCase: false,
                out CuratedAyaTextureLoader.Compression compression))
        {
            throw new InvalidDataException(
                $"Static-world texture has unsupported compression '{definition.Compression}'.");
        }
        return CuratedAyaTextureLoader.Load(
            definition.ResourcePath,
            definition.Width,
            definition.Height,
            compression);
    }

    private static ShaderMaterial CreateMaterial(
        MaterialDefinition definition,
        IReadOnlyDictionary<string, Texture2D> textures,
        IReadOnlyDictionary<string, TextureDefinition> textureDefinitions,
        Level100HeightFieldAsset terrain,
        float maximumHorizontalDistance,
        RetailMeshLightRig lightRig)
    {
        RetailTextureLayer?[] layers = definition.Layers
            .Select(layer => layer is null
                ? null
                : new RetailTextureLayer(
                    textures[layer.Texture],
                    checked((float)layer.Opacity),
                    new Vector2(
                        checked((float)layer.Offset[0]),
                        checked((float)layer.Offset[1])),
                    new Vector2(
                        checked((float)layer.Scale[0]),
                        checked((float)layer.Scale[1])),
                    textureDefinitions[layer.Texture].BlendTextureAlpha))
            .ToArray();
        // MODULATE2X, stated rather than inherited. Stage-zero
        // D3DTSS_COLOROP was read at the entry to every mesh render of one
        // whole Level 100 frame and is 5 = D3DTOP_MODULATE2X on all 134 mode-0
        // static-world draws and all 442 CRTTree mesh draws.
        //
        // OPEN, and deliberately not applied here: 19 further draws inside
        // CRTMesh::BuildRenderOutputs in that same frame run at mode 4 with
        // stage-zero COLOROP 4 = D3DTOP_MODULATE. They are a separate pass
        // issued after the cockpit, from four CRTMesh objects whose count
        // varies frame to frame (two in one observed frame, four in three
        // others), so they are not the 28 authored static-world meshes this
        // manifest builds and nothing here has been mapped to them. Giving any
        // mesh built from this manifest MODULATE would therefore be a guess,
        // not a measurement.
        return RetailFixedFunctionMaterial.Create(
            layers,
            terrain,
            maximumHorizontalDistance,
            maximumHorizontalDistance > 0f ? 8f / 255f : 0.5f,
            RetailStageZeroColorOperation.Modulate2X,
            lightRig);
    }

    private static Manifest LoadManifest()
    {
        byte[] source = LoadManifestBytes();
        return JsonSerializer.Deserialize<Manifest>(source, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("The Level 100 static-world manifest is empty.");
    }

    /// <summary>
    /// The animation manifest is a separate hash-pinned file; its own SHA-256 is
    /// checked inside
    /// <see cref="Level100StaticWorldAnimationManifest.Decode"/>, so this only
    /// bounds the read.
    /// </summary>
    private static byte[] LoadAnimationManifestBytes()
    {
        byte[] source = Godot.FileAccess.GetFileAsBytes(AnimationManifestPath);
        if (source.Length is < 1 or > 512_000)
        {
            throw new InvalidDataException(
                "The locally materialized Level 100 static-world animation manifest is missing.");
        }

        return source;
    }

    private static byte[] LoadManifestBytes()
    {
        byte[] source = Godot.FileAccess.GetFileAsBytes(ManifestPath);
        if (source.Length is < 1 or > 512_000 ||
            !StringComparer.Ordinal.Equals(
                Convert.ToHexString(SHA256.HashData(source)),
                ManifestSha256))
        {
            throw new InvalidDataException(
                "The locally materialized Level 100 static-world manifest is missing or changed.");
        }
        return source;
    }

    private static void ValidateManifest(Manifest manifest, Level100HeightFieldAsset terrain)
    {
        // The schema string is pinned independently of
        // Level100ActorDefinitionManifest, so it moves with every schema step
        // (v15 on 2026-09-26 adds the air units' flight scalars and the
        // CSafeSides, which this loader does not read).
        if (!StringComparer.Ordinal.Equals(manifest.Schema, "onslaught.level100-static-world.v15") ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                manifest.SourceArchiveSha256,
                SourceArchiveSha256) ||
            manifest.UnitRecordCount != 35 ||
            manifest.VisibleObjectCount != 33 ||
            manifest.SuppressedFernCount != 753 ||
            manifest.PineInstanceCount != 1481 ||
            manifest.Objects.Length != 33 ||
            manifest.Pines.Length != 1481 ||
            manifest.Meshes.Count != 28 ||
            manifest.Textures.Count != 34 ||
            Enumerable.Range(0, 4).Any(variant =>
                !manifest.Meshes.ContainsKey($"pinesnow{variant}")) ||
            !IsValidPineBillboards(manifest.PineBillboards, manifest.Textures) ||
            !manifest.Textures.TryGetValue(
                "meshtex-a8-fb-hangermorebits-lit",
                out TextureDefinition? blendTexture) ||
            blendTexture is null ||
            !blendTexture.BlendTextureAlpha ||
            manifest.Textures.Values.Count(texture => texture.BlendTextureAlpha) != 1 ||
            manifest.Water.TextureIndex != terrain.WaterTexture ||
            BitConverter.SingleToInt32Bits(checked((float)manifest.Water.Level)) !=
                BitConverter.SingleToInt32Bits(terrain.WaterLevel) ||
            !manifest.Textures.ContainsKey(manifest.Water.ReflectionTexture) ||
            !manifest.Textures.ContainsKey(manifest.Water.CausticTexture) ||
            !manifest.Textures.ContainsKey(manifest.Water.WavesTexture) ||
            !manifest.Textures.ContainsKey(manifest.Water.SunBlobTexture) ||
            !manifest.Textures.ContainsKey(manifest.Water.SunReflectionTexture) ||
            string.IsNullOrWhiteSpace(manifest.Water.SurfaceSha256))
        {
            throw new InvalidDataException(
                "Level 100 static-world identity, counts, or reconstruction profile changed.");
        }

        int[] variants = new int[4];
        foreach (double[] pine in manifest.Pines)
        {
            if (pine.Length != 3 || !pine.All(double.IsFinite))
            {
                throw new InvalidDataException("Level 100 has an invalid pine instance.");
            }
            int variant = checked((int)pine[2]);
            if (variant is < 0 or > 3 || pine[2] != variant)
            {
                throw new InvalidDataException("Level 100 has an invalid pine variant.");
            }
            variants[variant]++;
        }
        if (!variants.SequenceEqual([383, 355, 318, 425]))
        {
            throw new InvalidDataException("Level 100 pine variant counts do not match retail.");
        }

        if (manifest.Objects.Select(item => item.Ordinal).Distinct().Count() != 33)
        {
            throw new InvalidDataException("Level 100 static-world ordinals are not unique.");
        }
        WorldObject[] satTurrets = manifest.Objects
            .Where(item => StringComparer.Ordinal.Equals(item.Definition, SatTurretDefinition))
            .ToArray();
        if (satTurrets.Length != 1 ||
            !StringComparer.Ordinal.Equals(satTurrets[0].Mesh, SatTurretMesh) ||
            manifest.Objects.Any(item =>
                StringComparer.Ordinal.Equals(item.Mesh, SatTurretMesh) &&
                !StringComparer.Ordinal.Equals(item.Definition, SatTurretDefinition)))
        {
            throw new InvalidDataException("Level 100 SAT Turret identity does not match retail.");
        }
        foreach (WorldObject worldObject in manifest.Objects)
        {
            if (string.IsNullOrWhiteSpace(worldObject.Definition) ||
                worldObject.RetailPosition.Length != 3 ||
                !worldObject.RetailPosition.All(double.IsFinite) ||
                !double.IsFinite(worldObject.Yaw) ||
                !manifest.Meshes.ContainsKey(worldObject.Mesh))
            {
                throw new InvalidDataException("Level 100 has an invalid static-world object.");
            }
        }
        foreach (MeshDefinition mesh in manifest.Meshes.Values)
        {
            RequireOwnedResource(mesh.ResourcePath);
            if (!double.IsFinite(mesh.BaseClearance) || mesh.Materials.Count == 0 ||
                mesh.Materials.Values.Any(material =>
                    !IsValidMaterial(material, manifest.Textures)))
            {
                throw new InvalidDataException("Level 100 has an invalid static-world mesh.");
            }
        }
        foreach (TextureDefinition texture in manifest.Textures.Values)
        {
            RequireOwnedResource(texture.ResourcePath);
            if (texture.Width is < 1 or > 1024 || texture.Height is < 1 or > 1024)
            {
                throw new InvalidDataException("Level 100 has invalid static-world texture dimensions.");
            }
        }
        RequireOwnedResource(manifest.Water.SurfaceResourcePath);
    }

    private static bool IsValidPineBillboards(
        PineBillboardDefinition definition,
        IReadOnlyDictionary<string, TextureDefinition> textures)
    {
        if (!textures.TryGetValue(definition.Texture, out TextureDefinition? texture) ||
            texture is null ||
            texture.Width != 1024 ||
            texture.Height != 256 ||
            !StringComparer.Ordinal.Equals(texture.Compression, "Dxt2") ||
            // Retail's authored Geometry detail default: 0x004DD6B0 arm 1,
            // pinned by the image's own static initialiser at .data 0x006321A0
            // (file 0x231CA0) = 00 00 F0 41. Not this machine's
            // defaultoptions.bea, which holds the "High" arm's 70.0.
            BitConverter.SingleToInt32Bits(checked((float)definition.MeshQualityDistance)) !=
                BitConverter.SingleToInt32Bits(30f) ||
            // Manifest identity only. Retail's fast tree batch has no
            // established enable condition and is not drawn here, so nothing
            // consumes this phase; it stays pinned so a manifest change is
            // still caught.
            definition.FastStandingViewPhase != 0 ||
            definition.Variants.Length != 4)
        {
            return false;
        }

        int[][] expectedCenters =
        [
            [unchecked((int)0xBCCC7F20), 0x39BA4000, unchecked((int)0xBF6303AA)],
            [0x3D8FAD60, unchecked((int)0xBDA96080), unchecked((int)0xBF696408)],
            [0x3C9B2D60, unchecked((int)0xBDF5D470), unchecked((int)0xBF6A0AB4)],
            [0x3D429CA0, 0x3CD68540, unchecked((int)0xBF506532)],
        ];
        for (int variant = 0; variant < definition.Variants.Length; variant++)
        {
            PineBillboardVariant item = definition.Variants[variant];
            if (item.CenterOffset.Length != 3 || item.Views.Length != 6 ||
                !item.CenterOffset.All(double.IsFinite) ||
                item.Views.Any(view =>
                    view.Length != 6 ||
                    !view.All(double.IsFinite) ||
                    view[0] < 0.0 || view[0] >= view[1] || view[1] > 1.0 ||
                    view[2] < 0.0 || view[2] >= view[3] || view[3] > 1.0 ||
                    view[4] <= 0.0 || view[5] <= 0.0))
            {
                return false;
            }
            int[] centerBits = item.CenterOffset
                .Select(value => BitConverter.SingleToInt32Bits(checked((float)value)))
                .ToArray();
            if (!centerBits.SequenceEqual(expectedCenters[variant]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsValidMaterial(
        MaterialDefinition material,
        IReadOnlyDictionary<string, TextureDefinition> textures)
    {
        if (material.Layers.Length != 6 || material.Layers[0] is null)
        {
            return false;
        }
        for (int index = 0; index < material.Layers.Length; index++)
        {
            MaterialLayerDefinition? layer = material.Layers[index];
            if (layer is null)
            {
                continue;
            }
            if (!textures.ContainsKey(layer.Texture) ||
                !double.IsFinite(layer.Opacity) ||
                layer.Opacity is < 0.0 or > 1.0 ||
                layer.Offset.Length != 2 ||
                layer.Scale.Length != 2 ||
                !layer.Offset.All(double.IsFinite) ||
                !layer.Scale.All(double.IsFinite) ||
                layer.Scale.Any(value => value is < 0.0 or > 100.0) ||
                (index == 4 && layer.Scale.Any(value => value <= 0.0)))
            {
                return false;
            }
        }
        return true;
    }

    private static void RequireOwnedResource(string resourcePath)
    {
        if (!resourcePath.StartsWith(StaticResourcePrefix, StringComparison.Ordinal) ||
            resourcePath.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Level 100 static-world resource escaped its local owner.");
        }
    }

    private sealed record Manifest
    {
        public string Schema { get; init; } = string.Empty;
        public string SourceArchiveSha256 { get; init; } = string.Empty;
        public int UnitRecordCount { get; init; }
        public int VisibleObjectCount { get; init; }
        public int SuppressedFernCount { get; init; }
        public int PineInstanceCount { get; init; }
        public PineBillboardDefinition PineBillboards { get; init; } = new();
        public Dictionary<string, MeshDefinition> Meshes { get; init; } = [];
        public Dictionary<string, TextureDefinition> Textures { get; init; } = [];
        public WorldObject[] Objects { get; init; } = [];
        public double[][] Pines { get; init; } = [];
        public WaterDefinition Water { get; init; } = new();
    }

    private sealed record MeshDefinition
    {
        public double BaseClearance { get; init; }
        public Dictionary<string, MaterialDefinition> Materials { get; init; } = [];
        public string ResourcePath { get; init; } = string.Empty;
    }

    private sealed record PineBillboardDefinition
    {
        public int FastStandingViewPhase { get; init; }
        public double MeshQualityDistance { get; init; }
        public string Texture { get; init; } = string.Empty;
        public PineBillboardVariant[] Variants { get; init; } = [];
    }

    private sealed record PineBillboardVariant
    {
        public double[] CenterOffset { get; init; } = [];
        public double[][] Views { get; init; } = [];
    }

    private readonly record struct PinePlacement(
        int Variant,
        Vector3 GroundOrigin);

    private sealed record MaterialDefinition
    {
        public MaterialLayerDefinition?[] Layers { get; init; } = [];
    }

    private sealed record MaterialLayerDefinition
    {
        public double[] Offset { get; init; } = [];
        public double Opacity { get; init; }
        public double[] Scale { get; init; } = [];
        public string Texture { get; init; } = string.Empty;
    }

    private sealed record TextureDefinition
    {
        public bool BlendTextureAlpha { get; init; }
        public string Compression { get; init; } = string.Empty;
        public int Height { get; init; }
        public string ResourcePath { get; init; } = string.Empty;
        public int Width { get; init; }
    }

    private sealed record WorldObject
    {
        public string Definition { get; init; } = string.Empty;
        public string Mesh { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Ordinal { get; init; }
        public double[] RetailPosition { get; init; } = [];
        public double Yaw { get; init; }
    }

    private sealed record WaterDefinition
    {
        public string CausticTexture { get; init; } = string.Empty;
        public double Level { get; init; }
        public string ReflectionTexture { get; init; } = string.Empty;
        public string SunBlobTexture { get; init; } = string.Empty;
        public string SunReflectionTexture { get; init; } = string.Empty;
        public string SurfaceResourcePath { get; init; } = string.Empty;
        public string SurfaceSha256 { get; init; } = string.Empty;
        public int TextureIndex { get; init; }
        public string WavesTexture { get; init; } = string.Empty;
    }
}

internal sealed record RetailTextureLayer(
    Texture2D Texture,
    float Opacity,
    Vector2 Offset,
    Vector2 Scale,
    bool BlendTextureAlpha = false);

/// <summary>
/// The two-directional-light fixed-function rig a draw through
/// <see cref="RetailFixedFunctionMaterial"/> runs under: <c>D3DRS_AMBIENT</c>
/// plus light 0 and light 1, whose directions retail keeps exactly
/// anti-parallel on both measured rigs.
/// </summary>
/// <remarks>
/// This is a per-draw property in the released game, not a level constant. The
/// values below were read at the <c>IDirect3DDevice9</c> calls themselves — the
/// <c>SetLight</c> site at <c>0x005512e1</c> (the tail of <c>0x00551200</c>,
/// which builds the 0x68-byte <c>D3DLIGHT9</c> on the stack from the 0x5c-byte
/// engine record at <c>0x009c65c0 + index*0x5c</c>), the <c>LightEnable</c> site
/// at <c>0x00551101</c>, and the seven <c>SetMaterial</c> sites — over two
/// independent launches of the safe copy (sha256 <c>E1436EF7…FADF4</c>) at two
/// different level times, with every payload byte-identical between them:
/// <list type="bullet">
/// <item><description>130 mode-0 <c>CRTMesh</c> static-world draws:
/// <c>D3DRS_AMBIENT = 0x000d0f2b</c>, light 0 = the height field's sun colour
/// travelling along the height field's sun vector, light 1 = the full anti-sun
/// travelling the other way.</description></item>
/// <item><description>442 <c>CRTTree</c> close-pine draws:
/// <c>D3DRS_AMBIENT = 0x0039293e</c>, light 0 = the sun colour scaled by
/// <c>0.1</c> travelling straight down BEA <c>+Z</c>, light 1 = the full
/// anti-sun travelling straight up BEA <c>-Z</c>.</description></item>
/// </list>
/// <c>D3DLIGHT9.Ambient</c> is <c>(0,0,0,0)</c> on both lights at every mesh
/// draw — only <c>CDXLandscape::Render</c>'s own re-upload carries a light
/// ambient term — so the whole normal-independent part of the vertex colour is
/// <c>D3DRS_AMBIENT</c>, and <c>D3DLIGHT9.Diffuse</c> carries the rest.
/// </remarks>
internal readonly record struct RetailMeshLightRig(
    Vector3 AmbientColor,
    Vector3 KeyLightColor,
    Vector3 FillLightColor,
    Vector3 KeyLightDirection)
{
    /// <summary>
    /// <c>D3DRS_AMBIENT</c> at all 442 <c>CRTTree</c> draws of a frame,
    /// <c>0x0039293e</c> = (57, 41, 62)/255, constant across 438 and 436 draws
    /// in two launches.
    ///
    /// FLAGGED: unlike the static world's <c>0x000d0f2b</c>, which is the
    /// height field's own <c>CHFD + 0x108C</c>, this value has no established
    /// shipped source. A whole-image operand scan of the safe copy finds zero
    /// occurrences of <c>0x0039293e</c>, so retail composes it at runtime and
    /// the composition is not yet read. It is a measurement, not a derivation.
    /// </summary>
    public const uint ClosePineAmbientRgb24 = 0x0039293Eu;

    /// <summary>
    /// The scale retail applies to the sun colour for the close-pine key light.
    /// <c>CRTTree::BuildRenderOutputs</c> unpacks the packed RGB24 sun colour at
    /// <c>0x004ddcb8</c>–<c>0x004ddd2f</c> and multiplies each channel by
    /// <c>[0x005db060]</c> = <c>1/256</c> and by <c>[0x005d85c0]</c> =
    /// <c>0.10000000149011612</c>. The parallel anti-sun block at
    /// <c>0x004ddd59</c> applies <c>1/256</c> only, which is why the fill light
    /// arrives unscaled. Confirmed at the device: the uploaded light-0
    /// <c>Diffuse</c> <c>(0.07382812, 0.06914063, 0.04726563)</c> divided by the
    /// sun colour <c>(189, 177, 121)/256</c> is <c>0.1</c> to float32 on all
    /// three channels.
    /// </summary>
    public const float ClosePineKeyLightScale = 0.1f;

    /// <summary>
    /// The rig measured at the 130 mode-0 <c>CRTMesh</c> static-world draws.
    /// This is the law the reconstruction already implemented, reproduced here
    /// unchanged: the device-side reading confirms it channel for channel,
    /// including the 3.32x up/under step, so nothing about the buildings moves.
    /// </summary>
    public static RetailMeshLightRig StaticWorld(Level100HeightFieldAsset terrain) => new(
        ToColorVector(terrain.AmbientColorRgb24, 255f),
        ToColorVector(terrain.SunColorRgb24, 256f),
        ToColorVector(terrain.AntiSunColorRgb24, 256f),
        terrain.SunlightDirection);

    /// <summary>
    /// The rig measured at the 442 <c>CRTTree</c> close-pine draws.
    ///
    /// Both light colours still come from the shipped height field and the only
    /// scale is the shipped <c>0.1</c>; the one value with no shipped source is
    /// <see cref="ClosePineAmbientRgb24"/>. With stage-zero
    /// <c>MODULATE2X</c> and white vertex colour this yields
    /// <c>2 x (ambient + light)</c> = <c>(0.59472, 0.45985, 0.58081)</c> on an
    /// up-facing normal and <c>(0.72050, 0.59501, 0.92377)</c> on a down-facing
    /// one — a step of <c>(1.21, 1.29, 1.59)</c>, luminance <c>1.304</c>,
    /// against the retail median of <c>1.144</c> measured at 353 matched pixel
    /// pairs. Note the sign: retail's foliage undersides are brighter and bluer
    /// than its tops.
    /// </summary>
    public static RetailMeshLightRig ClosePine(Level100HeightFieldAsset terrain) => new(
        ToColorVector(ClosePineAmbientRgb24, 255f),
        ToColorVector(terrain.SunColorRgb24, 256f) * ClosePineKeyLightScale,
        ToColorVector(terrain.AntiSunColorRgb24, 256f),
        // The uploaded light-0 Direction is BEA (0, 0, +1) exactly, and BEA's Z
        // is down. Carried into Godot by the same map the rest of the world path
        // uses -- MapVector, (x, y, z) -> (x, -z, -y), which
        // Level100HeightFieldAsset applies to the height field's own sun vector
        // and which Level100VertexDiffuseTests proves is the composite of
        // emit_obj's diag(1,1,-1) and this asset's RotationDegrees(-90, 0, 0) --
        // that is (0, -1, 0). Light 1's Direction is BEA (0, 0, -1), the exact
        // negation, which is the anti-parallel form the shader assumes.
        new Vector3(0f, -1f, 0f));

    private static Vector3 ToColorVector(uint rgb, float divisor) => new(
        ((rgb >> 16) & 0xFF) / divisor,
        ((rgb >> 8) & 0xFF) / divisor,
        (rgb & 0xFF) / divisor);
}

/// <summary>
/// The stage-zero <c>D3DTSS_COLOROP</c> a draw through
/// <see cref="RetailFixedFunctionMaterial"/> runs under. The members carry the
/// Direct3D <c>D3DTOP</c> enumerant values so the source reads the same way the
/// runtime dump does.
/// </summary>
/// <remarks>
/// This is a per-draw property in the released game, not a constant. Every
/// stage-zero <c>D3DTSS_COLOROP</c> value below was read out of the running
/// safe copy's texture-stage-state shadow at <c>0x008557f4</c> — the caching
/// setter at <c>0x00513820</c> computes its index as
/// <c>(type + stage*0n30)*4 + 0x008557f0</c> at <c>0x0051382a</c>–<c>0x00513833</c>,
/// and the invalidator at <c>0x00513600</c> clears 0n240 = 8 stages x 0n30
/// dwords there — sampled at the entry to <c>CMeshRenderer::RenderMeshCore</c>
/// (<c>0x00549570</c>, the image's only mesh-render call site) for one whole
/// Level 100 frame:
/// <list type="bullet">
/// <item><description>134 mode-0 static-world draws: <c>5</c>
/// = <c>D3DTOP_MODULATE2X</c>.</description></item>
/// <item><description>442 <c>CRTTree</c> close-mesh draws: <c>5</c>
/// = <c>D3DTOP_MODULATE2X</c>.</description></item>
/// <item><description>All seven cockpit batches: <c>4</c>
/// = <c>D3DTOP_MODULATE</c>, read 16 times inside the cockpit window with zero
/// stage-zero <c>COLOROP</c> transitions while inside it.</description></item>
/// <item><description>19 mode-4 static-world draws in that same frame: <c>4</c>
/// = <c>D3DTOP_MODULATE</c>.</description></item>
/// </list>
/// <c>COLORARG1</c> is <c>2</c> = <c>D3DTA_TEXTURE</c>, <c>COLORARG2</c> is
/// <c>0</c> = <c>D3DTA_DIFFUSE</c> and stage-one <c>COLOROP</c> is <c>1</c> =
/// <c>D3DTOP_DISABLE</c> at every one of those draws.
/// </remarks>
internal enum RetailStageZeroColorOperation
{
    /// <summary>D3DTOP_MODULATE. Texture x diffuse, no doubling.</summary>
    Modulate = 4,

    /// <summary>D3DTOP_MODULATE2X. Texture x diffuse x 2.</summary>
    Modulate2X = 5,
}

internal static class RetailFixedFunctionMaterial
{
    private static Shader? _objectShader;

    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_back;

        uniform sampler2D base_texture : filter_linear_mipmap_anisotropic, repeat_enable;
        uniform sampler2D dot3_texture : filter_linear_mipmap, repeat_enable;
        uniform sampler2D reflection_texture : filter_linear_mipmap_anisotropic, repeat_enable;
        uniform sampler2D overlay_texture : filter_linear_mipmap, repeat_enable;
        uniform float has_dot3;
        uniform float has_reflection;
        uniform float has_overlay;
        uniform float base_blend_texture_alpha;
        uniform float alpha_reference;
        // Stage-zero D3DTSS_COLOROP as a multiplier: 1.0 for D3DTOP_MODULATE
        // (4), 2.0 for D3DTOP_MODULATE2X (5). Per draw, never a constant --
        // see RetailStageZeroColorOperation for the runtime reads that fix it.
        uniform float stage_zero_gain;
        uniform vec2 dot3_offset;
        uniform vec2 dot3_scale;
        uniform float reflection_factor_alpha;
        uniform vec2 overlay_offset;
        uniform vec2 overlay_scale;
        uniform float overlay_opacity;
        // D3DRS_AMBIENT and the two anti-parallel D3DLIGHT9 records, per draw
        // and never a level constant -- see RetailMeshLightRig for the device
        // reads at SetLight (0x005512e1) that fix them. sun_color is light 0's
        // Diffuse, anti_sun_color is light 1's, and sunlight_direction is light
        // 0's Direction mapped into Godot; light 1's Direction is its exact
        // negation on both measured rigs, which is why one vector serves both.
        // On the 442 CRTTree draws light 0 is the sun scaled by the shipped 0.1
        // at 0x005d85c0 and the axis is vertical, so the names describe the
        // static-world rig and the slots, not the tree rig's physical roles.
        uniform vec3 ambient_color;
        uniform vec3 sun_color;
        uniform vec3 anti_sun_color;
        uniform vec3 sunlight_direction;
        uniform vec3 fog_color;
        uniform float fog_density;
        uniform float maximum_horizontal_distance_squared;
        varying vec3 vertex_light_color;
        varying vec3 model_light_direction;
        varying vec2 reflection_uv;
        varying float horizontal_distance_squared;

        vec3 retail_output(vec3 color) {
            if (OUTPUT_IS_SRGB) {
                return color;
            }
            vec3 low = color / 12.92;
            vec3 high = pow((color + vec3(0.055)) / 1.055, vec3(2.4));
            return mix(low, high, step(vec3(0.04045), color));
        }

        vec3 apply_retail_fog(vec3 color, float view_depth) {
            float visibility = clamp(exp(-fog_density * view_depth), 0.0, 1.0);
            return mix(fog_color, color, visibility);
        }

        void vertex() {
            // MultiMesh instance position is available through MODEL_MATRIX
            // here, so carry its camera distance into the fragment stage.
            vec2 horizontal_offset =
                MODEL_MATRIX[3].xz - CAMERA_POSITION_WORLD.xz;
            horizontal_distance_squared = dot(
                horizontal_offset,
                horizontal_offset);
            vec3 world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
            float sun = max(dot(world_normal, -sunlight_direction), 0.0);
            float anti_sun = max(dot(world_normal, sunlight_direction), 0.0);
            vertex_light_color = ambient_color + (sun_color * sun) +
                (anti_sun_color * anti_sun);
            // Retail leaves D3DRS_LIGHTING on for the FVF 0x152 mesh draw and
            // sets D3DRS_DIFFUSEMATERIALSOURCE and D3DRS_AMBIENTMATERIALSOURCE
            // to D3DMCS_COLOR1 (D3DStateCache__UseDefaultRenderState,
            // 0x004EB1E0), leaving D3DRS_COLORVERTEX at its TRUE default. The
            // per-vertex DIFFUSE dword is therefore the diffuse and ambient
            // material reflectance for every term of the lighting equation, and
            // the lit result is stage-zero COLORARG2 = D3DTA_DIFFUSE.
            vertex_light_color *= COLOR.rgb;
            model_light_direction = normalize(
                transpose(mat3(MODEL_MATRIX)) * sunlight_direction);

            // D3DTSS_TCI_CAMERASPACEREFLECTIONVECTOR generates
            // 2(N.E)N-E per vertex. Steam then applies [.5,0;0,-.5]
            // and the (.5,.5) offset before interpolating the coordinates.
            vec3 view_position = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            vec3 view_normal = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
            vec3 eye = normalize(-view_position);
            vec3 reflection_vector =
                (2.0 * dot(view_normal, eye) * view_normal) - eye;
            reflection_uv = vec2(
                (reflection_vector.x * 0.5) + 0.5,
                (reflection_vector.y * -0.5) + 0.5);
        }

        void fragment() {
            if (maximum_horizontal_distance_squared >= 0.0 &&
                horizontal_distance_squared > maximum_horizontal_distance_squared) {
                discard;
            }
            // Steam's high static-world renderer applies a -1 mip bias to
            // hardware stage zero.
            vec4 texture_color = texture(base_texture, UV, -1.0);
            if (base_blend_texture_alpha < 0.5 && texture_color.a < alpha_reference) {
                discard;
            }
            // Stage zero is COLORARG1 = D3DTA_TEXTURE, COLORARG2 =
            // D3DTA_DIFFUSE, and COLOROP is whichever of MODULATE /
            // MODULATE2X the draw is under. Read out of the running safe copy
            // from the texture-stage-state shadow at 0x008557f4 (the setter at
            // 0x00513820 indexes it as (type + stage*0n30)*4 + 0x008557f0):
            // 5 = MODULATE2X on the 134 mode-0 static-world draws and the 442
            // CRTTree mesh draws of one frame, 4 = MODULATE on all seven
            // cockpit batches (16 reads inside the cockpit window, zero
            // transitions while inside it) and on the 19 mode-4 static-world
            // draws of that same frame. So the gain is per draw.
            vec3 retail_color = min(
                texture_color.rgb * vertex_light_color * stage_zero_gain,
                vec3(1.0));
            if (base_blend_texture_alpha > 0.5) {
                retail_color = mix(retail_color, texture_color.rgb, texture_color.a);
            }

            if (has_dot3 > 0.5) {
                vec3 dot3_sample = texture(
                    dot3_texture,
                    (UV * dot3_scale) + dot3_offset).rgb;
                vec3 encoded_light = round(clamp(
                    (model_light_direction * 127.0) + vec3(128.0),
                    vec3(0.0),
                    vec3(255.0))) / 255.0;
                float dot3_value = clamp(
                    4.0 * dot(dot3_sample - vec3(0.5), encoded_light - vec3(0.5)),
                    0.0,
                    1.0);
                retail_color = vec3(dot3_value);
            }

            if (has_reflection > 0.5) {
                // The reflection layer is another stage-zero world draw, and
                // it is a MODE-0 one: [0x00704e48], the RenderMeshCore mode
                // global, was read at every one of 4,393 mesh renders across
                // three launches and is 0 on 4,314 and 4 on 79 -- never 2,
                // never 6, never 8, and the mode-2/mode-6 draw calls at
                // 0x0054a423 / 0x0054a466 never fired at all. D3DRS_LIGHTING
                // ([0x00855764], the render-state shadow at 0x00855540 indexed
                // state*4) is 1 across all 576 world and tree draws, so the
                // layer is lit, which is what this branch already assumed. It
                // inherits stage zero's colour operation and sampler state;
                // stage one only scales its alpha with texture factor.
                vec4 reflection_color = texture(
                    reflection_texture,
                    reflection_uv,
                    -1.0);
                vec3 reflection_stage_color = min(
                    reflection_color.rgb * vertex_light_color * stage_zero_gain,
                    vec3(1.0));
                float reflection_alpha = clamp(
                    reflection_color.a * reflection_factor_alpha,
                    0.0,
                    1.0);
                retail_color = mix(
                    retail_color,
                    reflection_stage_color,
                    reflection_alpha);
            }

            if (has_overlay > 0.5) {
                vec4 overlay_color = texture(
                    overlay_texture,
                    (UV * overlay_scale) + overlay_offset);
                float overlay_alpha = clamp(
                    overlay_color.a * overlay_opacity,
                    0.0,
                    1.0);
                retail_color = mix(retail_color, overlay_color.rgb, overlay_alpha);
            }

            retail_color = apply_retail_fog(retail_color, max(-VERTEX.z, 0.0));
            ALBEDO = retail_output(retail_color);
        }
        """;

    public static ShaderMaterial Create(
        Texture2D texture,
        Level100HeightFieldAsset terrain,
        RetailStageZeroColorOperation stageZeroColorOperation)
    {
        return Create(
            [
                new RetailTextureLayer(texture, 1f, Vector2.Zero, Vector2.One),
                null,
                null,
                null,
                null,
                null,
            ],
            terrain,
            stageZeroColorOperation: stageZeroColorOperation);
    }

    /// <param name="stageZeroColorOperation">
    /// The stage-zero <c>D3DTSS_COLOROP</c> this draw runs under. Retail's
    /// value varies by draw, so every call site states the one it measured;
    /// the default is the <c>MODULATE2X</c> read at the 134 mode-0
    /// static-world and 442 tree draws, which are the majority of the draws
    /// through this shader. See <see cref="RetailStageZeroColorOperation"/>.
    /// </param>
    /// <param name="lightRig">
    /// The <c>D3DRS_AMBIENT</c> and two-<c>D3DLIGHT9</c> rig this draw runs
    /// under. Retail's rig also varies by draw — the close pines run a
    /// different ambient, a 0.1x key light and a vertical light axis — so a
    /// call site that draws trees must say so. Omitting it keeps the
    /// static-world rig measured at the 130 mode-0 <c>CRTMesh</c> draws, which
    /// is the majority case and what every other call site through this shader
    /// was measured at. See <see cref="RetailMeshLightRig"/>.
    /// </param>
    public static ShaderMaterial Create(
        IReadOnlyList<RetailTextureLayer?> layers,
        Level100HeightFieldAsset terrain,
        float maximumHorizontalDistance = 0f,
        float alphaReference = 0.5f,
        RetailStageZeroColorOperation stageZeroColorOperation =
            RetailStageZeroColorOperation.Modulate2X,
        RetailMeshLightRig? lightRig = null)
    {
        if (layers.Count != 6 || layers[0] is not RetailTextureLayer baseLayer)
        {
            throw new InvalidDataException("Retail material requires one base layer and six exact slots.");
        }
        if (!float.IsFinite(maximumHorizontalDistance) || maximumHorizontalDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHorizontalDistance));
        }
        if (!float.IsFinite(alphaReference) || alphaReference is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(alphaReference));
        }
        float stageZeroGain = StageZeroGain(stageZeroColorOperation);
        RetailTextureLayer? dot3Layer = layers[1];
        RetailTextureLayer? reflectionLayer = layers[2];
        RetailTextureLayer? overlayLayer = layers[4];
        var material = new ShaderMaterial
        {
            Shader = _objectShader ??= new Shader { Code = ShaderCode },
        };
        material.SetShaderParameter("base_texture", baseLayer.Texture);
        material.SetShaderParameter("dot3_texture", dot3Layer?.Texture ?? baseLayer.Texture);
        material.SetShaderParameter("reflection_texture", reflectionLayer?.Texture ?? baseLayer.Texture);
        material.SetShaderParameter("overlay_texture", overlayLayer?.Texture ?? baseLayer.Texture);
        material.SetShaderParameter("has_dot3", dot3Layer is null ? 0f : 1f);
        material.SetShaderParameter("has_reflection", reflectionLayer is null ? 0f : 1f);
        material.SetShaderParameter("has_overlay", overlayLayer is null ? 0f : 1f);
        material.SetShaderParameter(
            "base_blend_texture_alpha",
            baseLayer.BlendTextureAlpha ? 1f : 0f);
        material.SetShaderParameter("alpha_reference", alphaReference);
        material.SetShaderParameter("stage_zero_gain", stageZeroGain);
        material.SetShaderParameter("dot3_offset", dot3Layer?.Offset ?? Vector2.Zero);
        material.SetShaderParameter("dot3_scale", dot3Layer?.Scale ?? Vector2.One);
        material.SetShaderParameter(
            "reflection_factor_alpha",
            ToTextureFactorAlpha(reflectionLayer?.Opacity ?? 0f));
        material.SetShaderParameter("overlay_offset", overlayLayer?.Offset ?? Vector2.Zero);
        material.SetShaderParameter("overlay_scale", overlayLayer?.Scale ?? Vector2.One);
        material.SetShaderParameter("overlay_opacity", overlayLayer?.Opacity ?? 0f);
        RetailMeshLightRig rig = lightRig ?? RetailMeshLightRig.StaticWorld(terrain);
        material.SetShaderParameter("ambient_color", rig.AmbientColor);
        material.SetShaderParameter("sun_color", rig.KeyLightColor);
        material.SetShaderParameter("anti_sun_color", rig.FillLightColor);
        material.SetShaderParameter("sunlight_direction", rig.KeyLightDirection);
        material.SetShaderParameter("fog_color", new Vector3(
            terrain.FogColor.R,
            terrain.FogColor.G,
            terrain.FogColor.B));
        material.SetShaderParameter("fog_density", terrain.FogDensity);
        material.SetShaderParameter(
            "maximum_horizontal_distance_squared",
            maximumHorizontalDistance > 0f
                ? maximumHorizontalDistance * maximumHorizontalDistance
                : -1f);
        return material;
    }

    /// <summary>
    /// The stage-zero colour operation as the multiplier the shader applies.
    /// <c>D3DTOP_MODULATE</c> is texture x diffuse and <c>D3DTOP_MODULATE2X</c>
    /// is that doubled, so these are exactly 1 and 2. Any other enumerant is
    /// refused rather than silently collapsed onto one of them: nothing has
    /// observed retail using one at these draws.
    /// </summary>
    private static float StageZeroGain(RetailStageZeroColorOperation operation) =>
        operation switch
        {
            RetailStageZeroColorOperation.Modulate => 1f,
            RetailStageZeroColorOperation.Modulate2X => 2f,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static float ToTextureFactorAlpha(float strength)
    {
        int alpha = Math.Clamp(
            (int)MathF.Round(strength * byte.MaxValue, MidpointRounding.ToEven),
            byte.MinValue,
            byte.MaxValue);
        return alpha / (float)byte.MaxValue;
    }
}

/// <summary>One released hierarchy part bound to the node that carries it.</summary>
internal sealed record Level100StaticWorldAnimationBinding(
    Level100StaticWorldMeshAnimation Mesh,
    Level100StaticWorldAnimatedPart Part,
    MeshInstance3D Node);

/// <summary>
/// Plays the released Level 100 base-world hierarchy idle loops.
///
/// <para>Presentation only. It advances from the frame delta the world view
/// already passes to the water and cloud-shadow owners, holds its own wrapped
/// accumulator, writes nothing but node transforms, and is never read back by
/// Core or by the mission. The tracks it plays are decorative rigid parts on
/// scenery - a spinning docks crane, two counter-rotating radar dishes, four
/// solar-pod petals - so no simulation state observes them.</para>
///
/// <para>Playback snaps to a stored virtual frame and never interpolates,
/// because the released selection through <c>VHFM</c> is a table lookup per
/// virtual frame, not a curve.</para>
/// </summary>
internal sealed class Level100StaticWorldAnimationDriver(
    int framesPerSecond,
    IReadOnlyList<Level100StaticWorldAnimationBinding> bindings)
{
    private readonly int[] _shownFrames = new int[bindings.Count];
    private double _elapsedSeconds;

    public int BindingCount => bindings.Count;

    /// <summary>
    /// Virtual frames per second, straight from the manifest's own
    /// <c>framesPerSecond</c>. No rate is chosen here.
    /// </summary>
    public int FramesPerSecond { get; } = framesPerSecond;

    public void Update(float frameDelta)
    {
        if (!float.IsFinite(frameDelta) || frameDelta <= 0f)
        {
            return;
        }

        // Wrap on the longest lap present so the accumulator cannot drift into
        // the range where a double loses whole-frame resolution during a long
        // session. Every lap length divides evenly into its own modulus, so this
        // never shifts a mesh's phase.
        _elapsedSeconds += frameDelta;
        double period = LongestLapSeconds();
        if (period > 0d && _elapsedSeconds >= period)
        {
            _elapsedSeconds %= period;
        }

        for (int index = 0; index < bindings.Count; index++)
        {
            Level100StaticWorldAnimationBinding binding = bindings[index];
            int frame = binding.Mesh.SelectVirtualFrame(_elapsedSeconds, FramesPerSecond);
            if (frame == _shownFrames[index])
            {
                continue;
            }

            _shownFrames[index] = frame;
            binding.Node.Transform = ToObjSpaceTransform(binding.Part.Frames[frame]);
        }
    }

    /// <summary>
    /// One released delta into a Godot <see cref="Transform3D"/>, applied in OBJ
    /// space with no conversion.
    ///
    /// <para><c>basis</c> is nine floats ROW-major
    /// (<c>cmsh_static_preview.py:1084</c> flattens <c>obj_rows</c> row by row,
    /// under the storage convention stated at <c>:901-902</c>). Godot's
    /// three-vector constructor takes COLUMNS - the shipped
    /// <c>GodotSharp.dll</c> names its parameters
    /// <c>column0, column1, column2</c> - so the rows are transposed into
    /// columns here. Passing them straight through would build the transpose,
    /// which for a rotation is its inverse: every dish would spin backwards.</para>
    /// </summary>
    public static Transform3D ToObjSpaceTransform(Level100RigidFrame frame)
    {
        float[] basis = frame.Basis;
        return new Transform3D(
            new Basis(
                new Vector3(basis[0], basis[3], basis[6]),
                new Vector3(basis[1], basis[4], basis[7]),
                new Vector3(basis[2], basis[5], basis[8])),
            new Vector3(frame.Origin[0], frame.Origin[1], frame.Origin[2]));
    }

    private double LongestLapSeconds()
    {
        long lapFrames = 1;
        foreach (Level100StaticWorldAnimationBinding binding in bindings)
        {
            if (binding.Mesh.LoopFrameCount > 0)
            {
                lapFrames = Lcm(lapFrames, binding.Mesh.LoopFrameCount);
            }
        }

        return lapFrames / (double)FramesPerSecond;
    }

    private static long Lcm(long left, long right)
    {
        long a = left;
        long b = right;
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return left / a * right;
    }
}
