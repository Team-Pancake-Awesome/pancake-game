using UnityEngine;

public class MaximumLifetime : MonoBehaviour
{
    public float lifetimeSeconds = 5f;
    public bool removeIfOnPancake = false;
    private float ageSeconds = 0f;

    void Update()
    {
        ageSeconds += Time.deltaTime;
        if (ageSeconds < lifetimeSeconds)
        {
            return;
        }

        bool onPancake = IsOnPancake();
        if (onPancake && !removeIfOnPancake)
        {
            return;
        }

        Destroy(gameObject);
    }

    bool IsOnPancake()
    {
        // Attached toppings are parented under the pancake by ToppingController.
        if (GetComponentInParent<PancakeController>() != null)
        {
            return true;
        }

        // ToppingController removes collider when attached; use this as a fallback signal.
        return GetComponent<GravityScript>() == null;
    }
}