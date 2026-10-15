// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// How a sprite walks its texture atlas, from the authored <c>Anim_Type</c>.
///
/// <para><b>MEASURED 2026-07-28</b> over the 405 shipped
/// <see cref="ParticleDescriptorType.Sprite"/> records. For every one of the 56
/// records with <c>Anim_Type 1</c>, the identity
/// <c>Life * Anim_Speed == End_Frame - Texture_Number</c> holds to within two
/// percent - and for the eleven of those that author <c>Texture_Number &gt; 0</c> the
/// ratio is exactly 1.000 to three decimals, which is what distinguishes
/// "starts at <c>Texture_Number</c>" from "starts at cell 0". The same quantity
/// over the 106 <c>Anim_Type 2</c> records is spread 0.1 to 27, i.e. they run
/// off the end of their span and therefore repeat.</para>
/// </summary>
public enum ParticleAnimationMode
{
    /// <summary>
    /// <c>Anim_Type 0</c>, 243 records. The sprite shows one cell for its whole
    /// life. 215 of the 243 also author <c>End_Frame 0</c>; on the other 28 the
    /// authored <c>End_Frame</c> is inert, which is exactly why the four
    /// records that would otherwise violate the atlas-range check all sit here.
    /// </summary>
    Static,

    /// <summary>
    /// <c>Anim_Type 1</c>, 56 records. Runs <c>Texture_Number</c> to
    /// <c>End_Frame</c> once, at <c>Anim_Speed</c> cells per game turn, and by
    /// authoring lands on the last cell exactly as the sprite dies.
    /// </summary>
    PlayOnce,

    /// <summary>
    /// <c>Anim_Type 2</c>, 106 records. Same span and speed, but wraps.
    /// </summary>
    Loop,
}

/// <summary>
/// A resolved <see cref="ParticleDescriptorType.ColourRange"/> record.
/// </summary>
/// <param name="Name">The authored descriptor name.</param>
/// <param name="Start">Start RGB, each channel 0..1.</param>
/// <param name="End">End RGB, used only when <paramref name="UseEnd"/>.</param>
/// <param name="Transition">
/// Mid RGB, used only when <paramref name="UseTransition"/>, reached at
/// <paramref name="TransitionPoint"/> of the sprite's life.
/// </param>
/// <param name="UseEnd">Authored <c>Use_End</c>.</param>
/// <param name="UseTransition">Authored <c>Use_Transition</c>.</param>
/// <param name="TransitionPoint">Authored <c>Transition_Point</c>, 0..1.</param>
public readonly record struct ParticleColourRange(
    string Name,
    (float R, float G, float B) Start,
    (float R, float G, float B) End,
    (float R, float G, float B) Transition,
    bool UseEnd,
    bool UseTransition,
    float TransitionPoint);

/// <summary>
/// A resolved <see cref="ParticleDescriptorType.Shape"/> record - the volume an
/// emitter scatters its particles over.
/// </summary>
/// <param name="Name">The authored descriptor name.</param>
/// <param name="TypeId">
/// Authored <c>Type</c>. UNRESOLVED semantics: 0 and 1 both occur and we do not
/// have evidence for which is ring and which is sphere, so the consumer is told
/// the number rather than a guessed name.
/// </param>
/// <param name="RingAxis">Authored <c>Ring_Axis</c>.</param>
/// <param name="Hemisphere">Authored <c>Hemisphere</c>.</param>
/// <param name="NumParticles">Authored <c>Num_Particles</c>.</param>
/// <param name="Radius">Authored <c>Radius</c> base value.</param>
/// <param name="Hollow">
/// Authored <c>Hollow</c>. Absent on exactly one shipped record, which reads as
/// <see langword="false"/>.
/// </param>
/// <param name="RandomScale">Authored <c>RandomSX/SY/SZ</c>.</param>
public readonly record struct ParticleEmissionShape(
    string Name,
    int TypeId,
    int RingAxis,
    int Hemisphere,
    int NumParticles,
    float Radius,
    bool Hollow,
    (float X, float Y, float Z) RandomScale);

/// <summary>
/// One authored billboard layer of an effect, with every value that the
/// <c>.par</c> file actually carries resolved to a number.
///
/// <para>Nothing here is chosen. Every property is either a verbatim authored
/// field or a value derived from one by a law recorded on
/// <see cref="ParticleEffectResolver"/>. Where the shipped tree carries
/// something this type cannot express - a mesh fragment, a trail, a mover, a
/// param-function modifier - the resolver does not approximate it; it names it
/// in <see cref="ParticleEffectPlan.Unimplemented"/>.</para>
/// </summary>
public sealed record ParticleSpriteLayer
{
    /// <summary>The authored <see cref="ParticleDescriptorType.Sprite"/> name.</summary>
    public required string DescriptorName { get; init; }

    /// <summary>
    /// The reference chain from the effect root to this layer, for provenance
    /// in logs and tests - e.g.
    /// <c>Muspell Building Explosion Effect &gt; Building Smoke Emitter &gt; Smoke Sprite Anim Large Building</c>.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The texture file name with no directory, lower-cased - e.g.
    /// <c>alparticle5.tga</c>. The authored <c>Texture</c> field is the
    /// developers' own build-machine path
    /// (<c>C:\dev\Onslaught2\data\textures\Particle\...</c>) and its case is
    /// inconsistent across the corpus, so only the leaf is kept.
    /// </summary>
    public required string TextureName { get; init; }

