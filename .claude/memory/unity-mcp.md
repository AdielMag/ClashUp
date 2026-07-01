# Unity MCP CLI Usage

## Connection
- MCP config at both `client/ClashUp.Unity/.mcp.json` and root `.mcp.json` (copy, not symlink — Windows symlinks need admin)
- CLI: `npx unity-mcp-cli run-tool <tool-name> --input '<json>'`
- For multi-line input use `--input-file -` with heredoc: `<<'ENDJSON' ... ENDJSON`
- Working dir should be `client/ClashUp.Unity/` when running CLI

## script-execute (Dynamic C# Execution)

Two modes:

**Full code mode** (default, `isMethodBody=false`):
- Must define a complete class with a static method
- Default class: `Script`, default method: `Main()` — override with `className`/`methodName` params
- Can use `UnityEditor` namespace
- Example:
  ```json
  {"csharpCode": "using UnityEditor;\npublic class MyTool\n{\n    public static void Run()\n    {\n        Debug.Log(\"done\");\n    }\n}", "className": "MyTool", "methodName": "Run"}
  ```

**Body-only mode** (`isMethodBody=true`):
- Provide only the method body — tool auto-generates usings, class, and method
- Standard usings (System, UnityEngine, UnityEditor, etc.) are included automatically
- Simpler for quick one-off scripts
- Example:
  ```json
  {"csharpCode": "var mat = AssetDatabase.LoadAssetAtPath<Material>(\"Assets/Foo.mat\");\nmat.color = Color.red;\nEditorUtility.SetDirty(mat);", "isMethodBody": true}
  ```

## Tool Parameter Gotchas
- `gameobject-component-add`: parameter is `componentNames` (not `componentTypes`)
- `gameobject-component-add` **fails silently for complex Unity UI components** (e.g. `UnityEngine.UI.Slider`) — returns "No component names provided" or succeeds but the component isn't added. Use `script-execute` with `go.AddComponent<Slider>()` instead.
- `scene-open`: works reliably with `assetPath` (e.g. `"Assets/Core/Lobby/Content/Scenes/Lobby.unity"`). Use `script-execute` fallback only if it fails.
- `scene-set-active`: can fail on scenes that are already active/only scene — use script-execute fallback
- `scene-create`: `setupMode: "EmptyScene"` creates a truly empty scene (no camera/light)
- `assets-create-folder`: can fail with null ref if parent doesn't exist — verify parent folders first

## Unity Slider Prefab Setup via script-execute

Adding a `UnityEngine.UI.Slider` to a prefab via script:
- Use `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents` pattern
- Set `slider.minValue = 0`, `slider.maxValue = 1`, `slider.interactable = false`
- Set `slider.fillRect` to the Fill child's `RectTransform`
- **Pitfall**: Using an existing Image's RectTransform as `fillRect` may not render — Unity Slider expects the standard `Fill Area > Fill` hierarchy. If the fill doesn't appear, recreate the Image child instead of reusing the existing one.
- Set `slider.handleRect = null` (hide handle for a health bar)

## Recommended Workflow for Scene Setup
1. `scene-create` to create the .unity file
2. `gameobject-create` to add root GameObjects
3. `gameobject-component-add` to attach components (use full namespace)
4. `scene-save` to persist
5. `script-execute` to add to build settings (no built-in tool for this)
6. `script-execute` with `EditorSceneManager.OpenScene()` to restore the original scene

## Modifying SerializedDictionary via script-execute

`SerializedDictionary<K, V>` (from editor-toolbox) stores entries in a `pairs` array — NOT `_keys`/`_values`. Each pair has `Key` and `Value` (capitalized). When adding entries via `SerializedObject`:

```csharp
var mapsProp = so.FindProperty("_maps");               // the SerializedDictionary field
var pairsProp = mapsProp.FindPropertyRelative("pairs"); // array of {Key, Value} pairs
int idx = pairsProp.arraySize;
pairsProp.InsertArrayElementAtIndex(idx);
var pair = pairsProp.GetArrayElementAtIndex(idx);
pair.FindPropertyRelative("Key").stringValue = "my_key";
pair.FindPropertyRelative("Value").objectReferenceValue = myAsset;
so.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(target);
AssetDatabase.SaveAssets();
```

## Creating ScriptableObject Assets with Private Fields
Use `script-execute` with reflection when the SO has private serialized fields:
```csharp
var so = ScriptableObject.CreateInstance("FullTypeName");
var type = so.GetType();
var field = type.GetField("_fieldName", BindingFlags.NonPublic | BindingFlags.Instance);
// For nested struct arrays, use GetNestedType + Array.CreateInstance
field.SetValue(so, value);
AssetDatabase.CreateAsset(so, "Assets/Path/Name.asset");
AssetDatabase.SaveAssets();
```

