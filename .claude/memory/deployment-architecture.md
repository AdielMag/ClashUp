---
name: deployment-architecture
description: "Version-aware gateway, on-demand backend spawning, prewarm lifecycle, and mutually-exclusive CCU model for the ClashUp GCP fleet"
metadata:
  node_type: memory
  type: reference
  originSessionId: bbd46fcb-cd22-40db-833d-df585715de26
---

How the ClashUp server fleet runs on GCP. See also [[gcp-ops-gotchas]] for deploy/verify mechanics.

## Version-aware gateway (one image, two tiers)
- The SAME `clashup-gateway` image runs on BOTH the Services MIG and the GameServer MIG. They differ ONLY by config (`Gateway__Tier`, ports, `ImageRepository`, `PrewarmDiscoveredVersions`). There is NOT a separate gateway per tier.
- The gateway is a YARP proxy + `ProcessSupervisor`. It spawns versioned backend containers `clashup-{tier}:{version}` **on demand**, keyed by the `x-client-version` request header.
- `DefaultVersion=latest` routes header-less server-to-server (inter-tier) traffic.
- Unknown/missing client version on the Services tier → `FAILED_PRECONDITION` + `upgrade-client` trailer.
- Backend idle eviction: `IdleVersionTtlMinutes=30` — a backend unused for 30 min is stopped by `ProcessSupervisor.RunMaintenanceAsync`. `LastUsedUtc` is set on every `EnsureVersionAsync` (incl. prewarm at boot).

## Prewarm (GameServer only) + version-transition lifecycle
- **Why GS prewarms but Services doesn't:** Services backends spawn from the client's direct connection. GameServer has a bootstrap deadlock — the matchmaker only places a match on a GS instance already *registered* with Services, but registration is done by a backend at startup (`GameServerRegistrar` + heartbeat run inside each backend), and backends are on-demand. Prewarming one backend at boot registers the instance.
- `PrewarmDiscoveredVersions` now prewarms only the **single newest** published tag (`SelectNewestVersion` orders by `System.Version` — note `System.` prefix to avoid CS0104 collision with `Docker.DotNet.Models.Version`).
- **Prewarm runs only at gateway STARTUP.** Pushing a new image does NOT prewarm it on a running instance, and does NOT kill running backends.
- **New-version client flow:** prewarmed `1.0.1` + a `1.0.2` client → gateway spawns `gameserver:1.0.2` on demand (matchmaker's `PrepareMatch` triggers it), runs side-by-side with `1.0.1`. The client never touches the old backend. The old `1.0.1` backend idle-evicts ~30 min after its version goes unused.
- **Same-version client** uses the prewarmed backend (updates `LastUsedUtc`, keeps it warm).
- **No hard deadlock if all backends evict:** matchmaker's `ListHealthyAsync` filters by `Status=="Healthy"`, NOT heartbeat freshness. A stale instance is still selectable; the admin call spawns a fresh backend on demand (one-time cold-start). Heartbeat continuity is maintained as long as ANY backend runs (all backends heartbeat under the same instance id).
- **To make a new release warm fleet-wide immediately:** recreate the GS instances after publishing (prewarm-newest then warms the new version at boot). Otherwise the first match on a new version eats an on-demand spawn.

## Mutually-exclusive CCU model
A player counts in exactly ONE tier at a time: Services = lobby/matchmaking, GameServer = in-match.
- **Client:** `ServicesPresence` (`Core/Networking/Scripts/Services/`) holds a keepalive-pinged PingHub connection to Services while in the lobby. `Start()` on lobby entry / return-to-lobby; `Stop()` on match-enter. Wired in `GameFlowController` (CoreStarter), registered singleton in `CoreStarterLifetimeScope`. Pings every 15s, reconnects on failure.
- **Boot ping is separate:** `BootBootstrapper` calls `_pingHub.Disconnect()` after the boot readiness ping so it doesn't leak into CCU before lobby.
- **Server counters:** Services `ServicesCcuTracker` (grace = `Zero`). GameServer `GraceCcuCounter` with `CcuGracePeriodSeconds=45` (was 300) — dedups brief disconnect/reconnect.
- **Activation requires a client rebuild** (bundleVersion 1.0.1+) deployed to device.

## CCU pipeline latency (why Services CCU "took some time")
in-memory counter (instant) → `CcuMetricReporter` push interval → Cloud Monitoring ingestion lag → dashboard 5s poll. Services reporter interval lowered 30s→10s (`ClashUp.Services/Program.cs`) so lobby connects surface within ~10-20s. A code change like this only reaches clients via the versioned image THEY connect to — must be folded into the next tagged release + client rebuild, not just pushed to main.

## Dashboard (`src/Tools/ClashUp.Dashboard`)
- Delete-from-registry button: `GcpStatusService.DeleteImageVersionAsync` refuses `latest` and any version also tagged `latest`; resolves digest then `DeleteVersionAsync`. Needs SA role `roles/artifactregistry.repoAdmin` (read-only SA can't delete).
- `latest` tag is HIDDEN from registry pills (filtered in `index.html`) but the concept is kept (still pushed by CI, used as `DefaultVersion`).
- Non-semver versions show an "internal · default route" badge/tooltip (self-documenting label).
- Refresh countdown shows "auto-refresh every 5s · next in Ns".
- The running dashboard locks its `.exe`; `dotnet run -c Release` serves the COPIED wwwroot — restart to pick up `index.html`/JS changes.
