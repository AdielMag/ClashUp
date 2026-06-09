---
name: stat-health-system
description: "Character stats, health tracking, and deterministic RNG architecture for combat prediction"
metadata: 
  node_type: memory
  type: project
  originSessionId: ed44a8ee-8136-4a5b-85f8-0d83e840934d
---

# Stat & Health System Architecture

## Character Definitions (Shared)

Files in `src/Shared/ClashUp.Shared/Characters/`:
- `CharacterId` — string-backed struct, MessagePack-serializable, same pattern as `PlayerId`
- `StatBlock` — plain C# class: `MaxHealth` (100f), `Damage` (10f), `MoveSpeed` (5f). NOT MessagePack (static config)
- `CharacterDefinition` — `CharacterId Id`, `string DisplayName`, `StatBlock BaseStats`
- `CharacterRegistry` — static lookup. `Default` = "Brawler". `Get(id)`, `All`

**Why:** Both client and server need identical stat values. Hardcoded in Shared guarantees this.

## Health Tracking

`HealthTable` in `ClashUp.Shared/Simulation/` — `Dictionary<string, float>` wrapper.
- `Initialize(playerId, maxHealth)` — set starting health
- `ApplyDamage(playerId, amount)` → new health (clamped to 0); early-returns current health if player is invulnerable
- `ApplyHeal(playerId, amount, maxHealth)` → new health (clamped to max)
- `SnapHealth(playerId, health)` — server reconciliation override
- `SetInvulnerable(playerId, durationTicks)` — sets invulnerability timer
- `IsInvulnerable(playerId)` → bool — true if ticks remaining > 0
- `Tick()` — decrements all invuln counters; must be called once per simulation step
- `DefaultSpawnInvulnTicks = 90` — 3 seconds at 30 Hz

Owned by both `AetherServerSimulation` and `AetherClientSimulation` as sibling to `MatchPhysicsWorld`.

## Wire Protocol Keys

| DTO | Key | Field | Added |
|-----|-----|-------|-------|
| PlayerStateDto | 4 | `float Health` | stat system |
| PlayerStateDto | 5 | `int LastProcessedInputSeq` | netcode/reconciliation |
| PlayerStateDto | 6 | `bool IsInvulnerable` | spawn invulnerability |
| JoinResult | 6 | `uint RandomSeed` | stat system |
| PlayerSummary | 4 | `CharacterId CharacterId` | stat system |

**How to apply:** When adding new fields to these DTOs, use the next available Key index. Never reuse or change existing keys (MessagePack binary compat).

## Deterministic RNG

`DeterministicRng` in `ClashUp.Shared/Simulation/` — Xorshift32 PRNG.
- Constructor: `DeterministicRng(uint seed)` (seed=0 auto-corrects to 1)
- `Next()`, `NextFloat()` (0..1), `NextRange(min, max)`
- `ForTick(baseSeed, tick)` — per-tick re-seeding via `baseSeed ^ (uint)tick`

**Why per-tick re-seeding:** If client mispredicts tick N (different RNG call count), tick N+1 still produces correct random values because each tick is independently seeded. Critical for reconciliation — replaying ticks reproduces exact random sequences.

Server generates seed at `AetherServerSimulation` construction. Sent to client via `JoinResult.RandomSeed`.

## Movement Speed Integration

`MoveSpeed` flows from `StatBlock` through to physics:
- `MovementModel.Step()` accepts optional `float moveSpeed` param (defaults to `MoveSpeed` const for backward compat)
- `MatchPhysicsWorld.EnsurePlayer()` accepts optional `float moveSpeed` param, stores in `_playerMoveSpeeds` dictionary
- `MatchPhysicsWorld.Step()` reads per-player speed from `_playerMoveSpeeds` instead of hardcoded `MovementModel.MoveSpeed`
- Server/client simulations pass `stats.MoveSpeed` to `_world.EnsurePlayer()`

## Integration Points

- **Server `EnsurePlayer`**: Initializes health and passes move speed from `CharacterRegistry.Default.BaseStats`
- **Server `EncodeDelta`**: Includes `Health` from `HealthTable` in each `PlayerStateDto`
- **Client `ReconcileTo`**: Snaps health from server's `PlayerStateDto.Health` (local player only; remote health arrives via `RemotePlayerInterpolator`)
- **Client `SyncRenderStates`**: Copies health into `PlayerRenderState.Health` / `.MaxHealth` (local player only)
- **`MatchHub.JoinAsync`**: Sends `RandomSeed` and `CharacterId` in join result/summary

## Integration Points — Invulnerability

- **Server `EnsurePlayer`**: On first join (guarded by `_knownPlayers HashSet`), calls `_health.SetInvulnerable(player.Value, HealthTable.DefaultSpawnInvulnTicks)` after `Initialize`
- **Server `Step`**: Calls `_health.Tick()` after `_world.Step()` each tick
- **Server `EncodeDelta`**: Sets `IsInvulnerable = _health.IsInvulnerable(id)` on each `PlayerStateDto`
- **`PlayerRenderState`**: Has `IsInvulnerable` bool field — wired for future visual effects

## AetherServerSimulation EnsurePlayer Guard (Important)

`MatchTickLoop.Drain()` calls `simulation.EnsurePlayer()` every tick for every player. `MatchPhysicsWorld.EnsurePlayer` has an early-return guard, but the simulation layer must also guard health init — otherwise health resets to max every tick.

**Pattern**: `_knownPlayers HashSet<string>` in `AetherServerSimulation`. `if (!_knownPlayers.Add(player.Value)) return;` at the top of `EnsurePlayer`. Also prevents `_teamSlotCounters` from incrementing on every tick.

## Status

Infrastructure only — no combat mechanics. `HealthTable.ApplyDamage` is wired with invulnerability guard. Spawn invulnerability runs for 3s after join. Nothing deals damage yet.
