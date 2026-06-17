#!/usr/bin/env bash
# Startup script for ClashUp GameServer-tier gateway instances on
# Container-Optimized OS. Docker is preinstalled; no OS packages are installed.
# The gateway reports host memory itself (HostMetricsReporter), so no Ops Agent
# is needed. GameServer instances are reached directly by clients (no LB) — each
# backend auto-discovers this instance's external IP from the metadata server.
set -euo pipefail

REGISTRY_HOST="${registry_host}"
GATEWAY_IMAGE="${gateway_image}"
GAMESERVER_REPO="${gameserver_repo}"

# COS has an iptables INPUT policy of DROP (only SSH allowed by default).
# Open the gateway gRPC port + admin/health port for external traffic & health checks.
iptables -A INPUT -p tcp --dport 5101 -j ACCEPT
iptables -A INPUT -p tcp --dport 9101 -j ACCEPT

# Wait for the Docker daemon (preinstalled on COS) to be ready.
until docker info >/dev/null 2>&1; do sleep 1; done

# Authenticate to Artifact Registry with the instance service account. Use a
# writable config dir because the COS root filesystem is read-only.
export DOCKER_CONFIG=/var/lib/gateway-docker
mkdir -p "$DOCKER_CONFIG"
TOKEN=$(curl -s -H "Metadata-Flavor: Google" \
  "http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token" \
  | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)
echo "$TOKEN" | docker --config "$DOCKER_CONFIG" login -u oauth2accesstoken --password-stdin "https://$REGISTRY_HOST"

# Backend env carries the Services endpoint + JWT keys. GameServer__PublicEndpoint
# is intentionally NOT set: the backend resolves this instance's external IP via
# GCE metadata so it registers a client-reachable address (the gateway port).
# /proc is mounted so the gateway can report host (VM) memory.
docker --config "$DOCKER_CONFIG" pull "$GATEWAY_IMAGE"
docker rm -f clashup-gateway >/dev/null 2>&1 || true
docker run -d --name clashup-gateway \
  --network host \
  --restart always \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /proc:/host/proc:ro \
  -e "Gateway__Tier=GameServer" \
  -e "Gateway__ListenPort=5101" \
  -e "Gateway__AdminPort=9101" \
  -e "Gateway__BackendPort=5101" \
  -e "Gateway__ImageRepository=$GAMESERVER_REPO" \
  -e "Gateway__BackendEnvironment__0=GameServer__ServicesEndpoint=${services_endpoint}" \
  -e "Gateway__BackendEnvironment__1=Jwt__EndUserSigningKey=${jwt_end_user_signing_key}" \
  -e "Gateway__BackendEnvironment__2=Jwt__InterTierSigningKey=${jwt_inter_tier_signing_key}" \
  "$GATEWAY_IMAGE"
