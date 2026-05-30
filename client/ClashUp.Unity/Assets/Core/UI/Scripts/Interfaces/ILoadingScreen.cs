using System.Threading;
using Cysharp.Threading.Tasks;

namespace ClashUp.Client.UI
{
    public interface ILoadingScreen
    {
        UniTask ShowAsync(CancellationToken ct = default);
        UniTask HideAsync(CancellationToken ct = default);
        void SetProgress(float target);
        void SetStepText(string text);
        void SetUserId(string userId);
    }
}
