using UnityEngine;

public class SpatulaController : MonoBehaviour
{
    public ArduinoReader reader;

    [Header("Lateral Position")]
    public float rollDeadzone = 6f;
    public float maxRollForFullMove = 25f;
    public float maxMoveX = 3f;
    public float moveLerpSpeed = 8f;
    public bool invertRoll = true;

    [Header("Lock")]
    public KeyCode lockKey = KeyCode.Space;
    [Range(0f, 1f)]
    public float lockedMoveMultiplier = 0f; // 0 = full lock, 0.2 = reduced movement

    [Header("Visual Tilt")]
    public float maxVisualPitch = 20f;
    public float visualTiltSmooth = 8f;
    public bool invertPitchVisual = false;

    private float currentX;

    void Update()
    {
        if (reader == null)
            return;

        float roll = reader.roll;
        float pitch = reader.pitch;

        if (invertRoll)
            roll = -roll;

        float targetX = currentX;

        if (!Input.GetKey(lockKey))
        {
            float absRoll = Mathf.Abs(roll);

            if (absRoll < rollDeadzone)
            {
                roll = 0f;
            }
            else
            {
                float rollSign = Mathf.Sign(roll);
                float adjustedRoll = absRoll - rollDeadzone;
                float usableRange = Mathf.Max(0.001f, maxRollForFullMove - rollDeadzone);
                float normalized = Mathf.Clamp01(adjustedRoll / usableRange);

                targetX = rollSign * normalized * maxMoveX;
            }
        }
        else
        {
            targetX = Mathf.Lerp(currentX, 0f, lockedMoveMultiplier);
        }

        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * moveLerpSpeed);

        Vector3 pos = transform.position;
        pos.x = currentX;
        transform.position = pos;

        float visualPitch = Mathf.Clamp(pitch, -maxVisualPitch, maxVisualPitch);
        if (invertPitchVisual)
            visualPitch = -visualPitch;

        Quaternion targetRot = Quaternion.Euler(visualPitch, 0f, 0f);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * visualTiltSmooth);
    }
}