// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Adapts the released Level 100 fixed-function water path. The moving grid is
/// camera-relative presentation; the serialized SURF contours remain fixed in
/// retail world space.
/// </summary>
internal sealed class Level100WaterAsset
{
    private const int GridCellsPerAxis = 24;
    private const int GridVerticesPerAxis = GridCellsPerAxis + 1;
    private const float GridStart = -768f;
    private const float GridStep = 64f;
    private const float RadialScale = 0.0013586956774815917f;
    private const int SurfaceRecordCount = 515;
    private const int SurfaceSegmentCount = 514;
    private const int SurfaceSourceLength = 18_572;
    private const float RetailDepthBiasScale = 0.00014f;
    private const int ShorelineDepthBiasIndex = 4;
    private const int SunGlintDepthBiasIndex = 6;
    private const float CausticPhaseRadiansPerSecond = 1f;
    private const float WaveScrollPerSecond = 0.06f;
    private const float SunGlintCenterHeightScale = 6f;
    private const float SunGlintHalfWidthHeightScale = 2f;
    private const float SunGlintHalfLengthHeightScale = 8f;

    private static Shader? _gridShader;
    private static Shader? _shorelinePrimaryShader;
    private static Shader? _sunGlintShader;

    private readonly MeshInstance3D _grid;
    private readonly ShaderMaterial _gridMaterial;
    private readonly ShaderMaterial _shorelinePrimaryMaterial;
    private readonly MeshInstance3D _sunGlint;
    private readonly ShaderMaterial _sunGlintMaterial;
    private readonly Vector3 _sunGlintOffsetDirection;
    private readonly float _waterHeight;
    private float _causticPhase;
    private float _mainWaveScroll;

    private const string GridShaderCode = """
        shader_type spatial;
        render_mode unshaded, blend_mix, depth_draw_always, cull_disabled;

        uniform sampler2D caustic_texture : filter_linear_mipmap, repeat_enable;
        uniform sampler2D reflection_texture : filter_linear_mipmap, repeat_enable;
        uniform vec3 water_color;
        uniform vec2 retail_origin;
        uniform float caustic_phase;
        uniform vec3 fog_color;
        uniform float fog_density;
        varying vec3 water_world_position;

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
            water_world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
        }

        void fragment() {
            vec2 retail_xy = vec2(
                water_world_position.x + retail_origin.x,
                retail_origin.y - water_world_position.z);

            vec2 caustic_a = vec2(
                (retail_xy.x * 0.1) + (retail_xy.y * 0.03),
                (retail_xy.x * 0.03) - (retail_xy.y * 0.1));
            caustic_a += vec2(sin(caustic_phase), cos(caustic_phase)) * 0.1;

            float second_phase = caustic_phase + 3.14159265359;
            vec2 caustic_b = vec2(
                (retail_xy.x * 0.03) + (retail_xy.y * 0.1),
                (retail_xy.x * 0.1) - (retail_xy.y * 0.03));
            caustic_b += vec2(sin(second_phase), cos(second_phase)) * 0.1;

            // The active Steam path uses the camera/world translation with a
            // 1/256 texture transform. The animated 1/2-scale transform belongs
            // to the optional advanced path, which Level 100 does not enable.
            vec2 reflection_uv = retail_xy / 256.0;
            vec3 caustic_0 = texture(caustic_texture, caustic_a).rgb;
            vec3 caustic_1 = texture(caustic_texture, caustic_b).rgb;
            vec3 reflected = texture(reflection_texture, reflection_uv).rgb;

            // Steam disables texture stage 3 before switching to the one-UV
            // grid vertex format. The animated waves stage remains active only
            // for the authored shoreline passes.
            vec3 base_water = min((water_color * caustic_0 * caustic_1) + reflected, vec3(1.0));
            vec3 retail_color = min(COLOR.rgb * base_water, vec3(1.0));
            retail_color = apply_retail_fog(retail_color, max(-VERTEX.z, 0.0));
            ALBEDO = retail_output(retail_color);
            ALPHA = COLOR.a;
        }
        """;

