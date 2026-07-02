variable "project_id" {
  type        = string
  description = "GCP project id."
}

variable "region" {
  type        = string
  description = "Region for the regional MIGs, subnet, and Artifact Registry."
  default     = "us-central1"
}

variable "gateway_image_version" {
  type        = string
  description = "Tag of the clashup-gateway image baked into the instance templates. Game-version images are pulled on demand by the supervisor and are NOT set here."
  default     = "latest"
}

variable "services_domain" {
  type        = string
  description = "Domain for the Services tier. Empty = external passthrough L4 NLB on IP:5001 (plaintext h2c gRPC, no domain). Set = external HTTPS Application LB on :443 with a Google-managed cert (point the domain's A record at the LB IP after apply)."
  default     = ""
}

# --- Machine sizing ---------------------------------------------------------
# The fleet is idle most of the time and each MIG keeps >=1 instance always on,
# so the always-on box size is the dominant idle cost. We run small instances and
# let the autoscaler add MORE instances under load (GCP autoscalers scale
# horizontally — instance count — not up to bigger machine types). Sizes are
# variables so they're trivial to bump if an instance runs hot or OOMs.

variable "services_machine_type" {
  type        = string
  description = "Services-tier instance size. Lobby/matchmaking/auth is light and not latency-critical, so a small shared-core box is fine. Bump to e2-medium if multi-version windows (two backends + gateway) pressure the 2GB."
  default     = "e2-small"
}

variable "gameserver_machine_type" {
  type        = string
  description = "GameServer-tier instance size. e2-small (2 shared vCPUs, 0.5 sustained baseline, 2GB) is the cheapest practical size for pre-launch. NOTE: shared cores can be throttled under sustained load, which jitters the 30Hz sim — each match runs on ONE instance, so bump this to a DEDICATED-core type (e.g. e2-highcpu-2) before real player load."
  default     = "e2-small"
}

# --- Autoscaling bounds -----------------------------------------------------

variable "services_min_instances" {
  type    = number
  default = 1
}

variable "services_max_instances" {
  type    = number
  default = 5
}

variable "gameserver_min_instances" {
  type    = number
  default = 1
}

variable "gameserver_max_instances" {
  type    = number
  default = 10
}

variable "cpu_target_utilization" {
  type        = number
  description = "Target CPU utilization (0-1) for autoscaling. 0.8 = 80%."
  default     = 0.8
}

variable "ram_target_utilization" {
  type        = number
  description = "Target RAM utilization (0-1) via the Ops Agent memory metric. 0.8 = 80%."
  default     = 0.8
}

variable "gameserver_ccu_per_instance_target" {
  type        = number
  description = "Target concurrent users (match players) per GameServer instance before scaling out."
  default     = 100
}

variable "services_ccu_per_instance_target" {
  type        = number
  description = "Target concurrent users (live client hub connections) per Services instance before scaling out. Services load per user is lighter (lobby/matchmaking), so this is higher than the GameServer target."
  default     = 500
}

# --- Backend runtime configuration (injected into version containers) -------

variable "mongo_connection_string" {
  type        = string
  description = "MongoDB Atlas connection string."
  sensitive   = true
}

variable "jwt_end_user_signing_key" {
  type      = string
  sensitive = true
}

variable "jwt_inter_tier_signing_key" {
  type      = string
  sensitive = true
}

# --- MongoDB Atlas Admin API (fleet-controller allowlist automation) --------
# Create a project-scoped API key in Atlas (sidebar: Project Identity & Access →
# Applications → API Keys) with the "Project Network Access Manager" role (older
# Atlas UI/docs call this "Project IP Access List Admin" — same role, renamed),
# then pass its public/private key here. The controller uses it to allowlist the
# re-allocated NAT egress IP on each wake. The key's own API Access List must
# include 0.0.0.0/0 since Cloud Run's egress IP is not fixed.

variable "atlas_public_key" {
  type        = string
  description = "Atlas Admin API public key (Digest username)."
  default     = ""
}

variable "atlas_private_key" {
  type        = string
  description = "Atlas Admin API private key (Digest password)."
  sensitive   = true
  default     = ""
}

variable "atlas_project_id" {
  type        = string
  description = "Atlas project (group) id whose Network Access list holds the NAT IP."
  default     = ""
}
