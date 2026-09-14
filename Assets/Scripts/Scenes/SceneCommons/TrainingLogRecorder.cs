using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TrainingLogRecorder : MonoBehaviour
{
    public Transform cameraTransform; 
    public Vector3 cameraAngulation;  
    public Vector3 torque;            
    public bool isAngulationLocked;   
    public int currentSegment;        
    public bool polypDetected;        
    public bool inPolypIdentification; 

    public float loggingInterval = 0.2f; // Time interval in seconds to check (converted from X frames)
    private float lastLogTime = 0f;      // Last time we logged

    // For coverage training
    public List<float> coveragePercentages = new List<float>();

    private Vector3 prevCameraPosition;
    private Vector3 prevCameraAngulation;
    private Vector3 prevTorque;
    private bool prevIsAngulationLocked;
    private int prevCurrentSegment;
    private List<float> prevCoveragePercentages = new List<float>();
    private bool prevPolypDetected;
    private bool prevInPolypIdentification;

    private FileStream logFile;

    void Start()
    {
        logFile = new FileStream("TrainingData.bin", FileMode.Create);
        WriteHeaderToFile(); 
        CaptureState();      
    }

    void Update()
    {
        if (Time.time - lastLogTime >= loggingInterval)
        {
            if (HasStateChanged())
            {
                LogState(); 
            }
            lastLogTime = Time.time; 
        }
    }

    private bool HasStateChanged()
    {
        if (prevCameraPosition != cameraTransform.position || 
            prevCameraAngulation != cameraTransform.eulerAngles || 
            prevTorque != torque || 
            prevIsAngulationLocked != isAngulationLocked || 
            prevCurrentSegment != currentSegment ||
            prevPolypDetected != polypDetected ||
            prevInPolypIdentification != inPolypIdentification)
        {
            return true; 
        }

        if (HasCoverageChanged())
        {
            return true;
        }

        return false;
    }

    private bool HasCoverageChanged()
    {
        if (coveragePercentages.Count != prevCoveragePercentages.Count)
            return true;

        for (int i = 0; i < coveragePercentages.Count; i++)
        {
            if (Mathf.Abs(coveragePercentages[i] - prevCoveragePercentages[i]) > 0.01f) 
                return true;
        }

        return false;
    }

    private void CaptureState()
    {
        prevCameraPosition = cameraTransform.position;
        prevCameraAngulation = cameraTransform.eulerAngles;
        prevTorque = torque;
        prevIsAngulationLocked = isAngulationLocked;
        prevCurrentSegment = currentSegment;
        prevPolypDetected = polypDetected;
        prevInPolypIdentification = inPolypIdentification;
        prevCoveragePercentages = new List<float>(coveragePercentages); 
    }

    
    private void LogState()
    {
        // Capture current time (HH:MM:SS)
        string trainingElapsedTime = DateTime.Now.ToString("HH:mm:ss");

        using (BinaryWriter writer = new BinaryWriter(logFile, System.Text.Encoding.UTF8, true))
        {
            // Timestamp and basic variables
            writer.Write(trainingElapsedTime);
            writer.Write(cameraTransform.position.x);
            writer.Write(cameraTransform.position.y);
            writer.Write(cameraTransform.position.z);

            // Camera angulation and torque
            writer.Write(cameraTransform.eulerAngles.x);
            writer.Write(cameraTransform.eulerAngles.y);
            writer.Write(cameraTransform.eulerAngles.z);
            writer.Write(torque.x);
            writer.Write(torque.y);
            writer.Write(torque.z);

            // Boolean and integer values
            writer.Write(isAngulationLocked);
            writer.Write(currentSegment);

            // Coverage training variables
            writer.Write(coveragePercentages.Count);
            foreach (float coverage in coveragePercentages)
            {
                writer.Write(coverage);
            }

            // Polyp detection variables (for polyp identification training)
            writer.Write(polypDetected);
            writer.Write(inPolypIdentification);
        }

        // Update the previous state after logging
        CaptureState();
    }

    private void WriteHeaderToFile()
    {
        using (BinaryWriter writer = new BinaryWriter(logFile, System.Text.Encoding.UTF8, true))
        {
            writer.Write("Training Data Log"); 
            writer.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }

    void OnDestroy()
    {
        if (logFile != null)
        {
            logFile.Close(); 
        }
    }
}
