# ClashUp - Game Design Document

## 1. Overview

**Title:** ClashUp
**Genre:** Real-time multiplayer arena brawler
**Perspective:** Top-down isometric (camera at ~46 degrees, FOV 35)
**Platform:** Mobile (Android) + Desktop (Unity Editor)
**Engine:** Unity 6 LTS
**Networking:** Server-authoritative, gRPC (MagicOnion), 30 Hz tick rate

ClashUp is a fast-paced team-based arena brawler where players pick a character, queue into a match, and fight in short rounds. Each character has a unique stat line and two abilities — one auto-attack and one manually aimed skill. Matches are server-authoritative with client-side prediction for responsive controls.

---

## 2. Core Loop

```
Lobby → Queue → Team Assignment → Spawn → Fight → Match End → Lobby
```

1. **Lobby** — Player selects a character and taps "Find Match."
2. **Matchmaking** — Server groups players by mode, assigns teams round-robin.
3. **Spawn** — Each player spawns at their team's zone with full health and 3 seconds of invulnerability.
4. **Combat** — Players move, aim, and use abilities to eliminate opponents.
5. **Match End** — Timer expires or a win condition is met. Server broadcasts results.
6. **Return** — Players return to lobby. Reconnect flow handles mid-match disconnects.

---

## 3. Match Rules

| Parameter | Value |
|-----------|-------|
| Teams | 2 |
| Team size | 1 (expandable to 2–3) |
| Match duration | 20 seconds |
| Win condition | Survival — last team with alive players. Timer expiry falls back to score. |
| Spawn invulnerability | 3 seconds (90 ticks) |
| Respawn | None (single life per round) |

---

## 4. Characters

Characters are defined by a `CharacterId`, a `StatBlock`, and a list of ability IDs. The system supports an arbitrary roster; currently one character is implemented.

### 4.1 Stat Block

Every character has three base stats:

| Stat | Description | Brawler Default |
|------|-------------|-----------------|
| MaxHealth | Total hit points | 100 |
| Damage | Base ability damage scalar | 10 |
| MoveSpeed | Movement speed (units/sec) | 5 |

### 4.2 Brawler (Default)

**Identity:** Close-range melee fighter.

| Slot | Ability | Trigger | Cooldown | Damage | Range / Shape |
|------|---------|---------|----------|--------|---------------|
| 0 | Punch | Auto | 1 s | 10 | Circle (r=1.5) at 1.0u forward |
| 1 | Charge | Manual (aimed) | 3 s | 20 | Forward line (1.0w × 2.5l) at 2.5u forward |

- **Punch** fires automatically when an enemy is within 2.5 units. No friendly fire, no self-hit.
- **Charge** is player-aimed — direction follows the aim yaw input. Higher damage, longer cooldown.

---

## 5. Ability System

Abilities are defined as JSON node graphs with sequential and parallel branching.

### 5.1 Trigger & Cast Modes

| Trigger | Behavior |
|---------|----------|
| Auto | Fires when cooldown ready and enemy in `AutoRange` |
| Manual | Fires on player button press |

| Cast Mode | Behavior |
|-----------|----------|
| Instant | Executes at caster's current facing |
| Aimed | Uses the player's aim yaw as direction |

### 5.2 Node Types

| Node | Purpose |
|------|---------|
| Hitbox | Creates a melee damage/heal zone. Configurable shape, radius, duration, hit interval. |
| Projectile | Fires a traveling body with speed, radius, max range, and pierce count. |
| Parallel | Executes all children simultaneously; `Next` runs after all complete. |

Nodes chain sequentially via `Next` pointers. A root node's output feeds into the first execution node.

### 5.3 Hitbox Config

| Field | Description |
|-------|-------------|
| EffectType | `Damage` or `Heal` |
| Amount | HP value per hit |
| Radius | Area of effect |
| OffsetForward | Distance from caster center |
| Duration | Ticks the hitbox stays active |
| HitInterval | Ticks between re-hits on same target (0 = hit once) |
| HitSelf | Can hit caster |
| HitAllies | Can hit friendly team |

### 5.4 Telegraph Shapes

Telegraphs show players where an ability will land before it fires.

