using System;
using System.Collections.Generic;
using UnityEngine;

public class GuestProfileGenerator
{
    private static readonly string[] NamePool =
    {
        "Alex", "Sam", "Jordan", "Casey", "Riley", "Taylor", "Morgan", "Jules",
        "Mika", "Avery", "Parker", "Robin", "Cameron", "Sky", "Devin", "Quinn"
    };

    private static readonly PancakeToppingType[] ToppingPool =
    {
        PancakeToppingType.Blueberries,
        PancakeToppingType.ChocolateChips,
        PancakeToppingType.Strawberries,
    };

    public GuestProfile GenerateGuest(int guestId, WorkdayDifficultyState difficulty, System.Random rng)
    {
        float progress = difficulty != null ? difficulty.progress01 : 0f;

        GuestProfile guest = new()
        {
            guestId = guestId,
            displayName = BuildName(rng),
            patienceMultiplier = Mathf.Clamp(RandomRange(rng, 0.75f, 1.25f) - progress * 0.2f, 0.5f, 1.5f),
            donenessStrictness = Mathf.Clamp01(RandomRange(rng, 0.2f, 0.75f) + progress * 0.2f),
            generosity = Mathf.Clamp(RandomRange(rng, 0.85f, 1.2f), 0.5f, 1.5f)
        };

        int preferenceCount = Mathf.Clamp(Mathf.RoundToInt(RandomRange(rng, 1f, 2.5f + progress)), 1, ToppingPool.Length);
        guest.preferredToppingCount = preferenceCount;

        List<PancakeToppingType> shuffled = new(ToppingPool);
        Shuffle(shuffled, rng);
        for (int i = 0; i < preferenceCount; i++)
        {
            guest.preferredToppings.Add(shuffled[i]);
        }

        return guest;
    }

    private static string BuildName(System.Random rng)
    {
        int nameIndex = rng != null ? rng.Next(0, NamePool.Length) : UnityEngine.Random.Range(0, NamePool.Length);
        return NamePool[nameIndex];
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        if (rng == null)
        {
            return UnityEngine.Random.Range(min, max);
        }

        return min + ((float)rng.NextDouble() * (max - min));
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        if (list == null || list.Count < 2)
        {
            return;
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = rng != null ? rng.Next(0, i + 1) : UnityEngine.Random.Range(0, i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
