# ClashUp Fleet Dashboard

A local, read-only web dashboard for the ClashUp GCP fleet. Shows, per tier:

- which instances are running and their state,
- CPU and RAM utilization per instance,
- CCU per instance, broken down by the server version each backend is running,
- the image versions available in Artifact Registry,
- and a **server-uptime calendar**: awake-hours per day (the hours any instance was
  running) as a daily heatmap, with Daily / Weekly / Monthly toggles.

It authenticates with a **read-only service account**. The one exception is the
**Wake fleet** button: when the fleet has auto-slept (both MIGs scaled to 0 to save
cost), the dashboard shows a 💤 banner and the button POSTs to the Cloud Run
[fleet controller](../ClashUp.FleetController/) — which holds the compute-write
rights. The dashboard itself only needs `roles/run.invoker` on that service; it never
touches compute directly.

## Setup

`appsettings.json` is already pointed at this project (`clashup-499716` /
`us-central1`) and looks for its credentials at `dashboard-sa.json` **next to the
project** (a relative `CredentialsPath` is resolved against the content root, so it
works regardless of the launch directory). So the only thing you need locally is
the key file:

1. Create the dashboard service account and download its key as
   `src/Tools/ClashUp.Dashboard/dashboard-sa.json` (see
   [`../../../ops/terraform/README.md`](../../../ops/terraform/README.md) → bootstrap).
   It needs `compute.viewer`, `monitoring.viewer`, `artifactregistry.reader`.
   The file is **gitignored** — never commit it.

That's it. If `dashboard-sa.json` is missing, the dashboard falls back to ambient
ADC (`gcloud auth application-default login` / `GOOGLE_APPLICATION_CREDENTIALS`).

> For a different project, override via env vars (they win over `appsettings.json`):
> `Gcp__ProjectId`, `Gcp__Region`, `Gcp__CredentialsPath`.

To enable the **Wake** button, set `Gcp:FleetControllerUrl` (in `appsettings.json` or
`Gcp__FleetControllerUrl`) to the controller's Cloud Run URL — `terraform output
fleet_controller_url`. Leave it empty to hide/disable waking. The dashboard SA must
have `roles/run.invoker` on the controller (granted by Terraform).

## Run

```bash
dotnet run --project src/Tools/ClashUp.Dashboard
```

Open <http://localhost:8080>. The live view auto-refreshes every 5 seconds; the
uptime calendar refreshes every 5 minutes.

> Data sources: Compute Engine (instances/state), Cloud Monitoring (per-tier CCU
> — `custom.googleapis.com/gameserver/ccu` and `custom.googleapis.com/services/ccu`,
> native CPU, the gateway's `custom.googleapis.com/instance/memory_utilization`
> for RAM, and `compute.googleapis.com/instance/uptime` for the uptime calendar),
> and Artifact Registry (image tags). If a query fails (e.g. an API is not yet
> enabled), the dashboard shows a banner and still renders what it could fetch.

## Uptime calendar

`GET /api/uptime?from=yyyy-MM-dd&to=yyyy-MM-dd` returns per-day **awake-hours** —
the whole hours each UTC day in which any instance reported `instance/uptime`.
Because the fleet auto-sleeps to 0 instances when idle, "awake hours" is exactly
the time the fleet (and the billing meter) was running. The metric is aligned into
1-hour buckets and reduced across instances; a bucket with any sample is one awake
hour. Both `from`/`to` are optional and default to the last **~6 weeks** — the
window Cloud Monitoring retains this metric for, so older days read as `0`. For
durable multi-month/yearly history, export the metric to BigQuery and query that
instead. All days are **UTC**.
