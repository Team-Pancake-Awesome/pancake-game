using System.Collections;
using UnityEngine;

public class PancakeSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField]
    private GameObject pancakePrefab;

    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private Vector3 spawnOffset = new(0f, 0.1f, 0f);

    [SerializeField]
    private Vector3 spawnVelocity = new(0f, 0f, 0f);

    [SerializeField]
    private bool useSpawnPointRotation = true;

    [Header("Trigger")]
    [SerializeField]
    private string spatulaTag = "Spatula";

    [Min(0f)]
    [SerializeField]
    private float spawnCooldown = 0.15f;

    private float lastSpawnTime = -999f;

    void OnCollisionEnter(Collision collision)
    {
        TrySpawnFromCollider(collision?.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TrySpawnFromCollider(other);
    }

    private void TrySpawnFromCollider(Collider collider)
    {
        if (collider == null || !collider.CompareTag(spatulaTag))
        {
            return;
        }

        if (Time.time - lastSpawnTime < spawnCooldown)
        {
            return;
        }

        SpawnPancake();
        lastSpawnTime = Time.time;
    }

    public void SpawnPancake()
    {
        if (pancakePrefab == null || spawnPoint == null)
        {
            return;
        }

        Vector3 position = spawnPoint.position + spawnOffset;
        Quaternion rotation = useSpawnPointRotation ? spawnPoint.rotation : Quaternion.identity;
        GameObject pancake = Instantiate(pancakePrefab, position, rotation);
        if (pancake.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.velocity = spawnVelocity;
        }
    }

    public void SpawnPancakesWithDelay(int count)
    {
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(SpawnPancakeWithDelay(spawnCooldown));
        }
    }

    private IEnumerator SpawnPancakeWithDelay(float delaySeconds)
    {
        SpawnPancake();
        yield return new WaitForSeconds(delaySeconds);
    }
    public void RespawnPancake(PancakeController pancake)
    {
        if (pancake == null || spawnPoint == null)
        {
            return;
        }

        Vector3 position = spawnPoint.position + spawnOffset;
        Quaternion rotation = useSpawnPointRotation ? spawnPoint.rotation : Quaternion.identity;
        pancake.transform.SetPositionAndRotation(position, rotation);
        if (pancake.TryGetComponent<Rigidbody>(out var rb))
        {            
            rb.velocity = spawnVelocity;
        }
    }
}