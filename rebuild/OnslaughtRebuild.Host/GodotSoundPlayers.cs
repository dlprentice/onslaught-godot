// SPDX-License-Identifier: GPL-3.0-only
using Godot;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Host;

/// <summary>Real Godot sources in the native camera-local listener frame. Only the main-thread live host uses it.</summary>
public sealed class GodotSoundPlayers : IDisposable
{
    private readonly SubViewport _root;
    private readonly AudioListener3D _listener;
    private readonly HashSet<Player> _players = [];
    private readonly List<WeakRef> _retiring = [];
    private bool _disposed;
    public GodotSoundPlayers(Node parent, string? outputDevice = null)
    {
        if (!parent.IsInsideTree()) throw new InvalidOperationException("Live sources need an active main-thread scene owner.");
        if (AudioServer.GetDriverName() == "Dummy")
            throw new NotSupportedException("Real live audio requires a real audio driver; Dummy is not playback admission.");
        if (outputDevice is not null)
        {
            if (!AudioServer.GetOutputDeviceList().Contains(outputDevice, StringComparer.Ordinal))
                throw new NotSupportedException("The explicitly selected live audio output device is unavailable.");
            AudioServer.OutputDevice = outputDevice;
            if (AudioServer.OutputDevice != outputDevice)
                throw new InvalidOperationException("The audio driver did not retain the selected output device.");
        }
        // Godot's 3D mixer enumerates cameras, then uses each camera's audio-enabled viewport/listener.
        // Keep camera-local sources in their own audio world: rendered view cameras must not transform them again.
        _root = new()
        {
            Name = "LiveAudioCameraLocal", ProcessMode = Node.ProcessModeEnum.Always,
            OwnWorld3D = true, AudioListenerEnable3D = true, Size = new Vector2I(2, 2),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        _listener = new();
        var camera = new Camera3D { Current = true };
        try { parent.AddChild(_root); _root.AddChild(camera); _root.AddChild(_listener); _listener.MakeCurrent(); }
        catch
        {
            if (camera.GetParent() is null) camera.Free();
            if (!_listener.IsInsideTree()) _listener.Free();
            _root.Free(); throw;
        }
    }
    public ILiveAudioPlayer Create(SamplePcmData pcm)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!pcm.FullyInitialized) throw new InvalidDataException("Uninitialized PCM cannot become a Godot stream.");
        var stream = new AudioStreamWav
        {
            Format = pcm.Format.Bits == 16 ? AudioStreamWav.FormatEnum.Format16Bits : AudioStreamWav.FormatEnum.Format8Bits,
            MixRate = checked((int)pcm.Format.SampleRate), Stereo = false, Data = LiveAudioEncoding.ForGodot(pcm),
            LoopBegin = 0, LoopEnd = checked((int)pcm.Format.Frames), LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
        };
        var node = new AudioStreamPlayer3D
        {
            Stream = stream, MaxPolyphony = 1, ProcessMode = Node.ProcessModeEnum.Always,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, MaxDistance = 0,
            DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled,
            AttenuationFilterDb = 0, EmissionAngleEnabled = false,
        };
        try
        {
            _root.AddChild(node); var player = new Player(this, node, stream); _players.Add(player); return player;
        }
        catch { node.Free(); stream.Dispose(); throw; }
    }
    // Adapted from the earlier Godot client's AudioPlaybackRetirement: weak observers do not extend playback life.
    private void Observe(AudioStreamPlayer3D player)
    {
        Sweep();
        if (!player.HasStreamPlayback()) return;
        using AudioStreamPlayback playback = player.GetStreamPlayback();
        _retiring.Add(GodotObject.WeakRef(playback) ?? throw new InvalidOperationException("Invalid live audio playback handle."));
    }
    public int PendingRetirements { get { Sweep(); return _retiring.Count; } }
    private void Sweep()
    {
        for (int i = _retiring.Count - 1; i >= 0; --i)
        {
            using Variant value = _retiring[i].GetRef();
            if (value.VariantType != Variant.Type.Nil) continue;
            _retiring[i].Dispose(); _retiring.RemoveAt(i);
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        Exception? failure = null;
        foreach (var player in _players.ToArray())
            try { player.Dispose(); } catch (Exception e) { failure ??= e; }
        try { _listener.ClearCurrent(); }
        finally
        {
            _root.Free();
            foreach (var weak in _retiring) weak.Dispose();
            _retiring.Clear(); _disposed = true;
        }
        if (failure is not null) throw new InvalidOperationException("Godot audio resource teardown failed.", failure);
    }
    private sealed class Player(GodotSoundPlayers owner, AudioStreamPlayer3D node, AudioStreamWav stream) : ILiveAudioPlayer
    {
        private bool _disposed;
        public bool Playing => node.Playing;
        public float PlaybackPosition => node.GetPlaybackPosition();
        public bool Paused { get => node.StreamPaused; set => node.StreamPaused = value; }
        public void Play(float fromSeconds) { node.Play(fromSeconds); owner.Observe(node); }
        public void Stop() => node.Stop();
        public void SetLooping(bool looping) => stream.LoopMode = looping ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
        public void SetPitch(float pitch) => node.PitchScale = pitch;
        public void SetVolume(float decibels, bool muted) { if (muted) node.VolumeLinear = 0; else node.VolumeDb = decibels; }
        public void SetPosition(FVector position) => node.Position = new(position.X, -position.Z, -position.Y);
        public void Dispose()
        {
            if (_disposed) return;
            try { owner.Observe(node); }
            finally
            {
                try { node.Stop(); node.Stream = null; }
                finally
                {
                    node.Free(); stream.Dispose();
                    owner._players.Remove(this); _disposed = true;
                }
            }
        }
    }
}
