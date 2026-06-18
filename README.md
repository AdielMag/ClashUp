# ClashUp

A real-time multiplayer arena game — Unity client, server-authoritative C# backend,
deployed on GCP as an autoscaling, version-aware fleet.

| Layer | Tech |
|-------|------|
| **Client** | Unity 6 LTS · VContainer (DI) · UniTask (async) · NuGetForUnity |
| **Server** | C# / .NET 8 · ASP.NET Core · MagicOnion 7 (unary gRPC + StreamingHub) |
| **Physics** | AetherNet (internal package) — deterministic 2D, authoritative on server, predicted on client |
| **Data** | MongoDB (Atlas in prod) |
| **Auth** | JWT issued by the Services tier; phase-1 login = device ID in `PlayerPrefs` |
| **Infra** | GCP — regional MIGs on Container-Optimized OS, Cloud NAT, Artifact Registry, Cloud Monitoring; Terraform + GitHub Actions |

---

## Table of contents

1. [Core principles](#core-principles)
2. [Architecture at a glance](#architecture-at-a-glance)
3. [The version-aware gateway](#the-version-aware-gateway)
4. [Match → instance routing](#match--instance-routing)
5. [Netcode & physics](#netcode--physics)
6. [Autoscaling & CCU](#autoscaling--ccu)
7. [Repo layout](#repo-layout)
8. [Local development](#local-development)
9. [Deployment (GCP)](#deployment-gcp)
10. [Monitoring](#monitoring)
11. [Configuration & secrets](#configuration--secrets)
12. [Security](#security)
13. [Further reading](#further-reading)

---

## Core principles

- **Server-authoritative.** The server is the single source of truth for all game
  state. Match start/end, scoring, damage, respawns — all decided server-side.
- **Dumb client.** The client is a thin display layer. It renders what the server
  tells it and never synthesizes authoritative state (e.g. it never decides a
  match ended — it waits for the server's `OnMatchEnded`).
- **Versions are processes, not machines.** A game version is a container image,
  not a dedicated VM. One fleet serves every live client version simultaneously.

---

## Architecture at a glance

```
                 ┌──────────────────────────────────────────────┐
   Unity client  │                    GCP                        │
   ───────────►  │                                               │
   gRPC (h2c)    │   Services tier (regional MIG, behind LB)     │
   x-client-     │   ┌─────────────────────────────────────┐     │
   version: 1.2  │   │ gateway  →  clashup-services:1.2 ─┐  │     │
                 │   │          →  clashup-services:1.1 ─┼──┼──► MongoDB Atlas
                 │   └─────────────────────────────────────┘     │   (via Cloud NAT,
                 │              │ matchmaking assigns a GS         │    static IP only)
                 │              ▼                                  │
                 │   GameServer tier (regional MIG, public IPs)   │
   client connects│  ┌─────────────────────────────────────┐     │
   DIRECTLY to the│  │ gateway  →  clashup-gameserver:1.2   │     │
   assigned GS ──►│  │ (one instance hosts N live matches)  │     │
                 │   └─────────────────────────────────────┘     │
                 └──────────────────────────────────────────────┘
```

Two server tiers, each a **regional Managed Instance Group** of identical
**gateway** instances running on Container-Optimized OS:

1. **Services** (`src/Server/ClashUp.Services`) — everything outside an active
   match: auth, profile, lobby, matchmaking, game-server registry, config.
   Stateless; reached through a load balancer. **The only tier that talks to
   MongoDB.**
2. **GameServer** (`src/Server/ClashUp.GameServer`) — runs N concurrent
   authoritative matches per process. No load balancer: each instance has a
   public IP and clients connect to it **directly** after matchmaking. Talks to
   Services over gRPC; never touches the database.

Supporting projects:

- **`ClashUp.Server.Common`** — shared server library: JWT auth, Mongo context,
  GCE metadata helpers, interceptors, and the reusable **CCU reporting**
  infrastructure (`Ccu/` — `ICcuSource`, `GraceCcuCounter`, `CcuMetricReporter`).
- **`ClashUp.Gateway`** — the version-aware reverse proxy that fronts both tiers
  (see below).
- **`ClashUp.Shared`** — cross-tier wire contracts (hubs, MessagePack DTOs), also
  consumed by the Unity client as a local package.

---

## The version-aware gateway

Every fleet instance runs **one gateway container** (`ClashUp.Gateway`,
`--network host`). The gateway does **not** contain game logic — it is a reverse
proxy + process supervisor:

1. A client opens a gRPC connection and sends its build version in the
   **`x-client-version`** header (the Unity `Application.version` / Bundle
   Version).
2. The gateway's **`ProcessSupervisor`** ensures a backend container for that
   exact version is running — pulling `clashup-<tier>:<version>` from Artifact
   Registry on demand, port-mapped to `127.0.0.1:<random>` on the Docker bridge.
3. The gateway routes the call to that backend. Idle versions are reaped after a
   TTL; the next request respawns them.
4. **Unknown / unavailable version** → gRPC `FAILED_PRECONDITION` with
   `required-action: upgrade-client`. Old clients are told to update rather than
   silently breaking.

**Consequences:**

- **Shipping a game version = pushing its image.** Tag `v1.3.0`, CI builds and
  pushes `clashup-{services,gameserver,gateway}:1.3.0`. No infra change — the
  running fleet serves the new version the moment a client of that version
  connects.
- **Only a new *gateway* build needs a Terraform apply**
  (`-var gateway_image_version=<v>`), because the gateway image is baked into the
  instance template. Game-version images are never named in Terraform.
- One fleet hosts **all live versions at once**, so a staged client rollout needs
  no parallel infrastructure.

---

## Match → instance routing

Because a GameServer instance holds all of a match's state in memory, **every
player in a match must land on the same instance.** The chain that guarantees
this:

1. **Matchmaker** (`ClashUp.Services/Matchmaking/Matchmaker.cs`) picks the
   least-loaded healthy GS, and writes `GsInstanceId` + `GsEndpoint`
   (`http://<instance-public-ip>:5101`) into the match document.
2. On join/reconnect, **Services issues a `MatchToken`** (JWT) carrying a
   `gsInstanceId` claim, and hands the client that instance's `GsEndpoint`.
3. The client connects **directly** to that endpoint.
4. **`MatchHub.JoinAsync`** (GameServer) validates the token's `gsInstanceId`
   against its own identity and **rejects** a token minted for a different
   instance — a stray client can never join the wrong process.
5. The instance learns its own public address at boot: the startup script reads
   the external IP from the GCE metadata server and passes it to the backend as
   `GameServer__PublicEndpoint`, so the address it registers with Services is the
   one clients can actually reach.

Sticky reconnect uses the same `gsInstanceId`, so a dropped player returns to the
same match on the same instance.

---

## Netcode & physics

- **Physics:** AetherNet (deterministic 2D over a Box2D port). The same
  `MatchPhysicsWorld` runs on server (authority) and client (prediction).
- **Local player:** client-side prediction + server reconciliation, keyed by
  `LastProcessedInputSeq`. Rendered with sub-tick interpolation between 30 Hz
  fixed steps.
- **Remote players:** pure entity interpolation from buffered snapshots, rendered
  ~66 ms in the past — not run through the local physics world.
- **Wire protocol:** `InputCommand` up; `SnapshotPacket → WorldStatePacket →
  PlayerStateDto` down.

Deeper notes live in [`docs/`](docs/) and the project memory files.

---

## Autoscaling & CCU

Each MIG autoscales on three signals, and **each tier reports its own CCU**:

| Signal | Source | Services target | GameServer target |
|--------|--------|-----------------|-------------------|
| CPU | native | 80% | 80% |
| RAM | gateway's `HostMetricsReporter` → `custom.googleapis.com/instance/memory_utilization` | 80% | 80% |
| **CCU** | per-tier `CcuMetricReporter` | `custom.googleapis.com/services/ccu` (live hub connections) · target 500/inst | `custom.googleapis.com/gameserver/ccu` (match players) · target 100/inst |

CCU is implemented once in `ClashUp.Server.Common/Ccu/`:

- **`GraceCcuCounter`** — thread-safe counter with a disconnect grace window
  (reconnect within the window stays counted).
- **`CcuMetricReporter`** — `BackgroundService` that pushes an `ICcuSource`'s
  count to Cloud Monitoring under a tier-supplied metric type, labelled by server
  version. No-ops off-GCE.
- Each tier provides its own `ICcuSource`: GameServer counts **match players**
  (`CcuTracker`, 5-min grace); Services counts **live client hub connections**
  (`ServicesCcuTracker`, keyed by hub connection id).

No Ops Agent is required — the gateway self-reports host memory, so instances run
on the minimal Container-Optimized OS image.

---

## Repo layout

```
src/
  Shared/ClashUp.Shared/          # cross-tier wire contracts; dual-built for .NET + Unity
  Server/
    ClashUp.Server.Common/        # shared server lib (auth, Mongo, GCE, CCU reporting)
    ClashUp.Gateway/              # version-aware reverse proxy + process supervisor
    ClashUp.Services/             # Services tier (auth, lobby, matchmaking, registry)
    ClashUp.GameServer/           # per-match authoritative tier
    ClashUp.Server.sln            # server-only solution
  Tools/
    ClashUp.Dashboard/            # local read-only fleet dashboard
client/
  ClashUp.Unity/                  # Unity 6 project
external/
  AetherNet/                      # internal physics package (gitignored clone)
ops/
  docker/                         # Dockerfiles + compose (mongo, services, gameserver, gateway)
  terraform/                      # GCP infrastructure (see ops/terraform/README.md)
.github/workflows/                # server-ci / server-cd / server-dev
docs/
  GDD.md, rules/                  # design doc + contributor rules (read these first)
ClashUp.sln                       # full solution (server + Shared)
```

---

## Local development

**Prerequisites:** .NET 8 SDK (`global.json` pins the version), Docker, Unity 6
LTS (`6000.0.x`).

```sh
# Local Mongo
docker compose -f ops/docker/mongo.compose.yml up -d

# Build everything
dotnet build ClashUp.sln

# Run the tiers (separate terminals)
dotnet run --project src/Server/ClashUp.Services      # :5001 gRPC, :9001 admin
dotnet run --project src/Server/ClashUp.GameServer    # :5101 gRPC, :9101 admin

# Or the whole stack (mongo + both tiers, behind gateways) in containers:
docker compose -f ops/docker/docker-compose.yml up --build

# Unity client: open client/ClashUp.Unity in Unity 6 LTS.
```

Off-GCE, all cloud integrations degrade gracefully: the GCE metadata helpers
return null, CCU reporting disables itself, and public-endpoint resolution falls
back to the configured value. You can run and play locally with nothing but Mongo.

**Phone / device testing** uses Tailscale or `adb reverse` — see the project
memory (`dev-environment.md`) and `EnvironmentConfig` in the Unity client.

---

## Deployment (GCP)

Full runbook: **[`ops/terraform/README.md`](ops/terraform/README.md)**. In short:

1. One-time bootstrap — enable APIs, state bucket, Workload Identity Federation
   for keyless CI, a read-only dashboard SA, and GitHub repo vars/secrets.
2. **Two-phase apply** (instances pull the gateway image at boot):
   - Phase 1: registry + network + NAT + instance SA → push first images →
     allowlist the NAT IP in Atlas.
   - Phase 2: MIGs, load balancer, autoscalers, monitoring descriptors.
3. Point the Unity client's Services URL at `terraform output services_endpoint`
   and set its Bundle Version to a pushed image tag so `x-client-version` matches.

**CI/CD** (`.github/workflows/`):

- `server-ci.yml` — build + test on PRs.
- `server-cd.yml` — on a `v*.*.*` tag, build and push
  `clashup-{services,gameserver,gateway}:<version>` (+ `:latest`) to Artifact
  Registry via WIF (no long-lived keys).
- `server-dev.yml` — dev/preview builds.

**Transport:** by default the Services LB is an external **passthrough Network LB
(L4/TCP)** carrying cleartext h2c gRPC — fine for bring-up. Set `services_domain`
to switch to an **HTTPS Application LB** with a Google-managed certificate.

---

## Monitoring

`src/Tools/ClashUp.Dashboard` is a local ASP.NET dashboard (run with the
read-only `dashboard-sa.json`) showing, per tier and instance:

- versions running / available (Artifact Registry tags),
- **CCU per instance, broken down by server version** (both tiers' CCU series),
- CPU and RAM per instance.

```sh
dotnet run --project src/Tools/ClashUp.Dashboard   # needs dashboard-sa.json
```

---

## Configuration & secrets

- **Server config** is environment-driven (`Mongo__ConnectionString`,
  `Jwt__EndUserSigningKey`, `Jwt__InterTierSigningKey`,
  `GameServer__ServicesEndpoint`, …). In the fleet these are injected into version
  containers by the gateway via `Gateway__BackendEnvironment__N`, themselves set
  by the instance startup script from Terraform variables.
- **Terraform secrets** live only in gitignored `terraform.tfvars` (Mongo string,
  JWT keys) — never committed. Prefer `TF_VAR_*` env or Secret Manager for real
  deployments.
- **Package versions** live in two universes that must move in lockstep: server
  NuGet via Central Package Management (`Directory.Packages.props`), Unity via
  NuGetForUnity + UPM (`Packages/manifest.json`). See
  [`docs/rules/il2cpp-aot.md`](docs/rules/il2cpp-aot.md).

---

## Security

- **Database access is locked to one IP.** Only the Services tier connects to
  MongoDB, and it has **no external IP** — its egress to Atlas is NATed through a
  single reserved static IP. The GameServer tier never connects to Mongo. So the
  **Atlas IP-access list contains only the NAT IP as a `/32`**
  (`terraform output nat_ip`) — **never `0.0.0.0/0`**.
- **JWT keys are real secrets** supplied via tfvars, not the dev placeholders.
- **Plaintext bring-up vs TLS.** The default L4 NLB exposes gRPC (including JWTs)
  in cleartext — acceptable for bring-up only. Flip to TLS via `services_domain`
  before real players. GameServer match traffic is plaintext h2c by design
  (direct, low-latency, ephemeral IPs); hardening it is a separate follow-up.
- **No public SSH** — instances are reached via Identity-Aware Proxy only.

---

## Further reading

- [`docs/rules/`](docs/rules/) — contributor rules (read first).
- [`docs/GDD.md`](docs/GDD.md) — game design doc.
- [`ops/terraform/README.md`](ops/terraform/README.md) — full infra runbook.
- [`src/Tools/ClashUp.Dashboard/README.md`](src/Tools/ClashUp.Dashboard/README.md) — dashboard setup.
