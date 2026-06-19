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

**Idle fleet auto-sleep is LIVE** (Cloud Run controller + Cloud Scheduler) — see [[deployment-architecture]] for the full design + the wake gotcha (can't resize an ON MIG). Key ops facts:
- **Wake ≠ instant.** After wake (autoscalers→ON), Services shows RUNNING in ~1 min but GameServer takes ~2-4 min (COS boot → pull gateway → prewarm → register). During that window the dashboard shows NO GameServer backend/CCU even though the MIG is already scaling — this LOOKS like "only services woke" but is just cold-start latency, NOT a bug. Confirm with `gcloud compute instance-groups managed list-instances clashup-gameserver-mig --region=us-central1` (instanceStatus RUNNING) + autoscaler mode=ON.
- **Roll the controller after a new image:** Cloud Run doesn't auto-pull `:latest`. `gcloud run services update clashup-fleet-controller --region us-central1 --image ...@sha256:<digest>` (pin digest). Preserves env/SA. `terraform apply` won't redeploy (same `:latest` string).
- **Test an authenticated Cloud Run endpoint locally** (your user isn't a `run.invoker`; impersonation fails without Token Creator): activate the invoker SA key then mint an audience-scoped ID token — `gcloud auth activate-service-account --key-file=src/Tools/ClashUp.Dashboard/dashboard-sa.json; TOKEN=$(gcloud auth print-identity-token --audiences=<service-url>); curl -X POST -H "Content-Length: 0" -H "Authorization: Bearer $TOKEN" <url>/wake` then `gcloud config set account adiel12430@gmail.com`. NOTE: a bodyless POST gets `411 Length Required` from Google's frontend — always send `Content-Length: 0` (the dashboard's HttpClient does this automatically).

**Git Bash `/tmp` ≠ Windows Python paths:** when a Bash redirect writes `> /tmp/x.json` and you then read it with the Windows `python` (Python310), python resolves `/tmp/...` as `C:\tmp\...` → FileNotFound. Use a shared absolute path like `C:/Users/Adiel/AppData/Local/Temp/x.json` for files passed between Git Bash and Windows Python.

**Auto-mode classifier blocks prod IAM/infra changes without explicit user auth:** a `terraform apply -auto-approve` that creates an IAM binding (e.g. granting a role to an SA), or recreating shared instances, is denied unless the user explicitly authorized THAT action in-session. Show the `terraform plan` and confirm with the user (AskUserQuestion) before applying IAM/infra changes — a feature *question* is not authorization to deploy.

**CI pipelines:** `server-dev.yml` (push to main, paths `src/**`/`ops/docker/**`) pushes `:latest` only. `server-cd.yml` (tag `v*.*.*`) pushes `:<version>` + `:latest` — this is the real release path. A 1.0.0 client uses `:1.0.0` images, which `server-dev.yml` does NOT touch, so pushing to main does not change behavior for released clients — you must cut a version tag + rebuild the client. See [[deployment-architecture]].
