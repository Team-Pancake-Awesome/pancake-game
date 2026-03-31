using UnityEngine;
using System.Collections;

public class PancakeController : MonoBehaviour
{
    public Rigidbody rb;

    [Header("Pancake State")]
    public PancakeStats stats = new();
    [Tooltip("Reset will also clear toppings when true")]
    public bool clearToppingsOnReset = true;

    [Header("Scoop settings")]
    public float maxFlipDistance = 3.0f; 
    [Tooltip("Where to position the pancake relative to the spatula when scooped")]
    public Vector3 scoopOffset = new(0, 0.125f, 0);
    [Tooltip("How much horizontal force to add if they scoop off center")]
    public float sloppyFlingMultiplier = 3f;
    [Tooltip("How long it takes to ease the pancake onto the spatula")]
    public float scoopMoveDuration = 0.12f;
    [Tooltip("Optional rotation offset (degrees) after aligning to the spatula surface")]
    public Vector3 scoopRotationOffsetEuler = Vector3.zero;
    [Tooltip("Minimum time in seconds between successful scoops")]
    public float scoopCooldown = 0.75f;
    
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
    public bool IsAirborne => airborne;
    private bool airborne = false;
    private float lastLaunchTime = -999f;
    private float lastScoopTime = -999f;
    private Vector3 offCenterOffset;
    private Vector3 scoopedLocalOffset;
    private bool hasScoopedLocalOffset = false;
    private Coroutine scoopMoveRoutine;

    public PancakeDoneness CurrentDoneness
    {
        get { return stats != null ? stats.Doneness : PancakeDoneness.Raw; }
    }

    public float AverageCookAmount
    {
        get { return stats != null ? stats.AverageCookAmount : 0f; }
    }

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
    public bool TryScoop(Transform spatula)
    {
        if (airborne || spatula == null) return false;
        if (Time.time - lastScoopTime < scoopCooldown) return false;

        Vector2 spatPos = new(spatula.position.x, spatula.position.z);
        Vector2 panPos = new(transform.position.x, transform.position.z);
        float distance = Vector2.Distance(spatPos, panPos);

        if (distance > maxFlipDistance) return false;

        IsScooped = true;
        rb.isKinematic = true; 
        timeScooped = Time.time; // flip delay timer
        lastScoopTime = Time.time;

        // Calculate how off center the player was
        offCenterOffset = transform.position - spatula.position;
        offCenterOffset.y = 0; 

        // Preserve grab-time local X/Z while keeping authored Y clearance above spatula.
        Vector3 grabbedLocalOffset = spatula.InverseTransformPoint(transform.position);
        scoopedLocalOffset = new Vector3(
            grabbedLocalOffset.x + scoopOffset.x,
            scoopOffset.y,
            grabbedLocalOffset.z + scoopOffset.z
        );
        hasScoopedLocalOffset = true;

        StopScoopMoveRoutine();
        scoopMoveRoutine = StartCoroutine(SmoothMoveToSpatula(spatula));

        Debug.Log($"Pancake Scooped! Off-center amount: {offCenterOffset.magnitude:F2}");
        return true;
    }

    // Called by the SpatulaController when the lock key is released
    public void Drop()
    {
        if (IsScooped)
        {
            StopScoopMoveRoutine();
            IsScooped = false;
            rb.isKinematic = false; // Turn gravity back on
            Debug.Log("Pancake Dropped.");
        }
    }

    // Called by the SpatulaController when a valid swipe/flick is detected
    public bool LaunchFlip(float strength)
    {
        
        if (!IsScooped || (Time.time - timeScooped <= scoopGracePeriod)) return false;

        StopScoopMoveRoutine();
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
        Vector3 sloppyForce = sloppyFlingMultiplier * strength * offCenterOffset;

        float appliedTorque = baseTorque + (strength * torqueMultiplier);

        // Apply forces up and slop
        rb.AddForce((Vector3.up * upForce) + sloppyForce, ForceMode.Impulse);
        rb.AddTorque(Vector3.right * appliedTorque, ForceMode.Impulse);

        stats?.RegisterFlip();
        
        Debug.Log($"SUCCESSFUL LAUNCH! UpForce: {upForce:F2} | SloppyForce: {sloppyForce.magnitude:F2}");
        return true;
    }

    public void ApplyHeat(float heatIntensity)
    {
        if (stats == null)
        {
            return;
        }

        stats.ApplyHeat(heatIntensity, Time.deltaTime);
    }

    public PancakeTopping AddTopping(PancakeToppingType type, float amount = 1f, float coverage = 0.25f, string customName = "")
    {
        if (stats == null)
        {
            return null;
        }

        return stats.AddTopping(type, amount, coverage, customName);
    }

    public bool RemoveTopping(PancakeToppingType type, string customName = "")
    {
        if (stats == null)
        {
            return false;
        }

        return stats.RemoveTopping(type, customName);
    }

    public void ResetPancake()
    {
        StopScoopMoveRoutine();
        airborne = false;
        IsScooped = false;
        lastScoopTime = -999f;
        if (rb != null) rb.isKinematic = false;
        stats?.ResetForNewRound(!clearToppingsOnReset);
        
        if (spawnPoint != null)
        {
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void StopScoopMoveRoutine()
    {
        if (scoopMoveRoutine != null)
        {
            StopCoroutine(scoopMoveRoutine);
            scoopMoveRoutine = null;
        }
    }

    Vector3 GetScoopTargetPosition(Transform spatula)
    {
        if (spatula == null)
        {
            return transform.position;
        }

        if (hasScoopedLocalOffset)
        {
            return spatula.TransformPoint(scoopedLocalOffset);
        }

        return spatula.TransformPoint(scoopOffset);
    }

    IEnumerator SmoothMoveToSpatula(Transform spatula)
    {
        transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        float duration = Mathf.Max(0.0001f, scoopMoveDuration);
        float elapsed = 0f;


        while (elapsed < duration && IsScooped && spatula != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 targetPos = GetScoopTargetPosition(spatula);
            Quaternion targetRot = spatula.rotation * Quaternion.Euler(scoopRotationOffsetEuler);

            Vector3 syncedPos = Vector3.Lerp(startPos, targetPos, easedT);

            transform.SetPositionAndRotation(
                syncedPos,
                Quaternion.Slerp(startRot, targetRot, easedT)
            );

            yield return null;
        }

        if (IsScooped && spatula != null)
        {
            while (IsScooped && spatula != null)
            {
                Vector3 targetPos = GetScoopTargetPosition(spatula);
                Quaternion targetRot = spatula.rotation * Quaternion.Euler(scoopRotationOffsetEuler);

                transform.SetPositionAndRotation(targetPos, targetRot);
                yield return null;
            }
        }

        scoopMoveRoutine = null;
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