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
- `ClashUp.Gameplay.asmdef` references: `Unity.Cinemachine`, `Unity.InputSystem`, `AetherNet.Unity`, `Unity.TextMeshPro`; precompiledReferences includes `AetherNet.Shared.dll`
- `ClashUp.Match.asmdef` references: `Unity.Cinemachine` (added for MatchLifetimeScope vcam serialized field)

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
- **Wire protocol**: `InputCommand` up, `SnapshotPacket → WorldStatePacket → PlayerStateDto{X,Z,Yaw,Health,LastProcessedInputSeq,IsInvulnerable}` down
- **AetherNet.Shared**: `AetherNet.Shared.dll` (netstandard2.0, C# 10) committed in `Assets/Packages/AetherNet.Shared.0.1.0/`. Uses pre-built DLL — Unity can't compile C# 10 file-scoped namespaces.
- **AetherNet.Unity**: Source-only package copied to `Assets/Packages/AetherNet.Unity/` by `setup-aethernet.ps1`. These files ARE C# 9 compatible (block-scoped namespaces). Has Runtime + Editor asmdefs. `AetherSceneBaker.cs` excluded (depends on `AetherNet.Server`).
- **AetherNet.Unity asmdefs**: `AetherNet.Unity` (Runtime, unsafe, precompiled refs: AetherNet.Shared.dll + Aether.Physics2D.dll) and `AetherNet.Unity.Editor` (Editor-only, refs AetherNet.Unity)
- `Aether.Physics2D.dll` installed via NuGetForUnity. Both DLLs listed in `ClashUp.Shared.Unity.asmdef` precompiledReferences.
- **Server DLL wiring**: conditional MSBuild in `AetherNet.refs.props` (repo root) — `ProjectReference` when clone exists, `PackageReference` fallback
- **AetherNetSettings**: ScriptableObject at `Assets/Resources/AetherNetSettings.asset` — configures `SimulationPlane` (XZ) and `PixelsPerMeter` (1). Auto-applies in both editor (`[InitializeOnLoadMethod]`) and runtime (`[RuntimeInitializeOnLoadMethod]`).
- **Determinism watch**: Aether.Physics2D is float-based; monitor for rubber-banding jitter between x86 server and ARM client

### Client Prediction & Interpolation (Gambetta)
- **Local player**: client-side prediction + server reconciliation via `LastProcessedInputSeq` (sequence-based ack, NOT tick-based). Render with sub-tick alpha-lerp (prev/current) for smooth motion between 30 Hz fixed steps.
- **Remote players**: NOT in client physics world. Pure entity interpolation from buffered authoritative snapshots, rendered ~66ms in the past (2 × tick interval). `RemotePlayerInterpolator` ring buffer per player.
- **Lag compensation**: documented but not yet implemented (no combat). See [netcode-architecture.md](netcode-architecture.md).
- **Fixes to AetherNet**: must be generic/non-specific (upstreamable). Key fixes: `Directory.Packages.props` CPM opt-out, `SimulationPlane` enum, configurable `PixelsPerMeter`, `#nullable enable` on Unity files, `using` aliases for type ambiguities (RaycastHit, Vector2)

## Character / Stat / Health System
- **Characters**: `CharacterId` (string struct like PlayerId), `CharacterDefinition`, `CharacterRegistry` (static, in `ClashUp.Shared/Characters/`)
- **Stats**: `StatBlock` — `MaxHealth` (100), `Damage` (10), `MoveSpeed` (5). Plain C# class, not MessagePack (static config, not networked)
- **Per-player move speed**: `MatchPhysicsWorld.EnsurePlayer` accepts `moveSpeed` param, stores per-player speeds. `MovementModel.Step` also accepts optional `moveSpeed` param.
- **Default character**: "Brawler" via `CharacterRegistry.Default`. Single character for now, everyone gets the same.
- **Health**: `HealthTable` in `ClashUp.Shared/Simulation/` — `Initialize`, `ApplyDamage`, `ApplyHeal`, `SnapHealth`. Owned by both `AetherServerSimulation` and `AetherClientSimulation`.
- **Health in snapshots**: `PlayerStateDto.Health` (Key 4) — sent every tick, client reconciles against it
- **Random seed**: `DeterministicRng` (Xorshift32) in Shared. Per-tick re-seeding via `ForTick(baseSeed, tick)` to avoid drift. Seed generated server-side, sent in `JoinResult.RandomSeed` (Key 6).
- **PlayerSummary.CharacterId** (Key 4) — sent on join
- **PlayerRenderState**: has `Health`, `MaxHealth`, and `Prev{X,Z,Yaw}` fields. Local player synced from `HealthTable` in `SyncRenderStates()`; remote health comes from `RemotePlayerInterpolator`.
- **No combat yet**: HealthTable API exists but nothing deals damage. Health bar UI renders health (full bars until damage is added).
- **Health bar UI**: `WorldSpaceHealthBar.cs` in `Core/Gameplay/Scripts/UI/` — world-space filled Image under Player.prefab's NameLabel Canvas. `PlayerViewSystem` caches per-player reference and calls `SetHealth(current, max)` each frame.

## Map System
- **Shared POCOs**: `MapData`, `BakedEntityDef`, `BakedFixtureDef`, `SpawnArea` in `ClashUp.Shared/Maps/`
- **SpawnResolver**: Static `GetSpawnPosition(MapData?, teamId, slotIndex)` in Shared — falls back to linear layout when no map
- **Server**: `ServerMapStore` singleton loads `Maps/Data/*.json` (System.Text.Json). `AetherServerSimulation.LoadMap()` + spawn via `SpawnResolver`
- **Client**: `MapDefinition` SO (mapId, displayName, TextAsset json, visual prefab) + `MapRegistry` SO (`SerializedDictionary<string, MapDefinition>`)
- **Client deserialization**: `MapDataDeserializer` uses Newtonsoft.Json (not System.Text.Json — netstandard2.1)
- **Wire protocol**: `MapId` field on `MatchConfig` (Key 4), `MatchProvision` (Key 5), `JoinResult` (Key 7) — default `"arena_tdm"`
- **Baker**: `ClashUpMapBaker` editor tool ("ClashUp/Bake Map to JSON") — scans `AetherRigidbody` + `SpawnPointMarker` components
- **Visual prefab**: Instantiated by `MatchSessionRunner.LoadMap()`, destroyed on Dispose. NO Unity colliders — physics is AetherNet only
- **Materials**: `Assets/Core/Match/Content/Maps/Materials/` — WallGray, GroundGreen, SpawnZone (transparent)
- **Maps**: `arena_basic` (40×30 landscape, legacy), `arena_tdm` (50×80 portrait, current default) — 24 entities, teams at Z=±35
- **Map JSON location**: server `src/Server/ClashUp.GameServer/Maps/Data/` + client `Assets/Core/Match/Content/Maps/` — must keep both in sync

## Ability System
- **Shared POCOs**: `AbilityDefinition`, `AbilityNode`, `HitboxConfig`, `ProjectileConfig`, `TelegraphConfig` in `ClashUp.Shared/Abilities/`
- **Node types**: `Parallel=0, Hitbox=1, Projectile=2` (string enum in JSON — never integer, fragile on reorder)
- **Sequential chaining**: via `AbilityNode.Next` linked-list; root's output connects to first node, "Next" port chains the rest
- **Parallel execution**: Parallel node's `Children[]` — all run simultaneously, `Next` runs after all finish
- **No Sequence node**: sequential chaining is implicit via Next ports — Sequence node was removed
- **Wire protocol**: abilities NOT sent over wire; loaded from JSON at match start by both client and server
- **Server**: `ServerAbilityStore` loads `Abilities/Data/*.json`; `AbilityExecutor` (per-match) processes input and ticks nodes
- **Executor**: `ActiveAbility.Flatten()` builds flat node array; `EvaluateChain()` follows `Next` pointers; `EvaluateParallel()` uses `Children[]`
- **Telegraph shapes**: `CircleAroundCaster`, `TargetCircle`, `ForwardLine`, `ForwardCone` — direction always follows `AimYaw`
- **Editor tool**: `Tools → Ability Editor` (UIToolkit GraphView, `ClashUp.AbilityEditor.asmdef`). Save to BOTH server and client paths.
- **JSON serialization**: `JsonStringEnumConverter` (server, System.Text.Json) + `StringEnumConverter` (editor, Newtonsoft) — MUST use string enums
- **Wiring**: `CharacterDefinition.Abilities AbilityId[]` in `CharacterRegistry.cs`; server calls `AbilityExecutor.InitPlayer` on player spawn
- See [ability-authoring.md](ability-authoring.md) for full schema and examples

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

## Match Camera Architecture
- **MatchCamera** and **MatchVirtualCamera** are scene objects in `Match.unity` (NOT created via code)
- `MatchCamera`: Camera + CinemachineBrain + CameraRegistrant (`IsMatchCamera=true`, tag=MainCamera)
- `MatchVirtualCamera`: CinemachineCamera + CinemachineFollow (offset `(0, 32.5, -32.8)`, WorldSpace binding, 0.15 damping, FOV=35, rotation X=46.1)
- `MatchCameraRig` (VContainer `ITickable`) receives `CinemachineCamera` via DI, polls `PlayerViewSystem.LocalPlayerTransform` each tick, sets `_vcam.Follow` once player spawns
- `MatchLifetimeScope` has `[SerializeField] CinemachineCamera _virtualCamera` — must be wired to scene vcam

## Boot Flow Architecture
- **UniTask + VContainer** are core client frameworks (async + DI)
- **Scene loading**: `ISceneLoader` / `UniTaskSceneLoader` — additive load/unload via UniTask
- **Loading screen**: `LoadingScreenPresenter` in `PersistentUI` scene (Core/UI) — not DI-registered, found via `FindAnyObjectByType` after scene load
- **Lobby**: child scope of AppStarter via `LifetimeScope.EnqueueParent`
- **Environment picker**: prefab-based TMP UI loaded via `Resources.Load`, `#if CLASHUP_DEV || UNITY_EDITOR`
- **Environments**: Local (`localhost:5001`), Tailscale (`100.68.118.109:5001`), Dev (remote). Tailscale for phone→local-server testing. Emulator uses `adb reverse` + Local.
- **Critical**: Server ping must block & retry — never proceed to lobby on failure
- **Boot sequence**: load PersistentUI → show loading → env picker (dev) → identity → ping → load lobby → hide loading
- **Active scene**: `GameFlowController` calls `SceneManager.SetActiveScene()` after every additive load — ensures `new GameObject()` / `Instantiate()` go into the correct scene (not AppStarter). See [scene-ownership.md](scene-ownership.md).
- **Game flow**: Lobby → (Play) → Matchmaking scene → (matched) → Match scene → (end) → Lobby
- **Reconnect flow**: Lobby checks for active match on startup → if found, skip lobby UI → go straight to Match
- **Disconnect handling**: Server marks player disconnected (not removed), client can rejoin same match. `MatchHub.JoinAsync` replays `OnMatchEnded` to late-joining clients. See [debugging.md](debugging.md) for the full race-condition fix sequence.
- **Pause handling**: `SessionResetHandler` (AppStarter, DontDestroyOnLoad) shows popup on app unpause → user confirms → full boot reset.
- **Client is dumb**: NEVER synthesize match-end on the client. Server always delivers `OnMatchEnded`. See [feedback-client-authority.md](feedback-client-authority.md).
- **Near-end guard**: `CheckActiveMatchAsync` rejects reconnects to matches with <10s remaining — marks Ended, returns Queued.
- **Reconnect loop guard**: `LobbyEntryPoint` limits reconnect attempts (static counter, max 3). Reset on successful match join.

## Android / IL2CPP Build Requirements
- **MagicOnion Source Generator**: `[MagicOnionClientGeneration(...)]` attribute required in `ClashUp.Networking` for IL2CPP. See `MagicOnionGeneratedClientInitializer.cs`.
- **Standard shader**: Must be in `AlwaysIncludedShaders` (fileID: 46) — player materials use it.
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
- [feedback-reread-before-edit.md](feedback-reread-before-edit.md) — Always re-read files before editing after plan phase
- [netcode-architecture.md](netcode-architecture.md) — Gambetta netcode: prediction, reconciliation, entity interpolation
- [monday-api.md](monday-api.md) — Monday.com API integration, board IDs, column gotchas
- [mvp1-architecture.md](mvp1-architecture.md) — YARP gateway, session cache, write-behind persistence, version routing
- [feedback-no-singletons.md](feedback-no-singletons.md) — Use DI-registered services, not singleton pattern
- [feedback-ticket-status.md](feedback-ticket-status.md) — Never mark tickets Done without user confirmation it's working
- [ability-authoring.md](ability-authoring.md) — How to create ability JSON files: editor tool, schema, node types, wiring to characters
