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

variable "services_machine_type" {
  type    = string
  default = "e2-standard-2"
}

variable "gameserver_machine_type" {
  type    = string
  default = "e2-standard-4"
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

variable "ccu_per_instance_target" {
  type        = number
  description = "Target concurrent users per GameServer instance before scaling out."
  default     = 100
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
