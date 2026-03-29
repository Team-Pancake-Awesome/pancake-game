using UnityEngine;

public class SpatulaController : MonoBehaviour
{
    public ArduinoReader reader;

    [Header("Pitch Rotation")]
    public float rotationSmoothSpeed = 18f;
    public float minPitchInput = 30f;   // spatula down
    public float maxPitchInput = -40f;  // spatula up
    public float minAngle = -15f;       // visual down angle
    public float maxAngle = 45f;        // visual up angle

    [Header("Flip Snap")]
    public float snapGyroThreshold = 1.2f;
    public float requiredUpPitch = -8f;
    public float snapAngle = 22f;
    public float snapReturnSpeed = 12f;

    [Header("Roll Movement")]
    public float moveMultiplier = 0.13f;
    public float moveSmoothSpeed = 8f;
    public float rollDeadzone = 2f;
    public bool invertRoll = true;

    [Header("Lock")]
    public KeyCode lockKey = KeyCode.Space;
    [Range(0f, 1f)]
    public float lockedMoveMultiplier = 0f;

    private float targetX;
    private float snapOffset = 0f;

    void Start()
    {
        targetX = transform.position.x;
    }

    void Update()
    {
        if (reader == null)
            return;

        float pitch = reader.pitch;
        float roll = reader.roll;
        float gyro = reader.gyroY;

        if (invertRoll)
            roll = -roll;

        // Correct pitch mapping:
        // pitch 30  -> down  -> minAngle
        // pitch -40 -> up    -> maxAngle
        float normalizedPitch = Mathf.InverseLerp(minPitchInput, maxPitchInput, pitch);
        normalizedPitch = Mathf.Clamp01(normalizedPitch);
        float targetPitch = Mathf.Lerp(minAngle, maxAngle, normalizedPitch);

        // Quick visual pop upward during a valid flip-like motion//////////////////////////not sure this works anymore
        if (gyro > snapGyroThreshold && pitch <= requiredUpPitch)
        {
            snapOffset = snapAngle;
        }

        snapOffset = Mathf.Lerp(snapOffset, 0f, snapReturnSpeed * Time.deltaTime);
        targetPitch += snapOffset;

        Quaternion targetRotation = Quaternion.Euler(-targetPitch, 0f, 0f);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );

        float moveScale = 1f;
        if (Input.GetKey(lockKey))
            moveScale = lockedMoveMultiplier;

        if (Mathf.Abs(roll) > rollDeadzone)
        {
            targetX += roll * moveMultiplier * moveScale * Time.deltaTime;
        }

        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, targetX, moveSmoothSpeed * Time.deltaTime);
        transform.position = pos;
    }
}