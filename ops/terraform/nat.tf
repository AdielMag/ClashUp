# Egress for instances WITHOUT an external IP (the Services tier). They need to
# reach MongoDB Atlas (public internet); Private Google Access (see network.tf)
# only covers Google APIs. A reserved static NAT IP gives a stable address to
# allowlist in Atlas → Network Access.
#
# GameServer instances have their own external IP and egress through that, so
# Cloud NAT does not apply to them — only the NAT IP needs allowlisting in Atlas.

resource "google_compute_router" "router" {
  name    = "clashup-router"
  region  = var.region
  network = google_compute_network.vpc.id
}

resource "google_compute_address" "nat" {
  name   = "clashup-nat-ip"
  region = var.region
}

resource "google_compute_router_nat" "nat" {
  name                               = "clashup-nat"
  router                             = google_compute_router.router.name
  region                             = var.region
  nat_ip_allocate_option             = "MANUAL_ONLY"
  nat_ips                            = [google_compute_address.nat.self_link]
  source_subnetwork_ip_ranges_to_nat = "ALL_SUBNETWORKS_ALL_IP_RANGES"
}
