using Dummiesman;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class ObjLoaderFromFile : MonoBehaviour
{
    public string filePath;
    public bool load = false;
    private GameObject loadedObject;


    // Update is called once per frame
    void Update()
    {
        if(load)
        {
            load = false;
            if (!File.Exists(filePath))
            {
                Debug.LogError("File doesn't exist.");
            }
            else
            {
                if (loadedObject != null)
                    Destroy(loadedObject);
                loadedObject = new OBJLoader().Load(filePath);
            }
        }
    }
}
