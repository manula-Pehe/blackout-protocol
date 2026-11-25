# Main Editor Profiler capture — 2026-11-25

Source: `main-profiler-20261125.data`, 240 captured Editor frames, 239 analyzed. Unity 6000.3.25f1, Main in Play Mode, Intel Core i5-8365U (4 cores / 8 threads), 14 GiB RAM. The capture overlapped the first warmup/recording phase of the 4-versus-12 benchmark. Deep Profile was off. These are Editor CPU measurements and are not representative Player-build GPU timings.

| Measure | Captured value |
| --- | ---: |
| Main CPU frame time, mean / median / p95 / max | 21.109 / 17.210 / 38.029 / 379.366 ms |
| Frames over 16.667 ms | 152 of 239 (63.6%) |
| Whole-frame GC allocation, median / mean | 14,671 / 48,898 B per frame |
| `Blackout.AgentBody.Movement`, 3 humanoids | 717 calls; 0.173 ms total per analyzed frame; 0.154 ms median |
| Movement marker GC allocation | 40 B total, one observed frame; other sampled frames had 0 B in this marker |
| `Blackout.Hunter.RouteRequest` | 6 calls; 0.748 ms mean per call; 4,300 B mean per call |
| `AgentActionAnimation.LateUpdate` | 717 calls; 0.078 ms total per analyzed frame; 0 B GC in marker |
| `AgentAnimatorDriver.LateUpdate` | 717 calls; 0.018 ms total per analyzed frame; 0 B GC in marker |
| Native `Animators.Update` marker | 0.240 ms per analyzed frame; 0 B GC in marker |

The Profiler report does not isolate all rendering costs, and its `gpuFrameMs` was 0 because this was an Editor CPU capture. Route-request allocations are outside the steady movement marker. Six route markers during roughly 5.36 seconds of recording correspond to about 1.12 requests/s in this window, below the configured 4 Hz hard maximum; this is an observation, not a stress proof of the throttle. The four-body benchmark phase is confounded by the simultaneous recording and startup spikes; use the clean repeat CSV for the workload comparison.
