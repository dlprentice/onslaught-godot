// SPDX-License-Identifier: GPL-3.0-only
// CViewPointCamera 00419e00..0041a202, matched bea-decomp Camera.cpp.
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// The camera installed on player death. Retail's constructor accidentally initializes local timestamp variables,
/// leaving its two cache timestamps uninitialized. Its equality branch also accepts unordered (NaN) comparisons.
/// Defined rebuild boundary: both caches begin invalid and the first getter computes. This reproduces the measured
/// nonmatching-heap branch, not every possible retail first-use heap state. Later same-time calls retain the cache.
/// Arithmetic follows the original instructions, including the distance ramp's missing start-distance addition.
/// </summary>
public sealed class ViewPointCamera : Camera
{
    private readonly EventManager _events;
    private readonly FVector _lookAtPoint;
    private readonly float _rotateYawSpeed, _startDistance, _endDistance, _timeBetweenDistances, _startTime;
    private bool _positionValid, _orientationValid;
    private float _lastPositionTime, _lastOrientationTime;
    private FVector _position, _oldPosition;
    private FMatrix _orientation = FMatrix.Identity, _oldOrientation = FMatrix.Identity;

    public ViewPointCamera(EventManager events, FVector point, float rotateSpeed, float startDistance,
        float endDistance, float timeBetweenDistances)
    {
        _events = events;
        _lookAtPoint = _position = _oldPosition = point;
        _rotateYawSpeed = rotateSpeed;
        _startDistance = startDistance;
        _endDistance = endDistance;
        _timeBetweenDistances = timeBetweenDistances;
        _startTime = events.Time;
    }

    public override FVector GetPos()
    {
        float now = _events.Time;
        if (_positionValid && _lastPositionTime == now) return _position;
        _oldPosition = _position;
        float yaw = (float)X87.Mul(X87.Sub(now, _startTime), _rotateYawSpeed);
        FMatrix matrix = FMatrix.MakeRotationOutOfLine(yaw, 0, 0);
        double distance = now >= X87.Add(_startTime, _timeBetweenDistances) ? _endDistance
            : X87.Mul(X87.Div(X87.Sub(_endDistance, _startDistance), _timeBetweenDistances),
                X87.Sub(now, _startTime));
        double Offset(FVector row) => X87.Add(X87.Mul(row.Z, -distance), X87.Mul(row.X, distance));
        _position = new FVector(
            (float)X87.Add((float)Offset(matrix.Row0), _lookAtPoint.X),
            (float)X87.Add((float)Offset(matrix.Row1), _lookAtPoint.Y),
            (float)X87.Add(Offset(matrix.Row2), _lookAtPoint.Z));
        _lastPositionTime = now;
        _positionValid = true;
        return _position;
    }

    public override FMatrix GetOrientation()
    {
        float now = _events.Time;
        if (_orientationValid && _lastOrientationTime == now) return _orientation;
        _oldOrientation = _orientation;
        float yaw = (float)X87.Mul(X87.Sub(now, _startTime), _rotateYawSpeed);
        FMatrix matrix = FMatrix.MakeRotationOutOfLine(yaw, 0, 0);
        float x = (float)X87.Add(X87.Mul(matrix.Row0.X, -1), matrix.Row0.Z);
        float y = (float)X87.Add(X87.Mul(matrix.Row1.X, -1), matrix.Row1.Z);
        double z = X87.Add(X87.Mul(matrix.Row2.X, -1), matrix.Row2.Z);
        double Magnitude() => X87.Sqrt(X87.Add(X87.Add(X87.Mul(y, y), X87.Mul(z, z)), X87.Mul(x, x)));
        double magnitude = Magnitude();
        if (magnitude != 0)
        {
            double reciprocal = X87.Div(1, magnitude);
            x = (float)X87.Mul(x, reciprocal);
            y = (float)X87.Mul(y, reciprocal);
            z = X87.Mul(z, reciprocal);
        }
        float storedMagnitude = (float)Magnitude();
        float pitch = storedMagnitude > 0 ? (float)X87.Asin(X87.Div((float)z, storedMagnitude)) : 0;
        float facing = (float)-X87.Atan2(x, y);
        _orientation = FMatrix.MakeRotationOutOfLine(facing, pitch, 0);
        _lastOrientationTime = now;
        _orientationValid = true;
        return _orientation;
    }

    public override FVector GetOldPos() => _oldPosition;
    public override FMatrix GetOldOrientation() => _oldOrientation;
}