    private const string ShorelinePrimaryShaderCode = """
        shader_type spatial;
        render_mode unshaded, blend_mix, depth_draw_always, cull_disabled;

        uniform sampler2D caustic_texture : filter_linear_mipmap, repeat_enable;
        uniform sampler2D reflection_texture : filter_linear_mipmap, repeat_enable;
        uniform sampler2D waves_texture : filter_linear_mipmap, repeat_enable;
        uniform vec3 water_color;
        uniform vec2 retail_origin;
        uniform float caustic_phase;
        uniform float main_wave_scroll;
        uniform float projection_depth_bias;
        uniform vec3 fog_color;
        uniform float fog_density;
        varying vec3 water_world_position;

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
            water_world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
            // Retail subtracts index * ZBIAS_SCALER from D3D projection slot
            // 14. Godot's reversed-Z clip space uses the opposite direction.
            POSITION.z += projection_depth_bias * POSITION.w;
        }

        void fragment() {
            vec2 retail_xy = vec2(
                water_world_position.x + retail_origin.x,
                retail_origin.y - water_world_position.z);
            vec2 caustic_a = vec2(
                (retail_xy.x * 0.1) + (retail_xy.y * 0.03),
                (retail_xy.x * 0.03) - (retail_xy.y * 0.1));
            caustic_a += vec2(sin(caustic_phase), cos(caustic_phase)) * 0.1;
            float second_phase = caustic_phase + 3.14159265359;
            vec2 caustic_b = vec2(
                (retail_xy.x * 0.03) + (retail_xy.y * 0.1),
                (retail_xy.x * 0.1) - (retail_xy.y * 0.03));
            caustic_b += vec2(sin(second_phase), cos(second_phase)) * 0.1;
            vec3 caustic_0 = texture(caustic_texture, caustic_a).rgb;
            vec3 caustic_1 = texture(caustic_texture, caustic_b).rgb;
            vec3 reflected = texture(reflection_texture, retail_xy / 256.0).rgb;
            vec4 wave = texture(
                waves_texture,
                (UV * 0.5) + vec2(0.0, main_wave_scroll));

            // Stage 3 is D3DTOP_MULTIPLYADD with CURRENT as Arg0,
            // waves as Arg1, and vertex diffuse as Arg2, i.e.
            // result = Arg1 * Arg2 + Arg0 = waves * diffuse + current.
            // This shader previously computed waves + diffuse * current, which
            // is a different operator: it added the wave texture at full
            // strength independently of the vertex colour ramp. Measured
            // consequence in local-lab/godot-captures at the retail-matched
            // offsets t0+2/256/499/749 ms: 2,600-6,000 px per water box with
            // G >= 250 and B >= 250, against 0 such px in the same boxes of
            // the retail reference. waves is also the only water texture with
            // a hue (mean G-R = +43), so adding it unmodulated is what
            // inverted B > G > R.
            vec3 base_water = min((water_color * caustic_0 * caustic_1) + reflected, vec3(1.0));
            vec3 retail_color = min((wave.rgb * COLOR.rgb) + base_water, vec3(1.0));
            retail_color = apply_retail_fog(retail_color, max(-VERTEX.z, 0.0));
            ALBEDO = retail_output(retail_color);
            ALPHA = COLOR.a;
        }
        """;

    private const string SunGlintShaderCode = """
        shader_type spatial;
        render_mode unshaded, blend_mix, depth_draw_opaque, cull_disabled;

        uniform sampler2D sun_reflection_texture : filter_linear_mipmap, repeat_enable;
        uniform sampler2D sun_blob_texture : filter_linear_mipmap, repeat_enable;
        uniform float glint_phase;
        uniform float projection_depth_bias;
        uniform vec2 retail_origin;
        varying vec2 sun_reflection_coordinates;

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
            sun_reflection_coordinates = vec2(
                world_position.x + retail_origin.x,
                retail_origin.y - world_position.z);
            POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
            POSITION.z += projection_depth_bias * POSITION.w;
        }

        void fragment() {
            vec2 reflection_a_uv = vec2(
                (sun_reflection_coordinates.x * 0.1) + (sun_reflection_coordinates.y * 0.03),
                (sun_reflection_coordinates.x * 0.03) - (sun_reflection_coordinates.y * 0.1));
            reflection_a_uv += vec2(sin(glint_phase), cos(glint_phase)) * 0.1;

            float second_phase = glint_phase + 3.14159265359;
            vec2 reflection_b_uv = vec2(
                (sun_reflection_coordinates.x * 0.03) + (sun_reflection_coordinates.y * 0.1),
                (sun_reflection_coordinates.x * 0.1) - (sun_reflection_coordinates.y * 0.03));
            reflection_b_uv += vec2(sin(second_phase), cos(second_phase)) * 0.1;

            vec4 reflection_a = texture(sun_reflection_texture, reflection_a_uv);
            vec4 reflection_b = texture(sun_reflection_texture, reflection_b_uv);
            vec4 blob = texture(sun_blob_texture, UV);

            // Retail stages 0..2 use SELECTARG1, ADD, ADD for alpha, then
            // D3DCMP_GREATEREQUAL against alpha ref 0xC0. The colour path selects
            // the retained Level-100 texture-factor value #E8E8FF.
            float glint_alpha = min(reflection_a.a + reflection_b.a + blob.a, 1.0);
            if (glint_alpha < (192.0 / 255.0)) {
                discard;
            }
            ALBEDO = retail_output(vec3(232.0 / 255.0, 232.0 / 255.0, 1.0));
            ALPHA = 1.0;
        }
        """;

