# ClashUp Fleet Dashboard

A local, read-only web dashboard for the ClashUp GCP fleet. Shows, per tier:

- which instances are running and their state,
- CPU and RAM utilization per instance,
- CCU per instance, broken down by the server version each backend is running,
- and the image versions available in Artifact Registry.

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

Open <http://localhost:8080>. The page auto-refreshes every 5 seconds.

> Data sources: Compute Engine (instances/state), Cloud Monitoring (per-tier CCU
> — `custom.googleapis.com/gameserver/ccu` and `custom.googleapis.com/services/ccu`,
> native CPU, and the gateway's `custom.googleapis.com/instance/memory_utilization`
> for RAM), and Artifact Registry (image tags). If a query fails (e.g. an API is
> not yet enabled), the dashboard shows a banner and still renders what it could
> fetch.
