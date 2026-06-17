# Service account attached to every gateway instance. It must pull version
# images from Artifact Registry at runtime and write CCU + Ops Agent metrics.
resource "google_service_account" "instance" {
  account_id   = "clashup-instance"
  display_name = "ClashUp gateway/backends instance SA"
}

resource "google_project_iam_member" "instance_artifact_reader" {
  project = var.project_id
  role    = "roles/artifactregistry.reader"
  member  = "serviceAccount:${google_service_account.instance.email}"
}

resource "google_project_iam_member" "instance_metric_writer" {
  project = var.project_id
  role    = "roles/monitoring.metricWriter"
  member  = "serviceAccount:${google_service_account.instance.email}"
}

resource "google_project_iam_member" "instance_log_writer" {
  project = var.project_id
  role    = "roles/logging.logWriter"
  member  = "serviceAccount:${google_service_account.instance.email}"
}
