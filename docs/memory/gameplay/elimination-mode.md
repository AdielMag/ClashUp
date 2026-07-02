---
name: ""
metadata: 
  node_type: memory
  originSessionId: 844e760f-a894-491d-89fe-79fac96fcb3d
---

Second selectable game mode (added 2026-06-23). No respawn, points economy, last-team-standing. Survival is untouched (all new systems no-op unless `ObjectiveType == "elimination"`).

**Mode plumbing**: lobby `GameModeButton` (modeId) on each entry under `MathmakingPage` → discovered by `GetComponentsInChildren<GameModeButton>` → `OnModeSelected(modeId)` → `SelectedGameModeStore` (CoreStarter scope) → `QueueRequest.ModeId` → `MatchmakingQueue.TryDrain(size, modeId)` (mode-isolated; `Matchmaker` loops `GetQueuedModeIds()`) → `match:{modeId}` config → `MatchProvision.ObjectiveType` → `AetherServerSimulation.Configure` + `JoinResult.ObjectiveType` → client `MatchModeHolder`. Config seeded as `match:elimination` (2 teams, TeamSize 1, 180s, map `arena_pickup`).

**Server** (`ClashUp.GameServer/Simulation/`): `AetherServerSimulation` owns `BoxSimulation`, `PointOrbSimulation`, `PlayerProgression`. `Step` branches `HandleEliminations()` (mark `_eliminated`, drop points as orbs, emit `player_eliminated`, NO respawn timer) vs `HandleRespawns()`. `MatchTickLoop.ComputeAliveTeams()` excludes eliminated players (survival: never eliminated → identical to old roster logic). Timer expiry in elimination → `TeamWithMostPoints()`. `IServerSimulation` gained `Configure` / `IsEliminated` / `GetTeamScores` (also stubbed in Null/MovementServerSimulation).

**Boxes/orbs = server-only AetherNet bodies** (client renders from snapshot, like remote players). Gotchas:
- Orbs live on physics **layer bit 5** (`MatchPhysicsWorld.OrbQueryMask`); mask = Environment|Orb, **excludes players** — pickup is an `OverlapCircle` proximity query, NOT physical collision. `PhysicsWorldManager.OverlapCircle` **skips sensors**, so orbs must be SOLID (non-sensor) bodies to be found.
- `MatchPhysicsWorld` needs **entity-id recycling** (`_freeIds` stack) because orbs spawn/destroy constantly; `MaxBodies` bumped 256→512. `PhysicsWorldManager.DestroyBody` already exists in the committed `AetherNet.Shared.dll` (no vendor patch needed).
- Boxes = static Environment-layer bodies (block players, hittable). Ability/projectile damage routed: overlap entity → player (`GetPlayerByEntityId`) else box (`IsBoxEntity` → `BoxSimulation.ApplyDamage`). Box break → drop orbs + `box_broken` event + respawn timer.

**Scaling** ([[stat-health-system]]): `PointScaling` (Shared, tunable: +5 HP/pt cap +200, +2% dmg/pt cap 3x). `PlayerProgression.Add/Take` pushes `HealthTable.SetMaxHealth(healDelta:true)`; damage multiplier threaded through `AbilityExecutor` + `ProjectileSimulation` (optional/null in survival).

**Wire**: `WorldStatePacket` += `Boxes[]`/`Orbs[]` (Key 1/2); `PlayerStateDto` += `Points`(8)/`IsEliminated`(9)/`MaxHealth`(10). `MatchProvision.ObjectiveType`(9), `JoinResult.ObjectiveType`(10). Append-only keys.

**Client**: `BoxViewSystem`/`PointOrbViewSystem` read `ClientPredictionWorld.SnapshotDecoded` (new event w/ decoded packet) — code-driven primitives, no prefabs. `EliminationHudController` (points/players-left/feed). `RespawnScreenController` shows permanent "ELIMINATED" in elimination. `PlayerViewSystem` hides eliminated bodies + uses server `MaxHealth`. `MatchUI.ShowMatchEnded(result, localTeamId)` → VICTORY/DEFEAT/MATCH OVER + team score.

**Map** `arena_pickup` (50×50, hand-authored JSON in server `Maps/Data/` + client `Content/Maps/`, `MapData.BoxSpawns`). `BoxSpawnMarker` + baker support added for future scene authoring. Tests: `src/Server/ClashUp.GameServer.Tests/`.
