using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class DraggableScissorsUI2D :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public RectTransform draggableRect;
    public RectTransform dragArea;
    public ShoeCutMiniGame2D miniGame;
    public bool clampInsideDragArea = true;

    Vector2 startAnchoredPosition;
    bool dragging;

    void Awake()
    {
        CacheReferences();
        if (draggableRect != null)
        {
            startAnchoredPosition =
                draggableRect.anchoredPosition;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        CacheReferences();
        if (eventData.button != PointerEventData.InputButton.Left
            || miniGame == null
            || !miniGame.IsActive)
        {
            return;
        }

        dragging = true;
        draggableRect.SetAsLastSibling();
        miniGame.NotifyScissorsDragStarted();
        MoveToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        MoveToPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        MoveToPointer(eventData);
        dragging = false;
        miniGame?.NotifyScissorsDragEnded();
    }

    public void ResetToStart()
    {
        CacheReferences();
        dragging = false;
        if (draggableRect != null)
        {
            draggableRect.anchoredPosition =
                startAnchoredPosition;
            draggableRect.localRotation =
                Quaternion.identity;
        }
    }

    void MoveToPointer(PointerEventData eventData)
    {
        if (draggableRect == null || dragArea == null)
        {
            return;
        }

        Camera eventCamera =
            eventData.pressEventCamera != null
                ? eventData.pressEventCamera
                : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragArea,
                eventData.position,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        if (clampInsideDragArea)
        {
            Rect areaRect = dragArea.rect;
            Vector2 halfSize =
                Vector2.Scale(
                    draggableRect.rect.size * 0.5f,
                    new Vector2(
                        Mathf.Abs(draggableRect.localScale.x),
                        Mathf.Abs(draggableRect.localScale.y)));
            localPoint.x =
                Mathf.Clamp(
                    localPoint.x,
                    areaRect.xMin + halfSize.x,
                    areaRect.xMax - halfSize.x);
            localPoint.y =
                Mathf.Clamp(
                    localPoint.y,
                    areaRect.yMin + halfSize.y,
                    areaRect.yMax - halfSize.y);
        }

        draggableRect.anchoredPosition = localPoint;
        miniGame?.NotifyScissorsDragged();
    }

    void CacheReferences()
    {
        if (draggableRect == null)
        {
            draggableRect = transform as RectTransform;
        }

        if (dragArea == null && draggableRect != null)
        {
            dragArea = draggableRect.parent as RectTransform;
        }

        if (miniGame == null)
        {
            miniGame =
                GetComponentInParent<ShoeCutMiniGame2D>(true);
        }
    }
}
