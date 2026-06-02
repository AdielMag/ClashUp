using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ClashUp.Client.AppStarter
{
    /// <summary>
    /// Detects when the app resumes from a pause (focus loss / background) and
    /// forces a full session reset — unloads everything, destroys all
    /// DontDestroyOnLoad objects, and reloads the boot scene from scratch.
    ///
    /// Lives in the AppStarter scene. Moves itself to DontDestroyOnLoad so it
    /// survives the reload long enough to orchestrate teardown.
    /// </summary>
    public sealed class SessionResetHandler : MonoBehaviour
    {
        private bool _hasPaused;
        private bool _resetting;
        private GameObject _popupRoot;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _hasPaused = true;
                return;
            }

            if (_hasPaused && !_resetting)
            {
                _hasPaused = false;
                ShowResetPopup();
            }
        }

        private void ShowResetPopup()
        {
            if (_popupRoot != null) return;

            _popupRoot = new GameObject("SessionResetPopup");
            DontDestroyOnLoad(_popupRoot);

            var canvas = _popupRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = _popupRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            _popupRoot.AddComponent<GraphicRaycaster>();

            // Dim background
            var bg = new GameObject("Background");
            bg.transform.SetParent(_popupRoot.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            // Message
            var msgGo = new GameObject("Message");
            msgGo.transform.SetParent(_popupRoot.transform, false);
            var msgRect = msgGo.AddComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0.5f, 0.5f);
            msgRect.anchorMax = new Vector2(0.5f, 0.5f);
            msgRect.sizeDelta = new Vector2(500f, 60f);
            msgRect.anchoredPosition = new Vector2(0f, 40f);
            var msgText = msgGo.AddComponent<Text>();
            msgText.text = "Session interrupted. Reconnecting...";
            msgText.fontSize = 28;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.color = Color.white;
            msgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                           ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // OK button
            var btnGo = new GameObject("OkButton");
            btnGo.transform.SetParent(_popupRoot.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(200f, 50f);
            btnRect.anchoredPosition = new Vector2(0f, -40f);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.13f, 0.47f, 0.84f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(ExecuteReset);

            var btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTextRect = btnTextGo.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            var btnText = btnTextGo.AddComponent<Text>();
            btnText.text = "OK";
            btnText.fontSize = 24;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                           ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void ExecuteReset()
        {
            _resetting = true;

            // Destroy all DontDestroyOnLoad objects except this handler and the popup
            DestroyAllDontDestroyOnLoad();

            // Destroy the popup
            if (_popupRoot != null)
            {
                Destroy(_popupRoot);
                _popupRoot = null;
            }

            // Reload boot scene — Single mode unloads all additive scenes,
            // destroying all VContainer scopes and their registrations.
            SceneManager.LoadScene(0, LoadSceneMode.Single);

            // Self-destruct after reload kicks in
            Destroy(gameObject);
        }

        private void DestroyAllDontDestroyOnLoad()
        {
            // DontDestroyOnLoad objects live in a hidden scene.
            // The only reliable way to find them is via this gameObject's scene.
            var dontDestroyScene = gameObject.scene;
            var roots = dontDestroyScene.GetRootGameObjects();

            foreach (var root in roots)
            {
                if (root == gameObject) continue;
                if (root == _popupRoot) continue;
                Destroy(root);
            }
        }
    }
}
