# ClashUp Project Memory

## Project Overview
Unity multiplayer game with C# server backend (ASP.NET Core 8 + MagicOnion 7.10.0).

## Key Paths
- **Server solution**: `src/Server/ClashUp.Server.sln` (Services, GameServer, Server.Common)
- **Root solution**: `ClashUp.sln` (all projects including Shared)
- **Shared project**: `src/Shared/ClashUp.Shared/` — also a Unity local package via `file:` reference
- **Unity project**: `client/ClashUp.Unity/`
- **Docker**: `ops/docker/docker-compose.yml` (mongo + services + gameserver)
- **Build artifacts**: `.artifacts/` (redirected from bin/obj via Directory.Build.props)
- **Dotnet path (Windows)**: `"/c/Program Files/dotnet/dotnet.exe"`
- **AetherNet vendor clone**: `external/AetherNet/` (gitignored, run `tools/setup-aethernet.ps1` after cloning)

## Client Folder Structure (Unity Assets)
Scripts live in typed subfolders (Interfaces/, Services/, Clients/, Models/, Config/, Scopes/, EntryPoints/, Presenters/, UI/, Receivers/). See [folder-conventions.md](folder-conventions.md).

## Assembly Definitions (asmdef)
| Name | Namespace | Location |
|------|-----------|----------|
| ClashUp.AppStarter | ClashUp.Client.AppStarter | _Bootstrap/AppStarter/Scripts/ |
| ClashUp.Core | ClashUp.Client.Core | Core/Scripts/ |
| ClashUp.UI | ClashUp.Client.UI | Core/UI/Scripts/ |
| ClashUp.Networking | ClashUp.Client.Networking | Core/Networking/Scripts/ |
| ClashUp.Gameplay | ClashUp.Client.Gameplay | Core/Gameplay/Scripts/ — subfolders: Interfaces/, Services/, Input/, Player/, Camera/ |
| ClashUp.Match | ClashUp.Client.Match | Core/Match/Scripts/ |
| ClashUp.CoreStarter | ClashUp.Client.CoreStarter | Core/CoreStarter/Scripts/ |
| ClashUp.Lobby | ClashUp.Client.Lobby | Core/Lobby/Scripts/ |
| ClashUp.Matchmaking | ClashUp.Client.Matchmaking | Core/Matchmaking/Scripts/ |

## Unity Package Versions (manifest.json)
- `com.unity.cinemachine`: 3.1.6 — namespace `Unity.Cinemachine`; `BindingMode` in `Unity.Cinemachine.TargetTracking`
- `com.unity.inputsystem`: 1.19.0 — use `Keyboard.current`, `Touchscreen.current`, `Mouse.current` for raw polling
- Player Settings → Active Input Handling: must be **"Both"** for new Input System + legacy UGUI to coexist
- `ClashUp.Gameplay.asmdef` references: `Unity.Cinemachine`, `Unity.InputSystem`, `AetherNet.Unity`; precompiledReferences includes `AetherNet.Shared.dll`

## Server Package Versions (Directory.Packages.props)
- MagicOnion: 7.10.0 (7.10.1 does NOT exist on NuGet)
- MessagePack: 3.1.4
- Grpc: 2.71.0
- MongoDB.Driver: 3.1.0
- AetherNet.Shared: 0.1.0 (fallback NuGet version, normally uses local clone ProjectReference)

## GUID Generation
- Use `python tools/generate-guid.py [count]` for Unity-style GUIDs (32 hex, no dashes)
- Never hand-write or guess GUIDs — always generate
- Custom command: `/generate-guid`

## Architecture Rules
- **Dumb client**: The client is a thin display layer. NEVER put game logic, state decisions, or authoritative behavior on the client. All game state transitions (match start, match end, scoring, etc.) must come from the server. The client only renders what the server tells it.
- **Server-authoritative**: The server is the single source of truth for all game state.

