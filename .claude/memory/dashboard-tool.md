---
name: dashboard-tool
description: "ClashUp.Dashboard local fleet tool — GCP read clients, /api endpoints, the uptime calendar, and how to build/verify it (preview tools, Duration ambiguity, exe-lock)"
metadata: 
  node_type: memory
  type: reference
  originSessionId: dcb14720-2037-4ab6-8a44-4b21becc37f8
---

`src/Tools/ClashUp.Dashboard` is a **local-only**, read-only ASP.NET web tool — run with
`dotnet run --project src/Tools/ClashUp.Dashboard` → http://localhost:8080. It is **NOT
deployed** (no terraform, no Cloud Run). **Dashboard-only changes need NO deploy** — just
rerun it locally.

## GCP wiring
- Auth = ADC / `dashboard-sa.json` (gitignored, next to the project; relative `CredentialsPath`
  resolved against content root). SA roles: `compute.viewer`, `monitoring.viewer`,
  `artifactregistry.reader`, plus `run.invoker` on the fleet controller for the Wake button.
- Google clients in `GcpStatusService` (all `Lazy<Task<...>>`): `MetricServiceClient` (Cloud
  Monitoring), `RegionInstanceGroupManagersClient` (Compute), `ArtifactRegistryClient`,
  `CloudSchedulerClient`. Each section degrades independently → errors become entries in
  `FleetStatus.Errors`, never a 500.
- Endpoints (`Program.cs`, minimal API): `GET /api/status`, `POST /api/registry/delete`,
  `POST /api/wake`, `GET /api/uptime`.

## Uptime calendar (added 2026-06-19)
- `GET /api/uptime?from=yyyy-MM-dd&to=yyyy-MM-dd` → `UptimeCalendar { From, To, Days[] }`,
  `UptimeDay { Date, AwakeHours(0-24) }`. Both query params optional; default window =
  `today-41 .. today` (~6 weeks). `DateOnly` binds/serializes as `yyyy-MM-dd`.
- `GcpStatusService.GetUptimeCalendarAsync`: queries `compute.googleapis.com/instance/uptime`
  (`resource.type = "gce_instance"`) with `Aggregation { AlignmentPeriod=1h,
  PerSeriesAligner=AlignCount, CrossSeriesReducer=ReduceSum }`. The fleet auto-sleeps to 0
  instances when idle, so **presence of any sample in an hour bucket = one awake hour** (=
  billed running time). Buckets collected into a `HashSet<DateTime>` (dedup across series),
  grouped by **UTC** day. Older days come back 0 (Monitoring retention).
- UI (`wwwroot/index.html`): GitHub-style daily heatmap + Daily/Weekly/Monthly toggle
  (week/month are client-side rollups of the daily array). Lives in its **own `.wrap`
  container OUTSIDE `#root`** so the 5 s `/api/status` refresh doesn't wipe it. Calendar
  refetches every 5 min.
- **Why metric, not fleet-controller log events** (the originally-floated alternative): the
  `instance/uptime` metric needs no new IAM (logs would need `logging.viewer`) and no
  FleetController redeploy, reuses the existing `monitoring.viewer`, and is the literal billed
  running-time. Logs also default to 30-day retention < Monitoring's ~6 weeks. For durable
  multi-month/yearly history, export the metric to BigQuery. See [[gcp-ops-gotchas]].

## Build / verify gotchas
- **`Duration` is ambiguous** between `Google.Cloud.Compute.V1.Duration` and
  `Google.Protobuf.WellKnownTypes.Duration` (both namespaces are imported in `GcpStatusService`).
  For Monitoring `Aggregation.AlignmentPeriod`, fully-qualify
  `Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(...)`.
- **Rebuild fails MSB3027/MSB3021 (apphost.exe locked) while the dashboard is running** — the
  C# *compile* still succeeds (no `CS` errors); only the post-build copy fails. Stop the
  running process first (`Stop-Process`), then `dotnet build`.
- **Verify the running UI with the preview tools, not screenshots:** create `.claude/launch.json`
  (`runtimeExecutable: dotnet`, `runtimeArgs: ["run","--project","src/Tools/ClashUp.Dashboard"]`,
  `port: 8080`) → `preview_start` → `preview_eval` to drive (`setCalView(...)`) and read DOM
  state. `preview_screenshot` **times out** on this page (active 250 ms / 5 s `setInterval`
  loops keep the renderer busy) — fall back to `preview_eval` returning DOM counts/text.
- Build via `"/c/Program Files/dotnet/dotnet.exe" build src/Tools/ClashUp.Dashboard/ClashUp.Dashboard.csproj`.
