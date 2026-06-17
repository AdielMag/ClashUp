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
}
