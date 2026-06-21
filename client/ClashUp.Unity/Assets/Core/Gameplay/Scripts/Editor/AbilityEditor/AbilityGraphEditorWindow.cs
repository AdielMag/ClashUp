using System.IO;
using System.Linq;
using ClashUp.Shared.Abilities;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public sealed class AbilityGraphEditorWindow : EditorWindow
    {
        private const string UssPath =
            "Assets/Core/Gameplay/Scripts/Editor/AbilityEditor/AbilityEditor.uss";

        private AbilityGraphView _graphView;
        private VisualElement _emptyState;
        private string _currentFilePath;

        // Toolbar identity chip
        private Label _titleLabel;
        private Label _identityId;
        private Label _identitySub;
        private VisualElement _identityIconHost;

        [MenuItem("Tools/Ability Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<AbilityGraphEditorWindow>("Ability Editor");
            window.minSize = new Vector2(800, 600);
        }

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList("clashup-ability-editor");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null) rootVisualElement.styleSheets.Add(styleSheet);

            rootVisualElement.Add(BuildTitleBar());
            rootVisualElement.Add(BuildToolbar());

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

            UpdateIdentityFromGraph();
        }

        // ---------------------------------------------------------------- title bar
        private VisualElement BuildTitleBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("ae-titlebar");
            _titleLabel = new Label("Ability Editor");
            _titleLabel.AddToClassList("ae-titlebar__label");
            bar.Add(_titleLabel);
            return bar;
        }

        // ------------------------------------------------------------------ toolbar
        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("ae-toolbar");

            // ---- LEFT zone ----
            var left = new VisualElement();
            left.AddToClassList("ae-toolbar__zone");

            var segment = new VisualElement();
            segment.AddToClassList("ae-segment");
            segment.Add(GhostButton("New", "CreateAddNew", NewGraph));
            segment.Add(GhostButton("Browse", "Search Icon", ShowAbilityBrowser));
            left.Add(segment);

            left.Add(VerticalDivider());

            var loadBtn = new Button(LoadJson) { text = "Load JSON" };
            loadBtn.AddToClassList("ae-btn-outline");
            left.Add(loadBtn);

            var saveBtn = new Button(SaveJson) { text = "Save JSON" };
            saveBtn.AddToClassList("ae-btn-primary");
            left.Add(saveBtn);

            // ---- CENTER zone (ability identity chip) ----
            var center = new VisualElement();
            center.AddToClassList("ae-toolbar__zone");
            center.AddToClassList("ae-toolbar__zone--center");
            center.Add(BuildIdentityChip());

            // ---- RIGHT zone ----
            var right = new VisualElement();
            right.AddToClassList("ae-toolbar__zone");

            var search = new ToolbarSearchField();
            search.AddToClassList("ae-search");
            search.RegisterValueChangedCallback(e => SearchNodes(e.newValue));
            right.Add(search);

            right.Add(VerticalDivider());

            var addBtn = new Button(ShowAddNodeMenu) { text = "+ Add Node" };
            addBtn.AddToClassList("ae-btn-primary");
            right.Add(addBtn);

            toolbar.Add(left);
            toolbar.Add(center);
            toolbar.Add(right);
            return toolbar;
        }

        private VisualElement BuildIdentityChip()
        {
            var chip = new VisualElement();
            chip.AddToClassList("ae-identity");

            _identityIconHost = new VisualElement();
            _identityIconHost.AddToClassList("ae-icon-chip");
            chip.Add(_identityIconHost);

            var text = new VisualElement();
            text.AddToClassList("ae-identity__text");
            _identityId = new Label("—") { };
            _identityId.AddToClassList("ae-identity__id");
            _identitySub = new Label("");
            _identitySub.AddToClassList("ae-identity__sub");
            text.Add(_identityId);
            text.Add(_identitySub);
            chip.Add(text);

            var chevron = new Label("▾"); // ▾
            chevron.AddToClassList("ae-identity__chevron");
            chip.Add(chevron);
            return chip;
        }

        // A ghost button is a composite element (icon + label), NOT a Button:
        // a Button is a TextElement and renders its text in its own box, so any
        // child icon would overlap the label.
        private static VisualElement GhostButton(string label, string builtinIcon, System.Action onClick)
        {
            var btn = new VisualElement();
            btn.AddToClassList("ae-ghost-btn");

            var icon = EditorGUIUtility.IconContent(builtinIcon)?.image as Texture2D;
            if (icon != null)
            {
                var img = new VisualElement();
                img.AddToClassList("ae-ghost-btn__icon");
                img.style.backgroundImage = new StyleBackground(icon);
                btn.Add(img);
            }

            var lbl = new Label(label);
            lbl.AddToClassList("ae-ghost-btn__label");
            btn.Add(lbl);

            btn.AddManipulator(new Clickable(onClick));
            return btn;
        }

        private static VisualElement VerticalDivider()
        {
            var d = new VisualElement();
            d.AddToClassList("ae-divider-v");
            return d;
        }

        // Update the identity chip from the current graph's root node.
        private void UpdateIdentityFromGraph()
        {
            if (_identityId == null) return;
            var root = _graphView?.nodes.ToList().OfType<RootNode>().FirstOrDefault();

            string id = root?.IdField.value;
            if (string.IsNullOrEmpty(id)) id = "—";
            _identityId.text = id;

            if (root != null)
            {
                string display = string.IsNullOrEmpty(root.DisplayNameField.value)
                    ? id : root.DisplayNameField.value;
                _identitySub.text = $"{display} · Cooldown {root.CooldownField.value:0.#}s · Button {root.ButtonIndexField.value}";
            }
            else
            {
                _identitySub.text = "";
            }

            // category icon (Root accent)
            _identityIconHost.Clear();
            var accent = NodeVisuals.Accent(NodeCategory.Root);
            _identityIconHost.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.16f);
            var ic = new NodeIcon(NodeVisuals.Shape(NodeCategory.Root), accent);
            ic.style.position = Position.Absolute;
            ic.style.left = 0; ic.style.right = 0; ic.style.top = 0; ic.style.bottom = 0;
            _identityIconHost.Add(ic);
        }

        // Frame + select nodes whose title/id matches the search term.
        private void SearchNodes(string query)
        {
            if (_graphView == null) return;
            _graphView.ClearSelection();
            if (string.IsNullOrWhiteSpace(query)) return;

            query = query.Trim().ToLowerInvariant();
            foreach (var node in _graphView.nodes.ToList())
            {
                bool match = (node.title?.ToLowerInvariant().Contains(query) ?? false)
                    || (node is RootNode rn && (rn.IdField.value?.ToLowerInvariant().Contains(query) ?? false));
                if (match) _graphView.AddToSelection(node);
            }
            if (_graphView.selection.Count > 0) _graphView.FrameSelection();
        }

        private void ShowAddNodeMenu()
        {
            // Spawn new nodes at the last cursor position over the canvas.
            Vector2 pos = _graphView.LastMousePosition;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Parallel"),   false, () => _graphView.CreateParallelNode(pos));
            menu.AddItem(new GUIContent("Hitbox"),     false, () => _graphView.CreateHitboxNode(pos));
            menu.AddItem(new GUIContent("Projectile"), false, () => _graphView.CreateProjectileNode(pos));
            menu.AddItem(new GUIContent("Spawn"),      false, () => _graphView.CreateSpawnNode(pos));
            menu.ShowAsContext();
        }

        // ----------------------------------------------------------- empty state
        private VisualElement BuildEmptyState()
        {
            var root = new VisualElement();
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0;
            root.style.top = 0; root.style.bottom = 0;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = new Color(0.106f, 0.110f, 0.122f);

            var label = new Label("No Ability Loaded");
            label.style.fontSize = 22;
            label.style.color = new Color(0.5f, 0.5f, 0.5f);
            label.style.marginBottom = 20;
            root.Add(label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var btnNew = new Button(NewGraph) { text = "New Ability" };
            btnNew.AddToClassList("ae-btn-primary");
            row.Add(btnNew);

            var btnLoad = new Button(LoadJson) { text = "Load Ability..." };
            btnLoad.AddToClassList("ae-btn-outline");
            btnLoad.style.marginLeft = 12;
            row.Add(btnLoad);

            root.Add(row);
            return root;
        }

        private void ShowGraph()
        {
            _emptyState.style.display = DisplayStyle.None;
        }

        private void SetTitle(string abilityId)
        {
            string txt = string.IsNullOrEmpty(abilityId)
                ? "Ability Editor" : $"Ability Editor — {abilityId}";
            titleContent = new GUIContent(txt);
            if (_titleLabel != null) _titleLabel.text = txt;
        }

        private void NewGraph()
        {
            _graphView.ClearGraph();
            _graphView.CreateRootNode();
            _currentFilePath = null;
            SetTitle(null);
            ShowGraph();
            UpdateIdentityFromGraph();
        }

        private void LoadJson()
        {
            string path = EditorUtility.OpenFilePanel("Load Ability JSON", ServerAbilityDataPath(), "json");
            if (string.IsNullOrEmpty(path)) return;
            LoadAbilityFile(path);
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
                SetTitle(def.Id.Value);
                UpdateIdentityFromGraph();
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
                SetTitle(def.Id.Value);
                ShowGraph();
                UpdateIdentityFromGraph();
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
