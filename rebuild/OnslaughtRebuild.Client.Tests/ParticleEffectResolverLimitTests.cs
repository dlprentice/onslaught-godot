// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>Resolution limits that no shipped emitter reaches.</summary>
public sealed class ParticleEffectResolverLimitTests
{
    [Fact]
    public async Task EmitterLifeAtInt32MaximumIsRefusedInsteadOfLoopingForever()
    {
        ParticleSetFile set = ParticleSetFile.Parse(
            "ParticleSystemEd_File_synthetic\r\nFile_Version 1.000000\r\nNum_Particle_Descriptors 2\r\n" +
            Record(2, "Emitter", "Particle_Descriptor Sprite", "Emit_Per_Turn 0",
                "Life " + int.MaxValue.ToString(CultureInfo.InvariantCulture), "Shape NONE", "Mover NONE",
                "Initial_Velocity_X 1 NONE", "Initial_Velocity_Y 2 NONE", "Initial_Velocity_Z 3 NONE",
                "Outward_Velocity 0.5 NONE", "Velocity_Randomness 0.25 NONE") +
            Record(1, "Sprite", "Texture C:\\synthetic\\PARTICLE\\Fire.TGA", "Texture_Size 2", "Blend_Mode 0",
                "Texture_Number 1", "End_Frame 15", "Anim_Type 1", "Anim_Speed 1.4", "Random_Start_Frame 0", "Life 10",
                "Radius 0.3 NONE", "Final_Radius 1.5", "Life_Pct 0.75", "Fade_Col 1", "Axis_Aligned 0", "Gravity 0",
                "Velocity_Damp 0.125", "Colour_Range NONE", "Modifier NONE"));

        // The unguarded Int32 turn counter wraps and never ends, so resolve on
        // a worker with a deadline instead of hanging the test run.
        Task<ParticleEffectPlan> resolution = Task.Run(() => ParticleEffectResolver.Resolve(set, "Emitter"));
        Task finished = await Task.WhenAny(resolution, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(finished == resolution, "Resolution did not finish; the emitter turn loop is unguarded.");
        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => resolution);
        Assert.Equal("The source emitter loop cannot terminate when Life is Int32.MaxValue.", error.Message);
    }

    private static string Record(int type, string name, params string[] lines) =>
        "Particle_Descriptor_Type " + type.ToString(CultureInfo.InvariantCulture) + "\r\nParticle_Descriptor_Name " + name + "\r\n" +
        string.Concat(lines.Select(line => line + "\r\n")) + ParticleSetFile.RecordSeparator + "\r\n";
}
