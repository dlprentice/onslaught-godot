// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// Turns a named descriptor out of a shipped <c>.par</c> particle set into a
/// flat <see cref="ParticleEffectPlan"/> of billboard layers.
///
/// <para><b>What is authored and what is not.</b> Every number this resolver
/// emits is read from the file. The laws it uses to interpret those numbers are
/// recorded on the constants below, each with the measurement that establishes
/// it. Where the authored tree contains something no billboard can express -
/// a <see cref="ParticleDescriptorType.Mesh"/> fragment, a
/// <see cref="ParticleDescriptorType.Trail"/>, a
/// <see cref="ParticleDescriptorType.Mover"/>, a
/// <see cref="ParticleDescriptorType.PMesh"/>, or a
/// <see cref="ParticleDescriptorType.Function"/> modifier - the resolver
/// records it in <see cref="ParticleEffectPlan.Unimplemented"/> and drops it.
/// It never substitutes a plausible number.</para>
/// </summary>
public static class ParticleEffectResolver
{
    /// <summary>
    /// Game turns per second. <c>Life</c>, <c>Time</c>, <c>Emit_Per_Turn</c>
    /// and <c>Anim_Speed</c> are all denominated in these.
    ///
    /// <para>MEASURED: all 46 shipped
    /// <see cref="ParticleDescriptorType.Function"/> records author
    /// <c>Gameturn_Scale 20.000000</c>, and the released simulation's base
    /// cadence is independently established at 20 Hz
    /// (<c>rebuild/PROVENANCE.md</c>: "observes it at the exact 20 Hz base
    /// cadence inside the 30 Hz simulation").</para>
    /// </summary>
    public const int GameTurnsPerSecond = 20;

    /// <summary>
    /// <c>Blend_Mode</c> selects WHICH shipped copy of the texture is loaded,
    /// and thereby the blend equation.
    ///
    /// <para><b>MEASURED 2026-07-28</b>, and the partition is exact with no
    /// exceptions. Across the 40 distinct texture basenames referenced by the
    /// 405 shipped sprites, the set of <c>Blend_Mode</c> values that reference a
    /// texture matches the set of pixel formats that texture ships in, one for
    /// one:</para>
    /// <list type="bullet">
    /// <item>24 textures are referenced only with mode 0, and ship only as
    /// <c>(0)R5G6B5</c> - an archive with no alpha channel at all, which can
    /// only be blended additively.</item>
    /// <item>4 textures are referenced only with mode 1, and ship only as
    /// <c>(0)A4R4G4B4</c>.</item>
    /// <item>Every texture referenced with both modes
    /// (<c>alparticle4</c>, <c>Blood</c>, <c>fireball</c>, <c>particles</c>,
    /// <c>Small Puff</c>, <c>Blue Spark 2</c>) ships in both formats.</item>
    /// </list>
    /// <para>So mode 0 is the alpha-less additive copy and mode 1 is the
    /// alpha-blended copy. Mode 2 occurs on 4 sprites only, both of whose
    /// textures are dual-format; it is NOT resolved here and consumers are
    /// given the raw number.</para>
    /// </summary>
    public const int BlendModeSelectsShippedTextureFormat = 0;

    /// <summary>
    /// Our own bound on how many billboard instances one effect may start. It
    /// is a reconstruction limit, NOT a retail value: retail's emitters are
    /// throttled by their <c>Emit_Per_Turn</c> modifier curves, which this
    /// resolver does not implement.
    /// </summary>
    public const int MaximumInstancesPerEffect = 256;

