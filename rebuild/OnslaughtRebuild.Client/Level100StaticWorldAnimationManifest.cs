// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;

namespace OnslaughtRebuild.Client;

/// <summary>
/// Bounded decoder for the exact locally materialized Level 100 static-world
/// rigid-animation manifest
/// (<c>Assets/Level100/StaticWorld/level100-static-world-animation.json</c>,
/// schema <c>onslaught.level100-static-world-animation.v1</c>).
///
/// <para>Produced by <c>rebuild/tools/materialize_retail_assets.py</c>: the
/// emitter is at lines 2942-2965 and 3087-3124, and the decode itself is
/// <c>build_rigid_transform_tracks</c> in <c>rebuild/tools/cmsh_static_preview.py</c>
/// (lines 958-1102). The released inputs are the six Level 100 <c>.msh.aya</c>
/// hierarchies' <c>CMSH MESP</c> <c>HORI</c>/<c>HPOS</c> pose tables, selected
/// per virtual frame by <c>VHFM</c> and composed along <c>PRNT</c> root-to-leaf.</para>
///
/// <para>This is presentation input only. Nothing here reaches
/// <c>OnslaughtRebuild.Core</c>, moves the simulation state hash, or is observed
/// by the mission: these are decorative rigid part tracks on base-world scenery.</para>
/// </summary>
public static class Level100StaticWorldAnimationManifest
{
    /// <summary>
    /// Pinned by the producer at
    /// <c>rebuild/tools/materialize_retail_assets.py:157</c>
    /// (<c>STATIC_WORLD_ANIMATION_SHA256</c>), which re-derives it over the
    /// canonical serialization and fails the materialize run on any mismatch.
    /// </summary>
    public const string ExpectedManifestSha256 =
        "B826DC2D1F62069E3285F4F8CDDEAFB84511519B32065CF6C028615498AFA9C3";

    private const string ExpectedSchema = "onslaught.level100-static-world-animation.v1";

    /// <summary>
    /// The released base update rate, and the only timebase this manifest
    /// carries. It is <c>framesPerSecond</c> in the shipped file
    /// (<c>rebuild/tools/materialize_retail_assets.py:156</c>,
    /// <c>STATIC_WORLD_ANIMATION_HZ = 20</c>).
    ///
    /// <para>The file's own <c>provenance.rate</c> string is explicit that
    /// <em>the mesh format stores no rate</em> - 20 Hz is the released engine's
    /// base update, the same figure
    /// <c>FirstFlightWorldView.RetailAquilaAnimationHz</c> already uses for the
    /// Aquila hierarchy. It is therefore shipped data, but it is shipped
    /// engine-level data rather than a per-mesh authored field. No other rate is
    /// introduced here.</para>
    /// </summary>
    public const int RetailHierarchyPlaybackHz = 20;

