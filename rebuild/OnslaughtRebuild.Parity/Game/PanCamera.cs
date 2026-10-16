// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/BSpline.cpp (CBSpline: the constructor 00416d10, the destructor, BasisFunction 00416e30,
// GetPoint 00416fc0; reconstructed, not in Stuart's release) and src/Camera.cpp (CPanCamera: the constructor 004198d0,
// the destructor 00419a60, Update 00419b00, GetPos 00419d40, HandleEvent 00419de0; in Stuart's release). Read from the
// Steam BEA.exe (= the pristine specimen 74154bfa): every operation is one 24-bit rounding of two operands (the
// basis's knot differences are integer divisors, fidiv), so float arithmetic in the source's operations matches.

using System.Runtime.CompilerServices;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// CBSpline: a uniform B-spline through points (open uniform integer knots, the Cox-de Boor basis). The points are in a
/// set over the shared pool, as retail's SPtrSet&lt;FVector&gt; is, and are freed with the spline.
/// </summary>
public sealed class BSpline
{
    private readonly SPtrSet<StrongBox<FVector>> _points;
    private readonly int _order;
    private readonly int _num;
    private readonly int[] _knots;

    public BSpline(SPtrSet<StrongBox<FVector>> points, int order = 3)
    {
        _points = points;
        _order = order;
        _num = points.Size - 1;
        _knots = new int[_num + _order + 1];
        for (int j = 0; j <= _num + _order; j++)
        {
            _knots[j] = j < _order ? 0 : j <= _num ? j - _order + 1 : _num - _order + 2;
        }
    }

    /// <summary>~CBSpline: the points are deleted and their nodes go back to the pool.</summary>
    public void Delete() => _points.RemoveAll();

    /// <summary>BasisFunction: point i's basis of order k at t.</summary>
    public float BasisFunction(int i, int k, float t)
    {
        if (k == 1)
        {
            return _knots[i] <= t && t < _knots[i + 1] ? 1.0f : 0.0f;
        }

        if (_knots[i + k - 1] == _knots[i] && _knots[i + k] == _knots[i + 1])
        {
            return 0.0f;
        }

        if (_knots[i + k - 1] == _knots[i])
        {
            return (_knots[i + k] - t) / (_knots[i + k] - _knots[i + 1]) * BasisFunction(i + 1, k - 1, t);
        }

        if (_knots[i + k] == _knots[i + 1])
        {
            return (t - _knots[i]) / (_knots[i + k - 1] - _knots[i]) * BasisFunction(i, k - 1, t);
        }

        float first = (t - _knots[i]) / (_knots[i + k - 1] - _knots[i]) * BasisFunction(i, k - 1, t);
        float second = (_knots[i + k] - t) / (_knots[i + k] - _knots[i + 1]) * BasisFunction(i + 1, k - 1, t);
        return first + second;
    }

    /// <summary>GetPoint: the curve at t, 0 at the first point and 1 at the last (the set's own iterator walks the points).</summary>
    public FVector GetPoint(float t)
    {
        var result = new FVector(0, 0, 0);
        t *= _num - _order + 2;
        StrongBox<FVector>? point = _points.First();
        for (int i = 0; i <= _num; i++)
        {
            float b = BasisFunction(i, _order, t);
            result.X += point!.Value.X * b;
            result.Y += point.Value.Y * b;
            result.Z += point.Value.Z * b;
            point = _points.Next();
        }

        return result;
    }
}

/// <summary>
/// CPanCamera: a camera moving along a spline around a thing over a length of time, updated at the end of each frame
/// (UPDATE_CAMERA) until the time is up. Its orientation (towards the thing) is drawing-only and not kept.
/// </summary>
public sealed class PanCamera : Camera
{
    /// <summary>UPDATE_CAMERA.</summary>
    public const int UpdateCamera = 2000;

    private readonly ThingContext _context;
    private readonly ActiveReader<Thing> _forThing = new();
    private readonly BSpline _curve;
    private readonly float _startTime;
    private readonly float _length;
    private FVector _pos;
    private FMatrix _orientation = FMatrix.Identity;

    public PanCamera(ThingContext context, Thing? forThing, BSpline curve, float length)
    {
        _context = context;
        _forThing.SetReader(forThing);
        _curve = curve;
        _startTime = context.Events.Time;
        _length = length;
        _pos = new FVector(0, 0, 0);
        Update();
        OldPos = _pos;
        OldOrientation = _orientation;
    }

    public override FVector GetPos() => _pos;

    /// <summary>GetOrientation (00419d70): facing the thing, from the last update.</summary>
    public override FMatrix GetOrientation() => _orientation;

    /// <summary>GetOldPos (00419d90).</summary>
    public override FVector GetOldPos() => OldPos;

    /// <summary>GetOldOrientation (00419dc0).</summary>
    public override FMatrix GetOldOrientation() => OldOrientation;

    /// <summary>The previous update's position and orientation (mOldPos, mOldOrientation), for the render's interpolation.</summary>
    public FVector OldPos { get; private set; }

    public FMatrix OldOrientation { get; private set; } = FMatrix.Identity;

    /// <summary>
    /// Update: at (now - start) / length along the curve (stopping, with no further update, past 1), the thing's
    /// position plus the curve's point; again at the end of next frame.
    /// </summary>
    public void Update()
    {
        OldPos = _pos;
        OldOrientation = _orientation;
        float val = (_context.Events.Time - _startTime) / _length;
        if (val > 1.0f)
        {
            return;
        }

        FVector pos = _curve.GetPoint(val);
        if (_forThing.ToRead is { } thing)
        {
            _pos = thing.Pos + pos;

            // The view faces the thing: FMatrix(Azimuth, Elevation, 0). Presentation only (nothing in the simulation
            // reads it); its float sequence is not checked against retail's.
            FVector toThem = thing.Pos - _pos;
            float m = Collision.Geometry.MagnitudeOutOfLine(toThem);
            if (m != 0.0f)
            {
                float azimuth = -(float)Numerics.X87.Atan2(toThem.X, toThem.Y);
                float elevation = (float)Numerics.X87.Asin(toThem.Z / m);
                _orientation = FMatrix.FromYawPitchRoll(azimuth, elevation, 0.0f);
            }
        }

        _context.Events.AddEvent(UpdateCamera, this, EventManager.NextFrame, EventPriority.EndOfFrame);
    }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == UpdateCamera)
        {
            Update();
        }
    }

    /// <summary>~CPanCamera: the curve goes, then the readers are told (a pending update finds no one).</summary>
    public override void Release()
    {
        _curve.Delete();
        Destroy();
    }
}
