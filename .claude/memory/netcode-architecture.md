---
name: netcode-architecture
description: "Gambetta netcode: client-side prediction, server reconciliation via LastProcessedInputSeq, entity interpolation for remotes, lag compensation (future)"
metadata: 
  node_type: memory
  type: project
  originSessionId: cc3da936-60a3-4c80-af6e-9cc8f6eb2c16
---

# Netcode Architecture (Gambetta Model)

Based on [Gabriel Gambetta's series](https://www.gabrielgambetta.com/client-server-game-architecture.html). Four techniques; three implemented, one documented for future.

## 1. Server-Authoritative + Dumb Client

Server is the single source of truth. Clients send inputs (`InputCommand`), server simulates and broadcasts state (`SnapshotPacket`). Client only renders. See [[feedback-client-authority]].

## 2. Client-Side Prediction (Local Player)

The local player sees their movement instantly — no waiting for server round-trip.

- `LocalInputPublisher` fires at tick rate (30 Hz), creates `InputCommand` with `SequenceId`
- `ClientPredictionWorld.Predict()` applies input to `MatchPhysicsWorld` and queues it
- The local capsule renders from the predicted state, interpolated between prev/current tick by a sub-tick alpha (see §5)

## 3. Server Reconciliation

When a snapshot arrives, the client resets to the server's authoritative position and replays any inputs the server hasn't processed yet.

- **Key field**: `PlayerStateDto.LastProcessedInputSeq` (Key 5) — the server echoes the highest `SequenceId` it applied for each player
- `AetherServerSimulation` tracks `_lastSeq[playerId]` in `ApplyInput()` and emits it in `EncodeDelta()`
- `ClientPredictionWorld.ApplyServerSnapshot()`:
  1. Feeds remote DTOs to `RemotePlayerInterpolator` (see §4)
  2. Calls `AetherClientSimulation.ReconcileTo()` — snaps local player to server position, returns `ackedSeq`
  3. Drops pending inputs where `SequenceId <= ackedSeq`
  4. Replays remaining inputs on top of the authoritative state

**Why ack by sequence, not tick:** Client and server ticks drift with latency. Tick-based acking drops the wrong inputs → rubber-banding. Sequence ids are a monotonic client-local counter that the server echoes verbatim.

## 4. Entity Interpolation (Remote Players)

Remote players are NOT simulated on the client. They are rendered purely from buffered authoritative snapshots, played back ~66ms in the past.

- `RemotePlayerInterpolator` (ring buffer per player, capacity 32)
- Each snapshot: append `(serverStampMs, x, z, yaw, health)` samples for remote players
- Each frame: advance `renderClockMs` by `Time.deltaTime * 1000`; keep it `InterpolationDelayMs` behind the newest sample
- Lerp between the two samples bracketing `renderClockMs` → smooth motion at any framerate
- `InterpolationDelayMs = 2 × (1000 / tickRateHz)` ≈ 66ms at 30 Hz — survives one dropped packet

**Trade-off:** "See yourself in the present, others in the past." 66ms of display latency for remote players, but perfectly smooth motion.

## 5. Local Player Render Interpolation

Prediction steps at 30 Hz but the renderer runs at 60-144+ fps. To avoid visible stepping:

- `PlayerRenderState` stores both `Prev{X,Z,Yaw}` and current `{X,Z,Yaw}` — shifted each `SyncRenderStates()` call
- `LocalInputPublisher` computes `alpha = accumulator / tickInterval` and writes it to `ClientPredictionWorld.RenderAlpha`
- `PlayerViewSystem.Tick()` lerps: `pos = Lerp(prev, current, alpha)` → smooth + snappy

## 6. Lag Compensation (Future)

When combat is added: the server will need to rewind the world to the time the shooter fired (using `SnapshotPacket.ServerStampMs`) to validate hits against where remote players *appeared* on the shooter's screen. Not implemented — [[stat-health-system]] documents the health/damage API that will use this.

## Data Flow Summary

```
LOCAL PLAYER (present):
  Input → Predict (physics step) → render via alpha-lerp
  Snapshot arrives → snap to server state → replay pending inputs

REMOTE PLAYERS (66ms past):
  Snapshot arrives → buffer sample(serverStampMs, pos, yaw, health)
  Each frame → advance renderClock → lerp between bracketing samples
```

## Key Files

| Component | File |
|-----------|------|
| Wire protocol (LastProcessedInputSeq) | `src/Shared/ClashUp.Shared/MessagePackObjects/WorldStatePacket.cs` |
| Server seq tracking | `src/Server/ClashUp.GameServer/Simulation/AetherServerSimulation.cs` |
| Client prediction + reconcile | `Core/Gameplay/Scripts/Services/ClientPredictionWorld.cs` |
| Client sim (local-only physics) | `Core/Gameplay/Scripts/Services/AetherClientSimulation.cs` |
| Remote interpolation | `Core/Gameplay/Scripts/Services/RemotePlayerInterpolator.cs` |
| Render view | `Core/Gameplay/Scripts/Services/PlayerViewSystem.cs` |
| Alpha source | `Core/Match/Scripts/Services/LocalInputPublisher.cs` |
