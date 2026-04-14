using System;
using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator
{
    private static readonly PancakeDoneness[] DonenessPool =
    {
        PancakeDoneness.Undercooked,
        PancakeDoneness.Golden,
        PancakeDoneness.WellDone,
        PancakeDoneness.Burnt
    };

    public GuestOrder GenerateOrder(
        int orderId,
        GuestProfile guest,
        WorkdayDifficultyState difficulty,
        WorkdayDifficultyConfig config,
        float currentTime,
        System.Random rng)
    {
        GuestOrder order = new()
        {
            orderId = orderId,
            guestId = guest != null ? guest.guestId : -1,
            guestName = guest != null ? guest.displayName : "Guest",
            complexity01 = difficulty != null ? difficulty.orderComplexity01 : 0.2f,
            createdAt = currentTime,
            state = GuestOrderState.Waiting
        };

        float strictness = guest != null ? guest.donenessStrictness : 0.5f;
        float complexity = order.complexity01;

        order.requiredDoneness = PickDoneness(complexity, strictness, rng);

        int toppingCount = DetermineToppingCount(complexity, guest, rng);
        FillRequiredToppings(order.requiredToppings, toppingCount, guest, rng);

        float baseUrgency = ResolveUrgency(config, difficulty);
        float patienceMultiplier = guest != null ? guest.patienceMultiplier : 1f;
        order.urgencyDuration = Mathf.Max(5f, baseUrgency * patienceMultiplier);
        order.expiresAt = order.createdAt + order.urgencyDuration;

        return order;
    }

    private static PancakeDoneness PickDoneness(float complexity, float strictness, System.Random rng)
    {
        float extremeChance = Mathf.Clamp01(complexity * 0.5f + strictness * 0.4f);
        bool pickExtreme = Random01(rng) < extremeChance;

        if (pickExtreme)
        {
            return Random01(rng) < 0.5f ? PancakeDoneness.Undercooked : PancakeDoneness.WellDone;
        }

        int index = rng != null ? rng.Next(0, DonenessPool.Length) : UnityEngine.Random.Range(0, DonenessPool.Length);
        return DonenessPool[index];
    }

    private static int DetermineToppingCount(float complexity, GuestProfile guest, System.Random rng)
    {
        int baseCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 3f, complexity)), 1, 3);
        int preferredCount = guest != null ? Mathf.Max(1, guest.preferredToppingCount) : 1;

        int bonusChance = complexity > 0.75f ? 1 : 0;
        int count = Mathf.Clamp(baseCount + bonusChance, 1, Mathf.Min(4, preferredCount));

        if (Random01(rng) > 0.85f)
        {
            count = Mathf.Max(1, count - 1);
        }

        return count;
    }

    private static void FillRequiredToppings(List<PancakeToppingType> destination, int count, GuestProfile guest, System.Random rng)
    {
        destination.Clear();

        if (guest == null || guest.preferredToppings.Count == 0)
        {
            destination.Add(PancakeToppingType.Butter);
            return;
        }

        List<PancakeToppingType> pool = new(guest.preferredToppings);
        Shuffle(pool, rng);

        int finalCount = Mathf.Min(count, pool.Count);
        for (int i = 0; i < finalCount; i++)
        {
            destination.Add(pool[i]);
        }
    }

    private static float ResolveUrgency(WorkdayDifficultyConfig config, WorkdayDifficultyState difficulty)
    {
        if (config == null || difficulty == null)
        {
            return 30f;
        }

        float urgencySeconds = Mathf.Lerp(config.maxUrgencySeconds, config.minUrgencySeconds, difficulty.progress01);
        return urgencySeconds;
    }

    private static float Random01(System.Random rng)
    {
        return rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = rng != null ? rng.Next(0, i + 1) : UnityEngine.Random.Range(0, i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
