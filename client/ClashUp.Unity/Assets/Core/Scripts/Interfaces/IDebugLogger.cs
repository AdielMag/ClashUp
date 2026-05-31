namespace ClashUp.Client.Core
{
    public interface IDebugLogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}
