# Debugging & Common Pitfalls

## dotnet CLI on Windows/bash
- `dotnet` is NOT on PATH in the bash shell
- Always use: `"/c/Program Files/dotnet/dotnet.exe"`
- Don't try broad PATH searches — user will reject them. Just use the full path.

## Unity Local Package (com.clashup.shared) DLL Clash
- `src/Shared/ClashUp.Shared/` is imported by Unity via `file:` reference
- If `dotnet build` runs and outputs `bin/obj` inside that folder, Unity sees the compiled DLL and conflicts with the asmdef
- Fix: `Directory.Build.props` redirects output to `.artifacts/`. If stale `bin/obj` appear, delete them AND restart Unity (not just reimport)
- Empty `bin/obj` dirs can reappear even after deletion — `rmdir` them, don't just `rm -rf` (which leaves empty dirs)
- Unity caches package state in `Library/` — if error persists after cleanup, delete `Library/` for full reimport

## Bulk Namespace Renames
- After sed-replacing namespaces (e.g. `Foo.Bar.Baz` -> `Foo.Bar`), always check for **duplicate using lines**
- The old `using Foo.Bar.Baz;` and `using Foo.Bar;` both become `using Foo.Bar;` — two identical lines
- Run a dedup pass after every bulk rename: `grep -rn "duplicate pattern" --include="*.cs"`

## MagicOnion Hub Broadcast Methods
- Hub receiver methods (e.g. `OnSnapshot`) return `void` — they are fire-and-forget
- In MagicOnion v7, `group.All.OnSnapshot()` returns void, NOT a Task
- Do NOT `await` broadcast calls — use plain call and return `Task.CompletedTask` from the calling method

## NuGet Package Gotchas
- MagicOnion 7.10.1 does NOT exist — latest is 7.10.0. Always verify versions on nuget.org before upgrading.
- Server projects that call other servers as gRPC clients need BOTH `MagicOnion.Server` AND `MagicOnion.Client`
- `Grpc.Net.Client` must be explicitly referenced for `GrpcChannel` — it's NOT pulled in transitively by `Grpc.AspNetCore.Server`
- MessagePack v3 `MsgPack017` analyzer is strict about `init` properties with initializers — suppress in shared projects targeting netstandard2.1

## Unity ScriptableObject Assets
- `.asset` files CANNOT be created from CLI — they contain serialized GUIDs and binary references
- Must be created in Unity Editor via CreateAssetMenu
- When designing ScriptableObject-based config, flag upfront that the user needs to create the asset in Unity

## Unity asmdef References
- When a script uses types from a package (e.g. `SerializedDictionary` from editor-toolbox), the asmdef must reference that package's runtime asmdef
- Toolbox package runtime asmdef name: `Toolbox`
- Missing asmdef reference = `CS0246: type or namespace not found`

## Moving Unity Files
- Every file AND folder in Unity has a `.meta` file — they must move together to preserve GUIDs
- If `.meta` files are lost, Unity regenerates new GUIDs which breaks all references (prefab fields, scene refs, etc.)
- `git mv` fails on untracked files — use plain `mv` for newly created files, then `git add`
- After moving files, create `.meta` files for any new directories using `python tools/generate-guid.py`

## FindAnyObjectByType and Inactive Objects
- `FindAnyObjectByType<T>()` does NOT find inactive GameObjects (default behavior)
- If a MonoBehaviour calls `gameObject.SetActive(false)` in `Awake()`, it becomes unfindable
- **Fix**: Use `CanvasGroup.alpha = 0` + `blocksRaycasts = false` instead of `SetActive(false)` for objects that need to be found later

## VContainer RegisterEntryPoint and Concrete Type Injection
- `RegisterEntryPoint<T>()` only registers `T` as `IAsyncStartable` (or `IStartable`, etc.)
- If another class injects `T` by concrete type, VContainer throws "No such registration"
- **Fix**: Chain `.AsSelf()` — e.g. `builder.RegisterEntryPoint<GameFlowController>().AsSelf();`
- This registers as both `IAsyncStartable` AND `GameFlowController`

## Editor Scripts: Scene Creation
- `EditorSceneManager.NewScene(EmptyScene, Additive)` makes the new scene the active scene
- Any `new GameObject()` created after this is automatically placed in that scene
- `SceneManager.MoveGameObjectToScene()` is unnecessary and causes namespace ambiguity with `UnityEditor.SceneManagement`
- Always create one-time editor setup scripts (menu items) instead of leaving manual steps for the user

## Docker + Directory.Build.props
- `Directory.Build.props` redirects output to `.artifacts/` using `$(MSBuildThisFileDirectory)` absolute paths
- Inside Docker, this creates paths like `/src/.artifacts/bin/...` which break `dotnet run` (but NOT `dotnet publish -o`)
- Fix: Condition the redirect with `Condition="'$(DOTNET_RUNNING_IN_CONTAINER)' != 'true'"` — official .NET Docker images set this env var
- **Docker layer caching**: Changing `Directory.Build.props` may NOT invalidate cached build layers if the COPY step content hash is unchanged from Docker's perspective. Use `docker compose build --no-cache` to force rebuild.
- `pull_policy: build` in docker-compose rebuilds images on `up`, but does NOT recreate containers from stale images — use `docker compose down` then `up`, or `up --force-recreate`

