using UnityEditor;
using UnityEngine;

namespace ClashUp.Client.Gameplay.Editor
{
    [CustomEditor(typeof(AbilityVisualRegistry))]
    public sealed class AbilityVisualRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Refresh GUIDs from References", GUILayout.Height(28)))
            {
                var registry = (AbilityVisualRegistry)target;
                var entries = registry.Entries;
                bool changed = false;

                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Config == null) continue;
                    string path = AssetDatabase.GetAssetPath(entries[i].Config);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (entries[i].Guid != guid)
                    {
                        entries[i].Guid = guid;
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(registry);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[AbilityVisualRegistry] Refreshed {entries.Length} entries.");
                }
                else
                {
                    Debug.Log("[AbilityVisualRegistry] All GUIDs already up to date.");
                }
            }
        }
    }
}
