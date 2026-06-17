resource "google_compute_network" "vpc" {
  name                    = "clashup-vpc"
  auto_create_subnetworks = false
}

resource "google_compute_subnetwork" "subnet" {
  name          = "clashup-subnet"
  ip_cidr_range = "10.0.0.0/24"
  region        = var.region
  network       = google_compute_network.vpc.id

  # Lets instances without an external IP (Services tier) reach Google APIs —
  # Artifact Registry, Cloud Monitoring, metadata — over Google's private path.
  private_ip_google_access = true
}

# Services instances are reached only via the external LB, whose proxies use the
# Google LB/health-check ranges (see allow_health_checks below). No direct public
# ingress to :5001 is needed.

# GameServer instances are reached directly by clients (no LB) on the gateway port.
resource "google_compute_firewall" "allow_gameserver_public" {
  name    = "clashup-allow-gameserver"
  network = google_compute_network.vpc.id

  allow {
    protocol = "tcp"
    ports    = ["5101"]
  }

  source_ranges = ["0.0.0.0/0"]
  target_tags   = ["clashup-gameserver"]
}

# Health checks for both tiers (TCP — protocol-agnostic, avoids the h2c probe wrinkle).
resource "google_compute_firewall" "allow_health_checks" {
  name    = "clashup-allow-health-checks"
  network = google_compute_network.vpc.id

  allow {
    protocol = "tcp"
    ports    = ["5001", "5101"]
  }

  source_ranges = ["130.211.0.0/22", "35.191.0.0/16"]
  target_tags   = ["clashup-services", "clashup-gameserver"]
}

# Internal traffic between instances (Services <-> GameServer).
resource "google_compute_firewall" "allow_internal" {
  name    = "clashup-allow-internal"
  network = google_compute_network.vpc.id

  allow {
    protocol = "tcp"
  }

  source_ranges = ["10.0.0.0/24"]
}

# SSH via Identity-Aware Proxy for troubleshooting (no public SSH).
resource "google_compute_firewall" "allow_iap_ssh" {
  name    = "clashup-allow-iap-ssh"
  network = google_compute_network.vpc.id

  allow {
    protocol = "tcp"
    ports    = ["22"]
  }

  source_ranges = ["35.235.240.0/20"]
}
