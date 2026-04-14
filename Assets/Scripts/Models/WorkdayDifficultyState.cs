using System;
using UnityEngine;

[Serializable]
public class WorkdayDifficultyState
{
    [Range(0f, 1f)]
    public float progress01;

    [Min(0.25f)]
    public float guestArrivalInterval;

    [Range(0f, 1f)]
    public float orderComplexity01;

    [Range(0.5f, 1.5f)]
    public float urgencyScale = 1f;

    [Min(1)]
    public int maxConcurrentOrders = 1;
}
