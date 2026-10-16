// SPDX-License-Identifier: GPL-3.0-or-later
// The battle engine's cockpit. Adapted from bea-decomp src/Cockpit.cpp (CCockpit: the constructor 004244b0,
// AddShockShake 004247a0, MorphIntoWalkerCockpit 00424920, MorphIntoJetCockpit 00424990, HandleEvent 00424a00, Process
// 00424a20, ProcessAnimation 00424be0, ProcessWalker 00424ca0, ProcessJet 004250f0, GetRenderFrame 00425410,
// GetRenderPos 00425430, GetRenderOrientation 004254f0) and DXCockpit.cpp (Render 0053bb50).

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CCockpit: the walker's or jet's cockpit mesh, drawn in first-person view around the camera. Each frame's end it sways
/// with the engine's turn rates, bobs with the walk cycle, leans with the ground and shakes; its animation plays the
/// morphs between walker and jet.
/// </summary>
public sealed class Cockpit : Monitor, IRenderSubject
{
    private const int ProcessEvent = 2001;

    private readonly ThingContext _context;
    private readonly BattleEngine _engine;

    public Cockpit(ThingContext context, BattleEngine engine, MeshData? mesh)
    {
        _context = context;
        _engine = engine;
        RenderThing = mesh is null ? null : new RenderThing(context, mesh, this);
        AnimMode = mesh?.GetAnimModeByName(engine.State == BattleEngineState.Jet ? "jet" : "walk") ?? -1;
        context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame, EventPriority.EndOfFrame);
        FrameIncrement = RenderThing?.Mesh.GetFrameIncrement(AnimMode, out _realAnimIndex) ?? 0.0f;
    }

    public RenderThing? RenderThing { get; }

    public bool ShouldRender = true;
    public float[] Shake = new float[4];
    public float ShakeTime;
    public FVector ShakePos, OldShakePos;
    public FMatrix Orientation = FMatrix.Identity, OldOrientation = FMatrix.Identity, BaseOrientation = FMatrix.Identity;
    public int AnimMode;
    public float FrameIncrement;
    public float Frame, OldFrame;
    public int MorphState;
    public float UnknownA4, UnknownA8;
    private int _realAnimIndex;

    /// <summary>AddShockShake (004247a0): four random kicks (main thread rand), the first three clamped to 0.01.</summary>
    public void AddShockShake(float amount)
    {
        float scale = 128.0f / amount;
        amount *= 0.5f;
        Shake[0] = (_context.Crt.Rand() % 128 / scale) - amount;
        Shake[1] = (_context.Crt.Rand() % 128 / scale) - amount;
        Shake[2] = (_context.Crt.Rand() % 128 / scale) - amount;
        Shake[3] = (_context.Crt.Rand() % 32 / scale) - amount;
        ShakeTime = 0;
        for (int i = 0; i < 3; i++)
        {
            Shake[i] = Math.Clamp(Shake[i], -0.01f, 0.01f);
        }
    }

    public void MorphIntoWalkerCockpit() => Morph("flytowalk", 1);

    public void MorphIntoJetCockpit() => Morph("walktofly", 2);

    private void Morph(string mode, int state)
    {
        AnimMode = RenderThing?.Mesh.GetAnimModeByName(mode) ?? -1;
        FrameIncrement = RenderThing?.Mesh.GetFrameIncrement(AnimMode, out _realAnimIndex) ?? 0.0f;
        Frame = 0;
        OldFrame = 0;
        MorphState = state;
    }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == ProcessEvent)
        {
            Process();
        }
    }

    /// <summary>Process (00424a20): last frame's pose kept, the sway, the shake decayed, the next frame's event, the animation.</summary>
    private void Process()
    {
        OldShakePos = ShakePos;
        OldOrientation = Orientation;
        if (_engine.IsDying)
        {
            return;
        }

        if (_engine.State == BattleEngineState.Walker)
        {
            ProcessWalker();
        }
        else
        {
            ProcessJet();
        }

        float c = (float)Math.Cos(ShakeTime);
        ShakePos = new FVector(0, 0, 0);
        ShakePos.X += c * Shake[0];
        ShakePos.Y += c * Shake[1];
        ShakePos.Z += c * Shake[2];
        for (int i = 0; i < 4; i++)
        {
            Shake[i] *= 0.8f;
        }

        if (MathF.Abs(Shake[0]) > 0.001f || MathF.Abs(Shake[1]) > 0.001f || MathF.Abs(Shake[2]) > 0.001f || MathF.Abs(Shake[3]) > 0.001f)
        {
            ShakeTime += 0.8f;
        }

        _context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame, EventPriority.EndOfFrame);
        ProcessAnimation();
    }

    /// <summary>ProcessAnimation (00424be0): the frame advances; a finished morph settles into walk or fly.</summary>
    private void ProcessAnimation()
    {
        OldFrame = Frame;
        Frame += FrameIncrement;
        if (Frame > 1.0f)
        {
            if (MorphState is 1 or 2)
            {
                AnimMode = RenderThing?.Mesh.GetAnimModeByName(MorphState == 1 ? "walk" : "fly") ?? -1;
                FrameIncrement = RenderThing?.Mesh.GetFrameIncrement(AnimMode, out _realAnimIndex) ?? 0.0f;
                Frame = 0;
                MorphState = 0;
            }
            else
            {
                Frame = 0;
                FrameIncrement = 0;
            }
        }
    }

    /// <summary>ProcessWalker (00424ca0): the turn rates, the walk's bob and sway, and the lean towards the ground's slope.</summary>
    private void ProcessWalker()
    {
        BattleEngine be = _engine;
        Orientation = BaseOrientation;
        float roll = ((float)Math.Cos(ShakeTime) * Shake[3]) - be.Yawvel;
        float yaw = -be.Yawvel;
        float pitch = be.Pitchvel * -2.0f;
        if (be.IsWalking())
        {
            float cycle = be.WalkerPart!.WalkCycle;
            float c = (float)Math.Cos(cycle);
            roll += c * 0.02f;
            float bob = (float)Math.Cos(cycle * 2) * 0.02f;
            if (bob < 0.0)
            {
                bob = 0;
            }

            pitch += bob;
            yaw += c * 0.03f;
        }

        float heading = be.CurrentOrientation.Yaw;
        var ori = new FMatrix(new FVector(MathF.Cos(heading), -MathF.Sin(heading), 0.0f), new FVector(MathF.Sin(heading), MathF.Cos(heading), 0.0f),
            new FVector(0.0f, 0.0f, 1.0f));
        FVector forward = ori.Transform(new FVector(0.0f, 1.0f, 0.0f));
        FVector normal = be.IsOnGround() && !be.IsOnObject() ? _context.Map.Normal(be.Pos) : new FVector(0, 0, -1);
        FVector cross = FVector.Cross(forward, normal);
        FVector along = FVector.Cross(cross, normal);
        along.Normalise();
        float targetPitch = along.Z * -0.25f;
        FVector right = cross;
        right.Normalise();
        float targetRoll = right.Z * 0.75f;
        UnknownA4 = Ease(UnknownA4, targetPitch);
        UnknownA8 = Ease(UnknownA8, targetRoll);
        Orientation = Orientation.Multiply(FMatrix.FromYawPitchRoll(yaw, pitch + UnknownA4, roll + UnknownA8));

        static float Ease(float v, float target) =>
            target - 0.01f > v ? v + 0.01f : target + 0.01f < v ? v - 0.01f : target;
    }

    /// <summary>ProcessJet (004250f0): the turn rates; in flight the leans decay towards level.</summary>
    private void ProcessJet()
    {
        BattleEngine be = _engine;
        Orientation = BaseOrientation;
        float roll = ((float)Math.Cos(ShakeTime) * Shake[3]) - be.Yawvel;
        float yaw = -be.Yawvel;
        float pitch = be.Pitchvel * -2.0f;
        UnknownA4 = UnknownA4 < -0.01f ? UnknownA4 + 0.01f : UnknownA4 > 0.01f ? UnknownA4 - 0.01f : 0;
        UnknownA8 = UnknownA8 < -0.01f ? UnknownA8 + 0.01f : UnknownA8 > 0.01f ? UnknownA8 - 0.01f : 0;
        Orientation = Orientation.Multiply(FMatrix.FromYawPitchRoll(yaw, pitch + UnknownA4, roll + UnknownA8));
    }

    private float Fraction => _context.Game?.Loop.Fraction ?? 0.0f;

    /// <summary>GetRenderFrame (00425410).</summary>
    public float RenderFrame => OldFrame + ((Frame - OldFrame) * Fraction);

    public int RealAnimIndex => _realAnimIndex;

    /// <summary>GetRenderPos (00425430): the engine's position plus the shake, between the last two updates.</summary>
    public FVector RenderPos
    {
        get
        {
            FVector pos = _engine.Pos + ShakePos;
            FVector old = _engine.OldPos + OldShakePos;
            return old + ((pos - old) * Fraction);
        }
    }

    /// <summary>GetRenderOrientation (004254f0): the engine's times the cockpit's, between the last two updates, renormalised.</summary>
    public FMatrix RenderOrientation
    {
        get
        {
            FMatrix ori = _engine.GetOrientation().Multiply(Orientation);
            FMatrix old = _engine.OldOrientation.Multiply(OldOrientation);
            return old.Add(ori.Add(old.Scale(-1.0f)).Scale(Fraction)).NormaliseRows();
        }
    }

    public bool HasMotionController => false;

    public bool RenderUseHierarchy(int partNumber) => true;
}