    /// <summary>
    /// Authored <c>Blend_Mode</c>. See
    /// <see cref="ParticleEffectResolver.BlendModeSelectsShippedTextureFormat"/>
    /// for the measured meaning.
    /// </summary>
    public required int BlendMode { get; init; }

    /// <summary>Atlas columns, from <c>Texture_Size</c>. 4, 2 or 1.</summary>
    public required int AtlasColumns { get; init; }

    /// <summary>Atlas rows. Always equal to <see cref="AtlasColumns"/>.</summary>
    public required int AtlasRows { get; init; }

    /// <summary>
    /// Authored <c>Texture_Number</c>, the first cell. <c>-1</c> means the
    /// sprite uses the whole texture and ignores the grid; 129 of the 405
    /// shipped sprites author it.
    /// </summary>
    public required int StartCell { get; init; }

    /// <summary>
    /// Authored <c>End_Frame</c>, the last cell INCLUSIVE. Measured twice
    /// against pixels: <c>fireball.tga</c> authors 11 and has exactly cells
    /// 0-11 inked with 12-15 at mean luminance 0; <c>alparticle4.tga</c>
    /// authors 14 and has exactly cells 0-14 inked.
    /// </summary>
    public required int EndCell { get; init; }

    /// <summary>Authored <c>Anim_Type</c>, resolved.</summary>
    public required ParticleAnimationMode AnimationMode { get; init; }

    /// <summary>Authored <c>Anim_Speed</c>, in atlas cells per game turn.</summary>
    public required float AnimationCellsPerTurn { get; init; }

    /// <summary>Authored <c>Random_Start_Frame</c>.</summary>
    public required bool RandomStartCell { get; init; }

    /// <summary>
    /// Authored <c>Life</c>, in game turns. Negative values are authored
    /// (<c>-2</c> occurs) and mean the particle does not expire on its own.
    /// </summary>
    public required int LifeTurns { get; init; }

    /// <summary>Authored <c>Radius</c> base value, in game units.</summary>
    public required float StartRadius { get; init; }

    /// <summary>Authored <c>Final_Radius</c>, in game units.</summary>
    public required float FinalRadius { get; init; }

    /// <summary>Authored <c>Life_Pct</c>.</summary>
    public required float LifeFraction { get; init; }

    /// <summary>Authored <c>Fade_Col</c>.</summary>
    public required bool FadeColour { get; init; }

    /// <summary>Authored <c>Axis_Aligned</c>. UNRESOLVED semantics.</summary>
    public required int AxisAligned { get; init; }

    /// <summary>Authored <c>Gravity</c>.</summary>
    public required bool Gravity { get; init; }

    /// <summary>Authored <c>Velocity_Damp</c>, applied per game turn.</summary>
    public required float VelocityDamp { get; init; }

    /// <summary>The resolved <c>Colour_Range</c>, or null when authored NONE.</summary>
    public required ParticleColourRange? ColourRange { get; init; }

    /// <summary>
    /// How many instances of this layer the effect starts, from the authored
    /// emitter schedule. 1 when the layer is referenced directly rather than
    /// through an emitter.
    /// </summary>
    public required int InstanceCount { get; init; }

    /// <summary>
    /// The game turn each instance starts on, one entry per instance. Carries
    /// both the timeline <c>Time</c> offset and the emitter's own per-turn
    /// schedule.
    /// </summary>
    public required IReadOnlyList<int> StartTurns { get; init; }

    /// <summary>
    /// The emitter's <c>Shape</c>, when it has one. The consumer places
    /// instances on it; there is no authored per-instance position.
    /// </summary>
    public required ParticleEmissionShape? Shape { get; init; }

    /// <summary>
    /// Authored <c>Initial_Velocity_X/Y/Z</c> of the owning emitter, in game
    /// units per turn, in the retail X/Y/Z-down basis.
    /// </summary>
    public required (float X, float Y, float Z) InitialVelocity { get; init; }

    /// <summary>Authored <c>Outward_Velocity</c> of the owning emitter.</summary>
    public required float OutwardVelocity { get; init; }

    /// <summary>Authored <c>Velocity_Randomness</c> of the owning emitter.</summary>
    public required float VelocityRandomness { get; init; }
}

/// <summary>
/// A named effect resolved into the billboard layers a renderer can draw, plus
/// an explicit list of everything in the authored tree that was NOT resolved.
///
/// <para><see cref="Unimplemented"/> is the point of this type. A plan that
/// silently dropped the mesh shrapnel, the movers and the param-function
/// modifiers would look complete and be wrong; this one names each omission at
/// the site so the gap is visible to a test and to a reader.</para>
/// </summary>
/// <param name="EffectName">The authored descriptor name that was resolved.</param>
/// <param name="RootType">The type of that descriptor.</param>
/// <param name="Layers">The billboard layers, in authored order.</param>
/// <param name="Unimplemented">
/// One entry per authored element this resolver did not turn into a layer, each
/// naming the descriptor and why.
/// </param>
public sealed record ParticleEffectPlan(
    string EffectName,
    ParticleDescriptorType RootType,
    IReadOnlyList<ParticleSpriteLayer> Layers,
    IReadOnlyList<string> Unimplemented)
{
    /// <summary>Total billboard instances the plan starts.</summary>
    public int TotalInstances
    {
        get
        {
            int total = 0;
            foreach (ParticleSpriteLayer layer in Layers)
            {
                total += layer.InstanceCount;
            }

            return total;
        }
    }
}
