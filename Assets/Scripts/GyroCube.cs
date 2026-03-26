using UnityEngine;

public class GyroCube : MonoBehaviour
{
    public ArduinoReader ArduinoReader;

    [Header("Pitch Rotation")]
    public float rotationMultiplier = 1.5f;
    public float rotationSmoothSpeed = 8f;

    [Header("Roll Movement")]
    public float moveMultiplier = 0.02f;
    public float moveSmoothSpeed = 8f;
    public float rollDeadzone = 2f;  // not really calibrated

    private float targetX;

    void Start()
    {
        targetX = transform.position.x;
    }

    void Update()
    {
        if (ArduinoReader == null)
            return;

        // pitch rotates forward/back
        float targetPitch = ArduinoReader.pitch * rotationMultiplier;
        Quaternion targetRotation = Quaternion.Euler(targetPitch, 0f, 0f);

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );

        // roll moves left/right
        if (Mathf.Abs(ArduinoReader.roll) > rollDeadzone)
        {
            targetX -= ArduinoReader.roll * moveMultiplier * Time.deltaTime;
        }

        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, targetX, moveSmoothSpeed * Time.deltaTime);
        transform.position = pos;
    }
}