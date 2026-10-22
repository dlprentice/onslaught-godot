// SPDX-License-Identifier: GPL-3.0-only
// What each render does to the particles. Adapted from bea-decomp src/game.cpp (CGame::Render's frame length,
// 0046e460) and DXEngine.cpp (CDXEngine::Render's particle steps and sun, 0053e86d..0053eb6a), DXParticleManager.cpp
// (RenderOther 0054f7e0) and PDSimpleSprite.cpp (InitNoiseTable 004c7db0, CPDSimpleSprite::Unknown23 004c8040,
// CPDPMesh::Unknown11 004c9000).

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
using ParityCamera = OnslaughtRebuild.Parity.Game.Camera;

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>
/// The level's particles and their render-time steps. Each render of viewpoint 0: the particle step over the render's
/// frame length (unless paused), the effects' interpolation, the sun sprite when the camera sees the sun, then the
/// drawing, whose only effect on the game is the sprites' noise table, built with rand() the first time a sprite or
/// procedural mesh is drawn. (No shipped function descriptor draws rand(), so the rest of the drawing draws none.)
/// </summary>
public sealed class LevelParticles : IParticleWorld
{
    private const float ClockTick = 0.05f;
    private const string SunSprite = "Sun Sprite";

    private readonly ThingContext _context;
    private readonly ParticleDescriptor? _sunPD;
    private float _lastRenderFrameTime = -1.0f;
    private (FVector Pos, FMatrix Orientation)? _camera;

    public LevelParticles(ThingContext context, ParticleSet set, ParticleProcessState? state = null)
    {
        _context = context;
        Manager = new ParticleManager(set, context.Crt, this, state);
        World = new Render.WorldRender(context);
        _sunPD = set.GetPD(SunSprite);
    }

    public ParticleManager Manager { get; }

    /// <summary>The world pass: which meshes are drawn, and so which of their effect emitters run.</summary>
    public Render.WorldRender World { get; private set; } = null!;

    /// <summary>The 32 x 32 fractal noise the sprites are drawn with (-1..1), once built.</summary>
    public float[]? Noise { get => Manager.State.Noise; set => Manager.State.Noise = value; }

    public float EventTime => _context.Events.Time;

    /// <summary>GAME.mRenderFrameLength: the last render's length in game ticks (1 for the first).</summary>
    public float RenderFrameLength { get; private set; } = 1.0f;

    public (FVector Pos, FMatrix Orientation)? Camera => _camera;

    public float FrameRenderFraction => _context.Game?.Loop.Fraction ?? 0.0f;

    public float MapCollide(FVector pos) => _context.Map.Collide(pos);

    public float MeshRadius(string name) =>
        _context.Meshes.TryGetValue(name, out Mesh.MeshData? mesh)
            ? mesh.Radius
            : throw new NotSupportedException($"particle mesh '{name}' is not in the level resource file (retail loads it from disk)");

    /// <summary>
    /// CGame::Render's frame length (game.cpp:1682), every render before the world is drawn or skipped: the render's
    /// time in game ticks less the last render's.
    /// </summary>
    public void StartRender()
    {
        Game.Game game = _context.Game ?? throw new InvalidOperationException("particles render without a game");
        // VC6 folds the division by CLOCK_TICK into a multiplication by its reciprocal (20): the port's per-render
        // new_time agrees with the product at the last bit where the quotient does not (fe-lose P4942, P4943).
        float newTime = (_context.Events.Time + (game.Loop.Fraction * ClockTick)) * (1.0f / ClockTick);
        float length = _lastRenderFrameTime != -1.0f ? newTime - _lastRenderFrameTime : 1.0f;
        _lastRenderFrameTime = newTime;
        RenderFrameLength = length;
    }

    /// <summary>One render of viewpoint 0 (CDXEngine::Render's particle steps), after <see cref="StartRender"/>.</summary>
    public void Render(int numRenders)
    {
        Game.Game game = _context.Game ?? throw new InvalidOperationException("particles render without a game");
        Manager.State.RenderFraction = game.Loop.Fraction;
        bool first = numRenders == 0;
        _camera = game.GetCurrentCamera(0) is { } camera
            ? (ParityCamera.RenderView(camera, game.Loop.Fraction2) is var v ? (v.Pos, v.Ori) : null)
            : null;

        // The world pass (CDXEngine::Render draws the world before the particles step).
        if (game.GetCurrentCamera(0) is { } worldCamera)
        {
            World.Render(worldCamera, game.Loop.Fraction2);
        }

        if (!game.Paused)
        {
            Manager.Process(RenderFrameLength, first);
        }

        Manager.DoInterpolation();

        // The sun: a sprite where the sun is, when nothing stands in the line of sight to it.
        if (!game.Paused && _context.Map.VisibleSun && _sunPD is not null && _camera is { } cam)
        {
            FVector sun = _context.Map.SunPos * 0.6f;
            FVector toward = sun;
            toward.Normalise();
            toward *= 200.0f;
            Thing? ignore = game.GetPlayer(0)?.BattleEngine;
            if (WorldLineQuery.FindFirstThingToHitLine(_context, cam.Pos, cam.Pos + toward, ignore, new WorldLineReport(), true,
                    true, 1, 4, true) == LineHitType.Nothing)
            {
                Manager.CreateEffect(_sunPD, null, cam.Pos + sun, false, true);
            }
        }

        Manager.State.RenderOther(Manager);

        // draw_fairly_late_stuff: the cockpit, after the particles.
        World.RenderCockpit();

        // PostRender: the HUD.
        World.RenderHud();
    }


}
