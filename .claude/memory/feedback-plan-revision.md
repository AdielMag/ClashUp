---
name: feedback-plan-revision
description: When asked to revise a plan for "optimization and correctness", do a genuine efficiency/leak/scope pass — not just re-explain the same design
metadata:
  node_type: memory
  type: feedback
  originSessionId: 3e63192a-817e-4c0a-a7c1-76bb20ac77eb
---

When the user rejects `ExitPlanMode` and asks to "revise this plan and make sure it's well optimized and correct" (no further specifics), treat it as a request for a genuine critique pass, not a request to just restate the same plan more clearly.

**Why:** During the grass-stealth-zone plan (2026-07-02), the first draft was functionally complete but had three real optimization/correctness gaps: (1) it would always pay per-viewer-encode cost even when nobody was hidden — should fast-path to the old single-broadcast when nothing needs hiding; (2) a hidden player's DTO carried real position data alongside an `IsHidden` flag — leaks true position to a client that reads raw network bytes, defeating the point of hiding it; (3) new interface methods were sketched as abstract, which would've forced changes across all 4 `IServerSimulation` implementations for a feature only one of them needs. Re-reading the draft with "what would make this cheaper, what would make this leak, what's the minimal-diff way to add this" caught all three before implementation — the user approved on the very next pass with no further correction.

**How to apply:** On a plan-revision request like this, re-scan the draft for: (a) hot-path cost that could be gated behind a fast path for the common case, (b) any data sent/stored that shouldn't be (security/leak angle, not just correctness), (c) interface/API changes that could be scoped narrower (e.g. default interface methods, optional params) to reduce the diff. Don't just polish the prose of the same design.
