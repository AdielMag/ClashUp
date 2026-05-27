# Mongo Data

**When this applies:** Any code that reads from or writes to MongoDB.

## The rule

All Mongo access goes through repository classes inside `Persistence/`:

- `src/Server/ClashUp.Services/Persistence/` for Services-tier collections.
- `src/Server/ClashUp.GameServer/Persistence/` for the few lookups GameServer needs.

No `IMongoCollection<T>` or `IMongoDatabase` injection outside `Persistence/`. Services and hubs depend on `IAccountRepository`, `IMatchRepository`, `IGameServerInstanceRepository` — not on the driver.

## Why

- Tests can swap repositories without spinning up Mongo.
- Index requirements are visible at the seam where queries are written.
- Driver upgrades (Mongo 3.x → 4.x) touch one file per collection, not the whole codebase.

## Required indexes

- Every query path used in a hot code path is index-backed. Adding a new query without adding the matching index is a review blocker.
- Indexes are declared in an `IIndexInitializer` for each repository (or one global initializer that orchestrates them). It runs at startup and applies the indexes idempotently (`CreateOne` is idempotent for the same key+name).
- Specific indexes that already need to exist:
  - `accounts.deviceId` — unique. Drives `LoginWithDeviceIdAsync`.
  - `matches.players.playerId` — drives sticky-reconnect lookup (`ResolveMatchAsync`).
  - `gs_instances.lastHeartbeatAt` — drives the staleness sweeper.

## Return types

- Repositories return **domain types**, not `BsonDocument`. The mapping happens inside the repository.
- Mongo document classes have the suffix `Doc` (e.g. `AccountDoc`, `MatchDoc`) to distinguish them from domain types. See [`naming-conventions.md`](naming-conventions.md).
- Class maps / conventions registered once at startup. Avoid `[BsonElement]` attribute spam when a global convention covers it.

## Queries to avoid in hot paths

- `$lookup` across collections — denormalize or split into two queries.
- Unbounded `Find` without a limit. Always pass a `Limit` or assert a bounded result by design.
- `Skip` over large ranges for pagination — use range queries on indexed sort keys instead.

## Transactions

- Use Mongo transactions only when the consistency really requires it (multi-doc atomic match state changes, account creation flow). They're expensive; default to single-doc atomicity (Mongo's natural strong point).
