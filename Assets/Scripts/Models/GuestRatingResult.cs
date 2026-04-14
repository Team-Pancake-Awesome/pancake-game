using System;
using UnityEngine;

[Serializable]
public class GuestRatingResult
{
    public int guestId;
    public int orderId;
    public string guestName = "Guest";

    [Range(1f, 5f)]
    public float stars = 1f;

    [Range(0f, 1f)]
    public float totalScore01;

    [Range(0f, 1f)]
    public float donenessScore;

    [Range(0f, 1f)]
    public float toppingScore;

    [Range(0f, 1f)]
    public float timingScore;

    [Range(0f, 1f)]
    public float qualityScore;

    [Range(0f, 1f)]
    public float flipBonusScore;

    [Min(0)]
    public int flipCount;

    public bool expired;
}
