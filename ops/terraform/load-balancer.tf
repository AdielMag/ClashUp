# External Application Load Balancer for the Services tier only.
# GameServer traffic does NOT flow through any LB. Version routing happens inside
# each Services gateway instance, so the LB is a plain HTTP/2 distributor.

resource "google_compute_global_address" "services" {
  name = "clashup-services-ip"
}

resource "google_compute_backend_service" "services" {
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
  name            = "clashup-services-urlmap"
  default_service = google_compute_backend_service.services.id
}

resource "google_compute_target_http_proxy" "services" {
  name    = "clashup-services-proxy"
  url_map = google_compute_url_map.services.id
}

# The global external Application LB supports ports 80/8080 for HTTP proxies
# (443 for HTTPS). Clients reach Services at <lb-ip>:80; the LB forwards to each
# instance's gateway on the "grpc" named port (5001). Add an HTTPS proxy +
# managed cert on 443 for production TLS.
resource "google_compute_global_forwarding_rule" "services" {
  name                  = "clashup-services-fr"
  load_balancing_scheme = "EXTERNAL_MANAGED"
  port_range            = "80"
  target                = google_compute_target_http_proxy.services.id
  ip_address            = google_compute_global_address.services.id
}