## Creating Prefabs from Primitives
1. `gameobject-create` with `primitiveType: "Capsule"` (or Cube, Sphere, etc.)
2. `gameobject-component-destroy` to strip the auto-added collider (CapsuleCollider, BoxCollider, etc.)
3. `assets-prefab-create` with `prefabAssetPath` — auto-creates intermediate folders
4. `gameobject-destroy` to clean up the temp scene object

## Modifying Component Fields via MCP
- Use `gameobject-component-modify` with `pathPatches` for targeted field changes
- For Unity object references: `{"typeName": "FullTypeName", "value": {"instanceID": N}}`
- For enums: `{"typeName": "Full.Enum.Type", "value": "EnumValueName"}`
- For Vector2: use `fields` array with `x`/`y` sub-members
- **RectTransform**: use property names (`anchoredPosition`, `sizeDelta`, `anchorMin`, `anchorMax`, `pivot`), NOT serialized field names with `m_` prefix — `m_AnchoredPosition` etc. will 404 with "field not found"
- **Image fill**: `type: "Filled"`, `fillMethod: "Horizontal"`, `fillAmount: 1.0` are all properties, not fields

## Setting Cross-Object Unity Object References (SerializedField pointing to another GameObject/Component)

`gameobject-component-modify` **cannot reliably persist** Unity Object cross-references (e.g. a `[SerializeField] CinemachineCamera _vcam` on Scope A pointing to a component on a different scene GameObject B). The tool reports success but the value stays null after save/reload.

**Fix**: Use `script-execute` with reflection + `EditorUtility.SetDirty`:
```csharp
var holder = GameObject.Find("HolderObjectName");
var comp = holder.GetComponent<My.Namespace.MyComponent>();
var target = GameObject.Find("TargetObjectName").GetComponent<TargetType>();
var field = typeof(My.Namespace.MyComponent).GetField("_fieldName",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
field.SetValue(comp, target);
UnityEditor.EditorUtility.SetDirty(comp);
```
Then call `scene-save`. Always verify with a follow-up script-execute that reads the field back.

## Cinemachine BindingMode Enum Values
- `WorldSpace` = **4** (NOT 0)
- `LockToTargetOnAssign` = 0
- When setting via `pathPatches`, use the integer value: `{"typeName": "Unity.Cinemachine.TargetTracking.BindingMode", "value": 4}`

## assets-refresh Can Time Out (300s) Even When the Refresh Succeeded

Observed: `assets-refresh` occasionally sends no response/progress for 300s and the tool call aborts with a timeout error, even though nothing is actually wrong. Likely cause: the refresh triggers a domain reload, and the response plumbing gets lost across the reload boundary (same family of issue as [[unity-mcp]] "Screenshotting" step 1 — domain reload wipes in-flight state).

**Don't retry blindly or assume the editor is stuck.** Verify via a cheap, unrelated read-only call instead (e.g. `scene-list-opened`) — if it responds normally, the editor is fine and the refresh almost certainly completed; then check `console-get-logs` (Error/Exception filters) for actual compile problems before concluding anything failed.

## Unity MCP (ai-game-developer) Token Expiration Mid-Session

The `ai-game-developer` MCP connector's auth token can expire mid-session (`"requires re-authorization (token expired)"`). This is a non-interactive-session limitation — tell the user their MCP connection needs re-auth (via `claude mcp` or `/mcp`), and note that any pending Unity-side verification (compile check, prefab edit) is blocked until then. It can also silently resolve itself on a later call (observed: a later `assets-refresh` succeeded without any explicit re-auth action) — don't assume the whole session is unrecoverable, just flag the gap and retry the blocked step later.

## assets-refresh Pre-Flight Check

Before calling `assets-refresh`, always check if Unity is in play mode and stop it first. Refreshing while playing can cause compilation to be deferred or ignored until play mode exits.

```bash
# 1. Check state
npx unity-mcp-cli run-tool editor-application-get-state --input '{}'
# 2. If IsPlaying == true, stop play mode first
npx unity-mcp-cli run-tool editor-application-set-state --input '{"isPlaying": false}'
# 3. Then refresh
npx unity-mcp-cli run-tool assets-refresh --input '{}'
```

Or use Skill `editor-application-get-state` → Skill `editor-application-set-state` → Skill `assets-refresh`.

## Verifying Procedural Meshes / Flat XZ Visuals Without Running the Game

