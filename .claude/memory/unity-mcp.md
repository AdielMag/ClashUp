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
- `scene-open`: works reliably with `assetPath` (e.g. `"Assets/Core/Lobby/Content/Scenes/Lobby.unity"`). Use `script-execute` fallback only if it fails.
- `scene-set-active`: can fail on scenes that are already active/only scene — use script-execute fallback
- `scene-create`: `setupMode: "EmptyScene"` creates a truly empty scene (no camera/light)
- `assets-create-folder`: can fail with null ref if parent doesn't exist — verify parent folders first

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

## When to Use MCP vs Editor Scripts
- **MCP first**: For one-time setup tasks (creating scenes, modifying build settings, adding components)
- **Editor scripts**: Only when the setup needs to be repeatable by other team members without MCP
- User explicitly prefers MCP over manual steps — never tell user to run a menu item if MCP can do it
- **Proactive component wiring**: After introducing a new MonoBehaviour component, immediately use MCP to add it to all relevant existing scene GameObjects — don't wait for the user to ask. This is part of "automate everything".
