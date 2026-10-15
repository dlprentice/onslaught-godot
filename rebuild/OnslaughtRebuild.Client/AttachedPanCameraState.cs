// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client;

/// <summary>The event-manager phase that owns a pan-camera pose transition.</summary>
public enum AttachedPanCameraUpdatePhase
{
    EndOfEventFrame,
}

/// <summary>
/// A Godot-free camera pose in the Client's existing render coordinate system.
/// </summary>
public readonly record struct ClientCameraPose(
    Level100RenderVector3 Position,
    Level100RenderVector3 Forward,
    Level100RenderVector3 Up)
{
    public static ClientCameraPose Identity => new(
        default,
        new Level100RenderVector3(0f, 0f, -1f),
        new Level100RenderVector3(0f, 1f, 0f));
}

/// <summary>
/// Stable thing identity plus the attached camera pose and the right vector used
/// to construct Level 100's four-point opening spline.
/// </summary>
public readonly record struct AttachedCameraThingSnapshot(
    Level100ActorId ThingId,
    ClientCameraPose Pose,
    Level100RenderVector3 PanRight);

/// <summary>One input sampled at an event-manager update boundary.</summary>
public readonly record struct AttachedPanCameraFrame(
    int EventFrame,
    int PanElapsedTicks,
    int ZoomPermille,
    AttachedCameraThingSnapshot? AttachedThing);

/// <summary>
/// Previous/current pan state after one explicit end-of-event-frame update.
/// </summary>
public sealed record AttachedPanCameraSnapshot(
    int PanDurationTicks,
    int ControlViewHandoffTick,
    int ResetGeneration,
    AttachedPanCameraUpdatePhase UpdatePhase,
    AttachedPanCameraFrame PreviousFrame,
    AttachedPanCameraFrame CurrentFrame,
    ClientCameraPose PreviousPanPose,
    ClientCameraPose CurrentPanPose,
    bool PanUpdateScheduled)
{
    /// <summary>Canonical SHA-256 over every future presentation input.</summary>
    public string ComputeHash() => AttachedPanCameraState.ComputeHash(this);
}

/// <summary>The render-time projection sampled between two camera boundaries.</summary>
public readonly record struct AttachedPanCameraViewSnapshot(
    Level100ActorId? AttachedThingId,
    ClientCameraPose Pose,
    float Zoom,
    bool HudVisible,
    bool OpeningPanActive);

/// <summary>
/// Deterministic Client owner for the one attached/pan camera lifecycle already
/// consumed by <c>FirstFlightWorldView</c>.
/// </summary>
/// <remarks>
/// <para>
/// Source shape: <c>references/Onslaught/Camera.h:19-140</c> and
/// <c>Camera.cpp:344-393</c>. <c>CPanCamera::Update</c> first shadows current
/// pose into old pose, then samples its attached thing, and finally schedules
/// <c>UPDATE_CAMERA</c> for <c>NEXT_FRAME, END_OF_FRAME</c>. A missing active
/// reader retains the pan pose and still schedules the next update. The attached
/// camera carries thing identity/pose and the Battle Engine's old/current zoom.
/// </para>
/// <para>
/// This is not another camera framework. It carries only the existing Level 100
/// four-point pan, control-view handoff, attached first-person pose, zoom and HUD
/// state. Core remains the simulation owner; this type has no renderer, GPU,
/// filesystem, clock, process, or network dependency.
/// </para>
/// <para>
/// The duration is deliberately caller-supplied. Source
/// <c>game.cpp:303</c> supplies 3.0 seconds, while two controlled Steam Level 100
/// runs measured <c>CPanCamera.mLength = 6.0</c> and the source-shaped handoff at
/// <c>duration - 0.05</c>. The current Level 100 adapter therefore supplies 120
/// 20 Hz ticks and a one-event-frame lead; this type does not turn either value
/// into universal retail equality.
/// </para>
/// <para>
/// Retail constructor and ABI equality also remain bounded: the named
/// <c>CPanCamera__ctor</c> plate at <c>0x004198D0</c> is SOURCE_ANALOG, not an
/// exact constructor proof. This seam ports the reviewed state/update contract,
/// not unresolved allocation, vtable, monitor, or destructor boundaries.
/// </para>
/// </remarks>
public sealed class AttachedPanCameraState
{
    private const float UnitsToMeters = 0.001f;
    private const int HashSchemaVersion = 1;
    private static readonly Level100RenderVector3 WorldUp = new(0f, 1f, 0f);

