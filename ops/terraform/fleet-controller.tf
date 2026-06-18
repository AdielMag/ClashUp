# Idle-fleet auto-sleep / wake controller.
#
# GCP MIG autoscalers cannot scale below 1, so two e2-small boxes would otherwise
# run 24/7 (~$25/mo idle). This Cloud Run service drives both MIGs to 0 when nobody
# is online and back up on demand:
#   - Cloud Scheduler pings POST /tick every 30 min. When CCU has been 0 across the
#     lookback window, the controller turns both autoscalers OFF, resizes both MIGs
#     to 0, then PAUSES its own scheduler job (no point checking an asleep fleet).
#   - The dashboard's "Wake" button calls POST /wake (via run.invoker): autoscalers
#     back ON, MIGs resized to 1, scheduler job RESUMED.
# The service itself scales to zero, so it costs ~nothing.

variable "fleet_idle_check_schedule" {
  type        = string
  description = "Cron schedule for the idle check. Default: every 30 minutes."
  default     = "*/30 * * * *"
}

resource "google_project_service" "run" {
  service            = "run.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "cloudscheduler" {
  service            = "cloudscheduler.googleapis.com"
  disable_on_destroy = false
}

# --- Controller service account + permissions -------------------------------
resource "google_service_account" "fleet_controller" {
  account_id   = "clashup-fleet-controller"
  display_name = "ClashUp idle-fleet sleep/wake controller"
}

# Resize MIGs and flip autoscaler mode.
resource "google_project_iam_member" "fc_compute_admin" {
  project = var.project_id
  role    = "roles/compute.instanceAdmin.v1"
  member  = "serviceAccount:${google_service_account.fleet_controller.email}"
}

# Pause/resume its own Cloud Scheduler job.
resource "google_project_iam_member" "fc_scheduler_admin" {
  project = var.project_id
  role    = "roles/cloudscheduler.admin"
  member  = "serviceAccount:${google_service_account.fleet_controller.email}"
}

# Read the CCU gauges to judge idle.
resource "google_project_iam_member" "fc_monitoring_viewer" {
  project = var.project_id
  role    = "roles/monitoring.viewer"
  member  = "serviceAccount:${google_service_account.fleet_controller.email}"
}

# --- Cloud Run service ------------------------------------------------------
resource "google_cloud_run_v2_service" "fleet_controller" {
  name     = "clashup-fleet-controller"
  location = var.region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.fleet_controller.email

    scaling {
      min_instance_count = 0
      max_instance_count = 1
    }

    containers {
      image = local.fleet_controller_image

      ports {
        container_port = 8080
      }

      env {
        name  = "Fleet__ProjectId"
        value = var.project_id
      }
      env {
        name  = "Fleet__Region"
        value = var.region
      }
      env {
        name  = "Fleet__SchedulerJob"
        value = "clashup-idle-check"
      }
      env {
        name  = "Fleet__ServicesMig"
        value = google_compute_region_instance_group_manager.services.name
      }
      env {
        name  = "Fleet__ServicesAutoscaler"
        value = google_compute_region_autoscaler.services.name
      }
      env {
        name  = "Fleet__GameServerMig"
        value = google_compute_region_instance_group_manager.gameserver.name
      }
      env {
        name  = "Fleet__GameServerAutoscaler"
        value = google_compute_region_autoscaler.gameserver.name
      }
    }
  }

  depends_on = [google_project_service.run]
}

# Callers allowed to invoke the (authenticated) service.
resource "google_cloud_run_v2_service_iam_member" "scheduler_invoker" {
  name     = google_cloud_run_v2_service.fleet_controller.name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.fleet_controller.email}"
}

# The local read-only dashboard SA can TRIGGER the controller but cannot touch
# compute itself — fleet-control rights stay on the controller SA only.
resource "google_cloud_run_v2_service_iam_member" "dashboard_invoker" {
  name     = google_cloud_run_v2_service.fleet_controller.name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:clashup-dashboard@${var.project_id}.iam.gserviceaccount.com"
}

# --- Cloud Scheduler idle check ---------------------------------------------
resource "google_cloud_scheduler_job" "idle_check" {
  name             = "clashup-idle-check"
  region           = var.region
  schedule         = var.fleet_idle_check_schedule
  time_zone        = "Etc/UTC"
  attempt_deadline = "320s"

  http_target {
    http_method = "POST"
    uri         = "${google_cloud_run_v2_service.fleet_controller.uri}/tick"

    oidc_token {
      service_account_email = google_service_account.fleet_controller.email
      audience              = google_cloud_run_v2_service.fleet_controller.uri
    }
  }

  # The controller pauses this job on sleep and the dashboard resumes it on wake,
  # so its paused state lives outside Terraform — don't let apply fight it.
  lifecycle {
    ignore_changes = [paused]
  }

  depends_on = [google_project_service.cloudscheduler]
}

output "fleet_controller_url" {
  description = "Cloud Run URL of the idle-fleet controller. Set this as Gcp:FleetControllerUrl in the dashboard."
  value       = google_cloud_run_v2_service.fleet_controller.uri
}
