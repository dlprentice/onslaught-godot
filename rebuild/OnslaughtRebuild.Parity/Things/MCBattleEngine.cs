// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MCMech.cpp (CMCMech: the constructor 004983b0, HandleEvent 00498870, ProcessMovement
// 004988b0, SetMechParameters 00498bf0, Init 00498c40, GetFootHeight 00499bc0, AddMovement 00499d60, UpdateBone 00499e30,
// UpdateBoneHierarchyRecursive 0049bd50, GetPartTransform 0049be00, AdjustFrame 0049c1d0) and src/MCBattleEngine.h and
// MCBattleEngine.cpp (CMCBattleEngine: slot 10 FALSE, slot 11 the engine's CalculatePitch, slot 13 90, slot 14 the
// multiplayer foot trace). Reconstructed; not in Stuart's release. The decomp's byte-matching stand-ins (operand orders,
// helper spellings) are ported as their arithmetic: the bones feed drawing only, so they need no bit-exactness here.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CMCBattleEngine: the walker's motion controller (a CMCMech). Each frame its event 3000 rebuilds the legs' bones: the
/// body leans with the ground and its stepping legs; a leg steps when its foot has fallen behind and takes the LegMotion
/// frame whose length reaches the foot; a foot lies along the ground; a toe bends down to it. The bones feed drawing only
/// (the battle engine's simulated state never reads them). A rebuild runs only when something asked for a transform
/// since the last one (drawing does, through <see cref="Rendered"/>), or in the first five, or before panning.
/// </summary>
public sealed class MCBattleEngine : Monitor
{
    public const int ProcessEvent = 3000;
    private const float Pi = 3.14159265358979f;

    private readonly ThingContext _context;
    private readonly BattleEngine _thing;
    private bool _attached = true;

    /// <summary>CMotionController::SetThing: this controller is detached for jet mode and restored unchanged on landing.</summary>
    public void SetAttached(bool attached) => _attached = attached;
    public bool IsAttached => _attached;

    // SetMechParameters(0, 400, 1, 0.4, 0.9, 0.05, 2), as CBattleEngine::Init sets them.
    private float _paramA;
    private float _paramB = 200.0f;
    private float _paramC = 1.0f;
    private float _paramD = 0.4f;
    private float _paramE = 0.9f;
    private float _paramF = 0.05f;
    private int _paramG;

    private MeshData? _mesh;
    private int _numLegs;
    private int _numToes;
    private int _numFrames;
    private int _firstFrame;
    private int _toeFirstFrame;
    private int _toeLastFrame;
    private FVector[] _legPos = [];
    private bool[] _legA = [];
    private float[] _legB = [];
    private int[] _legC = [];
    private FVector[] _legTarget = [];
    private int[] _toeFrames = [];
    private FVector[] _partPos = [];
    private FMatrix[] _partOri = [];
    private float[] _partFrame = [];
    private FVector[] _oldPartPos = [];
    private FMatrix[] _oldPartOri = [];
    private float[] _oldPartFrame = [];
    private int[] _partTypes = [];
    private int[] _partLegs = [];
    private float[][] _legFrames = [];
    private FMatrix[][] _legOris = [];
    private FVector[]?[] _toeOffsets = [];
    private float _lastTime = -1.0f;
    private bool _used;
    private bool _wasUsed;
    private FMatrix _bodyOri = FMatrix.Identity;
    private FVector _unknown88;

