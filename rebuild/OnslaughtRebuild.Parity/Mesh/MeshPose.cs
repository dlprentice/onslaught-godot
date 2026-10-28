// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/MeshPose.cpp (CMeshPose::Build 004b4ba0, Update 004b4cd0, CMeshPart::EvaluatePose
// 004b4de0), src/meshpose.h (CMeshPose, Init), src/MeshRenderer.cpp (CMeshPart::EvaluateAnimatedTransform 004b5330)
// and src/rtmesh.cpp (CRTMesh::GetRTEmitter 004dd160). MeshPose.cpp and MeshRenderer.cpp are in Stuart's release;
// rtmesh.cpp is not.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>
/// What the mesh code asks of the thing it draws (IRenderableThing): its animation, frame, placement and per-part
/// hierarchy flag. Supported motion controllers expose their part hooks; unmodeled controller behavior is explicitly
/// refused instead of evaluating without it.
/// </summary>
public interface IRenderSubject
{
    /// <summary>GetRealAnimIndex: the position of the current animation in the mesh's table, or -1.</summary>
    int RealAnimIndex { get; }

    /// <summary>GetRenderFrame: the animation time.</summary>
    float RenderFrame { get; }

    /// <summary>GetRenderPos.</summary>
    FVector RenderPos { get; }

    /// <summary>GetRenderOrientation.</summary>
    FMatrix RenderOrientation { get; }

    /// <summary>Unsupported controller behavior remains: the pose code refuses it. Modeled hooks use PartController.</summary>
    bool HasMotionController { get; }

    /// <summary>RenderUseHierarchy(part number).</summary>
    bool RenderUseHierarchy(int partNumber);

    /// <summary>The motion controller's part hooks, for the controllers the core models.</summary>
    IPartController? PartController => null;
}

/// <summary>
/// CMeshPose: a render thing's cached pose, one transform per part of its mesh, rebuilt at most once per render frame
/// (and otherwise only every 30 render frames unless forced). A CRTMesh makes one for its mesh when it is initialised.
/// </summary>
public sealed class MeshPose
{
    public MeshPose(int numParts)
    {
        NumParts = numParts;
        Orientations = new FMatrix[numParts];
        Positions = new FVector[numParts];
        Frames = new int[numParts];
        Parents = new int[numParts];
        Array.Fill(Parents, -1);
    }

    public int NumParts { get; }

    /// <summary>Retail allocates these without clearing them: a part Build never reaches holds whatever the heap held.</summary>
    public FMatrix[] Orientations { get; }

    public FVector[] Positions { get; }

    public int[] Frames { get; }

    public int[] Parents { get; }

    public int LastFrame { get; private set; } = -9999;

    public float LastTime { get; private set; } = -99999.0f;

    /// <summary>CMeshPose::Update: rebuilds the pose from the root part unless this render frame already has one.</summary>
    public bool Update(MeshData mesh, IRenderSubject subject, int renderFrameNumber, bool force)
    {
        int frame = renderFrameNumber;
        if (frame == LastFrame)
        {
            return false;
        }

        if (frame < LastFrame + 30 && !force)
        {
            return false;
        }

        RequireNoMotionController(subject);
        float time = 0;
        int anim = subject.RealAnimIndex;
        LastFrame = -9999;
        if (mesh.NumAnimModes != 0 && anim >= 0)
        {
            AnimMode ma = mesh.AnimModes[anim];
            float t = subject.RenderFrame;
            time = (ma.FrameScaler * t) + ma.Start;
        }

        Build(new FVector(0, 0, 0), FMatrix.Identity, mesh.Parts[0], subject, 0);
        LastFrame = frame;
        LastTime = time;
        return true;
    }

    /// <summary>CMeshPose::Build: a part and its children, each from its parent's transform.</summary>
    public void Build(FVector pos, FMatrix ori, MeshPart part, IRenderSubject? subject, int frame)
    {
        int f = frame;
        MeshPart parent = EvaluateAnimatedTransform(subject, ref pos, ref ori, part, ref f);
        CheckPartNumber(part.PartNumber);
        Orientations[part.PartNumber] = ori;
        Positions[part.PartNumber] = pos;
        Parents[part.PartNumber] = parent.PartNumber;
        Frames[part.PartNumber] = f;
        for (int i = 0; i < part.NumChildren; i++)
        {
            Build(pos, ori, part.Children[i]!, subject, f);
        }
    }

