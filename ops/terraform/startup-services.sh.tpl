#!/usr/bin/env bash
# Startup script for ClashUp Services-tier gateway instances on
# Container-Optimized OS. Docker is preinstalled; no OS packages are installed.
# The gateway reports host memory itself (HostMetricsReporter), so no Ops Agent
# is needed. The gateway spawns clashup-services version backends on demand.
set -euo pipefail

REGISTRY_HOST="${registry_host}"
GATEWAY_IMAGE="${gateway_image}"
SERVICES_REPO="${services_repo}"

# COS has an iptables INPUT policy of DROP (only SSH allowed by default).
# Open the gateway gRPC port + admin/health port for external traffic & health checks.
iptables -A INPUT -p tcp --dport 5001 -j ACCEPT
iptables -A INPUT -p tcp --dport 9001 -j ACCEPT

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

# Run the gateway. /proc is mounted so it can report host (VM) memory.
docker --config "$DOCKER_CONFIG" pull "$GATEWAY_IMAGE"
docker rm -f clashup-gateway >/dev/null 2>&1 || true
docker run -d --name clashup-gateway \
  --network host \
  --restart always \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /proc:/host/proc:ro \
  -e "Gateway__Tier=Services" \
  -e "Gateway__ListenPort=5001" \
  -e "Gateway__AdminPort=9001" \
  -e "Gateway__BackendPort=5001" \
  -e "Gateway__ImageRepository=$SERVICES_REPO" \
  -e "Gateway__BackendEnvironment__0=Mongo__ConnectionString=${mongo_connection_string}" \
  -e "Gateway__BackendEnvironment__1=Jwt__EndUserSigningKey=${jwt_end_user_signing_key}" \
  -e "Gateway__BackendEnvironment__2=Jwt__InterTierSigningKey=${jwt_inter_tier_signing_key}" \
  "$GATEWAY_IMAGE"
