using UnityEngine;
using System.Linq;

public class SpatulaController : MonoBehaviour
{
    public enum InputMode { Arduino, Mouse }

    [Header("Input Settings")]
    [Tooltip("Toggle between two ISpatulaInput sources")]
    public InputMode currentInputMode = InputMode.Arduino;
    [Tooltip("Arduino input source component (must implement ISpatulaInput)")]
    public ArduinoReader arduinoInput;
    [Tooltip("Mouse input source component (must implement ISpatulaInput)")]
    public MouseInput mouseInput;

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
    [Tooltip("Max press duration to treat as a tap-drop while scooped")]
    public float scoopedTapDropThreshold = 0.18f;
    [Tooltip("Time in seconds after a successful launch before this spatula can scoop any pancake again")]
    public float postLaunchScoopCooldown = 0.5f;
    public float PotValue => currentPotValue;

    private float targetX;
    private float snapOffset = 0f;
    private PancakeController activePancake; 
    private bool lastRawLockHeld;
    private bool dropTapCandidate;
    private float scoopedPressStartTime = -999f;
    private float currentPotValue;
    private float lastSuccessfulLaunchTime = -999f;

    void Start()
    {
        targetX = transform.position.x;
        ResolveInputSources();
    }

    void ResolveInputSources()
    {
        if (arduinoInput == null)
            Debug.LogWarning("SpatulaController: Could not resolve a primary ISpatulaInput source.");

        if (mouseInput == null)
            Debug.LogWarning("SpatulaController: Could not resolve a secondary ISpatulaInput source.");
    }

    void Update()
    {
        activeInput = (currentInputMode == InputMode.Mouse) ? mouseInput : arduinoInput;
        if (activeInput == null || !activeInput.TryGetControlState(out SpatulaControlState inputState))
        {
            currentPotValue = 0f;
            lastRawLockHeld = false;
            return;
        }

        currentPotValue = Mathf.Clamp01(inputState.PotValue);

        bool rawLockHeld = inputState.LockHeld;
        bool rawLockPressed = rawLockHeld && !lastRawLockHeld;
        bool rawLockReleased = !rawLockHeld && lastRawLockHeld;
        lastRawLockHeld = rawLockHeld;

        HandlePancakeInteractions(inputState, rawLockPressed, rawLockHeld, rawLockReleased);
        HandleSpatulaMovement(inputState, rawLockHeld);
    }

    void HandlePancakeInteractions(SpatulaControlState inputState, bool rawLockPressed, bool rawLockHeld, bool rawLockReleased)
    {
        // Scoop only on press, not while continuously held.
        bool postLaunchCooldownActive = Time.time - lastSuccessfulLaunchTime < postLaunchScoopCooldown;
        bool scoopedThisFrame = false;

        if (activePancake == null && rawLockPressed && !postLaunchCooldownActive)
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
                    scoopedThisFrame = true;
                    dropTapCandidate = false;
                    scoopedPressStartTime = -999f;
                }
            }
        }

        // While scooped, press starts a tap/hold decision.
        if (activePancake != null && rawLockPressed && !scoopedThisFrame)
        {
            dropTapCandidate = true;
            scoopedPressStartTime = Time.time;
        }

        // Holding beyond threshold cancels tap-drop and keeps toss enabled.
        if (activePancake != null && rawLockHeld && dropTapCandidate)
        {
            if (Time.time - scoopedPressStartTime > scoopedTapDropThreshold)
            {
                dropTapCandidate = false;
            }
        }

        // Release drops only if that press was a short tap.
        if (activePancake != null && rawLockReleased && dropTapCandidate)
        {
            activePancake.Drop();
            activePancake = null;
            dropTapCandidate = false;
            scoopedPressStartTime = -999f;
            return;
        }

        if (rawLockReleased)
        {
            dropTapCandidate = false;
            scoopedPressStartTime = -999f;
        }

        // Holding lock while scooped allows toss when a valid flip/snap is detected.
        if (inputState.SnapRequested)
        {
            snapOffset = snapAngle; 

            if (activePancake != null && rawLockHeld && inputState.FlipTriggered)
            {
                bool launched = activePancake.LaunchFlip(inputState.FlipStrength);
                if (launched)
                {
                    lastSuccessfulLaunchTime = Time.time;
                    activePancake = null;
                    dropTapCandidate = false;
                    scoopedPressStartTime = -999f;
                }
            }
        }
    }

    void HandleSpatulaMovement(SpatulaControlState inputState, bool rawLockHeld)
    {
        float targetPitch = Mathf.Lerp(minAngle, maxAngle, inputState.PitchNormalized);
        bool aboutToToss = activePancake != null && activePancake.IsScooped && rawLockHeld;
        float moveScale = aboutToToss ? lockedMoveMultiplier : 1f;
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