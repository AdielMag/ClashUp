# Services tier: a regional MIG of identical gateway instances behind the LB.
# Each instance spawns clashup-services version backends on demand.

resource "google_compute_health_check" "services" {
  name                = "clashup-services-hc"
  check_interval_sec  = 10
  timeout_sec         = 5
  healthy_threshold   = 2
  unhealthy_threshold = 3

  # TCP check on the gateway gRPC port — protocol-agnostic (avoids h2c probing).
  tcp_health_check {
    port = 5001
  }
}

resource "google_compute_instance_template" "services" {
  name_prefix  = "clashup-services-"
  machine_type = var.services_machine_type
  tags         = ["clashup-services"]

  disk {
    # Container-Optimized OS: minimal, hardened, auto-updating, Docker preinstalled.
    source_image = "projects/cos-cloud/global/images/family/cos-stable"
    auto_delete  = true
    boot         = true
    disk_size_gb = 20
  }

  network_interface {
    subnetwork = google_compute_subnetwork.subnet.id
    # No external IP — reached only through the load balancer.
  }

  metadata = {
    startup-script = templatefile("${path.module}/startup-services.sh.tpl", {
      registry_host              = local.registry_host
      gateway_image              = local.gateway_image
      services_repo              = local.services_repo
      mongo_connection_string    = var.mongo_connection_string
      jwt_end_user_signing_key   = var.jwt_end_user_signing_key
      jwt_inter_tier_signing_key = var.jwt_inter_tier_signing_key
    })
  }

  service_account {
    email  = google_service_account.instance.email
    scopes = ["cloud-platform"]
  }

  lifecycle {
    create_before_destroy = true
  }
}

resource "google_compute_region_instance_group_manager" "services" {
  name               = "clashup-services-mig"
  region             = var.region
  base_instance_name = "clashup-services"

  version {
    instance_template = google_compute_instance_template.services.id
  }

  named_port {
    name = "grpc"
    port = 5001
  }

  auto_healing_policies {
    health_check      = google_compute_health_check.services.id
    initial_delay_sec = 120
  }

  update_policy {
    type                  = "PROACTIVE"
    minimal_action        = "REPLACE"
    max_surge_fixed       = 1
    max_unavailable_fixed = 0
  }
}

resource "google_compute_region_autoscaler" "services" {
  name   = "clashup-services-autoscaler"
  region = var.region
  target = google_compute_region_instance_group_manager.services.id

  autoscaling_policy {
    min_replicas    = var.services_min_instances
    max_replicas    = var.services_max_instances
    cooldown_period = 120

    cpu_utilization {
      target = var.cpu_target_utilization
    }

    # RAM via the gateway's self-reported host-memory metric (no Ops Agent needed,
    # so instances can run on Container-Optimized OS).
    metric {
      name   = "custom.googleapis.com/instance/memory_utilization"
      type   = "GAUGE"
      target = var.ram_target_utilization * 100
    }
  }
}
