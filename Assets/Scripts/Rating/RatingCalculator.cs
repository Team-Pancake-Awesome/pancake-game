using System.Collections.Generic;
using UnityEngine;

public class RatingCalculator
{
    private const float FlipBonusPerFlip = 0.015f;
    private const float MaxFlipBonus = 1f;

    public GuestRatingResult EvaluateServed(GuestOrder order, GuestProfile guest, PancakeStats pancakeStats, float servedTime)
    {
        GuestRatingResult result = CreateBaseResult(order, guest);

        result.donenessScore = EvaluateDoneness(order, pancakeStats, guest);
        result.toppingScore = EvaluateToppings(order, pancakeStats);
        result.timingScore = EvaluateTiming(order, servedTime);
        result.qualityScore = Mathf.Clamp01((pancakeStats != null ? pancakeStats.qualityScore : 0f) / 100f);
        result.flipCount = pancakeStats != null ? pancakeStats.flipCount : 0;

        float baseScore01 = Mathf.Clamp01(
            result.donenessScore * 0.4f +
            result.toppingScore * 0.3f +
            result.timingScore * 0.2f +
            result.qualityScore * 0.1f);

        result.flipBonusScore = EvaluateFlipBonus(result.flipCount, baseScore01);

        result.totalScore01 = Mathf.Clamp01(
            baseScore01 +
            result.flipBonusScore);

        result.stars = ToStars(result.totalScore01);
        result.expired = false;

        return result;
    }

    public GuestRatingResult EvaluateExpired(GuestOrder order, GuestProfile guest)
    {
        GuestRatingResult result = CreateBaseResult(order, guest);
        result.donenessScore = 0f;
        result.toppingScore = 0f;
        result.timingScore = 0f;
        result.qualityScore = 0f;
        result.flipCount = 0;
        result.flipBonusScore = 0f;
        result.totalScore01 = 0f;
        result.stars = 1f;
        result.expired = true;
        return result;
    }

    private static GuestRatingResult CreateBaseResult(GuestOrder order, GuestProfile guest)
    {
        return new GuestRatingResult
        {
            guestId = guest != null ? guest.guestId : (order != null ? order.guestId : -1),
            orderId = order != null ? order.orderId : -1,
            guestName = guest != null ? guest.displayName : (order != null ? order.guestName : "Guest")
        };
    }

    private static float EvaluateDoneness(GuestOrder order, PancakeStats pancakeStats, GuestProfile guest)
    {
        if (order == null || pancakeStats == null)
        {
            return 0f;
        }

        int required = (int)order.requiredDoneness;
        int actual = (int)pancakeStats.Doneness;
        int distance = Mathf.Abs(required - actual);

        float strictness = guest != null ? guest.donenessStrictness : 0.5f;
        float penaltyPerStep = Mathf.Lerp(0.2f, 0.35f, strictness);
        return Mathf.Clamp01(1f - (distance * penaltyPerStep));
    }

    private static float EvaluateToppings(GuestOrder order, PancakeStats pancakeStats)
    {
        if (order == null)
        {
            return 0f;
        }

        if (order.requiredToppings.Count == 0)
        {
            return 1f;
        }

        HashSet<PancakeToppingType> required = new(order.requiredToppings);
        int matched = 0;
        int extras = 0;

        if (pancakeStats != null)
        {
            for (int i = 0; i < pancakeStats.toppings.Count; i++)
            {
                PancakeToppingType toppingType = pancakeStats.toppings[i].type;
                if (required.Contains(toppingType))
                {
                    matched += 1;
                }
                else
                {
                    extras += 1;
                }
            }
        }

        float matchScore = (float)matched / required.Count;
        float extraPenalty = Mathf.Clamp01(extras * 0.15f);
        return Mathf.Clamp01(matchScore - extraPenalty);
    }

    private static float EvaluateTiming(GuestOrder order, float servedTime)
    {
        if (order == null || order.urgencyDuration <= 0.01f)
        {
            return 0f;
        }

        float elapsed = Mathf.Max(0f, servedTime - order.createdAt);
        float normalized = Mathf.Clamp01(elapsed / order.urgencyDuration);
        return 1f - normalized;
    }

    private static float ToStars(float score01)
    {
        float raw = 1f + (Mathf.Clamp01(score01) * 4f);
        return Mathf.Round(raw * 2f) * 0.5f;
    }

    private static float EvaluateFlipBonus(int flipCount, float baseScore01)
    {
        if (flipCount <= 0)
        {
            return 0f;
        }

        float rawBonus = Mathf.Min(MaxFlipBonus, flipCount * FlipBonusPerFlip);
        float performanceMultiplier = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(baseScore01));
        return rawBonus * performanceMultiplier;
    }
}