## JwtSecurityTokenHandler Claim Mapping
- Default `JwtSecurityTokenHandler` maps `sub` → `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`
- `FindFirst("sub")` returns null even though the JWT contains `sub`
- **Fix**: Set `MapInboundClaims = false` on the handler before calling `ValidateToken`
- This affects `MatchTokenValidator` — any code validating JWTs and reading claims by short name

## Docker Dual-Endpoint (PublicEndpoint vs InternalEndpoint)
- In Docker, services calls the GS via internal DNS (`gameserver:5101`), but clients connect via `localhost:5101`
- `PublicEndpoint` = what clients receive in MatchHandoff (e.g. `http://localhost:5101`)
- `InternalEndpoint` = what services uses for PrepareMatchAsync (e.g. `http://gameserver:5101`)
- Both are stored in `GameServerInstanceDoc` and `GsRegistration`
- Matchmaker uses `InternalEndpoint` for admin calls, `PublicEndpoint` for handoff
- docker-compose sets both: `GameServer__PublicEndpoint` and `GameServer__InternalEndpoint`
- `InternalEndpoint` defaults to `PublicEndpoint` if empty (non-Docker local dev)

## Stale GS Instance Records
- GS restarts generate new instance IDs, leaving orphaned "Healthy" records in MongoDB
- The matchmaker picks these dead instances and PrepareMatchAsync fails
- **Fix**: On registration, `DrainOthersByEndpointAsync` marks all other instances with the same InternalEndpoint as "Draining"
- Also: `CheckActiveMatchAsync` should end orphaned matches when the GS instance is gone

## MagicOnion Client Headers (gRPC Metadata)
- `MagicOnionClient.Create<T>(channel, ...)` — 2nd param is `IClientFilter[]`, NOT `CallOptions`
- `IClientFilter.RequestContext.CallOptions` is **read-only** — cannot set headers via filters
- **Fix**: Use `Grpc.Core.Interceptors.Interceptor` with `channel.Intercept(interceptor)`:
  ```csharp
  var invoker = channel.Intercept(new MyInterceptor());
  var client = MagicOnionClient.Create<T>(invoker);
  ```
- Override `AsyncUnaryCall` to inject headers via `ClientInterceptorContext`
- See `PlayerIdInterceptor` in `MatchmakingClient.cs`

## Match-End Frozen at 00:00 — Full Sequence of Fixes

**Symptom**: Client shows "Match in progress" with 00:00 frozen after match ends.

There are three distinct root causes, each fixed separately:

### 1. Late-join replay (client reconnects during 2s grace window)
- **Cause**: After the tick loop broadcasts `OnMatchEnded`, it waits 2s then cleans up. A client that joins DURING those 2s misses the broadcast.
- **Fix**: `MatchContext.IsEnded` + `EndResult` are set *before* broadcasting. `MatchHub.JoinAsync` checks `context.IsEnded` after adding client to group and calls `Client.OnMatchEnded(endResult)` directly.

### 2. DB race: lobby reconnects to a just-ended match
- **Cause**: `NotifyMatchEndedAsync` (GS → Services → MongoDB) was fire-and-forget launched *after* the 2s wait. Lobby's `CheckActiveMatchAsync` fires before MongoDB updates → sends client back into match.
- **Fix**: `MatchContext.OnMatchEndedEarly` fires `NotifyMatchEndedAsync` immediately at match end (before broadcast + 2s wait). This gives MongoDB 2+ seconds to update before any client finishes returning to lobby.
- Two callbacks on `MatchContext`: `OnMatchEndedEarly` (notify DB) and `OnMatchEnded` (remove from registry + dispose).

### 3. Match not in GS registry — client gets stuck
- **Cause**: If client returns to lobby after the 2s window (match fully cleaned up), GS throws Internal error. Client was stuck showing "Failed:...".
- **Fix A**: `MatchHub.JoinAsync` re-fires `ReportMatchEndedAsync` when match not found — ensures DB catches up even if the original notify was lost.
- **Fix B**: `MatchSessionRunner.StartAsync` calls `ReturnToLobbyAsync()` on any connect failure instead of showing a stuck error screen.

### 4. Near-end match guard (< 10s)
- **Cause**: After pause/reset, lobby reconnects to a match with < 10s remaining — useless join.
- **Fix**: `CheckActiveMatchAsync` checks `(UtcNow - MatchDoc.CreatedAt).TotalSeconds` vs `MatchDoc.DurationSeconds`. If remaining < 10s, marks match Ended and returns Queued.
- `MatchDoc.DurationSeconds` was added for this purpose; set by `Matchmaker` when creating the doc.

**Rule**: The client is dumb — it never synthesizes match-end on its own. The server must always deliver `OnMatchEnded`. `SessionResetHandler` handles the pause case separately (full boot reset on unpause).

## Unity Time.time Pauses on Focus Loss
- `Time.time` stops when Unity Editor loses focus (OnApplicationPause)
- For timers that should keep ticking (match countdown), use `DateTimeOffset.UtcNow` instead
- Pattern: store `_joinWallClock = DateTimeOffset.UtcNow` at start, compute elapsed as `(UtcNow - _joinWallClock).TotalSeconds`

## Server JWT Configuration
- `JwtKeyProvider` requires `Jwt:EndUserSigningKey` and `Jwt:InterTierSigningKey` (min 32 bytes each)
- Dev keys are in `appsettings.Development.json` for both Services and GameServer
- Docker-compose provides them via `Jwt__EndUserSigningKey` env vars (ASP.NET `__` separator convention)
- If server crashes with "Jwt:EndUserSigningKey is not configured", check ASPNETCORE_ENVIRONMENT=Development is set
