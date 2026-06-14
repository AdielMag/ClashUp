using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasOptimization : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform rectTransform;
    private Camera mainCamera;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (mainCamera == null) return;

        // Determine if Canvas is visible in the current camera view
        var viewportPoint = mainCamera.WorldToViewportPoint(rectTransform.position);
        bool isVisible = viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                         viewportPoint.y >= 0f && viewportPoint.y <= 1f;

        // Enable/disable the entire GameObject to save performance
        gameObject.SetActive(isVisible);
    }
}