using UnityEngine;

[CreateAssetMenu(fileName = "NewPolypProfile", menuName = "LoGiViT/Polyps/Polyp Profile")]
public class PolypProfile : ScriptableObject
{
    [SerializeField] private string id;
    public string Id => id;

    public string displayName;
    public string description;
    public Mesh mesh;
    public Material[] materials = new Material[1];
    public Sprite meshSprite;
    public Texture image;
    public Polyp.JNETClassification jnetClass;
    public Polyp.ParisClassification parisClass;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString();
    }

    public Polyp CreatePolypInstance()
    {
        return new Polyp(this);
    }
}
