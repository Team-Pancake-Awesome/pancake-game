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
    [Tooltip("Camera used for periscope FOV gizmo drawing. If null, a child camera is used when available.")]
    public Camera periscopeCamera;
    
    [Header("Positioning")]
    public Vector3 offset = new (0, 0.5f, 0);
    public Vector3 rotationOffset = Vector3.zero;
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

    [Header("Gizmos")]
    [Tooltip("Draw the periscope camera frustum in Scene view when selected.")]
    public bool drawFovGizmo = true;

    private PancakeController trackedPancake;
    private Quaternion defaultRotation;

    Quaternion GetRotationOffset()
    {
        return Quaternion.Euler(rotationOffset);
    }

    Quaternion GetDefaultRotation()
    {
        return defaultRotation * GetRotationOffset();
    }

    void Awake()
    {
        defaultRotation = transform.rotation;

        if (periscopeCamera == null)
        {
            periscopeCamera = GetComponentInChildren<Camera>();
        }
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
            transform.rotation = Quaternion.Slerp(transform.rotation, GetDefaultRotation(), resetRotateSpeed * Time.deltaTime);
            return;
        }

        Vector3 lookTarget = trackedPancake.transform.position + pancakeLookOffset;
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up) * GetRotationOffset();
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, trackingRotateSpeed * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boundingBox.center, boundingBox.size);

        DrawCameraFrustumGizmo();

        if (spatulaController != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, spatulaController.transform.position);
        }

        if (trackedPancake != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, trackedPancake.transform.position);
        }        
    }

    void DrawCameraFrustumGizmo()
    {
        if (!drawFovGizmo)
        {
            return;
        }

        Camera gizmoCamera = periscopeCamera != null ? periscopeCamera : GetComponentInChildren<Camera>();
        if (gizmoCamera == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(gizmoCamera.transform.position, gizmoCamera.transform.rotation, Vector3.one);
        Gizmos.DrawFrustum(
            Vector3.zero,
            gizmoCamera.fieldOfView,
            gizmoCamera.farClipPlane,
            gizmoCamera.nearClipPlane,
            gizmoCamera.aspect);
        Gizmos.matrix = previousMatrix;
    }
}