using System;
using System.Threading;

using ClashUp.Client.Core;

using Cysharp.Threading.Tasks;

using UnityEngine;
using UnityEngine.Networking;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Discovers the Services endpoint at boot for the cloud (Dev) environment, whose
    /// public IP is provisioned on demand by the fleet-controller (released while the
    /// fleet is asleep for $0 idle cost). The controller's Cloud Run URL is stable and
    /// free, so it is safe to bake into the client; a GET /resolve returns the current
    /// "http://IP:5001" endpoint and wakes the fleet if it was asleep.
    ///
    /// Retries indefinitely — a cold wake takes ~30-60s while instances boot/register,
    /// during which /resolve still returns the IP but the gateway isn't answering yet
    /// (the boot ping loop handles that second wait).
    /// </summary>
    public sealed class ServicesEndpointResolver
    {
        private const int RetryDelayMs = 3000;

        private readonly EnvironmentConfig _config;
        private readonly IDebugLogger _log;

        public ServicesEndpointResolver(EnvironmentConfig config, IDebugLogger log)
        {
            _config = config;
            _log = log;
        }

        public async UniTask<string> ResolveAsync(CancellationToken ct)
        {
            var url = _config.ControllerUrl.TrimEnd('/') + "/resolve";

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var req = UnityWebRequest.Get(url);
                    req.SetRequestHeader("X-ClashUp-Key", _config.ResolveKey);
                    await req.SendWebRequest().ToUniTask(cancellationToken: ct);

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var dto = JsonUtility.FromJson<ResolveResponse>(req.downloadHandler.text);
                        if (!string.IsNullOrEmpty(dto.endpoint))
                        {
                            _log.Log($"[Boot] Resolved Services endpoint: {dto.endpoint}");
                            return dto.endpoint;
                        }
                    }

                    _log.LogWarning($"[Boot] Resolve returned {req.responseCode} — retrying.");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"[Boot] Resolve failed ({ex.Message}) — retrying.");
                }

                await UniTask.Delay(RetryDelayMs, cancellationToken: ct);
            }

            ct.ThrowIfCancellationRequested();
            return string.Empty; // unreachable — the loop only exits via cancellation.
        }

        [Serializable]
        private struct ResolveResponse
        {
            public string endpoint;
        }
    }
}
