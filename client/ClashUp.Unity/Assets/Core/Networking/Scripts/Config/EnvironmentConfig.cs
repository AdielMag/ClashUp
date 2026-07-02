using UnityEngine;

namespace ClashUp.Client.Networking
{
    public enum ServerEnvironment
    {
        Local,
        Emulator,
        Tailscale,
        Dev
    }

    [CreateAssetMenu(fileName = "EnvironmentConfig", menuName = "ClashUp/Environment Config")]
    public sealed class EnvironmentConfig : ScriptableObject
    {
        [SerializeField] private ServerEnvironment current = ServerEnvironment.Dev;

        [SerializeField] private SerializedDictionary<ServerEnvironment, string> servicesUrls = new()
        {
            { ServerEnvironment.Local, "http://localhost:5001" },
            { ServerEnvironment.Emulator, "http://10.0.2.2:5001" },
            { ServerEnvironment.Tailscale, "http://100.68.118.109:5001" },
            { ServerEnvironment.Dev, "https://dev.clashup.example.com" }
        };

        [Header("Dynamic discovery (cloud/Dev only)")]
        [Tooltip("Fleet-controller Cloud Run URL. The client GETs {url}/resolve at boot to " +
                 "learn the current Services IP and wake the fleet. From `terraform output fleet_controller_url`.")]
        [SerializeField] private string controllerUrl = "";

        [Tooltip("Shared key sent as X-ClashUp-Key on /resolve. From `terraform output fleet_resolve_key`.")]
        [SerializeField] private string resolveKey = "";

        public ServerEnvironment Current => current;

        public string GetServicesUrl()
        {
            return servicesUrls.TryGetValue(current, out var url) ? url : "http://localhost:5001";
        }

        public string ControllerUrl => controllerUrl;
        public string ResolveKey => resolveKey;

        /// <summary>
        /// True when the current environment's Services IP is provisioned on demand by the
        /// fleet-controller (released while asleep) and must be discovered at boot rather than
        /// baked in. Only the cloud (Dev) environment uses this; local/emulator/tailscale are static.
        /// </summary>
        public bool RequiresDiscovery =>
            current == ServerEnvironment.Dev && !string.IsNullOrEmpty(controllerUrl);

        public void SetCurrent(ServerEnvironment env) => current = env;

        public ServerEnvironment[] GetAllEnvironments() =>
            (ServerEnvironment[])System.Enum.GetValues(typeof(ServerEnvironment));
    }
}
