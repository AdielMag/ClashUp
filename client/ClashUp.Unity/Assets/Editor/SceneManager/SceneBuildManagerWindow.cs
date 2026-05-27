using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.UIElements;

using Cursor = UnityEngine.UIElements.Cursor;

namespace ClashUp.Editor.SceneManager
{
    public sealed class SceneBuildManagerWindow : EditorWindow
    {
        private const string WindowTitle = "Scene Build Manager";

        private VisualElement _sceneListContainer;
        private Label _emptyLabel;

        [MenuItem("Tools/Scene Build Manager %g")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneBuildManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(420, 300);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            root.style.paddingTop = 0;
            root.style.paddingBottom = 12;
            root.style.paddingLeft = 0;
            root.style.paddingRight = 0;

            // Header
            var header = new VisualElement();
            header.style.backgroundColor = new Color(0.13f, 0.47f, 0.84f);
            header.style.paddingTop = 14;
            header.style.paddingBottom = 14;
            header.style.paddingLeft = 18;
            header.style.paddingRight = 18;
            header.style.marginBottom = 8;

            var title = new Label(WindowTitle);
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            header.Add(title);

            var subtitle = new Label("Toggle scenes to include or exclude them from Build Settings.");
            subtitle.style.fontSize = 11;
            subtitle.style.color = new Color(0.85f, 0.92f, 1f);
            subtitle.style.marginTop = 4;
            header.Add(subtitle);

            root.Add(header);

            // Toolbar
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.FlexEnd;
            toolbar.style.paddingLeft = 12;
            toolbar.style.paddingRight = 12;
            toolbar.style.marginBottom = 4;

            var refreshBtn = new Button(Refresh) { text = "Refresh" };
            refreshBtn.style.height = 24;
            refreshBtn.style.paddingLeft = 14;
            refreshBtn.style.paddingRight = 14;
            refreshBtn.style.borderTopLeftRadius = 4;
            refreshBtn.style.borderTopRightRadius = 4;
            refreshBtn.style.borderBottomLeftRadius = 4;
            refreshBtn.style.borderBottomRightRadius = 4;
            toolbar.Add(refreshBtn);

            root.Add(toolbar);

            // Scroll area
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 12;
            scroll.style.paddingRight = 12;

            _sceneListContainer = new VisualElement();
            scroll.Add(_sceneListContainer);

            _emptyLabel = new Label("No .unity scenes found in the project.");
            _emptyLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _emptyLabel.style.marginTop = 20;
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            scroll.Add(_emptyLabel);

            root.Add(scroll);

            Refresh();
        }

        private void Refresh()
        {
            _sceneListContainer.Clear();

            var buildScenes = EditorBuildSettings.scenes;
            var buildPaths = buildScenes.Select(s => s.path).ToList();
            var buildSet = new HashSet<string>(buildPaths);

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var allScenePaths = sceneGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/"))
                .Distinct()
                .ToList();

            var notInBuild = allScenePaths
                .Where(p => !buildSet.Contains(p))
                .OrderBy(p => p)
                .ToList();

            _emptyLabel.style.display = allScenePaths.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // --- In Build section ---
            if (buildPaths.Count > 0)
            {
                _sceneListContainer.Add(CreateSectionHeader("In Build Settings"));

                for (var i = 0; i < buildPaths.Count; i++)
                    _sceneListContainer.Add(CreateBuildSceneRow(buildPaths[i], i, buildPaths.Count));
            }

            // --- Not In Build section ---
            if (notInBuild.Count > 0)
            {
                _sceneListContainer.Add(CreateSectionHeader("Not In Build"));

                foreach (var path in notInBuild)
                    _sceneListContainer.Add(CreateAvailableSceneRow(path));
            }
        }

