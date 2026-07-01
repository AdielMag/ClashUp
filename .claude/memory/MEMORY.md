# ClashUp Project Memory

## Project Overview
Unity multiplayer game with C# server backend (ASP.NET Core 8 + MagicOnion 7.10.0).

## Key Paths
- **Server tests**: `src/Server/ClashUp.GameServer.Tests` — consolidated xUnit suite. See [server-test-suite.md](server-test-suite.md).
- **Server solution**: `src/Server/ClashUp.Server.sln` (Services, GameServer, Server.Common)
- **Root solution**: `ClashUp.sln` (all projects including Shared)
- **Shared project**: `src/Shared/ClashUp.Shared/` — also a Unity local package via `file:` reference
- **Unity project**: `client/ClashUp.Unity/`
- **Docker**: `ops/docker/docker-compose.yml` (mongo + services + gameserver)
- **Build artifacts**: `.artifacts/` (redirected from bin/obj via Directory.Build.props)
- **Dotnet path (Windows)**: `"/c/Program Files/dotnet/dotnet.exe"`
- **AetherNet vendor clone**: `external/AetherNet/` (gitignored, run `tools/setup-aethernet.ps1` after cloning)
- **UI Toolkit migration**: [ui-toolkit-migration.md](ui-toolkit-migration.md) — runtime UI moved UGUI→UI Toolkit; UXML/USS/fonts/PanelSettings in `Assets/Resources/UI/`

## Client Folder Structure (Unity Assets)
Scripts live in typed subfolders (Interfaces/, Services/, Clients/, Models/, Config/, Scopes/, EntryPoints/, Presenters/, UI/, Receivers/). See [folder-conventions.md](folder-conventions.md). Assembly definitions + package versions + Match Camera architecture: [project-structure.md](project-structure.md).

## Server Package Versions (Directory.Packages.props)
- MagicOnion: 7.10.0 (7.10.1 does NOT exist on NuGet)
- MessagePack: 3.1.4 | Grpc: 2.71.0 | MongoDB.Driver: 3.1.0
- AetherNet.Shared: 0.1.0 (fallback NuGet version, normally uses local clone ProjectReference)

## GUID Generation
- Use `python tools/generate-guid.py [count]` for Unity-style GUIDs (32 hex, no dashes) — never hand-write/guess. Custom command: `/generate-guid`

## Architecture Rules
- **Dumb client**: NEVER put game logic, state decisions, or authoritative behavior on the client. All game state transitions come from the server; client only renders. See [boot-flow.md](boot-flow.md), [feedback-client-authority.md](feedback-client-authority.md).
- **Server-authoritative**: server is the single source of truth for all game state.

## Physics, Netcode & Simulation
- **AetherNet physics** (vendored, GC-free deterministic 2D over Aether.Physics2D): [aethernet-architecture.md](aethernet-architecture.md)
- **Client prediction / server reconciliation / remote interpolation**: [netcode-architecture.md](netcode-architecture.md)

## Character / Stat / Health / Ability Systems
- **Characters, stats, health, RNG, respawn**: [stat-health-system.md](stat-health-system.md)
- **Character selection UI**: [character-selection.md](character-selection.md)
- **Ability runtime architecture** (nodes, executor, wire protocol, shapes, tuning, VFX): [ability-system-core.md](ability-system-core.md)
- **Ability JSON authoring/editor tool**: [ability-authoring.md](ability-authoring.md), [ability-editor-ui.md](ability-editor-ui.md), [feedback-ability-editor-sync.md](feedback-ability-editor-sync.md)
- **Projectiles**: [projectile-system.md](projectile-system.md) | **TargetPoint cast mode**: [target-point-cast.md](target-point-cast.md)

## Map System
Shared `MapData` POCOs, server/client loaders, baker editor tool, procedural visual builder. See [map-system.md](map-system.md), [map-visual-builder.md](map-visual-builder.md).

