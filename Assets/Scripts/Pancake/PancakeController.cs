using UnityEngine;
using System.Collections;

public class PancakeController : MonoBehaviour
{
    private const float BurntCookThreshold = 0.92f;

    public Rigidbody rb;

    [Header("Pancake State")]
    public PancakeStats stats = new();
    [Tooltip("Reset will also clear toppings when true")]
    public bool clearToppingsOnReset = true;

    [Header("Pancake Side Rules")]
    [Tooltip("When scooping, read the current physical/visual orientation before snapping to the spatula. This prevents pickup from forcing the same side down every time.")]
    public bool readVisualSideOnScoop = true;

    [Tooltip("A successful launched flip changes which pancake side is facing up when the flight ends.")]
    public bool swapSideOnSuccessfulFlip = true;

    [Tooltip("While held, rotate the visual pancake so the tracked side state is preserved on the spatula instead of always forcing the same side down.")]
    public bool snapVisualSideOnScoop = true;

    [Tooltip("When landing, flatten the pancake and show the side chosen by the game rule rather than relying on chaotic rigidbody rotation.")]
    public bool snapVisualSideOnLanding = true;

    [Tooltip("Rotation used to show the opposite side of the pancake. Usually 180 degrees around X for a flat pancake mesh.")]
    public Vector3 sideFlipRotationOffsetEuler = new(180f, 0f, 0f);

    [Tooltip("Use this only if the prefab's visual top side is opposite of transform.up.")]
    public bool invertVisualSideState = false;

    [Tooltip("Logs side-state changes caused by scoop/landing rules.")]
    public bool logSideChanges = true;

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
    [Tooltip("Keep the pancake on its scoop-time world Z while being held")]
    public bool keepScoopedWorldZ = true;
    
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
    [Tooltip("Ignores landing/failsafe collision checks immediately after launch so the pancake has time to separate from the spatula.")]
    public float launchGracePeriod = 0.35f;

    [Tooltip("Small upward nudge applied at launch to clear lingering contact with the spatula/counter collider.")]
    public float launchClearance = 0.08f;

    [Tooltip("OnCollisionStay failsafe cannot cancel a flight until at least this much time has passed.")]
    public float failsafeMinimumAirtime = 0.45f;

    [Tooltip("Failsafe only fires if the pancake never gained at least this much height above launch height.")]
    public float failsafeMaxHeightAboveLaunch = 0.12f;

    [Tooltip("Failsafe only fires when vertical speed is low or falling.")]
    public float failsafeMaxVerticalSpeed = 0.35f;

    [Header("Launch Collision Filter")]
    [Tooltip("Temporarily ignores collisions between the pancake and the spatula that just launched it. This prevents the pancake from bonking the scoop immediately after launch and turning a valid flip into a sad fall-off.")]
    public bool ignoreSpatulaCollisionsAfterLaunch = true;

    [Tooltip("How long after launch the pancake ignores the launching spatula's colliders.")]
    public float spatulaCollisionIgnoreDuration = 0.75f;

    [Tooltip("If true, collision enter events from the launching spatula are ignored while the launch collision filter is active even if Physics.IgnoreCollision missed a collider pair.")]
    public bool filterSpatulaLandingEventsDuringIgnoreWindow = true;

    [Tooltip("Optional debug logging for collision events ignored by the launch collision filter.")]
    public bool logIgnoredLaunchCollisions = false;

    [Tooltip("When true, collisions while the pancake is still moving upward do not count as a completed landing.")]
    public bool requireFallingBeforeLanding = true;

    [Tooltip("Vertical velocity must be at or below this value before a collision can count as landing when Require Falling Before Landing is enabled.")]
    public float landingMaxUpwardVelocity = 0.15f;

    [Header("Debug - Flight Metrics")]
    [Tooltip("Logs launch force, apex height, height gain, airtime, and whether the flight ended by landing or failsafe.")]
    public bool logFlightMetrics = true;
    [Tooltip("Optional spammy logging for LaunchFlip calls rejected because the pancake is not scooped or is still in scoop grace period.")]
    public bool logRejectedLaunchRequests = false;

    private int flightDebugId;
    private bool trackingFlight;
    private float flightStartTime;
    private float flightStartY;
    private float flightPeakY;
    private float flightPeakTime;
    private float flightLastStrength;
    private float flightLastUpForce;
    private float flightLastSloppyForce;
    private float flightLaunchVelocityY;