    private readonly int _panDurationTicks;
    private readonly int _controlViewHandoffTick;
    private AttachedPanCameraSnapshot? _snapshot;

    public AttachedPanCameraState(
        int panDurationTicks,
        int controlViewHandoffLeadTicks)
    {
        if (panDurationTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(panDurationTicks));
        }
        if (controlViewHandoffLeadTicks < 0 ||
            controlViewHandoffLeadTicks >= panDurationTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(controlViewHandoffLeadTicks));
        }

        _panDurationTicks = panDurationTicks;
        _controlViewHandoffTick =
            panDurationTicks - controlViewHandoffLeadTicks;
    }

    public AttachedPanCameraSnapshot CurrentSnapshot =>
        _snapshot ?? throw new InvalidOperationException(
            "No camera event frame has been supplied.");

    /// <summary>
    /// Adapts the fixed-step snapshots the existing world view already receives
    /// into this one camera state owner. Re-observing the same frame is
    /// idempotent, so render-only frames do not advance the event schedule.
    /// </summary>
    public AttachedPanCameraSnapshot Advance(
        WorldSnapshot previous,
        WorldSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        AttachedPanCameraFrame currentFrame = CreateFrame(current);
        if (_snapshot is { } observed && currentFrame == observed.CurrentFrame)
        {
            return observed;
        }

        AttachedPanCameraFrame previousFrame = CreateFrame(previous);
        if (_snapshot is null ||
            previousFrame.EventFrame >= _snapshot.CurrentFrame.EventFrame)
        {
            AdvanceAtEndOfEventFrame(previousFrame);
        }
        return AdvanceAtEndOfEventFrame(currentFrame);
    }

    /// <summary>
    /// Performs the source-shaped old/current transition at one explicit
    /// <see cref="AttachedPanCameraUpdatePhase.EndOfEventFrame"/> boundary.
    /// </summary>
    public AttachedPanCameraSnapshot AdvanceAtEndOfEventFrame(
        AttachedPanCameraFrame frame)
    {
        Validate(frame);

        if (_snapshot is null)
        {
            return ResetTo(frame, resetGeneration: 0);
        }

        AttachedPanCameraSnapshot current = _snapshot;
        bool reset = frame.EventFrame < current.CurrentFrame.EventFrame ||
            frame.PanElapsedTicks < current.CurrentFrame.PanElapsedTicks;
        if (reset)
        {
            return ResetTo(frame, checked(current.ResetGeneration + 1));
        }

        if (frame.EventFrame == current.CurrentFrame.EventFrame)
        {
            if (frame == current.CurrentFrame)
            {
                return current;
            }

            throw new InvalidOperationException(
                "One event frame cannot carry two different camera inputs.");
        }

        ClientCameraPose previousPanPose = current.CurrentPanPose;
        ClientCameraPose nextPanPose =
            current.PanUpdateScheduled && frame.AttachedThing is { } attached
            ? EvaluatePanPose(attached, frame.PanElapsedTicks, previousPanPose)
            : previousPanPose;

        _snapshot = new AttachedPanCameraSnapshot(
            _panDurationTicks,
            _controlViewHandoffTick,
            current.ResetGeneration,
            AttachedPanCameraUpdatePhase.EndOfEventFrame,
            current.CurrentFrame,
            frame,
            previousPanPose,
            nextPanPose,
            PanIsCurrent(frame));
        return _snapshot;
    }

    /// <summary>
    /// Samples the current old/new pair without advancing the event owner.
    /// </summary>
    public AttachedPanCameraViewSnapshot Sample(float interpolationAlpha)
    {
        if (!float.IsFinite(interpolationAlpha) ||
            interpolationAlpha is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(interpolationAlpha));
        }

        AttachedPanCameraSnapshot snapshot = CurrentSnapshot;
        float elapsedTicks = Lerp(
            snapshot.PreviousFrame.PanElapsedTicks,
            snapshot.CurrentFrame.PanElapsedTicks,
            interpolationAlpha);
        bool openingPanActive = elapsedTicks < _controlViewHandoffTick;
        if (openingPanActive)
        {
            return new AttachedPanCameraViewSnapshot(
                snapshot.CurrentFrame.AttachedThing?.ThingId,
                Interpolate(
                    snapshot.PreviousPanPose,
                    snapshot.CurrentPanPose,
                    interpolationAlpha),
                RetailCameraLaws.DefaultZoom,
                HudVisible: false,
                OpeningPanActive: true);
        }

        AttachedCameraThingSnapshot? attached =
            snapshot.CurrentFrame.AttachedThing;
        if (attached is null)
        {
            // CThingCamera's missing-active-reader arms return zero position,
            // identity orientation, unit zoom, and no HUD.
            return new AttachedPanCameraViewSnapshot(
                null,
                ClientCameraPose.Identity,
                RetailCameraLaws.DefaultZoom,
                HudVisible: false,
                OpeningPanActive: false);
        }

        ClientCameraPose attachedPose = attached.Value.Pose;
        if (snapshot.PreviousFrame.AttachedThing is { } previousAttached &&
            previousAttached.ThingId == attached.Value.ThingId &&
            Level100RenderInterpolation.DistanceSquared(
                previousAttached.Pose.Position,
                attached.Value.Pose.Position) <=
                    Level100RenderInterpolation.TeleportMeters *
                    Level100RenderInterpolation.TeleportMeters)
        {
            attachedPose = Interpolate(
                previousAttached.Pose,
                attached.Value.Pose,
                interpolationAlpha);
        }

        float zoomPermille = Lerp(
            snapshot.PreviousFrame.ZoomPermille,
            snapshot.CurrentFrame.ZoomPermille,
            interpolationAlpha);
        return new AttachedPanCameraViewSnapshot(
            attached.Value.ThingId,
            attachedPose,
            zoomPermille / SimulationConstants.ZoomScale,
            HudVisible: true,
            OpeningPanActive: false);
    }

    public static string ComputeHash(AttachedPanCameraSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(HashSchemaVersion);
            writer.Write(snapshot.PanDurationTicks);
            writer.Write(snapshot.ControlViewHandoffTick);
            writer.Write(snapshot.ResetGeneration);
            writer.Write((int)snapshot.UpdatePhase);
            WriteFrame(writer, snapshot.PreviousFrame);
            WriteFrame(writer, snapshot.CurrentFrame);
            WritePose(writer, snapshot.PreviousPanPose);
            WritePose(writer, snapshot.CurrentPanPose);
            writer.Write(snapshot.PanUpdateScheduled);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()))
            .ToLowerInvariant();
    }

    private AttachedPanCameraSnapshot ResetTo(
        AttachedPanCameraFrame frame,
        int resetGeneration)
    {
        ClientCameraPose panPose = frame.AttachedThing is { } attached
            ? EvaluatePanPose(attached, frame.PanElapsedTicks, ClientCameraPose.Identity)
            : ClientCameraPose.Identity;
        _snapshot = new AttachedPanCameraSnapshot(
            _panDurationTicks,
            _controlViewHandoffTick,
            resetGeneration,
            AttachedPanCameraUpdatePhase.EndOfEventFrame,
            frame,
            frame,
            panPose,
            panPose,
            PanIsCurrent(frame));
        return _snapshot;
    }

    private AttachedPanCameraFrame CreateFrame(WorldSnapshot world)
    {
        int panElapsedTicks = checked(
            _panDurationTicks - world.Level100OpeningTicksRemaining);
        if (panElapsedTicks < 0)
        {
            throw new InvalidOperationException(
                "Core exposed more opening-pan ticks than the caller supplied duration.");
        }

        Level100ActorSnapshot? player = world.Level100Actors.Actors.SingleOrDefault(
            actor => StringComparer.Ordinal.Equals(actor.Name, "Player 1"));
        AttachedCameraThingSnapshot? attached = player is null
            ? null
            : CreateAttachedThing(player.ActorId, world);
        return new AttachedPanCameraFrame(
            world.Tick,
            panElapsedTicks,
            world.ZoomPermille,
            attached);
    }

    private static AttachedCameraThingSnapshot CreateAttachedThing(
        Level100ActorId thingId,
        WorldSnapshot world)
    {
        float yaw = world.FacingYawMicroRad / 1_000_000f;
        float pitch = world.FacingPitchMicroRad / 1_000_000f;
        float roll = world.BodyRollMicroRad / 1_000_000f;
        float pitchCos = MathF.Cos(pitch);
        var forward = new Level100RenderVector3(
            -MathF.Sin(yaw) * pitchCos,
            -MathF.Sin(pitch),
            -MathF.Cos(yaw) * pitchCos);
        var right = new Level100RenderVector3(
            MathF.Cos(yaw),
            0f,
            -MathF.Sin(yaw));
        Level100RenderVector3 levelUp = Normalize(Cross(right, forward), WorldUp);
        Level100RenderVector3 bodyUp = Add(
            Scale(levelUp, MathF.Cos(roll)),
            Scale(right, MathF.Sin(roll)));
        var pose = new ClientCameraPose(
            new Level100RenderVector3(
                world.PlayerPosition.X * UnitsToMeters,
                world.PlayerElevationMillimeters * UnitsToMeters,
                -world.PlayerPosition.Z * UnitsToMeters),
            forward,
            bodyUp);
        return new AttachedCameraThingSnapshot(thingId, pose, right);
    }

    private ClientCameraPose EvaluatePanPose(
        AttachedCameraThingSnapshot attached,
        int panElapsedTicks,
        ClientCameraPose retained)
    {
        Level100RenderVector3 center = attached.Pose.Position;
        Level100RenderVector3 forward = attached.Pose.Forward;
        Level100RenderVector3 right = attached.PanRight;
        Level100RenderVector3 point0 = Add(
            Add(center, Scale(forward, 10f)),
            Scale(WorldUp, 4.3f));
        Level100RenderVector3 point1 = Add(
            Add(center, Scale(right, 5f)),
            Scale(WorldUp, -1.3f));
        Level100RenderVector3 point2 = Add(
            Add(center, Scale(forward, -9f)),
            Scale(WorldUp, 1.3f));
        Level100RenderVector3 point3 = Add(center, Scale(forward, -2.5f));
        float fraction = Math.Clamp(
            (float)panElapsedTicks / _panDurationTicks,
            0f,
            0.999999f);
        Level100RenderVector3 position = EvaluateOpeningSpline(
            point0,
            point1,
            point2,
            point3,
            fraction);
        Level100RenderVector3 toThing = Subtract(center, position);
        Level100RenderVector3 look = Normalize(toThing, retained.Forward);
        return new ClientCameraPose(position, look, WorldUp);
    }

    private bool PanIsCurrent(AttachedPanCameraFrame frame) =>
        frame.PanElapsedTicks < _controlViewHandoffTick;

    private static ClientCameraPose Interpolate(
        ClientCameraPose previous,
        ClientCameraPose current,
        float alpha)
    {
        if (alpha == 0f)
        {
            return previous;
        }
        if (alpha == 1f)
        {
            return current;
        }

        return new ClientCameraPose(
            Lerp(previous.Position, current.Position, alpha),
            Normalize(
                Lerp(previous.Forward, current.Forward, alpha),
                current.Forward),
            Normalize(Lerp(previous.Up, current.Up, alpha), current.Up));
    }

    private static Level100RenderVector3 EvaluateOpeningSpline(
        Level100RenderVector3 point0,
        Level100RenderVector3 point1,
        Level100RenderVector3 point2,
        Level100RenderVector3 point3,
        float fraction)
    {
        // Steam CBSpline uses order 3 with knots [0,0,0,1,2,2,2] for these
        // four points: a clamped quadratic B-spline.
        float u = fraction * 2f;
        if (u < 1f)
        {
            float oneMinusU = 1f - u;
            return Add(
                Add(
                    Scale(point0, oneMinusU * oneMinusU),
                    Scale(point1, (2f * u) - (1.5f * u * u))),
                Scale(point2, 0.5f * u * u));
        }

        float twoMinusU = 2f - u;
        float uMinusOne = u - 1f;
        return Add(
            Add(
                Scale(point1, 0.5f * twoMinusU * twoMinusU),
                Scale(
                    point2,
                    (2f * twoMinusU) -
                        (1.5f * twoMinusU * twoMinusU))),
            Scale(point3, uMinusOne * uMinusOne));
    }

    private static Level100RenderVector3 Add(
        Level100RenderVector3 left,
        Level100RenderVector3 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    private static Level100RenderVector3 Subtract(
        Level100RenderVector3 left,
        Level100RenderVector3 right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    private static Level100RenderVector3 Scale(
        Level100RenderVector3 value,
        float scale) =>
        new(value.X * scale, value.Y * scale, value.Z * scale);

    private static Level100RenderVector3 Cross(
        Level100RenderVector3 left,
        Level100RenderVector3 right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

    private static Level100RenderVector3 Normalize(
        Level100RenderVector3 value,
        Level100RenderVector3 fallback)
    {
        float magnitudeSquared =
            (value.X * value.X) +
            (value.Y * value.Y) +
            (value.Z * value.Z);
        return magnitudeSquared > 0f
            ? Scale(value, 1f / MathF.Sqrt(magnitudeSquared))
            : fallback;
    }

    private static Level100RenderVector3 Lerp(
        Level100RenderVector3 previous,
        Level100RenderVector3 current,
        float alpha) =>
        new(
            Lerp(previous.X, current.X, alpha),
            Lerp(previous.Y, current.Y, alpha),
            Lerp(previous.Z, current.Z, alpha));

    private static float Lerp(float previous, float current, float alpha) =>
        previous + ((current - previous) * alpha);

    private static void WriteFrame(
        BinaryWriter writer,
        AttachedPanCameraFrame frame)
    {
        writer.Write(frame.EventFrame);
        writer.Write(frame.PanElapsedTicks);
        writer.Write(frame.ZoomPermille);
        writer.Write(frame.AttachedThing.HasValue);
        if (frame.AttachedThing is not { } attached)
        {
            return;
        }

        writer.Write(attached.ThingId.Value);
        WritePose(writer, attached.Pose);
        WriteVector(writer, attached.PanRight);
    }

    private static void WritePose(BinaryWriter writer, ClientCameraPose pose)
    {
        WriteVector(writer, pose.Position);
        WriteVector(writer, pose.Forward);
        WriteVector(writer, pose.Up);
    }

    private static void WriteVector(
        BinaryWriter writer,
        Level100RenderVector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }

    private static void Validate(AttachedPanCameraFrame frame)
    {
        if (frame.EventFrame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }
        if (frame.PanElapsedTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }
        if (frame.ZoomPermille < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }
    }
}
