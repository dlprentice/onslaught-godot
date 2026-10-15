// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The in-level sun flare: the shipped <c>Sun Sprite</c> particle descriptor,
/// decoded out of <c>data/ParticleSets/MainSet.par</c> and drawn where the
/// released engine draws it.
///
/// <para><b>Nothing here is authored by us.</b> The descriptor name, the placement
/// law and the line-of-sight gate come from the pinned GPL source; the texture,
/// blend mode, radius and colour come from the shipped particle set; and the one
/// value neither source carries - how an authored <c>Radius</c> becomes a
/// billboard extent - is measured against a retail frame in
/// <c>Level100SunTests</c>. Fields the shipped record carries that no available
/// source explains are listed under "unread" below rather than guessed at.</para>
///
/// <para><b>Placement.</b> <c>references/Onslaught/DXEngine.cpp:1043-1046</c>
/// builds <c>FVector sun(hfp.SunPosX, hfp.SunPosY, hfp.SunPosZ)</c>, scales it by
/// <c>SUN_SCALE</c> (<c>0.6f</c>, <c>DXEngine.cpp:975</c>) and adds the particle
/// at <c>camera-&gt;GetPos() + sun</c> (<c>DXEngine.cpp:1064</c>). The sprite is
/// therefore a fixed short distance in front of the camera along the level's own
/// sun vector, not at any world position, and it is re-added every rendered
/// frame. Level 100's <c>SunPos</c> is already unit length, so the released
/// distance is 0.6 m; the raw vector is scaled rather than normalised here
/// because that is what the source does.</para>
///
/// <para><b>Occlusion.</b> Because the sprite sits 0.6 m from the eye, no depth
/// buffer can ever hide it behind a mountain - which is exactly why
/// <c>DXEngine.cpp:1050-1062</c> casts a 200 m ray along the sun direction and
/// skips the particle entirely unless the ray reaches open sky. That gate is
/// reproduced here against the height field. <b>Narrower than retail:</b> the
/// released test is <c>WORLD.FindFirstThingToHitLine</c>, which also considers
/// world things (the player's own battle engine excepted); this one considers
/// terrain only, so a sun seen through a building or a target still draws.</para>
///
/// <para><b>The gate is live but unexercised by any capture we have.</b>
/// Measured 2026-07-30 over the 91-frame gameplay plan: the sun draws in 76
/// frames and is absent in 15, and all 15 are the sprite being off-screen
/// rather than occluded - a control capture with the gate forced true is
/// pixel-identical across all 91. A second control that aims the probe ray into
/// the ground suppresses all 76, so the predicate does read the height field and
/// does return false when the ray is blocked. What no captured frame shows is
/// the gate firing on its own terms, because on this timeline the sun is never
/// behind terrain.</para>
///
/// <para><b>Fields of the shipped record this class does not read</b>, because no
/// available evidence says what they do: <c>Axis_Aligned 4</c> (the resolver
/// records its semantics as unresolved; a screen-aligned billboard is used and
/// the measured on-screen size is consistent with one), <c>Bounce 0</c>,
/// <c>Length 1.0</c>, <c>2D 0</c>, and <c>Velocity_Damp</c> - the last three
/// being inert for a sprite that never moves. <c>Final_Radius 1.0</c> and
/// <c>Life_Pct 1.0</c> are likewise never reached: <c>Life 0</c> means each
/// instance is only ever seen at its start radius, so the radius-over-life
/// interpolation law - which is absent from the source drop along with the rest
/// of the particle system - cannot affect this effect and is not invented.</para>
///
/// <para><b>Not reproduced, and visibly so.</b> <c>DXEngine.cpp:974</c> gates the
/// whole block on <c>hfp.VisibleSun</c>. That flag's offset inside the CHFD
/// payload is not decoded by <c>Level100Terrain</c>, so this class cannot consult
/// it. Level 100 plainly draws the sun in retail, so the reconstruction draws it
/// too, but a level that suppresses its sun would be reproduced wrongly. This is
/// an open item, not a solved one.</para>
/// </summary>
internal sealed class Level100SunAsset
{
    /// <summary>
    /// The descriptor the released engine looks up by name.
    /// <c>references/Onslaught/DXEngine.cpp:220</c>
    /// (<c>mSunPD = PARTICLE_SET.GetPD("Sun Sprite")</c>) and the equivalent
    /// per-frame lookup at <c>references/Onslaught/PCEngine.cpp:753</c>. The
    /// string is present in the released binary at file offset <c>0x2505C8</c>
    /// of the pristine specimen
    /// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>
    /// (sha256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>),
    /// so the shipped build resolves the same name.
    /// </summary>
    public const string DescriptorName = "Sun Sprite";

