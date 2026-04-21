using UnityEngine;
using System.Collections;

public class PancakeController : MonoBehaviour
{
    private const float BurntCookThreshold = 0.92f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

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

    [Header("Flip Outcome")]
    [Tooltip("When true, a valid launched flip will resolve to the opposite side on landing")]
    public bool successfulFlipAlwaysSwapsSides = true;

    [Header("Side Visuals")]
    [Tooltip("Optional visual root that should face upward when the pancake's top side is up")]
    public Transform pancakeVisualRoot;
    [Tooltip("Optional child that represents the pancake's top face")]
    public Renderer topFaceRenderer;
    [Tooltip("Optional child that represents the pancake's bottom face")]
    public Renderer bottomFaceRenderer;
    [Tooltip("Optional renderer for the pancake body/side wall")]
    public Renderer bodyRenderer;

    [Header("Landing Side Swap Presentation")]
    [Tooltip("How high the pancake visual hops when a successful landing resolves a side swap")]
    public float landingSideSwapBounceHeight = 0.04f;
    [Tooltip("How long the landing side swap bounce lasts")]
    public float landingSideSwapBounceDuration = 0.18f;

    [Header("Cook Visuals")]
    public Color uncookedColor = new(1f, 0.9f, 0.75f, 1f);
    public Color burntColor = new(0.22f, 0.12f, 0.06f, 1f);

    [Header("Testing")]
    public Transform spawnPoint;
    public KeyCode resetKey = KeyCode.R;
    public KeyCode testLaunchKey = KeyCode.T;

    [Header("Failsafe Tuning")]
    public float launchGracePeriod = 0.2f;

    public bool IsScooped { get; private set; } 
    public bool IsAirborne => airborne;
    private bool airborne = false;
    private bool pendingSuccessfulFlip = false;
    private float lastLaunchTime = -999f;
    private float lastScoopTime = -999f;
    private Vector3 offCenterOffset;
    private Vector3 scoopedLocalOffset;
    private bool hasScoopedLocalOffset = false;
    private float scoopedWorldZ;
    private Coroutine scoopMoveRoutine;
    private Coroutine landingSideSwapRoutine;
    private PancakeSpawner spawner;
    private MaterialPropertyBlock materialPropertyBlock;

    void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        materialPropertyBlock = new MaterialPropertyBlock();

