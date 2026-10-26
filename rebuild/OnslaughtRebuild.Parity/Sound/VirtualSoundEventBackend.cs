// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// CPCSoundManager Play00517790, Resume00517960, Pause00517990, Stop005179b0, Update00517ae0.
// The concrete device contract is onslaught-ce port/dsound/dsound.cpp, not arbitrary retail sound hardware.
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>
/// Explicit single-player virtual-port profile. The port's 12 sound channels plus separately owned music/voice
/// streams must remain below its 256-voice device limit. This adapter admits successful secondary-buffer allocation;
/// it does not invent device loss, allocation failures or another owner's Doppler behavior.
/// </summary>
public readonly record struct VirtualSoundBackendProfile(int SoundRate, int Channels, bool ContinuousRate, bool FlipSpeakers);

public readonly record struct SoundCameraState(FVector Position, FVector OldPosition, FMatrix Orientation);
public readonly record struct SoundOwnerSpatialState(FVector Position, FVector Velocity);

/// <summary>A diagnostic observation polls the virtual device, just like DirectSound GetStatus/GetCurrentPosition.</summary>
public readonly record struct VirtualSoundChannelState(uint Status, uint PlayCursor, uint WriteCursor, uint Frequency,
    int Volume, FVector Position, FVector PendingPosition);

/// <summary>The unchanged virtual-port device selection for the shared PC sound-event adapter.</summary>
public sealed partial class VirtualSoundEventBackend : PcSoundEventBackend
{
    public VirtualSoundEventBackend(SoundEffects sound, VirtualSoundBackendProfile profile, Func<long> blocksMixed,
        Func<SampleMetadata, bool> sampleHasBuffer, Func<SoundCameraState?> camera,
        Func<object, SoundOwnerSpatialState?> ownerSpatial)
        : base(sound, profile, Device(sound, blocksMixed), sampleHasBuffer, camera, ownerSpatial) { }

    private static ISoundBufferDevice Device(SoundEffects sound, Func<long> blocksMixed)
    {
        ArgumentNullException.ThrowIfNull(sound); ArgumentNullException.ThrowIfNull(blocksMixed);
        return new VirtualSoundBufferDevice(blocksMixed);
    }
}
