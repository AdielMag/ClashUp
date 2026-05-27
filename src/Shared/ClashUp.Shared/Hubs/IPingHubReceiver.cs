namespace ClashUp.Shared.Hubs
{
    public interface IPingHubReceiver
    {
        void OnHeartbeat(long serverStampMs);
    }
}
