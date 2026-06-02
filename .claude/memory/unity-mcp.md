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
- `scene-open`: unreliable for reopening scenes — use `script-execute` with `EditorSceneManager.OpenScene()` instead
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

## When to Use MCP vs Editor Scripts
- **MCP first**: For one-time setup tasks (creating scenes, modifying build settings, adding components)
- **Editor scripts**: Only when the setup needs to be repeatable by other team members without MCP
- User explicitly prefers MCP over manual steps — never tell user to run a menu item if MCP can do it
