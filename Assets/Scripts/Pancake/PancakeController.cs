using UnityEngine;

public class PancakeController : MonoBehaviour
{
    public FlipGestureDetector flipDetector;
    public Rigidbody rb;

    [Header("Testing")]
    public Transform spawnPoint;
    public KeyCode resetKey = KeyCode.R;
    public KeyCode testLaunchKey = KeyCode.T;

    private bool airborne = false;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (rb == null)
            return;

        if (Input.GetKeyDown(resetKey))
        {
            ResetPancake();
        }

        if (Input.GetKeyDown(testLaunchKey))
        {
            Debug.Log("MANUAL TEST LAUNCH");
            LaunchFlip(1.5f);
        }

        if (airborne || flipDetector == null)
            return;

        if (flipDetector.TryGetFlip(out float strength))
        {
            Debug.Log("FLIP DETECTOR RETURNED TRUE");
            LaunchFlip(strength);
        }
    }

    void LaunchFlip(float strength)
    {
        airborne = true;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float upForce = 7f + strength * 2f;

        rb.AddForce(Vector3.up * upForce, ForceMode.Impulse);
        rb.AddTorque(Vector3.right * 250f, ForceMode.Impulse);

        Debug.Log("SCRIPT LAUNCH | strength: " + strength + " upForce: " + upForce);
    }

    void ResetPancake()
    {
        airborne = false;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (spawnPoint != null)
            transform.position = spawnPoint.position;

        transform.rotation = Quaternion.identity;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (airborne)
            airborne = false;
    }
}