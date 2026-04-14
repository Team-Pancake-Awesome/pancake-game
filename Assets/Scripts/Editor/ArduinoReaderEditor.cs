using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArduinoReader))]
public class ArduinoReaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        ArduinoReader reader = (ArduinoReader)target;
        if (GUILayout.Button("Force Connect"))
        {
            reader.ForceConnectNow();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Refind Port"))
        {
            reader.ForceRefindPortNow();
        }
    }
}
