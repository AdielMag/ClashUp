# Unity MCP CLI Usage

## Connection
- MCP config at both `client/ClashUp.Unity/.mcp.json` and root `.mcp.json` (copy, not symlink — Windows symlinks need admin)
- CLI: `npx unity-mcp-cli run-tool <tool-name> --input '<json>'`
- For multi-line input use `--input-file -` with heredoc: `<<'ENDJSON' ... ENDJSON`
- Working dir should be `client/ClashUp.Unity/` when running CLI

## script-execute (Dynamic C# Execution)
- **Must** have a class named `Script` with a method named `Main()`
- NOT `Run()`, NOT body-only, NOT arbitrary class names
- Return type is `string` for simple results
- Can use `UnityEditor` namespace for editor operations
- Example:
  ```csharp
  {"csharpCode": "using UnityEditor;\npublic class Script\n{\n    public static string Main()\n    {\n        // your code here\n        return \"result\";\n    }\n}"}
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

## When to Use MCP vs Editor Scripts
- **MCP first**: For one-time setup tasks (creating scenes, modifying build settings, adding components)
- **Editor scripts**: Only when the setup needs to be repeatable by other team members without MCP
- User explicitly prefers MCP over manual steps — never tell user to run a menu item if MCP can do it
- **Proactive component wiring**: After introducing a new MonoBehaviour component, immediately use MCP to add it to all relevant existing scene GameObjects — don't wait for the user to ask. This is part of "automate everything".