To validate code-generated meshes (telegraph/area-flash shapes, etc.) without a running server or play mode:
1. `script-execute` to spawn the objects in the open scene, parented under one root GameObject (e.g. `FlashTestRoot`), giving meaningful positions.
2. `screenshot-isolated` on that root with `cameraView: "Top"`, `isolated: true`, `includeChildren: true`, a solid dark `backgroundColor` — renders flat XZ-plane meshes straight down so you can eyeball the shapes.
3. `script-execute` again to `DestroyImmediate` the root and clean up. **Don't `scene-save`** — leave the open scene untouched.

Gotchas:
- `screenshot-scene-view` / `screenshot-game-view` / `screenshot-camera` are often NOT surfaced via ToolSearch in this environment — only `screenshot-isolated` (and sometimes `screenshot-game-view`) are. Reach for `screenshot-isolated` for object/mesh checks.
- `screenshot-isolated` needs **MeshRenderer-based geometry** — it computes bounds from `Renderer`s. It **cannot frame world-space UGUI** (a `Canvas` with `TextMeshProUGUI`/`Image` uses `CanvasRenderer`, not `Renderer`) and errors `"No Renderers found on target GameObject or its children."`. So you can screenshot a procedural arena (cubes/planes) but NOT a world-space nameplate/points label. With the camera/scene-view tools unavailable, world-space-UGUI features may not be screenshot-verifiable at all — fall back to reasoning + a clean compile.
- A **runtime builder previewed in edit mode** (calling it from `script-execute`) spams the console: `UnityEngine.Object.Destroy` logs `"Destroy may not be called from edit mode! Use DestroyImmediate instead."` once per call (~25 errors for a 25-object build). These are editor-only artifacts (correct at runtime). Make runtime code dual-mode — `if (Application.isPlaying) Object.Destroy(x); else Object.DestroyImmediate(x);` — so it's clean when previewed (see `MapVisualBuilder.StripCollider`).
- `MonoBehaviour.Update()` does NOT run in edit mode, so a self-destruct/fade component (e.g. `AbilityAreaFlash`) persists for the screenshot — pass a long duration anyway and clean up manually.

## Adding a New Serialized Field to an Existing ScriptableObject

When you add a `[SerializeField]`/public field WITH a C# initializer (e.g. `public Color CastFlashColor = new Color(1,0.85f,0.2f,0.6f);`) and `assets-refresh`, existing `.asset` files were observed to pick up the **C# initializer default** (not transparent-black zero). Still:
- **Set the value explicitly** on assets that need a non-default via `assets-modify` `pathPatches` (`{"Path":"FieldName","Value":{"typeName":"UnityEngine.Color","value":{"r":..,"g":..,"b":..,"a":..}}}`) rather than relying on the initializer surviving.
- **Guard in code** (e.g. treat `alpha <= 0` as "unset, use fallback") so old assets that serialized a zero value still behave.

## Hand-authoring Unity YAML assets (.mat / .prefab / ScriptableObject)

When the MCP material/prefab-create tools aren't surfaced via ToolSearch (the env doesn't always expose them), writing the asset YAML + `.meta` directly with the `Write` tool then `assets-refresh` is reliable and deterministic:
- Generate the asset GUID with `python tools/generate-guid.py` and put it in the `.meta` (`guid:`). `.mat` → `NativeFormatImporter` with `mainObjectFileID: 2100000`; `.prefab` → `PrefabImporter`.
- A capsule prefab = copy `Brawler.prefab` (mesh `10208`, builtin guid `...e000...`), change `m_Name` + the `MeshRenderer.m_Materials` ref. Built-in **Standard** shader = `{fileID: 46, guid: 0000000000000000f000000000000000}`; built-in default material = `{fileID: 10303, guid: ...f000...}`.
- A material ref from a prefab uses `{fileID: 2100000, guid: <matGuid>, type: 2}`.
- **Prefab refs inside a ScriptableObject** (e.g. `CharacterPrefabMap._entries`) use `{fileID: <rootGameObjectFileID>, guid: <prefabGuid>, type: 3}`. Two prefabs can share the same internal `fileID` (scoped per-asset) — the `guid` disambiguates.
- **Editing a ScriptableObject's serialized array** (adding an `_entries` item) is more reliable by editing the `.asset` YAML directly with `Edit` than via `assets-modify` serialized-array patches. After editing, `assets-refresh` and read back with `assets-get-data` (use the `paths` param for a scoped, token-cheap read).

## Verifying server-side JSON / Shared model without the game

