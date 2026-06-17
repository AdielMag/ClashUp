output "services_lb_ip" {
  description = "Public IP of the Services load balancer. Clients connect here first (port 5001)."
  value       = google_compute_global_address.services.address
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
