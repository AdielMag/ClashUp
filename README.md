# ClashUp

A real-time multiplayer arena game.

- **Client**: Unity 6 LTS, VContainer, UniTask, NuGetForUnity.
- **Server**: C# / .NET 8 LTS, ASP.NET Core, MagicOnion (unary gRPC + StreamingHub).
- **Physics**: AetherNet (internal package), authoritative on server, predicted on client.
- **Data**: MongoDB.
- **Auth**: JWT issued by the Services tier; phase-1 login uses a device ID stored in `PlayerPrefs`.

## Architecture

Two server tiers:

1. **Services** (`src/Server/ClashUp.Services`) — single ASP.NET Core + MagicOnion host for everything outside an active match: auth, profile, lobby, matchmaking, game-server registry.
2. **GameServer** (`src/Server/ClashUp.GameServer`) — dedicated host running N concurrent authoritative matches per process. Sticky reconnect routes a dropped player back to the same instance.

See [`/root/.claude/plans/ok-we-are-going-partitioned-kay.md`](.) or `docs/` for the full plan, and [`docs/rules/README.md`](docs/rules/README.md) for the project rules every contributor must follow.

## Repo layout

```
src/
  Shared/ClashUp.Shared/          # cross-tier contracts; dual-built for .NET + Unity
  Server/ClashUp.Server.Common/   # shared server library
  Server/ClashUp.Services/        # the Services tier
  Server/ClashUp.GameServer/      # the per-match GameServer tier
client/
  ClashUp.Unity/                  # Unity 6 project
external/
  AetherNet/                      # internal physics package (submodule)
ops/
  docker/                         # mongo + container images
docs/
  rules/                          # project rules (read these first)
```

## Prerequisites

- **.NET 8 SDK** (see `global.json` for the pinned version).
- **Docker** (for local MongoDB via `ops/docker/mongo.compose.yml`).
- **Unity 6 LTS** (`6000.0.x`).

## Getting started

```sh
# Server build (once projects exist)
dotnet build ClashUp.sln

# Local Mongo
docker compose -f ops/docker/mongo.compose.yml up -d

# Run Services
dotnet run --project src/Server/ClashUp.Services

# Run a GameServer
dotnet run --project src/Server/ClashUp.GameServer

# Unity client: open client/ClashUp.Unity in Unity 6 LTS.
```

## Package version discipline

There are two parallel package universes:

- **Server**: NuGet pinned via Central Package Management (`Directory.Packages.props`).
- **Unity client**: NuGetForUnity (`Assets/Packages/`) + UPM (`Packages/manifest.json`).

When updating MagicOnion / MessagePack / Grpc, bump both universes in lockstep. The Unity side's MessagePack and MagicOnion source generators are wired into Unity's Roslyn analyzers — see [`docs/rules/il2cpp-aot.md`](docs/rules/il2cpp-aot.md).
