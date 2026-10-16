// SPDX-License-Identifier: GPL-3.0-or-later
// What drawing the world does to the game: which meshes are drawn each render, and so which of their effect emitters run.
// Adapted from bea-decomp src/DXEngine.cpp (CDXEngine::Render's view, gamut and world passes, 0053e2e0), engine.cpp
// (GetViewMatrixFromCamera 00449ef0), renderinfo.cpp (SetProjection 00550b10, SetView 00550be0), gcgamut.cpp
// (RenderThings 00476fe0), thing.cpp (CThing::Render 004f36d0), Building.cpp (CBuilding::Render 00417540),
// BattleEngine.cpp (CBattleEngine::Render 0040d530), rtmesh.cpp (CRTMesh::Init's effect tables, Render 004dcba0,
// UpdateRenderLists 004dd0c0) and MeshRenderer.cpp (Render's pose update 004b6260, RenderPart's effect emitters, 004b6350).

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Things;
using ParityCamera = OnslaughtRebuild.Parity.Game.Camera;

namespace OnslaughtRebuild.Parity.Render;

/// <summary>
/// One render of viewpoint 0's world pass: the view and its gamut, then each big thing and each renderable thing in the
/// map-who sectors over open gamut cells, culled to the screen where the cells are partly open; each mesh drawn moves to
/// this frame's list and runs its effect emitters (an effect starts the first time its part is drawn and follows the
/// part); afterwards the meshes drawn last frame and not this one lose their effects.
/// Drawn trees (CRTTree) have no render thing of this kind and no effects; imposters draw nothing that affects the game.
/// The screen test's matrices are computed in single precision as D3DX's; D3DXMatrixInverse's own operation order is
/// not modelled, so a thing at the very edge of the screen can be judged differently.
/// </summary>
public sealed class WorldRender(ThingContext context)
{
    private const float NearZ = 0.1f;
    private const float LandscapeFar = 700.0f;
    private const float AspectRatio = 0.75f;
    private const float ImposterFadeEnd = 40.0f;
    private const float MeshLodBias = 1.0f;
    private const float MeshLodMed = 10.0f;
    private const float MeshLodLow = 20.0f;
    private const float TreeMeshDistance = 30.0f;
    private const float ViewportWidth = 640.0f;
    private const int ViewportHalfWidth = 320;
    private const int ViewportHalfHeight = 240;

    public Gamut Gamut { get; } = new();

    /// <summary>The render-to-texture shadows: asked for in the world pass, drawn at the start of the next render.</summary>
    public Shadows Shadows { get; } = new();

    private RenderThing? _renderedThisFrame;
    private RenderThing? _renderedLastFrame;
    private FVector _campos;
    private float _zoom = 1.0f;
    private readonly float[] _wvp = new float[16];

    /// <summary>Things drawn as meshes in the last render (for checks).</summary>
    public int MeshesDrawn { get; private set; }

    /// <summary>The things whose Render ran in the last render, in order (for checks).</summary>
    public List<Thing> Rendered { get; } = [];

    /// <summary>Diagnostic: each mesh render's subject, LOD and whether its mesh was drawn, in the last render.</summary>
    public List<(IRenderSubject Subject, float Lod, bool Drawn)> MeshLog { get; } = [];

    /// <summary>The world pass of one render, from the camera's render view.</summary>
    public void Render(ParityCamera camera, float fraction2)
    {
        (FVector pos, FMatrix ori, float zoom) = ParityCamera.RenderView(camera, fraction2);
        _campos = pos;
        _zoom = zoom;
        FMatrix matview = FMatrix.MakeRotationPitchF(1.570796f).Multiply(ori.Transpose());
        Gamut.MakeIt(matview, 2048, zoom * 1.0f, zoom * AspectRatio, pos);
        BuildScreenMatrix(matview, pos, NearZ * zoom, NearZ * zoom * AspectRatio);
        MeshesDrawn = 0;
        Rendered.Clear();
        MeshLog.Clear();

        // The shadows asked for in the last render are drawn first (each unit from the light), then the list restarts.
        Shadows.RenderTextures(context.Game?.Clock.GetSysTimeFloat() ?? 0.0f, RenderThing);
        Shadows.ResetShads();
        RenderThings(ori);
        UpdateRenderLists();
    }

