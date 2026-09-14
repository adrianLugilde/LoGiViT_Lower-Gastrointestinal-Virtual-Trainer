using UnityEngine;
using UnityEditor;
using System;

public class AudioRecorder : MonoBehaviour
{
    private AudioClip recordedClip;
    public event Action<AudioClip> OnAudioRecorded;

    public void StartRecording()
    {
        recordedClip = Microphone.Start(null, false, 10, 44100); // record for 10 seconds max
    }

    public void StopRecording()
    {
        Microphone.End(null);
        OnAudioRecorded?.Invoke(recordedClip);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(AudioRecorder))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            AudioRecorder myScript = (AudioRecorder)target;
            if (GUILayout.Button("Start recording"))
            {
                myScript.StartRecording();
            }else if (GUILayout.Button("Stop recording"))
            {
                myScript.StopRecording();
            }
        }
    }
#endif

}
