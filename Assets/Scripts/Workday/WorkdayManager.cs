using System;
using System.Collections.Generic;
using UnityEngine;

public class WorkdayManager : MonoBehaviour
{
    [Header("References")]
    public WorkdayDifficultyConfig difficultyConfig;
    public PancakeController pancakePrefab;
    public Transform pancakeSpawnPoint;
    public Transform pancakeSpawnParent;

    [Header("Day Settings")]
    [Min(30f)]
    public float workdayDurationSeconds = 240f;
    public int daySeed = 20260401;
    public bool autoStartOnPlay = true;

    [Header("Controls")]
    public KeyCode serveCurrentOrderKey = KeyCode.Space; // TODO should be in ISpatulaInput
    public KeyCode selectPreviousOrderKey = KeyCode.UpArrow;
    public KeyCode selectNextOrderKey = KeyCode.DownArrow;

    [Header("Debug UI")]
    [Min(0.5f)]
    public float recentRatingDisplaySeconds = 2f;

    [Header("Debug")]
    public bool logEvents = true;

    public bool IsRunning => isRunning;
    public float ElapsedSeconds => isRunning ? Mathf.Max(0f, Time.time - dayStartTime) : lastElapsedTime;
    public float RemainingSeconds => Mathf.Max(0f, workdayDurationSeconds - ElapsedSeconds);
    public float AverageStars => currentSummary.averageStars;
    public IReadOnlyList<GuestOrder> ActiveOrders => activeOrders;
    public IReadOnlyList<GuestRatingResult> Ratings => currentSummary.ratings;

    public event Action<GuestOrder> OnOrderCreated;
    public event Action<GuestOrder, GuestRatingResult> OnOrderServed;
    public event Action<GuestOrder, GuestRatingResult> OnOrderExpired;
    public event Action<WorkdaySummary> OnDayEnded;

    private readonly List<GuestOrder> activeOrders = new();
    private readonly Dictionary<int, GuestProfile> guestsById = new();

    private readonly GuestProfileGenerator guestGenerator = new();
    private readonly OrderGenerator orderGenerator = new();
    private readonly DifficultyGenerator difficultyGenerator = new();
    private readonly RatingCalculator ratingCalculator = new();

    private WorkdaySummary currentSummary = new();
    private WorkdayDifficultyState currentDifficulty = new();

    private System.Random rng;
    private bool isRunning;
    private float dayStartTime;
    private float lastElapsedTime;
    private float nextArrivalTime;
    private int nextGuestId = 1;
    private int nextOrderId = 1;
    private int selectedOrderIndex;
    private bool loggedMissingPancakePrefab;

    private readonly List<RecentRatingLine> recentRatings = new();

    private struct RecentRatingLine
    {
        public string text;
        public float hideAtTime;
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            BeginWorkday();
        }
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        float now = Time.time;
        float elapsed = now - dayStartTime;

        currentDifficulty = difficultyGenerator.Generate(difficultyConfig, elapsed, workdayDurationSeconds, rng);

        TickArrivals(now);
        TickExpirations(now);
        CleanupRecentRatings(now);

        if (Input.GetKeyDown(selectPreviousOrderKey))
        {
            MoveSelection(-1);
        }

        if (Input.GetKeyDown(selectNextOrderKey))
        {
            MoveSelection(1);
        }

        if (Input.GetKeyDown(serveCurrentOrderKey))
        {
            ServeSelectedOrder();
        }

