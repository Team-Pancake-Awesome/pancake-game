using UnityEngine;

public class FlipGestureDetector : MonoBehaviour
{
    public ArduinoReader reader;

    [Header("Gesture Tuning")]
    public float gyroYThreshold = 1.75f; 
    public float rollLimit = 45f;
    public float cooldown = 0.35f;

    [Header("Debug")]
    public float debugGyroY;
    public float debugRoll;

    private float lastFlipTime = -999f;

    public bool TryGetFlip(out float strength)
    {
        strength = 0f;

        if (reader == null) return false;

        float roll = reader.roll;
        float gyroY = reader.gyroY; 

        debugGyroY = gyroY;
        debugRoll = roll;

        bool isFlickingUp = gyroY >= gyroYThreshold;
        bool rollOK = Mathf.Abs(roll) <= rollLimit;
        bool cooldownOK = (Time.time - lastFlipTime) >= cooldown;

        
        if (isFlickingUp)
        {
            if (!rollOK)
            {
                Debug.LogWarning($"FLIP REJECTED: You twisted too much! Roll: {roll:F2} (Limit: {rollLimit})");
            }
            else if (!cooldownOK)
            {
                Debug.LogWarning("FLIP REJECTED: Triggered too fast (Cooldown active).");
            }
            else
            {
                // Everything is perfect. Launch it.
                lastFlipTime = Time.time;
                strength = Mathf.Clamp(gyroY / gyroYThreshold, 1f, 2.5f);
                Debug.Log($"PERFECT FLIP! GyroY: {gyroY:F2} | Strength: {strength:F2}");
                return true;
            }
        }

        return false;
    }
}