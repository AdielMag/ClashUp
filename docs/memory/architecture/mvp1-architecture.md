---
name: mvp1-architecture
description: "Server persistence plan (not yet built) — in-memory session cache + write-behind with zero-data-loss mandate"
metadata:
  node_type: memory
  type: project
  originSessionId: 53d337e4-9754-428d-a73c-ce1d6a8bab4d
---

## Player persistence plan (decided 2026-06-09, NOT yet implemented as of 2026-07-02)

`grep` confirms `IPlayerSessionCache` / `write_intents` / write-behind do not exist in `src/` yet — this is still the intended design when player persistence is built. The version-aware gateway from the original MVP1 plan **has** shipped; see [[deployment-architecture]] for the as-built gateway.

### In-Memory Player Session Cache
- Load player profile from MongoDB once on connect, serve from `ConcurrentDictionary` during session
- DI-registered via `IPlayerSessionCache` interface (not singleton — see [[feedback-no-singletons]])
- Evict on disconnect after confirmed flush

### Write-Behind Persistence (Critical — zero data loss)
- **3-layer flush**: event-driven (match end, purchases) + periodic sweep (IHostedService, 10-30s) + graceful shutdown flush
- **Write-ahead intent log**: log mutation intent to MongoDB `write_intents` collection before applying the in-memory change — crash recovery replays incomplete intents
- **Never drop**: failed writes stay dirty, retried on next sweep with exponential backoff

**Why:** User stated "this has to be safe so we would never lose data and make sure it is robust and never fail!! super important" — data safety is non-negotiable.

**How to apply:** When implementing any server persistence or state mutation, always design the failure path. Never silently drop writes; always have a recovery mechanism.
