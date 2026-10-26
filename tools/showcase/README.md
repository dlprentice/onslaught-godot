# Staged development footage

Status: experimental staging tool
Last updated: 2026-10-26
Summary: generate a code-built Godot shot using locally supplied Battle Engine Aquila models and sounds, without creating network players or changing gameplay.

The five colored machines are presentation copies. Their colors, formation, camera and firing schedule are authored for this shot. Where the exterior mesh has no named gun emitters, muzzle positions are authored approximations; the flashes and tracers do not execute native weapon code. The caption identifies Godot staging; this is neither native-port gameplay nor multiplayer acceptance evidence. No game assets are included or downloaded.

Generate into an empty ignored directory, build its `Showcase.csproj`, then run `record.py` with explicit `--project`, `--game`, `--output`, `--gpu-lock` and `--heavy-lock` paths. Use the machine's normal nice/resource bounds. Rendering requires `godot-offscreen`, never headless Movie Maker. Use `--start-frame 150 --frames 90` for a short firing-section preview; the final take starts at zero. The recorder keeps GPU then heavy locks while the wrapper runs. Its fixed SubViewport writes 1280×720 RGBA frames to a two-thread FFmpeg encoder; the default shot is 600 frames at 30 fps.

`mix_audio.py RUN --frames 600` uses the captured shot ledger and the locally decoded, hash-checked Vulcan sample. It writes a stereo WAV for a final H.264/yuv420p + AAC mux. It does not record a microphone, desktop or playback device. Inspect the actual motion, frames and audio before sharing; a successful build is not visual or audible acceptance.