    /// <summary>
    /// The exact released virtual-frame counts of every Level 100 static mesh
    /// carrying more than one authored hierarchy pose, mirroring the producer's
    /// pinned <c>STATIC_WORLD_ANIMATED_MESHES</c> table at
    /// <c>rebuild/tools/materialize_retail_assets.py:162-169</c>. These are
    /// playback lengths read from the released <c>vFrames</c> field; nothing is
    /// resampled.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> ExpectedVirtualFrameCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["FB_Docks"] = 26,
            ["FB_Solar_Pod"] = 11,
            ["FB_radar_station"] = 26,
            ["ft_blaster"] = 41,
            ["ft_pulse"] = 101,
            ["ft_sam"] = 21,
        };

    private const int MaximumManifestBytes = 512_000;

    public static Level100StaticWorldAnimationSet Decode(ReadOnlySpan<byte> manifestBytes)
    {
        if (manifestBytes.Length is < 1 or > MaximumManifestBytes ||
            !StringComparer.Ordinal.Equals(
                Convert.ToHexString(SHA256.HashData(manifestBytes)),
                ExpectedManifestSha256))
        {
            throw new InvalidDataException(
                "The locally materialized Level 100 static-world animation manifest is missing or changed.");
        }

        Manifest manifest = JsonSerializer.Deserialize<Manifest>(
                manifestBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException(
                "The Level 100 static-world animation manifest is empty.");

        if (!StringComparer.Ordinal.Equals(manifest.Schema, ExpectedSchema) ||
            manifest.FramesPerSecond != RetailHierarchyPlaybackHz ||
            manifest.Meshes.Count != ExpectedVirtualFrameCounts.Count)
        {
            throw new InvalidDataException(
                "The Level 100 static-world animation schema, rate or mesh count changed.");
        }

        var meshes = new Dictionary<string, Level100StaticWorldMeshAnimation>(StringComparer.Ordinal);
        foreach ((string meshKey, MeshRecord record) in manifest.Meshes)
        {
            if (!ExpectedVirtualFrameCounts.TryGetValue(meshKey, out int expectedFrames) ||
                record.VirtualFrameCount != expectedFrames)
            {
                throw new InvalidDataException(
                    $"The Level 100 static-world animated mesh set changed at '{meshKey}'.");
            }

            meshes.Add(meshKey, DecodeMesh(meshKey, record));
        }

        return new Level100StaticWorldAnimationSet(manifest.FramesPerSecond, meshes);
    }

    private static Level100StaticWorldMeshAnimation DecodeMesh(string meshKey, MeshRecord record)
    {
        // `frameMaps` is the raw released VHFM byte array of every *directly*
        // authored mover, keyed by part index: one entry per virtual frame,
        // giving the HORI/HPOS pose that plays on that frame. Its shape is what
        // separates a released idle cycle from a released one-shot, and both
        // tests below are exact integer comparisons on shipped bytes - no
        // tolerance and no threshold is introduced.
        //
        //   holdsAtEnd     - the map's last two entries select the same pose, so
        //                    the track has stopped moving before the virtual
        //                    frame budget runs out. That trailing saturation is
        //                    the signature of a triggered one-shot whose final
        //                    pose is held: ft_sam holds pose 9 for 11 frames,
        //                    ft_blaster pose 17 for 22, ft_pulse pose 17 for 83.
        //   closesOnStart  - the map's first and last entries select the same
        //                    pose, so the last virtual frame duplicates the
        //                    first and the cycle is one frame shorter than the
        //                    frame count.
        //
        // Measured over the shipped file, those two flags partition the six
        // meshes 3/3, and that partition is confirmed independently by the WRES
        // `active` flag in the sibling level100-static-world.json: the three
        // cyclic meshes are the three active:true facilities (Forseti Docks,
        // Radar Station, Forseti Solar Pod) and the three saturating meshes are
        // the four active:false turret placements (Turret 01-04). Two unrelated
        // fields of the shipped data agree, which is why no trigger is invented
        // for the turrets here.
        if (record.FrameMaps.Count == 0)
        {
            throw new InvalidDataException(
                $"The Level 100 static-world animation for '{meshKey}' has no released frame map.");
        }

        bool holdsAtEnd = true;
        bool closesOnStart = true;
        foreach (int[] frameMap in record.FrameMaps.Values)
        {
            if (frameMap.Length != record.VirtualFrameCount || frameMap.Length < 2)
            {
                throw new InvalidDataException(
                    $"The Level 100 static-world frame map for '{meshKey}' does not cover its virtual frames.");
            }

            holdsAtEnd &= frameMap[^1] == frameMap[^2];
            closesOnStart &= frameMap[0] == frameMap[^1];
        }

        Level100StaticWorldPlayback playback = holdsAtEnd
            ? Level100StaticWorldPlayback.OneShotHold
            : Level100StaticWorldPlayback.CyclicLoop;

        // A cycle that closes on its start pose repeats that pose as its last
        // virtual frame, so playing it would stall for one frame every lap.
        int loopFrameCount = playback == Level100StaticWorldPlayback.CyclicLoop
            ? (closesOnStart ? record.VirtualFrameCount - 1 : record.VirtualFrameCount)
            : 0;

        var parts = new List<Level100StaticWorldAnimatedPart>();
        foreach (PartRecord part in record.Parts)
        {
            if (part.Frames is null)
            {
                continue;
            }

            if (part.Frames.Length != record.VirtualFrameCount ||
                part.ObjVertexStart < 1 ||
                part.ObjVertexCount < 1 ||
                part.ObjVertexStart + part.ObjVertexCount - 1 > record.ObjVertexCount)
            {
                throw new InvalidDataException(
                    $"The Level 100 static-world animated part '{meshKey}/{part.Name}' is inconsistent.");
            }

            var frames = new Level100RigidFrame[part.Frames.Length];
            for (int index = 0; index < frames.Length; index++)
            {
                FrameRecord frame = part.Frames[index];
                if (frame.Basis.Length != 9 || frame.Origin.Length != 3 ||
                    Array.Exists(frame.Basis, value => !double.IsFinite(value)) ||
                    Array.Exists(frame.Origin, value => !double.IsFinite(value)))
                {
                    throw new InvalidDataException(
                        $"The Level 100 static-world animated part '{meshKey}/{part.Name}' has an invalid frame.");
                }

                frames[index] = new Level100RigidFrame(
                    Array.ConvertAll(frame.Basis, value => (float)value),
                    Array.ConvertAll(frame.Origin, value => (float)value));
            }

            parts.Add(new Level100StaticWorldAnimatedPart(
                part.Part,
                part.Name,
                part.ObjVertexStart,
                part.ObjVertexCount,
                frames));
        }

        if (parts.Count == 0)
        {
            throw new InvalidDataException(
                $"The Level 100 static-world animation for '{meshKey}' binds no part.");
        }

        return new Level100StaticWorldMeshAnimation(
            meshKey,
            record.VirtualFrameCount,
            playback,
            loopFrameCount,
            record.ResourcePath,
            parts);
    }

    private sealed record Manifest
    {
        public int FramesPerSecond { get; init; }

        public Dictionary<string, MeshRecord> Meshes { get; init; } = [];

        public string Schema { get; init; } = string.Empty;
    }

    private sealed record MeshRecord
    {
        public Dictionary<string, int[]> FrameMaps { get; init; } = [];

        public int ObjVertexCount { get; init; }

        public PartRecord[] Parts { get; init; } = [];

        public string ResourcePath { get; init; } = string.Empty;

        public int VirtualFrameCount { get; init; }
    }

    private sealed record PartRecord
    {
        public FrameRecord[]? Frames { get; init; }

        public string Name { get; init; } = string.Empty;

        public int ObjVertexCount { get; init; }

        public int ObjVertexStart { get; init; }

        public int Part { get; init; }
    }

    private sealed record FrameRecord
    {
        public double[] Basis { get; init; } = [];

        public double[] Origin { get; init; } = [];
    }
}

