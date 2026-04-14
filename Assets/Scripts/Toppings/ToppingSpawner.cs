using UnityEngine;

[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class ToppingSpawner : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject toppingPrefab;
    public Transform spawnPoint;
    public Vector3 spawnOffset = new(0f, 0.1f, 0f);
    public bool useSpawnPointRotation = true;

    [Header("Trigger")]
    public string spatulaTag = "Spatula";
    [Min(0f)]
    public float spawnCooldown = 0.15f;
    public bool useStayChecks = false;

    [Header("Physics")]
    public bool forceKinematicRigidbody = true;

    private float lastSpawnTime = -999f;

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        if (forceKinematicRigidbody)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        TrySpawnFromCollider(collision?.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TrySpawnFromCollider(other);
    }

    void OnCollisionStay(Collision collision)
    {
        if (!useStayChecks)
        {
            return;
        }

        TrySpawnFromCollider(collision?.collider);
    }

    void OnTriggerStay(Collider other)
    {
        if (!useStayChecks)
        {
            return;
        }

        TrySpawnFromCollider(other);
    }

    void TrySpawnFromCollider(Collider other)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (other == null || !IsSpatula(other))
        {
            return;
        }

        if (Time.time - lastSpawnTime < spawnCooldown)
        {
            return;
        }

        if (toppingPrefab == null)
        {
            Debug.LogWarning($"{nameof(ToppingSpawner)} on {name} has no topping prefab assigned.");
            return;
        }

        Vector3 position = GetSpawnPosition();
        Quaternion rotation = GetSpawnRotation();

        Instantiate(toppingPrefab, position, rotation);
        lastSpawnTime = Time.time;
    }

    bool IsSpatula(Collider other)
    {
        if (other.CompareTag(spatulaTag))
        {
            return true;
        }

        Transform root = other.transform.root;
        return root != null && root.CompareTag(spatulaTag);
    }

    Vector3 GetSpawnPosition()
    {
        if (spawnPoint != null)
        {
            return spawnPoint.position;
        }

        return transform.TransformPoint(spawnOffset);
    }

    Quaternion GetSpawnRotation()
    {
        if (!useSpawnPointRotation)
        {
            return Quaternion.identity;
        }

        if (spawnPoint != null)
        {
            return spawnPoint.rotation;
        }

        return transform.rotation;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 position = GetSpawnPosition();

        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(position, 0.06f);
        Gizmos.DrawLine(transform.position, position);
    }
}