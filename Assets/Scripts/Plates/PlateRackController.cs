using System.Collections.Generic;
using UnityEngine;

public class PlateRackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private WorkdayManager workdayManager;

    [SerializeField]
    private Plate plateTemplate;

    [SerializeField]
    private Transform plateParent;

    [Header("Layout")]
    [SerializeField]
    private List<Transform> plateSlots = new();

    [SerializeField]
    private Vector3 fallbackSlotOffset = new(1.5f, 0f, 0f);

    [Header("Behavior")]
    [SerializeField]
    private bool clearOnDayEnded = true;

    [SerializeField]
    private bool logEvents;

    private readonly Dictionary<int, Plate> platesByOrderId = new();

    private void Awake()
    {
        if (workdayManager == null)
        {
            workdayManager = FindObjectOfType<WorkdayManager>();
        }

        if (plateParent == null)
        {
            plateParent = transform;
        }

        if (workdayManager == null)
        {
            Debug.LogError("PlateRackController: Missing WorkdayManager reference on " + name);
            return;
        }

        if (plateTemplate == null)
        {
            Debug.LogError("PlateRackController: Missing Plate template reference on " + name);
        }
    }

    private void OnEnable()
    {
        RegisterCallbacks();
        SyncFromWorkdayState();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void RegisterCallbacks()
    {
        if (workdayManager == null)
        {
            return;
        }

        workdayManager.OnOrderCreated += HandleOrderCreated;
        workdayManager.OnOrderServed += HandleOrderServed;
        workdayManager.OnOrderExpired += HandleOrderExpired;
        workdayManager.OnDayEnded += HandleDayEnded;
    }

    private void UnregisterCallbacks()
    {
        if (workdayManager == null)
        {
            return;
        }

        workdayManager.OnOrderCreated -= HandleOrderCreated;
        workdayManager.OnOrderServed -= HandleOrderServed;
        workdayManager.OnOrderExpired -= HandleOrderExpired;
        workdayManager.OnDayEnded -= HandleDayEnded;
    }

    private void HandleOrderCreated(GuestOrder order)
    {
        SpawnPlateForOrder(order);
        ReindexPlatesToQueue();
    }

    private void HandleOrderServed(GuestOrder order, GuestRatingResult _)
    {
        RemovePlate(order.orderId);
        ReindexPlatesToQueue();
    }

    private void HandleOrderExpired(GuestOrder order, GuestRatingResult _)
    {
        RemovePlate(order.orderId);
        ReindexPlatesToQueue();
    }

    private void HandleDayEnded(WorkdaySummary _)
    {
        if (!clearOnDayEnded)
        {
            return;
        }

        ClearAllPlates();
    }

    private void SyncFromWorkdayState()
    {
        if (workdayManager == null)
        {
            return;
        }

        ClearAllPlates();

        IReadOnlyList<GuestOrder> activeOrders = workdayManager.ActiveOrders;
        for (int i = 0; i < activeOrders.Count; i++)
        {
            SpawnPlateForOrder(activeOrders[i]);
        }

        ReindexPlatesToQueue();
    }

    private void SpawnPlateForOrder(GuestOrder order)
    {
        if (order == null || plateTemplate == null || workdayManager == null)
        {
            return;
        }

        if (platesByOrderId.ContainsKey(order.orderId))
        {
            return;
        }

        int queueIndex = ResolveQueueIndex(order.orderId);

        Pose spawnPose = ResolveSlotPose(queueIndex);
        GameObject instance = Instantiate(
            plateTemplate.gameObject,
            spawnPose.position,
            spawnPose.rotation,
            plateParent);

        if (!instance.TryGetComponent(out Plate plateInstance))
        {
            Destroy(instance);
            Debug.LogError("PlateRackController: Spawned template does not include Plate component.");
            return;
        }

        plateInstance.Initialize(workdayManager, order.orderId);
        platesByOrderId[order.orderId] = plateInstance;

        if (logEvents)
        {
            Debug.Log($"PlateRackController: Spawned plate for order {order.orderId} at slot {queueIndex}.");
        }
    }

    private void RemovePlate(int orderId)
    {
        if (!platesByOrderId.TryGetValue(orderId, out Plate plate))
        {
            return;
        }

        platesByOrderId.Remove(orderId);
        if (plate != null)
        {
            Destroy(plate.gameObject);
        }
    }

    private void ReindexPlatesToQueue()
    {
        if (workdayManager == null)
        {
            return;
        }

        HashSet<int> activeOrderIds = new();
        IReadOnlyList<GuestOrder> activeOrders = workdayManager.ActiveOrders;

        for (int i = 0; i < activeOrders.Count; i++)
        {
            GuestOrder order = activeOrders[i];
            activeOrderIds.Add(order.orderId);

            if (!platesByOrderId.TryGetValue(order.orderId, out Plate plate) || plate == null)
            {
                continue;
            }

            Pose slotPose = ResolveSlotPose(i);
            plate.transform.SetPositionAndRotation(slotPose.position, slotPose.rotation);
        }

        List<int> staleOrderIds = new();
        foreach (KeyValuePair<int, Plate> pair in platesByOrderId)
        {
            if (!activeOrderIds.Contains(pair.Key))
            {
                staleOrderIds.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleOrderIds.Count; i++)
        {
            RemovePlate(staleOrderIds[i]);
        }
    }

    private int ResolveQueueIndex(int orderId)
    {
        if (workdayManager == null)
        {
            return 0;
        }

        if (workdayManager.TryGetOrderIndexById(orderId, out int queueIndex))
        {
            return queueIndex;
        }

        return Mathf.Max(0, platesByOrderId.Count);
    }

    private Pose ResolveSlotPose(int queueIndex)
    {
        if (plateSlots != null && queueIndex >= 0 && queueIndex < plateSlots.Count && plateSlots[queueIndex] != null)
        {
            Transform slot = plateSlots[queueIndex];
            return new Pose(slot.position, slot.rotation);
        }

        Transform baseTransform = plateParent != null ? plateParent : transform;
        Vector3 position = baseTransform.position + (fallbackSlotOffset * Mathf.Max(0, queueIndex));
        Quaternion rotation = baseTransform.rotation;
        return new Pose(position, rotation);
    }

    private void ClearAllPlates()
    {
        foreach (KeyValuePair<int, Plate> pair in platesByOrderId)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        platesByOrderId.Clear();
    }
}
