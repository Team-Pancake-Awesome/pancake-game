using UnityEngine;

public class MouseInput : MonoBehaviour, ISpatulaInput
{
    [Header("Mouse Controls")]
    public KeyCode lockKey = KeyCode.Space;
    [Tooltip("Scales keyboard horizontal input so movement matches previous feel")]
    public float keyboardMoveBoost = 25f;

    [Header("Keyboard Pot Simulation")]
    public KeyCode potIncreaseKey = KeyCode.UpArrow;
    public KeyCode potDecreaseKey = KeyCode.DownArrow;
    [Tooltip("How fast pot value rises per second while holding the increase key")]
    public float potRiseSpeed = 0.8f;
    [Tooltip("How fast pot value falls per second while holding the decrease key")]
    public float potFallSpeed = 0.8f;
    [Range(0f, 1f)]
    public float initialPotValue = 0f;

    [Header("Mouse Swipe Tuning")]
    [Tooltip("Minimum vertical pixels per second to trigger a flip")]
    public float minSwipeSpeed = 1500f; 
    [Tooltip("Pixels per second for a maximum strength flip")]
    public float maxSwipeSpeed = 5500f; 
    public float cooldown = 0.35f;

    private float lastFlipTime = -999f;
    private float lastMouseY;
    private float simulatedPotValue;

    void Start()
    {
        lastMouseY = Input.mousePosition.y;
        simulatedPotValue = Mathf.Clamp01(initialPotValue);
    }

    public bool TryGetControlState(out SpatulaControlState state)
    {
        state = default;

        float potDelta = 0f;
        if (Input.GetKey(potIncreaseKey)) potDelta += potRiseSpeed * Time.deltaTime;
        if (Input.GetKey(potDecreaseKey)) potDelta -= potFallSpeed * Time.deltaTime;
        simulatedPotValue = Mathf.Clamp01(simulatedPotValue + potDelta);

        state.PotValue = simulatedPotValue;
        state.HorizontalInput = Input.GetAxis("Horizontal") * keyboardMoveBoost;
        state.PitchNormalized = Mathf.Clamp01(Input.mousePosition.y / Screen.height);
        state.LockPressed = Input.GetKeyDown(lockKey);
        state.LockHeld = Input.GetKey(lockKey);
        state.LockReleased = Input.GetKeyUp(lockKey);

        // Calculate how far the mouse moved this exact frame
        float currentMouseY = Input.mousePosition.y;
        float swipeDistanceY = currentMouseY - lastMouseY;
        float swipeSpeed = 0f;

        if (Time.deltaTime > 0)
        {
            // Convert to Pixels Per Second 
            swipeSpeed = swipeDistanceY / Time.deltaTime; 
        }

        // Always update the last position for the next frame
        lastMouseY = currentMouseY;

        if (Time.time - lastFlipTime < cooldown) return true;

        if (swipeDistanceY > 0 && swipeSpeed >= minSwipeSpeed)
        {
            lastFlipTime = Time.time;
            
            float t = Mathf.Clamp01((swipeSpeed - minSwipeSpeed) / (maxSwipeSpeed - minSwipeSpeed));
            float strength = Mathf.Lerp(1f, 2.5f, t);
            state.FlipTriggered = true;
            state.SnapRequested = true;
            state.FlipStrength = strength;
            
            //Debug.Log($"MOUSE FLIP! Speed: {swipeSpeed:F2} | Strength: {strength:F2}");
        }
        
        return true;
    }
}