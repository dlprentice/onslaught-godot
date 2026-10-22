// SPDX-License-Identifier: GPL-3.0-only
// The particle set as the game loads it, and the particle system run on it with a fixed camera and flat ground.

using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ParticleSystemTests
{
    private sealed class FlatWorld : IParticleWorld
    {
        public float EventTime { get; set; }

        public (FVector Pos, FMatrix Orientation)? Camera { get; set; } = (new FVector(0, -20, -5), FMatrix.Identity);

        public float FrameRenderFraction => 0.5f;

        public float MapCollide(FVector pos) => 0.0f;

        public float MeshRadius(string name) => 1.0f;
    }

    private static ParticleSet LoadMainSet()
    {
        var set = new ParticleSet();
        Assert.True(set.ReadData(GameData.Read(ParticleSet.InGameFile).Bytes));
        return set;
    }

    [GameDataFact]
    public void TheInGameSetLoadsEveryDescriptorInNameOrder()
    {
        byte[] file = GameData.Read(ParticleSet.InGameFile).Bytes;
        string header = System.Text.Encoding.Latin1.GetString(file).Split('\n')[2];
        int declared = int.Parse(header.Split(' ')[1]);
        ParticleSet set = LoadMainSet();
        Assert.Equal(declared, set.Count);
        int n = 0;
        for (ParticleDescriptor? pd = set.List; pd?.Next is not null; pd = pd.Next)
        {
            Assert.True(ParticleSet.Stricmp(pd.Name, pd.Next.Name) < 0, $"{pd.Name} before {pd.Next.Name}");
            n++;
        }

        Assert.Equal(declared - 1, n);
    }

    [Fact]
    public void VelocityRandomnessTakesTheSlotOfTheNameBeforeIt()
    {
        // Velocity_Randomness is read as a plain float, so the emitter's loader passes the last name's slot (Initial
        // Velocity Z's) to SetFloatRef: the randomness gets Z's function and Z keeps none.
        string file = string.Join("\r\n",
            "ParticleSystemEd_File_test", "File_Version 1.000000", "Num_Particle_Descriptors 2",
            "Particle_Descriptor_Type 2", "Particle_Descriptor_Name e",
            "Emit_Per_Turn 2.000000 NONE", "Initial_Velocity_X 0.000000 NONE", "Initial_Velocity_Y 0.000000 NONE",
            "Initial_Velocity_Z -0.500000 f", "Velocity_Randomness 0.250000",
            "*****************************************************************",
            "Particle_Descriptor_Type 10", "Particle_Descriptor_Name f", "Param_Function 3",
            "*****************************************************************", "");
        var set = new ParticleSet();
        Assert.True(set.ReadData(System.Text.Encoding.ASCII.GetBytes(file)));
        var e = (PdEmitter)set.FindByName("e")!;
        Assert.Equal(-0.5f, e.InitialVelocityZ.Value);
        Assert.Null(e.InitialVelocityZ.Ref);
        Assert.Equal(0.25f, e.VelocityRandomness.Value);
        Assert.Same(set.FindByName("f"), e.VelocityRandomness.Ref);
    }

    [GameDataFact]
    public void EveryFloatReferenceIsAFunction()
    {
        ParticleSet set = LoadMainSet();
        var bad = new List<string>();
        for (ParticleDescriptor? pd = set.List; pd is not null; pd = pd.Next)
        {
            foreach (System.Reflection.FieldInfo field in pd.GetType().GetFields())
            {
                if (field.GetValue(pd) is FloatRef { Ref: { } target } && target.Type != PdType.Function)
                {
                    bad.Add($"{pd.Name}.{field.Name} -> {target.Name} ({target.Type})");
                }
            }
        }

        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(10)));
    }

    [GameDataFact]
    public void EveryDescriptorRunsToTheEndOfItsEffect()
    {
        ParticleSet set = LoadMainSet();
        var world = new FlatWorld();
        var rand = new CrtRandStream();
        var manager = new ParticleManager(set, rand, world);
        int made = 0;
        for (ParticleDescriptor? pd = set.List; pd is not null; pd = pd.Next)
        {
            if (pd.Type is PdType.Mesh or PdType.FoR)
            {
                continue;
            }

            var link = new ParticleEffectLink(manager);
            manager.CreateEffect(pd, link, new FVector(0, 0, -2), false, false);
            for (int frame = 0; frame < 4; frame++)
            {
                world.EventTime += 0.05f;
                link.SetPos(new FVector(frame, 0, -2));
                manager.Process(0.5f, frame % 2 == 0);
            }

            link.Stop(false);
            link.Release();
            made++;
        }

        Assert.True(made > 1000);
        Assert.True(rand.Calls > 0);
        for (int frame = 0; frame < 4000 && manager.Process(1.0f, true); frame++)
        {
            world.EventTime += 0.05f;
        }

        Assert.True(manager.NumLiveParticles < 200, $"{manager.NumLiveParticles} particles still live");
    }
}
