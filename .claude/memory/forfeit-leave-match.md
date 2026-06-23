---
name: forfeit-leave-match
description: "In-match \"Leave Match\"/forfeit flow — client confirm prompt, server team-alive end condition, permanent player ejection"
metadata: 
  node_type: memory
  type: project
  originSessionId: 408d9580-71f7-400b-bd95-400b946e46a1
---

Mid-match forfeit / "Leave Match" feature (built 2026-06-22).

**Client** ([MatchUI.cs](client/ClashUp.Unity/Assets/Core/Match/Scripts/UI/MatchUI.cs)): a persistent top-right "Leave Match" button (visible during active play) opens a confirmation overlay ("Leave the match? This will forfeit..."). Confirm reuses the existing `OnBackToLobbyClicked` event → `MatchSessionRunner.OnBackToLobby`. `ShowMatchEnded` hides the leave button (the centered back button takes over, no confirm).

**CRITICAL gotcha — leave must be AWAITED before teardown.** `MatchSessionRunner.Dispose` fires `_session.LeaveAsync().Forget()` then immediately `_session.Dispose()` (disposes the gRPC channel). The fire-and-forget RPC is killed by the channel teardown before it reaches the server, so the server only sees `OnDisconnected` → treats it as a *reconnectable* disconnect → player stays in match → lobby reconnects ("still saying reconnect" bug). Fix: `OnBackToLobby` → `LeaveAndReturnAsync` which `await _session.LeaveAsync()` **then** `_flow.ReturnToLobbyAsync()`. The subsequent Dispose-time `LeaveAsync().Forget()` is then a server-side no-op (the `_left` guard). Correspondingly, server `MatchHub.LeaveAsync` **awaits** `ReportPlayerLeftAsync` (not fire-and-forget) so the Mongo `$pull` is done by the time the client's awaited call returns — otherwise `CheckActiveMatchAsync` races the DB write.

**Server end condition** ([MatchTickLoop.cs](src/Server/ClashUp.GameServer/Simulation/MatchTickLoop.cs)): each tick checks `MatchContext.GetAliveTeamIds()` (distinct TeamIds among remaining `_players`). Two forfeit-driven ends, both guarded against firing at startup before players join:
- `_sawAnyPlayer` (flips true once ≥1 player present) + `aliveTeams.Count == 0` → `EndMatchAsync(0, "all players left")` — **solo match / last player left just closes the match**.
- `_sawMultipleTeams` (flips true once ≥2 teams present) + `aliveTeams.Count <= 1` → `EndMatchAsync(survivingTeam, "last team standing")`.
Correctly handles >2 teams (one leaver still leaves 2+) and >1 player/team (a team isn't gone until all its players leave). `BuildMatchResult(int winningTeamId)` and `EndMatchAsync(winningTeamId, reason)` were extracted; timer-expiry path passes `0`.

**Permanent ejection** (the key requirement — never route a forfeiter back into the match):
- `MatchHub.LeaveAsync` ([MatchHub.cs](src/Server/ClashUp.GameServer/Hubs/MatchHub.cs)) sets a `_left` guard (so the following socket-close `OnDisconnected` doesn't re-mark them as a reconnectable disconnect), calls `MatchContext.Forfeit(playerId)` (adds to `_forfeited` set + `RemovePlayer`), decrements CCU, and fires `ReportPlayerLeftAsync` to Services.
- `MatchHub.JoinAsync` rejects `context.IsForfeited(playerId)` (throws) — GS-local barrier while the match is live.
- Services removal: new `GsPlayerLeft` MsgPack obj + `IGameServerRegistry.ReportPlayerLeftAsync` → `GameServerRegistryImpl` → `IMatchRepository.RemovePlayerAsync` (Mongo `$pull` from `MatchDoc.Players`). This makes `FindActiveForPlayerAsync` (used by `CheckActiveMatchAsync`) stop returning the match, so the lobby reconnect-on-startup path no longer pulls them back.

Forfeit vs disconnect: forfeit = permanent (`RemovePlayer` + `_forfeited` + doc removal); disconnect = reconnectable (`MarkDisconnected` keeps the player, doesn't reduce alive teams).

See [[stat-health-system]] and the disconnect/reconnect notes in [debugging.md](debugging.md).
