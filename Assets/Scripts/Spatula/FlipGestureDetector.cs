using UnityEngine;

public class FlipGestureDetector : MonoBehaviour, ISpatulaInput
{
    public ArduinoReader reader;

    [Header("Arduino Controls")]
    public KeyCode lockKey = KeyCode.Space;
    public float minPitchInput = 30f;
    public float maxPitchInput = -40f;
    public float rollDeadzone = 2f;
    public bool invertRoll = true;

    [Header("Gesture Tuning")]
    public float gyroYThreshold = 1.75f; 
    public float rollLimit = 45f;
    public float cooldown = 0.35f;

    [Header("Debug")]
    public float debugGyroY;
    public float debugRoll;

    private float lastFlipTime = -999f;
    private bool lastActionButtonHeld;

    public bool TryGetControlState(out SpatulaControlState state)
    {
        state = default;

        if (reader == null) return false;

        float roll = invertRoll ? -reader.roll : reader.roll;
        float pitch = reader.pitch;
        float gyroY = reader.gyroY; 

        debugGyroY = gyroY;
        debugRoll = roll;

        state.PotValue = Mathf.Clamp01(reader.sensorValue);
        state.HorizontalInput = Mathf.Abs(roll) > rollDeadzone ? roll : 0f;
        float normalizedPitch = Mathf.InverseLerp(minPitchInput, maxPitchInput, pitch);
        state.PitchNormalized = Mathf.Clamp01(normalizedPitch);


        bool currentActionButtonHeld = reader.actionButton == 1;

        state.LockPressed = Input.GetKeyDown(lockKey) || (currentActionButtonHeld && !lastActionButtonHeld);
        state.LockHeld = Input.GetKey(lockKey) || currentActionButtonHeld;
        state.LockReleased = Input.GetKeyUp(lockKey) || (!currentActionButtonHeld && lastActionButtonHeld);

        lastActionButtonHeld = currentActionButtonHeld;

        bool isFlickingUp = gyroY >= gyroYThreshold;
        bool rollOK = Mathf.Abs(roll) <= rollLimit;
        bool cooldownOK = (Time.time - lastFlipTime) >= cooldown;

        
        if (isFlickingUp)
        {
            if (!rollOK)
            {
                //Debug.LogWarning($"FLIP REJECTED: You twisted too much! Roll: {roll:F2} (Limit: {rollLimit})");
            }
            else if (!cooldownOK)
            {
                //Debug.LogWarning("FLIP REJECTED: Triggered too fast (Cooldown active).");
            }
            else
            {
                // Everything is perfect. Launch it.
                lastFlipTime = Time.time;
                float strength = Mathf.Clamp(gyroY / gyroYThreshold, 1f, 2.5f);
                state.FlipTriggered = true;
                state.SnapRequested = true;
                state.FlipStrength = strength;
                Debug.Log($"PERFECT FLIP! GyroY: {gyroY:F2} | Strength: {strength:F2}");
            }
        }

        return true;
    }
}