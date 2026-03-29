using UnityEngine;

public class PancakeController : MonoBehaviour
{
    public FlipGestureDetector flipDetector;
    public Rigidbody rb;

    [Header("Spatula Check")]
    [Tooltip("Drag your Spatula GameObject here in the inspector")]
    public Transform spatula; 
    [Tooltip("How close the spatula needs to be to flip the pancake")]
    public float maxFlipDistance = 1.5f; 

    [Header("Testing")]
    public Transform spawnPoint;
    public KeyCode resetKey = KeyCode.R;
    public KeyCode testLaunchKey = KeyCode.T;

    [Header("Flip Physics")]
    public float baseUpForce = 6f;
    public float forceMultiplier = 2.5f;
    
    public float baseTorque = 100f;
    public float torqueMultiplier = 150f;

    private bool airborne = false;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (rb == null) return;

        if (Input.GetKeyDown(resetKey))
        {
            ResetPancake();
        }

        if (Input.GetKeyDown(testLaunchKey))
        {
            LaunchFlip(1.5f);
        }

        if (airborne || flipDetector == null) return;

        if (flipDetector.TryGetFlip(out float strength))
        {
            
            if (spatula == null)
            {
                LaunchFlip(strength);
                return;
            }

            // Calculate the lateral distance (ignoring Y height so it works perfectly even if the pan is slightly below the pancake)
            Vector2 spatulaLateralPos = new Vector2(spatula.position.x, spatula.position.z);
            Vector2 pancakeLateralPos = new Vector2(transform.position.x, transform.position.z);
            float distance = Vector2.Distance(spatulaLateralPos, pancakeLateralPos);

            if (distance <= maxFlipDistance)
            {
                LaunchFlip(strength);
            }
            else
            {
                Debug.Log($"Flip motion detected, but spatula was too far away! Distance: {distance:F2}");
            }
        }
    }

    void LaunchFlip(float strength)
    {
        airborne = true;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float upForce = baseUpForce + (strength * forceMultiplier);
        float appliedTorque = baseTorque + (strength * torqueMultiplier);

        rb.AddForce(Vector3.up * upForce, ForceMode.Impulse);
        rb.AddTorque(Vector3.right * appliedTorque, ForceMode.Impulse);
    }

    public void ResetPancake()
    {
        airborne = false;
        
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
        if (airborne && collision.relativeVelocity.magnitude > 0.1f)
        {
            airborne = false;
        }
    }
}