// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;

namespace OnslaughtRebuild.GodotClient;

internal static class VisualPrimitives
{
    public static StandardMaterial3D CreateMaterial(
        Color color,
        float metallic = 0.15f,
        float roughness = 0.65f,
        Color? emission = null)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness,
        };

        if (color.A < 0.999f)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        }

        if (emission.HasValue)
        {
            material.EmissionEnabled = true;
            material.Emission = emission.Value;
            material.EmissionEnergyMultiplier = 2.2f;
        }

        return material;
    }

    public static MeshInstance3D CreateBox(
        string name,
        Vector3 size,
        Vector3 position,
        StandardMaterial3D material,
        Vector3? rotationDegrees = null)
    {
        return new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            Position = position,
            RotationDegrees = rotationDegrees ?? Vector3.Zero,
            MaterialOverride = material,
        };
    }

    public static MeshInstance3D CreateCylinder(
        string name,
        float radius,
        float height,
        Vector3 position,
        StandardMaterial3D material,
        Vector3? rotationDegrees = null)
    {
        return new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = height,
                RadialSegments = 20,
            },
            Position = position,
            RotationDegrees = rotationDegrees ?? Vector3.Zero,
            MaterialOverride = material,
        };
    }

    public static MeshInstance3D CreateSphere(
        string name,
        float radius,
        Vector3 position,
        StandardMaterial3D material)
    {
        return new MeshInstance3D
        {
            Name = name,
            Mesh = new SphereMesh
            {
                Radius = radius,
                Height = radius * 2f,
                RadialSegments = 20,
                Rings = 12,
            },
            Position = position,
            MaterialOverride = material,
        };
    }

    public static StyleBoxFlat CreatePanelStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14,
            ContentMarginTop = 10,
            ContentMarginRight = 14,
            ContentMarginBottom = 10,
        };
    }
}
