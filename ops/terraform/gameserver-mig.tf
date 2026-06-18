# GameServer tier: a regional MIG of identical gateway instances, each with a
# public IP (clients connect directly after matchmaking — no LB). Each instance
# spawns clashup-gameserver version backends on demand.

resource "google_compute_health_check" "gameserver" {
  name                = "clashup-gameserver-hc"
  check_interval_sec  = 10
  timeout_sec         = 5
  healthy_threshold   = 2
  unhealthy_threshold = 3

  tcp_health_check {
    port = 5101
  }
}

resource "google_compute_instance_template" "gameserver" {
  name_prefix  = "clashup-gameserver-"
  machine_type = var.gameserver_machine_type
  tags         = ["clashup-gameserver"]

  disk {
    # Container-Optimized OS: minimal, hardened, auto-updating, Docker preinstalled.
    source_image = "projects/cos-cloud/global/images/family/cos-stable"
    auto_delete  = true
    boot         = true
    disk_size_gb = 20
  }

  network_interface {
    subnetwork = google_compute_subnetwork.subnet.id

    # External IP so clients can connect directly to this instance's gateway.
    access_config {
      network_tier = "PREMIUM"
    }
  }

  metadata = {
    startup-script = templatefile("${path.module}/startup-gameserver.sh.tpl", {
      registry_host              = local.registry_host
      gateway_image              = local.gateway_image
      gameserver_repo            = local.gameserver_repo
      services_endpoint          = local.services_endpoint
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

resource "google_compute_region_instance_group_manager" "gameserver" {
  name               = "clashup-gameserver-mig"
  region             = var.region
  base_instance_name = "clashup-gameserver"

  version {
    instance_template = google_compute_instance_template.gameserver.id
  }

  auto_healing_policies {
    health_check      = google_compute_health_check.gameserver.id
    initial_delay_sec = 180
  }

  # OPPORTUNISTIC: never proactively replace instances hosting live matches.
  # New templates apply only to instances the autoscaler newly creates; existing
  # ones drain naturally (GracefulDrainService) and are replaced when unhealthy.
  update_policy {
    type                  = "OPPORTUNISTIC"
    minimal_action        = "REPLACE"
    max_surge_fixed       = 3
    max_unavailable_fixed = 3
  }
}

resource "google_compute_region_autoscaler" "gameserver" {
  name   = "clashup-gameserver-autoscaler"
  region = var.region
  target = google_compute_region_instance_group_manager.gameserver.id

  autoscaling_policy {
    min_replicas    = var.gameserver_min_instances
    max_replicas    = var.gameserver_max_instances
    cooldown_period = 300

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

    # Concurrent users — the per-instance gauge pushed by CcuMetricReporter.
    # target = max CCU per instance before the autoscaler adds capacity.
    metric {
      name   = "custom.googleapis.com/gameserver/ccu"
      type   = "GAUGE"
      target = var.gameserver_ccu_per_instance_target
    }
  }
}
