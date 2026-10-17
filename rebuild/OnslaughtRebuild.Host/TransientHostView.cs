// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;

namespace OnslaughtRebuild.Host;

/// <summary>
/// Owns one HUD/battle-line/particle render's nodes, meshes and materials. It never owns textures; shaders are
/// borrowed unless explicitly opted in for the battle line's newly created shader. The live host replaces these bounded subtrees once per parity Present;
/// terrain, actors, water, surf and HUD/battle-line textures have longer-lived owners.
/// </summary>
internal sealed class TransientHostView : IDisposable
{
    private readonly Dictionary<ulong, Resource> _resources = [];
    private readonly bool _ownsShaders;
    private bool _disposed;

    public TransientHostView(Node node, bool ownsShaders = false)
    {
        Node = node;
        _ownsShaders = ownsShaders;
        Collect(node);
    }

    public Node Node { get; }

    private void Own(Resource? resource)
    {
        if (resource is not null)
        {
            _resources.TryAdd(resource.GetInstanceId(), resource);
            if (_ownsShaders && resource is ShaderMaterial { Shader: { } shader })
            {
                _resources.TryAdd(shader.GetInstanceId(), shader);
            }
        }
    }

    private void MeshResources(Mesh? mesh)
    {
        if (mesh is null)
        {
            return;
        }

        Own(mesh);
        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            Own(mesh.SurfaceGetMaterial(surface));
        }
    }

    private void Collect(Node node)
    {
        if (node is MeshInstance3D mesh3D)
        {
            MeshResources(mesh3D.Mesh);
            Own(mesh3D.MaterialOverride);
        }

        if (node is MeshInstance2D mesh2D)
        {
            MeshResources(mesh2D.Mesh);
        }

        if (node is CanvasItem canvas)
        {
            Own(canvas.Material);
        }

        using var children = node.GetChildren();
        foreach (Node child in children)
        {
            Collect(child);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (GodotObject.IsInstanceValid(Node))
        {
            Node.GetParent()?.RemoveChild(Node);
            Node.Free();
        }

        foreach (Resource resource in _resources.Values)
        {
            resource.Dispose();
        }

        _resources.Clear();
    }
}
