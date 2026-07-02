---
name: mvp1-architecture
description: "MVP 1 server architecture decisions — YARP gateway, session cache, write-behind persistence, version routing"
metadata: 
  node_type: memory
  type: project
  originSessionId: 53d337e4-9754-428d-a73c-ce1d6a8bab4d
---

## Planned Server Architecture (MVP 1, decided 2026-06-09)

Detailed specs are on Monday.com tickets. Key decisions:

### In-Memory Player Session Cache
- Load player profile from MongoDB once on connect, serve from `ConcurrentDictionary` during session
- DI-registered via `IPlayerSessionCache` interface (not singleton pattern — see [[feedback-no-singletons]])
- Evict on disconnect after confirmed flush

### Write-Behind Persistence (Critical — zero data loss)
- **3-layer flush**: event-driven (match end, purchases) + periodic sweep (IHostedService, 10-30s) + graceful shutdown flush
- **Write-ahead intent log**: log mutation intent to MongoDB `write_intents` collection before applying in-memory change — crash recovery replays incomplete intents
- **Never drop**: failed writes stay dirty, retried on next sweep with exponential backoff
- User stressed this must be **robust and never fail** — data safety is non-negotiable

**Why:** User explicitly stated "this has to be safe so we would never lose data and make sure it is robust and never fail!! super important"

**How to apply:** When implementing any persistence or state mutation on the server, always consider the failure path. Never silently drop writes. Always have a recovery mechanism.

### Version-Aware Gateway (YARP)
- YARP reverse proxy as single entry point (:5001)
- Client sends `x-client-version` in gRPC metadata
- Routes to correct backend process (each version on unique port)
- Process Supervisor (IHostedService) spawns/monitors/restarts version processes from a JSON manifest
- Version mismatch → `FAILED_PRECONDITION` gRPC error → client shows "please upgrade" prompt
- Must work on both **Windows and Linux**
- Performance: <1ms proxy overhead, O(1) header routing, localhost loopback only

### CI/CD
- GitHub Actions → Docker build → push to ghcr.io
- Image tag === client app version (same semver)
- Missing server version in registry → client prompted to upgrade

### Match Loop
- Timed Team Deathmatch: 2 teams, 90s, kills = points, 3s respawn, sudden death tiebreaker
