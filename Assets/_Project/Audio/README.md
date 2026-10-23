# Phase 1 audio foundation

The three mono, 48 kHz, 16-bit PCM WAVs in `Placeholders/` are original local
mathematical synthesis, with no downloaded or recorded samples. Regenerate with
`python3 scripts/generate-placeholder-audio.py`. The fixed seed makes outputs
reproducible. These are placeholders pending listening review.

- `IndustrialAmbience.wav`: eight-second periodic industrial hum/air texture.
- `SecurityAlarm.wav`: two-second periodic pulsed alarm.
- `Confirmation.wav`: 0.32-second confirmation tone with a smooth envelope.

The loops are synthesized periodically rather than faded into a gap. Import
settings preserve uncompressed PCM with normalization off for predictable levels
and loop boundaries. Playback looping is configured on the AudioSources.

`BlackoutProtocol.Audio.AudioPresenter` creates three owned child AudioSources
at runtime. All are 2D, have Play On Awake off, and use separate volume controls
(ambience 0.08, alarm 0.18, one shots 0.35). Clip defaults are supplied through the
script's `.meta` default references when adding the component; verify them in the
Inspector. Nothing starts automatically, subscribes to events, or alters AI noise.
Repeated loop starts do not restart a playing loop. Disable stops every source;
re-enable remains silent until explicitly started. Missing clips are safe no-ops.

## Manual verification (not yet performed)

1. In a disposable empty scene, add one camera with one enabled AudioListener and
   an empty GameObject with AudioPresenter. Do not modify Main or run setup tools.
2. Verify the three clip assignments and import settings: PCM, Decompress On Load,
   normalization off, sample rate preserved. Enter Play Mode; require silence and
   exactly three child AudioSources with Play On Awake off.
3. Use the component context menu's Audio commands. Start ambience and listen for
   at least three loops; require no click or gap at eight-second boundaries.
   Start the alarm and listen for at least three two-second loops. Listen at a
   comfortable device level and adjust the component volumes if needed.
4. Start each loop repeatedly while playing: require uninterrupted playback with
   no additional children or stacked loop voices. Confirm both loops can coexist.
5. Play confirmation while both loops run: it must not interrupt either loop.
   Stop Alarm must leave ambience/confirmation alone; Stop All must silence all.
6. Disable/re-enable the component and its GameObject: require silence until an
   explicit start. Clear clip fields and invoke playback: require no exceptions.
   Exit/re-enter Play Mode: require no persistent sources or automatic playback.
7. Discard the temporary scene. Main, gameplay, HUD, AI, NavMesh data and existing
   unstaged files must remain unchanged. Gameplay event integration is deferred.
