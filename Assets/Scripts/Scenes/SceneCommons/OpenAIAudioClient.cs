using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using UnityEditor;
using System.IO;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;


public class OpenAIAudioClient : MonoBehaviour
{
    [SerializeField] private string llmModel = "gpt-4o-mini";
    [SerializeField] private string ttsModel = "tts-1";
    [SerializeField] private string sttModel = "whisper-1";
    [SerializeField] private TtsVoice ttsVoice = TtsVoice.alloy;
    [SerializeField] private bool isTTSEnabled = false;
    [SerializeField] private bool isDebugPromptEnabled = false;
    private string llmApiEndpoint = "https://api.openai.com/v1/chat/completions";
    private string ttsApiEndpoint = "https://api.openai.com/v1/audio/speech";
    private string sttApiEndpoint = "https://api.openai.com/v1/audio/transcriptions";
    public string apiKeyFilePath = "";
    private string apiKey = "";
    private AudioSource audioSource;
    private Queue<string> audioQueue = new Queue<string>();
    private bool isPlayingAudio = false;

    [Header(" Events ")]
    public Action<string, bool> OnLLMResponseReceived;
    public Action<string> OnSTTResponseReceived;
    public Action<bool> TTSEnableChanged;
    public string TestToolsUserString = "Start the training";
    public string TestToolsSystemString = "You are an AI assistant that controls a training simulator.\nYou can only act by calling one of the available functions.\nIf a user asks you to do something that matches one, call that function with correct arguments.\nIf you’re unsure which function fits, ask for clarification.\nOutput only valid tool calls or clarifying questions.";

    [SerializeField] private LLMToolsConfig toolsConfig;

    private void Awake()
    {
        //LocalizationSettings.InitializationOperation.WaitForCompletion() if want to load locale tables at begining
        //or use preoloading https://docs.unity3d.com/Packages/com.unity.localization@1.4/manual/StringTables.html#preloading
        apiKeyFilePath = PlayerPrefs.GetString("API_KEY_PATH");
        LoadApiKey();
        audioSource = GetComponent<AudioSource>();
    }

