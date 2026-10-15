// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Loads the exact walker, jet, and cockpit specimens consumed by this slice as
/// their authored part hierarchies. This is intentionally not a general AYA or
/// CMSH importer.
/// </summary>
internal sealed class RetailAquilaWalkerAsset
{
    private const string ExpectedSourceSha256 =
        "D4C8FA752229AF4111B31EFA5FF5928C892736FAA6A807915412767F3CD3C6B2";
    private const int ExpectedSourceLength = 484_616;
    private const int ExpectedInflatedLength = 932_797;
    private const int ExpectedTextureCount = 8;
    private const int ExpectedPartCount = 63;
    private const int ExpectedExpandedSurfaceCount = 54;
    private const int ExpectedLegMotionStart = 1;
    private const int ExpectedLegMotionEnd = 100;
    private const int ExpectedAnimatedPartCount = 20;
    private const int MaximumInflatedLength = 2 * 1024 * 1024;
    private const float TransformTolerance = 0.0001f;
    private static readonly AssetProfile s_walkerProfile = new(
        "Aquila walker",
        "RetailAquilaWalker",
        ExpectedSourceSha256,
        ExpectedSourceLength,
        ExpectedInflatedLength,
        ExpectedTextureCount,
        ExpectedPartCount,
        ExpectedExpandedSurfaceCount,
        ExpectedAnimatedPartCount,
        3_404,
        true,
        0f,
        1f,
        Vector3.Zero,
        true,
        [
            new AnimationMode("walk", 1, 1, 1, 1f),
            new AnimationMode("transform", 1, 1, 0, 0f),
            new AnimationMode("LegMotion", 1, 100, 99, 1f / 99f),
        ],
        // Unmeasured, and therefore unchanged. The 4,393-draw stage-zero
        // COLOROP census was taken from a first-person Level 100 frame, in
        // which the exterior walker is not drawn at all, so no observation
        // covers this asset. It keeps the MODULATE2X the whole shader used
        // before the cockpit reading split them apart; changing it would be a
        // guess.
        RetailStageZeroColorOperation.Modulate2X);
    private static readonly AssetProfile s_jetProfile = new(
        "Aquila jet",
        "RetailAquilaJet",
        "35AADA1313C3CBB796BA75DB071321035F7005096DA7C148A7514944F4772B4C",
        334_813,
        965_200,
        6,
        54,
        58,
        37,
        2_044,
        false,
        null,
        25f,
        new Vector3(0f, 0.6706632f, 0f),
        true,
        [
            new AnimationMode("walk", 25, 25, 1, 1f),
            new AnimationMode("walktofly", 25, 50, 25, 0.04f),
            new AnimationMode("flytowalk", 0, 25, 25, 0.04f),
            new AnimationMode("fly", 0, 0, 1, 1f),
        ],
        // Unmeasured, and therefore unchanged, for the same reason as the
        // walker profile above.
        RetailStageZeroColorOperation.Modulate2X);
    private static readonly AssetProfile s_cockpitProfile = new(
        "Aquila cockpit",
        "RetailAquilaCockpit",
        "008B9292C59A5564BA3696F65D5BD51030D3E57250BC792D9D2B7F01292CDD4A",
        31_750,
        137_757,
        3,
        21,
        10,
        6,
        4_084,
        false,
        null,
        25f,
        // Zero, from the released binary rather than from fitting. The player
        // camera is CThingCamera on the Battle Engine: 0x00418cd0 returns
        // thing->mPos verbatim when THING_TYPE_BATTLE_ENGINE (bit 3 of
        // thing+0x34) is set, adding the bounding-box lift only for other
        // things. The cockpit's own render-thing position provider,
        // 0x00425430, returns battleEngine->mPos + the cockpit shake offset,
        // interpolated against the previous frame. With no shake the cockpit
        // mesh origin therefore coincides exactly with the camera, and the
        // orientation provider 0x004254f0 composes the shake matrix onto the
        // Battle Engine orientation the camera also uses. There is no authored
        // camera-node offset in the attach path at all.
        Vector3.Zero,
        false,
        [
            new AnimationMode("walktofly", 26, 50, 24, 1f / 24f),
            new AnimationMode("walk", 25, 25, 1, 1f),
            new AnimationMode("flytowalk", 1, 25, 24, 1f / 24f),
            new AnimationMode("fly", 0, 0, 1, 1f),
        ],
        // D3DTOP_MODULATE, on two independent measurements.
        //
        // Bytes: stage-zero D3DTSS_COLOROP, read from the texture-stage-state
        // shadow at 0x008557f4 inside the cockpit render wrapper
        // (0x0053bb50 .. its one return at 0x0053ec6f), is 4 = D3DTOP_MODULATE
        // on all SEVEN draw batches. It is read 16 times inside that window --
        // the block dump at entry, the seven SetTransform batch markers at
        // 0x00551043, the seven CMeshRenderer::RenderMeshCore entries and the
        // block dump at exit -- and it is 4 every time, with zero stage-zero
        // COLOROP transitions while inside. The last change before entry is
        // 5 -> 4 written from 0x0055ae02. The batches are, in order,
        // 0=hood, 1=Rsidebit01, 2=Rsidebit02, 3=Lsidebit02, 4=Lsidebit01,
        // 5=Object03, 6=Object01, so there is no per-batch variation to carry.
        //
        // Pixels: over the 31,546-pixel geometric intersection mask on which
        // the same cockpit surface point is visible in both the retail frame
        // and the build, multiplying the build by 0.5 takes clean-mask meanD
        // from 42.51 to 11.42 and compare_capture's material fraction from
        // 98.93% to 25.35%, with 69.9% of pixels then within 8/255 on every
        // channel. Exact 0.5 beats the least-squares scale 0.5249 and beats
        // free per-channel scales (0.478, 0.500, 0.557), the ratio is flat at
        // ~0.52 across nine of ten build-brightness deciles so it is a scale
        // and not a gamma, and it is cockpit-specific rather than exposure:
        // the sky control band's retail/build ratio is 1.006/1.005/1.001 over
        // 13,644 pixels.
        //
        // The batch-3 Lsidebit02 "residual" (366 px, 1.2% of that mask) that
        // read as wanting the doubling kept is CLOSED, and it never wanted it:
        // those 366 samples are not cockpit pixels in either image, so no
        // per-part treatment is warranted and none is made here.
        //
        // Measured, in local-lab/COCKPIT-SIDEBIT-RESIDUAL-2026-07-26.md:
        //   * Build side. Halving this material's stage-zero gain moved every
        //     positive-determinant batch's own footprint by a factor of
        //     1.79-2.24 (hood 1.91, Rsidebit02 2.04, Lsidebit01 2.24,
        //     Object03 2.17, Object01 2.12). Over Lsidebit02's 248-px
        //     footprint the mean moved by 1.000/1.002/1.001 and 71.4% of the
        //     pixels are byte-identical, in a frame where 34% of all pixels
        //     changed. A pixel invariant under a change to this material is
        //     not written by this material.
        //   * Retail side. 91.3% of the 366 samples fall inside the
        //     lower-left instrument's 128x128 blit rect, whose ring
        //     FirstFlightHud.cs:18-19 already fits at centre (66.01, 417.25)
        //     r=46.56 from the retail frame itself. They read
        //     (119.9, 123.5, 132.7): 3.3-7.2x brighter than every other
        //     retail cockpit part (16.7-36.2) and near-neutral at B/R 1.11
        //     where the other six run 1.50-2.54. Retail composites that pass
        //     over the cockpit -- run3/cdb.log logs 11 further stage-0
        //     COLOROP writes after LEAVE cockpit, from return addresses
        //     0x00482150 (x9), 0x0048570c, 0x0053acc6 and 0x0053b21b.
        //
        // Batch 1, Rsidebit01, behaves the same way (footprint mean moved
        // 0.998/0.993/0.981) and is likewise excluded rather than fitted.
        // The intersection mask rasters cockpit geometry only and has no
        // occlusion test against the HUD pass, which is what admitted them.
        RetailStageZeroColorOperation.Modulate);
    private static readonly LegDefinition[] s_legs =
    [
        new(0, 18, 25, [18, 21, 22, 23, 24]),
        new(1, 28, 34, [28, 30, 31, 32, 33]),
        new(2, 46, 55, [46, 51, 52, 53, 54]),
        new(3, 3, 12, [3, 8, 9, 10, 11]),
    ];

