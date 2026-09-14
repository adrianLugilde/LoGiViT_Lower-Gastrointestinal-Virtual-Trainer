using TMPro;
using UnityEngine;

public class RankingTableCell : MonoBehaviour
{
    private TextMeshProUGUI _cellText;

    private void Awake()
    {
        _cellText = GetComponent<TextMeshProUGUI>();
    }
    
    public void SetText(string text)
    {
        _cellText.text = text;
    }
}