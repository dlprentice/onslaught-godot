// SPDX-License-Identifier: GPL-3.0-only
// FrontEndData.cpp (004687e0/004691c0), FrontEnd.cpp (00466ba0), DXFrontend.cpp (00540c30/00540fb0).
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>The front end's data and world adapter for the same particle manager used by levels.</summary>
public sealed class FrontEndParticles : IParticleWorld
{
    public const string ResourceFile = @"data\Resources\Frontend_res_PC.aya";
    private readonly FrontEnd _frontEnd;

    public FrontEndParticles(FrontEnd frontEnd, ParticleSet set, IReadOnlyDictionary<string, MeshData> meshes)
    {
        _frontEnd = frontEnd;
        Meshes = meshes;
        Manager = new ParticleManager(set, (frontEnd.Crt ?? throw new InvalidOperationException(
            "front-end particles need the process's CRT state")).MainThread, this, frontEnd.ParticleState);
    }

    public ParticleManager Manager { get; }
    public IReadOnlyDictionary<string, MeshData> Meshes { get; }
    public float EventTime => _frontEnd.Events.Time;
    public (FVector Pos, FMatrix Orientation)? Camera { get; set; } = (new(0, -20, 0), FMatrix.Identity);
    public float FrameRenderFraction => Manager.State.RenderFraction;
    public float MapCollide(FVector pos) => throw new NotSupportedException(
        "front-end particle ground collision has no admitted map; the shipped goodie/firework effects do not use it");
    public float MeshRadius(string name) => Meshes.TryGetValue(name, out MeshData? mesh) ? mesh.Radius :
        throw new NotSupportedException($"particle mesh '{name}' is not in the front-end resource file");

    public static FrontEndParticles Load(FrontEnd frontEnd, Func<string, (string Name, byte[] Bytes)> read)
    {
        (string name, byte[] bytes) = read(ResourceFile);
        Dictionary<string, MeshData> meshes = LevelResources.ReadMeshes(name, bytes, read);
        var set = new ParticleSet();
        if (!set.ReadData(read(ParticleSet.FrontEndFile).Bytes)) throw new InvalidDataException("invalid front-end particle set");
        return new FrontEndParticles(frontEnd, set, meshes);
    }

    /// <summary>RenderParticles: interpolation, mesh drawing, then RenderOther. Drawing stays with the host.</summary>
    public void Render()
    {
        Manager.DoInterpolation();
        Manager.State.RenderOther(Manager);
    }
}