    private readonly Part[] _parts;
    private readonly Node3D[] _partNodes;
    private readonly float[][] _legFrameLengths;
    private readonly AssetProfile _profile;

    private RetailAquilaWalkerAsset(
        Part[] parts,
        Node3D[] partNodes,
        Node3D root,
        float[][] legFrameLengths,
        float standingClearance,
        int surfaceCount,
        int animatedPartCount,
        AssetProfile profile)
    {
        _parts = parts;
        _partNodes = partNodes;
        _profile = profile;
        Root = root;
        _legFrameLengths = legFrameLengths;
        SurfaceCount = surfaceCount;
        AnimatedPartCount = animatedPartCount;
        StandingClearance = standingClearance;
        if (profile.IsWalker)
        {
            SetStandingPose();
        }
        else
        {
            SetVirtualFrame(profile.InitialFrame);
            Root.Position = profile.RootOffset;
        }
    }

    public Node3D Root { get; }

    public int PartCount => _parts.Length;

    public int SurfaceCount { get; }

    public int AnimatedPartCount { get; }

    public float StandingClearance { get; }

    public static RetailAquilaWalkerAsset Load(
        string resourcePath,
        IReadOnlyDictionary<int, Texture2D> textures,
        Level100HeightFieldAsset terrain)
        => LoadExact(s_walkerProfile, resourcePath, textures, terrain, null);

    public static RetailAquilaWalkerAsset LoadJet(
        string resourcePath,
        IReadOnlyDictionary<int, Texture2D> textures,
        Level100HeightFieldAsset terrain)
        => LoadExact(s_jetProfile, resourcePath, textures, terrain, null);

    public static RetailAquilaWalkerAsset LoadCockpit(
        string resourcePath,
        IReadOnlyDictionary<int, Texture2D> textures,
        IReadOnlyDictionary<string, Material> materialOverrides,
        Level100HeightFieldAsset terrain)
        => LoadExact(s_cockpitProfile, resourcePath, textures, terrain, materialOverrides);

    private static RetailAquilaWalkerAsset LoadExact(
        AssetProfile profile,
        string resourcePath,
        IReadOnlyDictionary<int, Texture2D> textures,
        Level100HeightFieldAsset terrain,
        IReadOnlyDictionary<string, Material>? materialOverrides)
    {
        byte[] source = Godot.FileAccess.GetFileAsBytes(resourcePath);
        string hash = Convert.ToHexString(SHA256.HashData(source));
        if (source.Length != profile.SourceLength ||
            !string.Equals(hash, profile.SourceSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The retained {profile.DisplayName} source does not match its reviewed specimen.");
        }

        byte[] data = InflateAya(source);
        if (data.Length != profile.InflatedLength)
        {
            throw new InvalidDataException($"The retained {profile.DisplayName} has an unexpected decoded length.");
        }

        ParsedAsset parsed = ParseExactAsset(data, profile);
        if (parsed.Parts.Length != profile.PartCount ||
            parsed.ExpandedSurfaceCount != profile.ExpandedSurfaceCount ||
            parsed.Parts.Count(part => part.HorizontalFrameCount > 1) != profile.AnimatedPartCount)
        {
            throw new InvalidDataException($"The retained {profile.DisplayName} does not match its reviewed hierarchy.");
        }

        ResolveAndValidateHierarchy(parsed.Parts, profile.BasePoseValidationFrame, profile.InitialFrame, profile.DisplayName);
        float[][] legFrameLengths = profile.IsWalker ? BuildLegFrameLengths(parsed.Parts) : [];
        float standingClearance = profile.IsWalker ? BuildStandingClearance(parsed.Parts) : 0f;
        Node3D[] partNodes = BuildPartNodes(
            parsed,
            textures,
            terrain,
            profile,
            materialOverrides,
            out Node3D root);
        return new RetailAquilaWalkerAsset(
            parsed.Parts,
            partNodes,
            root,
            legFrameLengths,
            standingClearance,
            parsed.ExpandedSurfaceCount,
            profile.AnimatedPartCount,
            profile);
    }

    public void SetVirtualFrame(float virtualFrame)
    {
        if (_profile.IsWalker)
        {
            throw new InvalidOperationException("The walker hierarchy is driven by its grounded leg pose.");
        }
        for (int index = 0; index < _parts.Length; index++)
        {
            _partNodes[index].Transform = ToGodotTransform(_parts[index].GetTransform(virtualFrame));
        }
    }

    public void SetGroundContactPose(IReadOnlyList<Vector3> contactsInPlayerSpace)
    {
        ArgumentNullException.ThrowIfNull(contactsInPlayerSpace);
        if (contactsInPlayerSpace.Count != s_legs.Length)
        {
            throw new ArgumentException(
                "The retained Aquila walker requires exactly four foot contacts.",
                nameof(contactsInPlayerSpace));
        }

        SetStandingPose();
        foreach (LegDefinition leg in s_legs)
        {
            Vector3 target = contactsInPlayerSpace[leg.Id] - Root.Position;
            Transform3D[] standingGlobals = BuildCurrentGlobalTransforms();
            Vector3 standingAnchor = standingGlobals[leg.RootPart].Origin;
            int frame = FindClosestLegFrame(
                leg.Id,
                standingAnchor.DistanceTo(target));

            foreach (int partIndex in leg.AnimatedParts)
            {
                _partNodes[partIndex].Transform =
                    ToGodotTransform(_parts[partIndex].GetTransform(frame));
            }

            Transform3D[] posedGlobals = BuildCurrentGlobalTransforms();
            Transform3D rootGlobal = posedGlobals[leg.RootPart];
            Vector3 source = posedGlobals[leg.FootPart].Origin - rootGlobal.Origin;
            Vector3 desired = target - rootGlobal.Origin;
            if (source.LengthSquared() <= 0.000001f ||
                desired.LengthSquared() <= 0.000001f)
            {
                continue;
            }

            var correction = new Basis(new Quaternion(
                source.Normalized(),
                desired.Normalized()));
            rootGlobal.Basis = correction * rootGlobal.Basis;
            _partNodes[leg.RootPart].Transform = _parts[leg.RootPart].Parent is int parent
                ? posedGlobals[parent].AffineInverse() * rootGlobal
                : rootGlobal;
        }
    }

    private void SetStandingPose()
    {
        for (int index = 0; index < _parts.Length; index++)
        {
            _partNodes[index].Transform =
                ToGodotTransform(_parts[index].GetStandingTransform());
        }

        Root.Position = new Vector3(0f, StandingClearance, 0f);
    }

    private Transform3D[] BuildCurrentGlobalTransforms()
    {
        var globals = new Transform3D[_parts.Length];
        for (int index = 0; index < _parts.Length; index++)
        {
            globals[index] = _parts[index].Parent is int parent
                ? globals[parent] * _partNodes[index].Transform
                : _partNodes[index].Transform;
        }
        return globals;
    }

    private int FindClosestLegFrame(int legId, float desiredLength)
    {
        int closestFrame = ExpectedLegMotionStart;
        float closestDelta = float.PositiveInfinity;
        for (int frame = ExpectedLegMotionStart; frame <= ExpectedLegMotionEnd; frame++)
        {
            float delta = Math.Abs(_legFrameLengths[legId][frame] - desiredLength);
            if (delta < closestDelta)
            {
                closestDelta = delta;
                closestFrame = frame;
            }
        }
        return closestFrame;
    }

    private static byte[] InflateAya(byte[] source)
    {
        using var output = new MemoryStream(ExpectedInflatedLength);
        int position = 0;
        byte[] buffer = new byte[16 * 1024];
        int records = 0;
        while (position < source.Length)
        {
            if (source.Length - position < sizeof(uint))
            {
                throw new InvalidDataException("The retained Aquila walker has a truncated AYA record.");
            }

            uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(
                source.AsSpan(position, sizeof(uint)));
            position += sizeof(uint);
            if (declaredLength is 0 or > int.MaxValue || declaredLength > source.Length - position)
            {
                throw new InvalidDataException("The retained Aquila walker has invalid AYA framing.");
            }

            int compressedLength = checked((int)declaredLength);
            using var compressed = new MemoryStream(
                source,
                position,
                compressedLength,
                writable: false,
                publiclyVisible: false);
            using (var inflater = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true))
            {
                int read;
                while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > MaximumInflatedLength)
                    {
                        throw new InvalidDataException("The retained Aquila walker exceeds its decoded-size limit.");
                    }
                    output.Write(buffer, 0, read);
                }
            }

