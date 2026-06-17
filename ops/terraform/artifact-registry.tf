resource "google_artifact_registry_repository" "docker" {
  location      = var.region
  repository_id = "clashup-docker"
  description   = "ClashUp server images (services, gameserver, gateway)."
  format        = "DOCKER"
}
