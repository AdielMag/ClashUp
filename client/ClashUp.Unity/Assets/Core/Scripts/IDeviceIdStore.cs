namespace ClashUp.Client.Core
{
    /// <summary>
    /// Persists the device GUID that identifies a phase-1 anonymous account.
    /// The concrete implementation in AppStarter is PlayerPrefs-backed; the
    /// interface lets tests substitute an in-memory store.
    /// </summary>
    public interface IDeviceIdStore
    {
        string GetOrCreate();
    }
}
