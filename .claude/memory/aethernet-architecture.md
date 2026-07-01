---
name: aethernet-architecture
description: "AetherNet physics vendoring, simulation seam, coordinate mapping, prediction/interpolation, and the DLL wiring gotchas"
metadata: 
  node_type: memory
  type: project
  originSessionId: 345c10fe-3909-4f06-ac80-7e8543804aea
---

## Physics / AetherNet Architecture
- **Library**: AetherNet (`external/AetherNet/` gitignored clone) — GC-free deterministic 2D physics over Aether.Physics2D (Box2D port)
- **Simulation seam**: `IClientSimulation` (client) / `IServerSimulation` (server) — AetherNet implementations are `AetherClientSimulation` / `AetherServerSimulation`
- **Shared world**: `MatchPhysicsWorld` in `ClashUp.Shared/Simulation/` — same code runs on client (prediction) and server (authority)
- **Coordinate mapping**: game (X, Z) ↔ Aether (x, y); gravity = zero for top-down
- **Player bodies**: dynamic circles, velocity set from input each tick (kinematic move-and-slide style)
- **Player radius**: `MatchPhysicsWorld` constructor parameter (default `0.5f`). Client reads from prefab's `AetherCircleCollider.Radius` (Player.prefab `_radius: 0.5`). Server uses default. **These MUST match** — a mismatch makes client/server resolve wall collisions at different distances → constant position disagreement → reconciliation shimmer against walls (was 0.4 server vs 0.5 prefab).
- **Wire protocol**: `InputCommand` up, `SnapshotPacket → WorldStatePacket → PlayerStateDto{X,Z,Yaw,Health,LastProcessedInputSeq,IsInvulnerable,RespawnInTicks}` down
- **AetherNet.Shared**: `AetherNet.Shared.dll` (netstandard2.0, C# 10) committed in `Assets/Packages/AetherNet.Shared.0.1.0/`. Uses pre-built DLL — Unity can't compile C# 10 file-scoped namespaces.
- **AetherNet.Unity**: Source-only package copied to `Assets/Packages/AetherNet.Unity/` by `setup-aethernet.ps1`. These files ARE C# 9 compatible (block-scoped namespaces). Has Runtime + Editor asmdefs. `AetherSceneBaker.cs` excluded (depends on `AetherNet.Server`).
- **AetherNet.Unity asmdefs**: `AetherNet.Unity` (Runtime, unsafe, precompiled refs: AetherNet.Shared.dll + Aether.Physics2D.dll) and `AetherNet.Unity.Editor` (Editor-only, refs AetherNet.Unity)
- `Aether.Physics2D.dll` installed via NuGetForUnity. Both DLLs listed in `ClashUp.Shared.Unity.asmdef` precompiledReferences.
- **Server DLL wiring**: conditional MSBuild in `AetherNet.refs.props` (repo root) — `ProjectReference` when clone exists, `PackageReference` fallback
- **AetherNetSettings**: ScriptableObject at `Assets/Resources/AetherNetSettings.asset` — configures `SimulationPlane` (XZ) and `PixelsPerMeter` (1). Auto-applies in both editor (`[InitializeOnLoadMethod]`) and runtime (`[RuntimeInitializeOnLoadMethod]`).
- **Determinism watch**: Aether.Physics2D is float-based; monitor for rubber-banding jitter between x86 server and ARM client

## Client Prediction & Interpolation (Gambetta)
- **Local player**: client-side prediction + server reconciliation via `LastProcessedInputSeq` (sequence-based ack, NOT tick-based). Render with sub-tick alpha-lerp (prev/current) for smooth motion between 30 Hz fixed steps.
- **Remote players**: NOT in client physics world. Pure entity interpolation from buffered authoritative snapshots, rendered ~66ms in the past (2 × tick interval). `RemotePlayerInterpolator` ring buffer per player.
- **Lag compensation**: documented but not yet implemented (no combat). See [netcode-architecture.md](netcode-architecture.md).
- **Fixes to AetherNet**: must be generic/non-specific (upstreamable). Key fixes: `Directory.Packages.props` CPM opt-out, `SimulationPlane` enum, configurable `PixelsPerMeter`, `#nullable enable` on Unity files, `using` aliases for type ambiguities (RaycastHit, Vector2)
- **Fix vendored packages at the source** — never create project-side workarounds for issues in vendored packages (AetherNet, etc.). Fix the package itself so it works correctly.
