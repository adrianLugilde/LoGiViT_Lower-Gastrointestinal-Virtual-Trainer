using System.Collections.Generic;
using UnityEngine;

namespace LargeIntestine
{
    [CreateAssetMenu(menuName = "LoGiViT/Large Intestine/Mesh Profile Catalog")]
    public class SplineMeshProfileCatalog : ScriptableObject
    {
        public const string ResourcesPath = "SplineMeshProfileCatalog";

        [SerializeField] private List<SplineMeshProfile> _profiles = new List<SplineMeshProfile>();

        public SplineMeshProfile GetById(string id) =>
            _profiles.Find(p => p != null && p.Id == id);

        public IReadOnlyList<SplineMeshProfile> GetAll() => _profiles;

        public static SplineMeshProfileCatalog Load() =>
            Resources.Load<SplineMeshProfileCatalog>(ResourcesPath);

#if UNITY_EDITOR
        public void Add(SplineMeshProfile profile)
        {
            if (profile == null || _profiles.Contains(profile)) return;
            _profiles.Add(profile);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void Remove(SplineMeshProfile profile)
        {
            if (_profiles.Remove(profile))
                UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
