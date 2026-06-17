#!/usr/bin/env bash
# Startup script for ClashUp GameServer-tier gateway instances.
# Installs Docker + the Ops Agent, then runs the gateway, which spawns
# per-version clashup-gameserver backends on demand. GameServer instances are
# reached directly by clients (no LB) — each backend auto-discovers this
# instance's external IP from the metadata server when it registers.
set -euo pipefail

REGISTRY_HOST="${registry_host}"
GATEWAY_IMAGE="${gateway_image}"
GAMESERVER_REPO="${gameserver_repo}"

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
# Backend env carries the Services endpoint + JWT keys. GameServer__PublicEndpoint
# is intentionally NOT set: the backend resolves this instance's external IP via
# GCE metadata so it registers a client-reachable address (the gateway port).
docker rm -f clashup-gateway >/dev/null 2>&1 || true
docker pull "$GATEWAY_IMAGE"
docker run -d --name clashup-gateway \
  --network host \
  --restart always \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e "Gateway__Tier=GameServer" \
  -e "Gateway__ListenPort=5101" \
  -e "Gateway__AdminPort=9101" \
  -e "Gateway__BackendPort=5101" \
  -e "Gateway__ImageRepository=$GAMESERVER_REPO" \
  -e "Gateway__BackendEnvironment__0=GameServer__ServicesEndpoint=${services_endpoint}" \
  -e "Gateway__BackendEnvironment__1=Jwt__EndUserSigningKey=${jwt_end_user_signing_key}" \
  -e "Gateway__BackendEnvironment__2=Jwt__InterTierSigningKey=${jwt_inter_tier_signing_key}" \
  "$GATEWAY_IMAGE"
