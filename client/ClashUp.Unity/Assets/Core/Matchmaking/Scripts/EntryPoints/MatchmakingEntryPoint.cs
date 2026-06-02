using System;
using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Client.CoreStarter;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Matchmaking
{
    public sealed class MatchmakingEntryPoint : IAsyncStartable
    {
        private readonly MatchmakingClient _matchmaking;
        private readonly GameFlowController _flow;
        private readonly IDebugLogger _log;

        public MatchmakingEntryPoint(MatchmakingClient matchmaking, GameFlowController flow, IDebugLogger log)
        {
            _matchmaking = matchmaking;
            _flow = flow;
            _log = log;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            var ui = MatchmakingUI.Create();
            var cancelCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            ui.OnCancelClicked += () => cancelCts.Cancel();

            var startTime = Time.time;

            try
            {
                ui.SetStatus("Searching for opponents...");

                var timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancelCts.Token);
                RunTimerAsync(ui, startTime, timerCts.Token).Forget();

                var handoff = await _matchmaking.EnqueueAndWaitAsync(
                    new QueueRequest { ModeId = "default" }, cancelCts.Token);

                timerCts.Cancel();
                ui.SetStatus("Match found!");
                ui.SetInteractable(false);
                _log.Log($"[Matchmaking] Matched! MatchId={handoff.MatchId}");

                await UniTask.Delay(500, cancellationToken: cancellation);

                ui.Destroy();
                _flow.EnterMatchFromMatchmaking(handoff);
            }
            catch (OperationCanceledException)
            {
                _log.Log("[Matchmaking] Cancelled by user");
                ui.Destroy();
                _flow.ReturnToLobbyFromMatchmaking();
            }
            catch (Exception ex)
            {
                _log.LogError($"[Matchmaking] Failed: {ex.Message}");
                ui.SetStatus($"Failed: {ex.Message}");
                ui.SetInteractable(true);

                await UniTask.Delay(2000, cancellationToken: cancellation);
                ui.Destroy();
                _flow.ReturnToLobbyFromMatchmaking();
            }
        }

        private static async UniTaskVoid RunTimerAsync(MatchmakingUI ui, float startTime, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ui.SetTimer(Time.time - startTime);
                await UniTask.Delay(250, cancellationToken: ct);
            }
        }
    }
}
