using UnityEngine;

namespace ClashUp.Client.Networking
{
    public enum ServerEnvironment
    {
        Local,
        Dev
    }

    [CreateAssetMenu(fileName = "EnvironmentConfig", menuName = "ClashUp/Environment Config")]
    public sealed class EnvironmentConfig : ScriptableObject
    {
        [SerializeField] private ServerEnvironment current = ServerEnvironment.Dev;

        [SerializeField] private SerializedDictionary<ServerEnvironment, string> servicesUrls = new()
        {
            { ServerEnvironment.Local, "http://localhost:5001" },
            { ServerEnvironment.Dev, "https://dev.clashup.example.com" }
        };

        public ServerEnvironment Current => current;

        public string GetServicesUrl()
        {
            return servicesUrls.TryGetValue(current, out var url) ? url : "http://localhost:5001";
        }
    }
}