    private void LoadApiKey()
    {
        try
        {
            if (File.Exists(apiKeyFilePath))
            {
                apiKey = File.ReadAllText(apiKeyFilePath).Trim();
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "API key loaded successfully.", Debugger.MessageType.Log);
            }
            else
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "API key file not found at path: " + apiKeyFilePath, Debugger.MessageType.Error);
            }
        }
        catch (Exception e)
        {
            Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "Error reading API key file: " + e.Message, Debugger.MessageType.Error);
        }
    }

    public void SendSupervisorRequest(string supervisorPrompt, string supervisorSystemPropmpt)
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"Application Prompt: {supervisorPrompt}", Debugger.MessageType.Log);
        SendLLMRequest(supervisorPrompt, supervisorSystemPropmpt);
    }

    public void SendLLMRequest(string userPrompt, string systemPrompt)
    {
        StartCoroutine(SendJsonRequest(GetLLMRequestJsonBody(userPrompt, systemPrompt), llmApiEndpoint, OnLLMResponse));
    }

    public void SendLLMRequestWithTools(string userPrompt, string systemPrompt)
    {
        Debug.Log("sending llm request with tools:\n " + GetLLMRequestJsonBodyWithTools(userPrompt, systemPrompt, MakeTools()).ToString());
        StartCoroutine(SendJsonRequest(GetLLMRequestJsonBodyWithTools(userPrompt, systemPrompt, MakeTools()), llmApiEndpoint, OnLLMToolsResponse));
    }

    public void SendTTSRequest(string text)
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"TTS REQUEST: {text}", Debugger.MessageType.Log);
        StartCoroutine(SendJsonRequest(GetTTSRequestJsonBody(text), ttsApiEndpoint, OnTTSResponse));
    }

    public void SendSTTRequest(AudioClip audioClip)
    {
        StartCoroutine(SendFormRequest(GetSTTRequestData(audioClip), sttApiEndpoint, OnSTTResponse));
    }


    private JObject GetLLMRequestJsonBody(string userPrompt, string systemPrompt)
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"SENT TO OPEN AI: {userPrompt}", Debugger.MessageType.Log);
        JObject jsonBody = new JObject(
            new JProperty("model", llmModel),
            new JProperty("messages",
                new JArray(
                    new JObject(
                        new JProperty("role", "system"),
                        new JProperty("content", systemPrompt)
                    ),
                    new JObject(
                        new JProperty("role", "user"),
                        new JProperty("content", userPrompt)
                    )
                )
            )
        );

        return jsonBody;
    }

    private JObject GetLLMRequestJsonBodyWithTools(string userPrompt, string systemPrompt, JArray tools, string toolChoice = "auto")
    {
        var jsonBody = new JObject(
            new JProperty("model", llmModel),
            new JProperty("messages", new JArray(
                new JObject(
                    new JProperty("role", "system"),
                    new JProperty("content", systemPrompt)
                ),
                new JObject(
                    new JProperty("role", "user"),
                    new JProperty("content", userPrompt)
                )
            )),
            // <-- Add the tools array here
            new JProperty("tools", tools),
            // optional: let the model decide, force none, or force a specific tool by name
            new JProperty("tool_choice", toolChoice) // "auto" | "none" | JObject { type:"function", function:{ name:"StartTraining"} }
        );

        return jsonBody;
    }

    private JArray MakeTools()
    {
        JObject Fn(string name, string description, JObject parameters)
            => new JObject(
                new JProperty("type", "function"),
                new JProperty("function", new JObject(
                    new JProperty("name", name),
                    new JProperty("description", description),
                    new JProperty("parameters", parameters)
                ))
            );

        JObject NoArgs()
            => new JObject(new JProperty("type", "object"),
                           new JProperty("properties", new JObject()),
                           new JProperty("additionalProperties", false));

        JObject BoolArg(string arg) => new JObject(
            new JProperty("type", "object"),
            new JProperty("properties", new JObject(
                new JProperty(arg, new JObject(new JProperty("type", "boolean")))
            )),
            new JProperty("required", new JArray(arg)),
            new JProperty("additionalProperties", false)
        );

        JObject SaveArgs() => new JObject(
            new JProperty("type", "object"),
            new JProperty("properties", new JObject(
                new JProperty("name", new JObject(
                    new JProperty("type", "string"),
                    new JProperty("minLength", 1)
                ))
            )),
            new JProperty("required", new JArray("name")),
            new JProperty("additionalProperties", false)
        );

        return new JArray(
            Fn("StartTraining", "Begin a training session.", NoArgs()),
            Fn("PauseTraining", "Pause the current session.", NoArgs()),
            Fn("ResumeTraining", "Resume a paused session.", NoArgs()),
            Fn("FinishTraining", "Finish the session and show results.", NoArgs()),
            Fn("RestartTraining", "Reset state and UI for a fresh run.", NoArgs()),
            Fn("SetTimeLimitState", "Enable/disable time limit.", BoolArg("state")),
            Fn("SetCoverageStatsViewState", "Show/hide coverage stats.", BoolArg("state")),
            Fn("SetAIAssistanceEnable", "Enable/disable AI assistance.", BoolArg("isEnabled")),
            Fn("ToggleInsideCoverageView", "Toggle inside-coverage view.", NoArgs()),
            Fn("ToggleAIVoiceAssistance", "Toggle TTS on/off.", NoArgs()),
            Fn("SaveTrainingResults", "Save current run to ranking.", SaveArgs()),
            Fn("ShowRanking", "Open ranking window.", NoArgs()),
            Fn("ExitTrainingRoom", "Request exit from the training room.", NoArgs())
        );
    }


    private JObject GetTTSRequestJsonBody(string text)
    {
        JObject jsonBody = new JObject(
            new JProperty("model", ttsModel),
            new JProperty("voice", ttsVoice.ToString()),
            new JProperty("input", text)
        );

        return jsonBody;
    }

    private WWWForm GetSTTRequestData(AudioClip audioClip)
    {
        //var bytesData = AudioConverter.AudioClipToMP3(audioClip);
        var bytesData = WavUtil.FromAudioClip(audioClip);
        WWWForm form = new WWWForm();
        //form.AddBinaryData("file", bytesData, "recording.mp3", "audio/mpeg");
        form.AddBinaryData("file", bytesData, "recording.wav", "audio/wav");
        form.AddField("model", sttModel);
        return form;
    }

    private void OnLLMToolsResponse(DownloadHandler downloadHandler)
    {
        string jsonResponse = downloadHandler.text;
        var responseObject = JObject.Parse(jsonResponse);
        var choices = responseObject["choices"] as JArray;
        var message = choices[0]["message"] as JObject;
        var toolCalls = message?["tool_calls"] as JArray;
        var fn = toolCalls[0]["function"] as JObject;
        var name = (string)fn?["name"];
        var argJson = (string)fn?["arguments"] ?? "{}";
        Debug.Log("LLM RESPONSE: Function call name: " + name + " args: " + argJson);
    }

    private void OnLLMResponse(DownloadHandler downloadHandler)
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "LLM RESPONSE", Debugger.MessageType.Log);
        string jsonResponse = downloadHandler.text;
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"RECEIVED FROM OPEN AI: {jsonResponse}", Debugger.MessageType.Log);
        var responseObject = JsonUtility.FromJson<OpenAIResponse>(jsonResponse);
        string responseText = responseObject.choices[0].message.content;
        OnLLMResponseReceived?.Invoke(responseText, false);
        if (isTTSEnabled) SendTTSRequest(responseText);
    }

    private void OnTTSResponse(DownloadHandler downloadHandler)
    {
        Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "TTS RESPONSE", Debugger.MessageType.Log);
        byte[] audioBytes = downloadHandler.data;
        var outputPath = Path.Combine(Application.persistentDataPath, $"output_{Guid.NewGuid()}.mp3");
        using (var output = File.Create(outputPath))
        {
            output.Write(audioBytes, 0, audioBytes.Length);
        }

        audioQueue.Enqueue(outputPath);

        if (!isPlayingAudio)
        {
            StartCoroutine(ProcessAudioQueue());
        }
    }

    private IEnumerator ProcessAudioQueue()
    {
        isPlayingAudio = true;

        while (audioQueue.Count > 0)
        {
            string filePath = audioQueue.Dequeue();
            yield return PlayAndDeleteAudio(filePath);
        }

        isPlayingAudio = false;
    }

    private IEnumerator PlayAndDeleteAudio(string filePath)
    {
        using (UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip("file:///" + filePath, AudioType.MPEG))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"Error while loading audio clip: {webRequest.error}", Debugger.MessageType.Error);
                yield break;
            }

            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(webRequest);
            if (audioClip == null)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "Failed to load AudioClip.", Debugger.MessageType.Error);
                yield break;
            }

            audioSource.clip = audioClip;
            audioSource.loop = false;
            audioSource.Play();

            yield return new WaitWhile(() => audioSource.isPlaying);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "Deleted temporary audio file: " + filePath, Debugger.MessageType.Log);
        }
    }

    private void OnSTTResponse(DownloadHandler downloadHandler)
    {
        JObject jsonResponse = JObject.Parse(downloadHandler.text);
        string audioTranscription = (string)jsonResponse["text"];
        //OnLLMResponseReceived?.Invoke(audioTranscription, true);
        OnSTTResponseReceived?.Invoke(audioTranscription);
    }

    private IEnumerator SendRequest(Func<UnityWebRequest> requestFactory, Action<DownloadHandler> onComplete = null)
    {
        Debug.Log("Sending request to OpenAI...");
        using (UnityWebRequest webRequest = requestFactory())
        {
            webRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "Error: " + webRequest.error, Debugger.MessageType.Error);
            }
            else
            {
                onComplete?.Invoke(webRequest.downloadHandler);
            }
        }
    }

    private IEnumerator SendJsonRequest(JObject jsonBody, string endpoint, Action<DownloadHandler> onComplete = null)
    {
        yield return SendRequest(() =>
        {
            UnityWebRequest webRequest = new UnityWebRequest(endpoint, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody.ToString());
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            return webRequest;
        }, onComplete);
    }

    private IEnumerator SendFormRequest(WWWForm form, string endpoint, Action<DownloadHandler> onComplete = null)
    {
        yield return SendRequest(() =>
        {
            UnityWebRequest webRequest = UnityWebRequest.Post(endpoint, form);
            return webRequest;
        }, onComplete);
    }

    private IEnumerator PlayAudio(string filePath)
    {
        using (UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip("file:///" + filePath, AudioType.MPEG))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, $"Error while loading audio clip: {webRequest.error}", Debugger.MessageType.Error);
                yield break;
            }

            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(webRequest);
            if (audioClip == null)
            {
                Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "Failed to load AudioClip.", Debugger.MessageType.Error);
                yield break;
            }

            audioSource.clip = audioClip;
            audioSource.loop = false;
            audioSource.Play();

            yield return new WaitWhile(() => audioSource.isPlaying);
        }
    }

    private IEnumerator PlayAudio(AudioClip audioClip)
    {
        audioSource.clip = audioClip;
        audioSource.loop = false;
        audioSource.Play();

        yield return new WaitWhile(() => audioSource.isPlaying);
    }

    public void ToggleTTS()
    {
        if (isTTSEnabled)
        {
            audioQueue.Clear();
            audioSource.Stop();
            audioSource.clip = null;
        }
        isTTSEnabled = !isTTSEnabled;
        TTSEnableChanged?.Invoke(isTTSEnabled);
    }

    public bool IsTTSEnabled()
    {
        return isTTSEnabled;
    }


    private void Test()
    {
        SendLLMRequest("Hi", "You are an AI assistant.");
        OnLLMResponseReceived?.Invoke("Hi", true);
    }

    private void ToolsTest()
    {
        SendLLMRequestWithTools(TestToolsUserString, TestToolsSystemString);
        /*if (toolsConfig != null)
        {

        }
        else
        {
            Debugger.PrintMethodCall(Debugger.GetCurrentMethodName(), this, "Tools config is not assigned.", Debugger.MessageType.Warn);
        }*/
    }

    [Serializable]
    public class OpenAIResponse
    {
        public Choice[] choices;
    }

    [Serializable]
    public class Choice
    {
        public Message message;
    }

    [Serializable]
    public class Message
    {
        public string content;
    }

    private enum TtsVoice
    {
        alloy,
        echo,
        fable,
        onyx,
        nova,
        shimmer
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(OpenAIAudioClient))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            OpenAIAudioClient myScript = (OpenAIAudioClient)target;
            if (GUILayout.Button("Test"))
            {
                myScript.Test();
            }
            else if (GUILayout.Button("Tools test"))
            {
                myScript.ToolsTest();
            }
        }
    }
#endif
}