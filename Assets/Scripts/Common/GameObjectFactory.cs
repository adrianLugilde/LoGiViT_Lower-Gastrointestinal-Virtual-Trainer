using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameObjectFactory
{
    // Cache by instance ID - it's unique per GameObject
    private readonly Dictionary<int, GameObject> cache;

    // Lookup table to find instance ID by name and parent
    private readonly Dictionary<(string name, int parentId), int> nameToInstanceId;

    public GameObjectFactory()
    {
        cache = new Dictionary<int, GameObject>();
        nameToInstanceId = new Dictionary<(string, int), int>();
    }

    public GameObject GetOrCreate(string name, Transform parent = null, params Type[] components)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("GameObject name cannot be null or empty");
            return null;
        }

        int parentId = parent != null ? parent.GetInstanceID() : 0;
        var key = (name, parentId);


        // Check if we already have this in cache
        if (nameToInstanceId.TryGetValue(key, out int instanceId))
        {
            if (cache.TryGetValue(instanceId, out var cached) && cached != null)
            {
                return cached;
            }

            // Object was destroyed externally, clean up
            nameToInstanceId.Remove(key);
            cache.Remove(instanceId);
        }

        // Create new GameObject
        GameObject newGo = CommonUtils.Create(name, parent, components);
        int newId = newGo.GetInstanceID();
        cache[newId] = newGo;
        nameToInstanceId[key] = newId;
        return newGo;
    }

    public bool Destroy(GameObject go)
    {
        if (go == null) return false;
        foreach (var kvp in cache)
        {
            if (kvp.Value == go)
                return Destroy(kvp.Key);
        }
        return false;
    }

    public bool Destroy(int instanceId)
    {
        if (cache.TryGetValue(instanceId, out var go))
        {
            cache.Remove(instanceId);

            // Remove from name lookup
            var nameKey = nameToInstanceId.FirstOrDefault(kvp => kvp.Value == instanceId).Key;
            if (nameKey != default)
            {
                nameToInstanceId.Remove(nameKey);
            }

            if (go != null)
            {
                CommonUtils.Destroy(go);
            }
            return true;
        }
        return false;
    }

    public GameObject Get(int instanceId)
    {
        if (cache.TryGetValue(instanceId, out var go) && go != null)
        {
            return go;
        }
        return null;
    }

    public bool Contains(int instanceId)
    {
        return cache.ContainsKey(instanceId) && cache[instanceId] != null;
    }

    public void Clear()
    {
        cache.Clear();
        nameToInstanceId.Clear();
    }

    public void DestroyAll()
    {
        foreach (var go in cache.Values)
        {
            if (go != null)
                CommonUtils.Destroy(go);
        }
        cache.Clear();
    }

    public void CleanupNullReferences()
    {
        var keysToRemove = new List<int>();

        foreach (var kvp in cache)
        {
            if (kvp.Value == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            cache.Remove(key);

            // Remove from name lookup
            var nameKey = nameToInstanceId.FirstOrDefault(kvp => kvp.Value == key).Key;
            if (nameKey != default)
            {
                nameToInstanceId.Remove(nameKey);
            }
        }
    }
}