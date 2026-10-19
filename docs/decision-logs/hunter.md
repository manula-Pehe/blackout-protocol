# Decision Log — Agent Controller / Hunter

## 2026-10-19 — Use normalized explicit curves with hysteresis

**Context:**  
The Hunter must choose between Wander, Investigate, Chase, Search and Attack from noisy perception and memory inputs. The first branch needs deterministic, testable decision logic without runtime or scene dependencies.

**Options considered:**
1. Unity `AnimationCurve` assets — visually editable, but couples the foundation to UnityEngine and serialized assets.
2. A rule-based state machine — simple, but does not demonstrate Utility AI and creates hard thresholds that switch rapidly.
3. Explicit mathematical response curves — engine-independent, reviewable in code, deterministic and straightforward to unit test.

**Decision:**  
Use normalized scalar inputs, SmoothStep response curves, weighted multiplicative scores, and a configurable bonus for the current action. Resolve exact ties with the fixed priority Attack, Chase, Investigate, Search, then Wander.

**Why:**  
SmoothStep suppresses insignificant input noise while remaining responsive through the useful middle range. Multiplication makes trust and attack eligibility meaningful gates. The current-action bonus provides hysteresis without hiding a clearly superior action. Returning both base and adjusted scores makes future runtime debug displays explainable.

**Evidence:**  
EditMode tests cover all five selected actions, low-trust noise, hysteresis, deterministic ties and invalid numeric inputs.

---
