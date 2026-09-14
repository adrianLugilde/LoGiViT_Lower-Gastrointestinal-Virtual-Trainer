using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomDelegates
{
    public delegate void DefaultDelegate();
    public delegate void DefaultIntDelegate(int value);
    public delegate void OnConfirm(string path = null, FileManager.OnFileOperationDone onFileOpDone = null);
}
