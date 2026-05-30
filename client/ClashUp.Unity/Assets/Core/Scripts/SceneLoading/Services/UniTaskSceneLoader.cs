using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClashUp.Client.Core
{
    public sealed class UniTaskSceneLoader : ISceneLoader
    {
        public async UniTask<SceneHandle> LoadAdditiveAsync(string sceneName, IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(op.progress / 0.9f);
                await UniTask.Yield(ct);
            }

            progress?.Report(1f);
            op.allowSceneActivation = true;
            await op.ToUniTask(cancellationToken: ct);

            var scene = SceneManager.GetSceneByName(sceneName);
            return new SceneHandle(scene);
        }

        public async UniTask UnloadAsync(SceneHandle handle, CancellationToken ct = default)
        {
            if (!handle.IsValid) return;
            await SceneManager.UnloadSceneAsync(handle.Scene).ToUniTask(cancellationToken: ct);
        }
    }
}
