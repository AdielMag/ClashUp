# ClashUp

Unity multiplayer game (mobile arena) with a C# server backend (ASP.NET Core 8 + MagicOnion 7.10.0)
and a vendored GC-free deterministic 2D physics engine (AetherNet over Aether.Physics2D).

## Core invariants (non-negotiable)
- **Dumb client.** NEVER put game logic, authoritative state, or decisions on the client. All game
  state transitions come from the server; the client only sends input and renders what it's told.
- **Server-authoritative.** The server is the single source of truth for all gameplay state.
- **Never synthesize server-authoritative events on the client as a fallback** — if the server isn't
  delivering something reliably, fix the server.
- **No static singletons for server services** — register via DI behind an interface.
- **Fix vendored packages at the source** (AetherNet, etc.) — never add project-side workarounds.

Detailed, path-scoped rules live in `docs/rules/` and auto-load via `.claude/rules/` when you edit
matching files (server-authority, shared-contracts, magiconion-hub-discipline, mongo-data, jwt-auth,
async-discipline, il2cpp-aot, naming-conventions, unity-folder-structure, vcontainer-scopes).

## Layout
- `client/ClashUp.Unity/` — Unity client. Scripts live in typed subfolders (Interfaces/, Services/,
  Clients/, Models/, Config/, Scopes/, EntryPoints/, Presenters/, UI/, Receivers/). Runtime UI is
  UI Toolkit. DI via VContainer.
- `src/Server/` — server solution `ClashUp.Server.sln` (Services, GameServer, Server.Common).
- `src/Shared/ClashUp.Shared/` — types crossing the client/server boundary; also consumed by Unity
  as a local UPM package via `file:` reference.
- `external/AetherNet/` — vendored physics (gitignored; run `tools/setup-aethernet.ps1` after clone).
- `ops/docker/docker-compose.yml` — mongo + services + gameserver.
- `docs/` — Obsidian vault: GDD, `rules/`, and Claude auto-memory in `memory/`. Start at `docs/Home.md`.
- Build output is redirected to `.artifacts/` (via Directory.Build.props) to avoid polluting Unity's
  local package import. Central package versions in `Directory.Packages.props`.

## Commands (Windows)
- dotnet is at `"/c/Program Files/dotnet/dotnet.exe"` (not on a bare `dotnet` alias in this shell).
- Build server: `"/c/Program Files/dotnet/dotnet.exe" build src/Server/ClashUp.Server.sln`
- Tests: consolidated xUnit suite in `src/Server/ClashUp.GameServer.Tests`.
- Unity edits: prefer the `ai-game-developer` MCP tools first, editor scripts second — never leave
  manual Editor steps for the user unless truly unscriptable (e.g. authoring a ScriptableObject .asset).
- Unity-style GUIDs: `python tools/generate-guid.py [count]` — never hand-write them.
- git push: `env -u GITHUB_TOKEN -u GH_TOKEN git push origin main` (an invalid `GITHUB_TOKEN` env var
  otherwise overrides the working Windows credential manager).

## Memory
Claude auto-memory is git-tracked at `docs/memory/` (via `autoMemoryDirectory`), organized into topical
subfolders with `MEMORY.md` as the index. See `docs/memory/reference/claude-memory-system.md`. Memory
writes surface as git changes — commit them periodically (`/reflect` does this).

## Working style
Concise and action-oriented — do it, don't over-explain. Terse/typo'd requests are normal; infer intent
and implement rather than asking many clarifying questions. Hierarchical folders, not flat. Automate
rebuild/setup steps rather than leaving them manual.