    private Transform lastScoopSpatula;
    private Collider[] pancakeColliders = System.Array.Empty<Collider>();
    private Collider[] lastScoopSpatulaColliders = System.Array.Empty<Collider>();
    private Coroutine restoreLaunchCollisionRoutine;
    private float spatulaCollisionIgnoreUntil = -999f;

    public bool IsScooped { get; private set; } 
    public bool IsAirborne => airborne;
    private bool airborne = false;
    private bool pendingSideSwapAfterLaunch = false;
    private bool sideSwapAppliedThisFlight = false;
    private float lastLaunchTime = -999f;
    private float lastScoopTime = -999f;
    private Vector3 offCenterOffset;
    private Vector3 scoopedLocalOffset;
    private bool hasScoopedLocalOffset = false;
    private float scoopedWorldZ;
    private Coroutine scoopMoveRoutine;
    private PancakeSpawner spawner;

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        spawner = FindObjectOfType<PancakeSpawner>();
        if (spawner == null)
        {
            Debug.LogError("PancakeSpawner not found!");
        }

        PancakeRegistry.Instance.Register(this);
    }

    void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (PancakeRegistry.TryGetInstance(out PancakeRegistry registry))
        {
            registry.Unregister(this);
        }

        RestoreLaunchCollisionIgnore();
    }

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

        TrackFlightHeight();

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

        CacheLaunchCollisionContext(spatula);

        if (readVisualSideOnScoop)
        {
            SyncSideStateFromCurrentVisual("scoop");
        }

        IsScooped = true;
        rb.isKinematic = true; 
        timeScooped = Time.time; // flip delay timer
        lastScoopTime = Time.time;
        scoopedWorldZ = transform.position.z;

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
            pendingSideSwapAfterLaunch = false;
            sideSwapAppliedThisFlight = false;
            rb.isKinematic = false; // Turn gravity back on
            Debug.Log("Pancake Dropped.");
        }
    }

    // Called by the SpatulaController when a valid swipe/flick is detected
    public bool LaunchFlip(float strength)
    {
        if (!IsScooped)
        {
            LogRejectedLaunchRequest("not-scooped", strength);
            return false;
        }

        if (Time.time - timeScooped <= scoopGracePeriod)
        {
            LogRejectedLaunchRequest("scoop-grace-period", strength);
            return false;
        }

        StopScoopMoveRoutine();
        airborne = true;
        IsScooped = false;
        pendingSideSwapAfterLaunch = swapSideOnSuccessfulFlip;
        sideSwapAppliedThisFlight = false;
        lastLaunchTime = Time.time;

        // Turn physics back on for the launch
        rb.isKinematic = false;

        BeginLaunchCollisionIgnore();

        // Give the pancake a tiny clearance nudge so the launch impulse is not immediately eaten
        // by a lingering OnCollisionStay contact with the spatula/counter.
        if (launchClearance > 0f)
        {
            Vector3 clearedPosition = rb.position + Vector3.up * launchClearance;
            rb.position = clearedPosition;
            transform.position = clearedPosition;
            Physics.SyncTransforms();
        }

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

        BeginFlightDebug(strength, upForce, sloppyForce.magnitude);

        SoundManager.Instance.PlayFromCue(SoundCues.FlipPancake, transform.position);

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

        if (IsPancakeRuined())
        {
            SoundManager.Instance.PlayFromCue(
                SoundCues.RuinedPancake,
                transform.position,
                CuePlaybackPolicy<SoundCues>.YieldToPlayingCue);
        }
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
        RestoreLaunchCollisionIgnore();
        EndFlightDebug("reset", null);
        airborne = false;
        IsScooped = false;
        pendingSideSwapAfterLaunch = false;
        sideSwapAppliedThisFlight = false;
        lastScoopTime = -999f;
        if (rb != null) rb.isKinematic = false;
        stats?.ResetForNewRound(!clearToppingsOnReset);

        if (clearToppingsOnReset)
        {
            Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                Transform child = childTransforms[i];
                if (child == null || child.gameObject == gameObject)
                {
                    continue;
                }

                if (child.GetComponent("ToppingController") != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        
        if (spawnPoint != null)
        {
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }
        else if (spawner != null)
        {
            spawner.RespawnPancake(this);
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

    void CacheLaunchCollisionContext(Transform spatula)
    {
        lastScoopSpatula = spatula;

        if (pancakeColliders == null || pancakeColliders.Length == 0)
        {
            pancakeColliders = GetComponentsInChildren<Collider>();
        }

        lastScoopSpatulaColliders = spatula != null
            ? spatula.GetComponentsInChildren<Collider>()
            : System.Array.Empty<Collider>();
    }

    void BeginLaunchCollisionIgnore()
    {
        if (!ignoreSpatulaCollisionsAfterLaunch)
        {
            return;
        }

        if (pancakeColliders == null || pancakeColliders.Length == 0)
        {
            pancakeColliders = GetComponentsInChildren<Collider>();
        }

        if (lastScoopSpatula != null)
        {
            lastScoopSpatulaColliders = lastScoopSpatula.GetComponentsInChildren<Collider>();
        }

        if (lastScoopSpatulaColliders == null || lastScoopSpatulaColliders.Length == 0)
        {
            return;
        }

        RestoreLaunchCollisionIgnore();

        float duration = Mathf.Max(0f, spatulaCollisionIgnoreDuration);
        spatulaCollisionIgnoreUntil = Time.time + duration;

        SetLaunchCollisionIgnore(true);

        if (duration > 0f)
        {
            restoreLaunchCollisionRoutine = StartCoroutine(RestoreLaunchCollisionIgnoreAfterDelay(duration));
        }
    }

    IEnumerator RestoreLaunchCollisionIgnoreAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RestoreLaunchCollisionIgnore();
    }

    void RestoreLaunchCollisionIgnore()
    {
        if (restoreLaunchCollisionRoutine != null)
        {
            StopCoroutine(restoreLaunchCollisionRoutine);
            restoreLaunchCollisionRoutine = null;
        }

        SetLaunchCollisionIgnore(false);
        spatulaCollisionIgnoreUntil = -999f;
    }

    void SetLaunchCollisionIgnore(bool shouldIgnore)
    {
        if (pancakeColliders == null || lastScoopSpatulaColliders == null)
        {
            return;
        }

        for (int pancakeIndex = 0; pancakeIndex < pancakeColliders.Length; pancakeIndex++)
        {
            Collider pancakeCollider = pancakeColliders[pancakeIndex];
            if (pancakeCollider == null)
            {
                continue;
            }

            for (int spatulaIndex = 0; spatulaIndex < lastScoopSpatulaColliders.Length; spatulaIndex++)
            {
                Collider spatulaCollider = lastScoopSpatulaColliders[spatulaIndex];
                if (spatulaCollider == null || spatulaCollider == pancakeCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(pancakeCollider, spatulaCollider, shouldIgnore);
            }
        }
    }

    bool IsWithinSpatulaCollisionIgnoreWindow()
    {
        return Time.time <= spatulaCollisionIgnoreUntil;
    }

    bool CollisionIsWithLaunchingSpatula(Collision collision)
    {
        if (collision == null || collision.collider == null || lastScoopSpatulaColliders == null)
        {
            return false;
        }

        Collider hitCollider = collision.collider;

        for (int i = 0; i < lastScoopSpatulaColliders.Length; i++)
        {
            Collider spatulaCollider = lastScoopSpatulaColliders[i];
            if (spatulaCollider == hitCollider)
            {
                return true;
            }
        }

        return false;
    }

    bool ShouldIgnoreLaunchCollision(Collision collision)
    {
        if (!filterSpatulaLandingEventsDuringIgnoreWindow || !IsWithinSpatulaCollisionIgnoreWindow())
        {
            return false;
        }

        return CollisionIsWithLaunchingSpatula(collision);
    }

    bool CanCollisionCountAsLanding(Collision collision)
    {
        if (ShouldIgnoreLaunchCollision(collision))
        {
            if (logIgnoredLaunchCollisions)
            {
                string hitObject = collision != null && collision.collider != null ? collision.collider.name : "none";
                Debug.Log($"PANCAKE LANDING IGNORED | reason=launch-spatula-collision | hit={hitObject} | timeSinceLaunch={Time.time - lastLaunchTime:F2}");
            }

            return false;
        }

        if (Time.time - lastLaunchTime <= launchGracePeriod)
        {
            return false;
        }

        if (requireFallingBeforeLanding && rb != null && rb.velocity.y > landingMaxUpwardVelocity)
        {
            if (logIgnoredLaunchCollisions)
            {
                string hitObject = collision != null && collision.collider != null ? collision.collider.name : "none";
                Debug.Log($"PANCAKE LANDING IGNORED | reason=still-moving-up | hit={hitObject} | velocityY={rb.velocity.y:F2}");
            }

            return false;
        }

        return true;
    }

    Vector3 GetScoopTargetPosition(Transform spatula)
    {
        if (spatula == null)
        {
            return ConstrainScoopedPosition(transform.position);
        }

        if (hasScoopedLocalOffset)
        {
            return ConstrainScoopedPosition(spatula.TransformPoint(scoopedLocalOffset));
        }

        return ConstrainScoopedPosition(spatula.TransformPoint(scoopOffset));
    }

    Vector3 ConstrainScoopedPosition(Vector3 position)
    {
        if (keepScoopedWorldZ && IsScooped)
        {
            position.z = scoopedWorldZ;
        }

        return position;
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
            Quaternion targetRot = GetScoopedSideRotation(spatula);

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
                Quaternion targetRot = GetScoopedSideRotation(spatula);

                transform.SetPositionAndRotation(targetPos, targetRot);
                yield return null;
            }
        }

        scoopMoveRoutine = null;
    }
    
    Quaternion GetScoopedSideRotation(Transform spatula)
    {
        Quaternion baseRotation = spatula.rotation * Quaternion.Euler(scoopRotationOffsetEuler);

        if (!snapVisualSideOnScoop)
        {
            return baseRotation;
        }

        return GetSideAdjustedRotation(baseRotation);
    }

    Quaternion GetSideAdjustedRotation(Quaternion baseRotation)
    {
        if (stats == null)
        {
            return baseRotation;
        }

        bool showOriginalTopSideUp = stats.topSideUp;
        if (invertVisualSideState)
        {
            showOriginalTopSideUp = !showOriginalTopSideUp;
        }

        if (showOriginalTopSideUp)
        {
            return baseRotation;
        }

        return baseRotation * Quaternion.Euler(sideFlipRotationOffsetEuler);
    }

    void SyncSideStateFromCurrentVisual(string reason)
    {
        if (stats == null)
        {
            return;
        }

        bool visualTopSideFacingWorldUp = Vector3.Dot(transform.up, Vector3.up) >= 0f;
        bool desiredTopSideUpState = invertVisualSideState
            ? !visualTopSideFacingWorldUp
            : visualTopSideFacingWorldUp;

        if (stats.topSideUp == desiredTopSideUpState)
        {
            return;
        }

        stats.topSideUp = desiredTopSideUpState;

        if (logSideChanges)
        {
            Debug.Log($"PANCAKE SIDE SYNCED | reason={reason} | topSideUp={stats.topSideUp}");
        }
    }

    void ApplyPendingSideSwap(string reason)
    {
        if (!pendingSideSwapAfterLaunch || sideSwapAppliedThisFlight || stats == null)
        {
            pendingSideSwapAfterLaunch = false;
            return;
        }

        stats.RegisterFlip();
        sideSwapAppliedThisFlight = true;
        pendingSideSwapAfterLaunch = false;

        if (logSideChanges)
        {
            Debug.Log($"PANCAKE SIDE SWAPPED | reason={reason} | topSideUp={stats.topSideUp} | flipCount={stats.flipCount}");
        }
    }

    void CompleteAirborneLanding(string result, Collision collision, bool playLandingSound)
    {
        RestoreLaunchCollisionIgnore();
        ApplyPendingSideSwap(result);

        if (snapVisualSideOnLanding)
        {
            SnapVisualRotationToTrackedSide(collision);
        }

        EndFlightDebug(result, collision);
        airborne = false;

        if (playLandingSound)
        {
            SoundManager.Instance.PlayFromCue(SoundCues.PancakeLand, transform.position);
            Debug.Log("Pancake Landed! Ready to scoop.");
        }
    }

    void SnapVisualRotationToTrackedSide(Collision collision)
    {
        Vector3 surfaceUp = GetLandingSurfaceUp(collision);
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, surfaceUp);

        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(transform.right, surfaceUp);
        }

        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
        }

        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        Quaternion baseRotation = Quaternion.LookRotation(flatForward.normalized, surfaceUp);
        Quaternion sideRotation = GetSideAdjustedRotation(baseRotation);

        transform.rotation = sideRotation;

        if (rb != null)
        {
            rb.rotation = sideRotation;
            rb.angularVelocity = Vector3.zero;
        }
    }

    Vector3 GetLandingSurfaceUp(Collision collision)
    {
        if (collision == null || collision.contactCount == 0)
        {
            return Vector3.up;
        }

        Vector3 normal = collision.GetContact(0).normal;

        // If the pancake hits a wall/ceiling, do not let that surface flip the pancake sideways or stick it to the ceiling.
        // Treat the game state as the source of truth and keep the pancake visually upright in world space.
        if (normal.y < 0.25f)
        {
            return Vector3.up;
        }

        return normal.normalized;
    }

    void TrackFlightHeight()
    {
        if (!trackingFlight)
        {
            return;
        }

        float currentY = transform.position.y;
        if (currentY > flightPeakY)
        {
            flightPeakY = currentY;
            flightPeakTime = Time.time;
        }
    }

    void BeginFlightDebug(float strength, float upForce, float sloppyForce)
    {
        if (!logFlightMetrics)
        {
            return;
        }

        flightDebugId++;
        trackingFlight = true;
        flightStartTime = Time.time;
        flightStartY = transform.position.y;
        flightPeakY = flightStartY;
        flightPeakTime = flightStartTime;
        flightLastStrength = strength;
        flightLastUpForce = upForce;
        flightLastSloppyForce = sloppyForce;
        flightLaunchVelocityY = rb != null ? rb.velocity.y : 0f;

        Debug.Log(
            $"PANCAKE FLIGHT START #{flightDebugId} | strength={strength:F2} | upForce={upForce:F2} | sloppyForce={sloppyForce:F2} | launchY={flightStartY:F2} | launchVelY={flightLaunchVelocityY:F2}"
        );
    }

    void EndFlightDebug(string result, Collision collision)
    {
        if (!logFlightMetrics || !trackingFlight)
        {
            return;
        }

        trackingFlight = false;

        float endY = transform.position.y;
        float airtime = Time.time - flightStartTime;
        float timeToPeak = flightPeakTime - flightStartTime;
        float heightGain = flightPeakY - flightStartY;
        string hitObject = collision != null && collision.collider != null ? collision.collider.name : "none";

        Debug.Log(
            $"PANCAKE FLIGHT END #{flightDebugId} | result={result} | hit={hitObject} | heightGain={heightGain:F2} | peakY={flightPeakY:F2} | startY={flightStartY:F2} | endY={endY:F2} | airtime={airtime:F2} | timeToPeak={timeToPeak:F2} | strength={flightLastStrength:F2} | upForce={flightLastUpForce:F2} | sloppyForce={flightLastSloppyForce:F2} | launchVelY={flightLaunchVelocityY:F2}"
        );
    }

    void LogRejectedLaunchRequest(string reason, float strength)
    {
        if (!logRejectedLaunchRequests)
        {
            return;
        }

        Debug.Log(
            $"PANCAKE LAUNCH BLOCKED | reason={reason} | strength={strength:F2} | isScooped={IsScooped} | airborne={airborne} | timeSinceScoop={Time.time - timeScooped:F2}"
        );
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!airborne)
        {
            return;
        }

        if (!CanCollisionCountAsLanding(collision))
        {
            return;
        }

        CompleteAirborneLanding("landed", collision, playLandingSound: true);
    }

    void OnCollisionStay(Collision collision)
    {
        if (!airborne)
        {
            return;
        }

        if (ShouldIgnoreLaunchCollision(collision))
        {
            return;
        }

        float timeSinceLaunch = Time.time - lastLaunchTime;
        float minimumFailsafeTime = Mathf.Max(launchGracePeriod, failsafeMinimumAirtime);

        if (timeSinceLaunch <= minimumFailsafeTime)
        {
            return;
        }

        float heightAboveLaunch = transform.position.y - flightStartY;
        float verticalSpeed = rb != null ? rb.velocity.y : 0f;

        bool stillNearLaunchHeight = heightAboveLaunch <= failsafeMaxHeightAboveLaunch;
        bool notMovingUpMeaningfully = verticalSpeed <= failsafeMaxVerticalSpeed;

        if (stillNearLaunchHeight && notMovingUpMeaningfully)
        {
            CompleteAirborneLanding("failsafe-resting", collision, playLandingSound: false);
            Debug.Log(
                $"Pancake Failsafe: Reset airborne to false while resting. heightAboveLaunch={heightAboveLaunch:F2} verticalSpeed={verticalSpeed:F2}"
            );
        }
    }

    bool IsPancakeRuined()
    {
        if (stats == null)
        {
            return false;
        }

        return stats.topCookAmount >= BurntCookThreshold ||
               stats.bottomCookAmount >= BurntCookThreshold;
    }
}