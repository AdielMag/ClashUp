using System.IO;
using System.Linq;
using ClashUp.Shared.Abilities;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public sealed class AbilityGraphEditorWindow : EditorWindow
    {
        private AbilityGraphView _graphView;
        private VisualElement _emptyState;
        private string _currentFilePath;

        [MenuItem("Tools/Ability Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<AbilityGraphEditorWindow>("Ability Editor");
            window.minSize = new Vector2(800, 600);
        }

        private void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            toolbar.style.paddingLeft = 4; toolbar.style.paddingRight = 4;
            toolbar.style.paddingTop = 2; toolbar.style.paddingBottom = 2;
            toolbar.Add(new Button(NewGraph)          { text = "New" });
            toolbar.Add(new Button(ShowAbilityBrowser){ text = "Browse" });
            toolbar.Add(new Button(LoadJson)          { text = "Load JSON" });
            toolbar.Add(new Button(SaveJson)          { text = "Save JSON" });
            rootVisualElement.Add(toolbar);

            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.position = Position.Relative;
            rootVisualElement.Add(container);

            _graphView = new AbilityGraphView();
            _graphView.style.position = Position.Absolute;
            _graphView.style.left = 0; _graphView.style.right = 0;
            _graphView.style.top = 0; _graphView.style.bottom = 0;
            _graphView.RegisterCallback<ContextualMenuPopulateEvent>(_graphView.BuildContextMenu);
            container.Add(_graphView);

            _emptyState = BuildEmptyState();
            container.Add(_emptyState);
        }

        private VisualElement BuildEmptyState()
        {
            var root = new VisualElement();
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0;
            root.style.top = 0; root.style.bottom = 0;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            var label = new Label("No Ability Loaded");
            label.style.fontSize = 22;
            label.style.color = new Color(0.5f, 0.5f, 0.5f);
            label.style.marginBottom = 20;
            root.Add(label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var btnNew = new Button(NewGraph) { text = "New Ability" };
            StyleEmptyStateButton(btnNew);
            row.Add(btnNew);

            var btnLoad = new Button(LoadJson) { text = "Load Ability..." };
            StyleEmptyStateButton(btnLoad);
            btnLoad.style.marginLeft = 12;
            row.Add(btnLoad);

            root.Add(row);
            return root;
        }

        private static void StyleEmptyStateButton(Button btn)
        {
            btn.style.width = 140;
            btn.style.height = 38;
            btn.style.fontSize = 13;
        }

        private void ShowGraph()
        {
            _emptyState.style.display = DisplayStyle.None;
        }

        private void NewGraph()
        {
            _graphView.ClearGraph();
            _graphView.CreateRootNode();
            _currentFilePath = null;
            titleContent = new GUIContent("Ability Editor");
            ShowGraph();
        }

        private void LoadJson()
        {
            string path = EditorUtility.OpenFilePanel("Load Ability JSON", ServerAbilityDataPath(), "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var def = JsonConvert.DeserializeObject<AbilityDefinition>(json, JsonSettings());
                if (def == null) { Debug.LogError("Failed to parse ability JSON."); return; }
                AbilityGraphSerializer.DeserializeToGraph(_graphView, def);
                _currentFilePath = path;
                titleContent = new GUIContent($"Ability Editor — {def.Id.Value}");
                ShowGraph();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load ability: {ex.Message}");
            }
        }

        private void SaveJson()
        {
            var def = AbilityGraphSerializer.SerializeGraph(_graphView);
            if (def == null) { Debug.LogError("Graph has no root node."); return; }

            string defaultPath = string.IsNullOrEmpty(_currentFilePath)
                ? Path.Combine(ServerAbilityDataPath(), $"ability_{def.Id.Value}.json")
                : _currentFilePath;

            string path = EditorUtility.SaveFilePanel("Save Ability JSON", Path.GetDirectoryName(defaultPath),
                Path.GetFileName(defaultPath), "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var json = JsonConvert.SerializeObject(def, Formatting.Indented, JsonSettings());
                File.WriteAllText(path, json);
                _currentFilePath = path;
                titleContent = new GUIContent($"Ability Editor — {def.Id.Value}");
                Debug.Log($"Ability saved to {path}");
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save ability: {ex.Message}");
            }
        }

        private void ShowAbilityBrowser()
        {
            var serverDir = ServerAbilityDataPath();
            if (!Directory.Exists(serverDir))
            {
                Debug.LogWarning($"Ability data directory not found: {serverDir}");
                return;
            }

            var files = Directory.GetFiles(serverDir, "ability_*.json", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToArray();

            if (files.Length == 0)
            {
                Debug.LogWarning("No ability files found (looking for ability_*.json).");
                return;
            }

            var menu = new GenericMenu();
            foreach (var file in files)
            {
                var relativePath = file.Substring(serverDir.Length + 1).Replace('\\', '/');
                var menuPath = relativePath.Replace(".json", "").Replace("ability_", "");
                var fileCopy = file;
                menu.AddItem(new GUIContent(menuPath), false, () => LoadAbilityFile(fileCopy));
            }
            menu.ShowAsContext();
        }

        private void LoadAbilityFile(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                var def = JsonConvert.DeserializeObject<AbilityDefinition>(json, JsonSettings());
                if (def == null) { Debug.LogError("Failed to parse ability JSON."); return; }
                AbilityGraphSerializer.DeserializeToGraph(_graphView, def);
                _currentFilePath = path;
                titleContent = new GUIContent($"Ability Editor — {def.Id.Value}");
                ShowGraph();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load ability: {ex.Message}");
            }
        }

        private static string AbilityDataPath() =>
            Path.Combine(Application.dataPath, "Core", "Gameplay", "Content", "Abilities");

        private static string ServerAbilityDataPath()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",".."));
            return Path.Combine(projectRoot, "src", "Server", "ClashUp.GameServer", "Abilities", "Data");
        }

        private static JsonSerializerSettings JsonSettings() => new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };
    }
}
