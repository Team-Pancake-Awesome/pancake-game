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

    [Header("Debug")]
    public bool logEvents = true;

    public bool IsRunning => isRunning;
    public bool IsLastCall => isLastCall;
    public float ElapsedSeconds => isRunning ? Mathf.Max(0f, Time.time - dayStartTime) : lastElapsedTime;
    public float RemainingSeconds => Mathf.Max(0f, workdayDurationSeconds - ElapsedSeconds);
    public float AverageStars => currentSummary.averageStars;
    public IReadOnlyList<GuestOrder> ActiveOrders => activeOrders;
    public IReadOnlyList<GuestRatingResult> Ratings => currentSummary.ratings;
    public int SelectedOrderIndex => selectedOrderIndex;
    public int CurrentMaxConcurrentOrders => currentDifficulty.maxConcurrentOrders;
    public float CurrentGuestArrivalInterval => currentDifficulty.guestArrivalInterval;
    public float CurrentOrderComplexity01 => currentDifficulty.orderComplexity01;

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
    private bool isLastCall;
    private float dayStartTime;
    private float lastElapsedTime;
    private float nextArrivalTime;
    private int nextGuestId = 1;
    private int nextOrderId = 1;
    private int selectedOrderIndex;
    private bool loggedMissingPancakePrefab;

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

        if (!isLastCall && elapsed >= workdayDurationSeconds)
        {
            BeginLastCall();
        }

        if (!isLastCall)
        {
            currentDifficulty = difficultyGenerator.Generate(difficultyConfig, elapsed, workdayDurationSeconds, rng);
            TickArrivals(now);
        }

        TickExpirations(now);

        if (isLastCall && activeOrders.Count == 0)
        {
            EndWorkday();
        }
    }

    public void BeginWorkday(int? overrideSeed = null)
    {
        int seed = overrideSeed ?? daySeed;

        rng = new System.Random(seed);
        isRunning = true;
        isLastCall = false;
        dayStartTime = Time.time;
        lastElapsedTime = 0f;
        nextArrivalTime = dayStartTime;
        nextGuestId = 1;
        nextOrderId = 1;
        selectedOrderIndex = 0;

        activeOrders.Clear();
        guestsById.Clear();

        currentSummary = new WorkdaySummary
        {
            dayDurationSeconds = workdayDurationSeconds
        };

        if (logEvents)
        {
            Debug.Log($"Workday started with seed {seed}.");
        }
    }

    private void BeginLastCall()
    {
        if (isLastCall)
        {
            return;
        }

        isLastCall = true;

        if (logEvents)
        {
            Debug.Log("Workday timer ended. Last call started: finish or fail remaining orders.");
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
        isLastCall = false;
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

    public void SelectPreviousOrder()
    {
        MoveSelection(-1);
    }

    public void SelectNextOrder()
    {
        MoveSelection(1);
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
        ClampSelectedOrderIndex();

        OnOrderExpired?.Invoke(order, rating);

        if (logEvents)
        {
            Debug.Log($"Expired order from {order.guestName} at t={now:F1}s");
        }
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
