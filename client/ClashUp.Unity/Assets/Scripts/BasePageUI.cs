using UnityEngine;
using UnityEngine.UI;

public class BasePageUI : MonoBehaviour
{
    [SerializeField] private GameObject scrollViewPrefab;
    [SerializeField] private Font titleFont;
    [SerializeField] private float itemSpacing = 10f;

    public Text TitleText { get; private set; }

    public GameObject SetupPage(string pageName, Transform parent)
    {
        if (scrollViewPrefab == null)
        {
            Debug.LogError("scrollViewPrefab not assigned on BasePageUI.");
            return null;
        }

        GameObject scrollObj = Instantiate(scrollViewPrefab, parent);
        scrollObj.name = $"{pageName}_ScrollView";

        ScrollRect scrollRect = scrollObj.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogError($"ScrollRect component missing on {scrollObj.name}");
            return null;
        }

        HorizontalLayoutGroup layoutGroup = scrollObj.GetComponentInChildren<HorizontalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.spacing = itemSpacing;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        }

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(scrollObj.transform, false);
        TitleText = titleObj.AddComponent<Text>();
        TitleText.font = titleFont != null ? titleFont : Font.CreateDynamicFontFromOSFont("Arial", 24);
        TitleText.text = pageName.ToUpper();
        TitleText.color = Color.white;
        TitleText.alignment = TextAnchor.MiddleCenter;
        RectTransform rt = titleObj.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, 100);

        return scrollObj;
    }
}
