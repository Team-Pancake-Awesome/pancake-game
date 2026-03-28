using UnityEngine;

public class FlipGestureDetector : MonoBehaviour
{
    public ArduinoReader reader;

    [Header("Flip Detection")]
    public float flickThreshold = 2.5f;
    public float cooldown = 0.5f;

    float lastFlipTime;

    public bool TryGetFlip(out float strength)
    {
        strength = 0;

        if (Time.time - lastFlipTime < cooldown)
            return false;

        float gyro = reader.gyroY;

        if (gyro > flickThreshold)
        {
            strength = Mathf.Clamp(gyro, flickThreshold, flickThreshold * 3f);
            lastFlipTime = Time.time;
            return true;
        }

        return false;
    }
}