| Shape | Parameters |
|-------|------------|
| CircleAroundCaster | Radius |
| ForwardLine | Width, Length |
| ForwardCone | Radius, Angle |
| TargetCircle | Radius (at cursor/aim position) |

Direction always follows `AimYaw`.

---

## 6. Maps

Maps are JSON-baked from Unity scenes. They define static physics geometry (walls, obstacles) and team spawn areas. The visual prefab is separate from the physics data — no Unity colliders exist at runtime; all collision is through the AetherNet physics engine.

### 6.1 Arena TDM (Default)

| Property | Value |
|----------|-------|
| ID | `arena_tdm` |
| Dimensions | 52 × 82 units (portrait) |
| Boundaries | Walls at ±25.5 X, ±40.5 Z |
| Entities | 24 static obstacles (corner bunkers, mid-map cover) |
| Team 0 spawn (south) | Z = −35, X = {−8, 0, 8} |
| Team 1 spawn (north) | Z = 35, X = {−8, 0, 8} |
| Max players per team | 3 |

### 6.2 Arena Basic (Legacy)

| Property | Value |
|----------|-------|
| ID | `arena_basic` |
| Dimensions | 42 × 31 units (landscape) |
| Boundaries | Walls at ±20.5 X, ±15.5 Z |
| Entities | 6 symmetric box obstacles |
| Team 0 spawn (left) | X = −16, Z = {−3, 0, 3} |
| Team 1 spawn (right) | X = 16, Z = {−3, 0, 3} |

---

## 7. Movement & Physics

### 7.1 Input

| Axis | Encoding | Range |
|------|----------|-------|
| MoveX / MoveZ | Signed short (−32767 to +32767) | −1.0 to +1.0 |
| AimYaw | Quantized degrees | −180 to +180 |
| Buttons | Bitmask | Bit 0 = ability 0, bit 1 = ability 1 |

Diagonal input is normalized to prevent faster diagonal movement.

### 7.2 Movement Model

Each tick:
```
direction = normalize(moveX, moveZ)  // clamped to unit circle
position += direction * moveSpeed * deltaTime
yaw = atan2(moveX, moveZ)
```

### 7.3 Physics Engine

- **Library:** AetherNet (wrapper around Aether.Physics2D / Box2D port)
- **Player bodies:** Dynamic circles, radius 0.5 units
- **Map geometry:** Static bodies (walls, obstacles), friction 0.2, zero restitution
- **Gravity:** Zero (top-down plane)
- **Coordinate mapping:** Game (X, Z) maps to physics (x, y)
- **Simulation plane:** XZ

---

## 8. Health & Combat

### 8.1 Health Table

Each player has an entry in a shared `HealthTable`:

| Field | Description |
|-------|-------------|
| Current HP | Starts at `MaxHealth` (100) |
| Max HP | From character stats |
| Invulnerability ticks | Counts down each tick, blocks all damage while > 0 |

### 8.2 Damage Flow

1. Ability executor activates a hitbox node on the server.
2. Physics query detects players within hitbox radius + offset.
3. `HealthTable.ApplyDamage(playerId, amount)` is called.
4. If invulnerable → damage blocked.
5. If HP drops to 0 → player is dead (cannot act, still receives snapshots).
6. Health is broadcast every tick in `PlayerStateDto.Health`.

### 8.3 Invulnerability

- Granted on spawn: 90 ticks (3 seconds at 30 Hz).
- Decays by 1 each server tick.
- Visual indicator on client (shield effect).

---

## 9. Networking

### 9.1 Architecture

```
┌──────────┐     gRPC      ┌──────────────┐     gRPC     ┌────────────┐
│  Client   │ ◄──────────► │   Services   │ ◄──────────► │ GameServer │
│  (Unity)  │              │  (Matchmake, │              │  (Physics, │
│           │              │   Auth, DB)  │              │   Ticks)   │
└──────────┘              └──────────────┘              └────────────┘
                                 │
                                 ▼
                            ┌─────────┐
                            │ MongoDB  │
                            └─────────┘
```

- **Services tier:** Matchmaking, authentication (device ID → JWT), lobby, game-server registry.
- **GameServer tier:** Runs N concurrent matches (max 8). Each match has its own tick loop.
- **Client:** Thin display layer. Sends input, renders server state. Never decides game outcomes.

