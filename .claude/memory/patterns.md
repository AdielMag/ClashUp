# Code Patterns & Conventions

## IDebugLogger (Logging Service)
- **Interface**: `ClashUp.Client.Core.IDebugLogger` in `Core/Scripts/Interfaces/IDebugLogger.cs`
- **Implementation**: `ClashUp.Client.Core.UnityDebugLogger` in `Core/Scripts/Services/UnityDebugLogger.cs`
- **Methods**: `Log(string)`, `LogWarning(string)`, `LogError(string)`
- **Registration**: Both `AppStarterLifetimeScope` and `CoreStarterLifetimeScope` register `IDebugLogger → UnityDebugLogger` as Singleton
- **Usage**: Inject via constructor, never use `UnityEngine.Debug.Log` directly in DI-managed classes
- **Editor scripts**: Can still use `Debug.Log` directly (not DI-managed)

## Canvas Scaler Convention
- All code-generated canvases use `matchWidthOrHeight = 1f` (match height, not width)

## Boot Flow Error Handling
- Server connection (ping) must **block and retry** — never swallow exceptions and continue
- Pattern: `while` loop with `try/catch`, retry after delay, only `break` on success
- `OperationCanceledException` must be re-thrown (don't catch-and-retry cancellation)
- Show retry feedback in loading screen ("Connection failed. Retrying...")

## Environment Picker
- Uses legacy `UnityEngine.UI.Dropdown` (NOT TMP_Dropdown — too complex to code-generate template)
- Confirm button triggers `UniTaskCompletionSource.TrySetResult`
- Must ensure `EventSystem` exists (check + create if null)
- Sort order 200 (above loading screen at 100)

## Camera Ownership
- Main Camera lives in Lobby scene (Core), NOT AppStarter
- AppStarter scene is bootstrap-only — no visual/rendering objects

## Dumb Client Principle
- Client is a thin display layer — NEVER put game logic on the client
- All game state transitions (match end, scoring, phase changes) must originate from the server
- Client only renders what the server tells it — no local state decisions
- If client misses a server broadcast, the SERVER must re-send it — don't add client-side fallback logic
- Client-side timers are display-only — they don't trigger state changes

## Server-Authoritative Timers
- Match countdown must use server-provided `ElapsedSeconds` as the base, not start from zero
- `JoinResult.ElapsedSeconds` = how far into the match (from `MatchClock.ElapsedSeconds`)
- Client computes: `remaining = DurationSeconds - (ElapsedSeconds + localElapsedSinceJoin)`
- Use `DateTimeOffset.UtcNow` for local elapsed (survives focus loss), not `Time.time`

## Player Identity Flow
- `SessionTokenStore` holds `PlayerId` (set during auth login)
- `MatchmakingClient` sends `x-clashup-player` header via gRPC `Interceptor` on all calls
- Server's `ResolveCurrentPlayerId()` reads this header (fallback: random GUID — should NOT happen)
- Same player ID must be used for Enqueue, Poll, and CheckActiveMatch — otherwise reconnect fails

## MatchContext End-of-Match Callbacks
`MatchContext` has two separate callbacks wired by `MatchRegistry.Register`:
- `OnMatchEndedEarly`: fires `NotifyMatchEndedAsync` (GS → Services, marks DB "Ended") — invoked **before** broadcasting to clients, giving DB time to update
- `OnMatchEnded`: removes from registry + disposes context — invoked **after** 2s client broadcast window
- Never merge these — the DB notification must lead the cleanup by ~2s to prevent lobby reconnect loops.
- `IsEnded` + `EndResult` on `MatchContext` are set at the same moment as `OnMatchEndedEarly`, for late-join replay in `JoinAsync`.

## ConfigSeeder
- `ConfigSeeder` always upserts (no skip-if-exists) so config values take effect on server restart without manual DB cleanup.
- Current match config: `{"NumberOfTeams":1,"TeamSize":1,"DurationSeconds":20,"ObjectiveType":"survival"}`

## Match Reconnection Pattern
- Server: `MatchContext` tracks players as connected/disconnected (not removed on disconnect)
- Server: `OnDisconnected` → `MarkDisconnected` + broadcast `OnPlayerLeft(Disconnect)`, keep in player list
- Server: `JoinAsync` checks `IsPlayerInMatch` → reconnect (MarkConnected) vs new join (AddPlayer)
- Server: `CheckActiveMatchAsync` on `IMatchmakingService` — looks up active match by player ID, issues fresh token
- Server: If GS instance is gone, marks orphaned match as "Ended" and returns no active match
- Client: `LobbyEntryPoint` checks for active match on startup → skip lobby, go straight to match
- Client: `GameFlowController.EnterMatchFromLobby()` — lobby → match (bypasses matchmaking)
