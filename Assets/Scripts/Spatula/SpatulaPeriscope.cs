using UnityEngine;

[System.Serializable]
public struct BoundingBox
{
    public Vector3 center;
    public Vector3 size;
}

public class SpatulaPeriscope : MonoBehaviour
{
    [Header("References")]
    public SpatulaController spatulaController;
    
    [Header("Positioning")]
    public Vector3 offset = new (0, 0.5f, 0);
    public BoundingBox boundingBox = new() { center = Vector3.zero, size = new Vector3(10f, 10f, 10f) };

    [Header("Flip Tracking")]
    [Tooltip("How close the spatula must be to start tracking a flipped pancake")]
    public float trackingTriggerDistance = 2.5f;
    [Tooltip("How quickly the periscope rotates to face the tracked pancake")]
    public float trackingRotateSpeed = 10f;
    [Tooltip("How quickly the periscope returns to its default rotation after tracking")]
    public float resetRotateSpeed = 8f;
    [Tooltip("Optional look-at offset on pancake center")]
    public Vector3 pancakeLookOffset = new(0f, 0.1f, 0f);

    private PancakeController trackedPancake;
    private Quaternion defaultRotation;

    void Awake()
    {
        defaultRotation = transform.rotation;
    }

    void LateUpdate()
    {
        if (spatulaController == null)
            return;

        Vector3 targetPosition = spatulaController.transform.position;
        targetPosition += offset;
        Vector3 halfSize = boundingBox.size * 0.5f;
        targetPosition.x = Mathf.Clamp(targetPosition.x, boundingBox.center.x - halfSize.x, boundingBox.center.x + halfSize.x);
        targetPosition.y = Mathf.Clamp(targetPosition.y, boundingBox.center.y - halfSize.y, boundingBox.center.y + halfSize.y);
        targetPosition.z = Mathf.Clamp(targetPosition.z, boundingBox.center.z - halfSize.z, boundingBox.center.z + halfSize.z);
        transform.position = targetPosition;

        UpdateTrackedPancake();
        RotateTowardTrackedPancake();
    }

    void UpdateTrackedPancake()
    {
        if (trackedPancake != null)
        {
            if (trackedPancake.IsAirborne)
            {
                float trackedDistance = Vector3.Distance(spatulaController.transform.position, trackedPancake.transform.position);
                if (trackedDistance <= trackingTriggerDistance)
                {
                    return;
                }
            }

            trackedPancake = null;
        }

        PancakeController[] pancakes = FindObjectsOfType<PancakeController>();
        float bestDistance = Mathf.Infinity;

        foreach (PancakeController pancake in pancakes)
        {
            if (!pancake.IsAirborne)
            {
                continue;
            }

            float distanceToSpatula = Vector3.Distance(spatulaController.transform.position, pancake.transform.position);
            if (distanceToSpatula > trackingTriggerDistance || distanceToSpatula >= bestDistance)
            {
                continue;
            }

            bestDistance = distanceToSpatula;
            trackedPancake = pancake;
        }
    }

    void RotateTowardTrackedPancake()
    {
        if (trackedPancake == null || !trackedPancake.IsAirborne)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, resetRotateSpeed * Time.deltaTime);
            return;
        }

        Vector3 lookTarget = trackedPancake.transform.position + pancakeLookOffset;
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, trackingRotateSpeed * Time.deltaTime);
    }
}