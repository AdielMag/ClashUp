namespace ClashUp.Client.Networking;

/// <summary>
/// Backend endpoint configuration. Populated at boot from a config asset
/// or environment override; for phase 1 we ship defaults that match the
/// local dev hosts (Services on :5001, GameServer on :5101).
/// </summary>
public sealed class ClashUpEndpoints
{
    public string ServicesAddress { get; init; } = "http://localhost:5001";
}