        private static VisualElement CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.fontSize = 11;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.55f, 0.55f, 0.55f);
            header.style.letterSpacing = 1;
            header.style.marginTop = 10;
            header.style.marginBottom = 4;
            header.style.paddingLeft = 4;
            return header;
        }

        private VisualElement CreateBuildSceneRow(string scenePath, int index, int total)
        {
            var row = CreateBaseRow(scenePath, true);

            // Index badge
            var badge = new Label(index.ToString());
            badge.style.width = 22;
            badge.style.height = 22;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.fontSize = 11;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = new Color(0.85f, 0.95f, 0.85f);
            badge.style.backgroundColor = new Color(0.22f, 0.50f, 0.22f);
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.marginRight = 8;
            row.Insert(0, badge);

            // Reorder buttons
            var reorderBlock = new VisualElement();
            reorderBlock.style.flexDirection = FlexDirection.Row;
            reorderBlock.style.marginLeft = 6;

            var upBtn = CreateArrowButton("\u25B2", index > 0, () =>
            {
                SwapBuildScenes(index, index - 1);
                Refresh();
            });
            reorderBlock.Add(upBtn);

            var downBtn = CreateArrowButton("\u25BC", index < total - 1, () =>
            {
                SwapBuildScenes(index, index + 1);
                Refresh();
            });
            reorderBlock.Add(downBtn);

            row.Add(reorderBlock);

            // Remove toggle
            var toggle = new Toggle();
            toggle.value = true;
            toggle.style.marginLeft = 6;
            var capturedPath = scenePath;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    RemoveSceneFromBuild(capturedPath);
                    Refresh();
                }
            });
            row.Add(toggle);

            return row;
        }

        private VisualElement CreateAvailableSceneRow(string scenePath)
        {
            var row = CreateBaseRow(scenePath, false);

            // Grey dot placeholder to align with badge column
            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.borderTopLeftRadius = 4;
            dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4;
            dot.style.borderBottomRightRadius = 4;
            dot.style.marginRight = 8;
            dot.style.backgroundColor = new Color(0.45f, 0.45f, 0.45f);
            row.Insert(0, dot);

            // Add toggle
            var toggle = new Toggle();
            toggle.value = false;
            toggle.style.marginLeft = 6;
            var capturedPath = scenePath;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    AddSceneToBuild(capturedPath);
                    Refresh();
                }
            });
            row.Add(toggle);

            return row;
        }

        private VisualElement CreateBaseRow(string scenePath, bool isInBuild)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.marginBottom = 2;
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;
            row.style.backgroundColor = isInBuild
                ? new Color(0.16f, 0.30f, 0.16f)
                : new Color(0.22f, 0.22f, 0.22f);

            var hoverColor = isInBuild
                ? new Color(0.20f, 0.38f, 0.20f)
                : new Color(0.28f, 0.28f, 0.28f);
            var normalColor = row.style.backgroundColor.value;

            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = hoverColor);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = normalColor);

            var capturedOpenPath = scenePath;
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Toggle || evt.target is VisualElement ve && ve.GetFirstAncestorOfType<Toggle>() != null)
                    return;
                if (evt.target is Button || evt.target is VisualElement ve2 && ve2.GetFirstAncestorOfType<Button>() != null)
                    return;

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(capturedOpenPath);
            });

            // Scene name + path
            var textBlock = new VisualElement();
            textBlock.style.flexGrow = 1;
            textBlock.style.flexShrink = 1;
            textBlock.style.overflow = Overflow.Hidden;

            var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            var nameLabel = new Label(sceneName);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = isInBuild
                ? new Color(0.7f, 1f, 0.7f)
                : new Color(0.78f, 0.78f, 0.78f);
            textBlock.Add(nameLabel);

            var pathLabel = new Label(scenePath);
            pathLabel.style.fontSize = 10;
            pathLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            pathLabel.style.marginTop = 1;
            textBlock.Add(pathLabel);

            row.Add(textBlock);

            return row;
        }

        private static Button CreateArrowButton(string label, bool enabled, System.Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.style.width = 24;
            btn.style.height = 20;
            btn.style.fontSize = 9;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.SetEnabled(enabled);
            return btn;
        }

        private static void SwapBuildScenes(int indexA, int indexB)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (indexA < 0 || indexA >= scenes.Count || indexB < 0 || indexB >= scenes.Count)
                return;

            (scenes[indexA], scenes[indexB]) = (scenes[indexB], scenes[indexA]);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddSceneToBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(s => s.path == scenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void RemoveSceneFromBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
