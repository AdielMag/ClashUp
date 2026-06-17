# ClashUp Fleet Dashboard

A local, read-only web dashboard for the ClashUp GCP fleet. Shows, per tier:

- which instances are running and their state,
- CPU and RAM utilization per instance,
- CCU per instance, broken down by the server version each backend is running,
- and the image versions available in Artifact Registry.

It authenticates with a **read-only service account** and never mutates anything.

## Setup

1. Create the dashboard service account and download its key (see
   [`../../../ops/terraform/README.md`](../../../ops/terraform/README.md) → bootstrap).
   It needs `compute.viewer`, `monitoring.viewer`, `artifactregistry.reader`.

2. Point the dashboard at your project. Either edit `appsettings.json`:

   ```json
   "Gcp": {
     "ProjectId": "my-clashup-project",
     "Region": "us-central1",
     "CredentialsPath": "C:/path/to/dashboard-sa.json"
   }
   ```

   …or set env vars (override config): `Gcp__ProjectId`, `Gcp__Region`,
   `Gcp__CredentialsPath` (or the standard `GOOGLE_APPLICATION_CREDENTIALS`).

## Run

```bash
dotnet run --project src/Tools/ClashUp.Dashboard
```

Open <http://localhost:8080>. The page auto-refreshes every 5 seconds.

> Data sources: Compute Engine (instances/state), Cloud Monitoring
> (`custom.googleapis.com/gameserver/ccu`, CPU, and the Ops Agent RAM metric),
> and Artifact Registry (image tags). If a query fails (e.g. an API is not yet
> enabled), the dashboard shows a banner and still renders what it could fetch.
