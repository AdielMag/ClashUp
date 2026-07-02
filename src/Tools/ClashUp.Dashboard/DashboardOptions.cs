namespace ClashUp.Tools.Dashboard;

/// <summary>Bound from the <c>Gcp</c> config section.</summary>
public sealed class DashboardOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string Region { get; set; } = "us-central1";

    /// <summary>
    /// Path to the read-only dashboard service-account key JSON. If empty,
    /// Application Default Credentials are used (e.g. GOOGLE_APPLICATION_CREDENTIALS
    /// or `gcloud auth application-default login`).
    /// </summary>
    public string? CredentialsPath { get; set; }

    public string Repository { get; set; } = "clashup-docker";

    public string ServicesMig { get; set; } = "clashup-services-mig";
    public string GameServerMig { get; set; } = "clashup-gameserver-mig";

    /// <summary>Cloud Scheduler idle-check job — read for the "next check in X" countdown.</summary>
    public string SchedulerJob { get; set; } = "clashup-idle-check";

    /// <summary>
    /// Base URL of the Cloud Run fleet controller (from <c>terraform output
    /// fleet_controller_url</c>). The Wake button POSTs to <c>{url}/wake</c>. Empty = Wake disabled.
    /// </summary>
    public string? FleetControllerUrl { get; set; }

    /// <summary>
    /// Admin key sent as <c>X-ClashUp-Key</c> on the controller's /wake + /state routes (from
    /// <c>terraform output fleet_admin_key</c>). The controller has public ingress and gates
    /// these routes in-app by this shared key rather than Cloud Run IAM.
    /// </summary>
    public string? FleetControllerAdminKey { get; set; }
}