        if (elapsed >= workdayDurationSeconds)
        {
            EndWorkday();
        }
    }

    public void BeginWorkday(int? overrideSeed = null)
    {
        int seed = overrideSeed ?? daySeed;

        rng = new System.Random(seed);
        isRunning = true;
        dayStartTime = Time.time;
        lastElapsedTime = 0f;
        nextArrivalTime = dayStartTime;
        nextGuestId = 1;
        nextOrderId = 1;
        selectedOrderIndex = 0;

        activeOrders.Clear();
        guestsById.Clear();
        recentRatings.Clear();

        MusicManager.Instance.PlayMusicNow(MusicCues.Normal);

        currentSummary = new WorkdaySummary
        {
            dayDurationSeconds = workdayDurationSeconds
        };

        if (logEvents)
        {
            Debug.Log($"Workday started with seed {seed}.");
        }
    }

    public void EndWorkday()
    {
        if (!isRunning)
        {
            return;
        }

        float now = Time.time;
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            ExpireOrder(activeOrders[i], now);
        }

        isRunning = false;
        lastElapsedTime = Mathf.Min(workdayDurationSeconds, now - dayStartTime);

        currentSummary.Recalculate();
        OnDayEnded?.Invoke(currentSummary);

        if (logEvents)
        {
            Debug.Log($"Workday ended. Orders: {currentSummary.totalOrders}, Avg Stars: {currentSummary.averageStars:F2}");
        }
    }

    public bool ServeOldestOrder()
    {
        return ServeOrderAtIndex(0);
    }

    public bool ServeSelectedOrder()
    {
        return ServeOrderAtIndex(selectedOrderIndex);
    }

    private bool ServeOrderAtIndex(int index)
    {
        if (!isRunning || activeOrders.Count == 0)
        {
            return false;
        }

        int safeIndex = Mathf.Clamp(index, 0, activeOrders.Count - 1);
        GuestOrder order = activeOrders[safeIndex];
        activeOrders.RemoveAt(safeIndex);
        order.state = GuestOrderState.Served;

        guestsById.TryGetValue(order.guestId, out GuestProfile guest);

        PancakeController servedPancake = null;
        PancakeStats pancakeStats = null;
        if (PancakeRegistry.TryGetInstance(out PancakeRegistry registry))
        {
            servedPancake = FindServePancakeCandidate(registry);
            if (servedPancake != null)
            {
                pancakeStats = servedPancake.stats;
            }
        }

        GuestRatingResult rating = ratingCalculator.EvaluateServed(order, guest, pancakeStats, Time.time);

        if (servedPancake != null)
        {
            RespawnServedPancake(servedPancake);
        }

        currentSummary.ratings.Add(rating);
        currentSummary.Recalculate();
        string bonusText = rating.flipBonusScore > 0f
            ? $" (+{rating.flipBonusScore:F2} flip bonus, {rating.flipCount} flips)"
            : "";
        AddRecentRating($"{order.guestName}: {rating.stars:F1} stars{bonusText}", Time.time);
        ClampSelectedOrderIndex();

        OnOrderServed?.Invoke(order, rating);

        if (logEvents)
        {
            Debug.Log($"Served {order.guestName}: {rating.stars:F1} stars");
        }

        return true;
    }

    private void TickArrivals(float now)
    {
        if (now < nextArrivalTime)
        {
            return;
        }

        if (activeOrders.Count >= currentDifficulty.maxConcurrentOrders)
        {
            nextArrivalTime = now + 0.25f;
            return;
        }

        SpawnOrder(now);

        float interval = Mathf.Max(0.25f, currentDifficulty.guestArrivalInterval);
        nextArrivalTime = now + interval;
    }

    private void SpawnOrder(float now)
    {
        GuestProfile guest = guestGenerator.GenerateGuest(nextGuestId++, currentDifficulty, rng);
        guestsById[guest.guestId] = guest;

        GuestOrder order = orderGenerator.GenerateOrder(
            nextOrderId++,
            guest,
            currentDifficulty,
            difficultyConfig,
            now,
            rng);

        activeOrders.Add(order);
        ClampSelectedOrderIndex();
        OnOrderCreated?.Invoke(order);

        if (logEvents)
        {
            Debug.Log($"New order: {order.guestName} wants {order.requiredDoneness} + {FormatToppings(order)} in {order.urgencyDuration:F1}s");
        }
    }

    private void TickExpirations(float now)
    {
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            GuestOrder order = activeOrders[i];
            if (!order.IsExpired(now))
            {
                continue;
            }

            ExpireOrder(order, now);
        }
    }

    private void ExpireOrder(GuestOrder order, float now)
    {
        activeOrders.Remove(order);
        order.state = GuestOrderState.Expired;

        guestsById.TryGetValue(order.guestId, out GuestProfile guest);
        GuestRatingResult rating = ratingCalculator.EvaluateExpired(order, guest);

        currentSummary.ratings.Add(rating);
        currentSummary.Recalculate();
        AddRecentRating($"{order.guestName}: expired", now);
        ClampSelectedOrderIndex();

        OnOrderExpired?.Invoke(order, rating);

        if (logEvents)
        {
            Debug.Log($"Expired order from {order.guestName} at t={now:F1}s");
        }
    }

    private void OnGUI()
    {
        Rect panel = new(10f, 10f, 520f, 340f);
        GUILayout.BeginArea(panel, GUI.skin.box);

        GUILayout.Label($"Workday Running: {isRunning}");
        GUILayout.Label($"Time: {ElapsedSeconds:F1}s / {workdayDurationSeconds:F1}s");
        GUILayout.Label($"Orders Active: {activeOrders.Count} (max {currentDifficulty.maxConcurrentOrders})");
        GUILayout.Label($"Arrival Interval: {currentDifficulty.guestArrivalInterval:F2}s");
        GUILayout.Label($"Complexity: {currentDifficulty.orderComplexity01:F2}");
        GUILayout.Label($"Average Stars: {currentSummary.averageStars:F2}");
        GUILayout.Label($"Controls: [{selectPreviousOrderKey}] up, [{selectNextOrderKey}] down, [{serveCurrentOrderKey}] serve");

        if (activeOrders.Count > 0)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Order Queue:");

            int maxShown = Mathf.Min(activeOrders.Count, 8);
            for (int i = 0; i < maxShown; i++)
            {
                GuestOrder order = activeOrders[i];
                string selector = i == selectedOrderIndex ? ">" : " ";
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

    private void MoveSelection(int delta)
    {
        if (activeOrders.Count == 0)
        {
            selectedOrderIndex = 0;
            return;
        }

        int next = selectedOrderIndex + delta;
        if (next < 0)
        {
            next = activeOrders.Count - 1;
        }
        else if (next >= activeOrders.Count)
        {
            next = 0;
        }

        selectedOrderIndex = next;
    }

    private void ClampSelectedOrderIndex()
    {
        if (activeOrders.Count == 0)
        {
            selectedOrderIndex = 0;
            return;
        }

        selectedOrderIndex = Mathf.Clamp(selectedOrderIndex, 0, activeOrders.Count - 1);
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

    private void RespawnServedPancake(PancakeController servedPancake)
    {
        if (servedPancake == null)
        {
            return;
        }

        Transform sourceTransform = servedPancake.transform;

        // Ensure we break scoop/hold links before replacement and removal.
        servedPancake.Drop();

        GameObject spawnTemplate = pancakePrefab != null
            ? pancakePrefab.gameObject
            : servedPancake.gameObject;

        if (spawnTemplate != null)
        {
            Transform spawnTransform = pancakeSpawnPoint ?? sourceTransform;
            Transform spawnParent = pancakeSpawnParent ?? sourceTransform.parent;

            // TODO: Replace this instantiate path with object pooling.
            Instantiate(
                spawnTemplate,
                spawnTransform.position,
                spawnTransform.rotation,
                spawnParent);
        }
        else if (logEvents && !loggedMissingPancakePrefab)
        {
            loggedMissingPancakePrefab = true;
            Debug.LogWarning("WorkdayManager has no usable pancake prefab/template. Served pancake removed without replacement.");
        }

        if (PancakeRegistry.TryGetInstance(out PancakeRegistry registry))
        {
            registry.Unregister(servedPancake);
        }

        // Hide immediately so it is visually gone this frame.
        servedPancake.gameObject.SetActive(false);
        Destroy(servedPancake.gameObject);
    }

    private static PancakeController FindServePancakeCandidate(PancakeRegistry registry)
    {
        if (registry == null)
        {
            return null;
        }

        IReadOnlyList<PancakeController> pancakes = registry.Pancakes;
        for (int i = pancakes.Count - 1; i >= 0; i--)
        {
            PancakeController pancake = pancakes[i];
            if (pancake != null && pancake.IsScooped)
            {
                return pancake;
            }
        }

        registry.TryGetPancake(out PancakeController fallback);
        return fallback;
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
