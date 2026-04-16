using System.Collections.Generic;
using UnityEngine;

public class FlamePot : MonoBehaviour
{
    public SpatulaController spatulaController;
    public ParticleSystem flame;

    [Header("emission")]
    public float minEmission = 0f;
    public float maxEmission = 120f;

    [Header("particle size")]
    public float minSize = 0.08f;
    public float maxSize = 0.35f;

    [Header("particle speed")]
    public float minSpeed = 0.15f;
    public float maxSpeed = 1.8f;

    [Header("shape")]
    public float minRadius = 0.01f;
    public float maxRadius = 0.05f;

    [Header("off")]
    public float offThreshold = 0.03f;

    [Header("pancake heat")]
    [Min(0.1f)]
    public float heatRadius = 1.25f;
    [Min(0f)]
    public float heatPerSecondAtFullFlame = 0.9f;
    public Vector3 heatCenterOffset = Vector3.zero;

    [Header("pancake color")]
    public Color uncookedColor = new(1f, 0.9f, 0.75f, 1f); // TODO: once proper textures are in, move texture business in pancake and call from radius check
    public Color burntColor = new(0.22f, 0.13f, 0.08f, 1f);

    [Header("stored heat")]
    [Range(0f, 1f)]
    public float currentHeat01 = 0f;
    public float CurrentHeat01 => currentHeat01;


    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private readonly Collider[] heatHits = new Collider[32];
    private MaterialPropertyBlock materialPropertyBlock;

    void Start()
    {
        materialPropertyBlock = new MaterialPropertyBlock();

        if (flame == null)
            flame = GetComponent<ParticleSystem>();

        if (flame != null && !flame.isPlaying)
            flame.Play();
    }

    void Update()
    {
        if (flame == null)
            return;

        float t = currentHeat01;

        var main = flame.main;
        var emission = flame.emission;
        var shape = flame.shape;

        emission.enabled = true;
        shape.enabled = true;
        main.loop = true;

        if (t <= offThreshold)
        {
            emission.rateOverTime = 0f;
            UpdatePancakeVisuals();
            return;
        }

        emission.rateOverTime = Mathf.Lerp(minEmission, maxEmission, t);
        main.startSize = Mathf.Lerp(minSize, maxSize, t);
        main.startSpeed = Mathf.Lerp(minSpeed, maxSpeed, t);
        shape.radius = Mathf.Lerp(minRadius, maxRadius, t);

        ApplyHeatToNearbyPancakes(t);
        UpdatePancakeVisuals();
    }

    void ApplyHeatToNearbyPancakes(float flame01)
    {
        Vector3 heatCenter = GetHeatCenter();
        int hitCount = Physics.OverlapSphereNonAlloc(heatCenter, heatRadius, heatHits);
        if (hitCount <= 0)
        {
            return;
        }

        float heatAmount = heatPerSecondAtFullFlame * flame01;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = heatHits[i];
            heatHits[i] = null;
            if (hit == null)
            {
                continue;
            }

            PancakeController pancake = hit.GetComponentInParent<PancakeController>();
            if (pancake == null)
            {
                continue;
            }

            pancake.ApplyHeat(heatAmount);
        }
    }

    public void AddHeatDelta(float delta)
    {
        currentHeat01 = Mathf.Clamp01(currentHeat01 + delta);
    }


    void UpdatePancakeVisuals()
    {
        if (!PancakeRegistry.TryGetInstance(out PancakeRegistry registry))
        {
            return;
        }

        IReadOnlyList<PancakeController> pancakes = registry.Pancakes;
        for (int i = 0; i < pancakes.Count; i++)
        {
            PancakeController pancake = pancakes[i];
            if (pancake == null)
            {
                continue;
            }

            Renderer meshRenderer = pancake.GetComponentInChildren<Renderer>();
            if (meshRenderer == null)
            {
                continue;
            }

            float cooked01 = Mathf.Clamp01(pancake.AverageCookAmount);
            Color cookedColor = Color.Lerp(uncookedColor, burntColor, cooked01);

            meshRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor(BaseColorId, cookedColor);
            materialPropertyBlock.SetColor(ColorId, cookedColor);
            meshRenderer.SetPropertyBlock(materialPropertyBlock);
        }
    }

    Vector3 GetHeatCenter()
    {
        return transform.TransformPoint(heatCenterOffset);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.75f);
        Gizmos.DrawWireSphere(GetHeatCenter(), Mathf.Max(0.01f, heatRadius));
    }
}