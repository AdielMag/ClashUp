using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class HorizontalScrollerCreator : MonoBehaviour
{
    // This method creates a prefab called HorizontalScroller in Resources/Pages
    [MenuItem("Tools/Create Horizontal Scroller Prefab")]
    public static void CreateHorizontalScrollerPrefab()
    {
        // Create root GameObject
        GameObject root = new GameObject("HorizontalScroller");
        root.AddComponent<RectTransform>(); // needed for LayoutGroup

        // Add ScrollRect component
        ScrollRect scrollRect = root.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;   // enable horizontal scrolling
        scrollRect.vertical = false;    // no vertical scrolling
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.normalizedPosition = Vector2.zero;

        // Add HorizontalLayoutGroup for the content
        HorizontalLayoutGroup layoutGroup = root.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.spacing = 10f;
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);

        // Add ContentSizeFitter to keep the content size flexible
        ContentSizeFitter sizeFitter = root.AddComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Save as a prefab under Resources/Pages
        string prefabPath = "Assets/Resources/Pages/HorizontalScroller.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

        Debug.Log($"[HorizontalScrollerCreator] Prefab saved to {prefabPath}");

        // Optionally destroy the temporary GameObject (keeps the scene clean)
        // EditorApplication.delayCall += () => DestroyImmediate(root);
    }
}