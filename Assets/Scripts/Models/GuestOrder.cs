using System;
using System.Collections.Generic;
using UnityEngine;

public enum GuestOrderState
{
    Waiting,
    Served,
    Expired
}

[Serializable]
public class GuestOrder
{
    public int orderId;
    public int guestId;
    public string guestName = "Guest";

    public PancakeDoneness requiredDoneness = PancakeDoneness.Golden;
    public List<PancakeToppingType> requiredToppings = new();

    [Min(5f)]
    public float urgencyDuration = 35f;

    [Min(0f)]
    public float createdAt;

    [Min(0f)]
    public float expiresAt;

    [Range(0f, 1f)]
    public float complexity01;

    public GuestOrderState state = GuestOrderState.Waiting;

    public float RemainingTime(float currentTime)
    {
        return Mathf.Max(0f, expiresAt - currentTime);
    }

    public bool IsExpired(float currentTime)
    {
        return state == GuestOrderState.Waiting && currentTime >= expiresAt;
    }
}
