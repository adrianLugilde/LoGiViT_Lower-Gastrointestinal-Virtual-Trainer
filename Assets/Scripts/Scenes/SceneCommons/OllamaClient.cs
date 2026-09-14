using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.Events;


public class OllamaClient : MonoBehaviour
{
    public string llm = "llama3.2";
    private string ollamaUrl = "http://localhost:11434/api/generate"; // Ollama API URL

    [Header(" Events ")]
    public UnityEvent<string, bool> onResponseReceived;


    public void SendRequest(string userPrompt)
    {
        // Define a system prompt (or persona) for the model
        string systemPrompt = "You are a colonscopy expert. You are supervising a student doing a colonoscopy coverage training. The objective of the training is to visualize most of the surface of the large intestine walls. The student will give information about the training progress. You have to offer suggestions to the student based on the given information to promote the student improvement. Give suggestions in a single paragraph of less than 50 words, and do not expect answers from the student.";
        string fullPrompt = systemPrompt + " " + userPrompt;
        StartCoroutine(SendPromptToOllama(fullPrompt));
    }

    private IEnumerator SendPromptToOllama(string prompt)
    {
        // Create the request data with "stream": false
        string jsonBody = "{\"model\": \"" + llm + "\", \"prompt\": \"" + prompt + "\", \"stream\": false}";

        using (UnityWebRequest webRequest = new UnityWebRequest(ollamaUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error: " + webRequest.error);
            }
            else
            {
                string responseTextContent = webRequest.downloadHandler.text;
                Debug.Log("Full JSON Response: " + responseTextContent);

                JObject jsonResponse = JObject.Parse(responseTextContent);
                string modelResponse = (string)jsonResponse["response"];  // Extract the "response" field

                Debug.Log("Extracted Response: " + modelResponse);

                onResponseReceived?.Invoke(modelResponse, false);
            }
        }
    }
    private void Test()
    {
        SendRequest("Hi");
        onResponseReceived?.Invoke("Hi", true);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(OllamaClient))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            OllamaClient myScript = (OllamaClient)target;
            if (GUILayout.Button("Test"))
            {
                myScript.Test();
            }
        }
    }
#endif
}
