using UnityEngine;
using System.Collections.Generic;

public class DebugWorkdayUI : MonoBehaviour
{
    [Header("References")]
    public WorkdayManager workdayManager;

    [Header("Display")]
    [Min(0.5f)]
    public float recentRatingDisplaySeconds = 2f;

    private readonly List<RecentRatingLine> recentRatings = new();

    private struct RecentRatingLine
    {
        public string text;
        public float hideAtTime;
    }

    private void Awake()
    {
        if (workdayManager == null)
        {
            workdayManager = GetComponent<WorkdayManager>();
        }
        if (workdayManager == null)
        {
            Debug.LogError("DebugWorkdayUI: No WorkdayManager reference found on " + name);
        }
    }

    private void OnEnable()
    {
        RegisterCallbacks();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void Update()
    {
        float now = Time.time;
        CleanupRecentRatings(now);
    }

    private void OnGUI()
    {
        if (workdayManager == null)
        {
            return;
        }

        Rect panel = new(10f, 10f, 520f, 340f);
        GUILayout.BeginArea(panel, GUI.skin.box);

        GUILayout.Label($"Stage: {workdayManager.CurrentStage} | Day: {workdayManager.CurrentDayNumber}");
        GUILayout.Label($"Workday Running: {workdayManager.IsRunning} {(workdayManager.IsLastCall ? "(last call)" : "")}");
        GUILayout.Label($"Time: {workdayManager.ElapsedSeconds:F1}s / {workdayManager.CurrentWorkdayDurationSeconds:F1}s");
        GUILayout.Label($"Orders Active: {workdayManager.ActiveOrders.Count} (max {workdayManager.CurrentMaxConcurrentOrders})");
        GUILayout.Label($"Arrival Interval: {workdayManager.CurrentGuestArrivalInterval:F2}s");
        GUILayout.Label($"Complexity: {workdayManager.CurrentOrderComplexity01:F2}");
        GUILayout.Label($"Average Stars: {workdayManager.AverageStars:F2}");

        IReadOnlyList<GuestOrder> activeOrders = workdayManager.ActiveOrders;
        if (activeOrders.Count > 0)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Order Queue:");

            int maxShown = Mathf.Min(activeOrders.Count, 8);
            for (int i = 0; i < maxShown; i++)
            {
                GuestOrder order = activeOrders[i];
                string selector = i == workdayManager.SelectedOrderIndex ? ">" : " ";
                GUILayout.Label($"{selector} [{i + 1}] {order.guestName} -> {order.requiredDoneness}, tops {FormatToppings(order)}, left {order.RemainingTime(Time.time):F1}s");
            }

            if (activeOrders.Count > maxShown)
            {
                GUILayout.Label($"... {activeOrders.Count - maxShown} more orders");
            }
        }
        else
        {
            GUILayout.Space(6f);
            GUILayout.Label("Order Queue: (empty)");
        }

        if (recentRatings.Count > 0)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Recent Results:");
            for (int i = 0; i < recentRatings.Count; i++)
            {
                GUILayout.Label($"+ {recentRatings[i].text}");
            }
        }

        GUILayout.EndArea();
    }

    private void RegisterCallbacks()
    {
        if (workdayManager == null)
        {
            return;
        }

        workdayManager.OnOrderServed += HandleOrderServed;
        workdayManager.OnOrderExpired += HandleOrderExpired;
    }

    private void UnregisterCallbacks()
    {
        if (workdayManager == null)
        {
            return;
        }

        workdayManager.OnOrderServed -= HandleOrderServed;
        workdayManager.OnOrderExpired -= HandleOrderExpired;
    }

    private void HandleOrderServed(GuestOrder order, GuestRatingResult rating)
    {
        string bonusText = rating.flipBonusScore > 0f
            ? $" (+{rating.flipBonusScore:F2} flip bonus, {rating.flipCount} flips)"
            : "";
        AddRecentRating($"{order.guestName}: {rating.stars:F1} stars{bonusText}", Time.time);
    }

    private void HandleOrderExpired(GuestOrder order, GuestRatingResult rating)
    {
        AddRecentRating($"{order.guestName}: expired", Time.time);
    }

    private void AddRecentRating(string text, float now)
    {
        recentRatings.Add(new RecentRatingLine
        {
            text = text,
            hideAtTime = now + Mathf.Max(0.5f, recentRatingDisplaySeconds)
        });

        const int maxRecentLines = 4;
        while (recentRatings.Count > maxRecentLines)
        {
            recentRatings.RemoveAt(0);
        }
    }

    private void CleanupRecentRatings(float now)
    {
        for (int i = recentRatings.Count - 1; i >= 0; i--)
        {
            if (now >= recentRatings[i].hideAtTime)
            {
                recentRatings.RemoveAt(i);
            }
        }
    }

    private static string FormatToppings(GuestOrder order)
    {
        if (order == null || order.requiredToppings == null || order.requiredToppings.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", order.requiredToppings);
    }
}