    /// <summary>
    /// The factor taking an authored <c>Radius</c> to the side of the billboard
    /// quad the engine draws for it. <b>The authored <c>Radius</c> is the HALF
    /// extent</b>, so the quad is <c>2 * Radius</c> on a side.
    ///
    /// <para><b>MEASURED 2026-07-31 from retail draw-call geometry</b>
    /// (task #151), which needs no camera parameters and no pixel fitting. The
    /// d3d9 proxy records raw vertex positions, and the sun sprite is
    /// identifiable in every logged in-level frame without ambiguity: it is the
    /// only single-quad <c>fvf=0x142</c> draw, additive <c>ONE/ONE</c>, over a
    /// 128x128 DXT1 texture, with vertex diffuse <c>0x00808080</c> - the flat
    /// 128/255 its <c>Sun Colour</c> record authors.</para>
    ///
    /// <para>Its quad measures <b>0.20009 x 0.19998</b> world units, all four
    /// edges within 0.0001 of 0.2000 and the diagonal consistent with a square,
    /// and it is bit-identical across nine logged frames from three independent
    /// launches (<c><maintainer evidence>\bea-d3d9-capture\B{1,2,3}-level100-*\d3d9-draws.log</c>,
    /// draws 1130/1123/1123). <c>Sun Sprite</c> authors
    /// <c>Radius 0.100000</c>. So the quad is 2.000x the authored radius, and
    /// the competing reading - <c>Radius</c> as the full extent - is out by
    /// exactly a factor of two.</para>
    ///
    /// <para><b>Why that draw is certainly the sun, and the buffer certainly
    /// world space at unit scale.</b> Retail places the sprite at
    /// <c>camera + SunPos * 0.6</c>
    /// (<c>references/Onslaught/DXEngine.cpp:1043-1064</c>). Running that law
    /// backwards from the measured quad centre with Level 100's authored unit
    /// <c>SunPos</c> puts the eye at <c>(288.6875, 243.2500)</c>, which is the
    /// level's own coordinate origin to five decimals -
    /// <see cref="Core.Level100Terrain.MinimumRelativeXMillimeters"/> and
    /// <see cref="Core.Level100Terrain.MinimumRelativeZMillimeters"/> are
    /// -288688 and -243250. Two coordinates from three independent sources
    /// landing together also rules out a scaled world matrix, which would have
    /// moved the derived eye off the origin.</para>
    ///
    /// <para>Independently corroborated by task #148's frame measurement, which
    /// fitted the flare's on-screen half extent at 65-69 px against the 69.0 px
    /// this factor predicts, and refuted the full-extent reading at 34.5 px
    /// (fit error 6.10 against 6.98 for drawing no flare at all).</para>
    ///
    /// <para>The particle system is absent from the GPL drop - there is no
    /// <c>Particle.cpp</c>, no <c>ParticleManager.h</c> and no sprite-renderer
    /// implementation, only call sites - so this cannot be ported and had to be
    /// measured.</para>
    /// </summary>
    public const float AuthoredRadiusIsHalfTheQuadSide = 2f;

    /// <summary>
    /// The side of the billboard quad the engine draws for an authored
    /// <c>Radius</c>. <b>This is the one owner of that conversion</b>; sprite
    /// sizes must not be hard-coded already-doubled, because a literal cannot be
    /// traced back to the record it came from.
    /// </summary>
    /// <param name="authoredRadius">
    /// A <c>Radius</c> or <c>Final_Radius</c> exactly as the <c>.par</c> record
    /// spells it.
    /// </param>
    /// <seealso cref="AuthoredRadiusIsHalfTheQuadSide"/>
    public static float BillboardQuadSide(float authoredRadius) =>
        authoredRadius * AuthoredRadiusIsHalfTheQuadSide;

    /// <summary>
    /// The atlas grid a sprite's <c>Texture_Size</c> selects.
    ///
    /// <para><b>MEASURED 2026-07-28 from pixels, not from consistency alone.</b>
    /// <c>Particle%Muzzle Flash Side.tga(0)R5G6B5.aya</c> is 256x256 and holds
    /// exactly three 128x128 muzzle flashes arranged 2x2 with the fourth cell at
    /// mean luminance 0 - and the three sprites that use it author
    /// <c>Texture_Size 3</c> with <c>Texture_Number</c> 0, 1 and 2. That refutes
    /// the alternative reading in which <c>Texture_Size</c> names a fixed cell
    /// pixel size, because a 64-pixel cell would give that texture 16 cells.
    /// </para>
    ///
    /// <para>Corroborated corpus-wide: over the 278 shipped sprites that
    /// actually index the grid (162 animating plus 116 static with an explicit
    /// cell), no <c>Texture_Number</c> or <c>End_Frame</c> falls outside the
    /// range this mapping predicts. Zero counterexamples. The four records that
    /// look like violations all author <c>Anim_Type 0</c>, where
    /// <c>End_Frame</c> is inert.</para>
    /// </summary>
    public static int AtlasGridSide(int textureSize) => textureSize switch
    {
        2 => 4,
        3 => 2,
        4 => 1,
        _ => throw new InvalidDataException(
            $"Particle sprite authored an unshipped Texture_Size {textureSize}; " +
            "the shipped corpus uses only 2, 3 and 4."),
    };

