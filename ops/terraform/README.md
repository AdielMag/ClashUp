# ClashUp GCP Infrastructure

Terraform for the ClashUp server fleet on GCP: one Managed Instance Group per
tier (Services, GameServer), each of identical **gateway** instances (on
Container-Optimized OS) that spawn per-version backend containers on demand.
Autoscaling is driven by CPU (80%, native), RAM (80%, via the gateway's
self-reported `custom.googleapis.com/instance/memory_utilization`), and a custom
per-tier CCU metric — **each tier reports its own**: GameServer reports match
players (`custom.googleapis.com/gameserver/ccu`), Services reports live client
hub connections (`custom.googleapis.com/services/ccu`). No Ops Agent — the
gateway reports host memory itself.

## Architecture recap

- **Services tier** — behind a load balancer. By default an **external
  passthrough Network LB (L4/TCP) on `IP:5001`** carrying cleartext h2c gRPC (no
  domain needed). Set `services_domain` to switch to an **external HTTPS
  Application LB on `:443`** with a Google-managed cert. Stateless.
- **GameServer tier** — no LB; each instance has a public IP and clients connect
  directly on `:5101` after matchmaking.
- **Egress / database access** — only the **Services** tier talks to MongoDB.
  Services instances have **no external IP**: Private Google Access reaches
  Google APIs, and a Cloud NAT (with a reserved static IP) reaches MongoDB Atlas.
  The GameServer tier **never connects to Mongo** (it talks only to Services over
  gRPC), so the Atlas IP-access list should contain **only** the NAT IP
  (`terraform output nat_ip`, as a `/32`) — never `0.0.0.0/0`.
- **Versions are processes, not instances.** Each instance runs the gateway,
  which pulls `clashup-<tier>:<client-version>` on demand and routes by the
  `x-client-version` gRPC header. Unknown version → gRPC `FAILED_PRECONDITION` +
  `required-action: upgrade-client`.
- **Shipping a game version = pushing its image** (CI on a `v*.*.*` tag). No MIG
  change. Only a new *gateway* build needs `terraform apply -var gateway_image_version=<v>`.

## One-time bootstrap (manual / scripted)

```bash
PROJECT=my-clashup-project
REGION=us-central1

# 0. Enable APIs
gcloud services enable \
  compute.googleapis.com artifactregistry.googleapis.com monitoring.googleapis.com \
  logging.googleapis.com iam.googleapis.com iamcredentials.googleapis.com \
  sts.googleapis.com cloudresourcemanager.googleapis.com storage.googleapis.com \
  --project "$PROJECT"

# 1. Terraform state bucket
gsutil mb -l $REGION gs://clashup-terraform-state

# 2. Workload Identity Federation for GitHub Actions (keyless CI auth)
gcloud iam service-accounts create clashup-ci --display-name "ClashUp CI"
gcloud projects add-iam-policy-binding $PROJECT \
  --member="serviceAccount:clashup-ci@$PROJECT.iam.gserviceaccount.com" \
  --role="roles/artifactregistry.writer"
gcloud projects add-iam-policy-binding $PROJECT \
  --member="serviceAccount:clashup-ci@$PROJECT.iam.gserviceaccount.com" \
  --role="roles/compute.instanceAdmin.v1"
gcloud projects add-iam-policy-binding $PROJECT \
  --member="serviceAccount:clashup-ci@$PROJECT.iam.gserviceaccount.com" \
  --role="roles/iam.serviceAccountUser"

gcloud iam workload-identity-pools create clashup-github-pool --location=global
gcloud iam workload-identity-pools providers create-oidc clashup-github-provider \
  --location=global --workload-identity-pool=clashup-github-pool \
  --issuer-uri="https://token.actions.githubusercontent.com" \
  --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository" \
  --attribute-condition="assertion.repository=='AdielMag/ClashUp'"
gcloud iam service-accounts add-iam-policy-binding \
  clashup-ci@$PROJECT.iam.gserviceaccount.com \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/$(gcloud projects describe $PROJECT --format='value(projectNumber)')/locations/global/workloadIdentityPools/clashup-github-pool/attribute.repository/AdielMag/ClashUp"

# 3. Read-only dashboard service account (run the dashboard locally with its key)
gcloud iam service-accounts create clashup-dashboard --display-name "ClashUp dashboard (read-only)"
for ROLE in roles/compute.viewer roles/monitoring.viewer roles/artifactregistry.reader; do
  gcloud projects add-iam-policy-binding $PROJECT \
    --member="serviceAccount:clashup-dashboard@$PROJECT.iam.gserviceaccount.com" --role="$ROLE"
done
gcloud iam service-accounts keys create dashboard-sa.json \
  --iam-account=clashup-dashboard@$PROJECT.iam.gserviceaccount.com
```

### GitHub repo settings (for CI/CD)

| Kind   | Name                              | Value |
|--------|-----------------------------------|-------|
| var    | `GCP_PROJECT_ID`                  | project id |
| var    | `GCP_REGION`                      | e.g. `us-central1` |
| secret | `GCP_WORKLOAD_IDENTITY_PROVIDER`  | full provider resource name |
| secret | `GCP_SERVICE_ACCOUNT`             | `clashup-ci@<project>.iam.gserviceaccount.com` |

