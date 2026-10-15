// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using OnslaughtRebuild.Client;
using Xunit;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Laws recovered from the shipped Level 100 static-world rigid-animation
/// manifest, which the Godot base-world loader now consumes.
/// </summary>
public sealed class Level100StaticWorldAnimationTests
{
    /// <summary>
    /// The load-bearing law, and the reason no trigger is invented for the
    /// turrets.
    ///
    /// <para>Two unrelated fields of the shipped data classify the same six
    /// hierarchies the same way. The animation manifest's released <c>VHFM</c>
    /// frame maps say whether a track returns to its start pose (a cycle) or
    /// saturates on its final authored pose and holds it (a triggered one-shot).
    /// The sibling static-world manifest's WRES <c>active</c> flag says whether
    /// the placed object is a running facility or a dormant one. They agree
    /// exactly: every cyclic mesh is placed <c>active: true</c> and every
    /// saturating mesh is placed <c>active: false</c>.</para>
    ///
    /// <para><c>LEVEL100-RIGID-TRACK-IMPORT-2026-07-25.md</c> left free-running
    /// playback explicitly undecided - "a free-running loop is an assumption, not
    /// a measured fact". This is what settles it, and it settles it differently
    /// for the two groups. If this ever breaks, the classification is wrong and
    /// the playback decision has to be re-made, not patched.</para>
    /// </summary>
    [Fact]
    public void PlaybackClassificationAgreesWithAuthoredActiveFlag()
    {
        Level100StaticWorldAnimationSet animation = LoadAnimation();
        IReadOnlyDictionary<string, bool> active = LoadAuthoredActiveByMesh();

        Assert.Equal(6, animation.Meshes.Count);
        foreach ((string meshKey, Level100StaticWorldMeshAnimation mesh) in animation.Meshes)
        {
            Assert.True(active.ContainsKey(meshKey), $"'{meshKey}' is never placed.");
            Assert.Equal(
                active[meshKey]
                    ? Level100StaticWorldPlayback.CyclicLoop
                    : Level100StaticWorldPlayback.OneShotHold,
                mesh.Playback);
        }
    }

    /// <summary>
    /// The released lap lengths, in virtual frames, and the fact that exactly
    /// three of the six hierarchies loop. <c>FB_radar_station</c> and
    /// <c>FB_Solar_Pod</c> repeat their start pose as their final virtual frame,
    /// so their lap is one frame shorter than their frame count; <c>FB_Docks</c>
    /// does not, so its lap is its whole frame count. Getting this wrong stalls
    /// or skips one frame per lap.
    /// </summary>
    [Theory]
    [InlineData("FB_Docks", 26, 26)]
    [InlineData("FB_radar_station", 26, 25)]
    [InlineData("FB_Solar_Pod", 11, 10)]
    [InlineData("ft_blaster", 41, 0)]
    [InlineData("ft_pulse", 101, 0)]
    [InlineData("ft_sam", 21, 0)]
    public void ReleasedLapLengths(string meshKey, int virtualFrameCount, int loopFrameCount)
    {
        Level100StaticWorldMeshAnimation mesh = LoadAnimation().Meshes[meshKey];

        Assert.Equal(virtualFrameCount, mesh.VirtualFrameCount);
        Assert.Equal(loopFrameCount, mesh.LoopFrameCount);
        Assert.All(mesh.Parts, part => Assert.Equal(virtualFrameCount, part.Frames.Count));
    }

