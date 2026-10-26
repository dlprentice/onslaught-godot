# Live audio adapter

Status: experimental; initial code and engine-free checks, real Godot playback unverified
Last updated: 2026-10-26
Summary: opt-in main-thread effects and prepared PCM voices, using Parity's single sound-event owner.
Evidence: SOURCE — native MessageBox/PC sound-manager contracts and the buffer adapter; UNKNOWN — real Godot playback and output fidelity until runtime validation.

`HOST_LIVE=1 HOST_AUDIO=1 ONSLAUGHT_VOICE_PCM=/path/to/voices.json` selects this adapter. It requires a real
Godot audio driver. A Dummy driver refuses admission. `HOST_TAPE` keeps the existing virtual reference device and
cannot be combined with `HOST_AUDIO=1`. The threaded `HOST_LIVE=flow` route does not use this adapter.
`HOST_AUDIO_OUTPUT_DEVICE` optionally selects an exact Godot output-device name before any source is created;
an unavailable name refuses admission. This changes only this application's output, not the desktop default.

The Host supplies an explicit engineering policy: 256 initially empty event seeds (channel -1, position zero),
twelve channels and continuous frequency adjustment. Those seeds are not measured retail heap contents. Startup
options supply sound rate, speaker flip and master volume; without an options file, native CLI defaults apply.
Hardware sound is refused because its pitch-variance configuration and extra RNG draw are not implemented here.
DirectSound device indices and HRTF methods are unsupported. Device/rate/speaker changes during the preview stop
it with a reinitialization requirement. The existing volume slider updates the shared manager's gain.

Effects use `SampleBankPcmCatalog` and the installed English XAP. Voices need actual decoded data: the old asset
materializer's TutorialAudio files are copied Ogg streams, not PCM. Prepare selected voices with
`rebuild/tools/materialize_voice_pcm.py`, supplying a local native decoder executable and its SHA-256, the decoder
library SHA-256 and reader source SHA-256. Run `--help` for the arguments. The supplied decoder must implement
exactly one native `COggFileRead::Read(1048576)` under worker control word `027f`, with retail x87 FIST quantization,
and report the actual read size. This repository does not download, embed or certify an arbitrary decoder.

The generated manifest binds the actual source Ogg and PCM bytes, decoder/source/library identities and capped
read contract. Put it and its PCM files in ignored local data. Prepare every voice the selected route may request;
an absent entry stops with an explicit error. The library is preloaded before starting the level, so its ready-data
loader has no simulated load-duration timer. It supports mono 44100 Hz PCM16 input. The existing native sample
converter applies the selected output rate; unknown native allocation tails are refused. Effects and voices share
the same `SoundProcess`, buffer admission, monitored completion and sample-kill paths. No second queue, owner
callback timer or random stream exists in the audio host.

Each duplicate owns a Godot PCM stream/player and immutable channel/generation token. Status is observed from
the player; a Play request does not prove playback. Pause stores the observed frame-aligned position before Stop.
Host focus suspension pauses sources without advancing the core. Normal end, unsupported behavior, errors and
exit dispose all owned sources. Weak playback observers follow asynchronous AudioServer retirement without
extending its lifetime. The adapter exposes no fabricated DirectSound hardware write cursor: channel cursor
inspection is explicitly unsupported.

The selected CE distance curve is applied once; Godot supplies spatial panning and resampling. This is real-audio
presentation, not waveform-equivalence evidence. Music, films, the stepped-flow audio bridge, asynchronous loader
timing parity, exact hardware mixing and heard output remain outside these code checks. Runtime validation must
inspect fresh output and source retirement; passing authored player controls does not establish either.
