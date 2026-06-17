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

# Host memory utilization (%), pushed by the gateway's HostMetricsReporter on
# every instance. Replaces the Ops Agent RAM metric so instances run on COS.
resource "google_monitoring_metric_descriptor" "memory" {
  type         = "custom.googleapis.com/instance/memory_utilization"
  metric_kind  = "GAUGE"
  value_type   = "DOUBLE"
  unit         = "%"
  display_name = "Instance Memory Utilization"
  description  = "Host VM memory utilization percentage, reported by the gateway."
}