## Physics / AetherNet Architecture
- **Library**: AetherNet (`external/AetherNet/` gitignored clone) — GC-free deterministic 2D physics over Aether.Physics2D (Box2D port)
- **Simulation seam**: `IClientSimulation` (client) / `IServerSimulation` (server) — AetherNet implementations are `AetherClientSimulation` / `AetherServerSimulation`
- **Shared world**: `MatchPhysicsWorld` in `ClashUp.Shared/Simulation/` — same code runs on client (prediction) and server (authority)
- **Coordinate mapping**: game (X, Z) ↔ Aether (x, y); gravity = zero for top-down
- **Player bodies**: dynamic circles, velocity set from input each tick (kinematic move-and-slide style)
- **Player radius**: `MatchPhysicsWorld` constructor parameter (default `0.4f`). Client reads from prefab's `AetherCircleCollider.Radius`. Server uses default.
- **Wire protocol**: `InputCommand` up, `SnapshotPacket → WorldStatePacket → PlayerStateDto{X,Z,Yaw,Health}` down
- **AetherNet.Shared**: `AetherNet.Shared.dll` (netstandard2.0, C# 10) committed in `Assets/Packages/AetherNet.Shared.0.1.0/`. Uses pre-built DLL — Unity can't compile C# 10 file-scoped namespaces.
- **AetherNet.Unity**: Source-only package copied to `Assets/Packages/AetherNet.Unity/` by `setup-aethernet.ps1`. These files ARE C# 9 compatible (block-scoped namespaces). Has Runtime + Editor asmdefs. `AetherSceneBaker.cs` excluded (depends on `AetherNet.Server`).
- **AetherNet.Unity asmdefs**: `AetherNet.Unity` (Runtime, unsafe, precompiled refs: AetherNet.Shared.dll + Aether.Physics2D.dll) and `AetherNet.Unity.Editor` (Editor-only, refs AetherNet.Unity)
- `Aether.Physics2D.dll` installed via NuGetForUnity. Both DLLs listed in `ClashUp.Shared.Unity.asmdef` precompiledReferences.
- **Server DLL wiring**: conditional MSBuild in `AetherNet.refs.props` (repo root) — `ProjectReference` when clone exists, `PackageReference` fallback
- **AetherNetSettings**: ScriptableObject at `Assets/Resources/AetherNetSettings.asset` — configures `SimulationPlane` (XZ) and `PixelsPerMeter` (1). Auto-applies in both editor (`[InitializeOnLoadMethod]`) and runtime (`[RuntimeInitializeOnLoadMethod]`).
- **Determinism watch**: Aether.Physics2D is float-based; monitor for rubber-banding jitter between x86 server and ARM client
- **Fixes to AetherNet**: must be generic/non-specific (upstreamable). Key fixes: `Directory.Packages.props` CPM opt-out, `SimulationPlane` enum, configurable `PixelsPerMeter`, `#nullable enable` on Unity files, `using` aliases for type ambiguities (RaycastHit, Vector2)

## Character / Stat / Health System
- **Characters**: `CharacterId` (string struct like PlayerId), `CharacterDefinition`, `CharacterRegistry` (static, in `ClashUp.Shared/Characters/`)
- **Stats**: `StatBlock` — `MaxHealth` (100), `Damage` (10). Plain C# class, not MessagePack (static config, not networked)
- **Default character**: "Brawler" via `CharacterRegistry.Default`. Single character for now, everyone gets the same.
- **Health**: `HealthTable` in `ClashUp.Shared/Simulation/` — `Initialize`, `ApplyDamage`, `ApplyHeal`, `SnapHealth`. Owned by both `AetherServerSimulation` and `AetherClientSimulation`.
- **Health in snapshots**: `PlayerStateDto.Health` (Key 4) — sent every tick, client reconciles against it
- **Random seed**: `DeterministicRng` (Xorshift32) in Shared. Per-tick re-seeding via `ForTick(baseSeed, tick)` to avoid drift. Seed generated server-side, sent in `JoinResult.RandomSeed` (Key 6).
- **PlayerSummary.CharacterId** (Key 4) — sent on join
- **PlayerRenderState**: has `Health` and `MaxHealth` fields, synced from `HealthTable` in `SyncRenderStates()`
- **No combat yet**: HealthTable API exists but nothing deals damage. Infrastructure only.

## Important Conventions
- Central package management via `Directory.Packages.props`
- Build output redirected to `.artifacts/` to avoid polluting Unity's local package import
- `MsgPack017` suppressed in ClashUp.Shared.csproj (MessagePack v3 stricter about init properties)
- Server projects need both `MagicOnion.Server` AND `MagicOnion.Client` (server-to-server RPC)
- Server projects need `Grpc.Net.Client` for `GrpcChannel`
- EnvironmentConfig is a ScriptableObject using `SerializedDictionary<ServerEnvironment, string>` from editor-toolbox
- Toolbox package: `com.browar.editor-toolbox` (asmdef name: `Toolbox`)

## User Preferences
- Prefers concise, action-oriented responses — do it, don't explain it
- Wants hierarchical folder structure, not flat
- Automate Unity Editor steps via MCP tools first, editor scripts second — NEVER leave manual instructions
- Only leave to user what truly can't be scripted (e.g. creating ScriptableObject .asset files)
- Wants persistent learnings across sessions (memory files, /reflect command)
- Uses custom commands and subagents — see `.claude/commands/` and `.claude/agents/`
- Doesn't want manual rebuild steps — automate everything (e.g. `pull_policy: build` in docker-compose)
- Prefers quick iterative fixes over lengthy exploration/planning when the problem is clear
- **Fix vendored packages at the source** — never create project-side workarounds for issues in vendored packages (AetherNet, etc.). Fix the package itself so it works correctly.

## Boot Flow Architecture
- **UniTask + VContainer** are core client frameworks (async + DI)
- **Scene loading**: `ISceneLoader` / `UniTaskSceneLoader` — additive load/unload via UniTask
- **Loading screen**: `LoadingScreenPresenter` in `PersistentUI` scene (Core/UI) — not DI-registered, found via `FindAnyObjectByType` after scene load
- **Lobby**: child scope of AppStarter via `LifetimeScope.EnqueueParent`
- **Environment picker**: prefab-based TMP UI loaded via `Resources.Load`, `#if CLASHUP_DEV || UNITY_EDITOR`
- **Environments**: Local (`localhost:5001`), Tailscale (`100.68.118.109:5001`), Dev (remote). Tailscale for phone→local-server testing. Emulator uses `adb reverse` + Local.
- **Critical**: Server ping must block & retry — never proceed to lobby on failure
- **Boot sequence**: load PersistentUI → show loading → env picker (dev) → identity → ping → load lobby → hide loading
- **Game flow**: Lobby → (Play) → Matchmaking scene → (matched) → Match scene → (end) → Lobby
- **Reconnect flow**: Lobby checks for active match on startup → if found, skip lobby UI → go straight to Match
- **Disconnect handling**: Server marks player disconnected (not removed), client can rejoin same match. `MatchHub.JoinAsync` replays `OnMatchEnded` to late-joining clients. See [debugging.md](debugging.md) for the full race-condition fix sequence.
- **Pause handling**: `SessionResetHandler` (AppStarter, DontDestroyOnLoad) shows popup on app unpause → user confirms → full boot reset.
- **Client is dumb**: NEVER synthesize match-end on the client. Server always delivers `OnMatchEnded`. See [feedback-client-authority.md](feedback-client-authority.md).
- **Near-end guard**: `CheckActiveMatchAsync` rejects reconnects to matches with <10s remaining — marks Ended, returns Queued.
- **Reconnect loop guard**: `LobbyEntryPoint` limits reconnect attempts (static counter, max 3). Reset on successful match join.

## Android / IL2CPP Build Requirements
- **MagicOnion Source Generator**: `[MagicOnionClientGeneration(...)]` attribute required in `ClashUp.Networking` for IL2CPP. See `MagicOnionGeneratedClientInitializer.cs`.
- **Standard shader**: Must be in `AlwaysIncludedShaders` (fileID: 46) — player materials use it; `PlayerSpawner` still uses `CreatePrimitive()` for ground plane.
- **Custom AndroidManifest.xml**: Do NOT add one — Unity generates it correctly. Adding a minimal one strips the launcher activity.
- **Emulator ports**: `adb reverse tcp:5001 tcp:5001` AND `tcp:5101 tcp:5101` (Services + GameServer).
- **Package name**: `com.DefaultCompany.ClashUp.Unity`
- **adb path**: `C:\Users\Adiel\AppData\Local\Android\Sdk\platform-tools\adb.exe`

## See Also
- [project-structure.md](project-structure.md) — Detailed architecture notes
- [scene-ownership.md](scene-ownership.md) — Domain ownership rules (assets live where their lifespan is)
- [folder-conventions.md](folder-conventions.md) — Script subfolder rules (Interfaces/, Services/, etc.)
- [patterns.md](patterns.md) — Code patterns (IDebugLogger, canvas scaler, etc.)
- [debugging.md](debugging.md) — Common pitfalls and solutions (incl. full match-end freeze sequence)
- [feedback-client-authority.md](feedback-client-authority.md) — Never synthesize server state on the client
- [unity-mcp.md](unity-mcp.md) — Unity MCP CLI usage patterns and gotchas
- [dev-environment.md](dev-environment.md) — CLASHUP_DEV define, Tailscale phone testing, ServerEnvironment enum
- [stat-health-system.md](stat-health-system.md) — Character stats, health table, deterministic RNG architecture
- [feedback-scope-narrowing.md](feedback-scope-narrowing.md) — Start with minimum fields, ask before over-designing
