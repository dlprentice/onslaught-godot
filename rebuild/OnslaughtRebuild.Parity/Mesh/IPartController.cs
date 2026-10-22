// SPDX-License-Identifier: GPL-3.0-only
// A motion controller's part hooks, as CMotionController declares them (onslaught-decomp src/Motioncontroller.h). The base
// class's bodies do nothing (MotionController.cpp: Unknown3 004bae60, AdjustFrame, Unknown6 00452da0, Unknown7 004d6b20).

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>The hooks a thing's motion controller gives the mesh code for its parts.</summary>
public interface IPartController
{
    /// <summary>Slot 3: before a part's own keys apply (nothing in the base).</summary>
    void Unknown3(MeshPart part, ref FVector pos, ref FMatrix ori, bool world)
    {
    }

    /// <summary>Slot 4: after a part's own keys apply.</summary>
    void GetPartTransform(MeshPart part, ref FVector pos, ref FMatrix ori, bool world)
    {
    }

    /// <summary>Slot 5: the part's animation time.</summary>
    void AdjustFrame(MeshPart part, ref float time)
    {
    }

    /// <summary>Slot 6: flag bit 0 hides the part (from collision and drawing).</summary>
    void Unknown6(MeshPart part, ref int flags)
    {
    }

    /// <summary>Slot 7: the part's damage level (that many steps along its chain).</summary>
    int Unknown7(MeshPart part) => 0;

    /// <summary>Slot 9: the animation loops (FALSE in the base).</summary>
    bool Unknown9() => false;
}
