using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TrainingRankingViewBase : MonoBehaviour
{
    [SerializeField] protected GameObject _rankingTableCellPrefab;
    [SerializeField] protected Transform _rankingTableRoot;

    public void PopulateRankingTable(List<TrainingResult> results)
    {
        //Start at 4 to skip the header cells
        for (int i = 4; i < _rankingTableRoot.childCount; i++)
        {
            Destroy(_rankingTableRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            AddCell((i + 1) + ". " + result.PlayerName);
            var (h, m, s) = CommonUtils.SecToHMS((int)Mathf.Floor(result.TimeInSeconds));
            AddCell(string.Format("{0:00}:{1:00}:{2:00}", h, m, s));
            AddCell(result.GetExclusiveTrainingParameterString());
            AddCell(string.Format("{0:0.00}/10", result.Score));

        }
    }

    protected virtual void AddCell(string text)
    {
        var cellObj = Instantiate(_rankingTableCellPrefab, _rankingTableRoot);
        cellObj.GetComponent<TextMeshProUGUI>().text = text;
    }
}