            if (compressed.Position != compressed.Length)
            {
                throw new InvalidDataException("The retained Aquila walker has trailing compressed data.");
            }

            position += compressedLength;
            records++;
        }

        if (records == 0)
        {
            throw new InvalidDataException("The retained Aquila walker contains no AYA records.");
        }
        return output.ToArray();
    }

    private static ParsedAsset ParseExactAsset(byte[] data, AssetProfile profile)
    {
        int cursor = 0;
        Chunk cmsh = ReadChunk(data, ref cursor, data.Length, "CMSH", 372);
        int textureCount = ReadInt32(data, cmsh.PayloadOffset + 4);
        int partCount = ReadInt32(data, cmsh.PayloadOffset + 0x15C);
        if (textureCount != profile.TextureCount || partCount != profile.PartCount)
        {
            throw new InvalidDataException($"The retained {profile.DisplayName} has unexpected CMSH counts.");
        }

        _ = ReadChunk(data, ref cursor, data.Length, "CMST", textureCount * 36);
        var textures = new TextureMetadata[textureCount];
        for (int texture = 0; texture < textureCount; texture++)
        {
            Chunk msht = ReadChunk(data, ref cursor, data.Length, "MSHT", 156);
            int nested = msht.PayloadOffset;
            Chunk texb = ReadChunk(data, ref nested, msht.EndOffset, "TEXB", 148);
            textures[texture] = new TextureMetadata(
                ReadSingle(data, texb.PayloadOffset),
                new Vector2(
                    ReadSingle(data, texb.PayloadOffset + 4),
                    ReadSingle(data, texb.PayloadOffset + 8)),
                new Vector2(
                    ReadSingle(data, texb.PayloadOffset + 12),
                    ReadSingle(data, texb.PayloadOffset + 16)));
            RequireEnd(nested, msht.EndOffset, "MSHT");
        }

        var parts = new Part[partCount];
        int expandedSurfaceCount = 0;
        for (int index = 0; index < partCount; index++)
        {
            Chunk mesp = ReadChunk(data, ref cursor, data.Length, "MESP");
            parts[index] = ParsePart(data, mesp, index, partCount, profile.DisplayName);
        }

        foreach (Part part in parts)
        {
            Geometry? geometry = part.Geometry;
            if (geometry is null && part.Reference is int reference)
            {
                if (reference >= part.Index || parts[reference].Reference is not null)
                {
                    throw new InvalidDataException($"The retained {profile.DisplayName} has an unsupported geometry reference.");
                }
                geometry = parts[reference].Geometry;
                part.Geometry = geometry ?? throw new InvalidDataException(
                    $"The retained {profile.DisplayName} references an empty geometry part.");
            }
            expandedSurfaceCount += geometry?.Groups.Length ?? 0;
        }

        Chunk camd = ReadChunk(data, ref cursor, data.Length, "CAMD", profile.AnimationModes.Length * 36);
        ValidateAnimations(data, camd, profile);
        _ = ReadChunk(data, ref cursor, data.Length, "BBOX", 48);
        _ = ReadChunk(data, ref cursor, data.Length, "CEMT", profile.CemtLength);
        RequireEnd(cursor, data.Length, "CMSH");
        return new ParsedAsset(parts, textures, expandedSurfaceCount);
    }

    private static Part ParsePart(
        byte[] data,
        Chunk mesp,
        int index,
        int partCount,
        string displayName)
    {
        int cursor = mesp.PayloadOffset;
        Chunk cmsp = ReadChunk(data, ref cursor, mesp.EndOffset, "CMSP", 316);
        int number = ReadInt32(data, cmsp.PayloadOffset + 0x88);
        int childCount = ReadInt32(data, cmsp.PayloadOffset + 0x90);
        int virtualFrameCount = ReadInt32(data, cmsp.PayloadOffset + 0xB8);
        int horizontalFrameCount = ReadInt32(data, cmsp.PayloadOffset + 0xBC);
        if (number != index || childCount is < 0 || childCount > partCount ||
            virtualFrameCount is < 1 or > 101 || horizontalFrameCount is < 1 or > 101)
        {
            throw new InvalidDataException($"The retained {displayName} has invalid part metadata.");
        }

        string name = ReadFixedString(data, cmsp.PayloadOffset + 0xDC, 32);
        var part = new Part(
            index,
            name,
            virtualFrameCount,
            horizontalFrameCount,
            ReadBeaTransform(data, cmsp.PayloadOffset + 0x30, cmsp.PayloadOffset + 0x70));

        while (cursor < mesp.EndOffset)
        {
            string tag = ReadTag(data, cursor, mesp.EndOffset);
            Chunk chunk = ReadChunk(data, ref cursor, mesp.EndOffset, tag);
            switch (tag)
            {
                case "CHLD":
                    if (chunk.Length != childCount * sizeof(int) || childCount == 0)
                    {
                        throw new InvalidDataException("The retained Aquila walker has invalid child metadata.");
                    }
                    part.Children = ReadInt32Array(data, chunk, childCount, partCount);
                    break;

                case "PRNT":
                    part.Parent = ReadIndex(data, chunk, partCount, "parent");
                    break;

                case "REFR":
                    part.Reference = ReadIndex(data, chunk, partCount, "geometry reference");
                    break;

                case "VHFM":
                    if (chunk.Length != virtualFrameCount)
                    {
                        throw new InvalidDataException("The retained Aquila walker has an invalid virtual-frame map.");
                    }
                    part.FrameMap = data.AsSpan(chunk.PayloadOffset, chunk.Length).ToArray();
                    if (part.FrameMap.Any(frame => frame >= horizontalFrameCount))
                    {
                        throw new InvalidDataException("The retained Aquila walker maps beyond a stored transform.");
                    }
                    break;

                case "HORI":
                    if (chunk.Length != horizontalFrameCount * 48)
                    {
                        throw new InvalidDataException("The retained Aquila walker has invalid orientation frames.");
                    }
                    part.Orientations = ReadOrientations(data, chunk, horizontalFrameCount);
                    break;

                case "HPOS":
                    if (chunk.Length != horizontalFrameCount * 16)
                    {
                        throw new InvalidDataException("The retained Aquila walker has invalid position frames.");
                    }
                    part.Positions = ReadPositions(data, chunk, horizontalFrameCount);
                    break;

                case "HFOV":
                    if (chunk.Length != horizontalFrameCount * sizeof(float))
                    {
                        throw new InvalidDataException($"The retained {displayName} has invalid field-of-view frames.");
                    }
                    for (int frame = 0; frame < horizontalFrameCount; frame++)
                    {
                        _ = ReadSingle(data, chunk.PayloadOffset + (frame * sizeof(float)));
                    }
                    break;

                case "PMVB":
                    part.Geometry = ParseGeometry(data, chunk, part.Reference is not null);
                    break;

                case "BBOX":
                case "PBKT":
                case "CPOS":
                case "CORI":
                case "NMIC":
                    break;

                default:
                    throw new InvalidDataException($"The retained Aquila walker contains unsupported part data '{tag}'.");
            }
        }

        if (part.Children.Length != childCount || part.FrameMap.Length != virtualFrameCount ||
            part.Orientations.Length != horizontalFrameCount || part.Positions.Length != horizontalFrameCount)
        {
            throw new InvalidDataException("The retained Aquila walker part is incomplete.");
        }
        return part;
    }

    private static Geometry? ParseGeometry(byte[] data, Chunk pmvb, bool isReference)
    {
        int cursor = pmvb.PayloadOffset;
        Chunk cmvb = ReadChunk(data, ref cursor, pmvb.EndOffset, "CMVB", 296);
        int groupCount = data[cmvb.PayloadOffset + 264];
        int stride = ReadInt32(data, cmvb.PayloadOffset + 276);
        int fvf = ReadInt32(data, cmvb.PayloadOffset + 280);
        int topology = ReadInt32(data, cmvb.PayloadOffset + 284);
        if (isReference)
        {
            // A zero-group reference owns no vertex stream. Released meshes
            // leave the otherwise-unused stride/FVF/topology words populated
            // with non-semantic data; the earlier reviewed geometry owner is
            // the complete source for this part.
            if (groupCount != 0)
            {
                throw new InvalidDataException("The retained Aquila walker has an invalid referenced geometry source.");
            }
            RequireEnd(cursor, pmvb.EndOffset, "referenced PMVB");
            return null;
        }
        if (groupCount is < 0 or > 12 || (groupCount > 0 && (stride != 36 || fvf != 0x152 || topology != 4)))
        {
            throw new InvalidDataException("The retained Aquila walker has an unsupported geometry profile.");
        }
        if (groupCount == 0)
        {
            RequireEnd(cursor, pmvb.EndOffset, "empty PMVB");
            return null;
        }

        Vector3[]? vertices = null;
        Vector3[]? normals = null;
        Vector2[]? textureCoordinates = null;
        Color[]? colors = null;
        var groups = new GeometryGroup[groupCount];
        int declaredVertexBytes = -1;
        int declaredVertexCount = -1;
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            Chunk mmpt = ReadChunk(data, ref cursor, pmvb.EndOffset, "MMPT", 24);
            int vertexBytes = ReadInt32(data, mmpt.PayloadOffset);
            int indexBytes = ReadInt32(data, mmpt.PayloadOffset + 4);
            int indexCount = ReadInt32(data, mmpt.PayloadOffset + 8);
            int vertexCount = ReadInt32(data, mmpt.PayloadOffset + 12);
            int primitiveCount = ReadInt32(data, mmpt.PayloadOffset + 16);
            int active = ReadInt32(data, mmpt.PayloadOffset + 20);
            if (active != 1 || indexCount < 3 || indexBytes != indexCount * sizeof(ushort) ||
                primitiveCount != indexCount - 2 || vertexCount is < 1 or > 100_000 ||
                vertexBytes != vertexCount * 36)
            {
                throw new InvalidDataException("The retained Aquila walker has invalid material-group metadata.");
            }
            if (groupIndex == 0)
            {
                declaredVertexBytes = vertexBytes;
                declaredVertexCount = vertexCount;
            }
            else if (vertexBytes != declaredVertexBytes || vertexCount != declaredVertexCount)
            {
                throw new InvalidDataException("The retained Aquila walker has inconsistent shared vertices.");
            }

            Chunk ibuf = ReadChunk(data, ref cursor, pmvb.EndOffset, "IBUF", indexBytes);
            Chunk vbuf = ReadChunk(data, ref cursor, pmvb.EndOffset, "VBUF", groupIndex == 0 ? vertexBytes : 0);
            Chunk texr = ReadChunk(data, ref cursor, pmvb.EndOffset, "TEXR", 24);
            if (groupIndex == 0)
            {
                (vertices, normals, textureCoordinates, colors) = ReadVertices(data, vbuf, vertexCount);
            }

            int[] strip = ReadIndices(data, ibuf, indexCount, vertexCount);
            groups[groupIndex] = new GeometryGroup(
                BuildTriangles(strip),
                Enumerable.Range(0, 6)
                    .Select(layer => ReadInt32(data, texr.PayloadOffset + (layer * sizeof(int))))
                    .ToArray());
        }
        RequireEnd(cursor, pmvb.EndOffset, "PMVB");
        return new Geometry(
            vertices ?? throw new InvalidDataException("The retained Aquila walker has no vertices."),
            normals ?? throw new InvalidDataException("The retained Aquila walker has no normals."),
            textureCoordinates ?? throw new InvalidDataException("The retained Aquila walker has no UVs."),
            colors ?? throw new InvalidDataException("The retained Aquila walker has no vertex colours."),
            groups);
    }

    private static (Vector3[] Vertices, Vector3[] Normals, Vector2[] TextureCoordinates, Color[] Colors) ReadVertices(
        byte[] data,
        Chunk vbuf,
        int count)
    {
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var textureCoordinates = new Vector2[count];
        var colors = new Color[count];
        for (int index = 0; index < count; index++)
        {
            int offset = vbuf.PayloadOffset + (index * 36);
            Vector3 position = ReadVector3(data, offset);
            Vector3 normal = ReadVector3(data, offset + 12);
            Vector2 uv = new(ReadSingle(data, offset + 28), ReadSingle(data, offset + 32));
            if (!position.IsFinite() || !normal.IsFinite() || !uv.IsFinite())
            {
                throw new InvalidDataException("The retained Aquila walker has a non-finite vertex.");
            }
            vertices[index] = MapVector(position);
            normals[index] = MapVector(normal).Normalized();
            textureCoordinates[index] = uv;
            colors[index] = ReadDiffuse(data, offset + 24);
        }
        return (vertices, normals, textureCoordinates, colors);
    }

    /// <summary>
    /// Reads the D3DCOLOR DIFFUSE dword that stride-36 FVF 0x152
    /// (XYZ|NORMAL|DIFFUSE|TEX1 = 12+12+4+8) carries at vertex offset 24.
    /// </summary>
    /// <remarks>
    /// Retail consumes this. <c>D3DStateCache__UseDefaultRenderState</c> at
    /// 0x004EB1E0 sets <c>D3DRS_LIGHTING</c> TRUE with both
    /// <c>D3DRS_DIFFUSEMATERIALSOURCE</c> and
    /// <c>D3DRS_AMBIENTMATERIALSOURCE</c> at <c>D3DMCS_COLOR1</c>, and stage
    /// zero to <c>COLORARG1 = D3DTA_TEXTURE</c> / <c>COLORARG2 =
    /// D3DTA_DIFFUSE</c>. <c>D3DRS_COLORVERTEX</c> (<c>0x8D</c>, NOT 60 - 60 is TEXTUREFACTOR and IS
    /// written) is written at none of the
    /// 490 render-state call sites, so it keeps its TRUE D3D9 default.
    /// The base mesh pass runs lit, now measured rather than inferred:
    /// <c>D3DRS_LIGHTING</c>, read from the render-state shadow at
    /// <c>0x00855764</c> (the array at <c>0x00855540</c> indexed
    /// <c>state*4</c>, per the caching setter at <c>0x00513bc8</c>), is
    /// <c>1</c> at all 576 world and tree mesh draws of a Level 100 frame.
    /// <c>CMeshRenderer__RenderMeshCore</c> does clear lighting around its
    /// mode-2 and mode-6 branches, but those branches are dead in Level 100:
    /// breakpoints on their draw calls at <c>0x0054a423</c> and
    /// <c>0x0054a466</c> never fired across 4,393 observed mesh renders.
    /// The shared <c>RetailFixedFunctionMaterial</c> shader already multiplies
    /// its lit vertex colour by <c>COLOR.rgb</c>.
    /// </remarks>
    private static Color ReadDiffuse(byte[] data, int offset)
    {
        if (offset < 0 || offset + sizeof(uint) > data.Length)
        {
            throw new InvalidDataException("The retained Aquila walker ended before a vertex diffuse dword.");
        }
        uint diffuse = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
        if ((diffuse >> 24) != 0xFF)
        {
            // Stage zero also runs ALPHAOP MODULATE against D3DTA_DIFFUSE, so a
            // non-opaque dword would be a real alpha term rather than padding.
            // Every vertex of all three retained specimens is alpha 255; refuse
            // rather than silently drop an alpha this path does not implement.
            throw new InvalidDataException("The retained Aquila walker has a non-opaque vertex diffuse alpha.");
        }
        return new Color(
            ((diffuse >> 16) & 0xFF) / 255f,
            ((diffuse >> 8) & 0xFF) / 255f,
            (diffuse & 0xFF) / 255f,
            1f);
    }

    private static int[] ReadIndices(byte[] data, Chunk ibuf, int count, int vertexCount)
    {
        var indices = new int[count];
        for (int index = 0; index < count; index++)
        {
            indices[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                data.AsSpan(ibuf.PayloadOffset + (index * sizeof(ushort)), sizeof(ushort)));
            if (indices[index] >= vertexCount)
            {
                throw new InvalidDataException("The retained Aquila walker contains an out-of-range index.");
            }
        }
        return indices;
    }

    private static int[] BuildTriangles(int[] strip)
    {
        var triangles = new List<int>((strip.Length - 2) * 3);
        for (int ordinal = 0; ordinal < strip.Length - 2; ordinal++)
        {
            int a;
            int b;
            int c = strip[ordinal + 2];
            if ((ordinal & 1) == 0)
            {
                a = strip[ordinal];
                b = strip[ordinal + 1];
            }
            else
            {
                b = strip[ordinal];
                a = strip[ordinal + 1];
            }
            if (a == b || b == c || a == c)
            {
                continue;
            }

            // MapVector's reflection already changes BEA's strip winding to the
            // clockwise front-face order required by Godot ArrayMesh.
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
        if (triangles.Count == 0)
        {
            throw new InvalidDataException("The retained Aquila walker contains an empty triangle strip.");
        }
        return triangles.ToArray();
    }

    private static void ResolveAndValidateHierarchy(
        Part[] parts,
        float? basePoseValidationFrame,
        float initialFrame,
        string displayName)
    {
        int[] claimedParents = Enumerable.Repeat(-1, parts.Length).ToArray();
        foreach (Part parent in parts)
        {
            foreach (int child in parent.Children)
            {
                if (claimedParents[child] != -1)
                {
                    throw new InvalidDataException("The retained Aquila walker has a multiply-owned part.");
                }
                claimedParents[child] = parent.Index;
            }
        }

        int rootCount = 0;
        var globals = new BeaTransform[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            Part part = parts[index];
            if (part.Parent is null)
            {
                rootCount++;
                if (claimedParents[index] != -1)
                {
                    throw new InvalidDataException("The retained Aquila walker root is also claimed as a child.");
                }
                if (basePoseValidationFrame is float frame)
                {
                    globals[index] = part.GetTransform(frame);
                }
            }
            else
            {
                int parent = part.Parent.Value;
                if (parent >= index || claimedParents[index] != parent)
                {
                    throw new InvalidDataException("The retained Aquila walker hierarchy is not reciprocal.");
                }
                if (basePoseValidationFrame is float frame)
                {
                    globals[index] = BeaTransform.Compose(
                        globals[parent],
                        part.GetTransform(frame));
                }
            }

            if (basePoseValidationFrame is float validationFrame &&
                !globals[index].ApproximatelyEquals(part.BaseGlobalTransform, TransformTolerance))
            {
                throw new InvalidDataException(
                    $"The retained {displayName} authored hierarchy is inconsistent at part {index} '{part.Name}' for frame {validationFrame}.");
            }
        }
        if (rootCount != 1)
        {
            throw new InvalidDataException("The retained Aquila walker must have one hierarchy root.");
        }

        // Steam's animated renderer recursively composes the VHFM-mapped
        // HPOS/HORI transform through the parent pointer. The serialized base
        // transform happens to match the reviewed walker pose, but is not an
        // animated-frame invariant for the jet or cockpit hierarchies.
        if (BuildGlobalTransforms(parts, initialFrame).Any(transform => !transform.IsFinite))
        {
            throw new InvalidDataException($"The retained {displayName} has an invalid composed pose.");
        }
    }

    private static float[][] BuildLegFrameLengths(Part[] parts)
    {
        var result = new float[s_legs.Length][];
        foreach (LegDefinition leg in s_legs)
        {
            result[leg.Id] = new float[ExpectedLegMotionEnd + 1];
        }

        for (int frame = ExpectedLegMotionStart; frame <= ExpectedLegMotionEnd; frame++)
        {
            BeaTransform[] globals = BuildGlobalTransforms(parts, frame);
            foreach (LegDefinition leg in s_legs)
            {
                float length = globals[leg.RootPart].Position.DistanceTo(
                    globals[leg.FootPart].Position);
                if (!float.IsFinite(length) || length <= 0f)
                {
                    throw new InvalidDataException(
                        "The retained Aquila walker has an invalid LegMotion extension.");
                }
                result[leg.Id][frame] = length;
            }
        }
        return result;
    }

    private static BeaTransform[] BuildGlobalTransforms(Part[] parts, float virtualFrame)
    {
        var globals = new BeaTransform[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            BeaTransform local = parts[index].GetTransform(virtualFrame);
            globals[index] = parts[index].Parent is int parent
                ? BeaTransform.Compose(globals[parent], local)
                : local;
        }
        return globals;
    }

    private static float BuildStandingClearance(Part[] parts) =>
        BuildClearance(parts, ExpectedLegMotionStart, useObservedStandingPose: true);

    private static bool TryGetObservedLevel100StandingTransform(
        int partIndex,
        out BeaTransform transform)
    {
        // Canonical Steam specimen, fresh AppCore-owned Level 100 copy, raw
        // walker state 2 at the exact authored spawn. Two read-only samples
        // 100 ms apart produced the same complete pose-buffer SHA-256:
        // 53FD05BBFCF2E72B9AFBF7E9EC120DDFD5449EC2ED00A79D3A94B2069A4E5465.
        // The actor root was removed from these twenty local leg-chain poses.
        transform = partIndex switch
        {
            3 => StandingPose(
                0.761326793f, 0.359408711f, 0.539635826f,
                0.623082613f, -0.635733713f, -0.455642889f,
                0.179302603f, 0.683130860f, -0.707942486f,
                0.264985863f, -0.240135936f, 0.192155838f),
            8 => StandingPose(
                0.999999881f, -0.000000067f, 0.000000089f,
                0f, 0.853719029f, 0.520733486f,
                0.000000124f, -0.520733462f, 0.853719112f,
                0.016658484f, 0.667089011f, 0.081205942f),
            9 => StandingPose(
                0.999999881f, 0f, 0.000000129f,
                -0.000000067f, 0.853718584f, -0.520734130f,
                0.000000089f, 0.520734163f, 0.853718716f,
                0.002022146f, -0.316406124f, 0.092081674f),
            10 => StandingPose(
                0.999999879f, 0.000000059f, 0.000000107f,
                0f, 0.835001183f, -0.550247591f,
                0.000000140f, 0.550247746f, 0.835001243f,
                -0.000014044f, 0.526826375f, 0.077583322f),
            11 => StandingPose(
                0.999999876f, -0.000000101f, 0.000000091f,
                0.000000056f, 0.349686880f, 0.936866403f,
                0.000000122f, -0.936866271f, 0.349687003f,
                -0.004456563f, 0.365370782f, -0.045191237f),
            18 => StandingPose(
                -0.815748495f, -0.450529976f, -0.362735548f,
                -0.578401559f, 0.632749656f, 0.514858875f,
                -0.002438605f, 0.629801989f, -0.776751876f,
                -0.264029511f, 0.235405520f, 0.194670677f),
            21 => StandingPose(
                0.999999947f, -0.000000081f, -0.000000069f,
                -0.000000070f, 0.911006247f, 0.412392461f,
                0f, -0.412392326f, 0.911006596f,
                0.015948282f, 0.667869326f, 0.081984562f),
            22 => StandingPose(
                0.999999947f, -0.000000088f, 0f,
                -0.000000087f, 0.907372543f, -0.420327126f,
                -0.000000066f, 0.420327208f, 0.907372909f,
                0.002016941f, -0.331826637f, 0.096348314f),
            23 => StandingPose(
                0.999999947f, -0.000000101f, 0f,
                -0.000000095f, 0.813683381f, -0.581308174f,
                0f, 0.581308538f, 0.813683546f,
                -0.000009314f, 0.542245624f, 0.073320433f),
            24 => StandingPose(
                0.999999947f, 0f, -0.000000082f,
                -0.000000101f, 0.090553397f, 0.995891700f,
                0f, -0.995891584f, 0.090553774f,
                -0.003310197f, 0.294647794f, -0.061619548f),
            28 => StandingPose(
                0.732616259f, 0.507456906f, -0.453608418f,
                -0.664665069f, 0.676933473f, -0.316197685f,
                0.146605998f, 0.533149362f, 0.833221555f,
                0.264990639f, 0.234743025f, 0.190869331f),
            30 => StandingPose(
                0.999999587f, 0f, -0.000000087f,
                0f, 0.953800761f, -0.300438797f,
                -0.000000072f, 0.300438816f, 0.953800794f,
                0.016667433f, 0.668478490f, -0.081851848f),
            31 => StandingPose(
                0.999999587f, 0f, -0.000000063f,
                0f, 0.953800975f, 0.300438154f,
                -0.000000087f, -0.300438115f, 0.953801019f,
                0.002014266f, -0.329448259f, -0.107830470f),
            32 => StandingPose(
                0.999999587f, 0.000000058f, 0f,
                0f, 0.832961669f, 0.553330163f,
                -0.000000063f, -0.553330172f, 0.832961735f,
                -0.000025600f, 0.539871676f, -0.061838969f),
            33 => StandingPose(
                0.999999577f, 0f, 0f,
                0.000000073f, 0.013604437f, -0.999907166f,
                -0.000000068f, 0.999907204f, 0.013604478f,
                -0.000996739f, 0.312797201f, 0.042775920f),
            46 => StandingPose(
                0.695770255f, -0.341931611f, -0.631653668f,
                -0.664584183f, -0.640052036f, -0.385565466f,
                -0.272454292f, 0.688052118f, -0.672571540f,
                -0.262623694f, -0.240139779f, 0.192155838f),
            51 => StandingPose(
                0.999999753f, -0.000000074f, -0.000000182f,
                -0.000000131f, 0.853718887f, 0.520733426f,
                -0.000000126f, -0.520733362f, 0.853718923f,
                0.016676005f, 0.667087769f, 0.081200642f),
            52 => StandingPose(
                0.999999750f, -0.000000171f, -0.000000135f,
                -0.000000078f, 0.853718513f, -0.520734095f,
                -0.000000196f, 0.520734065f, 0.853718507f,
                0.002017372f, -0.316407125f, 0.092092830f),
            53 => StandingPose(
                0.999999750f, -0.000000254f, 0f,
                -0.000000171f, 0.835000958f, -0.550247735f,
                -0.000000135f, 0.550247673f, 0.835001097f,
                -0.000001134f, 0.526826393f, 0.077580618f),
            54 => StandingPose(
                0.999999753f, -0.000000078f, -0.000000241f,
                -0.000000240f, 0.349686857f, 0.936866195f,
                0f, -0.936866345f, 0.349686983f,
                -0.004460498f, 0.365365977f, -0.045196509f),
            _ => default,
        };
        return partIndex is 3 or 8 or 9 or 10 or 11 or 18 or 21 or 22 or 23 or 24 or
            28 or 30 or 31 or 32 or 33 or 46 or 51 or 52 or 53 or 54;
    }

    private static BeaTransform StandingPose(
        float m00, float m01, float m02,
        float m10, float m11, float m12,
        float m20, float m21, float m22,
        float x, float y, float z) =>
        new(new Matrix3(m00, m01, m02, m10, m11, m12, m20, m21, m22), new Vector3(x, y, z));

    private static float BuildClearance(Part[] parts, float virtualFrame, bool useObservedStandingPose)
    {
        var globals = new BeaTransform[parts.Length];
        float lowestY = float.PositiveInfinity;
        for (int index = 0; index < parts.Length; index++)
        {
            Part part = parts[index];
            BeaTransform local = useObservedStandingPose
                ? part.GetStandingTransform()
                : part.GetTransform(virtualFrame);
            globals[index] = part.Parent is int parent
                ? BeaTransform.Compose(globals[parent], local)
                : local;
            if (part.Geometry is null)
            {
                continue;
            }

            foreach (Vector3 mappedVertex in part.Geometry.Vertices)
            {
                // Geometry vertices have already been mapped; convert back for the
                // BEA transform, then map the resulting global point.
                Vector3 beaVertex = MapVector(mappedVertex);
                float y = MapVector(globals[index].TransformPoint(beaVertex)).Y;
                lowestY = Math.Min(lowestY, y);
            }
        }
        if (!float.IsFinite(lowestY))
        {
            throw new InvalidDataException("The retained Aquila walker has no bounded geometry.");
        }
        return -lowestY;
    }

    private static Node3D[] BuildPartNodes(
        ParsedAsset parsed,
        IReadOnlyDictionary<int, Texture2D> textures,
        Level100HeightFieldAsset terrain,
        AssetProfile profile,
        IReadOnlyDictionary<string, Material>? materialOverrides,
        out Node3D root)
    {
        Part[] parts = parsed.Parts;
        root = new Node3D { Name = profile.RootName };
        var nodes = new Node3D[parts.Length];
        var meshByGeometry = new Dictionary<Geometry, ArrayMesh>();
        var materialBySignature = new Dictionary<string, Material>(StringComparer.Ordinal);
        for (int index = 0; index < parts.Length; index++)
        {
            Part part = parts[index];
            var node = new Node3D
            {
                Name = $"Part{index:D2}-{SanitizeNodeName(part.Name)}",
                Transform = ToGodotTransform(profile.IsWalker
                    ? part.GetStandingTransform()
                    : part.GetTransform(profile.InitialFrame)),
            };
            nodes[index] = node;
            if (part.Parent is int parent)
            {
                nodes[parent].AddChild(node);
            }
            else
            {
                root.AddChild(node);
            }

            if (part.Geometry is not null)
            {
                if (!meshByGeometry.TryGetValue(part.Geometry, out ArrayMesh? mesh))
                {
                    mesh = BuildMesh(
                        part.Geometry,
                        parsed.Textures,
                        textures,
                        terrain,
                        materialBySignature,
                        materialOverrides,
                        profile.StageZeroColorOperation);
                    meshByGeometry.Add(part.Geometry, mesh);
                }
                node.AddChild(new MeshInstance3D
                {
                    Name = "Geometry",
                    Mesh = mesh,
                    CastShadow = profile.CastShadow
                        ? GeometryInstance3D.ShadowCastingSetting.On
                        : GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
        }
        return nodes;
    }

    private static ArrayMesh BuildMesh(
        Geometry geometry,
        IReadOnlyList<TextureMetadata> metadata,
        IReadOnlyDictionary<int, Texture2D> textures,
        Level100HeightFieldAsset terrain,
        IDictionary<string, Material> materialBySignature,
        IReadOnlyDictionary<string, Material>? materialOverrides,
        RetailStageZeroColorOperation stageZeroColorOperation)
    {
        var mesh = new ArrayMesh();
        foreach (GeometryGroup group in geometry.Groups)
        {
            string signature = MaterialSignature(group.TextureIndices);
            if (!materialBySignature.TryGetValue(signature, out Material? material))
            {
                if (materialOverrides is not null &&
                    materialOverrides.TryGetValue(signature, out Material? materialOverride))
                {
                    material = materialOverride;
                }
                else
                {
                    var layers = new RetailTextureLayer?[6];
                    for (int index = 0; index < group.TextureIndices.Length; index++)
                    {
                        int textureIndex = group.TextureIndices[index];
                        if (textureIndex == -1)
                        {
                            continue;
                        }
                        if (textureIndex < 0 || textureIndex >= metadata.Count ||
                            !textures.TryGetValue(textureIndex, out Texture2D? texture))
                        {
                            throw new InvalidDataException(
                                $"The retained Aquila asset references unmapped texture {textureIndex}.");
                        }
                        TextureMetadata textureMetadata = metadata[textureIndex];
                        layers[index] = new RetailTextureLayer(
                            texture,
                            textureMetadata.Opacity,
                            textureMetadata.Offset,
                            textureMetadata.Scale);
                    }
                    material = RetailFixedFunctionMaterial.Create(
                        layers,
                        terrain,
                        stageZeroColorOperation: stageZeroColorOperation);
                }
                materialBySignature.Add(signature, material);
            }

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = geometry.Vertices;
            arrays[(int)Mesh.ArrayType.Normal] = geometry.Normals;
            arrays[(int)Mesh.ArrayType.TexUV] = geometry.TextureCoordinates;
            // The FVF 0x152 DIFFUSE dword, which retail's own render state
            // consumes as stage-zero COLORARG2. See ReadDiffuse.
            arrays[(int)Mesh.ArrayType.Color] = geometry.Colors;
            arrays[(int)Mesh.ArrayType.Index] = group.Triangles;
            int surface = mesh.GetSurfaceCount();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetName(surface, signature);
            mesh.SurfaceSetMaterial(surface, material);
        }
        return mesh;
    }

    private static string MaterialSignature(IReadOnlyList<int> textureIndices)
    {
        if (textureIndices.Count != 6)
        {
            throw new InvalidDataException("The retained Aquila walker has an invalid material signature.");
        }
        return "layers-" + string.Join(
            "-",
            textureIndices.Select(value => unchecked((uint)value).ToString("x8")));
    }

    private static void ValidateAnimations(byte[] data, Chunk camd, AssetProfile profile)
    {
        const int entryLength = 36;
        if (camd.Length != entryLength * profile.AnimationModes.Length)
        {
            throw new InvalidDataException($"The retained {profile.DisplayName} has an unexpected animation table.");
        }
        for (int index = 0; index < profile.AnimationModes.Length; index++)
        {
            int entry = camd.PayloadOffset + (entryLength * index);
            AnimationMode expected = profile.AnimationModes[index];
            string name = ReadFixedString(data, entry, 16);
            int start = ReadInt32(data, entry + 20);
            int end = ReadInt32(data, entry + 24);
            int span = ReadInt32(data, entry + 28);
            float increment = ReadSingle(data, entry + 32);
            if (!string.Equals(name, expected.Name, StringComparison.Ordinal) ||
                start != expected.Start || end != expected.End || span != expected.Span ||
                Math.Abs(increment - expected.Increment) > 0.000001f)
            {
                throw new InvalidDataException(
                    $"The retained {profile.DisplayName} has an unexpected '{expected.Name}' animation range.");
            }
        }
    }

    private static BeaTransform[] ReadOrientations(byte[] data, Chunk chunk, int count)
    {
        var result = new BeaTransform[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = new BeaTransform(
                ReadMatrix(data, chunk.PayloadOffset + (index * 48)),
                Vector3.Zero);
        }
        return result;
    }

    private static Vector3[] ReadPositions(byte[] data, Chunk chunk, int count)
    {
        var result = new Vector3[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = ReadVector3(data, chunk.PayloadOffset + (index * 16));
            if (!result[index].IsFinite())
            {
                throw new InvalidDataException("The retained Aquila walker has a non-finite position frame.");
            }
        }
        return result;
    }

    private static BeaTransform ReadBeaTransform(byte[] data, int orientationOffset, int positionOffset) =>
        new(ReadMatrix(data, orientationOffset), ReadVector3(data, positionOffset));

    private static Matrix3 ReadMatrix(byte[] data, int offset)
    {
        var matrix = new Matrix3(
            ReadSingle(data, offset),
            ReadSingle(data, offset + 4),
            ReadSingle(data, offset + 8),
            ReadSingle(data, offset + 16),
            ReadSingle(data, offset + 20),
            ReadSingle(data, offset + 24),
            ReadSingle(data, offset + 32),
            ReadSingle(data, offset + 36),
            ReadSingle(data, offset + 40));
        if (!matrix.IsFinite)
        {
            throw new InvalidDataException("The retained Aquila walker has a non-finite orientation.");
        }
        return matrix;
    }

    private static Transform3D ToGodotTransform(BeaTransform transform)
    {
        Matrix3 source = transform.Rotation;
        var basis = new Basis(
            new Vector3(source.M00, -source.M20, -source.M10),
            new Vector3(-source.M02, source.M22, source.M12),
            new Vector3(-source.M01, source.M21, source.M11));
        return new Transform3D(basis, MapVector(transform.Position));
    }

    private static Vector3 MapVector(Vector3 value) => new(value.X, -value.Z, -value.Y);

    private static int[] ReadInt32Array(byte[] data, Chunk chunk, int count, int exclusiveLimit)
    {
        var result = new int[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = ReadInt32(data, chunk.PayloadOffset + (index * sizeof(int)));
            if (result[index] < 0 || result[index] >= exclusiveLimit)
            {
                throw new InvalidDataException("The retained Aquila walker contains an out-of-range part index.");
            }
        }
        return result;
    }

    private static int ReadIndex(byte[] data, Chunk chunk, int exclusiveLimit, string role)
    {
        if (chunk.Length != sizeof(int))
        {
            throw new InvalidDataException($"The retained Aquila walker has invalid {role} metadata.");
        }
        int value = ReadInt32(data, chunk.PayloadOffset);
        if (value < 0 || value >= exclusiveLimit)
        {
            throw new InvalidDataException($"The retained Aquila walker has an out-of-range {role}.");
        }
        return value;
    }

    private static Chunk ReadChunk(
        byte[] data,
        ref int cursor,
        int end,
        string expectedTag,
        int? expectedLength = null)
    {
        if (cursor < 0 || end > data.Length || cursor + 8 > end)
        {
            throw new InvalidDataException($"The retained Aquila walker ended before {expectedTag}.");
        }
        string actualTag = ReadTag(data, cursor, end);
        int length = ReadInt32(data, cursor + 4);
        int payloadOffset = cursor + 8;
        int next;
        try
        {
            next = checked(payloadOffset + length);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("The retained Aquila walker has an overflowing chunk length.", exception);
        }
        if (!string.Equals(actualTag, expectedTag, StringComparison.Ordinal) || length < 0 || next > end ||
            (expectedLength is int exactLength && length != exactLength))
        {
            throw new InvalidDataException($"The retained Aquila walker has an invalid {expectedTag} chunk.");
        }
        cursor = next;
        return new Chunk(payloadOffset, length);
    }

    private static string ReadTag(byte[] data, int offset, int end)
    {
        if (offset < 0 || offset + 4 > end || end > data.Length)
        {
            throw new InvalidDataException("The retained Aquila walker ended before a chunk tag.");
        }
        return Encoding.ASCII.GetString(data, offset, 4);
    }

    private static string ReadFixedString(byte[] data, int offset, int length)
    {
        if (offset < 0 || offset + length > data.Length)
        {
            throw new InvalidDataException("The retained Aquila walker has a truncated name.");
        }
        ReadOnlySpan<byte> field = data.AsSpan(offset, length);
        int terminator = field.IndexOf((byte)0);
        if (terminator < 0)
        {
            terminator = length;
        }
        return Encoding.ASCII.GetString(field[..terminator]);
    }

    private static string SanitizeNodeName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }
        return builder.Length == 0 ? "Unnamed" : builder.ToString();
    }

    private static int ReadInt32(byte[] data, int offset)
    {
        if (offset < 0 || offset + sizeof(int) > data.Length)
        {
            throw new InvalidDataException("The retained Aquila walker ended before an integer field.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, sizeof(int)));
    }

    private static float ReadSingle(byte[] data, int offset)
    {
        float value = BitConverter.Int32BitsToSingle(ReadInt32(data, offset));
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException("The retained Aquila walker contains a non-finite number.");
        }
        return value;
    }

    private static Vector3 ReadVector3(byte[] data, int offset) =>
        new(ReadSingle(data, offset), ReadSingle(data, offset + 4), ReadSingle(data, offset + 8));

    private static void RequireEnd(int cursor, int expected, string role)
    {
        if (cursor != expected)
        {
            throw new InvalidDataException($"The retained Aquila walker has trailing {role} data.");
        }
    }

    private readonly record struct Chunk(int PayloadOffset, int Length)
    {
        public int EndOffset => PayloadOffset + Length;
    }

    private sealed record ParsedAsset(
        Part[] Parts,
        TextureMetadata[] Textures,
        int ExpandedSurfaceCount);

    private sealed record AssetProfile(
        string DisplayName,
        string RootName,
        string SourceSha256,
        int SourceLength,
        int InflatedLength,
        int TextureCount,
        int PartCount,
        int ExpandedSurfaceCount,
        int AnimatedPartCount,
        int CemtLength,
        bool IsWalker,
        float? BasePoseValidationFrame,
        float InitialFrame,
        Vector3 RootOffset,
        bool CastShadow,
        AnimationMode[] AnimationModes,
        RetailStageZeroColorOperation StageZeroColorOperation);

    private sealed record AnimationMode(
        string Name,
        int Start,
        int End,
        int Span,
        float Increment);

    private sealed record LegDefinition(
        int Id,
        int RootPart,
        int FootPart,
        int[] AnimatedParts);

    private sealed class Part(
        int index,
        string name,
        int virtualFrameCount,
        int horizontalFrameCount,
        BeaTransform baseGlobalTransform)
    {
        public int Index { get; } = index;
        public string Name { get; } = name;
        public int VirtualFrameCount { get; } = virtualFrameCount;
        public int HorizontalFrameCount { get; } = horizontalFrameCount;
        public BeaTransform BaseGlobalTransform { get; } = baseGlobalTransform;
        public int? Parent { get; set; }
        public int? Reference { get; set; }
        public int[] Children { get; set; } = [];
        public byte[] FrameMap { get; set; } = [];
        public BeaTransform[] Orientations { get; set; } = [];
        public Vector3[] Positions { get; set; } = [];
        public Geometry? Geometry { get; set; }

        public BeaTransform GetTransform(float virtualFrame)
        {
            float clamped = Mathf.Clamp(virtualFrame, 0f, VirtualFrameCount - 1);
            int firstVirtual = Mathf.FloorToInt(clamped);
            int secondVirtual = Math.Min(firstVirtual + 1, VirtualFrameCount - 1);
            int firstStored = FrameMap[firstVirtual];
            int secondStored = FrameMap[secondVirtual];
            BeaTransform first = new(Orientations[firstStored].Rotation, Positions[firstStored]);
            BeaTransform second = new(Orientations[secondStored].Rotation, Positions[secondStored]);
            return BeaTransform.Interpolate(first, second, clamped - firstVirtual);
        }

        public BeaTransform GetStandingTransform() =>
            TryGetObservedLevel100StandingTransform(Index, out BeaTransform observed)
                ? observed
                : GetTransform(ExpectedLegMotionStart);
    }

    private sealed record Geometry(
        Vector3[] Vertices,
        Vector3[] Normals,
        Vector2[] TextureCoordinates,
        Color[] Colors,
        GeometryGroup[] Groups);

    private sealed record GeometryGroup(int[] Triangles, int[] TextureIndices);

    private sealed record TextureMetadata(float Opacity, Vector2 Offset, Vector2 Scale);

    private readonly record struct BeaTransform(Matrix3 Rotation, Vector3 Position)
    {
        public bool IsFinite => Rotation.IsFinite && Position.IsFinite();

        public Vector3 TransformPoint(Vector3 point) => Rotation.Transform(point) + Position;

        public static BeaTransform Compose(BeaTransform parent, BeaTransform child) =>
            new(
                Matrix3.Multiply(parent.Rotation, child.Rotation),
                parent.Rotation.Transform(child.Position) + parent.Position);

        // Retail CMeshPart__InterpolateSegmentTransform (0x004b0d00) blends
        // every stored HORI component directly. Quaternion slerp would erase
        // released scale/shear terms and produces a different fractional pose.
        public static BeaTransform Interpolate(BeaTransform first, BeaTransform second, float weight) =>
            new(
                Matrix3.Lerp(first.Rotation, second.Rotation, weight),
                first.Position.Lerp(second.Position, weight));

        public bool ApproximatelyEquals(BeaTransform other, float tolerance) =>
            Rotation.ApproximatelyEquals(other.Rotation, tolerance) &&
            Position.DistanceTo(other.Position) <= tolerance;
    }

    private readonly record struct Matrix3(
        float M00,
        float M01,
        float M02,
        float M10,
        float M11,
        float M12,
        float M20,
        float M21,
        float M22)
    {
        public bool IsFinite =>
            float.IsFinite(M00) && float.IsFinite(M01) && float.IsFinite(M02) &&
            float.IsFinite(M10) && float.IsFinite(M11) && float.IsFinite(M12) &&
            float.IsFinite(M20) && float.IsFinite(M21) && float.IsFinite(M22);

        public Vector3 Transform(Vector3 value) => new(
            (M00 * value.X) + (M01 * value.Y) + (M02 * value.Z),
            (M10 * value.X) + (M11 * value.Y) + (M12 * value.Z),
            (M20 * value.X) + (M21 * value.Y) + (M22 * value.Z));

        public static Matrix3 Lerp(Matrix3 first, Matrix3 second, float weight) => new(
            first.M00 + ((second.M00 - first.M00) * weight),
            first.M01 + ((second.M01 - first.M01) * weight),
            first.M02 + ((second.M02 - first.M02) * weight),
            first.M10 + ((second.M10 - first.M10) * weight),
            first.M11 + ((second.M11 - first.M11) * weight),
            first.M12 + ((second.M12 - first.M12) * weight),
            first.M20 + ((second.M20 - first.M20) * weight),
            first.M21 + ((second.M21 - first.M21) * weight),
            first.M22 + ((second.M22 - first.M22) * weight));

        public static Matrix3 Multiply(Matrix3 first, Matrix3 second) => new(
            (first.M00 * second.M00) + (first.M01 * second.M10) + (first.M02 * second.M20),
            (first.M00 * second.M01) + (first.M01 * second.M11) + (first.M02 * second.M21),
            (first.M00 * second.M02) + (first.M01 * second.M12) + (first.M02 * second.M22),
            (first.M10 * second.M00) + (first.M11 * second.M10) + (first.M12 * second.M20),
            (first.M10 * second.M01) + (first.M11 * second.M11) + (first.M12 * second.M21),
            (first.M10 * second.M02) + (first.M11 * second.M12) + (first.M12 * second.M22),
            (first.M20 * second.M00) + (first.M21 * second.M10) + (first.M22 * second.M20),
            (first.M20 * second.M01) + (first.M21 * second.M11) + (first.M22 * second.M21),
            (first.M20 * second.M02) + (first.M21 * second.M12) + (first.M22 * second.M22));

        public bool ApproximatelyEquals(Matrix3 other, float tolerance) =>
            Math.Abs(M00 - other.M00) <= tolerance &&
            Math.Abs(M01 - other.M01) <= tolerance &&
            Math.Abs(M02 - other.M02) <= tolerance &&
            Math.Abs(M10 - other.M10) <= tolerance &&
            Math.Abs(M11 - other.M11) <= tolerance &&
            Math.Abs(M12 - other.M12) <= tolerance &&
            Math.Abs(M20 - other.M20) <= tolerance &&
            Math.Abs(M21 - other.M21) <= tolerance &&
            Math.Abs(M22 - other.M22) <= tolerance;
    }
}
