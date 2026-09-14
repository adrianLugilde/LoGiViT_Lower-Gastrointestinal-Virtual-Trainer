using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ScrollRectCancelDragFix : MonoBehaviour, IPointerUpHandler
{
    private ScrollRect _scrollRect;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("Pointer up detected by ScrollRectCancelDragFix.");
        if (_scrollRect != null)
        {
            Debug.Log("Pointer up detected, stopping ScrollRect movement.");
            _scrollRect.StopMovement();
            _scrollRect.OnEndDrag(eventData);
        }
    }
}
