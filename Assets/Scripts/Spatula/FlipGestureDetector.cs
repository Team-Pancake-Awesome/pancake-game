using UnityEngine;

public class FlipGestureDetector : MonoBehaviour
{
    public ArduinoReader reader;

    [Header("Flip Mode (The Clutch)")]
    public KeyCode flipModeKey = KeyCode.Space;

    [Header("Raw Physics Thresholds")]
    // Note: Depending on how your sensor is physically mounted to the spatula, 
    // you may need to adjust these numbers (or make them negative).
    [Tooltip("How much upward physical force is required")]
    public float requiredAccelZ = 13f; 
    
    [Tooltip("How fast the wrist must snap")]
    public float requiredGyroY = 2.0f; 
    
    [Tooltip("Keep the pan relatively flat to avoid spilling")]
    public float rollLimit = 25f;      

    [Header("Debug View")]
    public float debugAccelZ;
    public float debugGyroY;
    public float debugRoll;

    private float cooldown = 0.5f;
    private float lastFlipTime = -999f;

    public bool TryGetFlip(out float strength)
    {
        strength = 0f;

        if (reader == null) return false;

        float accelZ = reader.accelZ;
        float gyroY = reader.gyroY;
        float roll = reader.roll;

        debugAccelZ = accelZ;
        debugGyroY = gyroY;
        debugRoll = roll;

        if (!Input.GetKey(flipModeKey)) return false;
        if (Time.time - lastFlipTime < cooldown) return false;

        // THE DIRECTIONAL FIX:
        // If your threshold is positive, we look for a spike above it.
        // If your threshold is negative, we look for a spike below it.
        bool isLifting = requiredAccelZ > 0 ? accelZ >= requiredAccelZ : accelZ <= requiredAccelZ;
        bool isSnapping = requiredGyroY > 0 ? gyroY >= requiredGyroY : gyroY <= requiredGyroY;
        
        // Roll still uses Abs() because tilting left OR right should both spill the pancake
        bool isFlat = Mathf.Abs(roll) <= rollLimit;

        if (isLifting && isSnapping && isFlat)
        {
            lastFlipTime = Time.time;

            // Calculate strength (still using Abs here just so strength is always a positive multiplier)
            strength = Mathf.Clamp(Mathf.Abs(gyroY) / Mathf.Abs(requiredGyroY), 1f, 2.5f);

            Debug.Log($"PERFECT FLIP! AccelZ: {accelZ:F2} | GyroY: {gyroY:F2} | Strength: {strength:F2}");
            return true;
        }

        return false;
    }
}