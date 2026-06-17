#!/usr/bin/env bash
# Startup script for ClashUp Services-tier gateway instances.
# Installs Docker + the Ops Agent (for RAM metrics), then runs the gateway
# container, which spawns per-version clashup-services backends on demand.
set -euo pipefail

REGISTRY_HOST="${registry_host}"
GATEWAY_IMAGE="${gateway_image}"
SERVICES_REPO="${services_repo}"

# --- Docker ---------------------------------------------------------------
if ! command -v docker >/dev/null 2>&1; then
  apt-get update -y
  apt-get install -y docker.io
  systemctl enable --now docker
fi

# --- Ops Agent (RAM / process metrics) ------------------------------------
if ! systemctl is-active --quiet google-cloud-ops-agent; then
  curl -sSO https://dl.google.com/cloudagents/add-google-cloud-ops-agent-repo.sh
  bash add-google-cloud-ops-agent-repo.sh --also-install
fi

# --- Artifact Registry auth (for pulling the gateway image) ---------------
ACCESS_TOKEN=$(curl -s -H "Metadata-Flavor: Google" \
  "http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
echo "$ACCESS_TOKEN" | docker login -u oauth2accesstoken --password-stdin "https://$REGISTRY_HOST"

# --- Run the gateway ------------------------------------------------------
docker rm -f clashup-gateway >/dev/null 2>&1 || true
docker pull "$GATEWAY_IMAGE"
docker run -d --name clashup-gateway \
  --network host \
  --restart always \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e "Gateway__Tier=Services" \
  -e "Gateway__ListenPort=5001" \
  -e "Gateway__AdminPort=9001" \
  -e "Gateway__BackendPort=5001" \
  -e "Gateway__ImageRepository=$SERVICES_REPO" \
  -e "Gateway__BackendEnvironment__0=Mongo__ConnectionString=${mongo_connection_string}" \
  -e "Gateway__BackendEnvironment__1=Jwt__EndUserSigningKey=${jwt_end_user_signing_key}" \
  -e "Gateway__BackendEnvironment__2=Jwt__InterTierSigningKey=${jwt_inter_tier_signing_key}" \
  "$GATEWAY_IMAGE"
