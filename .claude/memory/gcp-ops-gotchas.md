---
name: gcp-ops-gotchas
description: "Operational gotchas for deploying/verifying the ClashUp GCP fleet (git auth, MIG update policies, GS health verification)"
metadata: 
  node_type: memory
  type: reference
  originSessionId: bbd46fcb-cd22-40db-833d-df585715de26
---

GCP project: `clashup-499716`, region `us-central1`. gcloud is authed as adiel12430@gmail.com.

**Git push / `gh` fail with "Invalid username or token":** the `GITHUB_TOKEN` env var is set to an invalid token and overrides the working Windows credential manager. Push with `env -u GITHUB_TOKEN -u GH_TOKEN git push origin main`. `gh` CLI has no valid token — can't query Actions run history; verify CI *results* via the Artifact Registry image timestamps instead.

**MIG update policies differ (matters when changing instance templates, e.g. machine_type):**
- Services MIG = `PROACTIVE` → recreates instances automatically on template change.
- GameServer MIG = `OPPORTUNISTIC` → does NOT auto-replace. Force it: `gcloud compute instance-groups managed recreate-instances clashup-gameserver-mig --region=us-central1 --instances=<name>`.

**Verifying a (recreated) GameServer is bootstrapped** — do NOT curl `:9101/admin/status` or `/healthz` from outside; that port isn't open to arbitrary IPs (returns connection-refused/000). Instead:
- MIG instance health: `gcloud compute instance-groups managed list-instances clashup-gameserver-mig --region=us-central1 --format="table(instance.basename(),instanceStatus,instanceHealth[0].detailedHealthState)"` → `HEALTHY` means the gateway is up.
- Prewarm+registration proof: query the `custom.googleapis.com/gameserver/ccu` series via the Monitoring REST API (gcloud has no `monitoring time-series` subcommand in this version): `curl -G https://monitoring.googleapis.com/v3/projects/clashup-499716/timeSeries -H "Authorization: Bearer $(gcloud auth print-access-token)" --data-urlencode 'filter=metric.type="custom.googleapis.com/gameserver/ccu"' --data-urlencode "interval.startTime=..." --data-urlencode "interval.endTime=..."`. A present series (even value 0) proves the prewarmed backend is running + registered.

**Recreated GameServer gets a new ephemeral external IP** — fine, clients receive it dynamically via the matchmaking [[netcode-architecture|MatchHandoff]]; no client config change needed.

**Instance sizing is intentionally minimal for pre-launch cost** (`ops/terraform/variables.tf`): Services = `e2-small`, GameServer = `e2-small` (both 2 shared/burstable vCPUs). ⚠️ **Before real player load, bump GameServer to a DEDICATED-core type (e.g. `e2-highcpu-2`)** — each match's 30Hz physics sim runs entirely on one instance, and shared cores can be throttled → tick jitter. The autoscaler adds instances (spreading matches) but can't help a single match's CPU budget. Idle fleet ≈ $25/mo (down from ~$146). Bigger lever if needed: Spot VMs for Services (stateless, low risk) — not GameServer (preemption kills live matches).

**Why GameServer prewarms but Services doesn't** (`PrewarmDiscoveredVersions`): Services backends spawn on-demand from the client's direct connection. GameServer has a bootstrap deadlock — the matchmaker can only place a match on a GS instance already *registered* with Services, but registration is done by a backend at startup, and backends are on-demand. Prewarming one backend at boot registers the instance. Cleaner long-term fix (not done): move registration to the always-on gateway so backends stay fully on-demand and prewarm can be dropped. See [[deployment-architecture]].

**CI pipelines:** `server-dev.yml` (push to main, paths `src/**`/`ops/docker/**`) pushes `:latest` only. `server-cd.yml` (tag `v*.*.*`) pushes `:<version>` + `:latest` — this is the real release path. A 1.0.0 client uses `:1.0.0` images, which `server-dev.yml` does NOT touch, so pushing to main does not change behavior for released clients — you must cut a version tag + rebuild the client. See [[deployment-architecture]].
