output "services_lb_ip" {
  description = "Public IP of the Services load balancer. Clients connect here first."
  value       = local.services_lb_ip
}

output "services_endpoint" {
  description = "Full Services endpoint clients/GameServer use (http://IP:5001 or https://domain)."
  value       = local.services_endpoint
}

output "nat_ip" {
  description = "Static egress IP for Services instances. Allowlist this in MongoDB Atlas → Network Access."
  value       = google_compute_address.nat.address
}

output "artifact_registry" {
  description = "Base Docker image path. Push images as <base>/clashup-<tier>:<version>."
  value       = local.registry_base
}

output "services_mig" {
  description = "Services MIG self-link (used by CD for rolling updates)."
  value       = google_compute_region_instance_group_manager.services.id
}

output "gameserver_mig" {
  description = "GameServer MIG self-link (used by CD for set-instance-template)."
  value       = google_compute_region_instance_group_manager.gameserver.id
}

output "instance_service_account" {
  description = "Service account attached to all instances."
  value       = google_service_account.instance.email
}
