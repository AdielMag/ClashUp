using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ScrollRect))]
public class HorizontalSnapScroll : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public int pageCount = 5;
    public float snapThreshold = 0.5f; // fraction of content width to trigger snap
    public float lockAxisThreshold = 0.6f; // when dominant axis exceeds this, lock direction

    private ScrollRect scrollRect;
    private RectTransform contentRect;
    private float pageWidth; // width of one page in content space

    private int currentPage = 0;
    private bool isLocked = false;
    private Vector2 initialPointerPosition;
    private Vector2 contentStartPosition;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        contentRect = scrollRect.content;

        // Assume content is a Horizontal Layout Group with pages
        // Calculate page width based on content size
    }

    void Start()
    {
        // Ensure content size fits pageCount pages horizontally
        // If using LayoutGroup, content size should already be set.
        // For simplicity, we assume each page is equal width.
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        initialPointerPosition = eventData.position;
        contentStartPosition = contentRect.anchoredPosition;
        isLocked = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - initialPointerPosition;

        // Determine dominant axis
        bool lockVertical = Mathf.Abs(delta.y) > delta.x * lockAxisThreshold;
        bool lockHorizontal = !lockVertical;

        if (isLocked && lockVertical)
        {
            // Locked to vertical, ignore horizontal movement
            float y = delta.y;
            float newY = contentStartPosition.y + y;
            contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, newY);
        }
        else if (isLocked && lockHorizontal)
        {
            // Locked to horizontal, ignore vertical movement
            float x = delta.x;
            float newX = contentStartPosition.x + x;
            contentRect.anchoredPosition = new Vector2(newX, contentRect.anchoredPosition.y);
        }
        else
        {
            // Not locked yet, check if we should lock
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * lockAxisThreshold)
            {
                // Predominantly horizontal drag -> lock to horizontal
                isLocked = true;
                scrollRect.velocity = Vector2.zero; // optional: stop momentum
            }
            else if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * lockAxisThreshold)
            {
                // Predominantly vertical drag -> lock to vertical
                isLocked = true;
                scrollRect.velocity = Vector2.zero;
            }

            // Apply movement
            Vector2 newPosition = contentStartPosition + new Vector2(delta.x, delta.y);
            contentRect.anchoredPosition = newPosition;
        }

        // Snap logic: check if we passed a page boundary
        if (!isLocked)
        {
            float viewportWidth = scrollRect.viewport.sizeDelta.x;
            float contentWidth = contentRect.rect.width;
            float pageWidth = contentWidth / pageCount;

            // Determine which page we are on based on content position
            int targetPage = Mathf.FloorToInt(-contentRect.anchoredPosition.x / pageWidth);
            targetPage = Mathf.Clamp(targetPage, 0, pageCount - 1);

            // Check if we should snap
            float offsetFromPageCenter = Mathf.Abs(contentRect.anchoredPosition.x - (targetPage + 0.5f) * pageWidth);
            if (offsetFromPageCenter < snapThreshold * pageWidth)
            {
                // Snap to page
                float snappedX = (targetPage + 0.5f) * pageWidth;
                float newX = contentStartPosition.x + (snappedX - (targetPage + 0.5f) * pageWidth);
                contentRect.anchoredPosition = new Vector2(newX, contentRect.anchoredPosition.y);
                currentPage = targetPage;
            }
        }
    }
}