using UnityEngine;

public class DebugOptionsController : MonoBehaviour
{

    [SerializeField] private GameObject[] objectsToToggle;

    public void ToggleObjects()
    {
        for(int i=0; i<objectsToToggle.Length; i++)
        {
            objectsToToggle[i].SetActive(!objectsToToggle[i].activeSelf);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            ToggleObjects();
        }
    }
}