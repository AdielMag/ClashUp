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

## Idle fleet auto-sleep (cost optimization)
- **Why:** MIG autoscalers can't scale below 1 (`min_replicas=1`), so 2× e2-small run 24/7 (~$25/mo idle). No native scale-to-zero for Compute MIGs.
- **Mechanism:** "sleep" = set autoscaler `mode=OFF` (else it re-creates the min) THEN resize MIG to 0. "wake" = set autoscaler `mode=ON` ONLY — the autoscaler restores `min_replicas` itself.
- **GOTCHA (fixed in a9e637c):** you CANNOT manually resize a MIG whose autoscaler is `mode=ON` — Compute throws `FAILED_PRECONDITION "Resizing of autoscaled regional managed instance groups is not allowed"`. So wake must NOT resize; it only flips mode ON. The original wake did `mode=ON` then `resize(1)` → 500, aborting mid-loop (Services recovered via its autoscaler, GameServer stuck at 0, scheduler left PAUSED). `SetAutoscalerModeAsync` GETs the full autoscaler, sets `.AutoscalingPolicy.Mode`, `UpdateAsync` (full PUT, preserves metrics), and **awaits the operation to completion** (`op.PollUntilCompletedAsync()`) so sleep's resize sees mode=OFF. Idempotent (no-op if already in target mode).
- **Manual recovery if a tier is stuck OFF/0:** REST GET the autoscaler, set `autoscalingPolicy.mode="ON"`, PUT back (`PUT .../regions/{r}/autoscalers?autoscaler={name}`) — preserves the policy/metrics. Don't use `gcloud ... set-autoscaling` (it REPLACES the policy, dropping the CPU/RAM/CCU metrics). Terraform can't fix mode either (the `ignore_changes=[mode]` guard blocks it). Then resume the scheduler.
- **Cloud Run doesn't auto-pull `:latest`** — after CI pushes a new controller image, roll a new revision: `gcloud run services update clashup-fleet-controller --region us-central1 --image ...@sha256:<digest>` (pin the digest to be sure). `terraform apply` sees no diff (same `:latest` string) so it won't redeploy.
- **Controller:** `src/Tools/ClashUp.FleetController` (new) — minimal .NET API on **Cloud Run** (scales to zero). Image `clashup-fleet-controller:latest`, built by `.github/workflows/fleet-controller.yml` (separate from server images; server workflows exclude `src/Tools/**`). Endpoints: `POST /tick` (idle check → sleep + pause scheduler), `POST /wake` (mode ON + resize + resume scheduler), `GET /state`, `GET /healthz`. Uses `RegionInstanceGroupManagersClient.ResizeAsync` + `RegionAutoscalersClient` (GET→set `.AutoscalingPolicy.Mode` string "ON"/"OFF"→`UpdateAsync`) + `CloudSchedulerClient.Pause/ResumeJobAsync` + Monitoring CCU query.
- **Cron:** Cloud Scheduler `clashup-idle-check` every 30 min → OIDC POST `/tick`. Controller **pauses** the job on sleep (no point polling a dead fleet) and the dashboard **resumes** it on wake. `lifecycle ignore_changes=[paused]` so apply doesn't fight runtime state.
- **Idle signal:** both CCU metrics (`gameserver/ccu` + `services/ccu`) read 0 across a 35-min lookback window (> the 30-min cadence = hysteresis; a between-matches dip can't trigger sleep). Lobby presence keeps Services CCU>0, so a connected player blocks sleep.
- **BOTH tiers sleep** (user chose max savings). Tradeoff: a fully-asleep fleet can't auto-wake a player (nothing listening) — wake is manual via dashboard. If real players ever expected, revisit keeping Services at min 1 as a doorman.
- **Drift guard:** both `google_compute_region_autoscaler` have `lifecycle ignore_changes=[autoscaling_policy[0].mode]` so `terraform apply` won't revert a slept fleet to ON.
- **Perms split:** controller SA `clashup-fleet-controller` holds `compute.instanceAdmin.v1` + `cloudscheduler.admin` + `monitoring.viewer`, PLUS `compute.networkAdmin` + `compute.loadBalancerAdmin` (for the networking teardown, see below). Dashboard SA gets `cloudscheduler.viewer` for the countdown.
- **Terraform:** `ops/terraform/fleet-controller.tf` (Cloud Run + SA + IAM + scheduler + invoker + `run`/`cloudscheduler` API enablement). Output `fleet_controller_url` → dashboard `Gcp:FleetControllerUrl`.

## Idle networking teardown → $0 (extends auto-sleep)
- **Why:** even with MIGs at 0, four networking resources billed ~$25/mo idle: the L4 forwarding rule (~$18, bills with 0 backends), two static IPv4s (~$3.65 each, in-use or not), Cloud NAT. Auto-sleep only zeroed compute. To hit $0, sleep must DELETE these and wake RE-CREATE them. See [[gcp-ops-gotchas]].
- **Ownership handoff:** these 4 resources moved OUT of Terraform to fleet-controller ownership (TF can't ignore-away a deleted resource): `clashup-services-ip`, `clashup-services-l4-fr`, `clashup-nat-ip`, `clashup-nat`. TF keeps the durable/free pieces (health check, backend service, firewall, router). Migrate an existing deploy with `terraform state rm` (the 4) then `apply` — doesn't touch the live resources, hands them over live.
- **`FleetManager.SleepAsync`** (after resize 0): delete forwarding rule → release Services IP → remove NAT from router → release NAT IP. **`WakeAsync`** (after autoscalers ON): allocate Services IP → create forwarding rule → existing backend → allocate NAT IP → add NAT to router → Atlas allowlist. All get-then-act idempotent; a `SemaphoreSlim(1,1)` serializes (Cloud Run max 1 instance).
- **Compute V1 gotchas:** `Address` proto's IP field is `.Address_` (renamed — collides with the type name). Cloud NAT is a nested `Router.Nats` field, not a top-level resource → GET router, mutate `.Nats`, full `RoutersClient.UpdateAsync` (PUT preserves immutable `network`). NAT `NatIps` wants the address `.SelfLink`; forwarding rule `IPAddress` takes the literal IP string.
- **Client discovers the IP** (it's no longer stable): `EnvironmentConfig` Dev env holds `controllerUrl` + `resolveKey` (from `fleet_controller_url` / `fleet_resolve_key` outputs), NOT a Services IP. `BootBootstrapper` → `ServicesEndpointResolver.ResolveAsync` GETs `{controller}/resolve` (UnityWebRequest + `X-ClashUp-Key`), which wakes the fleet and returns `http://IP:5001`; existing ping-retry loop covers the ~30-60s cold start. `RequiresDiscovery` = Dev && controllerUrl set. **Needs a client rebuild.** Static envs (local/emulator/tailscale) unchanged.
- **NAT IP ↔ Atlas:** the NAT egress IP is allowlisted in MongoDB Atlas; since it's re-allocated each wake, `AtlasAccessListClient` (Admin API v2, HTTP Digest with `atlas_public_key`/`atlas_private_key`, media-type `application/vnd.atlas.2023-11-15+json`) adds the fresh IP then prunes stale entries stamped with our comment. New TF vars `atlas_*`; create a Project-IP-Access-List-Admin API key in Atlas.
- **Security model changed:** Cloud Run is now **public ingress (`allUsers` run.invoker)** because the client hits `/resolve` before it has any token. Routes are gated IN-APP by shared keys (Program.cs `X-ClashUp-Key`): `ResolveKey` (client, bounded-cost wake — worst case keeps fleet awake, idle check reaps it), `AdminKey` (dashboard `/wake`+`/state`), `/tick` unauthenticated (only sleeps an idle fleet). Keys are `random_password`, surfaced via sensitive outputs `fleet_resolve_key` / `fleet_admin_key`. Dashboard `WakeFleetAsync` now sends `AdminKey` (was OIDC) — set `Gcp:FleetControllerAdminKey`.
- **First bring-up:** after `apply`, the networking chain doesn't exist yet (controller creates it on wake) → instances can't reach Mongo until a wake provisions NAT + Atlas. Trigger one `POST /wake` (dashboard button or curl with `AdminKey`).
- **Deploy ordering:** controller image MUST exist in Artifact Registry BEFORE `terraform apply` creates the Cloud Run service (else pull fails). Push to main → CI builds image → apply.
- **Packages:** added `Google.Cloud.Scheduler.V1` 3.6.0 (latest; NOT 3.7.0) + `Google.Apis.Auth` 1.72.0 (must match transitive from Google.Cloud libs — 1.69 caused NU1605 downgrade) to `Directory.Packages.props`.

## Dashboard (`src/Tools/ClashUp.Dashboard`)
- **Wake button + ASLEEP banner:** `FleetStatus.Asleep` (both MIGs `TargetSize==0` via `QueryAsleepAsync`). `index.html` shows 💤 banner + Wake button when asleep → `POST /api/wake` → `GcpStatusService.WakeFleetAsync` calls the controller (see auto-sleep section). Button latches "waking…" until a refresh sees instances back.
- Delete-from-registry button: `GcpStatusService.DeleteImageVersionAsync` refuses `latest` and any version also tagged `latest`; resolves digest then `DeleteVersionAsync`. Needs SA role `roles/artifactregistry.repoAdmin` (read-only SA can't delete).
- `latest` tag is HIDDEN from registry pills (filtered in `index.html`) but the concept is kept (still pushed by CI, used as `DefaultVersion`).
- Non-semver versions show an "internal · default route" badge/tooltip (self-documenting label).
- Refresh countdown shows "auto-refresh every 5s · next in Ns".
- The running dashboard locks its `.exe`; `dotnet run -c Release` serves the COPIED wwwroot — restart to pick up `index.html`/JS changes.
