# Server Authority

**When this applies:** Any code that touches gameplay state — inputs, simulation, hit detection, scoring, match outcomes.

## The rule

The server's AetherNet simulation is the only source of truth for gameplay state. The client predicts locally for responsiveness, then reconciles against authoritative snapshots from the server.

Never let client-only state decide a gameplay outcome.

## What the client may do

- Run a local `ClientPredictionWorld` (AetherNet client instance) and step it forward using locally-gathered inputs.
- Display predicted state immediately for responsiveness.
- On every `OnSnapshot` from the server: rewind local state to `snap.Tick`, apply the snapshot, re-run queued local inputs forward.
- Send `InputCommand` describing **intent** (button mask, move axes, aim direction): never a result.

## What the client must NOT do

- Decide whether a hit landed, whether a player died, whether a pickup was collected. Submit intent and let the server's snapshot resolve it.
- Reorder, drop, or "smooth out" snapshot data before it reaches the reconciler. The reconciler is the only thing allowed to interpret authoritative state.
- Ship gameplay logic that runs only in the editor / standalone build but not the server simulation. If the sim needs it, it goes in `AetherNet` or in code the server runs.

## What the server must do

- Run a fixed-tick loop per match. Tick rate from config (30–60 Hz today).
- Drain inputs, advance the simulation, encode a delta snapshot vs each client's last-acked baseline, broadcast.
- Validate every input against rate / sanity bounds before it enters the buffer. Garbage inputs are dropped, not crashed on.
- Never trust client clock values for gameplay decisions. Client timestamps are diagnostic only.

## Cheating posture

Anything that, if forged by a malicious client, would let them win or grief is a server-side decision. Phase 1 is not focused on anti-cheat hardening but the architecture must not paint us into a corner where adding it later means rewriting gameplay code.
