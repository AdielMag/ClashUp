using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ScrollRect))]
public class CustomScroll : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Scrolling Settings")]
    public int pageCount = 5; // number of vertical pages
    public float snapThreshold = 0.5f; // fraction of page height to trigger snap
    public float lockAxisThreshold = 0.6f; // dominant-axis ratio to lock direction

    private ScrollRect scrollRect;
    private RectTransform contentRect;
    private float pageHeight; // height of one page (in content space)
    private int currentPage = 0; // which page we are currently on
    private bool isLocked = false; // true while we are locked to an axis
    private bool lockVertical = false; // true → only vertical movement allowed
    private bool lockHorizontal = false; // true → only horizontal movement allowed
    private Vector2 initialPointerPosition; // start of drag
    private Vector2 contentStartPosition; // content position at drag start

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        contentRect = scrollRect.content;
    }

    void Start()
    {
        // pageHeight will be calculated in Update (layout may not be final yet)
    }

    #region Drag Handlers ---------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        initialPointerPosition = eventData.position;
        contentStartPosition = contentRect.anchoredPosition;
        isLocked = false;
        lockVertical = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - initialPointerPosition;

        // ---- Determine dominant axis (if not already locked) --------------------
        if (!lockVertical && !lockHorizontal)
        {
            bool verticalDominant = Mathf.Abs(delta.y) > delta.x * lockAxisThreshold;
            bool horizontalDominant = Mathf.Abs(delta.x) > delta.y * lockAxisThreshold;

            if (verticalDominant)
            {
                lockVertical = true;
                lockHorizontal = false;
            }
            else if (horizontalDominant)
            {
                lockVertical = false;
                lockHorizontal = true;
            }
        }

        // ---- Apply movement respecting the lock -------------------------------
        if (lockVertical)
        {
            // Only vertical movement allowed
            float y = delta.y;
            float newY = contentStartPosition.y + y;
            contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, newY);
        }
        else if (lockHorizontal)
        {
            // Only horizontal movement allowed
            float x = delta.x;
            float newX = contentStartPosition.x + x;
            contentRect.anchoredPosition = new Vector2(newX, contentRect.anchoredPosition.y);
        }
        else
        {
            // Not locked yet → apply full delta
            Vector2 newPosition = contentStartPosition + new Vector2(delta.x, delta.y);
            contentRect.anchoredPosition = newPosition;
        }
    }

    #endregion

    void Update()
    {
        // ---- Ensure pageHeight is up‑to‑date ---------------------------------
        if (pageHeight <= 0f && contentRect != null)
        {
            pageHeight = contentRect.rect.height / pageCount;
        }

        // ---- Snap logic ----------------------------------------------------
        if (pageHeight > 0f)
        {
            float offsetFromCenter = Mathf.Abs(contentRect.anchoredPosition.y - (currentPage + 0.5f) * pageHeight);
            if (offsetFromCenter < snapThreshold * pageHeight)
            {
                // Snap to the nearest page
                int targetPage = Mathf.FloorToInt((contentRect.anchoredPosition.y + pageHeight * 0.5f) / pageHeight);
                targetPage = Mathf.Clamp(targetPage, 0, pageCount - 1);
                float snappedY = (targetPage + 0.5f) * pageHeight;
                float correction = snappedY - (currentPage + 0.5f) * pageHeight;
                contentRect.anchoredPosition += new Vector2(0f, correction);
                currentPage = targetPage;
            }
        }
    }
}