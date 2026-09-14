using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LoGiViT/Polyps/Polyp Profile Catalog")]
public class PolypProfileCatalog : ScriptableObject
{
    public const string ResourcesPath = "PolypProfileCatalog";

    [SerializeField] private List<PolypProfile> _profiles = new List<PolypProfile>();

    public PolypProfile GetById(string id) =>
        _profiles.Find(p => p != null && p.Id == id);

    public IReadOnlyList<PolypProfile> GetAll() => _profiles;

    public static PolypProfileCatalog Load() =>
        Resources.Load<PolypProfileCatalog>(ResourcesPath);

#if UNITY_EDITOR
    public void Add(PolypProfile profile)
    {
        if (profile == null || _profiles.Contains(profile)) return;
        _profiles.Add(profile);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void Remove(PolypProfile profile)
    {
        if (_profiles.Remove(profile))
            UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