/// <summary>
/// How the released data says a static-world hierarchy track is meant to run.
/// Derived from the shipped <c>VHFM</c> frame maps; see
/// <see cref="Level100StaticWorldAnimationManifest"/>.
/// </summary>
public enum Level100StaticWorldPlayback
{
    /// <summary>
    /// The track returns to its start pose, so it runs free and repeats. The
    /// three <c>active: true</c> Forseti facilities.
    /// </summary>
    CyclicLoop,

    /// <summary>
    /// The track saturates on its final authored pose and holds it, which is a
    /// triggered one-shot rather than an idle. The four <c>active: false</c>
    /// turret placements. No trigger owner exists in this reconstruction, so
    /// these are held at their authored rest pose and are not played.
    /// </summary>
    OneShotHold,
}

/// <summary>One released per-part rigid delta, in OBJ space.</summary>
/// <param name="Basis">
/// Nine floats, row-major (<c>[r00,r01,r02, r10,r11,r12, r20,r21,r22]</c>),
/// applied as <c>v' = Basis * v + Origin</c> to that part's OBJ rest vertices.
/// Already composed root-to-leaf, so a part's delta must NOT be chained through
/// its parent again.
/// </param>
/// <param name="Origin">Three floats, same space.</param>
public readonly record struct Level100RigidFrame(float[] Basis, float[] Origin);

/// <summary>One released hierarchy part that carries a rigid track.</summary>
public sealed record Level100StaticWorldAnimatedPart(
    int Part,
    string Name,
    int ObjVertexStart,
    int ObjVertexCount,
    IReadOnlyList<Level100RigidFrame> Frames);

/// <summary>One released mesh hierarchy's complete rigid animation.</summary>
public sealed class Level100StaticWorldMeshAnimation(
    string meshKey,
    int virtualFrameCount,
    Level100StaticWorldPlayback playback,
    int loopFrameCount,
    string resourcePath,
    IReadOnlyList<Level100StaticWorldAnimatedPart> parts)
{
    public string MeshKey { get; } = meshKey;

    public int VirtualFrameCount { get; } = virtualFrameCount;

    public Level100StaticWorldPlayback Playback { get; } = playback;

    /// <summary>
    /// Virtual frames in one lap, for a <see cref="Level100StaticWorldPlayback.CyclicLoop"/>;
    /// zero otherwise.
    /// </summary>
    public int LoopFrameCount { get; } = loopFrameCount;

    public string ResourcePath { get; } = resourcePath;

    public IReadOnlyList<Level100StaticWorldAnimatedPart> Parts { get; } = parts;

    /// <summary>
    /// The virtual frame showing at <paramref name="elapsedSeconds"/> of
    /// presentation time. Snap only - the released playback selects a stored
    /// pose per virtual frame through <c>VHFM</c> and never interpolates, so
    /// neither does this.
    ///
    /// <para>A <see cref="Level100StaticWorldPlayback.OneShotHold"/> track has no
    /// trigger owner in this reconstruction and stays on virtual frame 0, which
    /// the producer guarantees is the OBJ rest pose.</para>
    /// </summary>
    public int SelectVirtualFrame(double elapsedSeconds, int framesPerSecond)
    {
        if (Playback != Level100StaticWorldPlayback.CyclicLoop ||
            !double.IsFinite(elapsedSeconds) ||
            elapsedSeconds <= 0d ||
            LoopFrameCount < 1)
        {
            return 0;
        }

        double frames = Math.Floor(elapsedSeconds * framesPerSecond);
        if (frames <= 0d || frames >= long.MaxValue)
        {
            return 0;
        }

        return (int)((long)frames % LoopFrameCount);
    }
}

/// <summary>The decoded Level 100 static-world rigid-animation manifest.</summary>
public sealed class Level100StaticWorldAnimationSet(
    int framesPerSecond,
    IReadOnlyDictionary<string, Level100StaticWorldMeshAnimation> meshes)
{
    public int FramesPerSecond { get; } = framesPerSecond;

    public IReadOnlyDictionary<string, Level100StaticWorldMeshAnimation> Meshes { get; } = meshes;
}
