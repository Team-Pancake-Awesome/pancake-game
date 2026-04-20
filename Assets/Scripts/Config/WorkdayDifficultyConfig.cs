using UnityEngine;

[CreateAssetMenu(menuName = "Pancake Game/Workday Difficulty Config", fileName = "WorkdayDifficultyConfig")]
public class WorkdayDifficultyConfig : ScriptableObject
{
    [Header("Stage Timing")]
    [Min(0f)]
    public float beginStageSeconds = 0f;

    [Min(30f)]
    public float workdayDurationSeconds = 240f;

    [Min(0f)]
    public float ratingStageSeconds = 3f;

    [Header("Guest Arrival")]
    [Min(0.5f)]
    public float startArrivalInterval = 14f;

    [Min(0.25f)]
    public float endArrivalInterval = 4f;

    [Tooltip("Scales arrival speed over normalized day progress.")]
    public AnimationCurve arrivalCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Order Complexity")]
    [Range(0f, 1f)]
    public float startComplexity = 0.15f;

    [Range(0f, 1f)]
    public float endComplexity = 0.9f;

    public AnimationCurve complexityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Urgency")]
    [Min(10f)]
    public float maxUrgencySeconds = 45f;

    [Min(5f)]
    public float minUrgencySeconds = 18f;

    public AnimationCurve urgencyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Queue")]
    [Min(1)]
    public int startMaxConcurrentOrders = 2;

    [Min(1)]
    public int endMaxConcurrentOrders = 4;

    [Header("Volatility")]
    [Range(0f, 0.5f)]
    public float arrivalJitter = 0.1f;

    [Range(0f, 0.25f)]
    public float complexityJitter = 0.05f;

    [Range(0, 100)]
    public int numPancakesMin = 2;

    [Range(0, 100)]
    public int numPancakesMax = 10;
}
