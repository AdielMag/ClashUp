# Descriptor for the custom CCU gauge pushed by CcuMetricReporter and consumed by
# the GameServer autoscaler + local dashboard.
resource "google_monitoring_metric_descriptor" "ccu" {
  type         = "custom.googleapis.com/gameserver/ccu"
  metric_kind  = "GAUGE"
  value_type   = "INT64"
  display_name = "GameServer Concurrent Users"
  description  = "Concurrent connected users on a GameServer instance (5-min disconnect grace applied)."

  labels {
    key         = "version"
    value_type  = "STRING"
    description = "Server version of the backend process reporting CCU."
  }
}
