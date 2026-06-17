# ClashUp GCP Infrastructure

Terraform for the ClashUp server fleet on GCP: one Managed Instance Group per
tier (Services, GameServer), each of identical **gateway** instances (on
Container-Optimized OS) that spawn per-version backend containers on demand.
Autoscaling is driven by CPU (80%, native), RAM (80%, via the gateway's
self-reported `custom.googleapis.com/instance/memory_utilization`), and a custom
CCU metric (GameServer). No Ops Agent — the gateway reports host memory itself.

## Architecture recap

- **Services tier** — behind an external L7 load balancer (`<lb-ip>:80`). Stateless.
- **GameServer tier** — no LB; each instance has a public IP and clients connect
  directly on `:5101` after matchmaking.
- **Versions are processes, not instances.** Each instance runs the gateway,
  which pulls `clashup-<tier>:<client-version>` from Artifact Registry on demand
  and routes by the `x-client-version` gRPC header. A version with no image →
  gRPC `FAILED_PRECONDITION` + `required-action: upgrade-client`.
- **Shipping a game version = pushing its image** (CI on a `v*.*.*` tag). No MIG
  change. Only a new *gateway* build needs `terraform apply -var gateway_image_version=<v>`.

## One-time bootstrap (manual)

```bash
PROJECT=my-clashup-project
REGION=us-central1

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

## Apply

```bash
cp terraform.tfvars.example terraform.tfvars   # fill in secrets (or use TF_VAR_*)
terraform init
terraform apply
```

`terraform output services_lb_ip` is the address clients use first.

## Rolling out a new gateway build

```bash
terraform apply -var="gateway_image_version=1.4.0"
```

Services MIG updates proactively; GameServer MIG is `OPPORTUNISTIC` (existing
instances keep their live matches and are replaced as they drain / go unhealthy).
