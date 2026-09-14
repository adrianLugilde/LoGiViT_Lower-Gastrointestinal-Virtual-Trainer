using UnityEngine;

public class ScrollAreaAutoScroller : MonoBehaviour
{
    private RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void DelayScrollDown()
    {
        Invoke("ScrollDown", .1f);
    }

    private void ScrollDown()
    {
        Vector2 anchoredPosition = rt.anchoredPosition;
        anchoredPosition.y = Mathf.Max(0, rt.sizeDelta.y);
        rt.anchoredPosition = anchoredPosition;
    }
}