        SyncSideVisuals();
        UpdateCookVisuals();
    }

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
        SyncSideVisuals();
        UpdateCookVisuals();
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

        if (landingSideSwapRoutine != null)
        {
            StopCoroutine(landingSideSwapRoutine);
            landingSideSwapRoutine = null;
        }

        // Make sure the visual root is back in a valid side state before scooping.
        SyncSideVisuals();

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
            pendingSuccessfulFlip = false;
            rb.isKinematic = false; // Turn gravity back on
            SyncSideVisuals();
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

        // Phase 1 rule:
        // keep the chaotic visual motion, but resolve the gameplay side swap on landing.
        pendingSuccessfulFlip = successfulFlipAlwaysSwapsSides;

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
        UpdateCookVisuals();

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

        if (landingSideSwapRoutine != null)
        {
            StopCoroutine(landingSideSwapRoutine);
            landingSideSwapRoutine = null;
        }

        airborne = false;
        IsScooped = false;
        pendingSuccessfulFlip = false;
        lastScoopTime = -999f;
        if (rb != null) rb.isKinematic = false;
        stats?.ResetForNewRound(!clearToppingsOnReset);
        SyncSideVisuals();
        UpdateCookVisuals();

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
            Quaternion targetRot = GetScoopedRotation(spatula);

            Vector3 syncedPos = Vector3.Lerp(startPos, targetPos, easedT);
            Quaternion syncedRot = Quaternion.Slerp(startRot, targetRot, easedT);

            transform.SetPositionAndRotation(syncedPos, syncedRot);

            yield return null;
        }

        if (IsScooped && spatula != null)
        {
            while (IsScooped && spatula != null)
            {
                Vector3 targetPos = GetScoopTargetPosition(spatula);
                Quaternion targetRot = GetScoopedRotation(spatula);

                transform.SetPositionAndRotation(targetPos, targetRot);
                yield return null;
            }
        }

        scoopMoveRoutine = null;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!airborne || Time.time - lastLaunchTime <= launchGracePeriod)
        {
            return;
        }

        if (!IsLandingSurface(collision))
        {
            return;
        }

        ResolveLanding();
    }

   void OnCollisionStay(Collision collision)
    {
        if (!airborne || Time.time - lastLaunchTime <= launchGracePeriod)
        {
            return;
        }

        if (!IsLandingSurface(collision))
        {
            return;
        }

        ResolveLanding();
        Debug.Log("Pancake Failsafe: Reset airborne to false while resting.");
    }

    bool IsLandingSurface(Collision collision)
    {
        if (collision == null || collision.contactCount == 0)
        {
            return false;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            // We only want surfaces that are basically "under" the pancake.
            // If the normal points upward enough, this is a landing-style contact.
            if (contact.normal.y > 0.6f)
            {
                return true;
            }
        }

        return false;
    }

    void ResolveLanding()
    {
        airborne = false;

        if (pendingSuccessfulFlip && stats != null)
        {
            stats.ResolveSuccessfulSideSwap();
            pendingSuccessfulFlip = false;

            PlayLandingSideSwapPresentation();

            UpdateCookVisuals();
            SoundManager.Instance.PlayFromCue(SoundCues.PancakeLand, transform.position);
            Debug.Log($"Pancake Landed! Flip resolved. TopSideUp: {stats.topSideUp}");
        }
        else
        {
            pendingSuccessfulFlip = false;
            SyncSideVisuals();
            SoundManager.Instance.PlayFromCue(SoundCues.PancakeLand, transform.position);
            Debug.Log("Pancake Landed! Ready to scoop.");
        }
    }

    void PlayLandingSideSwapPresentation()
    {
        if (pancakeVisualRoot == null)
        {
            Debug.LogWarning("Landing side swap presentation skipped: pancakeVisualRoot is null.");
            SyncSideVisuals();
            return;
        }

        Debug.Log("Starting landing side swap bounce.");

        if (landingSideSwapRoutine != null)
        {
            StopCoroutine(landingSideSwapRoutine);
        }

        landingSideSwapRoutine = StartCoroutine(PlayLandingSideSwapBounce());
    }

    void SyncSideVisuals()
    {
        if (stats == null)
        {
            return;
        }

        if (topFaceRenderer != null)
        {
            topFaceRenderer.gameObject.SetActive(true);
        }

        if (bottomFaceRenderer != null)
        {
            bottomFaceRenderer.gameObject.SetActive(true);
        }

        if (pancakeVisualRoot != null)
        {
            pancakeVisualRoot.localPosition = Vector3.zero;
            pancakeVisualRoot.localRotation = Quaternion.Euler(stats.topSideUp ? 0f : 180f, 0f, 0f);
        }
    }

    void UpdateCookVisuals()
    {
        if (stats == null)
        {
            return;
        }

        UpdateRendererCookColor(topFaceRenderer, stats.topCookAmount);
        UpdateRendererCookColor(bottomFaceRenderer, stats.bottomCookAmount);
        UpdateRendererCookColor(bodyRenderer, stats.AverageCookAmount);
    }

    void UpdateRendererCookColor(Renderer targetRenderer, float cookAmount)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (materialPropertyBlock == null)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        Color cookedColor = Color.Lerp(uncookedColor, burntColor, Mathf.Clamp01(cookAmount));

        targetRenderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetColor(BaseColorId, cookedColor);
        materialPropertyBlock.SetColor(ColorId, cookedColor);
        targetRenderer.SetPropertyBlock(materialPropertyBlock);
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

    Quaternion GetScoopedRotation(Transform spatula)
    {
        return spatula.rotation * Quaternion.Euler(scoopRotationOffsetEuler);
    }

    float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;

        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
    

    private IEnumerator PlayLandingSideSwapBounce()
    {
        if (pancakeVisualRoot == null)
        {
            Debug.LogWarning("Landing side swap skipped: pancakeVisualRoot is null.");
            yield break;
        }

        Vector3 baseLocalPosition = Vector3.zero;
        
        // 1. Determine Rotations
        // The stats have already resolved, so topSideUp is currently our TARGET state.
        float targetX = stats.topSideUp ? 0f : 180f;
        
        // By forcing startX to be exactly targetX - 180, we guarantee a continuous 
        // 180-degree rotation in the same direction every time, avoiding Quaternion shortest-path weirdness.
        float startX = targetX - 180f; 

        float duration = Mathf.Max(0.0001f, landingSideSwapBounceDuration);
        float elapsed = 0f;

        Transform visualParent = pancakeVisualRoot.parent;
        
        // Calculate up-vector relative to the parent so the bounce doesn't skew if the pancake landed on a slope
        Vector3 localWorldUp = visualParent != null
            ? visualParent.InverseTransformDirection(Vector3.up).normalized
            : Vector3.up;

        while (elapsed < duration && pancakeVisualRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // --- The Hop ---
            // Mathf.Sin creates a perfect 0 -> 1 -> 0 arc. Apex is at t = 0.5.
            float bounceT = Mathf.Sin(t * Mathf.PI);
            float yOffset = bounceT * landingSideSwapBounceHeight;

            // --- The Turnover ---
            // SmoothStep creates a buttery ease-in / ease-out curve.
            // At exactly t = 0.5 (our vertical apex), rotationT will be exactly 0.5 (straight up).
            float rotationT = Mathf.SmoothStep(0f, 1f, t);
            float currentX = Mathf.Lerp(startX, targetX, rotationT);

            pancakeVisualRoot.localPosition = baseLocalPosition + (localWorldUp * yOffset);
            pancakeVisualRoot.localRotation = Quaternion.Euler(currentX, 0f, 0f);

            yield return null;
        }

        // 3. Guarantee Clean End State
        if (pancakeVisualRoot != null)
        {
            pancakeVisualRoot.localPosition = baseLocalPosition;
            pancakeVisualRoot.localRotation = Quaternion.Euler(targetX, 0f, 0f);
        }

        landingSideSwapRoutine = null;
    }







    // IEnumerator PlayLandingSideSwapBounce()
    // {
    //     Debug.Log("PlayLandingSideSwapBounce running.");
    //     if (pancakeVisualRoot == null)
    //     {
    //         yield break;
    //     }

    //     Vector3 baseLocalPosition = Vector3.zero;

    //     float startX = NormalizeSignedAngle(pancakeVisualRoot.localEulerAngles.x);
    //     float targetX = stats != null && stats.topSideUp ? 0f : 180f;
    //     float midX = 90f;

    //     float duration = Mathf.Max(0.0001f, landingSideSwapBounceDuration);
    //     float elapsed = 0f;

    //     Transform visualParent = pancakeVisualRoot.parent;

    //     // Convert world-up into the visual root parent's local space,
    //     // so the bounce always reads as an upward pop instead of drifting in local axes.
    //     Vector3 localWorldUp = visualParent != null
    //         ? visualParent.InverseTransformDirection(Vector3.up).normalized
    //         : Vector3.up;

    //     while (elapsed < duration && pancakeVisualRoot != null)
    //     {
            
    //         elapsed += Time.deltaTime;
    //         float t = Mathf.Clamp01(elapsed / duration);

    //         // Small hop arc: 0 -> 1 -> 0
    //         float bounceT = Mathf.Sin(t * Mathf.PI);
    //         float yOffset = bounceT * landingSideSwapBounceHeight;

    //         float currentX;
    //         Debug.Log($"Bounce t={t:F2} yOffset={yOffset:F3}");
    //         if (t < 0.5f)
    //         {
    //             float riseT = Mathf.SmoothStep(0f, 1f, t / 0.5f);
    //             currentX = Mathf.LerpAngle(startX, midX, riseT);
    //         }
    //         else
    //         {
    //             float fallT = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.5f);
    //             currentX = Mathf.LerpAngle(midX, targetX, fallT);
    //         }

    //         pancakeVisualRoot.localPosition = baseLocalPosition + (localWorldUp * yOffset);
    //         pancakeVisualRoot.localRotation = Quaternion.Euler(currentX, 0f, 0f);

    //         yield return null;
    //     }

    //     if (pancakeVisualRoot != null)
    //     {
    //         pancakeVisualRoot.localPosition = baseLocalPosition;
    //         pancakeVisualRoot.localRotation = Quaternion.Euler(targetX, 0f, 0f);
    //     }

    //     landingSideSwapRoutine = null;
    // }
}