    // REMOVED 2026-07-25: an additive (`SRCALPHA`/`ONE`) second draw of the
    // whole shoreline mesh and an uncited flat sun-glint slab. The additive
    // shoreline model remains falsified by retail pixels:
    //
    //  * The additive overlay re-applied `water-waves` — the only water texture
    //    with a hue — on top of a primary pass that already consumed it, so the
    //    single hue-bearing texture landed twice. No `D3DRS_SRCBLEND` /
    //    `DESTBLEND` / `ALPHABLENDENABLE` write is decoded anywhere in
    //    `reverse-engineering/` for the water or sky (see
    //    `local-lab/WATER-SKY-EVIDENCE-2026-07-25.md` 5.2); the two
    //    `CDXSurf__Render` call sites in `CWaterRenderSystem__RenderMainPass`
    //    (`DXSurf.cpp.md:25,55`) enter the *same* function with the same bound
    //    render state and differ only in its `validated_mode` byte, so they
    //    cannot be a modulate pass plus an additive pass.
    // The recovered RenderMainPass range 0x0055C3ED..0x0055CAAD now supplies
    // the missing sun-glint chain: sunreflect on stages 0/1, sunblob on stage 2,
    // SELECT/ADD/ADD alpha, 0xC0 alpha ref, #E8E8FF texture factor, index-6
    // projection bias, and one camera-height-relative quad. That bounded pass
    // is implemented below; its sampler edge behavior and pixel identity remain
    // open rather than reviving the discarded full-slab model.

    private Level100WaterAsset(
        Node3D root,
        MeshInstance3D grid,
        ShaderMaterial gridMaterial,
        ShaderMaterial shorelinePrimaryMaterial,
        MeshInstance3D sunGlint,
        ShaderMaterial sunGlintMaterial,
        Vector3 sunGlintOffsetDirection,
        float waterHeight,
        int shorelineTriangleCount)
    {
        Root = root;
        _grid = grid;
        _gridMaterial = gridMaterial;
        _shorelinePrimaryMaterial = shorelinePrimaryMaterial;
        _sunGlint = sunGlint;
        _sunGlintMaterial = sunGlintMaterial;
        _sunGlintOffsetDirection = sunGlintOffsetDirection;
        _waterHeight = waterHeight;
        ShorelineTriangleCount = shorelineTriangleCount;
    }

    public Node3D Root { get; }

    public int GridVertexCount => GridVerticesPerAxis * GridVerticesPerAxis;

    public int GridTriangleCount => GridCellsPerAxis * GridCellsPerAxis * 2;

    public int ShorelineTriangleCount { get; }

