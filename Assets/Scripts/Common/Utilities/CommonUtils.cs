using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CommonUtils
{

    public delegate void MethodToTest();

    public static void TestMethod(MethodToTest methodToTest, string methodName = "")
    {
        float startTime = Time.realtimeSinceStartup;
        methodToTest();
        Debug.Log(((Time.realtimeSinceStartup - startTime) * 1f) + " seconds for method -> " + /*methodName == "" ? */methodToTest.Method.Name/* : methodName*/);
    }

    public static bool IsInRange(float value, float minValue, float maxValue, bool inclusive = true)
    {
        var res = (value - minValue) * (maxValue - value);
        return inclusive ? res >= 0 : res > 0;
    }

    public static bool IsInRange(int value, int minValue, int maxValue, bool inclusive = true)
    {
        var res = (value - minValue) * (maxValue - value);
        return inclusive ? res >= 0 : res > 0;
    }

    public static GameObject Instantiate(GameObject prefab, Transform parent, string name = null, bool keepTransform = false)
    {
        var res = UnityEngine.Object.Instantiate(prefab, parent);
        res.name = name ?? prefab.name;
        if (!keepTransform)
        {
            res.transform.localPosition = Vector3.zero;
            res.transform.localRotation = Quaternion.identity;
            res.transform.localScale = Vector3.one;
        }
        return res;
    }

    /*public static GameObject Create(string name, GameObject parent = null, params Type[] components)
    {
        var res = new GameObject(name, components);
        if (parent != null) res.transform.parent = parent.transform;
        res.transform.localPosition = Vector3.zero;
        res.transform.localScale = Vector3.one;
        res.transform.localRotation = Quaternion.identity;
        return res;
    }*/

    /*public static GameObject Create(string name, string layerName = null, GameObject parent = null, params Type[] components)
    {
        var res = new GameObject(name, components);
        if (parent != null) res.transform.parent = parent.transform;
        if (layerName != null) res.layer = LayerMask.NameToLayer(layerName);
        res.transform.localPosition = Vector3.zero;
        res.transform.localScale = Vector3.one;
        res.transform.localRotation = Quaternion.identity;
        return res;
    }*/
    

    public static GameObject Create(string name, string layerName = null, Transform parent = null, params Type[] components)
    {
        var res = new GameObject(name, components);
        if (parent != null) res.transform.SetParent(parent);
        if (layerName != null) res.layer = LayerMask.NameToLayer(layerName);
        res.transform.localPosition = Vector3.zero;
        res.transform.localScale = Vector3.one;
        res.transform.localRotation = Quaternion.identity;
        return res;
    }

    public static GameObject Create(string name, GameObject parent = null, params Type[] components)
    {
        return Create(name, parent != null ? parent.transform : null, components);
    }

    public static GameObject Create(string name, string layerName = null, GameObject parent = null, params Type[] components)
    {
        return Create(name, layerName, parent != null ? parent.transform : null, components);
    }

    public static GameObject Create(string name, Transform parent = null, params Type[] components)
    {
         return Create(name, null, parent, components);
    }

    /*public static GameObject Create(string name, Transform parent = null, params Type[] components)
    {
        var res = new GameObject(name, components);
        if (parent != null) res.transform.SetParent(parent);
        res.transform.localPosition = Vector3.zero;
        res.transform.localScale = Vector3.one;
        res.transform.localRotation = Quaternion.identity;
        return res;
    }*/

    public static Vector3 TransformVector(Transform trans, Vector3 vector)
    {
        return trans.rotation * vector + trans.position;
    }

    public static string IEnumerableOfIEnumerablesToString<T>(IEnumerable<IEnumerable<T>> enumerable, string separator = " - ")
    {
        var count = enumerable.Count();
        var res = "";
        for (int i = 0; i < count; i++)
        {
            var item = enumerable.ElementAt(i);
            var subCount = item.Count(); ;
            for (int j = 0; j < subCount; j++)
            {
                res += item.ElementAt(j).ToString() + separator;
            }
            res += Environment.NewLine;
        }
        return res;
    }

    public static void BuildIntIndexArray(int indexCount, out int[] array)
    {
        array = new int[indexCount];
        for (int i = 0; i < indexCount; i++)
        {
            array[i] = i;
        }
    }

    public static float4 ColorToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }

    public static Color Float4ToColor(float4 float4)
    {
        return new Color(float4.x, float4.y, float4.z, float4.w);
    }

    public static (int h, int m, int s) SecToHMS(int total)
    {
        int h = total / 3600;
        total -= h * 3600;
        int m = total / 60;
        int s = total - m * 60;
        return (h, m, s);
    }

    public class CircularBuffer<T>
    {
        private readonly Queue<T> _queue;
        private readonly int _size;

        public CircularBuffer(int size)
        {
            _queue = new Queue<T>(size);
            _size = size;
        }

        public void Add(T obj)
        {
            if (_queue.Count == _size)
            {
                _queue.Dequeue();
            }
            _queue.Enqueue(obj);
        }

        public T GetAt(int index)
        {
            if (index < 0 || index >= _queue.Count)
            {
                throw new System.IndexOutOfRangeException("Index is out of range.");
            }
            return _queue.ToArray()[index];
        }

        public (T, T)? GetLastTwoElements()
        {
            if (_queue.Count >= 2)
            {
                var array = _queue.ToArray();
                return (array[^2], array[^1]);
            }
            return null;
        }

        public List<T> GetLastNElements(int n)
        {
            int countToFetch = Mathf.Min(n, _queue.Count);
            return new List<T>(_queue.ToArray()[^countToFetch..]);
        }

        public int Count => _queue.Count;

        public void Clear()
        {
            _queue.Clear();
        }
    }

    public static GameObject FindByTagInScene(string sceneName, string tag)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;

        var all = GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t.gameObject.scene == scene && t.CompareTag(tag))
                return t.gameObject;
        }
        return null;
    }

    public static void Destroy(GameObject go)
    {
        if (go != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(go);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }

    public static void Destroy(Component comp)
    {
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(comp);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(comp);
        }
    }

    public static void DestroyChildren(GameObject go)
    {
        var childList = go.transform.Cast<Transform>().ToList();
        foreach (Transform childTransform in childList)
        {
            Destroy(childTransform.gameObject);
        }
    }
}