## Important Conventions
- Central package management via `Directory.Packages.props`
- Build output redirected to `.artifacts/` to avoid polluting Unity's local package import
- `MsgPack017` suppressed in ClashUp.Shared.csproj (MessagePack v3 stricter about init properties)
- Server projects need both `MagicOnion.Server` AND `MagicOnion.Client` (server-to-server RPC), plus explicit `Grpc.Net.Client`
- EnvironmentConfig is a ScriptableObject using `SerializedDictionary<ServerEnvironment, string>` from editor-toolbox (asmdef name: `Toolbox`)

## User Preferences
- Prefers concise, action-oriented responses — do it, don't explain it. Terse/typo-laden requests are normal; infer intent and implement rather than asking lots of clarifying questions.
- Wants hierarchical folder structure, not flat
- Automate Unity Editor steps via MCP tools first, editor scripts second — NEVER leave manual instructions. Only leave to user what truly can't be scripted (e.g. creating ScriptableObject .asset files)
- Wants persistent learnings across sessions (memory files, `/reflect` command)
- Uses custom commands and subagents — see `.claude/commands/` and `.claude/agents/`
- Doesn't want manual rebuild steps — automate everything (e.g. `pull_policy: build` in docker-compose)
- Prefers quick iterative fixes over lengthy exploration/planning when the problem is clear
- **Fix vendored packages at the source** — never create project-side workarounds for issues in vendored packages (AetherNet, etc.)
- **Tests on a real phone** and reports UX bugs tersely ("doesn't work well") without repro steps — when this happens, audit the relevant code for root cause (e.g. a threshold/geometry mismatch) rather than asking for more detail; MCP tools can't drive live multi-touch, so reason from the UI Toolkit event model and flag explicitly that a fix is untested on-device.

## Boot Flow / Client Lifecycle
Scene loading, environment picker, reconnect/disconnect, pause-reset. See [boot-flow.md](boot-flow.md).

## In-Match UI (UI Toolkit)
Joysticks (shared circular touch-zone routing, floating re-anchor, cancel bar, cooldown badges): [joystick-ui.md](joystick-ui.md). General UI Toolkit migration notes/gotchas: [ui-toolkit-migration.md](ui-toolkit-migration.md).

## Android / IL2CPP Build
MagicOnion source-gen requirement, shader stripping, emulator ports. See [android-build.md](android-build.md).

## See Also
- [project-structure.md](project-structure.md) — Assembly defs, package versions, Match Camera, VContainer scopes, Docker Compose, networking layer, additive scenes
- [scene-ownership.md](scene-ownership.md) — Domain ownership rules (assets live where their lifespan is)
- [folder-conventions.md](folder-conventions.md) — Script subfolder rules (Interfaces/, Services/, etc.)
- [patterns.md](patterns.md) — Code patterns (IDebugLogger, canvas scaler, DI patterns, etc.)
- [debugging.md](debugging.md) — Common pitfalls and solutions (incl. full match-end freeze sequence, UI Toolkit gotchas)
- [unity-mcp.md](unity-mcp.md) — Unity MCP CLI usage patterns and gotchas
- [dev-environment.md](dev-environment.md) — CLASHUP_DEV define, Tailscale phone testing, ServerEnvironment enum
- [feedback-ticket-status.md](feedback-ticket-status.md) — Never mark tickets Done without user confirmation it's working
- [elimination-mode.md](elimination-mode.md) — 2nd game mode: no-respawn + points economy + breakable boxes/orbs
- [forfeit-leave-match.md](forfeit-leave-match.md) — Mid-match "Leave Match"/forfeit flow (client + server)
- [bot-system.md](bot-system.md) — Server-side AI bots: matchmaking fill, BotDirector FSM, perception
- [lobby-ui.md](lobby-ui.md) / [lobby-ui-reskin.md](lobby-ui-reskin.md) — Lobby pager UI + Clash-style reskin
- [gcp-ops-gotchas.md](gcp-ops-gotchas.md) — Deploy/verify gotchas (GITHUB_TOKEN, MIG update policies, Monitoring REST API)
- [deployment-architecture.md](deployment-architecture.md) — Version-aware gateway, on-demand backend spawning, CCU model
- [dashboard-tool.md](dashboard-tool.md) — Local fleet dashboard (`src/Tools/ClashUp.Dashboard`, :8080, NOT deployed)
