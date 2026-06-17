# Services-tier load balancer. GameServer traffic never flows through any LB
# (clients connect directly after matchmaking). Version routing happens inside
# each gateway instance, so the LB only needs to distribute connections.
#
# Default (services_domain == ""): external passthrough Network LB (L4/TCP) on
# :5001 — carries cleartext h2c gRPC, no domain required.
#
# services_domain set: external HTTPS Application LB (:443) with a Google-managed
# certificate; terminates TLS and speaks HTTP/2 (h2c) to the gateway backends.

locals {
  use_https = var.services_domain != ""

  services_endpoint = local.use_https ? "https://${var.services_domain}" : "http://${one(google_compute_address.services_l4[*].address)}:5001"
  services_lb_ip    = local.use_https ? one(google_compute_global_address.services_https[*].address) : one(google_compute_address.services_l4[*].address)
}

# ---------------------------------------------------------------------------
# L4 external passthrough Network LB (default — no domain, plaintext h2c)
# ---------------------------------------------------------------------------

resource "google_compute_address" "services_l4" {
  count  = local.use_https ? 0 : 1
  name   = "clashup-services-ip"
  region = var.region
}

resource "google_compute_region_health_check" "services_l4" {
  count  = local.use_https ? 0 : 1
  name   = "clashup-services-l4-hc"
  region = var.region

  tcp_health_check {
    port = 5001
  }
}

resource "google_compute_region_backend_service" "services_l4" {
  count                 = local.use_https ? 0 : 1
  name                  = "clashup-services-l4-backend"
  region                = var.region
  load_balancing_scheme = "EXTERNAL"
  protocol              = "TCP"
  health_checks         = [google_compute_region_health_check.services_l4[0].id]

  backend {
    group = google_compute_region_instance_group_manager.services.instance_group
  }
}

resource "google_compute_forwarding_rule" "services_l4" {
  count                 = local.use_https ? 0 : 1
  name                  = "clashup-services-l4-fr"
  region                = var.region
  load_balancing_scheme = "EXTERNAL"
  ip_protocol           = "TCP"
  ports                 = ["5001"]
  ip_address            = google_compute_address.services_l4[0].id
  backend_service       = google_compute_region_backend_service.services_l4[0].id
}

# A passthrough NLB preserves the client source IP, so clients reach instances
# directly on :5001 — allow public ingress to that port (NLB mode only).
resource "google_compute_firewall" "allow_services_l4" {
  count   = local.use_https ? 0 : 1
  name    = "clashup-allow-services-l4"
  network = google_compute_network.vpc.id

  allow {
    protocol = "tcp"
    ports    = ["5001"]
  }

  source_ranges = ["0.0.0.0/0"]
  target_tags   = ["clashup-services"]
}

# ---------------------------------------------------------------------------
# L7 external HTTPS Application LB (when services_domain is set)
# ---------------------------------------------------------------------------

resource "google_compute_global_address" "services_https" {
  count = local.use_https ? 1 : 0
  name  = "clashup-services-ip"
}

resource "google_compute_managed_ssl_certificate" "services" {
  count = local.use_https ? 1 : 0
  name  = "clashup-services-cert"

  managed {
    domains = [var.services_domain]
  }
}

resource "google_compute_backend_service" "services_https" {
  count                 = local.use_https ? 1 : 0
  name                  = "clashup-services-backend"
  protocol              = "HTTP2"
  port_name             = "grpc"
  load_balancing_scheme = "EXTERNAL_MANAGED"
  timeout_sec           = 86400 # long-lived gRPC streams

  health_checks = [google_compute_health_check.services.id]

  backend {
    group           = google_compute_region_instance_group_manager.services.instance_group
    balancing_mode  = "UTILIZATION"
    max_utilization = 0.8
  }
}

resource "google_compute_url_map" "services" {
  count           = local.use_https ? 1 : 0
  name            = "clashup-services-urlmap"
  default_service = google_compute_backend_service.services_https[0].id
}

resource "google_compute_target_https_proxy" "services" {
  count            = local.use_https ? 1 : 0
  name             = "clashup-services-proxy"
  url_map          = google_compute_url_map.services[0].id
  ssl_certificates = [google_compute_managed_ssl_certificate.services[0].id]
}

resource "google_compute_global_forwarding_rule" "services_https" {
  count                 = local.use_https ? 1 : 0
  name                  = "clashup-services-fr"
  load_balancing_scheme = "EXTERNAL_MANAGED"
  port_range            = "443"
  target                = google_compute_target_https_proxy.services[0].id
  ip_address            = google_compute_global_address.services_https[0].id
}
