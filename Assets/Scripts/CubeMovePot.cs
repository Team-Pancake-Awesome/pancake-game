using UnityEngine;

public class CubeMovePot : MonoBehaviour
{
    public ArduinoReader arduinoReader;

    public float minY = 0f;
    public float maxY = 5f;

    private float startX;
    private float startZ;

    void Start()
    {
        Vector3 pos = transform.position;
        startX = pos.x;
        startZ = pos.z;
    }

    void Update()
    {
        if (arduinoReader == null)
            return;

        float t = Mathf.Clamp01(arduinoReader.sensorValue);
        float y = Mathf.Lerp(minY, maxY, t);

        transform.position = new Vector3(startX, y, startZ);
    }
}