# Egress for instances WITHOUT an external IP (the Services tier). They need to
# reach MongoDB Atlas (public internet); Private Google Access (see network.tf)
# only covers Google APIs. A reserved static NAT IP gives a stable address to
# allowlist in Atlas → Network Access.
#
# GameServer instances have their own external IP and egress through that, so
# Cloud NAT does not apply to them — only the NAT IP needs allowlisting in Atlas.

# The router is durable (free while idle) and stays in Terraform. The NAT config
# (clashup-nat) and its egress IP (clashup-nat-ip) are provisioned at runtime by
# the fleet-controller: released on sleep (→ $0 idle) and re-created on wake, which
# also re-allowlists the fresh NAT IP in MongoDB Atlas Network Access. See
# src/Tools/ClashUp.FleetController + ops/terraform/README.md for the state rm bootstrap.
resource "google_compute_router" "router" {
  name    = "clashup-router"
  region  = var.region
  network = google_compute_network.vpc.id
}
