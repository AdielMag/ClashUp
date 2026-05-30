using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ClashUp.Client.Core
{
    public interface ISceneLoader
    {
        UniTask<SceneHandle> LoadAdditiveAsync(string sceneName, IProgress<float> progress = null,
            CancellationToken ct = default);

        UniTask UnloadAsync(SceneHandle handle, CancellationToken ct = default);
    }
}
