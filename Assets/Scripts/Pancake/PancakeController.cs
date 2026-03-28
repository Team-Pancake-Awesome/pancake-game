using UnityEngine;

public class PancakeController : MonoBehaviour
{
    public FlipGestureDetector flipDetector;

    public Rigidbody rb;

    [Header("Flip")]
    public float baseUpForce = 5f;
    public float strengthMultiplier = 1.5f;
    public float spinMultiplier = 200f;

    bool airborne = false;

    void Update()
    {
        if (!airborne && flipDetector.TryGetFlip(out float strength))
        {
            Launch(strength);
        }
    }

    void Launch(float strength)
    {
        airborne = true;

        float upForce = baseUpForce + strength * strengthMultiplier;

        rb.AddForce(Vector3.up * upForce, ForceMode.Impulse);

        float spin = strength * spinMultiplier;
        rb.AddTorque(Vector3.right * spin);

        Debug.Log("FLIP! strength: " + strength);
    }

    void OnCollisionEnter(Collision col)
    {
        if (airborne)
        {
            airborne = false;
            Debug.Log("LANDED");
        }
    }
}