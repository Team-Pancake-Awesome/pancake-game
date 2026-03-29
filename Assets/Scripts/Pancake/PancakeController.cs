using UnityEngine;

public class PancakeController : MonoBehaviour
{
    public FlipGestureDetector flipDetector;
    public Rigidbody rb;

    [Header("Scoop Mechanic (The Fake Slide-Under)")]
    public Transform spatula; 
    public KeyCode scoopKey = KeyCode.Space; 
    public float maxFlipDistance = 3.0f; 
    [Tooltip("How high to visually pop the pancake up so it looks resting on the spatula")]
    public float scoopHeightOffset = 0.3f;
    [Tooltip("How much horizontal forceto add if they scoop off center")]
    public float sloppyFlingMultiplier = 3f;

    [Header("Testing")]
    public Transform spawnPoint;
    public KeyCode resetKey = KeyCode.R;
    public KeyCode testLaunchKey = KeyCode.T;

    [Header("Flip Physics")]
    public float baseUpForce = 6f;
    public float forceMultiplier = 2.5f;
    public float baseTorque = 100f;
    public float torqueMultiplier = 150f;

    [Header("Failsafe Tuning")]
    public float launchGracePeriod = 0.2f;

    private bool airborne = false;
    private bool isScooped = false;
    private float lastLaunchTime = -999f;
    private Vector3 offCenterOffset;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (rb == null) return;

        if (Input.GetKeyDown(resetKey)) ResetPancake();
        if (Input.GetKeyDown(testLaunchKey)) LaunchFlip(1.5f);

        HandleScooping();

        if (flipDetector == null) return;

        // Only allow a flip if we successfully scooped the pancake!
        if (isScooped && flipDetector.TryGetFlip(out float strength))
        {
            LaunchFlip(strength);
        }
    }

    void HandleScooping()
    {
        // 1. ATTEMPT TO SCOOP (When you press Space)
        if (Input.GetKeyDown(scoopKey) && !airborne && spatula != null)
        {
            Vector2 spatPos = new Vector2(spatula.position.x, spatula.position.z);
            Vector2 panPos = new Vector2(transform.position.x, transform.position.z);
            float distance = Vector2.Distance(spatPos, panPos);

            if (distance <= maxFlipDistance)
            {
                isScooped = true;
                rb.isKinematic = true; // Freezes physics so it stops resting on the pan!
                
                // Visually pop it up to "rest" on the spatula
                transform.position = new Vector3(transform.position.x, spatula.position.y + scoopHeightOffset, transform.position.z);

                // Calculate how sloppy (off-center) the player was
                offCenterOffset = transform.position - spatula.position;
                offCenterOffset.y = 0; // We only care about horizontal sloppiness

                Debug.Log($"Pancake Scooped! Off-center amount: {offCenterOffset.magnitude:F2}");
            }
        }

        // 2. DROP IT (If you let go of Space without flipping)
        if (Input.GetKeyUp(scoopKey) && isScooped)
        {
            isScooped = false;
            rb.isKinematic = false; // Turn gravity back on
            Debug.Log("Pancake Dropped.");
        }
    }

    void LaunchFlip(float strength)
    {
        airborne = true;
        isScooped = false;
        lastLaunchTime = Time.time;

        // Turn physics back on for the launch
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Calculate standard upward force
        float upForce = baseUpForce + (strength * forceMultiplier);
        
        // Calculate sloppy lateral force (flinging away if off-center)
        Vector3 sloppyForce = offCenterOffset * sloppyFlingMultiplier * strength;

        float appliedTorque = baseTorque + (strength * torqueMultiplier);

        // Apply forces (Upward + Sloppy Sideways)
        rb.AddForce((Vector3.up * upForce) + sloppyForce, ForceMode.Impulse);
        rb.AddTorque(Vector3.right * appliedTorque, ForceMode.Impulse);
        
        Debug.Log($"SUCCESSFUL LAUNCH! UpForce: {upForce:F2} | SloppyForce: {sloppyForce.magnitude:F2}");
    }

    public void ResetPancake()
    {
        airborne = false;
        isScooped = false;
        if (rb != null) rb.isKinematic = false;
        
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }
        
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
    
    // --- BULLETPROOF LANDING DETECTION ---

    void OnCollisionEnter(Collision collision)
    {
        if (airborne && Time.time - lastLaunchTime > launchGracePeriod)
        {
            airborne = false;
            Debug.Log("Pancake Landed! Ready to scoop.");
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (airborne && Time.time - lastLaunchTime > launchGracePeriod)
        {
            airborne = false;
            Debug.Log("Pancake Failsafe: Reset airborne to false while resting.");
        }
    }
}