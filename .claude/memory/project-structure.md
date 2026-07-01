# Project Structure Details

## VContainer Scope Hierarchy
- **AppStarterLifetimeScope** (standalone) — boot-time services, root scope
- **CoreStarterLifetimeScope** (child of AppStarter via `EnqueueParent`) — gameplay session, GameFlowController
  - **LobbyLifetimeScope** (child of CoreStarter) — lobby screen
  - **MatchmakingLifetimeScope** (child of CoreStarter) — matchmaking waiting screen
  - **MatchLifetimeScope** (child of CoreStarter) — per-match, created/destroyed per match

## Data Flow Between Scopes
- AppStarter -> CoreStarter: via `SessionHandoff` (PlayerId, JWT, expiry)
- CoreStarter -> Match: via `MatchHandoff` (set on `GameFlowController.PendingHandoff` before child scope builds)
- GameFlowController orchestrates all scene transitions (Lobby ↔ Matchmaking ↔ Match) with loading screen

## Docker Compose (ops/docker/docker-compose.yml)
- mongo: port 27017, healthcheck
- services: port 5001, depends on healthy mongo, env overrides for Mongo + JWT keys
- gameserver: port 5101, depends on services, env overrides for services endpoint + JWT keys + dual endpoints
- `GameServer__PublicEndpoint=http://localhost:5101` (client-facing)
- `GameServer__InternalEndpoint=http://gameserver:5101` (service-to-service inside Docker)
- Uses ASP.NET `Section__Key` env var convention for config overrides
- `pull_policy: build` on both server services — auto-rebuilds images on `docker compose up`
- `Directory.Build.props` has condition to skip `.artifacts/` redirect inside containers

## Unity Local Package: com.clashup.shared
- Referenced via `file:../../../src/Shared/ClashUp.Shared` in manifest.json
- CRITICAL: `bin/` and `obj/` must NEVER exist in this folder — Unity imports everything
- Build output is redirected to `.artifacts/` via Directory.Build.props to prevent this
- If DLL clash error appears, delete any stale bin/obj and restart Unity

## Networking Layer
- Uses MagicOnion (gRPC-based) for client-server RPC
- YetAnotherHttpHandler for IL2CPP/AOT compatibility (replaces gRPC C-core)
- Hub pattern: IPingHub (smoke test), IMatchHub (gameplay)
- Service pattern: IAuthService, IMatchmakingService, IGameServerProvisionerService
- Client sends `x-clashup-player` header via `PlayerIdInterceptor` (gRPC Interceptor, not MagicOnion filter)
- `SessionTokenStore` holds PlayerId, EndUserJwt, MatchToken
- Server `ResolveCurrentPlayerId()` reads from `x-clashup-player` request header

## Additive Scenes
| Scene | Location | Loaded By | Purpose |
|-------|----------|-----------|---------|
| AppStarter | `_Bootstrap/AppStarter/Content/Scenes/` | Build Settings (0) | Root scope, boot |
| PersistentUI | `Core/UI/Content/Scenes/` | BootBootstrapper | Loading screen (app-wide) |
| Lobby | `Core/Lobby/Content/Scenes/` | GameFlowController | Lobby screen |
| Match | `Core/Match/Content/Scenes/` | GameFlowController | Per-match gameplay |
| Matchmaking | `Core/Matchmaking/Content/Scenes/` | GameFlowController | Matchmaking waiting screen |

See [scene-ownership.md](scene-ownership.md) for domain placement rules.

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
| ClashUp.EditorTools | ClashUp.Client.EditorTools | EditorTools/ (editor-only) |

Every client script lives under a domain folder with an asmdef. The old generic `Assets/Scripts/` and `Assets/Editor/` were removed — editor tools live in `Assets/EditorTools/`.

### Unity Package Versions (manifest.json)
- `com.unity.cinemachine`: 3.1.6 — namespace `Unity.Cinemachine`; `BindingMode` in `Unity.Cinemachine.TargetTracking`
- `com.unity.inputsystem`: 1.19.0 — use `Keyboard.current`, `Touchscreen.current`, `Mouse.current` for raw polling
- Player Settings → Active Input Handling: must be **"Both"** for new Input System + legacy UGUI to coexist
- `ClashUp.Gameplay.asmdef` refs: `Unity.Cinemachine`, `Unity.InputSystem`, `AetherNet.Unity`, `Unity.TextMeshPro`; precompiled `AetherNet.Shared.dll`
- `ClashUp.Match.asmdef` refs: `Unity.Cinemachine` (MatchLifetimeScope vcam field)

## Match Camera Architecture
- **MatchCamera** and **MatchVirtualCamera** are scene objects in `Match.unity` (NOT created via code)
- `MatchCamera`: Camera + CinemachineBrain + CameraRegistrant (`IsMatchCamera=true`, tag=MainCamera)
- `MatchVirtualCamera`: CinemachineCamera + CinemachineFollow (offset `(0, 32.5, -32.8)`, WorldSpace binding, 0.15 damping, FOV=35, rotation X=46.1)
- `MatchCameraRig` (VContainer `ITickable`) receives `CinemachineCamera` via DI, polls `PlayerViewSystem.LocalPlayerTransform` each tick, sets `_vcam.Follow` once player spawns
- `MatchLifetimeScope` has `[SerializeField] CinemachineCamera _virtualCamera` — must be wired to scene vcam

## Client Environment Switching
- `EnvironmentConfig` ScriptableObject in `Core/Networking/Scripts/Config/`
- `ServerEnvironment` enum: Local, Dev
- URLs stored in `SerializedDictionary<ServerEnvironment, string>`
- `SetCurrent(env)` + `GetAllEnvironments()` methods for runtime switching
- Dev-only `EnvironmentPickerUI` (legacy UI.Dropdown + Confirm button, code-generated, `#if DEVELOPMENT_BUILD || UNITY_EDITOR`)
- `ClashUpEndpoints.ServicesAddress` has public setter — picker updates it after scope construction
