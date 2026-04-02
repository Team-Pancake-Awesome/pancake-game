using System;
using UnityEngine;

public class DifficultyGenerator
{
    public WorkdayDifficultyState Generate(
        WorkdayDifficultyConfig config,
        float elapsedDayTime,
        float dayDuration,
        System.Random rng)
    {
        WorkdayDifficultyState state = new();
        if (config == null)
        {
            state.progress01 = 0f;
            state.guestArrivalInterval = 10f;
            state.orderComplexity01 = 0.2f;
            state.urgencyScale = 1f;
            state.maxConcurrentOrders = 2;
            return state;
        }

        float progress = dayDuration > 0.01f ? Mathf.Clamp01(elapsedDayTime / dayDuration) : 1f;

        float arrivalT = Mathf.Clamp01(config.arrivalCurve.Evaluate(progress));
        float complexityT = Mathf.Clamp01(config.complexityCurve.Evaluate(progress));
        float urgencyT = Mathf.Clamp01(config.urgencyCurve.Evaluate(progress));

        float arrival = Mathf.Lerp(config.startArrivalInterval, config.endArrivalInterval, arrivalT);
        float complexity = Mathf.Lerp(config.startComplexity, config.endComplexity, complexityT);

        float jitterUnit = rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
        float signedJitter = (jitterUnit * 2f) - 1f;

        arrival = Mathf.Max(0.25f, arrival * (1f + signedJitter * config.arrivalJitter));
        complexity = Mathf.Clamp01(complexity + (signedJitter * config.complexityJitter));

        int maxOrders = Mathf.RoundToInt(
            Mathf.Lerp(
                config.startMaxConcurrentOrders,
                config.endMaxConcurrentOrders,
                progress));

        float urgency = Mathf.Lerp(config.maxUrgencySeconds, config.minUrgencySeconds, urgencyT);
        float urgencyScale = config.maxUrgencySeconds > 0.01f ? urgency / config.maxUrgencySeconds : 1f;

        state.progress01 = progress;
        state.guestArrivalInterval = arrival;
        state.orderComplexity01 = complexity;
        state.urgencyScale = Mathf.Clamp(urgencyScale, 0.5f, 1.5f);
        state.maxConcurrentOrders = Mathf.Max(1, maxOrders);

        return state;
    }
}
