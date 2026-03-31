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

    [Header("Pitch Rotation")]
    public float rotationSmoothSpeed = 18f;
    public float minAngle = -15f;       // visual down angle
    public float maxAngle = 45f;        // visual up angle

    [Header("Flip Snap")]
    public float snapAngle = 22f;
    public float snapReturnSpeed = 12f;

    [Header("Roll Movement")]
    public float moveMultiplier = 0.13f;
    public float moveSmoothSpeed = 8f;

    [Header("Lock / Scoop")]
    [Range(0f, 1f)]
    public float lockedMoveMultiplier = 0f;
    public float scoopRadius = 2.5f;
    public bool usePotForLock = true;
    [Range(0f, 1f)]
    public float potLockThreshold = 0.08f;
    [Tooltip("Time in seconds after a successful launch before this spatula can scoop any pancake again")]
    public float postLaunchScoopCooldown = 0.5f;
    public float PotValue => currentPotValue;

    private float targetX;
    private float snapOffset = 0f;
    private PancakeController activePancake; 
    private bool lastEffectiveLockHeld;
    private float currentPotValue;
    private float lastSuccessfulLaunchTime = -999f;

    void Start()
    {
        targetX = transform.position.x;

        arduinoInput = GetComponent<FlipGestureDetector>();
        if (arduinoInput == null) arduinoInput = FindObjectOfType<FlipGestureDetector>();
        mouseInput = GetComponent<MouseInput>();
        if (mouseInput == null) mouseInput = FindObjectOfType<MouseInput>();

        if (arduinoInput == null) Debug.LogWarning("SpatulaController: Could not find FlipGestureDetector (Arduino).");
        if (mouseInput == null) Debug.LogWarning("SpatulaController: Could not find MouseInput.");
    }

    void Update()
    {
        activeInput = (currentInputMode == InputMode.Mouse) ? mouseInput : arduinoInput;
        if (activeInput == null || !activeInput.TryGetControlState(out SpatulaControlState inputState))
        {
            currentPotValue = 0f;
            return;
        }

        currentPotValue = Mathf.Clamp01(inputState.PotValue);

        bool lockHeldFromPot = currentPotValue > potLockThreshold;
        bool effectiveLockHeld = usePotForLock ? (lockHeldFromPot || inputState.LockHeld) : inputState.LockHeld;
        inputState.LockPressed = effectiveLockHeld && !lastEffectiveLockHeld;
        inputState.LockHeld = effectiveLockHeld;
        inputState.LockReleased = !effectiveLockHeld && lastEffectiveLockHeld;
        lastEffectiveLockHeld = effectiveLockHeld;

        HandlePancakeInteractions(inputState);
        HandleSpatulaMovement(inputState);
    }

    void HandlePancakeInteractions(SpatulaControlState inputState)
    {
        // Search for the closest pancake
        bool postLaunchCooldownActive = Time.time - lastSuccessfulLaunchTime < postLaunchScoopCooldown;

        if (inputState.LockHeld && activePancake == null && !postLaunchCooldownActive)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, scoopRadius); // TODO OverlapSphereNonAlloc with caching for performance
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
                if (closestPancake.TryScoop(transform))
                {
                    activePancake = closestPancake;
                }
            }
        }
        
        // Let go of the current pancake
        if (inputState.LockReleased && activePancake != null)
        {
            activePancake.Drop();
            activePancake = null; 
        }

        // Check for flick throw the active pancake
        if (inputState.SnapRequested)
        {
            
            snapOffset = snapAngle; 

            if (activePancake != null && inputState.FlipTriggered)
            {
                bool launched = activePancake.LaunchFlip(inputState.FlipStrength);
                if (launched)
                {
                    lastSuccessfulLaunchTime = Time.time;
                    activePancake = null;
                }
            }
        }
    }

    void HandleSpatulaMovement(SpatulaControlState inputState)
    {
        float targetPitch = Mathf.Lerp(minAngle, maxAngle, inputState.PitchNormalized);
        float moveScale = inputState.LockHeld ? lockedMoveMultiplier : 1f;
        targetX += inputState.HorizontalInput * moveMultiplier * moveScale * Time.deltaTime;

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