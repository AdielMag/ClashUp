# ClashUp Project Rules

These are the rules for working on ClashUp. **Keep this file minimal.** When you need to add a rule, create a new `.md` file in this folder and reference it below with a single-line explanation of what it covers and when it applies.

## Project-wide principles

- **Shared C# is the single source of truth across server and Unity.** Cross-tier types live in `src/Shared/ClashUp.Shared/` — never duplicate them.
- **Server-authoritative gameplay.** The server's AetherNet simulation decides outcomes. The client predicts and reconciles.
- **No new top-level folders or LifetimeScopes without updating this index.** New conventions need a rule file before they're adopted.
- **Phase-1 scope is small on purpose.** No leaderboards, no MMR, no regions, no social login. Shape APIs so these can be added later without changing call sites.

## Index of detailed rules

- [`unity-folder-structure.md`](unity-folder-structure.md) — How `Assets/` is organized: domain folders, each with `Scripts/` and optional `Content/` subfolders by asset type. Applies to all Unity client code and assets.
- [`shared-contracts.md`](shared-contracts.md) — All cross-tier types (hub interfaces, DTOs, wire formats) live in `src/Shared/ClashUp.Shared/`. No duplicating types on client or server. Applies to any new RPC, hub, or wire DTO.
- [`server-authority.md`](server-authority.md) — Server runs the authoritative AetherNet sim; client predicts and reconciles. Never let client-only state decide gameplay outcomes. Applies to all gameplay code.
- [`vcontainer-scopes.md`](vcontainer-scopes.md) — `AppStarter` and `CoreStarter` are independent LifetimeScope roots; gameplay scopes (Match, etc.) parent under `CoreStarter`; `AppStarter` does NOT parent `CoreStarter`. Applies whenever adding a new scope.
- [`magiconion-hub-discipline.md`](magiconion-hub-discipline.md) — Hub methods only validate + enqueue inputs and forward broadcasts emitted by the tick loop. No sim work inside hub methods. Applies to all StreamingHubs.
- [`async-discipline.md`](async-discipline.md) — Server returns `Task`/`ValueTask`; Unity returns `UniTask`. Never `.Result`/`.Wait()`. Pass `CancellationToken` through long-running ops. Applies to every async method we write.
- [`jwt-auth.md`](jwt-auth.md) — Two signing keys: end-user (issued by `IAuthService`) and inter-tier per-match (used by `MatchToken` for sticky handoff). Claim names are normative. Applies to anything that mints or validates tokens.
- [`mongo-data.md`](mongo-data.md) — Mongo access only through repository classes in `Persistence/`; required indexes are declared on startup; no raw `IMongoCollection` calls from services or hubs. Applies to all data access.
- [`il2cpp-aot.md`](il2cpp-aot.md) — MessagePack + MagicOnion source generators must be wired; `link.xml` maintained; no `Reflection.Emit`. Applies to Unity client and any Shared code.
- [`naming-conventions.md`](naming-conventions.md) — Namespace `ClashUp.<Tier>.<Domain>`, asmdef `ClashUp.<Domain>`, one public type per file. Applies project-wide.
