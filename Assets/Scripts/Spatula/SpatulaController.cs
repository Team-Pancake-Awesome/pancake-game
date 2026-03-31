using UnityEngine;

public class SpatulaController : MonoBehaviour
{
    public enum InputMode { Arduino, Mouse }

    [Header("Input Settings")]
    [Tooltip("Toggle between Arduino hardware or Mouse swipes")]
    public InputMode currentInputMode = InputMode.Arduino;

    private FlipGestureDetector arduinoInput;
    private MouseInput mouseInput;
    private ISpatulaInput activeInput;

    [Header("References")]
    public ArduinoReader reader;

    [Header("Pitch Rotation")]
    public float rotationSmoothSpeed = 18f;
    public float minPitchInput = 30f;   // spatula down
    public float maxPitchInput = -40f;  // spatula up
    public float minAngle = -15f;       // visual down angle
    public float maxAngle = 45f;        // visual up angle

    [Header("Flip Snap")]
    public float snapGyroThreshold = 1.2f;
    public float requiredUpPitch = -8f;
    public float snapAngle = 22f;
    public float snapReturnSpeed = 12f;

    [Header("Roll Movement")]
    public float moveMultiplier = 0.13f;
    public float moveSmoothSpeed = 8f;
    public float rollDeadzone = 2f;
    public bool invertRoll = true;

    [Header("Lock / Scoop")]
    public KeyCode lockKey = KeyCode.Space;
    [Range(0f, 1f)]
    public float lockedMoveMultiplier = 0f;
    public float scoopRadius = 2.5f;

    private float targetX;
    private float snapOffset = 0f;
    private PancakeController activePancake; 

    void Start()
    {
        targetX = transform.position.x;

        arduinoInput = GetComponent<FlipGestureDetector>() ?? FindObjectOfType<FlipGestureDetector>();
        mouseInput = GetComponent<MouseInput>() ?? FindObjectOfType<MouseInput>();

        if (arduinoInput == null) Debug.LogWarning("SpatulaController: Could not find FlipGestureDetector (Arduino).");
        if (mouseInput == null) Debug.LogWarning("SpatulaController: Could not find MouseInput.");
    }

    void Update()
    {
        HandlePancakeInteractions();
        HandleSpatulaMovement();
    }

    void HandlePancakeInteractions()
    {
        // Search for the closest pancake
        if (Input.GetKeyDown(lockKey) && activePancake == null)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, scoopRadius);
            PancakeController closestPancake = null;
            float closestDistance = Mathf.Infinity;

            foreach (var hit in hitColliders)
            {
                PancakeController pc = hit.GetComponent<PancakeController>();
                if (pc != null && !pc.IsScooped)
                {
                    float distance = Vector3.Distance(transform.position, pc.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPancake = pc;
                    }
                }
            }

            if (closestPancake != null)
            {
                activePancake = closestPancake;
                activePancake.TryScoop(transform);
            }
        }
        
        // Let go of the current pancake
        if (Input.GetKeyUp(lockKey) && activePancake != null)
        {
            activePancake.Drop();
            activePancake = null; 
        }

        // Assign active input
        activeInput = (currentInputMode == InputMode.Mouse) ? (ISpatulaInput)mouseInput : (ISpatulaInput)arduinoInput;

        // Check for flick throw the active pancake
        if (activeInput != null && activeInput.TryGetFlip(out float strength))
        {
            
            snapOffset = snapAngle; 

            if (activePancake != null)
            {
                activePancake.LaunchFlip(strength);
                activePancake = null; 
            }
        }
    }

    void HandleSpatulaMovement()
    {
        float targetPitch = minAngle; // Default resting angle

        // mouse input mode directly maps cursor Y to spatula pitch, and keyboard left/right to horizontal movement
        if (currentInputMode == InputMode.Mouse)
        {
            // Keyboard 
            float moveInput = Input.GetAxis("Horizontal"); 
            float moveScale = Input.GetKey(lockKey) ? lockedMoveMultiplier : 1f;
            targetX += moveInput * (moveMultiplier * 25f) * moveScale * Time.deltaTime; 

            // mouse: map the cursor's Y position on the screen directly to the spatula's tilt
            float normalizedMouseY = Mathf.Clamp01(Input.mousePosition.y / Screen.height);
            targetPitch = Mathf.Lerp(minAngle, maxAngle, normalizedMouseY);
        }
        else if (reader != null)
        {
            float pitch = reader.pitch;
            float roll = invertRoll ? -reader.roll : reader.roll;
            float gyro = reader.gyroY;

            float normalizedPitch = Mathf.InverseLerp(minPitchInput, maxPitchInput, pitch);
            targetPitch = Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(normalizedPitch));

            // Arduino auto-snap fallback
            if (gyro > snapGyroThreshold && pitch <= requiredUpPitch)
            {
                snapOffset = snapAngle;
            }

            float scale = Input.GetKey(lockKey) ? lockedMoveMultiplier : 1f;
            if (Mathf.Abs(roll) > rollDeadzone)
            {
                targetX += roll * moveMultiplier * scale * Time.deltaTime;
            }
        }

        // Apply Visual Rotation 
        targetPitch += snapOffset;
        
        // Prevent the spatula from spinning backward
        targetPitch = Mathf.Clamp(targetPitch, minAngle, maxAngle + 65f);

        Quaternion targetRot = Quaternion.Euler(-targetPitch, 0f, 0f);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotationSmoothSpeed * Time.deltaTime);


        // Smoothly return the snap offset to 0 over time
        snapOffset = Mathf.Lerp(snapOffset, 0f, snapReturnSpeed * Time.deltaTime);

        // Smooth 
        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, targetX, moveSmoothSpeed * Time.deltaTime);
        transform.position = pos;
    }
}