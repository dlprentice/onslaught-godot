// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client;

public readonly record struct Level100RenderVector3(
    float X,
    float Y,
    float Z);

public readonly record struct Level100RenderBasis3(
    Level100RenderVector3 XAxis,
    Level100RenderVector3 YAxis,
    Level100RenderVector3 ZAxis);

public readonly record struct Level100TargetVisualBinding(
    string DefinitionName,
    string MeshBinding);

public readonly record struct Level100TargetVisualDescriptor(
    Level100ActorId ActorId,
    string DefinitionName,
    string MeshBinding,
    bool Visible,
    Level100RenderVector3 Position,
    Level100RenderBasis3 Basis)
{
    public Level100TargetVisualBinding Binding =>
        new(DefinitionName, MeshBinding);
}

/// <summary>
/// Presentation-only conversion from the canonical Core actor pose into the
/// right-handed world transform consumed by the Godot client.
/// </summary>
public static class Level100TargetPresentation
{
    private const float MillimetersToMeters = 0.001f;

    public static Level100TargetVisualBinding TargetTankBinding { get; } =
        new("Target Tank", "m_f_pulsetank_training.msh.aya");

    public static Level100TargetVisualBinding TargetTruckBinding { get; } =
        new("Target Truck", "m_f_truck_training.msh.aya");

    public static Level100TargetVisualBinding WarehouseBinding { get; } =
        new("Warehouse", "m_m_warehouse.msh.aya");

    /// <summary>
    /// The ambient U-17 Highside Transporter authored into the level world at
    /// RLWD ordinal 21 with script <c>Transporter</c>. MEASURED, retail
    /// <c>default physics.dat</c> record #636: its <c>CUnitMesh</c> is
    /// <c>f_lifter.msh</c>, shipped as
    /// <c>data/resources/meshes/m_f_lifter.msh.aya</c> (26,211 bytes, sha256
    /// <c>ced8fdd2…3df3</c>).
    /// </summary>
    public static Level100TargetVisualBinding TransporterBinding { get; } =
        new("U-17 Highside Transporter", "m_f_lifter.msh.aya");

    /// <summary>
    /// The Air Trainer. Two instances carry this definition: the ambient one
    /// authored at RLWD ordinal 40 with script <c>Flyby</c>, and the one the
    /// Airfield spawns for the dodge exercise with script <c>AirTrainer</c>.
    /// MEASURED, <c>default physics.dat</c> record #601: <c>CUnitMesh</c>
    /// <c>fa_f24_training.msh</c>, shipped as
    /// <c>data/resources/meshes/m_FA_F24_training.msh.aya</c> (26,677 bytes,
    /// sha256 <c>48876552…6ec5</c>).
    /// </summary>
    public static Level100TargetVisualBinding AirTrainerBinding { get; } =
        new("Air Trainer", "m_FA_F24_training.msh.aya");

    /// <summary>
    /// The nine airborne Target Drones. MEASURED, <c>default physics.dat</c>
    /// record #660: its <c>CUnitMesh</c> is the SAME
    /// <c>fa_f24_training.msh</c> the Air Trainer carries, so one retention and
    /// one conversion serve both definitions. The binding is still distinct
    /// because the definition name is what Core projects.
    /// </summary>
    public static Level100TargetVisualBinding TargetDroneBinding { get; } =
        new("Target Drone", "m_FA_F24_training.msh.aya");

    /// <summary>
    /// Every <c>(definitionName, meshBinding)</c> pair a Level 100 world actor
    /// can carry into the renderer. The renderer <em>throws</em> on a binding it
    /// has no mesh for rather than skipping it, so an actor reaching
    /// <see cref="Project"/> with a pair missing here kills the client on the
    /// first frame that admits it. Keeping the set here, next to the six
    /// constants, is what lets a test derive the eligible set from the pinned
    /// manifest and compare against it in both directions.
    /// </summary>
    public static IReadOnlyList<Level100TargetVisualBinding> RenderedBindings
    { get; } =
    [
        TargetTankBinding,
        TargetTruckBinding,
        WarehouseBinding,
        TransporterBinding,
        AirTrainerBinding,
        TargetDroneBinding,
    ];

    public static Level100TargetVisualDescriptor Project(
        TargetSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Level100ActorPoseSnapshot pose = target.Pose;
        Level100FloatBasis3Bits core = pose.BasisFloatBits;

        float c00 = Decode(core.Row0X);
        float c01 = Decode(core.Row0Y);
        float c02 = Decode(core.Row0Z);
        float c10 = Decode(core.Row1X);
        float c11 = Decode(core.Row1Y);
        float c12 = Decode(core.Row1Z);
        float c20 = Decode(core.Row2X);
        float c21 = Decode(core.Row2Y);
        float c22 = Decode(core.Row2Z);

        return new Level100TargetVisualDescriptor(
            target.ActorId,
            target.DefinitionName,
            target.MeshBinding,
            target.IsActive,
            new Level100RenderVector3(
                pose.PositionMillimeters.X * MillimetersToMeters,
                pose.PositionMillimeters.Y * MillimetersToMeters,
                -pose.PositionMillimeters.Z * MillimetersToMeters),
            new Level100RenderBasis3(
                new Level100RenderVector3(c00, c10, -c20),
                new Level100RenderVector3(c01, c11, -c21),
                new Level100RenderVector3(-c02, -c12, c22)));
    }

    private static float Decode(int bits) =>
        BitConverter.Int32BitsToSingle(bits);
}
