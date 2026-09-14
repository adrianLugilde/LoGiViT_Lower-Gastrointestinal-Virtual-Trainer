using UnityEditor;
using UnityEngine;

public class VRHandsForceEnable : MonoBehaviour
{
    public GameObject leftController;
    public GameObject rightController;
    public GameObject leftHand;
    public GameObject rightHand;

    void Start()
    {
        ShowHands();
    }


    void ShowHands()
    {
        leftController.SetActive(false);
        rightController.SetActive(false);
        leftHand.SetActive(true);
        rightHand.SetActive(true);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(VRHandsForceEnable))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            VRHandsForceEnable myScript = (VRHandsForceEnable)target;
            if (GUILayout.Button("ShowHands"))
            {
                myScript.ShowHands();
            }
        }
    }
#endif
}