To smoke-test the real `System.Text.Json` deserialization path (e.g. new `ProjectileConfig`/`TelegraphConfig` fields), spin up a throwaway console under `tools/_verify_*/` with a `ProjectReference` to `ClashUp.Shared.csproj`, deserialize the JSON with the same options as `ServerAbilityStore` (`PropertyNameCaseInsensitive` + `JsonStringEnumConverter`), assert the fields, `dotnet run`, then delete the temp dir (`.artifacts` is gitignored; not in any .sln so it won't affect solution builds).

## Verifying an EditorWindow visually (GraphView/UIToolkit editor tools)

`screenshot-game-view` / `screenshot-scene-view` / `screenshot-isolated` only
capture Game/Scene/object renders — NOT custom `EditorWindow`s. To eyeball a
custom editor (e.g. the Ability Editor):
1. `script-execute` → open it (`AbilityGraphEditorWindow.ShowWindow()`), and drive
   private methods via reflection (`GetMethod(..., NonPublic|Instance).Invoke`) to
   load state, e.g. `LoadAbilityFile(path)`. Grab private fields the same way
   (`_graphView`) to inspect/manipulate.
2. Use the **computer-use** MCP to screenshot the real desktop. Gotchas:
   - Unity runs on a **non-primary monitor** here (`DELL U2518D`) → `request_access`
     for "Unity", then `switch_display "DELL U2518D"` before `screenshot`.
   - Call `open_application "Unity"` right before each click — the desktop shell
     can steal frontmost focus between calls (clicks then error).
   - The screenshot is downscaled (~1456px vs 2560 monitor). Click coords are in
     screenshot space (tool rescales), but do NOT try to derive Unity panel coords
     from screenshot pixels — verify placement by reading back values in-engine
     (`node.GetPosition()`, `viewTransform`) via `script-execute` instead.
3. To check node placement/coords precisely, log `node.GetPosition()` for all nodes
   via `script-execute` rather than measuring the screenshot.

## Screenshotting a Runtime UI Toolkit Screen (Play Mode)

`ScreenCapture.CaptureScreenshot` needs Play Mode to render a runtime `UIDocument` (edit-mode preview doesn't render runtime overlay panels to the Game view). Recipe — each numbered step is a **separate** `script-execute` call:

1. `EditorApplication.isPlaying = true`, then return immediately. **Entering Play Mode triggers a domain reload that wipes static fields and any `EditorApplication.update` subscription** — a single script that does `EditorApplication.update += Tick` and expects `Tick` to keep firing across the play-mode-entry boundary will silently stop after one call (the subscription is gone once `isPlaying` flips). Don't try to drive a multi-frame countdown that straddles entry; just flip the flag and poll separately.
2. Wait a few seconds of wall-clock (Bash `sleep`), then in a **new** script-execute call, confirm `EditorApplication.isPlaying == true` and build the target UI (e.g. `MyScreenUI.Create(...)`). Calls made *after* Play Mode is already active do NOT trigger another domain reload, so normal static state persists fine across these later calls.
3. The normal boot flow (env picker, loading screen, other lobby canvases) is still running underneath and will cover your target UI in the screenshot. Hide it in the same or a follow-up call:
   ```csharp
   foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
   foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
       if (d.gameObject.name != "MyTargetDocName") d.gameObject.SetActive(false);
   ```
   Both passes are needed — legacy UGUI boot screens are `Canvas`-based, but the loading screen is itself a `UIDocument` (UI Toolkit), so disabling only `Canvas` still leaves it covering everything.
4. In another call: `ScreenCapture.CaptureScreenshot(absPath)` (an absolute path under the scratchpad dir; the capture happens at end-of-frame).
5. Poll with a Bash loop (`for i in ...; do [ -f path ] && break; sleep 1; done`) until the file exists, then `Read` the PNG.
6. `EditorApplication.isPlaying = false` to clean up when done.

**Fast CSS iteration bonus**: after this, editing the screen's `.uxml`/`.uss` and calling `assets-refresh` hot-reloads the already-open `UIDocument`'s visual tree with **no domain reload** (non-.cs assets don't force a recompile) — logs `"UI was recreated and no companion MonoBehaviour found, some UI functionality may have been lost."` This is fine for a quick re-screenshot loop, but note it **rebuilds the tree from the raw UXML defaults**, discarding any C# runtime data-binding applied after `Create()` (e.g. `Select()`-populated label text reverts to the UXML's placeholder values). Often convenient anyway since UXML placeholder text/values are usually the mock's own demo data.

## When to Use MCP vs Editor Scripts
- **MCP first**: For one-time setup tasks (creating scenes, modifying build settings, adding components)
- **Editor scripts**: Only when the setup needs to be repeatable by other team members without MCP
- User explicitly prefers MCP over manual steps — never tell user to run a menu item if MCP can do it
- **Proactive component wiring**: After introducing a new MonoBehaviour component, immediately use MCP to add it to all relevant existing scene GameObjects — don't wait for the user to ask. This is part of "automate everything".
