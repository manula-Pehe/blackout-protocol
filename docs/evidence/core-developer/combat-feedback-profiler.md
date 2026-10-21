# Player Combat Feedback — Profiler Evidence

## Captured result

The user-provided Unity Profiler screenshot is saved as [`CombatFeedback_Profiler.png`](CombatFeedback_Profiler.png).

- Profiler target: Play Mode.
- Selected frame: 210 of 1,191 recorded frames.
- `GC Allocated In Frame` at the selected frame: **254.5 KB**.
- CPU frame time shown: **16.34 ms**; GPU time is unavailable in the capture.
- The graph contains multiple GC allocation spikes during the recording.

This is an Editor Play Mode capture. The selected-frame value is a per-frame allocation, not a per-shot measurement. The screenshot does not show a call stack, the scene name, or which allocation caused the value, so it does not prove that the 254.5 KB came from `PlayerCombatFeedback` or from one shot. The screenshot itself warns that Editor memory usage differs from a Player build.

## Capture procedure

1. Open `Assets/_Project/Scenes/Sandbox/Ashen/PlayerShootingTest.unity` and enter Play Mode.
2. Open **Window > Analysis > Profiler** and select **CPU Usage**.
3. Clear the Profiler recording, then repeatedly fire at the test target for several seconds. Include impacts and a reload if practical.
4. Select representative firing frames and inspect the **GC Alloc** column and allocation call stack for the shot/impact feedback path.
5. Capture a screenshot showing the Profiler module, selected frame(s), and GC Alloc details. Save it as `docs/evidence/core-developer/CombatFeedback_Profiler.png`.
6. Record the Unity version, scene, capture duration, observed per-frame allocations, and caveats in **Captured result** above. Report measured values as seen; do not infer zero allocation from particle pooling alone.