    public MCBattleEngine(ThingContext context, BattleEngine thing)
    {
        _context = context;
        _thing = thing;
        _context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame);
        _context.MotionControllers.Add(this);
    }

    /// <summary>mProcessCount: the bone rebuilds so far.</summary>
    public int ProcessCount { get; private set; }

    /// <summary>mAnimationBlend: towards the engine's own animation (level 100 keeps 0).</summary>
    public float AnimationBlend { get; set; }

    /// <summary>SetMechParameters (00498bf0).</summary>
    public void SetMechParameters(float a, float b, float c, float d, float e, float f, int g)
    {
        (_paramA, _paramB, _paramC, _paramF, _paramD, _paramE, _paramG) = (a, b, c, f, d, e, g);
    }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == ProcessEvent)
        {
            ProcessMovement(false);
            _context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame, EventPriority.StartOfFrame, null, e as ScheduledEvent);
        }
    }

    /// <summary>
    /// The engine was drawn: its parts' transforms were asked for (GetPartTransform / AdjustFrame), which first rebuilds
    /// the bones (forced, a 0.05 s step) when the last rebuild was not asked for, then marks them used.
    /// </summary>
    public void Rendered()
    {
        if (!_attached) return;
        if (!_wasUsed)
        {
            ProcessMovement(true);
        }

        _used = true;
    }

    /// <summary>ProcessMovement (004988b0).</summary>
    public void ProcessMovement(bool force)
    {
        _wasUsed = _used;
        if (!_used && _context.Game!.State >= GameState.Panning && !force && ProcessCount >= 5)
        {
            return;
        }

        if (!_attached) return;

        if (_thing.RenderThing?.MeshOrNull is not { } mesh || mesh.Parts.Count == 0 || mesh.Parts[0] is not { } root)
        {
            return;
        }

        ProcessCount++;
        float dt = force ? 0.05f : _context.Events.Time - _lastTime;
        UpdateBoneHierarchyRecursive(new FVector(0, 0, 0), FMatrix.Identity, root, -1, 0, dt);
        _lastTime = _context.Events.Time;
        _used = false;
    }

    /// <summary>AddMovement (00499d60): the legs, their targets and the parts move with the engine's forced movement.</summary>
    public void AddMovement(FVector movement)
    {
        for (int i = 0; i < _numLegs; i++)
        {
            _legPos[i] += movement;
            _legTarget[i] += movement;
        }

        for (int i = 0; i < _partPos.Length; i++)
        {
            _partPos[i] += movement;
        }
    }

    /// <summary>
    /// GetPartTransform (0049be00), for drawing: the part's transform between its last two rebuilds by the render
    /// fraction (the orientation renormalised), in the world, or in the engine's space when not <paramref name="world"/>.
    /// Null before the first rebuild.
    /// </summary>
    public (FVector Pos, FMatrix Ori)? GetPartTransform(int partNumber, float fraction, bool world = true)
    {
        if (_oldPartFrame.Length == 0)
        {
            return null;
        }

        FVector pos;
        FMatrix ori;
        if (_oldPartFrame[partNumber] != -1.0f)
        {
            pos = _oldPartPos[partNumber] + ((_partPos[partNumber] - _oldPartPos[partNumber]) * fraction);
            ori = _oldPartOri[partNumber].Add(_partOri[partNumber].Add(_oldPartOri[partNumber].Scale(-1.0f)).Scale(fraction)).NormaliseRows();
        }
        else
        {
            pos = _partPos[partNumber];
            ori = _partOri[partNumber];
        }

        if (!world)
        {
            var subject = (IRenderSubject)_thing;
            FMatrix inv = subject.RenderOrientation.Inverse();
            pos = inv.Transform(pos - subject.RenderPos);
            ori = inv.Multiply(ori);
        }

        return (pos, ori);
    }

    /// <summary>AdjustFrame (0049c1d0): the part's animation frame between its last two rebuilds.</summary>
    public float? GetPartFrame(int partNumber, float fraction)
    {
        if (_oldPartFrame.Length == 0)
        {
            return null;
        }

        return _oldPartFrame[partNumber] != -1.0f
            ? ((_partFrame[partNumber] - _oldPartFrame[partNumber]) * fraction) + _oldPartFrame[partNumber]
            : _partFrame[partNumber];
    }

    private void Init(MeshData mesh)
    {
        _mesh = mesh;
        _numLegs = 1;
        while (mesh.GetEmitterSubpart("Footbase", _numLegs) is not null)
        {
            _numLegs++;
        }

        _numLegs--;
        _numToes = 1;
        while (mesh.GetEmitterSubpart("toestop", _numToes) is not null)
        {
            _numToes++;
        }

        _numToes = (_numToes - 1) / Math.Max(_numLegs, 1);
        if (_numToes == 0)
        {
            _numToes = 1;
        }

        _legPos = new FVector[_numLegs];
        _legA = new bool[_numLegs];
        _legB = new float[_numLegs];
        _legC = new int[_numLegs];
        _legTarget = new FVector[_numLegs];
        _toeFrames = new int[_numLegs * _numToes];
        if (mesh.GetAnimModePtr(mesh.GetAnimModeByName("ToeMotion")) is { } toe)
        {
            _toeFirstFrame = toe.Start;
            _toeLastFrame = toe.Last;
        }

        Array.Fill(_toeFrames, _toeFirstFrame);
        int parts = mesh.Parts.Count;
        _partPos = new FVector[parts];
        _partOri = new FMatrix[parts];
        _partFrame = new float[parts];
        _oldPartPos = new FVector[parts];
        _oldPartOri = new FMatrix[parts];
        _oldPartFrame = new float[parts];
        _partTypes = new int[parts];
        _partLegs = new int[parts];
        for (int i = 0; i < parts; i++)
        {
            _partFrame[i] = -1.0f;
            _oldPartFrame[i] = -1.0f;
            string name = mesh.Parts[i].Name;
            if (name.Equals("Body", StringComparison.OrdinalIgnoreCase))
            {
                _partTypes[i] = 1;
            }
            else if (name.StartsWith("Leg", StringComparison.OrdinalIgnoreCase))
            {
                _partTypes[i] = 2;
                _partLegs[i] = Atoi(name, 3);
            }
            else if (name.StartsWith("Foot", StringComparison.OrdinalIgnoreCase))
            {
                _partTypes[i] = 3;
            }
            else if (name.StartsWith("Toe", StringComparison.OrdinalIgnoreCase))
            {
                _partTypes[i] = 4;
                _partLegs[i] = Atoi(name, 3) - 1;
            }
            else if (name.Equals("turret", StringComparison.OrdinalIgnoreCase))
            {
                _partTypes[i] = 5;
            }
        }

        AnimMode legMotion = mesh.GetAnimModePtr(mesh.GetAnimModeByName("LegMotion"))
            ?? throw new InvalidOperationException("CMCMech::Init: the mesh has no LegMotion");
        _firstFrame = legMotion.Start;
        _numFrames = legMotion.Last - legMotion.Start + 1;
        _legFrames = new float[_numFrames][];
        _legOris = new FMatrix[_numFrames][];
        var subject = (IRenderSubject)_thing;
        for (int i = 0; i < _numFrames; i++)
        {
            _legFrames[i] = new float[_numLegs];
            _legOris[i] = new FMatrix[_numLegs];
            for (int l = 0; l < _numLegs; l++)
            {
                MeshPart footbase = mesh.GetEmitterSubpart("Footbase", l + 1)!;
                footbase.GetWorldTransforms(_firstFrame + i, 0, 0, subject, out _, out FVector bpos, true, false, _context.TransformStatics, controller: false);
                float length = 0;
                foreach (MeshPart p in mesh.Parts)
                {
                    if (p.Name.StartsWith("Leg", StringComparison.OrdinalIgnoreCase) && Atoi(p.Name, 3) == l + 1)
                    {
                        p.GetWorldTransforms(_firstFrame + i, 0, 0, subject, out _, out FVector lpos, true, false, _context.TransformStatics, controller: false);
                        FVector d = lpos - bpos;
                        length = d.Magnitude();
                        _legOris[i][l] = FMatrix.MakeFromDirection(d, new FVector(0, -1, 0));
                    }
                }

                _legFrames[i][l] = length;
            }
        }

        _toeOffsets = new FVector[]?[_numLegs * _numToes];
        for (int i = 0; i < _numLegs * _numToes; i++)
        {
            if (mesh.GetEmitterSubpart("Toestop", i + 1) is not { } stop || stop.Parent?.Parent is not { } leg)
            {
                continue;
            }

            var offsets = new FVector[_toeLastFrame - _toeFirstFrame + 1];
            for (int fr = _toeFirstFrame; fr <= _toeLastFrame; fr++)
            {
                stop.GetWorldTransforms(fr, fr, 0, null, out _, out FVector spos, true, false, _context.TransformStatics, controller: false);
                leg.GetWorldTransforms(fr, fr, 0, null, out FMatrix lori, out FVector lpos, true, false, _context.TransformStatics, controller: false);
                offsets[fr - _toeFirstFrame] = lori.Inverse().Transform(spos - lpos);
            }

            _toeOffsets[i] = offsets;
        }
    }

    private static int Atoi(string s, int start)
    {
        int i = start;
        int v = 0;
        while (i < s.Length && char.IsAsciiDigit(s[i]))
        {
            v = (v * 10) + (s[i] - '0');
            i++;
        }

        return v;
    }

    /// <summary>GetFootHeight (00499bc0): the ground under the foot (the line trace is multiplayer's only).</summary>
    private float GetFootHeight(FVector pos) => _context.Map.Collide(pos);

    /// <summary>
    /// UpdateBoneHierarchyRecursive (0049bd50): the part, then its children with what it left in pos, ori, the leg and
    /// the frame (UpdateBone takes all four by pointer; the children get copies).
    /// </summary>
    private void UpdateBoneHierarchyRecursive(FVector pos, FMatrix ori, MeshPart part, int leg, int frame, float dt)
    {
        UpdateBone(ref pos, ref ori, part, ref leg, ref frame, dt);
        foreach (MeshPart? child in part.Children)
        {
            if (child is not null)
            {
                UpdateBoneHierarchyRecursive(pos, ori, child, leg, frame, dt);
            }
        }
    }

    /// <summary>UpdateBone (00499e30): one part's position and orientation this rebuild.</summary>
    private void UpdateBone(ref FVector pos, ref FMatrix ori, MeshPart part, ref int leg, ref int frame, float dt)
    {
        MeshData mesh = _mesh ?? (_thing.RenderThing!.Mesh);
        if (_numFrames == 0)
        {
            Init(mesh);
        }

        var subject = (IRenderSubject)_thing;
        bool rotated = false;
        bool moved = false;
        FMatrix m = FMatrix.Identity;
        FVector offset = new(0, 0, 0);
        int f = 0;
        int key = part.VHFrameMap[0];
        int partNumber = part.PartNumber;
        int type = _partTypes[partNumber];
        if (type == 1)
        {
            // The body leans with the ground (here with the engine's yaw and pitch) and with its stepping legs.
            float pitch = 0;
            float roll = 0;
            FVector foot = pos + ori.Transform(part.GetKeyPosition(key));
            var r = FMatrix.FromYawPitchRoll(_thing.CurrentOrientation.Yaw, _thing.CurrentOrientation.Pitch, 0);
            FVector up = new(-r.Row0.Z, -r.Row1.Z, -r.Row2.Z);
            up = (up * (1.0f - _paramE)) + (_unknown88 * _paramE);
            _unknown88 = up;
            m = FMatrix.MakeFromDirection(up, new FVector(0, -1, 0));
            for (int i = 0; i < _numLegs; i++)
            {
                if (_legC[i] > 0)
                {
                    mesh.GetEmitterSubpart("Footbase", i + 1)!.GetWorldTransforms(0, 0, 0, subject, out _, out FVector p, false, false, _context.TransformStatics, controller: false);
                    FVector d = (p - foot) * (float)Math.Sin((_legC[i] * 3.14f) / 180.0f);
                    d = new FVector(Math.Clamp(d.X, -5.0f, 5.0f), Math.Clamp(d.Y, -5.0f, 5.0f), d.Z);
                    pitch += d.Y * 2;
                    roll += d.X * 2;
                }
            }

            pitch *= _paramA;
            roll *= _paramA;
            FMatrix pm = FMatrix.MakeRotationPitchF((pitch * 3.14f) / 180.0f);
            float rr = (roll * 3.14f) / 180.0f;
            var rm = new FMatrix(new FVector((float)Math.Cos(rr), 0, (float)Math.Sin(rr)), new FVector(0, 1, 0),
                new FVector(-(float)Math.Sin(rr), 0, (float)Math.Cos(rr)));
            if (_thing.IsDying)
            {
                throw new NotSupportedException("CMCMech::UpdateBone for a dying unit (Unknown106) is not ported");
            }

            _bodyOri = m = m.Multiply(pm).Multiply(rm);
        }
        else if (type == 2)
        {
            // A leg steps when its foot has fallen behind, then reaches down to it.
            leg = _partLegs[partNumber];
            int l = leg - 1;
            FVector foot = pos + ori.Transform(part.GetKeyPosition(key));
            mesh.GetEmitterSubpart("Footbase", leg)!.GetWorldTransforms(0, 0, 0, subject, out _, out FVector baseP, false, false, _context.TransformStatics, controller: false);
            FVector d = foot - _legTarget[l];
            _legTarget[l] = foot;
            FVector p = baseP;
            p.Z = GetFootHeight(baseP);
            p += d;
            if (dt > 2.0f)
            {
                _legPos[l] = p;
            }

            FVector v = p - _legPos[l];
            _legB[l] = 0;
            if (_legC[l] > 0)
            {
                _legC[l] += (int)(dt * _paramB);
                if (_legC[l] > 180)
                {
                    _legC[l] = 0;
                }
                else
                {
                    FVector step = v * _legC[l];
                    _legPos[l] += new FVector(step.X / 180.0f, step.Y / 180.0f, step.Z / 180.0f);
                    _legA[l] = false;
                    _legB[l] = -(float)Math.Sin((_legC[l] * 3.14f) / 180.0f) * _paramD;
                }
            }
            else
            {
                float limit;
                float distance;
                if (MathF.Sqrt((d.Y * d.Y) + (d.Z * d.Z) + (d.X * d.X)) > 0.01f)
                {
                    limit = _paramC;
                    distance = v.Magnitude();
                }
                else
                {
                    limit = _paramF;
                    distance = MathF.Sqrt((v.X * v.X) + (v.Y * v.Y));
                }

                if (distance > limit)
                {
                    // Step, unless too many legs are stepping or a neighbour is.
                    int n = 0;
                    bool odd = false;
                    bool even = false;
                    bool ok = true;
                    for (int j = 0; j < _numLegs; j++)
                    {
                        if (_legC[j] != 0 && _legC[j] <= 90)
                        {
                            n++;
                        }

                        int c = _legC[j];
                        if ((c == 0 || c > 90) && j != l)
                        {
                            if ((j & 1) == 1)
                            {
                                odd = true;
                            }
                            else
                            {
                                even = true;
                            }
                        }
                        else if (j == leg - 2)
                        {
                            ok = false;
                        }
                    }

                    bool can = ((l & 1) == 1 ? odd : even) && ok;
                    if (n >= _paramG)
                    {
                        can = false;
                    }

                    if (distance > limit * 2 || can)
                    {
                        _legC[l] = 1;
                    }
                }
            }

            FVector q = _legPos[l];
            q.Z = baseP.Z;
            _legPos[l].Z = GetFootHeight(q);
            FVector rpos = _legPos[l];
            rpos.Z += _legB[l];
            FVector diff = foot - rpos;
            if (!_legA[l] && _legB[l] > -0.01f)
            {
                _legA[l] = true;
            }

            // The LegMotion frame whose length reaches the foot.
            float len = diff.Magnitude();
            int best = 0;
            float bestDist = 0;
            for (int i = 0; i < _numFrames; i++)
            {
                float e = MathF.Abs(_legFrames[i][l] - len);
                if (bestDist == 0 || e < bestDist)
                {
                    bestDist = e;
                    best = i;
                }
            }

            frame = _firstFrame + best;
            var legUp = new FVector(-_bodyOri.Row0.Y, -_bodyOri.Row1.Y, -_bodyOri.Row2.Y);
            moved = true;
            offset = foot;
            m = FMatrix.MakeFromDirection(diff, legUp);
            ori = _bodyOri.Inverse().Multiply(ori);
            ori = ori.Multiply(_legOris[best][l].Inverse());
        }
        else if (type == 3)
        {
            // A foot lies along the ground.
            m = FMatrix.MakeFromDirection(_context.Map.Normal(_legPos[leg - 1]), new FVector(0, -1, 0));
            rotated = true;
        }
        else if (type == 4)
        {
            // A toe takes the ToeMotion frame that puts its tip on the ground.
            int t = _partLegs[partNumber];
            if (mesh.GetEmitterSubpart("Toestop", t + 1) is { } stop && stop.Parent?.Parent is { } footPart)
            {
                int idx = ((leg - 1) * _numToes) + (t % 3);
                if (idx < _numLegs * _numToes && _toeOffsets[t] is { } offsets)
                {
                    while (true)
                    {
                        FVector tip = _partOri[footPart.PartNumber].Transform(offsets[_toeFrames[idx] - _toeFirstFrame])
                            + _partPos[footPart.PartNumber];
                        float h = _context.Map.Collide(tip);
                        if (h + 0.1f < tip.Z)
                        {
                            _toeFrames[idx]--;
                            if (_toeFrames[idx] < _toeFirstFrame)
                            {
                                _toeFrames[idx] = _toeFirstFrame;
                                break;
                            }

                            continue;
                        }

                        if (h - 0.1f > tip.Z)
                        {
                            _toeFrames[idx]++;
                            if (_toeFrames[idx] > _toeLastFrame)
                            {
                                _toeFrames[idx] = _toeLastFrame;
                            }
                        }

                        break;
                    }

                    frame = _toeFrames[idx];
                }
            }
        }
        else if (type == 5)
        {
            throw new NotSupportedException("CMCMech::UpdateBone for a turret part is not ported");
        }

        if (leg != -1)
        {
            f = frame;
            key = part.VHFrameMap[f];
        }

        if (part.PartNumber == 0)
        {
            pos += subject.RenderPos;
            ori = ori.Multiply(subject.RenderOrientation);
        }

        if (!subject.RenderUseHierarchy(part.PartNumber))
        {
            pos = moved ? offset : pos + part.GetKeyPosition(key);
            ori = rotated ? m : m.Multiply(part.GetHFrameOrientation(key));
        }
        else
        {
            pos = moved ? offset : pos + ori.Transform(part.GetKeyPosition(key));
            ori = rotated ? m : m.Multiply(ori.Multiply(part.GetHFrameOrientation(key)));
        }

        if (AnimationBlend != 0)
        {
            throw new NotSupportedException("CMCMech::UpdateBone's animation blend (mAnimationBlend) is not ported");
        }

        if (ProcessCount < 3)
        {
            _oldPartPos[partNumber] = pos;
            _oldPartOri[partNumber] = ori;
            _oldPartFrame[partNumber] = f;
        }
        else
        {
            _oldPartPos[partNumber] = _partPos[partNumber];
            _oldPartOri[partNumber] = _partOri[partNumber];
            _oldPartFrame[partNumber] = _partFrame[partNumber];
        }

        _partPos[partNumber] = pos;
        _partOri[partNumber] = ori;
        _partFrame[partNumber] = f;
    }
}