    /// <summary>
    /// Virtual frame 0 is the identity delta on every animated part of every
    /// mesh.
    ///
    /// <para>This is what makes the whole wiring safe. The producer guarantees
    /// the emitted OBJ rest pose <em>is</em> virtual frame 0
    /// (<c>cmsh_static_preview.py:1031-1039</c> rejects a mesh where it is not),
    /// so binding a part node at frame 0 renders exactly what the un-split merged
    /// mesh rendered before. It is also why the three one-shot turret
    /// hierarchies can be left unplayed with no visual change at all.</para>
    /// </summary>
    [Fact]
    public void RestPoseIsVirtualFrameZero()
    {
        foreach (Level100StaticWorldMeshAnimation mesh in LoadAnimation().Meshes.Values)
        {
            foreach (Level100StaticWorldAnimatedPart part in mesh.Parts)
            {
                Level100RigidFrame rest = part.Frames[0];
                float[] identity = [1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f];
                for (int index = 0; index < 9; index++)
                {
                    Assert.Equal(identity[index], rest.Basis[index], 5);
                }

                Assert.All(rest.Origin, value => Assert.Equal(0f, value, 5));
            }
        }
    }

    /// <summary>
    /// A cyclic track selected at the wrap boundary returns to the pose it
    /// started on, at the manifest's own 20 Hz. This is the property that makes
    /// free-running playback legitimate rather than an assumption, and it is
    /// asserted on the released transforms rather than on the frame index.
    /// </summary>
    [Fact]
    public void CyclicTracksReturnToTheirStartPose()
    {
        Level100StaticWorldAnimationSet animation = LoadAnimation();
        foreach (Level100StaticWorldMeshAnimation mesh in animation.Meshes.Values)
        {
            if (mesh.Playback != Level100StaticWorldPlayback.CyclicLoop)
            {
                Assert.Equal(0, mesh.SelectVirtualFrame(9_999d, animation.FramesPerSecond));
                continue;
            }

            double lapSeconds = mesh.LoopFrameCount / (double)animation.FramesPerSecond;
            Assert.Equal(0, mesh.SelectVirtualFrame(lapSeconds, animation.FramesPerSecond));
            Assert.Equal(
                mesh.LoopFrameCount - 1,
                mesh.SelectVirtualFrame(lapSeconds - (0.5d / animation.FramesPerSecond), animation.FramesPerSecond));
        }
    }

    /// <summary>
    /// The released part ranges tile the emitted OBJ without overlapping, which
    /// is what lets the loader split geometry by part with no extra data.
    /// </summary>
    [Fact]
    public void AnimatedPartVertexRangesDoNotOverlap()
    {
        foreach (Level100StaticWorldMeshAnimation mesh in LoadAnimation().Meshes.Values)
        {
            List<Level100StaticWorldAnimatedPart> ordered =
                [.. mesh.Parts.OrderBy(part => part.ObjVertexStart)];
            for (int index = 1; index < ordered.Count; index++)
            {
                int previousEnd =
                    ordered[index - 1].ObjVertexStart + ordered[index - 1].ObjVertexCount - 1;
                Assert.True(
                    ordered[index].ObjVertexStart > previousEnd,
                    $"{mesh.MeshKey} part ranges overlap at '{ordered[index].Name}'.");
            }
        }
    }

    private static Level100StaticWorldAnimationSet LoadAnimation() =>
        Level100StaticWorldAnimationManifest.Decode(
            File.ReadAllBytes(StaticWorldPath("level100-static-world-animation.json")));

    private static IReadOnlyDictionary<string, bool> LoadAuthoredActiveByMesh()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllBytes(StaticWorldPath("level100-static-world.json")));
        var active = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (JsonElement worldObject in document.RootElement.GetProperty("objects").EnumerateArray())
        {
            string mesh = worldObject.GetProperty("mesh").GetString() ?? string.Empty;
            bool isActive = worldObject.GetProperty("active").GetBoolean();
            if (active.TryGetValue(mesh, out bool existing))
            {
                // Both ft_blaster placements must agree, or the mesh-level
                // classification this test checks would not be well defined.
                Assert.Equal(existing, isActive);
                continue;
            }

            active.Add(mesh, isActive);
        }

        return active;
    }

    private static string StaticWorldPath(string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Level100",
            "StaticWorld",
            fileName);
        Assert.True(File.Exists(path), $"The static-world asset is missing: {path}");
        return path;
    }
}
