using ClashUp.Client.UI;

using TMPro;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Editor
{
    public static class SetupLoadingScreenAndLobby
    {
        [MenuItem("Tools/Setup Loading Screen & Lobby (one-time)")]
        public static void Run()
        {
            CreatePersistentUIScene();
            CreateLobbyScene();
            Debug.Log("[Setup] All done! Open AppStarter scene and press Play.");
        }

        private static void CreatePersistentUIScene()
        {
            const string sceneDir = "Assets/Core/UI/Content/Scenes";

            EnsureFolder("Assets/Core/UI");
            EnsureFolder("Assets/Core/UI/Content");
            EnsureFolder("Assets/Core/UI/Content/Scenes");

            const string scenePath = sceneDir + "/PersistentUI.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // Build loading screen hierarchy directly in the scene
            var root = new GameObject("LoadingScreen");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            root.AddComponent<GraphicRaycaster>();
            var canvasGroup = root.AddComponent<CanvasGroup>();

            // Background
            var bg = CreateChild("Background", root.transform);
            var bgRect = bg.GetComponent<RectTransform>();
            Stretch(bgRect);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // Step label
            var stepGo = CreateChild("StepLabel", root.transform);
            var stepRect = stepGo.GetComponent<RectTransform>();
            stepRect.anchorMin = new Vector2(0.5f, 0.5f);
            stepRect.anchorMax = new Vector2(0.5f, 0.5f);
            stepRect.sizeDelta = new Vector2(600, 50);
            stepRect.anchoredPosition = new Vector2(0, 40);
            var stepTmp = stepGo.AddComponent<TextMeshProUGUI>();
            stepTmp.text = "Initializing...";
            stepTmp.fontSize = 24;
            stepTmp.alignment = TextAlignmentOptions.Center;
            stepTmp.color = Color.white;

            // Progress bar background
            var barBg = CreateChild("ProgressBarBg", root.transform);
            var barBgRect = barBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.5f, 0.5f);
            barBgRect.anchorMax = new Vector2(0.5f, 0.5f);
            barBgRect.sizeDelta = new Vector2(500, 16);
            barBgRect.anchoredPosition = new Vector2(0, -10);
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Progress bar fill
            var barFill = CreateChild("ProgressBarFill", barBg.transform);
            var barFillRect = barFill.GetComponent<RectTransform>();
            Stretch(barFillRect);
            var barFillImg = barFill.AddComponent<Image>();
            barFillImg.color = new Color(0.2f, 0.6f, 1f, 1f);
            barFillImg.type = Image.Type.Filled;
            barFillImg.fillMethod = Image.FillMethod.Horizontal;
            barFillImg.fillAmount = 0f;

            // User ID label
            var userIdGo = CreateChild("UserIdLabel", root.transform);
            var userIdRect = userIdGo.GetComponent<RectTransform>();
            userIdRect.anchorMin = new Vector2(1f, 0f);
            userIdRect.anchorMax = new Vector2(1f, 0f);
            userIdRect.pivot = new Vector2(1f, 0f);
            userIdRect.sizeDelta = new Vector2(400, 30);
            userIdRect.anchoredPosition = new Vector2(-20, 20);
            var userIdTmp = userIdGo.AddComponent<TextMeshProUGUI>();
            userIdTmp.text = "";
            userIdTmp.fontSize = 14;
            userIdTmp.alignment = TextAlignmentOptions.BottomRight;
            userIdTmp.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            // LoadingScreenPresenter — wire via SerializedObject
            var presenter = root.AddComponent<LoadingScreenPresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("progressFill").objectReferenceValue = barFillImg;
            so.FindProperty("stepLabel").objectReferenceValue = stepTmp;
            so.FindProperty("userIdLabel").objectReferenceValue = userIdTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);

            AddToBuildSettings(scenePath);
            Debug.Log($"[Setup] Created scene: {scenePath}");
        }

        private static void CreateLobbyScene()
        {
            const string sceneDir = "Assets/Core/Lobby/Content/Scenes";

            EnsureFolder("Assets/Core/Lobby/Content");
            EnsureFolder(sceneDir);

            const string scenePath = sceneDir + "/Lobby.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var lobbyRoot = new GameObject("LobbyLifetimeScope");

            lobbyRoot.AddComponent<LobbyLifetimeScope>();

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);

            AddToBuildSettings(scenePath);
            Debug.Log($"[Setup] Created scene: {scenePath}");
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            var folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
