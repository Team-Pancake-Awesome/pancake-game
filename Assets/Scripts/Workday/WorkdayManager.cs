using System;
using System.Collections.Generic;
using UnityEngine;

public class WorkdayManager : DoNotDestroySingletonManager<WorkdayManager>
{
    #region Inspector

    [Header("References")]
    public WorkdayDifficultyConfig difficultyConfig;
    public PancakeController pancakePrefab;
    public Transform pancakeSpawnPoint;
    public Transform pancakeSpawnParent;
    public List<WorkdayDifficultyConfig> dayDifficultyConfigs = new();
    public bool loopDayConfigs = true;
    public PancakeSpawner spawner;

    [Header("Session Settings")]
    public int daySeed = 20260401;
    public bool autoStartOnPlay = true;

    [Header("Debug")]
    public bool logEvents = true;

    #endregion

    #region Public Properties

    public bool IsRunning => currentStage == WorkdayStage.Work || currentStage == WorkdayStage.LastCall;
    public bool IsLastCall => currentStage == WorkdayStage.LastCall;
    public WorkdayStage CurrentStage => currentStage;
    public int CurrentDayNumber => currentDayIndex + 1;
    public float ElapsedSeconds => IsRunning ? Mathf.Max(0f, Time.time - dayStartTime) : lastElapsedTime;
    public float RemainingSeconds => Mathf.Max(0f, CurrentWorkdayDurationSeconds - ElapsedSeconds);
    public float AverageStars => currentSummary.averageStars;
    public IReadOnlyList<GuestOrder> ActiveOrders => activeOrders;
    public IReadOnlyList<GuestRatingResult> Ratings => currentSummary.ratings;
    public int SelectedOrderIndex => selectedOrderIndex;
    public int CurrentMaxConcurrentOrders => currentDifficulty.maxConcurrentOrders;
    public float CurrentGuestArrivalInterval => currentDifficulty.guestArrivalInterval;
    public float CurrentOrderComplexity01 => currentDifficulty.orderComplexity01;
    public float CurrentWorkdayDurationSeconds => Mathf.Max(30f, currentDayConfig != null ? currentDayConfig.workdayDurationSeconds : 240f);
    public WorkdayDifficultyConfig CurrentDayConfig => currentDayConfig;

    #endregion

    #region Events

    public event Action<GuestOrder> OnOrderCreated;
    public event Action<GuestOrder, GuestRatingResult> OnOrderServed;
    public event Action<GuestOrder, GuestRatingResult> OnOrderExpired;
    public event Action<WorkdaySummary> OnDayEnded;

    #endregion

    #region Runtime State

    private readonly List<GuestOrder> activeOrders = new();
    private readonly Dictionary<int, GuestProfile> guestsById = new();

    private readonly GuestProfileGenerator guestGenerator = new();
    private readonly OrderGenerator orderGenerator = new();
    private readonly DifficultyGenerator difficultyGenerator = new();
    private readonly RatingCalculator ratingCalculator = new();

    private WorkdaySummary currentSummary = new();
    private WorkdayDifficultyState currentDifficulty = new();
    private WorkdayStage currentStage = WorkdayStage.Begin;
    private WorkdayDifficultyConfig queuedDayConfig;
    private WorkdayDifficultyConfig currentDayConfig;

