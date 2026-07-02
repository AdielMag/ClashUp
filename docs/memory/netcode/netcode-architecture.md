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

### Server input buffering (critical for 1:1 with prediction)

The client predicts **one physics step per input sent**, so the server MUST apply
**one input per player per tick**. `InputBuffer` is a per-player queue, consumed
**immediately** (no playout delay) by `MatchTickLoop.Drain()` — one per player per
tick. Two rules, both load-bearing:

1. **Hold on underflow** — if nothing is queued, apply NOTHING (player holds at zero
   velocity). Never repeat the last input: repeating stale movement pushes the server
   past what the client predicted → sinks into a wall a tick after the finger lifts.
   A held (unconsumed) input stays pending on the client and is replayed by
   reconciliation, so holding is correct and causes no drag.
2. **No playout slack, tight cap** — `MaxQueueDepth = 2`, drop-oldest. Any buffered
   "move" backlog is felt directly as post-release overshoot ("after a short delay it
   keeps moving into the wall"). Consuming immediately + drop-oldest means a fresh
   "stop" pushes stale "move" out fast. Both ends run 30 Hz so steady depth is ~0–1.

**History / do-not-repeat:** the original bug was `Drain()` draining ALL queued inputs
into one `Step` (burst-collapse → rubber-band). A playout buffer (TargetDepth 2) was
tried to absorb frame-vs-clock drift but its latency caused the post-release overshoot
above — reverted. A repeat-last-on-underflow fallback was tried but caused wall-sink —
reverted. The current immediate-consume + hold-on-underflow keeps the server's applied
sequence 1:1 with prediction at minimum latency.

### Reconciliation dead-zone (client) — kills collision micro-jitter

Even with a perfect 1:1 input stream, the client's snap-and-replay re-runs Box2D
collision from the server's position every snapshot, landing a few mm off the client's
continuous prediction → re-injected each snapshot → shimmer against walls (on contact
AND on release). Fix in `ClientPredictionWorld.ApplyServerSnapshot`: after snap+replay,
compare the result to the pre-reconcile prediction; if within `ReconcileDeadzoneSq`
(0.06 m, above Box2D contact slop, below any real desync) the two agree, so call
`IClientSimulation.SnapLocalPosition(prePhys)` to restore the smooth prediction and add
NO correction. Only beyond the dead-zone do we keep the authoritative result + smoothed
`CorrectionX/Z`. `SnapLocalPosition` added to `IClientSimulation` (+ Aether/Null/Movement
impls) for exactly this revert.

**Bug this fixed (2026-06):** the old `Drain()` drained ALL queued inputs (a single
global queue) and overwrote `_pendingVel`, then stepped once → bursts collapsed to
one step, empty ticks zeroed velocity. Symptoms: avatar drifts forward after joystick
release + constant rubber-band jitter. Any future change to drain/step cadence must
preserve the 1:1 contract.

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