    /// <summary>
    /// Resolves <paramref name="effectName"/> in <paramref name="set"/>.
    /// </summary>
    /// <param name="set">A parsed particle set.</param>
    /// <param name="effectName">
    /// The authored descriptor name, exactly as <c>default physics.dat</c> or
    /// the retail binary spells it.
    /// </param>
    public static ParticleEffectPlan Resolve(ParticleSetFile set, string effectName)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentException.ThrowIfNullOrEmpty(effectName);

        ParticleDescriptor root = set.Require(effectName);
        ResolveState state = new(set);
        state.Visit(root, effectName, startTurn: 0, emitter: null);
        return new ParticleEffectPlan(
            effectName,
            root.Type,
            state.Layers,
            state.Unimplemented);
    }

    private sealed class ResolveState(ParticleSetFile set)
    {
        private readonly HashSet<string> _active = new(StringComparer.Ordinal);
        private readonly List<ParticleSpriteLayer> _layers = [];
        private readonly List<string> _unimplemented = [];
        private int _instances;

        public IReadOnlyList<ParticleSpriteLayer> Layers => _layers;

        public IReadOnlyList<string> Unimplemented => _unimplemented;

        public void Visit(
            ParticleDescriptor descriptor,
            string path,
            int startTurn,
            EmitterContext? emitter)
        {
            // A shipped tree may reference the same descriptor from two
            // branches; a cycle would hang. Guard on the active path only, so
            // a legitimate second reference from a sibling still resolves.
            if (!_active.Add(descriptor.Name))
            {
                _unimplemented.Add(
                    $"{path}: cyclic reference back to '{descriptor.Name}'");
                return;
            }

            try
            {
                switch (descriptor.Type)
                {
                    case ParticleDescriptorType.Sprite:
                        AddSprite(descriptor, path, startTurn, emitter);
                        break;
                    case ParticleDescriptorType.Emitter:
                        VisitEmitter(descriptor, path, startTurn, emitter);
                        break;
                    case ParticleDescriptorType.Timeline:
                        VisitTimeline(descriptor, path, startTurn);
                        break;
                    case ParticleDescriptorType.Selector:
                        VisitRandom(descriptor, path, startTurn, emitter);
                        break;
                    case ParticleDescriptorType.FoR:
                        VisitSystem(descriptor, path, startTurn);
                        break;
                    default:
                        _unimplemented.Add(
                            $"{path} > {descriptor.Name}: type {descriptor.TypeId} " +
                            $"({descriptor.Type}) is authored but not drawn");
                        break;
                }
            }
            finally
            {
                _active.Remove(descriptor.Name);
            }
        }

        private void VisitTimeline(ParticleDescriptor timeline, string path, int startTurn)
        {
            int declared = timeline.Int("Num_Entries");
            IReadOnlyList<string> children = timeline.RawAll("Particle_Descriptor");
            IReadOnlyList<string> times = timeline.RawAll("Time");
            if (children.Count != declared || times.Count != declared)
            {
                throw new InvalidDataException(
                    $"Timeline '{timeline.Name}' declares {declared} entries but " +
                    $"authors {children.Count} descriptors and {times.Count} times.");
            }

            for (int index = 0; index < declared; index++)
            {
                string childName = children[index];
                if (string.Equals(childName, "NONE", StringComparison.Ordinal))
                {
                    continue;
                }

                int time = int.Parse(times[index], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture);
                ParticleDescriptor? child = set.Find(childName);
                if (child is null)
                {
                    _unimplemented.Add(
                        $"{path}: timeline entry '{childName}' is not in this set");
                    continue;
                }

                Visit(child, $"{path} > {childName}", startTurn + time, emitter: null);
            }
        }

        private void VisitSystem(ParticleDescriptor system, string path, int startTurn)
        {
            foreach (string key in (string[])["Initial", "Death"])
            {
                if (system.Reference(key) is not { } childName)
                {
                    continue;
                }

                ParticleDescriptor? child = set.Find(childName);
                if (child is null)
                {
                    _unimplemented.Add($"{path}: {key} '{childName}' is not in this set");
                    continue;
                }

                Visit(child, $"{path} > {childName}", startTurn, emitter: null);
            }

            if (system.Reference("Mover") is { } mover)
            {
                _unimplemented.Add($"{path}: Mover '{mover}' is authored but not applied");
            }
        }

        private void VisitRandom(
            ParticleDescriptor random,
            string path,
            int startTurn,
            EmitterContext? emitter)
        {
            // Retail draws ONE of the four PER PARTICLE, weighted by
            // Probability_0..3, from the process-global CRT random whose live
            // phase rebuild/PROVENANCE.md records as unresolved. We cannot
            // reproduce that draw, so the emitter's instances are split across
            // the branches at their EXPECTED counts instead, by largest
            // remainder so the split is exact and deterministic.
            //
            // This is a stated reconstruction of the sampling, not a claim
            // about Steam's RNG phase - the same boundary the message-box
            // portrait sampler already sits behind.
            IReadOnlyList<int> starts = emitter?.StartTurns ?? [startTurn];
            List<(string Name, int Weight, int Index)> branches = [];
            int totalWeight = 0;
            for (int index = 0; index < 4; index++)
            {
                string? childName = random.Reference($"Particle_Descriptor_{index}");
                int weight = random.Int($"Probability_{index}");
                if (childName is null || weight <= 0)
                {
                    continue;
                }

                branches.Add((childName, weight, index));
                totalWeight += weight;
            }

            if (branches.Count == 0 || totalWeight == 0)
            {
                return;
            }

            int[] allotted = LargestRemainder(
                starts.Count, [.. branches.Select(branch => branch.Weight)], totalWeight);

            for (int index = 0; index < branches.Count; index++)
            {
                (string childName, int weight, _) = branches[index];
                string branchPath =
                    $"{path} > {childName} (weight {weight}/{totalWeight})";
                if (allotted[index] == 0)
                {
                    _unimplemented.Add(
                        $"{branchPath}: rounds to zero instances of the " +
                        $"{starts.Count} this emitter starts");
                    continue;
                }

                ParticleDescriptor? child = set.Find(childName);
                if (child is null)
                {
                    _unimplemented.Add($"{branchPath}: not in this set");
                    continue;
                }

                Visit(
                    child,
                    branchPath,
                    startTurn,
                    emitter is null
                        ? null
                        : emitter.Value with
                        {
                            StartTurns = [.. starts.Take(allotted[index])],
                        });
            }
        }

        /// <summary>
        /// Splits <paramref name="total"/> across <paramref name="weights"/>
        /// so the parts sum to exactly <paramref name="total"/>.
        /// </summary>
        private static int[] LargestRemainder(int total, int[] weights, int weightSum)
        {
            int[] result = new int[weights.Length];
            double[] remainder = new double[weights.Length];
            int assigned = 0;
            for (int index = 0; index < weights.Length; index++)
            {
                double exact = total * (double)weights[index] / weightSum;
                result[index] = (int)Math.Floor(exact);
                remainder[index] = exact - result[index];
                assigned += result[index];
            }

            while (assigned < total)
            {
                int best = 0;
                for (int index = 1; index < weights.Length; index++)
                {
                    if (remainder[index] > remainder[best])
                    {
                        best = index;
                    }
                }

                result[best]++;
                remainder[best] = -1;
                assigned++;
            }

            return result;
        }

        private void VisitEmitter(
            ParticleDescriptor emitter,
            string path,
            int startTurn,
            EmitterContext? outer)
        {
            // An emitter reached THROUGH another emitter is emitted once per
            // outer particle, so its schedule repeats at each outer start turn.
            if (outer is { } parent && parent.StartTurns.Count > 1)
            {
                foreach (int turn in parent.StartTurns)
                {
                    VisitEmitter(emitter, path, turn, outer: null);
                }

                return;
            }

            if (outer is { StartTurns.Count: 1 } single)
            {
                startTurn = single.StartTurns[0];
            }

            string? childName = emitter.Reference("Particle_Descriptor");
            if (childName is null)
            {
                return;
            }

            ParticleDescriptor? child = set.Find(childName);
            if (child is null)
            {
                _unimplemented.Add($"{path}: emitted '{childName}' is not in this set");
                return;
            }

            (float emitPerTurn, string? emitModifier) = emitter.FloatWithModifier("Emit_Per_Turn");
            if (emitModifier is not null)
            {
                _unimplemented.Add(
                    $"{path}: Emit_Per_Turn modifier '{emitModifier}' " +
                    "(a ParamFunction curve) is authored but not applied, so this " +
                    "emitter runs at its unmodulated authored rate");
            }

            if (emitter.Reference("Mover") is { } mover)
            {
                _unimplemented.Add(
                    $"{path}: Mover '{mover}' is authored but not applied");
            }

            ParticleEmissionShape? shape = null;
            if (emitter.Reference("Shape") is { } shapeName)
            {
                ParticleDescriptor? shapeDescriptor = set.Find(shapeName);
                if (shapeDescriptor is null)
                {
                    _unimplemented.Add($"{path}: Shape '{shapeName}' is not in this set");
                }
                else
                {
                    shape = ReadShape(shapeDescriptor);
                }
            }

            // The emitter's own Life is in game turns and bounds its emission
            // window; Life 0 is a single burst. -2 occurs and means "does not
            // expire", which no finite plan can express, so it is bounded here
            // and the bound is declared.
            int emitterLife = emitter.Int("Life");
            bool unbounded = emitterLife < 0;
            int lastTurn = unbounded ? 0 : emitterLife;
            if (unbounded)
            {
                _unimplemented.Add(
                    $"{path}: emitter Life {emitterLife} does not expire; this plan " +
                    "emits only its first turn");
            }
            // The Int32 turn counter below wraps at this bound and never
            // terminates. Refuse it rather than run it or cap it silently.
            if (lastTurn == int.MaxValue)
            {
                throw new InvalidDataException(
                    "The source emitter loop cannot terminate when Life is Int32.MaxValue.");
            }

            // `Emit_Per_Turn` IS the particle count per game turn, and the
            // Shape supplies positions rather than a count.
            //
            // MEASURED, and it refutes the obvious alternative. `BE Respawn Air
            // Emitter` and `Big Rocket Explosion Emitter` both author
            // `Emit_Per_Turn 80.0` with `Life 0` over shapes whose own
            // `Num_Particles` is 10, and `Beam Charge Emitter 02` authors 2.0
            // over a 3-point shape. If the shape's count governed, those
            // authored rates would carry no information at all. Across the
            // corpus the Pass=1 rates run 0.2 to 80 and are plainly counts.
            //
            // `Pass_Num_Particles` is therefore NOT the count switch. Its
            // meaning is UNRESOLVED; the reading consistent with the above is
            // that it tells the shape to distribute the emitted count instead
            // of its own `Num_Particles`, which is how the consumer treats it.
            // Nothing here depends on that being right.
            double accumulator = 0;
            List<int> startTurns = [];
            for (int turn = 0; turn <= lastTurn; turn++)
            {
                accumulator += emitPerTurn;
                int thisTurn = (int)Math.Floor(accumulator);
                accumulator -= thisTurn;
                for (int index = 0; index < thisTurn; index++)
                {
                    startTurns.Add(startTurn + turn);
                }
            }

            if (startTurns.Count == 0)
            {
                _unimplemented.Add(FormattableString.Invariant(
                    $"{path}: Emit_Per_Turn {emitPerTurn} over Life {emitterLife} ") +
                    "emits no whole particle");
                return;
            }

            EmitterContext context = new(
                Shape: shape,
                StartTurns: startTurns,
                InitialVelocity: (
                    emitter.FloatWithModifier("Initial_Velocity_X").Value,
                    emitter.FloatWithModifier("Initial_Velocity_Y").Value,
                    emitter.FloatWithModifier("Initial_Velocity_Z").Value),
                OutwardVelocity: emitter.FloatWithModifier("Outward_Velocity").Value,
                VelocityRandomness: emitter.RetailDirectFloat("Velocity_Randomness"));

            Visit(child, $"{path} > {childName}", startTurn, context);
        }

        private void AddSprite(
            ParticleDescriptor sprite,
            string path,
            int startTurn,
            EmitterContext? emitter)
        {
            string? texture = sprite.Raw("Texture");
            if (texture is null)
            {
                // Exactly one shipped sprite omits Texture entirely.
                _unimplemented.Add($"{path}: sprite '{sprite.Name}' authors no Texture");
                return;
            }

            IReadOnlyList<int> starts = emitter?.StartTurns ?? [startTurn];
            int available = MaximumInstancesPerEffect - _instances;
            if (available <= 0)
            {
                _unimplemented.Add(
                    $"{path}: dropped entirely at the {MaximumInstancesPerEffect}-instance " +
                    "reconstruction bound");
                return;
            }

            if (starts.Count > available)
            {
                _unimplemented.Add(
                    $"{path}: authored {starts.Count} instances, kept {available} at the " +
                    $"{MaximumInstancesPerEffect}-instance reconstruction bound");
                starts = [.. starts.Take(available)];
            }

            _instances += starts.Count;

            int textureSize = sprite.Int("Texture_Size");
            int side = AtlasGridSide(textureSize);
            ParticleColourRange? colourRange = null;
            if (sprite.Reference("Colour_Range") is { } colourRangeName)
            {
                ParticleDescriptor? colour = set.Find(colourRangeName);
                if (colour is null)
                {
                    _unimplemented.Add(
                        $"{path}: Colour_Range '{colourRangeName}' is not in this set");
                }
                else
                {
                    colourRange = ReadColourRange(colour);
                }
            }

            if (sprite.Reference("Modifier") is { } modifier)
            {
                _unimplemented.Add(
                    $"{path}: sprite Modifier '{modifier}' is authored but not applied");
            }

            _layers.Add(new ParticleSpriteLayer
            {
                DescriptorName = sprite.Name,
                Path = path,
                TextureName = LeafTextureName(texture),
                BlendMode = sprite.Int("Blend_Mode"),
                AtlasColumns = side,
                AtlasRows = side,
                StartCell = sprite.Int("Texture_Number"),
                EndCell = sprite.Int("End_Frame"),
                AnimationMode = sprite.Int("Anim_Type") switch
                {
                    0 => ParticleAnimationMode.Static,
                    1 => ParticleAnimationMode.PlayOnce,
                    2 => ParticleAnimationMode.Loop,
                    var other => throw new InvalidDataException(
                        $"Sprite '{sprite.Name}' authored an unshipped Anim_Type {other}."),
                },
                AnimationCellsPerTurn = sprite.Float("Anim_Speed"),
                RandomStartCell = sprite.IntOrDefault("Random_Start_Frame", 0) != 0,
                LifeTurns = sprite.Int("Life"),
                StartRadius = sprite.FloatWithModifier("Radius").Value,
                FinalRadius = sprite.Float("Final_Radius"),
                LifeFraction = sprite.Float("Life_Pct"),
                FadeColour = sprite.Int("Fade_Col") != 0,
                AxisAligned = sprite.Int("Axis_Aligned"),
                Gravity = sprite.Int("Gravity") != 0,
                VelocityDamp = sprite.Float("Velocity_Damp"),
                ColourRange = colourRange,
                InstanceCount = starts.Count,
                StartTurns = starts,
                Shape = emitter?.Shape,
                InitialVelocity = emitter?.InitialVelocity ?? (0f, 0f, 0f),
                OutwardVelocity = emitter?.OutwardVelocity ?? 0f,
                VelocityRandomness = emitter?.VelocityRandomness ?? 0f,
            });
        }

        private static ParticleEmissionShape ReadShape(ParticleDescriptor shape) =>
            new(
                shape.Name,
                shape.Int("Type"),
                shape.Int("Ring_Axis"),
                shape.Int("Hemisphere"),
                shape.Int("Num_Particles"),
                shape.FloatWithModifier("Radius").Value,
                shape.IntOrDefault("Hollow", 0) != 0,
                (shape.Float("RandomSX"), shape.Float("RandomSY"), shape.Float("RandomSZ")));

        private static ParticleColourRange ReadColourRange(ParticleDescriptor range) =>
            new(
                range.Name,
                (range.FloatWithModifier("Start_Red").Value,
                    range.FloatWithModifier("Start_Green").Value,
                    range.FloatWithModifier("Start_Blue").Value),
                (range.FloatWithModifier("End_Red").Value,
                    range.FloatWithModifier("End_Green").Value,
                    range.FloatWithModifier("End_Blue").Value),
                (range.FloatWithModifier("Transition_Red").Value,
                    range.FloatWithModifier("Transition_Green").Value,
                    range.FloatWithModifier("Transition_Blue").Value),
                range.Int("Use_End") != 0,
                range.Int("Use_Transition") != 0,
                range.Float("Transition_Point"));

        private static string LeafTextureName(string authoredPath)
        {
            int slash = authoredPath.LastIndexOfAny(['\\', '/']);
            string leaf = slash < 0 ? authoredPath : authoredPath[(slash + 1)..];
            return leaf.ToLowerInvariant();
        }
    }

    private readonly record struct EmitterContext(
        ParticleEmissionShape? Shape,
        IReadOnlyList<int> StartTurns,
        (float X, float Y, float Z) InitialVelocity,
        float OutwardVelocity,
        float VelocityRandomness);
}
