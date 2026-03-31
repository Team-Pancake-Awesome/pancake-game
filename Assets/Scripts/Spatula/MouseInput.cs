using UnityEngine;

public class MouseInput : MonoBehaviour, ISpatulaInput
{
    [Header("Mouse Swipe Tuning")]
    [Tooltip("Minimum vertical pixels per second to trigger a flip")]
    public float minSwipeSpeed = 1500f; 
    [Tooltip("Pixels per second for a maximum strength flip")]
    public float maxSwipeSpeed = 5500f; 
    public float cooldown = 0.35f;

    private float lastFlipTime = -999f;
    private float lastMouseY;

    void Start()
    {
        lastMouseY = Input.mousePosition.y;
    }

    public bool TryGetFlip(out float strength)
    {
        strength = 0f;

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

        
        if (Time.time - lastFlipTime < cooldown) return false;

        
        if (swipeDistanceY > 0 && swipeSpeed >= minSwipeSpeed)
        {
            lastFlipTime = Time.time;
            
            float t = Mathf.Clamp01((swipeSpeed - minSwipeSpeed) / (maxSwipeSpeed - minSwipeSpeed));
            strength = Mathf.Lerp(1f, 2.5f, t);
            
            Debug.Log($"MOUSE FLIP! Speed: {swipeSpeed:F2} | Strength: {strength:F2}");
            return true;
        }
        
        return false;
    }
}