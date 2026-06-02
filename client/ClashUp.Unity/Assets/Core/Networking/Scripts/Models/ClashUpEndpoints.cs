namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Backend endpoint configuration. Populated at boot from a config asset
    /// or environment override; for phase 1 we ship defaults that match the
    /// local dev hosts (Services on :5001, GameServer on :5101).
    /// </summary>
    public sealed class ClashUpEndpoints
    {
        /// <summary>Resolved URL set by AppStarter after env picker, read by CoreStarter.</summary>
        public static string ResolvedServicesAddress { get; set; } = string.Empty;

        public string ServicesAddress { get; set; }

        public ClashUpEndpoints(EnvironmentConfig config)
        {
            ServicesAddress = config.GetServicesUrl();
        }

        public ClashUpEndpoints()
        {
            ServicesAddress = ResolvedServicesAddress;
        }
    }
}
