using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.UI
{
    public sealed class LoadingScreenPresenter : MonoBehaviour, ILoadingScreen
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text stepLabel;
        [SerializeField] private TMP_Text userIdLabel;
        [SerializeField] private float lerpSpeed = 3f;
        [SerializeField] private float fadeDuration = 0.3f;

        private float _targetProgress;
        private float _currentProgress;

        private void Awake()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentProgress, _targetProgress)) return;
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, lerpSpeed * Time.deltaTime);
            progressFill.fillAmount = _currentProgress;
        }

        public async UniTask ShowAsync(CancellationToken ct = default)
        {
            _currentProgress = 0f;
            _targetProgress = 0f;
            progressFill.fillAmount = 0f;
            stepLabel.text = "Initializing...";
            userIdLabel.text = string.Empty;
            canvasGroup.blocksRaycasts = true;
            await FadeAsync(1f, ct);
        }

        public async UniTask HideAsync(CancellationToken ct = default)
        {
            await FadeAsync(0f, ct);
            canvasGroup.blocksRaycasts = false;
        }

        public void SetProgress(float target)
        {
            _targetProgress = Mathf.Clamp01(target);
        }

        public void SetStepText(string text)
        {
            stepLabel.text = text;
        }

        public void SetUserId(string userId)
        {
            userIdLabel.text = userId;
        }

        public async UniTask WaitForProgressComplete(CancellationToken ct = default)
        {
            while (!Mathf.Approximately(_currentProgress, _targetProgress))
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield(ct);
            }
        }

        private async UniTask FadeAsync(float targetAlpha, CancellationToken ct)
        {
            var startAlpha = canvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                await UniTask.Yield(ct);
            }

            canvasGroup.alpha = targetAlpha;
        }
    }
}