    public static Level100WaterAsset Create(
        Level100HeightFieldAsset terrain,
        Texture2D reflection,
        Texture2D caustic,
        Texture2D waves,
        Texture2D sunBlob,
        Texture2D sunReflection,
        string surfaceResourcePath,
        string surfaceSha256)
    {
        var root = new Node3D { Name = "RetailLevel100Water" };
        var gridMaterial = new ShaderMaterial
        {
            Shader = _gridShader ??= new Shader { Code = GridShaderCode },
        };
        gridMaterial.SetShaderParameter("reflection_texture", reflection);
        gridMaterial.SetShaderParameter("caustic_texture", caustic);
        gridMaterial.SetShaderParameter("water_color", new Vector3(
            0x21 / 255f,
            0x21 / 255f,
            0x3D / 255f));
        gridMaterial.SetShaderParameter("retail_origin", new Vector2(
            Level100HeightFieldAsset.PlayerStartX,
            Level100HeightFieldAsset.PlayerStartZ));
        gridMaterial.SetShaderParameter("caustic_phase", 0f);
        gridMaterial.RenderPriority = 1;
        SetFogParameters(gridMaterial, terrain);

        var grid = new MeshInstance3D
        {
            Name = "RetailCameraRelativeWaterGrid",
            Mesh = BuildGridMesh(),
            MaterialOverride = gridMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(grid);

        var shorelineMaterial = new ShaderMaterial
        {
            Shader = _shorelinePrimaryShader ??= new Shader { Code = ShorelinePrimaryShaderCode },
        };
        shorelineMaterial.SetShaderParameter("reflection_texture", reflection);
        shorelineMaterial.SetShaderParameter("caustic_texture", caustic);
        shorelineMaterial.SetShaderParameter("waves_texture", waves);
        shorelineMaterial.SetShaderParameter("water_color", new Vector3(
            0x21 / 255f,
            0x21 / 255f,
            0x3D / 255f));
        shorelineMaterial.SetShaderParameter("retail_origin", new Vector2(
            Level100HeightFieldAsset.PlayerStartX,
            Level100HeightFieldAsset.PlayerStartZ));
        shorelineMaterial.SetShaderParameter("caustic_phase", 0f);
        shorelineMaterial.SetShaderParameter("main_wave_scroll", 0f);
        shorelineMaterial.SetShaderParameter(
            "projection_depth_bias",
            ShorelineDepthBiasIndex * RetailDepthBiasScale);
        shorelineMaterial.RenderPriority = 0;
        SetFogParameters(shorelineMaterial, terrain);
        ArrayMesh shorelineMesh = BuildShorelineMesh(
            surfaceResourcePath,
            surfaceSha256);
        var shoreline = new MeshInstance3D
        {
            Name = "RetailAuthoredShorelineBands",
            Mesh = shorelineMesh,
            MaterialOverride = shorelineMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(shoreline);

        var sunGlintMaterial = new ShaderMaterial
        {
            Shader = _sunGlintShader ??= new Shader { Code = SunGlintShaderCode },
        };
        sunGlintMaterial.SetShaderParameter("sun_reflection_texture", sunReflection);
        sunGlintMaterial.SetShaderParameter("sun_blob_texture", sunBlob);
        sunGlintMaterial.SetShaderParameter("glint_phase", 0f);
        sunGlintMaterial.SetShaderParameter("retail_origin", new Vector2(
            Level100HeightFieldAsset.PlayerStartX,
            Level100HeightFieldAsset.PlayerStartZ));
        sunGlintMaterial.SetShaderParameter(
            "projection_depth_bias",
            SunGlintDepthBiasIndex * RetailDepthBiasScale);
        sunGlintMaterial.RenderPriority = 2;
        var sunGlint = new MeshInstance3D
        {
            Name = "RetailCameraRelativeWaterSunGlint",
            Mesh = BuildSunGlintMesh(),
            MaterialOverride = sunGlintMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(sunGlint);

        Vector3 horizontalSun = new(
            terrain.SunPosition.X,
            0f,
            -terrain.SunPosition.Y);
        Vector3 sunGlintOffsetDirection = horizontalSun.LengthSquared() > 0f
            ? -horizontalSun.Normalized()
            : Vector3.Forward;

        return new Level100WaterAsset(
            root,
            grid,
            gridMaterial,
            shorelineMaterial,
            sunGlint,
            sunGlintMaterial,
            sunGlintOffsetDirection,
            terrain.WaterRelativeHeight,
            SurfaceSegmentCount * 4);
    }

    private static void SetFogParameters(
        ShaderMaterial material,
        Level100HeightFieldAsset terrain)
    {
        material.SetShaderParameter("fog_color", new Vector3(
            terrain.FogColor.R,
            terrain.FogColor.G,
            terrain.FogColor.B));
        material.SetShaderParameter("fog_density", terrain.FogDensity);
    }

    public void Update(Vector3 cameraPosition, float frameDelta)
    {
        if (float.IsFinite(frameDelta) && frameDelta > 0f)
        {
            _causticPhase = Mathf.PosMod(
                _causticPhase + (frameDelta * CausticPhaseRadiansPerSecond),
                Mathf.Tau);
            _mainWaveScroll = Mathf.PosMod(
                _mainWaveScroll + (frameDelta * WaveScrollPerSecond),
                1f);
        }
        _gridMaterial.SetShaderParameter("caustic_phase", _causticPhase);
        _shorelinePrimaryMaterial.SetShaderParameter("caustic_phase", _causticPhase);
        _shorelinePrimaryMaterial.SetShaderParameter("main_wave_scroll", _mainWaveScroll);
        _sunGlintMaterial.SetShaderParameter("glint_phase", _causticPhase);

        _grid.Position = new Vector3(cameraPosition.X, _waterHeight, cameraPosition.Z);

        float cameraHeight = cameraPosition.Y - _waterHeight;
        Vector3 center = new(cameraPosition.X, _waterHeight, cameraPosition.Z);
        center += _sunGlintOffsetDirection *
            (cameraHeight * SunGlintCenterHeightScale);
        _sunGlint.Position = center;
        _sunGlint.Rotation = new Vector3(
            0f,
            Mathf.Atan2(_sunGlintOffsetDirection.X, _sunGlintOffsetDirection.Z),
            0f);
        _sunGlint.Scale = new Vector3(
            cameraHeight * SunGlintHalfWidthHeightScale,
            1f,
            cameraHeight * SunGlintHalfLengthHeightScale);
    }

    private static ArrayMesh BuildSunGlintMesh()
    {
        Vector3[] vertices =
        [
            new(-1f, 0f, -1f),
            new(1f, 0f, -1f),
            new(-1f, 0f, 1f),
            new(1f, 0f, 1f),
        ];
        Vector2[] uvs =
        [
            new(0f, 0f),
            new(1f, 0f),
            new(0f, 1f),
            new(1f, 1f),
        ];
        Color[] colors = Enumerable.Repeat(Colors.White, vertices.Length).ToArray();
        return BuildMeshSurface(vertices, colors, uvs, [0, 2, 1, 1, 2, 3]);
    }

    private static ArrayMesh BuildGridMesh()
    {
        int vertexCount = GridVerticesPerAxis * GridVerticesPerAxis;
        var vertices = new Vector3[vertexCount];
        var colors = new Color[vertexCount];
        var indices = new int[GridCellsPerAxis * GridCellsPerAxis * 6];

        int vertex = 0;
        for (int z = 0; z < GridVerticesPerAxis; z++)
        {
            float localZ = GridStart + (z * GridStep);
            for (int x = 0; x < GridVerticesPerAxis; x++)
            {
                float localX = GridStart + (x * GridStep);
                vertices[vertex] = new Vector3(localX, 0f, localZ);
                int alpha = Math.Clamp(
                    500 - (int)MathF.Round(
                        MathF.Sqrt((localX * localX) + (localZ * localZ)) *
                        RadialScale *
                        500f),
                    0,
                    255);
                colors[vertex] = new Color(1f, 1f, 1f, alpha / 255f);
                vertex++;
            }
        }

        int index = 0;
        for (int z = 0; z < GridCellsPerAxis; z++)
        {
            for (int x = 0; x < GridCellsPerAxis; x++)
            {
                int topLeft = (z * GridVerticesPerAxis) + x;
                int bottomLeft = topLeft + GridVerticesPerAxis;
                indices[index++] = topLeft;
                indices[index++] = bottomLeft;
                indices[index++] = topLeft + 1;
                indices[index++] = topLeft + 1;
                indices[index++] = bottomLeft;
                indices[index++] = bottomLeft + 1;
            }
        }

        return BuildMeshSurface(vertices, colors, null, indices);
    }

    private static ArrayMesh BuildShorelineMesh(
        string resourcePath,
        string expectedSha256)
    {
        byte[] source = Godot.FileAccess.GetFileAsBytes(resourcePath);
        if (source.Length != SurfaceSourceLength ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                Convert.ToHexString(SHA256.HashData(source)),
                expectedSha256))
        {
            throw new InvalidDataException(
                "The locally materialized Level 100 shoreline is missing or changed.");
        }
        RequireChunk(source, 0, "SURF"u8, SurfaceSourceLength - 8);
        RequireChunk(source, 8, "SURF"u8, SurfaceSourceLength - 16);
        if (BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(16, 4)) != 1)
        {
            throw new InvalidDataException("Level 100 has an unsupported shoreline array count.");
        }
        RequireChunk(source, 20, "OUTL"u8, SurfaceSourceLength - 28);
        if (BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(28, 4)) != SurfaceSegmentCount ||
            source.Length != 32 + (SurfaceRecordCount * 9 * sizeof(float)))
        {
            throw new InvalidDataException("Level 100 has an unsupported shoreline contour count.");
        }

        var contours = new Vector3[3, SurfaceRecordCount];
        int offset = 32;
        for (int point = 0; point < SurfaceRecordCount; point++)
        {
            for (int contour = 0; contour < 3; contour++)
            {
                float retailX = ReadSingle(source, offset);
                float retailY = ReadSingle(source, offset + 4);
                float retailZ = ReadSingle(source, offset + 8);
                offset += 12;
                if (!float.IsFinite(retailX) || !float.IsFinite(retailY) ||
                    BitConverter.SingleToInt32Bits(retailZ) != unchecked((int)0xC10D70A4))
                {
                    throw new InvalidDataException("Level 100 shoreline contains an invalid point.");
                }
                contours[contour, point] = new Vector3(
                    retailX - Level100HeightFieldAsset.PlayerStartX,
                    Level100HeightFieldAsset.PlayerStartElevation - retailZ,
                    Level100HeightFieldAsset.PlayerStartZ - retailY);
            }
        }

        var mesh = new ArrayMesh();
        AddShorelineBand(mesh, contours, 0, 1, innerBand: true);
        AddShorelineBand(mesh, contours, 1, 2, innerBand: false);
        return mesh;
    }