### 9.2 Tick Loop (30 Hz)

Each server tick:

1. Drain one `InputCommand` per player from input buffer.
2. Step physics simulation (`AetherNet`).
3. Tick health (decay invulnerability).
4. Tick abilities (process queued activations, auto-attack checks).
5. Encode `WorldStatePacket` (all player positions, health, invuln flags).
6. Broadcast snapshot to all players.
7. Broadcast ability events (hits).
8. Check match end condition.

### 9.3 Client Prediction (Gambetta Model)

| Component | Role |
|-----------|------|
| **Local player** | Client-side prediction + server reconciliation via `LastProcessedInputSeq` |
| **Remote players** | Entity interpolation from buffered snapshots, rendered ~66 ms in the past |

- Client runs a local physics world mirroring the server.
- Inputs are applied locally for instant response.
- On snapshot receipt: rewind to server state, replay unacknowledged inputs.
- Server is always authoritative — client state never overrides.

### 9.4 Wire Protocol

| Direction | Message | Contents |
|-----------|---------|----------|
| Client → Server | `InputCommand` | MoveX, MoveZ, AimYaw, ButtonMask, SequenceId |
| Server → Client | `SnapshotPacket` | `PlayerStateDto[]` — X, Z, Yaw, Health, InvulnFlag, LastProcessedInputSeq |
| Server → Client | `MatchEvent` | Ability cast/hit events for VFX |
| Server → Client | `OnMatchEnded` | `MatchResult` — WinningTeamId, TeamScores, EndedAtMs |

---

## 10. UI & Flow

### 10.1 Boot Sequence

```
AppStarter → PersistentUI → Environment Picker (dev only) → Identity → Server Ping → Lobby
```

### 10.2 Lobby

- Character selection (dropdown, currently Brawler only).
- **Play button** → enters matchmaking queue.
- Reconnect check on startup — if an active match exists, skip lobby and rejoin.
- Reconnect loop guard: max 3 attempts before giving up.

### 10.3 In-Match HUD

- Player health bars (world-space, above each character).
- Ability buttons with cooldown indicators.
- Telegraph preview for aimed abilities.
- Match timer.

### 10.4 Match End

- Server broadcasts `OnMatchEnded` — client never synthesizes this.
- Result screen shows winner.
- Return to lobby.

### 10.5 Disconnect Handling

- Server marks player as disconnected (not removed from match).
- Client can rejoin the same match within the remaining duration.
- Matches with < 10 seconds remaining reject reconnects — client returns to queue.
- On app unpause, a popup offers full boot reset.

---

## 11. Team Assignment

- Round-robin: Player 0 → Team 0, Player 1 → Team 1, Player 2 → Team 0, etc.
- Assignments are made by the matchmaker and sent in `MatchProvision.PlayerAssignments`.
- Spawn positions are determined by team ID and slot index via `SpawnResolver`.

---

## 12. Future Systems (Designed, Not Yet Implemented)

| System | Status |
|--------|--------|
| Lag compensation (server-side rewind for hit validation) | Architecture documented, not implemented |
| Additional characters beyond Brawler | Catalog system ready, no new characters defined |
| Projectile abilities | Node type exists, no abilities use it yet |
| Respawn / multi-round matches | Health system supports it, match flow doesn't |
| Score-based win conditions | `TeamScores` field exists, not populated |
| Heal abilities | `EffectType.Heal` supported, no abilities use it |
| Larger team sizes (2v2, 3v3) | Spawn slots support 3 per team, match config is parameterized |

---

## 13. Technical Summary

| Component | Technology |
|-----------|------------|
| Client engine | Unity 6 LTS |
| Client DI | VContainer |
| Client async | UniTask |
| Server framework | ASP.NET Core 8 + MagicOnion 7.10.0 |
| Serialization | MessagePack 3.1.4 |
| Physics | AetherNet (Aether.Physics2D / Box2D port) |
| Database | MongoDB 3.1.0 driver |
| Transport | gRPC (Grpc.Net 2.71.0) |
| Auth | Device ID → JWT (Phase 1) |
| Build | IL2CPP (Android), MagicOnion source generator |