    /// <summary>
    /// <c>SUN_SCALE</c>, <c>references/Onslaught/DXEngine.cpp:975</c>.
    /// <c>references/Onslaught/PCEngine.cpp:709</c> defines the same 0.6.
    /// </summary>
    public const float RetailSunScale = 0.6f;

    /// <summary>
    /// The line-of-sight probe length, <c>references/Onslaught/DXEngine.cpp:1051</c>
    /// (<c>sunch *= 200.f</c>).
    /// </summary>
    public const float RetailLineOfSightMetres = 200f;

    /// <summary>
    /// The one shipped <c>sun3.tga</c> archive. <c>Sun Sprite</c> authors
    /// <c>Blend_Mode 0</c>, which
    /// <see cref="ParticleEffectResolver.BlendModeSelectsShippedTextureFormat"/>
    /// measures as the alpha-less additive copy - and <c>(0)R5G6B5</c> is the
    /// only format this texture ships in, so no selection ambiguity arises.
    /// </summary>
    private const string SunTexturePath =
        "res://Assets/Level100/Textures/particle-sun3-additive.texture.aya";

    private const string MainSetPath =
        "res://Assets/Level100/ParticleSets/MainSet.par";

    private const int SunTextureSize = 128;

    /// <summary>Ray-march step for the occlusion probe, in metres.</summary>
    private const float LineOfSightStepMetres = 2f;

    private readonly Level100HeightFieldAsset _terrain;
    private readonly Vector3 _offsetFromCamera;
    private readonly Vector3 _directionFromCamera;

    private Level100SunAsset(
        Level100HeightFieldAsset terrain,
        ParticleSpriteLayer layer,
        MeshInstance3D root,
        Vector3 offsetFromCamera)
    {
        _terrain = terrain;
        Layer = layer;
        Root = root;
        _offsetFromCamera = offsetFromCamera;
        _directionFromCamera = offsetFromCamera.Normalized();
    }

    /// <summary>The node to add to the world and reposition each frame.</summary>
    public MeshInstance3D Root { get; }

    /// <summary>
    /// The decoded descriptor, exposed so tests assert against the shipped file
    /// rather than against constants transcribed out of it.
    /// </summary>
    public ParticleSpriteLayer Layer { get; }

    /// <summary>Whether the last <see cref="Update"/> left the flare drawn.</summary>
    public bool Visible => Root.Visible;

    /// <summary>
    /// Decodes <c>Sun Sprite</c> out of the shipped particle set and builds the
    /// billboard for it.
    /// </summary>
    public static Level100SunAsset Create(Level100HeightFieldAsset terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);

        byte[] source = Godot.FileAccess.GetFileAsBytes(MainSetPath);
        if (source.Length == 0)
        {
            throw new InvalidDataException(
                $"The shipped particle set '{MainSetPath}' is missing. " +
                "Run 'npm run prepare:rebuild-assets'.");
        }

        ParticleSetFile set = ParticleSetFile.Parse(source);
        ParticleEffectPlan plan = ParticleEffectResolver.Resolve(set, DescriptorName);

        // `Sun Sprite` is a bare type-1 record: no emitter, no timeline, no
        // modifier. If a future read of the file makes it anything else, that is
        // a finding and must not be silently averaged away.
        if (plan.Unimplemented.Count != 0)
        {
            throw new InvalidDataException(
                $"'{DescriptorName}' resolved with unimplemented elements, which the " +
                $"shipped record does not have: {string.Join("; ", plan.Unimplemented)}");
        }

        ParticleSpriteLayer layer = plan.Layers.Count == 1
            ? plan.Layers[0]
            : throw new InvalidDataException(
                $"'{DescriptorName}' resolved to {plan.Layers.Count} layers; the shipped " +
                "record is a single sprite.");

        if (!string.Equals(layer.TextureName, "sun3.tga", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"'{DescriptorName}' names texture '{layer.TextureName}', but the only " +
                "sun texture retained by the materializer is 'sun3.tga'.");
        }

        if (layer.BlendMode != 0)
        {
            throw new InvalidDataException(
                $"'{DescriptorName}' authors Blend_Mode {layer.BlendMode}; only the " +
                "additive mode-0 copy of its texture is retained.");
        }

        Texture2D texture = CuratedAyaTextureLoader.Load(
            SunTexturePath,
            SunTextureSize,
            SunTextureSize,
            CuratedAyaTextureLoader.Compression.Dxt1);