    /// <summary>
    /// CMeshPart::EvaluateAnimatedTransform: the part's key at the subject's animation frame, interpolated towards the
    /// next frame, applied to the parent's transform in <paramref name="pos"/> and <paramref name="ori"/>. A part
    /// controller (a building's segments) adjusts the frame, sees the parent's transform first, says whether the
    /// animation loops, adjusts the result and sets the part's flags; a damaged part would swap in its chain's (not
    /// ported). Returns the part to draw. Bit 3 of <paramref name="frame"/> selects the mesh renderer's global animation,
    /// which the simulation never sets.
    /// </summary>
    public static MeshPart EvaluateAnimatedTransform(IRenderSubject? subject, ref FVector pos, ref FMatrix ori, MeshPart part,
        ref int frame)
    {
        if (subject is not null)
        {
            RequireNoMotionController(subject);
        }

        IPartController? mc = subject?.PartController;
        if ((frame & 8) == 8)
        {
            throw new NotSupportedException("the mesh renderer's global animation (frame bit 3) is a rendering path");
        }

        int anim = subject?.RealAnimIndex ?? -1;
        float time = 0;
        if (part.NumVFrames > 1 && part.Mesh.NumAnimModes != 0 && anim >= 0)
        {
            AnimMode ma = part.Mesh.AnimModes[anim];
            if (subject is not null)
            {
                time = subject.RenderFrame;
            }

            time = ma.FrameScaler * time;
            time = ma.Start + time;
        }

        mc?.AdjustFrame(part, ref time);
        int f0 = X87.Fist(Math.Floor(time));
        float t = time - MathF.Floor(time);
        if (f0 >= part.NumVFrames)
        {
            f0 = part.NumVFrames - 1;
        }

        mc?.Unknown3(part, ref pos, ref ori, true);
        bool loop = mc?.Unknown9() ?? true;
        byte k0 = part.VHFrameMap[f0];
        int f1 = f0 + 1;
        if (f1 >= part.NumVFrames)
        {
            f1 = loop ? 0 : part.NumVFrames - 1;
        }

        byte k1 = part.VHFrameMap[f1];
        FVector p = part.GetKeyPosition(k0) + ((part.GetKeyPosition(k1) - part.GetKeyPosition(k0)) * t);
        FMatrix o = MatrixOps.Add(part.GetHFrameOrientation(k0),
            MatrixOps.Scale(MatrixOps.Sub(part.GetHFrameOrientation(k1), part.GetHFrameOrientation(k0)), t));

        if (subject is null || subject.RenderUseHierarchy(part.PartNumber))
        {
            pos += ori.Transform(p);
            ori = ori.Multiply(o);
        }
        else
        {
            pos += p;
            ori = o;
        }

        if (mc is not null)
        {
            mc.GetPartTransform(part, ref pos, ref ori, true);
            mc.Unknown6(part, ref frame);
            if (mc.Unknown7(part) > 0)
            {
                throw new NotSupportedException("EvaluateAnimatedTransform: a damaged part's chained stage is not ported");
            }
        }

        return part;
    }

    /// <summary>
    /// CMeshPart::EvaluatePose: the part's transform for the subject's current frame. With a pose and a render frame
    /// number above 1 it comes from the pose (rebuilt if due), relative to the thing when <paramref name="local"/>,
    /// else placed in the world; otherwise from GetWorldTransforms at the resolved frame.
    /// </summary>
    public static void EvaluatePose(MeshData? mesh, MeshPose? pose, IRenderSubject subject, MeshPart? part, MeshTransformStatics statics,
        out FVector pos, out FMatrix ori, bool local, bool force)
    {
        pos = new FVector(0, 0, 0);
        ori = FMatrix.Identity;
        if (mesh is null || part is null)
        {
            return;
        }

        if (pose is not null && statics.RenderFrameNumber > 1)
        {
            pose.Update(mesh, subject, statics.RenderFrameNumber, force);
            pose.CheckPartNumber(part.PartNumber);
            pos = pose.Positions[part.PartNumber];
            ori = pose.Orientations[part.PartNumber];
            if (!local)
            {
                FVector tpos = subject.RenderPos;
                FMatrix tori = subject.RenderOrientation;
                FVector p = pos;
                pos = tpos + MatrixOps.CachedPoseWorldPosition(tori, p);
                ori = MatrixOps.CachedPoseWorldOrientation(tori, ori);
            }

            return;
        }

        RequireNoMotionController(subject);
        int frame = subject.RealAnimIndex >= 0
            ? part.ResolveFrameNumber(subject.RenderFrame, subject.RealAnimIndex, out _, subject.PartController)
            : 0;
        part.GetWorldTransforms(frame, frame, 0, subject, out ori, out pos, local, uncached: false, statics);
    }

    /// <summary>
    /// CRTMesh::GetRTEmitter: the transform of the emitter with that name and number (the origin and identity when the
    /// mesh has no such emitter). The simulation's thruster, dust and vent counts call it local and unforced, and stop
    /// at the first zero position.
    /// </summary>
    public static (FVector Pos, FMatrix Ori) GetRTEmitter(MeshData? mesh, MeshPose? pose, IRenderSubject subject, string name,
        int index, MeshTransformStatics statics, bool local = true, bool force = false)
    {
        FVector pos = new(0, 0, 0);
        FMatrix ori = FMatrix.Identity;
        if (mesh is not null)
        {
            MeshPart? part = mesh.GetEmitterSubpart(name, index);
            if (part is not null)
            {
                EvaluatePose(mesh, pose, subject, part, statics, out pos, out ori, local, force);
            }
        }

        return (pos, ori);
    }

    /// <summary>
    /// A part found through a chained (lower-detail) mesh carries that mesh's part number, but retail indexes the base
    /// mesh's pose with it; a number past the pose's arrays would read past them in retail.
    /// </summary>
    private void CheckPartNumber(int partNumber)
    {
        if (partNumber < 0 || partNumber >= NumParts)
        {
            throw new InvalidOperationException($"part number {partNumber} is outside the pose's {NumParts} parts (retail reads past them)");
        }
    }

    private static void RequireNoMotionController(IRenderSubject subject)
    {
        if (subject.HasMotionController)
        {
            throw new NotSupportedException("motion controllers are not ported yet");
        }
    }
}
