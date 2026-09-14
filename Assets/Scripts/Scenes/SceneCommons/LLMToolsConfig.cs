using UnityEngine;

[CreateAssetMenu(fileName="LLMToolsConfig", menuName="LoGiViT/AI/LLM Tools Config")]
public class LLMToolsConfig : ScriptableObject
{
    [TextArea(10, 50)]
    public string toolsJson;
}