        var root = new MeshInstance3D
        {
            Name = "RetailLevel100SunSprite",
            // MEASURED: the authored `Radius` is the billboard's HALF extent, so
            // the quad is 2*Radius on a side. The conversion has one owner -
            // see ParticleEffectResolver.AuthoredRadiusIsHalfTheQuadSide for the
            // retail draw-call geometry that establishes it and the readings it
            // refutes.
            Mesh = new QuadMesh
            {
                Size = new Vector2(
                    ParticleEffectResolver.BillboardQuadSide(layer.StartRadius),
                    ParticleEffectResolver.BillboardQuadSide(layer.StartRadius)),
            },
            MaterialOverride = CreateSunMaterial(texture, layer),
            // The released engine draws the sun before the particle pass and
            // before the cockpit (PCEngine.cpp, the SUN / PARTICLE SYSTEM /
            // Render Cockpit sequence), so cockpit geometry occludes it. Leaving
            // depth testing on reproduces that ordering, since the cockpit is
            // parented to the camera and is nearer than the sprite.
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        Vector3 offset = ToGodotDirection(terrain.SunPosition) * RetailSunScale;
        return new Level100SunAsset(terrain, layer, root, offset);
    }

    /// <summary>
    /// Places the sprite for this frame and applies the released line-of-sight
    /// gate.
    /// </summary>
    public void Update(Vector3 cameraPosition)
    {
        Root.Position = cameraPosition + _offsetFromCamera;
        Root.Visible = HasLineOfSightToSun(cameraPosition);
    }

    /// <summary>
    /// <c>references/Onslaught/DXEngine.cpp:1050-1062</c>: a 200 m ray along the
    /// normalised sun vector, and the particle is added only when it hits
    /// nothing. Reproduced against the height field alone - see the class
    /// remarks for what that leaves out.
    /// </summary>
    private bool HasLineOfSightToSun(Vector3 cameraPosition)
    {
        for (float travelled = LineOfSightStepMetres;
            travelled <= RetailLineOfSightMetres;
            travelled += LineOfSightStepMetres)
        {
            Vector3 point = cameraPosition + (_directionFromCamera * travelled);
            if (point.Y <= _terrain.SampleRelativeHeight(point.X, point.Z))
            {
                return false;
            }
        }

        return true;
    }

    private static StandardMaterial3D CreateSunMaterial(
        Texture2D texture,
        ParticleSpriteLayer layer)
    {
        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            // The authored `Colour_Range`, applied as a flat modulation. This is
            // legitimate only because the shipped `Sun Colour` record makes it
            // unambiguous - see ResolveConstantColour.
            AlbedoColor = ResolveConstantColour(layer),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // `Blend_Mode 0`, measured as the alpha-less additive copy.
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale = true,
        };
    }

    /// <summary>
    /// The colour the authored <c>Colour_Range</c> holds for this sprite's whole
    /// life.
    ///
    /// <para>How a colour range is interpolated over a particle's life is NOT
    /// recoverable - the particle system is absent from the source drop. This
    /// method does not need it: the shipped <c>Sun Colour</c> record authors
    /// <c>Start</c> and <c>End</c> equal at <c>0.501961</c> per channel
    /// (<c>128/255</c>) with <c>Use_Transition 0</c>, so every interpolation law
    /// agrees on the same constant. If a future file breaks that tie the
    /// interpolation becomes load-bearing, and this throws rather than picking
    /// an end.</para>
    /// </summary>
    private static Color ResolveConstantColour(ParticleSpriteLayer layer)
    {
        if (layer.ColourRange is not { } range)
        {
            return Colors.White;
        }

        if (range.UseTransition)
        {
            throw new InvalidDataException(
                $"Colour range '{range.Name}' authors Use_Transition, whose " +
                "interpolation law is not recovered.");
        }

        if (range.UseEnd && range.End != range.Start)
        {
            throw new InvalidDataException(
                $"Colour range '{range.Name}' ramps from {range.Start} to {range.End}; " +
                "the ramp law over a particle's life is not recovered.");
        }

        return new Color(range.Start.R, range.Start.G, range.Start.B);
    }

    /// <summary>
    /// The engine's <c>(X, Y, Z)</c> into Godot's basis, the same mapping
    /// <see cref="Level100HeightFieldAsset"/> uses for the light direction.
    /// </summary>
    private static Vector3 ToGodotDirection(Vector3 beaVector) =>
        new(beaVector.X, -beaVector.Z, -beaVector.Y);
}