    private System.Random rng;
    private float dayStartTime;
    private float lastElapsedTime;
    private float stageStartTime;
    private float nextArrivalTime;
    private int runSeedBase;
    private int queuedDayIndex = -1;
    private int currentDayIndex = -1;
    private int nextGuestId = 1;
    private int nextOrderId = 1;
    private int selectedOrderIndex;
    private bool loggedMissingPancakePrefab;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (autoStartOnPlay)
        {
            BeginWorkday();
        }
    }

    private void Update()
    {
        float now = Time.time;

        switch (currentStage)
        {
            case WorkdayStage.Begin:
                TickBeginStage(now);
                return;
            case WorkdayStage.Work:
                TickWorkStage(now);
                return;
            case WorkdayStage.LastCall:
                TickLastCallStage(now);
                return;
            case WorkdayStage.Rating:
                TickRatingStage(now);
                return;
        }
    }

    #endregion

    #region Public API

    public void BeginWorkday(int? overrideSeed = null)
    {
        runSeedBase = overrideSeed ?? daySeed;
        currentDayIndex = -1;
        lastElapsedTime = 0f;

        activeOrders.Clear();
        guestsById.Clear();

        MusicManager.Instance.PlayMusicNow(MusicCues.Intro);

        currentSummary = new WorkdaySummary
        {
            dayDurationSeconds = CurrentWorkdayDurationSeconds
        };

        if (logEvents)
        {
            Debug.Log($"Workday started with seed {runSeedBase}.");
        }
        // Start immediately when beginning a run.
        EnterBeginStage(skipBeginDelay: true);
    }

    public void EndWorkday()
    {
        if (!IsRunning)
        {
            return;
        }

        float now = Time.time;
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            ExpireOrder(activeOrders[i], now);
        }

        CompleteCurrentDay(now);
        EnterRatingStage();
    }

    public bool ServeOldestOrder()
    {
        return ServeOrderAtIndex(0);
    }

    public bool ServeSelectedOrder()
    {
        return ServeOrderAtIndex(selectedOrderIndex);
    }

    public bool ServeOrderById(int orderId)
    {
        if (!TryGetOrderIndexById(orderId, out int orderIndex))
        {
            return false;
        }

        return ServeOrderAtIndex(orderIndex);
    }

    public bool ServeOrderById(int orderId, PancakeController servedPancake)
    {
        if (!TryGetOrderIndexById(orderId, out int orderIndex))
        {
            return false;
        }

        return ServeOrderAtIndex(orderIndex, servedPancake);
    }

    public bool TryGetOrderIndexById(int orderId, out int orderIndex)
    {
        for (int i = 0; i < activeOrders.Count; i++)
        {
            if (activeOrders[i].orderId == orderId)
            {
                orderIndex = i;
                return true;
            }
        }

        orderIndex = -1;
        return false;
    }

    public void SelectPreviousOrder()
    {
        MoveSelection(-1);
    }

    public void SelectNextOrder()
    {
        MoveSelection(1);
    }

    public bool ServeOrderAtIndex(int index)
    {
        return ServeOrderAtIndex(index, null);
    }

    private bool ServeOrderAtIndex(int index, PancakeController preferredPancake)
    {
        if (!IsRunning || activeOrders.Count == 0)
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
            servedPancake = ResolveServePancakeCandidate(registry, preferredPancake);
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

        RecordRating(rating);
        ClampSelectedOrderIndex();

        OnOrderServed?.Invoke(order, rating);
        SoundManager.Instance.PlayFromCue(SoundCues.OrderSubmitted, transform.position);

        if (logEvents)
        {
            Debug.Log($"Served {order.guestName}: {rating.stars:F1} stars");
        }

        return true;
    }

    #endregion

    #region Stage Flow

    private void TickBeginStage(float now)
    {
        if (queuedDayConfig == null)
        {
            return;
        }

        float beginDuration = Mathf.Max(0f, queuedDayConfig.beginStageSeconds);
        if (now - stageStartTime < beginDuration)
        {
            return;
        }

        StartQueuedDay(now);
    }

    private void TickWorkStage(float now)
    {
        float elapsed = now - dayStartTime;

        if (elapsed >= CurrentWorkdayDurationSeconds)
        {
            EnterLastCallStage();
            return;
        }

        currentDifficulty = difficultyGenerator.Generate(currentDayConfig, elapsed, CurrentWorkdayDurationSeconds, rng);
        TickArrivals(now);
        TickExpirations(now);
    }

    private void TickLastCallStage(float now)
    {
        TickExpirations(now);

        if (activeOrders.Count == 0)
        {
            CompleteCurrentDay(now);
            EnterRatingStage();
        }
    }

    private void TickRatingStage(float now)
    {
        float ratingDuration = currentDayConfig != null ? Mathf.Max(0f, currentDayConfig.ratingStageSeconds) : 0f;
        if (now - stageStartTime < ratingDuration)
        {
            return;
        }

        EnterBeginStage(skipBeginDelay: false);
    }

    private void EnterBeginStage(bool skipBeginDelay)
    {
        int nextDayIndex = currentDayIndex + 1;
        if (!TryResolveDayConfig(nextDayIndex, out WorkdayDifficultyConfig nextConfig, out int resolvedIndex))
        {
            queuedDayConfig = null;
            queuedDayIndex = -1;
            currentStage = WorkdayStage.Begin;

            if (logEvents)
            {
                Debug.Log("No additional day configs available. Workday loop stopped at BEGIN stage.");
            }

            return;
        }

        queuedDayConfig = nextConfig;
        queuedDayIndex = resolvedIndex;
        currentStage = WorkdayStage.Begin;
        stageStartTime = Time.time;

        spawner.SpawnPancakesWithDelay(queuedDayConfig.numPancakesMin);

        if (skipBeginDelay)
        {
            stageStartTime -= Mathf.Max(0f, queuedDayConfig.beginStageSeconds);
        }
    }

    private void StartQueuedDay(float now)
    {
        currentDayIndex = queuedDayIndex;
        currentDayConfig = queuedDayConfig;
        queuedDayIndex = -1;
        queuedDayConfig = null;

        int seed = runSeedBase + (currentDayIndex * 9973);
        rng = new System.Random(seed);

        currentStage = WorkdayStage.Work;
        stageStartTime = now;
        dayStartTime = now;
        lastElapsedTime = 0f;
        nextArrivalTime = dayStartTime;
        nextGuestId = 1;
        nextOrderId = 1;
        selectedOrderIndex = 0;

        activeOrders.Clear();
        guestsById.Clear();

        currentSummary = new WorkdaySummary
        {
            dayDurationSeconds = CurrentWorkdayDurationSeconds
        };

        currentDifficulty = difficultyGenerator.Generate(currentDayConfig, 0f, CurrentWorkdayDurationSeconds, rng);

        if (logEvents)
        {
            Debug.Log($"Day {CurrentDayNumber} started with seed {seed}.");
        }
    }

    private void EnterLastCallStage()
    {
        if (currentStage == WorkdayStage.LastCall)
        {
            return;
        }

        currentStage = WorkdayStage.LastCall;
        stageStartTime = Time.time;

        if (logEvents)
        {
            Debug.Log($"Day {CurrentDayNumber}: last call started. Finish or fail remaining orders.");
        }
    }

    private void EnterRatingStage()
    {
        currentStage = WorkdayStage.Rating;
        stageStartTime = Time.time;
    }

    private void CompleteCurrentDay(float now)
    {
        lastElapsedTime = Mathf.Min(CurrentWorkdayDurationSeconds, now - dayStartTime);

        currentSummary.Recalculate();
        OnDayEnded?.Invoke(currentSummary);

        if (logEvents)
        {
            Debug.Log($"Day {CurrentDayNumber} ended. Orders: {currentSummary.totalOrders}, Avg Stars: {currentSummary.averageStars:F2}");
        }
    }

    private bool TryResolveDayConfig(int requestedDayIndex, out WorkdayDifficultyConfig config, out int resolvedDayIndex)
    {
        if (dayDifficultyConfigs != null && dayDifficultyConfigs.Count > 0)
        {
            if (loopDayConfigs)
            {
                int safeCount = dayDifficultyConfigs.Count;
                resolvedDayIndex = requestedDayIndex;
                config = dayDifficultyConfigs[requestedDayIndex % safeCount];
                return config != null;
            }

            if (requestedDayIndex >= 0 && requestedDayIndex < dayDifficultyConfigs.Count)
            {
                resolvedDayIndex = requestedDayIndex;
                config = dayDifficultyConfigs[requestedDayIndex];
                return config != null;
            }

            config = null;
            resolvedDayIndex = requestedDayIndex;
            return false;
        }

        resolvedDayIndex = requestedDayIndex;
        config = difficultyConfig;
        return config != null;
    }

    #endregion

    #region Order Processing

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
            currentDayConfig,
            now,
            rng);

        activeOrders.Add(order);
        ClampSelectedOrderIndex();
        OnOrderCreated?.Invoke(order);
        SoundManager.Instance.PlayFromCue(SoundCues.ReceiveOrder, transform.position);

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

        RecordRating(rating);
        ClampSelectedOrderIndex();

        OnOrderExpired?.Invoke(order, rating);
        SoundManager.Instance.PlayFromCue(SoundCues.FailedOrder, transform.position);

        if (logEvents)
        {
            Debug.Log($"Expired order from {order.guestName} at t={now:F1}s");
        }
    }

    #endregion

    #region Selection Helpers

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

    #endregion

    #region Rating & Pancake Helpers

    private void RecordRating(GuestRatingResult rating)
    {
        currentSummary.ratings.Add(rating);
        currentSummary.Recalculate();
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

        bool spawnedViaSpawner = false;
        if (spawner != null &&
            currentDayConfig != null &&
            PancakeRegistry.TryGetInstance(out PancakeRegistry registryForCount) &&
            registryForCount.Pancakes.Count < currentDayConfig.numPancakesMax)
        {
            spawner.SpawnPancake();
            spawnedViaSpawner = true;
        }

        if (!spawnedViaSpawner)
        {
            GameObject spawnTemplate = pancakePrefab != null
                ? pancakePrefab.gameObject
                : servedPancake.gameObject;

            if (spawnTemplate != null)
            {
                Transform spawnTransform = pancakeSpawnPoint != null ? pancakeSpawnPoint : sourceTransform;
                Transform spawnParent = pancakeSpawnParent != null ? pancakeSpawnParent : sourceTransform.parent;

                // TODO: Replace this instantiate path with object pooling.
                Instantiate(
                    spawnTemplate,
                    spawnTransform.position,
                    spawnTransform.rotation,
                    spawnParent);

                SoundManager.Instance.PlayFromCue(SoundCues.PourBatter, spawnTransform.position);
            }
            else if (logEvents && !loggedMissingPancakePrefab)
            {
                loggedMissingPancakePrefab = true;
                Debug.LogWarning("WorkdayManager has no usable pancake prefab/template. Served pancake removed without replacement.");
            }
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

    private static PancakeController ResolveServePancakeCandidate(PancakeRegistry registry, PancakeController preferredPancake)
    {
        if (registry == null)
        {
            return null;
        }

        if (preferredPancake != null)
        {
            IReadOnlyList<PancakeController> pancakes = registry.Pancakes;
            for (int i = 0; i < pancakes.Count; i++)
            {
                if (pancakes[i] == preferredPancake)
                {
                    return preferredPancake;
                }
            }
        }

        return FindServePancakeCandidate(registry);
    }

    private static string FormatToppings(GuestOrder order)
    {
        if (order == null || order.requiredToppings == null || order.requiredToppings.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", order.requiredToppings);
    }

    #endregion
}
