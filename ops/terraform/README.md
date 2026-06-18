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
cp terraform.tfvars.example terraform.tfvars   # fill in project, Mongo, JWT keys
terraform init

# Phase 1: registry + network + NAT + instance SA, so images can be pushed and
# the NAT IP can be allowlisted in Atlas.
terraform apply \
  -target=google_artifact_registry_repository.docker \
  -target=google_compute_subnetwork.subnet \
  -target=google_compute_router_nat.nat \
  -target=google_service_account.instance

terraform output nat_ip   # → add to MongoDB Atlas → Network Access

# Push the first images: tag a release so CI builds clashup-{services,gameserver,gateway}.
git tag v1.0.0 && git push origin v1.0.0

# Phase 2: everything else (MIGs, LB, autoscalers, monitoring).
terraform apply
```

`terraform output services_endpoint` is what clients (and the GameServer tier)
use. Point the Unity client's Services URL at it, and set the client's Bundle
Version to a pushed image tag (e.g. `1.0.0`) so `x-client-version` matches.

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