## Apply (two-phase — instances pull the gateway image at boot)

```bash
cp terraform.tfvars.example terraform.tfvars   # fill in project, Mongo, JWT keys, Atlas API key
terraform init

# Phase 1: registry + network + router + instance SA, so images can be pushed.
terraform apply \
  -target=google_artifact_registry_repository.docker \
  -target=google_compute_subnetwork.subnet \
  -target=google_compute_router.router \
  -target=google_service_account.instance

# Push the first images: tag a release so CI builds clashup-{services,gameserver,gateway}.
git tag v1.0.0 && git push origin v1.0.0

# Phase 2: everything else (MIGs, LB backend, autoscalers, monitoring, controller).
terraform apply

# Provision the runtime networking chain (public IP + forwarding rule + Cloud NAT)
# and allowlist the NAT IP in Atlas — the fleet-controller owns these, so trigger
# one wake after apply (dashboard Wake button, or):
curl -H "X-ClashUp-Key: $(terraform output -raw fleet_admin_key)" \
  -X POST "$(terraform output -raw fleet_controller_url)/wake"
```

Clients no longer bake a Services IP — they discover it at boot via the controller's
`/resolve` (which also wakes the fleet). Bake `fleet_controller_url` +
`fleet_resolve_key` into the client `EnvironmentConfig` (see below), and set the
client Bundle Version to a pushed image tag (e.g. `1.0.0`) so `x-client-version` matches.

## Idle networking → $0 (fleet-controller owned resources)

The MIG auto-sleep drives compute to 0, but four networking resources used to bill
~$25/mo even while asleep (an L4 forwarding rule ~$18, two static IPv4s ~$7, Cloud
NAT). To reach $0 idle, the **fleet-controller owns their full lifecycle** instead
of Terraform — it releases them on sleep and re-creates them on wake:

| Resource | Terraform | Runtime (controller) |
|---|---|---|
| `clashup-services-ip` (public IP) | ❌ | allocated on wake, released on sleep |
| `clashup-services-l4-fr` (forwarding rule) | ❌ | created on wake → `services-l4-backend` |
| `clashup-nat-ip` (NAT egress IP) | ❌ | allocated on wake, released on sleep |
| `clashup-nat` (Cloud NAT) | ❌ | added to the router on wake |
| health check, backend service, firewall, router | ✅ (durable, free) | referenced by the rule/NAT |

Because the public IP changes each wake, the client discovers it at boot via
`GET {controller}/resolve` (returns `http://IP:5001`, waking the fleet if asleep).
Because the NAT IP changes each wake, the controller re-allowlists it in **MongoDB
Atlas** via the Admin API (`atlas_*` vars) — so no static NAT IP to pin.

### One-time setup

1. **Atlas API key** — in Atlas sidebar: Project Identity & Access → Applications →
   API Keys → create a project key with role **Project Network Access Manager**
   (older Atlas UI/docs call this "Project IP Access List Admin" — same role,
   renamed). The key's own API Access List must include `0.0.0.0/0` (Cloud Run's
   egress IP isn't fixed). Put `atlas_public_key`, `atlas_private_key`,
   `atlas_project_id` (found under the ⚙️ Project Settings gear) in `terraform.tfvars`.
2. **Client config** — bake into the Unity `EnvironmentConfig` asset (Dev env):
   `controllerUrl = terraform output -raw fleet_controller_url` and
   `resolveKey = terraform output -raw fleet_resolve_key`. Rebuild + redeploy the client.
3. **Dashboard config** — set `Gcp:FleetControllerAdminKey = terraform output -raw fleet_admin_key`
   (alongside the existing `Gcp:FleetControllerUrl`).

### Migrating an EXISTING deployment (resources currently in state)

The four resources above are already in Terraform state from the old design. Hand
them to the controller WITHOUT deleting the live resources (avoids a public-IP
flap): remove them from state, then apply.

```bash
terraform state rm \
  google_compute_address.services_l4 \
  google_compute_forwarding_rule.services_l4 \
  google_compute_address.nat \
  google_compute_router_nat.nat

terraform apply    # adds controller IAM/env/keys; leaves the live resources alone
```

From then on the controller manages them: the next sleep releases them, the next
wake (or client `/resolve`) re-creates them. The security model is public Cloud Run
ingress gated in-app by shared keys — `resolveKey` for `/resolve` (bounded-cost
wake), `adminKey` for `/wake`+`/state`, `/tick` unauthenticated (only sleeps an
already-idle fleet). See `src/Tools/ClashUp.FleetController`.

## TLS (production)

Set `services_domain` (and re-apply), then point the domain's A record at
`terraform output services_lb_ip`. The managed cert provisions once DNS resolves;
the Services endpoint becomes `https://<domain>`. Until then the L4 NLB serves
plaintext gRPC — fine for bring-up, not for real players.

## Rolling out a new gateway build

```bash
terraform apply -var="gateway_image_version=1.4.0"
```

Services MIG updates proactively; GameServer MIG is `OPPORTUNISTIC` (existing
instances keep their live matches and are replaced as they drain / go unhealthy).
