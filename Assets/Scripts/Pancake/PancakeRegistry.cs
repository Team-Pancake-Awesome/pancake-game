using System.Collections.Generic;
using UnityEngine;

public class PancakeRegistry : MonoBehaviour
{
    private static PancakeRegistry instance;

    public static PancakeRegistry Instance => GetOrCreateInstance();

    private readonly List<PancakeController> pancakes = new();
    public IReadOnlyList<PancakeController> Pancakes => pancakes;

    private static PancakeRegistry GetOrCreateInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<PancakeRegistry>();
        if (instance != null)
        {
            return instance;
        }

        GameObject registryObject = new("PancakeRegistry");
        instance = registryObject.AddComponent<PancakeRegistry>();
        return instance;
    }

    public static bool TryGetInstance(out PancakeRegistry registry)
    {
        registry = instance;
        if (registry != null)
        {
            return true;
        }

        registry = FindObjectOfType<PancakeRegistry>();
        if (registry != null)
        {
            instance = registry;
            return true;
        }

        return false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void Register(PancakeController pancake)
    {
        if (pancake == null)
        {
            return;
        }

        if (!pancakes.Contains(pancake))
        {
            pancakes.Add(pancake);
        }
    }

    public void Unregister(PancakeController pancake)
    {
        if (pancake == null)
        {
            return;
        }

        pancakes.Remove(pancake);
    }

    public bool TryGetPancakeStats(out PancakeStats stats)
    {
        if (TryGetPancake(out PancakeController pancake))
        {
            stats = pancake.stats;
            return true;
        }

        stats = null;
        return false;
    }

    public bool TryGetPancake(out PancakeController pancake)
    {
        for (int i = pancakes.Count - 1; i >= 0; i--)
        {
            PancakeController candidate = pancakes[i];
            if (candidate != null)
            {
                pancake = candidate;
                return true;
            }

            pancakes.RemoveAt(i);
        }

        pancake = null;
        return false;
    }
}
