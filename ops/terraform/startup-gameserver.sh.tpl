#!/usr/bin/env bash
# Startup script for ClashUp GameServer-tier gateway instances on
# Container-Optimized OS. Docker is preinstalled; no OS packages are installed.
# The gateway reports host memory itself (HostMetricsReporter), so no Ops Agent
# is needed. GameServer instances are reached directly by clients (no LB). The
# startup script resolves the external IP and passes it to backend containers.
set -euo pipefail

REGISTRY_HOST="${registry_host}"
GATEWAY_IMAGE="${gateway_image}"
GAMESERVER_REPO="${gameserver_repo}"

# COS has an iptables INPUT policy of DROP (only SSH allowed by default).
# Open the gateway gRPC port + admin/health port for external traffic & health checks.
iptables -A INPUT -p tcp --dport 5101 -j ACCEPT
iptables -A INPUT -p tcp --dport 9101 -j ACCEPT

# Retry a command until it succeeds (egress may not be ready the instant Docker
# is — without this the one-shot login/pull races network readiness and `set -e`
# aborts the whole script, leaving the gateway unstarted).
retry() {
  local n=0 max=30
  until "$@"; do
    n=$((n + 1))
    if [ "$n" -ge "$max" ]; then
      echo "FATAL: command failed after $max attempts: $*" >&2
      return 1
    fi
    echo "retry $n/$max: $*" >&2
    sleep 5
  done
}

# Wait for the Docker daemon (preinstalled on COS) to be ready.
until docker info >/dev/null 2>&1; do sleep 1; done

# Authenticate to Artifact Registry with the instance service account. Use a
# writable config dir because the COS root filesystem is read-only.
export DOCKER_CONFIG=/var/lib/gateway-docker
mkdir -p "$DOCKER_CONFIG"
ar_login() {
  local token
  token=$(curl -s -H "Metadata-Flavor: Google" \
    "http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token" \
    | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)
  [ -n "$token" ] || return 1
  echo "$token" | docker --config "$DOCKER_CONFIG" login -u oauth2accesstoken --password-stdin "https://$REGISTRY_HOST"
}
retry ar_login

# Resolve this instance's external IP from the GCE metadata server (host level,
# guaranteed to work). Backend containers on Docker bridge may not reliably reach
# metadata, so we pass the resolved endpoint explicitly.
EXTERNAL_IP=$(curl -s -H "Metadata-Flavor: Google" \
  "http://metadata.google.internal/computeMetadata/v1/instance/network-interfaces/0/access-configs/0/external-ip")
if [ -z "$EXTERNAL_IP" ]; then
  echo "FATAL: Could not resolve external IP from GCE metadata" >&2
  exit 1
fi
PUBLIC_ENDPOINT="http://$EXTERNAL_IP:5101"

# /proc is mounted so the gateway can report host (VM) memory.
retry docker --config "$DOCKER_CONFIG" pull "$GATEWAY_IMAGE"
docker rm -f clashup-gateway >/dev/null 2>&1 || true
docker run -d --name clashup-gateway \
  --network host \
  --restart always \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /proc:/host/proc:ro \
  -e "DOTNET_EnableWriteXorExecute=0" \
  -e "Gateway__Tier=GameServer" \
  -e "Gateway__ListenPort=5101" \
  -e "Gateway__AdminPort=9101" \
  -e "Gateway__BackendPort=5101" \
  -e "Gateway__ImageRepository=$GAMESERVER_REPO" \
  -e "Gateway__PrewarmDiscoveredVersions=true" \
  -e "Gateway__BackendEnvironment__0=GameServer__ServicesEndpoint=${services_endpoint}" \
  -e "Gateway__BackendEnvironment__1=Jwt__EndUserSigningKey=${jwt_end_user_signing_key}" \
  -e "Gateway__BackendEnvironment__2=Jwt__InterTierSigningKey=${jwt_inter_tier_signing_key}" \
  -e "Gateway__BackendEnvironment__3=GameServer__PublicEndpoint=$PUBLIC_ENDPOINT" \
  "$GATEWAY_IMAGE"
