using MessagePack;

namespace ClashUp.Shared.Services
{
    /// <summary>
    /// Server-side seam that will spin up new GS instances on demand
    /// (k8s/docker/cloud API). Phase-1 implementation is a TODO stub —
    /// it logs and returns NotProvisioned so the matchmaker can call it
    /// safely without a real cluster behind it.
    /// </summary>
    public interface IGameServerProvisioner
    {
        System.Threading.Tasks.Task<ProvisionerResponse> RequestNewInstanceAsync(
            System.Threading.CancellationToken cancellationToken);
    }

    [MessagePackObject]
    public sealed class ProvisionerResponse
    {
        [Key(0)] public bool Started { get; init; }
        [Key(1)] public string Reason { get; init; } = string.Empty;
    }
}
