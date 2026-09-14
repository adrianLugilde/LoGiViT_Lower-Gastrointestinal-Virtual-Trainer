using UnityEngine;
using System;
using System.IO.Ports;
using UnityEditor;

/*
e  para activar
d para desactivar
+ para avanzar
- para retorceder
+100 o cualquier cantidad para avanzar esa cantidad
-100 o cualquier cantidad para retroceder esa cantidad
*/

public class SerialUSB : MonoBehaviour
{
    [Header("Serial Port Settings")]
    public string portName = "COM3";   // e.g. COM3, /dev/ttyUSB0, /dev/ttyACM0
    public int baudRate = 9600;

    [Header("Outgoing")]
    [Tooltip("Message to send when using the Inspector button or calling SendInspectorMessage().")]
    public string outgoingMessage = "Hello from Unity";

    [Header("Runtime Info (Read-Only)")]
    [SerializeField] private string latestMessage = "";
    [SerializeField] private bool isConnected = false;

    private SerialPort serialPort;

    void Start()
    {
        OpenPort();
    }

    void Update()
    {
        ReadIncomingData();
    }

    public void OpenPort()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            Debug.LogWarning("[SerialUSB] Port already open.");
            isConnected = true;
            return;
        }

        try
        {
            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 10,
                WriteTimeout = 50,
                NewLine = "\n"
            };

            serialPort.Open();
            isConnected = true;

            Debug.Log($"[SerialUSB] Connected to {portName} at {baudRate} baud.");
        }
        catch (Exception e)
        {
            isConnected = false;
            Debug.LogError($"[SerialUSB] Failed to open port {portName}: {e.Message}");
        }
    }

    private void ReadIncomingData()
    {
        if (serialPort == null || !serialPort.IsOpen)
            return;

        try
        {
            // Non-blocking check
            while (serialPort.BytesToRead > 0)
            {
                string line = serialPort.ReadLine();
                latestMessage = line.Trim();
                // If you want: Debug.Log("[SerialUSB] Received: " + latestMessage);
            }
        }
        catch (TimeoutException)
        {
            // Safe to ignore small timeouts
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SerialUSB] Read error: {e.Message}");
        }
    }

    public void WriteToPort(string message)
    {
        if (serialPort == null || !serialPort.IsOpen)
        {
            Debug.LogWarning("[SerialUSB] Cannot send, port not open.");
            return;
        }

        try
        {
            serialPort.WriteLine(message);
            Debug.Log($"[SerialUSB] Sent: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SerialUSB] Write error: {e.Message}");
        }
    }

    // Called by the inspector button
    public void SendInspectorMessage()
    {
        if (string.IsNullOrEmpty(outgoingMessage))
        {
            Debug.LogWarning("[SerialUSB] Outgoing message is empty.");
            return;
        }

        WriteToPort(outgoingMessage);
    }

    public string GetLatestMessage()
    {
        return latestMessage;
    }

    void OnApplicationQuit()
    {
        ClosePort();
    }

    public void ClosePort()
    {
        if (serialPort == null)
            return;

        try
        {
            if (serialPort.IsOpen)
                serialPort.Close();

            Debug.Log("[SerialUSB] Port closed.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SerialUSB] Error closing port: {e.Message}");
        }
        finally
        {
            serialPort = null;
            isConnected = false;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SerialUSB))]
public class SerialUSBEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default fields
        DrawDefaultInspector();

        SerialUSB serialUSB = (SerialUSB)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Send Serial Message"))
        {
            serialUSB.SendInspectorMessage();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Latest Received:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(serialUSB.GetLatestMessage(), MessageType.Info);
    }
}
#endif
