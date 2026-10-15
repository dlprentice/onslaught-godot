// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/tree.h and src/tree.cpp (CTree::Init 004f6080, SetThingType). Not in Stuart's release.

using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CTree: a tree (one draw for its variation, one more when its type has no variant number).</summary>
public sealed class Tree(ThingContext context) : Thing(context, Oid.Tree)
{
    public int Variation;
    public float TreeLife;
    public byte[] MeshName = [];

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Tree | ThingType.Stationary);

    public override void Init(InitThing init)
    {
        Variation = (int)((float)(Context.Random.Random() % 65536) * (1.0f / 2048.0f));
        init.Pos.Z = Context.Map.Collide(init.Pos);
        init.Orientation = FMatrix.Identity;
        init.OrientationType = OrientationType.DirectionCosineMatrix;
        var name = new List<byte>(init.TreeType);
        if (!init.TreeTypeComplete)
        {
            name.AddRange(System.Text.Encoding.ASCII.GetBytes((Context.Random.Random() % 4).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        name.AddRange(".MSH"u8.ToArray());
        MeshName = name.ToArray();
        // CreateRenderThing(2): a CRTTree, with the mesh of that name (CMesh::GetMesh, ignoring case); its radius and
        // bounding box size the tree's map-who entry. Its imposter and slope are rendering only.
        string meshName = System.Text.Encoding.ASCII.GetString(MeshName);
        if (!Context.Meshes.TryGetValue(meshName, out Mesh.MeshData? mesh))
        {
            throw new InvalidOperationException($"tree mesh '{meshName}' is not in the level resource file");
        }

        RenderThing = new RenderThing(Context, mesh, NoSubject.Instance);
        string text = System.Text.Encoding.ASCII.GetString(MeshName);
        if (text.StartsWith("fern", StringComparison.OrdinalIgnoreCase) || text.StartsWith("bush", StringComparison.OrdinalIgnoreCase))
        {
            init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        }

        base.Init(init);
        TreeLife = 1.5f;
    }
}

/// <summary>A render subject for things whose pose the simulation never evaluates (trees).</summary>
internal sealed class NoSubject : Mesh.IRenderSubject
{
    public static readonly NoSubject Instance = new();

    public int RealAnimIndex => -1;

    public float RenderFrame => 0;

    public FVector RenderPos => default;

    public FMatrix RenderOrientation => FMatrix.Identity;

    public bool HasMotionController => false;

    public bool RenderUseHierarchy(int partNumber) => true;
}
