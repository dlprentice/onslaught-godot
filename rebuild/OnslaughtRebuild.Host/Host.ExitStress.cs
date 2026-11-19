// SPDX-License-Identifier: GPL-3.0-only
using Godot;

namespace OnslaughtRebuild.Host;

public partial class Host
{
    /// <summary>
    /// Test-only exit stress (HOST_EXIT_STRESS=N), a positive control for the shutdown routine: just before the Host
    /// quits, creates N each of the wrapper kinds seen leaking at exit (a SurfaceTool that has committed a mesh, an
    /// ImageTexture from a small Image, an ArrayMesh) and drops them undisposed, each held by a finalizable owner that
    /// releases it about a millisecond into its own finalizer. A collection is then started without waiting for it, so
    /// engine releases are still pending on the finalizer thread for about 3N milliseconds while the engine shuts down,
    /// the race seen at exit. Unset or not positive, it does nothing.
    /// </summary>
    private static void ExitStress()
    {
        if (!int.TryParse(System.Environment.GetEnvironmentVariable("HOST_EXIT_STRESS"), out int count) || count <= 0)
        {
            return;
        }

        Image image = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        for (int i = 0; i < count; i++)
        {
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(Vector3.Right);
            surface.AddVertex(Vector3.Up);
            surface.Commit();
            _ = new SlowRelease(surface);
            _ = new SlowRelease(ImageTexture.CreateFromImage(image));
            _ = new SlowRelease(new ArrayMesh());
        }

        System.GC.Collect();
        GD.Print($"HOST_EXIT_STRESS {count}");
    }

    private sealed class SlowRelease(GodotObject wrapper)
    {
        ~SlowRelease()
        {
            System.Threading.Thread.Sleep(1);
            wrapper.Dispose();
        }
    }
}
