namespace ClashUp.Tools.FleetController;

/// <summary>Bound from the <c>Fleet</c> config section (Cloud Run env vars in prod).</summary>
public sealed class FleetControllerOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string Region { get; set; } = "us-central1";

    /// <summary>Cloud Scheduler job that pings <c>/tick</c>. Paused on sleep, resumed on wake.</summary>
    public string SchedulerJob { get; set; } = "clashup-idle-check";

    public string ServicesMig { get; set; } = "clashup-services-mig";
    public string ServicesAutoscaler { get; set; } = "clashup-services-autoscaler";
    public string GameServerMig { get; set; } = "clashup-gameserver-mig";
    public string GameServerAutoscaler { get; set; } = "clashup-gameserver-autoscaler";

    /// <summary>
    /// How far back to look for CCU before declaring the fleet idle. Kept larger than
    /// the scheduler interval (30 min) so a single between-matches dip can't trigger sleep —
    /// the whole window must read zero.
    /// </summary>
    public int IdleLookbackMinutes { get; set; } = 35;
}
