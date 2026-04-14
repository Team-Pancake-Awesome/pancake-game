using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GuestProfile
{
    public int guestId;
    public string displayName = "Guest";

    [Range(0.5f, 1.5f)]
    public float patienceMultiplier = 1f;

    [Range(0f, 1f)]
    public float donenessStrictness = 0.5f;

    [Range(0.5f, 1.5f)]
    public float generosity = 1f;

    [Min(1)]
    public int preferredToppingCount = 1;

    public List<PancakeToppingType> preferredToppings = new();
}
