using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HorizontalScrollerPrefabCreator
{
    private const string PrefabPath = "Assets/Resources/Pages/HorizontalScroller.prefab";

    static HorizontalScrollerPrefabCreator()
    {
        if (AssetDatabase.AssetPathToGUID(PrefabPath) != "")
            return;

        EditorApplication.delayCall += CreateHorizontalScrollerPrefab;
    }

    private static void CreateHorizontalScrollerPrefab()
    {
        if (AssetDatabase.AssetPathToGUID(PrefabPath) != "")
            return;

        System.IO.Directory.CreateDirectory("Assets/Resources/Pages");

        GameObject root = new GameObject("HorizontalScroller");
        root.AddComponent<RectTransform>();

        ScrollRect scrollRect = root.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        HorizontalLayoutGroup layoutGroup = root.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.spacing = 10f;
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter sizeFitter = root.AddComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        AssetDatabase.Refresh();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[HorizontalScrollerPrefabCreator] Prefab saved to {PrefabPath}");
    }
}