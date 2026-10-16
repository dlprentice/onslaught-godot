// SPDX-License-Identifier: GPL-3.0-or-later
// What drawing the world does to the game: which meshes are drawn each render, and so which of their effect emitters run.
// Adapted from bea-decomp src/DXEngine.cpp (CDXEngine::Render's view, gamut and world passes, 0053e2e0), engine.cpp
// (GetViewMatrixFromCamera 00449ef0), renderinfo.cpp (SetProjection 00550b10, SetView 00550be0), gcgamut.cpp
// (RenderThings 00476fe0), thing.cpp (CThing::Render 004f36d0), Building.cpp (CBuilding::Render 00417540),
// BattleEngine.cpp (CBattleEngine::Render 0040d530), rtmesh.cpp (CRTMesh::Init's effect tables, Render 004dcba0,
// UpdateRenderLists 004dd0c0) and MeshRenderer.cpp (RenderPart's effect emitters, 004b6350).

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

    private RenderThing? _renderedThisFrame;
    private RenderThing? _renderedLastFrame;
    private FVector _campos;
    private float _zoom = 1.0f;
    private readonly float[] _wvp = new float[16];

    /// <summary>Things drawn as meshes in the last render (for checks).</summary>
    public int MeshesDrawn { get; private set; }

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
                            RenderThing(thing);
                        }
                    }
                }
            }
        }
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

        RenderMesh(thing, rt);
    }

    /// <summary>CBattleEngine::Render's test: drawn unless its player (viewpoint 0's) is in first-person view.</summary>
    public bool BattleEngineDrawn(BattleEngine be)
    {
        Game.Player? player = context.Game?.GetPlayer(0) is { } p0 && ReferenceEquals(p0.BattleEngine, be) ? p0 : null;
        return player is null || player.CurrentView != Game.PlayerView.FirstPerson || be.Visible || player.Number - 1 != 0;
    }

    /// <summary>CRTMesh::Render: onto this frame's list; drawn as a mesh when near enough (no imposter, or within the fade).</summary>
    private void RenderMesh(Thing thing, RenderThing rt)
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
        float lod = dist / radius * 1.0f * MeshLodBias * _zoom;
        bool hasImposter = CanBeImpostered(thing);
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