    /// <summary>The world, view and projection product RenderThings tests with (world = identity after the water).</summary>
    private void BuildScreenMatrix(FMatrix ori, FVector pos, float width, float height)
    {
        // SetView: the inverse of [rows of ori, pos]; the rows are orthonormal, so the inverse is the transpose and the
        // negated, rotated position.
        float[] view =
        [
            ori.Row0.X, ori.Row1.X, ori.Row2.X, 0,
            ori.Row0.Y, ori.Row1.Y, ori.Row2.Y, 0,
            ori.Row0.Z, ori.Row1.Z, ori.Row2.Z, 0,
            -((pos.X * ori.Row0.X) + (pos.Y * ori.Row0.Y) + (pos.Z * ori.Row0.Z)),
            -((pos.X * ori.Row1.X) + (pos.Y * ori.Row1.Y) + (pos.Z * ori.Row1.Z)),
            -((pos.X * ori.Row2.X) + (pos.Y * ori.Row2.Y) + (pos.Z * ori.Row2.Z)), 1,
        ];
        float q = LandscapeFar / (LandscapeFar - NearZ);
        float[] proj =
        [
            NearZ / width, 0, 0, 0,
            0, NearZ / height, 0, 0,
            0, 0, q, 1.0f,
            0, 0, -(NearZ * LandscapeFar) / (LandscapeFar - NearZ), 0,
        ];
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                float s = 0;
                for (int k = 0; k < 4; k++)
                {
                    s += view[(r * 4) + k] * proj[(k * 4) + c];
                }

                _wvp[(r * 4) + c] = s;
            }
        }
    }

    private (float X, float Y, float W) Transform(FVector v) =>
        ((v.X * _wvp[0]) + (v.Y * _wvp[4]) + (v.Z * _wvp[8]) + _wvp[12],
            (v.X * _wvp[1]) + (v.Y * _wvp[5]) + (v.Z * _wvp[9]) + _wvp[13],
            (v.X * _wvp[3]) + (v.Y * _wvp[7]) + (v.Z * _wvp[11]) + _wvp[15]);

    private void RenderThings(FMatrix camOri)
    {
        var right = new FVector(camOri.Row0.X, camOri.Row1.X, camOri.Row2.X);
        float sx = ViewportWidth / ViewportHalfWidth;
        float sy = ViewportWidth / ViewportHalfHeight;
        foreach (Thing thing in context.World.BigThings.ToList())
        {
            // A big thing asks for its shadow before its screen test.
            if (WantsShadow(thing, component: true))
            {
                Shadows.RequestShadow(thing, ShadowDistance(thing));
            }

            if (OnScreen(thing, right, sx, sy, partial: true))
            {
                RenderThing(thing);
            }
        }

        for (int layer = 4; layer >= 3; layer--)
        {
            int scale = 1 << (4 - layer);
            int widthCells = Gamut.GridSize / scale;
            int x0 = Math.Max((Gamut.MinX / scale) - 1, 0);
            int x1 = Math.Min((Gamut.MaxX / scale) + 2, widthCells);
            int y0 = Math.Max((Gamut.MinY / scale) - 1, 0);
            int y1 = Math.Min((Gamut.MaxY / scale) + 2, widthCells);
            int size = scale * 8;
            float sectorR2 = size * size * 2 * 0.5f;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    MapWhoNode node = context.MapWho.GetSector(new Sector(x, y, layer));
                    if (node.First is null)
                    {
                        continue;
                    }

                    float half = size * 0.5f;
                    var centre = new FVector((x * size) + half, (y * size) + half, 0);
                    FVector d = _campos - centre;
                    float d2 = (d.X * d.X) + (d.Y * d.Y) - sectorR2;
                    int sx0 = Math.Min(Math.Max((x - 1) * scale, Gamut.MinX), Gamut.MaxX);
                    int sy0 = Math.Min(Math.Max((y - 1) * scale, Gamut.MinY), Gamut.MaxY);
                    int sx1 = Math.Min(Math.Max((x + 2) * scale, Gamut.MinX), Gamut.MaxX);
                    int sy1 = Math.Min(Math.Max((y + 2) * scale, Gamut.MinY), Gamut.MaxY);
                    if (sy0 >= sy1)
                    {
                        continue;
                    }

                    bool visible = false;
                    bool partial = false;
                    for (int gy = sy0; gy < sy1; gy++)
                    {
                        for (int gx = sx0; gx < sx1; gx++)
                        {
                            if (Gamut.Open(gx, gy))
                            {
                                visible = true;
                                if (partial)
                                {
                                    gy = sy1;
                                    gx = sx1;
                                }
                            }
                            else
                            {
                                partial = true;
                            }
                        }
                    }

                    if (!visible)
                    {
                        continue;
                    }

                    for (MapWhoEntry? entry = context.MapWho.GetFirstEntry(node); entry is not null; entry = context.MapWho.GetNextEntry())
                    {
                        Thing thing = entry.Thing;
                        if (!thing.IsA(ThingType.Renderable))
                        {
                            continue;
                        }

                        if (thing.IsA(ThingType.Tree))
                        {
                            // The sector's trees, together, when it is near enough: CRTTree has no effects.
                            float td = TreeMeshDistance + 10.0f;
                            _ = d2 < td * td;
                            break;
                        }

                        if (OnScreen(thing, right, sx, sy, partial))
                        {
                            // A sector thing asks for its shadow once it passes the test (components never do).
                            if (WantsShadow(thing, component: false))
                            {
                                Shadows.RequestShadow(thing, ShadowDistance(thing));
                            }

                            RenderThing(thing);
                        }
                    }
                }
            }
        }
    }

    /// <summary>Diagnostic: the thing's sector tests and screen test values in the last render.</summary>
    public string Explain(Thing thing)
    {
        var sb = new System.Text.StringBuilder();
        foreach (int layer in new[] { 4, 3 })
        {
            Sector s = context.MapWho.WorldToSector(thing.Pos, layer);
            int scale = 1 << (4 - layer);
            int sx0 = Math.Min(Math.Max((s.X - 1) * scale, Gamut.MinX), Gamut.MaxX);
            int sy0 = Math.Min(Math.Max((s.Y - 1) * scale, Gamut.MinY), Gamut.MaxY);
            int sx1 = Math.Min(Math.Max((s.X + 2) * scale, Gamut.MinX), Gamut.MaxX);
            int sy1 = Math.Min(Math.Max((s.Y + 2) * scale, Gamut.MinY), Gamut.MaxY);
            int open = 0, closed = 0;
            for (int gy = sy0; gy < sy1; gy++)
            {
                for (int gx = sx0; gx < sx1; gx++)
                {
                    if (Gamut.Open(gx, gy)) open++; else closed++;
                }
            }

            if (layer == 4)
            {
                for (int gy = sy0; gy < sy1; gy++)
                {
                    for (int gx = sx0; gx < sx1; gx++)
                    {
                        if (!Gamut.Open(gx, gy))
                        {
                            sb.Append($"closed ({gx},{gy}) high {Gamut.Spans[((gy * 64) + gx) * 2]} low {Gamut.Spans[(((gy * 64) + gx) * 2) + 1]} raw low {Gamut.RawLow[(gy * 64) + gx]:R} raw high {Gamut.RawHigh[(gy * 64) + gx]:R} campos {_campos.X} {_campos.Y} {_campos.Z}; ");
                        }
                    }
                }
            }

            sb.Append($"L{layer} sector {s.X},{s.Y} cells [{sx0},{sx1})x[{sy0},{sy1}) open {open} closed {closed}; ");
        }

        if (thing is IRenderSubject subject && thing.RenderThing?.MeshOrNull is { } mesh)
        {
            FVector pos = subject.RenderPos;
            if (mesh.BoundingBox is { } box)
            {
                pos += thing.GetOrientation().Transform(box.Origin);
            }

            var right = new FVector(_wvp[0], 0, 0);
            (float x1, float y1, float w1) = Transform(pos);
            sb.Append($"screen x/w {x1 / w1 + 1} y/w {y1 / w1 + 1} w {w1} radius {mesh.Radius} gamut x[{Gamut.MinX},{Gamut.MaxX}] y[{Gamut.MinY},{Gamut.MaxY}]");
        }

        return sb.ToString();
    }

    /// <summary>The shadow request's test: units and buildings, not the battle engine, not those statically shadowed.</summary>
    private static bool WantsShadow(Thing thing, bool component) =>
        (thing.IsA(ThingType.Unit) || thing.IsA(ThingType.Building)) && (component || !thing.IsA(ThingType.Component))
        && !thing.IsA(ThingType.BattleEngine) && !thing.CanBeStaticallyShadowed;

    /// <summary>The request's distance, 1 / cue: the cue factor over the squared distance to the box centre.</summary>
    private float ShadowDistance(Thing thing)
    {
        FVector pos = thing is IRenderSubject subject ? subject.RenderPos : thing.Pos;
        if (thing.RenderThing?.MeshOrNull?.BoundingBox is { } box)
        {
            pos += thing.GetOrientation().Transform(box.Origin);
        }

        float dist2 = (pos - _campos).MagnitudeSq();
        float cueFactor = thing.RenderThing is null ? 0.0001f : thing.RenderThing.MeshOrNull?.CueFactor ?? 0.00001f;
        float cue = cueFactor / (dist2 + 0.001f);
        return 1.0f / cue;
    }

    /// <summary>The visibility test: drawn unless (partly open cells only) its screen extent misses and the camera is outside it.</summary>
    private bool OnScreen(Thing thing, FVector right, float sx, float sy, bool partial)
    {
        if (!partial)
        {
            return true;
        }

        if (thing is not IRenderSubject subject || thing.RenderThing?.MeshOrNull is not { } mesh)
        {
            return true;
        }

        FVector pos = subject.RenderPos;
        if (mesh.BoundingBox is { } box)
        {
            pos += thing.GetOrientation().Transform(box.Origin);
        }

        float dist2 = (pos - _campos).MagnitudeSq();
        float radius = mesh.Radius;
        (float x1, float y1, float w1) = Transform(pos);
        (float x2, _, float w2) = Transform(pos + (right * radius));
        float vx1 = (x1 / w1) + 1.0f;
        float vy1 = (y1 / w1) + 1.0f;
        float vx2 = (x2 / w2) + 1.0f;
        float r = vx2 - vx1;
        float radius2 = radius * radius;
        if (!(vx1 > -r && r + sx > vx1 && vy1 > -r && r + sy > vy1))
        {
            if (!(dist2 < radius2) && !thing.IsA(ThingType.Cutscene))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The thing's Render: CThing's (not invisible or unrendered), with the building's and battle engine's own tests.</summary>
    private void RenderThing(Thing thing)
    {
        if ((thing.Flags & ThingFlags.Invisible) != 0 || thing.RenderThing is not { MeshOrNull: not null } rt
            || (thing.Flags & ThingFlags.DontRender) != 0 || thing is Tree)
        {
            return;
        }

        if (thing is Building && thing.IsDying)
        {
            // CBuilding::Render: a dying building without destructible segments is not drawn (segments are not ported).
            return;
        }

        if (thing is BattleEngine be && !BattleEngineDrawn(be))
        {
            return;
        }

        Rendered.Add(thing);
        RenderMesh(CanBeImpostered(thing), rt);
    }

    /// <summary>
    /// draw_fairly_late_stuff's cockpit: in first-person view, a living battle engine's cockpit is drawn (CDXCockpit::Render
    /// through its CRTMesh, which cannot be impostered), after the particles.
    /// </summary>
    public void RenderCockpit()
    {
        if (context.Game?.GetPlayer(0) is not { } player || player.BattleEngine is not BattleEngine be || be.IsDying
            || player.CurrentView != Game.PlayerView.FirstPerson || be.Cockpit is not { ShouldRender: true, RenderThing: { } rt })
        {
            return;
        }

        RenderMesh(false, rt);
    }

    /// <summary>
    /// PostRender's HUD.Render, as far as it affects the game: for viewpoint 0's player, when its camera shows the HUD
    /// and its battle engine is alive (RenderOverlayForViewpoint), Render00485d50's warning line runs the battle engine's
    /// TestDanger while the game is no further than playing and no radar threat is newer than 5 seconds. The radar
    /// warning receiver is not ported; its threats are rounds homing on the battle engine, and rounds are not ported
    /// either (spawning one throws), so its list is empty and the test always runs. The other HUD passes draw only.
    /// </summary>
    public void RenderHud()
    {
        if (context.Game is not { } game || game.GetPlayer(0)?.BattleEngine is not BattleEngine be
            || game.GetCurrentCamera(0)?.GetShowHud() != true || be.IsDying)
        {
            return;
        }

        if (game.State <= Game.GameState.Playing)
        {
            be.TestDanger();
        }
    }

    /// <summary>CBattleEngine::Render's test: drawn unless its player (viewpoint 0's) is in first-person view.</summary>
    public bool BattleEngineDrawn(BattleEngine be)
    {
        Game.Player? player = context.Game?.GetPlayer(0) is { } p0 && ReferenceEquals(p0.BattleEngine, be) ? p0 : null;
        return player is null || player.CurrentView != Game.PlayerView.FirstPerson || be.Visible || player.Number - 1 != 0;
    }

    /// <summary>CRTMesh::Render: onto this frame's list; drawn as a mesh when near enough (no imposter, or within the fade).</summary>
    private void RenderMesh(bool hasImposter, RenderThing rt)
    {
        RenderMesh(hasImposter, rt, (rt.Subject as Thing)?.GetRenderImposterBias() ?? 1.0f);
    }

    private void RenderMesh(bool hasImposter, RenderThing rt, float imposterBias)
    {
        if (rt.NextRendered is not null)
        {
            rt.NextRendered.PrevRendered = rt.PrevRendered;
        }

        if (rt.PrevRendered is not null)
        {
            rt.PrevRendered.NextRendered = rt.NextRendered;
        }

        if (_renderedLastFrame == rt)
        {
            _renderedLastFrame = rt.NextRendered;
        }

        if (_renderedThisFrame == rt)
        {
            _renderedThisFrame = rt.NextRendered;
        }

        rt.NextRendered = _renderedThisFrame;
        rt.PrevRendered = null;
        if (_renderedThisFrame is not null)
        {
            _renderedThisFrame.PrevRendered = rt;
        }

        _renderedThisFrame = rt;

        MeshData mesh = rt.Mesh;
        IRenderSubject subject = rt.Subject;
        float dist = (_campos - subject.RenderPos).Magnitude();
        float radius = mesh.BoundingBox?.Radius ?? mesh.Radius;
        float lod = dist / radius * imposterBias * MeshLodBias * _zoom;
        MeshLog.Add((subject, lod, !(hasImposter && !(lod < ImposterFadeEnd))));
        if (hasImposter && !(lod < ImposterFadeEnd))
        {
            return;
        }

        MeshData drawn = mesh;
        if (mesh.NextInChain is { } med)
        {
            if (lod > MeshLodMed)
            {
                drawn = med;
            }

            if (lod > MeshLodLow && drawn.NextInChain is { } low)
            {
                drawn = low;
            }
        }

        MeshesDrawn++;

        // CMeshRenderer::Render (004b6260): a render thing with a pose rebuilds it for the drawn mesh, once a render
        // frame. Collision between renders reads that pose, interpolated as the render saw it.
        if (drawn.NumParts > 0 && rt.Pose is { } pose)
        {
            pose.Update(drawn, subject, context.RenderFrameNumber, force: true);
        }

        RunEffects(rt, drawn, subject);
    }

    /// <summary>GetCanBeImpostered: every thing but the battle engine (and rounds and components, not drawn here).</summary>
    private static bool CanBeImpostered(Thing thing) => thing is not BattleEngine;

    /// <summary>The drawn mesh's effect emitters (part type 5), in part order: started the first time, then following the part.</summary>
    private void RunEffects(RenderThing rt, MeshData drawn, IRenderSubject subject)
    {
        if (context.Particles is not { } particles)
        {
            return;
        }

        MeshEffects effects = rt.Effects ??= MakeEffects(rt.Mesh, particles);
        if (effects.Parts.Length == 0)
        {
            return;
        }

        foreach (MeshPart part in drawn.Parts)
        {
            if (part.PartType != 5)
            {
                continue;
            }

            int n = Array.IndexOf(effects.Parts, part);
            if (n < 0 || effects.Types[n] is null)
            {
                continue;
            }

            if (effects.Links[n] is null)
            {
                var link = new ParticleEffectLink(particles.Manager);
                effects.Links[n] = link;
                particles.Manager.CreateEffect(effects.Types[n], link, new FVector(0, 0, 0), true, false);
                link.SetPos(new FVector(0, 0, 100.0f));
                link.SetPos(new FVector(0, 0, 100.0f));
                effects.Active[n] = false;
            }

            MeshPose.EvaluatePose(drawn, rt.Pose, subject, part, context.TransformStatics, out FVector pos, out FMatrix ori,
                local: false, force: true);
            ParticleEffectLink effect = effects.Links[n]!;
            int id = effects.Ids[n];
            if (!effects.Active[n])
            {
                effects.Active[n] = true;
                effect.SetPos(pos);
                effect.SetPos(pos);
                if ((id | 1) == 1 || (id | 2) == 2)
                {
                    effect.SetOrientation(ori);
                }
            }
            else
            {
                effect.SetPos(pos);
                effect.SetPos(pos);
                if ((id | 1) == 1)
                {
                    effect.SetOrientation(ori);
                }
            }
        }
    }

    /// <summary>CRTMesh::Init's tables: the emitters named '_' + a descriptor's name, in emitter order.</summary>
    private static MeshEffects MakeEffects(MeshData mesh, LevelParticles particles)
    {
        List<int> found = [];
        for (int i = 0; i < mesh.Emitters.Count; i++)
        {
            string name = mesh.Emitters[i].Name;
            if (name.Length > 0 && name[0] == '_')
            {
                found.Add(i);
            }
        }

        return new MeshEffects
        {
            Parts = found.Select(i => mesh.Emitters[i].MeshPart).ToArray(),
            Ids = found.Select(i => mesh.Emitters[i].Number).ToArray(),
            Types = found.Select(i => particles.Manager.Set.FindByName(mesh.Emitters[i].Name[1..])).ToArray(),
            Links = new ParticleEffectLink?[found.Count],
            Active = new bool[found.Count],
        };
    }

    /// <summary>UpdateRenderLists: the meshes drawn last frame and not this one lose their effects.</summary>
    private void UpdateRenderLists()
    {
        while (_renderedLastFrame is { } rt)
        {
            if (rt.Effects is { } effects)
            {
                for (int i = 0; i < effects.Links.Length; i++)
                {
                    if (effects.Links[i] is { } link)
                    {
                        link.Stop(false);
                        link.Release();
                        effects.Links[i] = null;
                    }
                }
            }

            rt.PrevRendered = null;
            _renderedLastFrame = rt.NextRendered;
            rt.NextRendered = null;
        }

        _renderedLastFrame = _renderedThisFrame;
        _renderedThisFrame = null;
    }
}
