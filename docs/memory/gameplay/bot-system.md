---
name: bot-system
description: "Server-side AI bots — bot-fill matchmaking, BotDirector FSM, bot-only match cleanup"
metadata: 
  node_type: memory
  type: project
  originSessionId: 5ba90579-1194-4856-9914-4e0824254ebc
---

Server-authoritative AI bots (added 2026-06-23). Client unchanged — bots are just remote players rendered from snapshots.

**Bot-fill matchmaking** (`ClashUp.Services/Matchmaking/`): per-mode `MatchConfig` gained `FillWithBots`/`BotFillWaitSeconds`/`MinRealPlayers` (Keys 5-7, off by default). `Matchmaker.DrainOnceAsync` falls back after a full human batch fails: if the oldest queued ticket for the mode has waited ≥ `BotFillWaitSeconds` and queued ≥ `MinRealPlayers`, it `MatchmakingQueue.DrainUpTo(matchSize, modeId)` (new partial-drain helper, alongside `CountQueued`/`OldestEnqueuedAt`) and provisions with `botCount = matchSize - realCount`. `ProvisionMatchAsync(modeId, config, batch, botCount, ct)` builds bot `PlayerAssignment`s (`IsBot=true`, id `"bot:"+guid`, teams continue the modulo). Bots get NO match token and are NOT in `MatchDoc.Players`. **Bot-fill only produces combat for `NumberOfTeams >= 2`** (enemy detection is team-based). Seeded example: `match:elimination` has `FillWithBots:true, BotFillWaitSeconds:10` (existing Mongo docs need dropping to pick up new fields — seeder is write-if-missing).

**Wire flags**: `PlayerAssignment.IsBot` (Key 2), `PlayerSummary.IsBot` (Key 5). Both additive/backward-compatible. Client may optionally label bots (not done).

**Bot AI** (`ClashUp.GameServer/Simulation/BotDirector.cs`, scoped service, registered in `Program.cs`): per-bot FSM Wander→Chase→Attack→Flee→**SeekBox**, seeded `DeterministicRng`. `Think(botId, sim, tick)` returns a synthetic `InputCommand` (movement via `MovementModel.EncodeAxis`). Perception via `IServerSimulation.TryGetBotView(botId, out BotView)` — nearest living different-team player AND nearest alive box (`BoxSimulation.TryGetNearestBox`); `NullServerSimulation`/`MovementServerSimulation` return false. **Auto-attacks auto-fire already** (`AbilityExecutor.Tick`), so bots only steer + press the active ability (slot 1, `ButtonMask = (1u<<1) | InputCommand.AutoAimFlag`; executor no-ops while on cooldown / if no active ability). **Box-seeking (utility AI)**: bots farm boxes for points but engage a player when one is detected and `EnemyDistance <= BoxDistance * PlayerPreference` (1.25 → players preferred even when somewhat farther). Active ability is NOT spent on boxes (auto-attack breaks them). Tunables in `BotDirector`: DetectRange 12, AttackRange 4, PlayerPreference 1.25, wander 30t, attack-hold 30t, flee 45t.

**Auto-attacks AND ability auto-aim now hit boxes** (real players AND bots): all three target-acquisition paths in `AbilityExecutor` consider the nearest enemy player OR breakable box (via `world.IsBoxEntity` / `_boxes != null`): the auto-attack auto-trigger (`Tick`), the directional manual-ability auto-aim (`ProcessInput`), and the TargetPoint auto-aim (`ActivateTargetPoint`). Helpers: `FindNearestTargetYaw` + `FindNearestTargetPoint` (the old players-only `FindNearestEnemyYaw`/`FindNearestEnemyPoint` were removed). `EvaluateHitbox`/projectiles already damaged boxes; the bug was targeting never *selected* a box, so abilities/auto-attacks ignored them.

**Client bot UI**: `PlayerSummary.IsBot` reaches `PlayerViewSystem.RegisterPlayer`; bot nameplates are tinted orange (`BotNameColor`). DisplayName is already "Bot xxxx" from the server. No new prefab wiring.

**Bot materialization**: `MatchRegistry.Register` creates bot `PlayerSummary`s from `provision.PlayerAssignments` (random character from roster) and `context.Bots.RegisterBot` BEFORE the tick loop starts, so they spawn + appear in `JoinResult.Players`. `MatchTickLoop.Drain` branches: bots get `Bots.Think` input, humans dequeue from `InputBuffer`.

**Bot-only cleanup**: `MatchContext.RealPlayerCount()` = `_players` where `!IsBot`. `MatchTickLoop` tracks `_sawRealPlayer`; **before** the last-team-standing check it ends the match (reason "all human players left") once `_sawRealPlayer && realCount == 0`, so a bot-only match never lingers and no bot is declared winner. Forfeit (`LeaveAsync`→`Forfeit`→`RemovePlayer`) drops the human → triggers next tick. Disconnect keeps them in roster (reconnect grace, unchanged).

Tests: `ClashUp.GameServer.Tests/BotTests.cs` (queue helpers + BotDirector FSM via a FakeSim). See [[stat-health-system]], [[netcode-architecture]].
