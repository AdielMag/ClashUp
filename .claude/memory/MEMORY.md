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
- `ClashUp.Gameplay.asmdef` references: `Unity.Cinemachine`, `Unity.InputSystem`

## Server Package Versions (Directory.Packages.props)
- MagicOnion: 7.10.0 (7.10.1 does NOT exist on NuGet)
- MessagePack: 3.1.4
- Grpc: 2.71.0
- MongoDB.Driver: 3.1.0

## GUID Generation
- Use `python tools/generate-guid.py [count]` for Unity-style GUIDs (32 hex, no dashes)
- Never hand-write or guess GUIDs — always generate
- Custom command: `/generate-guid`

## Architecture Rules
- **Dumb client**: The client is a thin display layer. NEVER put game logic, state decisions, or authoritative behavior on the client. All game state transitions (match start, match end, scoring, etc.) must come from the server. The client only renders what the server tells it.
- **Server-authoritative**: The server is the single source of truth for all game state.

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

## Boot Flow Architecture
- **UniTask + VContainer** are core client frameworks (async + DI)
- **Scene loading**: `ISceneLoader` / `UniTaskSceneLoader` — additive load/unload via UniTask
- **Loading screen**: `LoadingScreenPresenter` in `PersistentUI` scene (Core/UI) — not DI-registered, found via `FindAnyObjectByType` after scene load
- **Lobby**: child scope of AppStarter via `LifetimeScope.EnqueueParent`
- **Environment picker**: code-generated UI (no prefab), `#if DEVELOPMENT_BUILD || UNITY_EDITOR`
- **Critical**: Server ping must block & retry — never proceed to lobby on failure
- **Boot sequence**: load PersistentUI → show loading → env picker (dev) → identity → ping → load lobby → hide loading
- **Game flow**: Lobby → (Play) → Matchmaking scene → (matched) → Match scene → (end) → Lobby
- **Reconnect flow**: Lobby checks for active match on startup → if found, skip lobby UI → go straight to Match
- **Disconnect handling**: Server marks player disconnected (not removed), client can rejoin same match. `MatchHub.JoinAsync` replays `OnMatchEnded` to late-joining clients. See [debugging.md](debugging.md) for the full race-condition fix sequence.
- **Pause handling**: `SessionResetHandler` (AppStarter, DontDestroyOnLoad) shows popup on app unpause → user confirms → full boot reset.
- **Client is dumb**: NEVER synthesize match-end on the client. Server always delivers `OnMatchEnded`. See [feedback-client-authority.md](feedback-client-authority.md).
- **Near-end guard**: `CheckActiveMatchAsync` rejects reconnects to matches with <10s remaining — marks Ended, returns Queued.

## See Also
- [project-structure.md](project-structure.md) — Detailed architecture notes
- [scene-ownership.md](scene-ownership.md) — Domain ownership rules (assets live where their lifespan is)
- [folder-conventions.md](folder-conventions.md) — Script subfolder rules (Interfaces/, Services/, etc.)
- [patterns.md](patterns.md) — Code patterns (IDebugLogger, canvas scaler, etc.)
- [debugging.md](debugging.md) — Common pitfalls and solutions (incl. full match-end freeze sequence)
- [feedback-client-authority.md](feedback-client-authority.md) — Never synthesize server state on the client
- [unity-mcp.md](unity-mcp.md) — Unity MCP CLI usage patterns and gotchas
