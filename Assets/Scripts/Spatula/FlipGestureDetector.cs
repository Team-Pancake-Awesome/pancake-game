using UnityEngine;

public class FlipGestureDetector : MonoBehaviour
{
    public ArduinoReader reader;

    [Header("Gesture Tuning")]
    [Tooltip("How fast the gyro needs to be spinning UPWARD. (Try 2.0)")]
    public float gyroYThreshold = 2.0f; 
    
    [Tooltip("How much sideways twist is allowed during a flip.")]
    public float rollLimit = 30f; 
    
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
        float gyroY = reader.gyroY; // Raw wrist snap velocity!

        debugGyroY = gyroY;
        debugRoll = roll;

        if (Time.time - lastFlipTime < cooldown) return false;

        // ONLY LOOK AT GYRO! Ignore pitch completely!
        // Upward flicks are positive. Downward swings are negative.
        bool isFlickingUp = gyroY >= gyroYThreshold;
        bool rollOK = Mathf.Abs(roll) <= rollLimit;

        if (isFlickingUp && rollOK)
        {
            lastFlipTime = Time.time;

            // Strength is purely based on how hard you snapped your wrist
            strength = Mathf.Clamp(gyroY / gyroYThreshold, 1f, 2.5f);

            Debug.Log($"PERFECT FLIP! GyroY: {gyroY:F2} | Strength: {strength:F2}");
            return true;
        }

        return false;
    }
}