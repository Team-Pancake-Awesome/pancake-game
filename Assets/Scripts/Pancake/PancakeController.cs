using UnityEngine;

public class PancakeController : MonoBehaviour
{
    public Rigidbody rb;

    [Header("Scoop settings")]
    public float maxFlipDistance = 3.0f; 
    [Tooltip("How high to visually pop the pancake up so it looks resting on the spatula")]
    public float scoopHeightOffset = 0.3f;
    [Tooltip("How much horizontal force to add if they scoop off center")]
    public float sloppyFlingMultiplier = 3f;
    
    [Tooltip("Time in seconds after scooping before a flip is allowed")]
    public float scoopGracePeriod = 0.25f;
    private float timeScooped = -999f;

    [Header("Flip Physics")]
    public float baseUpForce = 6f;
    public float forceMultiplier = 2.5f;
    public float baseTorque = 100f;
    public float torqueMultiplier = 150f;

    [Header("Testing")]
    public Transform spawnPoint;
    public KeyCode resetKey = KeyCode.R;
    public KeyCode testLaunchKey = KeyCode.T;

    [Header("Failsafe Tuning")]
    public float launchGracePeriod = 0.2f;

    public bool IsScooped { get; private set; } 
    private bool airborne = false;
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
    }

    // Called by the SpatulaController when the lock key is pressed
    public void TryScoop(Transform spatula)
    {
        if (airborne) return;

        Vector2 spatPos = new Vector2(spatula.position.x, spatula.position.z);
        Vector2 panPos = new Vector2(transform.position.x, transform.position.z);
        float distance = Vector2.Distance(spatPos, panPos);

        if (distance <= maxFlipDistance)
        {
            IsScooped = true;
            rb.isKinematic = true; 
            timeScooped = Time.time; // flip delay timer
            
            // Visually move it up to the spatula
            transform.position = new Vector3(transform.position.x, spatula.position.y + scoopHeightOffset, transform.position.z);

            // Calculate how off center the player was
            offCenterOffset = transform.position - spatula.position;
            offCenterOffset.y = 0; 

            Debug.Log($"Pancake Scooped! Off-center amount: {offCenterOffset.magnitude:F2}");
        }
    }

    // Called by the SpatulaController when the lock key is released
    public void Drop()
    {
        if (IsScooped)
        {
            IsScooped = false;
            rb.isKinematic = false; // Turn gravity back on
            Debug.Log("Pancake Dropped.");
        }
    }

    // Called by the SpatulaController when a valid swipe/flick is detected
    public void LaunchFlip(float strength)
    {
        
        if (!IsScooped || (Time.time - timeScooped <= scoopGracePeriod)) return;

        airborne = true;
        IsScooped = false;
        lastLaunchTime = Time.time;

        // Turn physics back on for the launch
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Calculate standard upward force
        float upForce = baseUpForce + (strength * forceMultiplier);
        
        // Calculate sloppy lateral force
        Vector3 sloppyForce = offCenterOffset * sloppyFlingMultiplier * strength;

        float appliedTorque = baseTorque + (strength * torqueMultiplier);

        // Apply forces up and slop
        rb.AddForce((Vector3.up * upForce) + sloppyForce, ForceMode.Impulse);
        rb.AddTorque(Vector3.right * appliedTorque, ForceMode.Impulse);
        
        Debug.Log($"SUCCESSFUL LAUNCH! UpForce: {upForce:F2} | SloppyForce: {sloppyForce.magnitude:F2}");
    }

    public void ResetPancake()
    {
        airborne = false;
        IsScooped = false;
        if (rb != null) rb.isKinematic = false;
        
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }
        
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
    
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