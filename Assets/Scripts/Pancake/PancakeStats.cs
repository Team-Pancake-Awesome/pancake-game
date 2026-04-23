using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PancakeTopping
{
    public PancakeToppingType type = PancakeToppingType.Blueberries;
    public string customName = string.Empty;

    [Min(0f)]
    public float amount = 1f;

    [Range(0f, 1f)]
    public float coverage = 0.25f;

    [Range(0f, 1f)]
    public float meltAmount = 0f;

    public string DisplayName
    {
        get
        {
            if (type == PancakeToppingType.Custom && !string.IsNullOrWhiteSpace(customName))
            {
                return customName;
            }

            return type.ToString();
        }
    }
}

[Serializable]
public class PancakeStats
{
    [Header("Cook State")]
    [Range(0f, 1f)]
    public float topCookAmount = 0f;

    [Range(0f, 1f)]
    public float bottomCookAmount = 0.15f;

    [Range(0f, 1f)]
    public float moisture = 1f;

    [Header("Gameplay Stats")]
    [Min(0f)]
    public float qualityScore = 100f;

    [Min(0)]
    public int flipCount = 0;

    public bool topSideUp = true;

    [Header("Toppings")]
    public List<PancakeTopping> toppings = new();

    public float AverageCookAmount
    {
        get { return (topCookAmount + bottomCookAmount) * 0.5f; }
    }

    public PancakeDoneness Doneness
    {
        get { return EvaluateDoneness(AverageCookAmount); }
    }

    public void ApplyHeat(float heatIntensity, float deltaTime)
    {
        float appliedHeat = Mathf.Max(0f, heatIntensity) * Mathf.Max(0f, deltaTime);

        if (topSideUp)
        {
            topCookAmount = Mathf.Clamp01(topCookAmount + appliedHeat);
        }
        else
        {
            bottomCookAmount = Mathf.Clamp01(bottomCookAmount + appliedHeat);
        }

        moisture = Mathf.Clamp01(moisture - appliedHeat * 0.35f);

        if (AverageCookAmount > 0.95f)
        {
            qualityScore = Mathf.Max(0f, qualityScore - (appliedHeat * 70f));
        }
    }

    public void RegisterFlip()
    {
        topSideUp = !topSideUp;
        flipCount += 1;
    }

    public PancakeTopping AddTopping(PancakeToppingType type, float amount = 1f, float coverage = 0.25f, string customName = "")
    {
        PancakeTopping existing = FindTopping(type, customName);
        if (existing != null)
        {
            existing.amount += Mathf.Max(0f, amount);
            existing.coverage = Mathf.Clamp01(Mathf.Max(existing.coverage, coverage));
            return existing;
        }

        PancakeTopping topping = new()
        {
            type = type,
            customName = customName,
            amount = Mathf.Max(0f, amount),
            coverage = Mathf.Clamp01(coverage)
        };

        toppings.Add(topping);
        return topping;
    }

    public bool RemoveTopping(PancakeToppingType type, string customName = "")
    {
        PancakeTopping topping = FindTopping(type, customName);
        if (topping == null)
        {
            return false;
        }

        toppings.Remove(topping);
        return true;
    }

    public PancakeTopping FindTopping(PancakeToppingType type, string customName = "")
    {
        for (int i = 0; i < toppings.Count; i++)
        {
            PancakeTopping topping = toppings[i];
            if (topping.type != type)
            {
                continue;
            }

            bool isCustomMatch = type != PancakeToppingType.Custom ||
                                 string.Equals(topping.customName, customName, StringComparison.OrdinalIgnoreCase);

            if (isCustomMatch)
            {
                return topping;
            }
        }

        return null;
    }

    public void ResetForNewRound(bool keepToppings)
    {
        topCookAmount = 0f;
        bottomCookAmount = 0.15f;
        moisture = 1f;
        qualityScore = 100f;
        flipCount = 0;
        topSideUp = true;

        if (!keepToppings)
        {
            toppings.Clear();
        }
    }

    private static PancakeDoneness EvaluateDoneness(float cookAmount)
    {
        if (cookAmount < 0.2f)
        {
            return PancakeDoneness.Raw;
        }

        if (cookAmount < 0.45f)
        {
            return PancakeDoneness.Undercooked;
        }

        if (cookAmount < 0.7f)
        {
            return PancakeDoneness.Golden;
        }

        if (cookAmount < 0.92f)
        {
            return PancakeDoneness.WellDone;
        }

        return PancakeDoneness.Burnt;
    }
}