    private static void AddShorelineBand(
        ArrayMesh mesh,
        Vector3[,] contours,
        int firstContour,
        int secondContour,
        bool innerBand)
    {
        var vertices = new Vector3[SurfaceRecordCount * 2];
        var colors = new Color[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var indices = new int[SurfaceSegmentCount * 6];

        for (int point = 0; point < SurfaceRecordCount; point++)
        {
            float phase = point * 0.125f;
            float wave = MathF.Sin(phase * 0.5f) * 0.5f;
            int first = point * 2;
            int second = first + 1;
            vertices[first] = contours[firstContour, point];
            vertices[second] = contours[secondContour, point];

            if (innerBand)
            {
                colors[first] = new Color(1f, 1f, 1f, 0f);
                colors[second] = new Color(1f, 1f, 1f, 192f / 255f);
                uvs[first] = new Vector2(phase, wave - 0.25f - (phase * 0.25f));
                uvs[second] = new Vector2(phase, wave - (phase * 0.25f));
            }
            else
            {
                colors[first] = new Color(1f, 1f, 1f, 192f / 255f);
                colors[second] = new Color(0f, 0f, 0f, 1f);
                uvs[first] = new Vector2(phase, wave - (phase * 0.25f));
                uvs[second] = new Vector2(
                    phase + 0.0625f,
                    wave + 2f - (phase * 0.25f));
            }
        }

        int index = 0;
        for (int segment = 0; segment < SurfaceSegmentCount; segment++)
        {
            int first = segment * 2;
            int second = first + 1;
            int nextFirst = first + 2;
            int nextSecond = first + 3;
            indices[index++] = first;
            indices[index++] = second;
            indices[index++] = nextFirst;
            indices[index++] = nextFirst;
            indices[index++] = second;
            indices[index++] = nextSecond;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    private static ArrayMesh BuildMeshSurface(
        Vector3[] vertices,
        Color[] colors,
        Vector2[]? uvs,
        int[] indices)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        if (uvs is not null)
        {
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        }
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void RequireChunk(byte[] source, int offset, ReadOnlySpan<byte> tag, int size)
    {
        if (!source.AsSpan(offset, 4).SequenceEqual(tag) ||
            BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset + 4, 4)) != size)
        {
            throw new InvalidDataException("Level 100 shoreline has invalid chunk framing.");
        }
    }

    private static float ReadSingle(byte[] source, int offset) =>
        BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset, sizeof(float))));
}
