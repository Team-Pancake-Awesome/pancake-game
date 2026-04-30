using System.Collections.Generic;
using UnityEngine;

public class TicketController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private WorkdayManager workdayManager;

    [SerializeField]
    private TicketUI ticketTemplate; // Your TicketUI prefab

    [SerializeField]
    private Transform ticketParent; // The UI Panel with the Layout Group

    [Header("Behavior")]
    [SerializeField]
    private bool clearOnDayEnded = true;

    // We use a dictionary just like in PlateRackController to keep track of active tickets
    private readonly Dictionary<int, TicketUI> ticketsByOrderId = new();

    private void Awake()
    {
        if (workdayManager == null) workdayManager = FindObjectOfType<WorkdayManager>();
        if (ticketParent == null) ticketParent = transform;
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

    // --- EVENT SUBSCRIPTIONS ---
    // This matches the exact structure of your PlateRackController!
    private void RegisterCallbacks()
    {
        if (workdayManager == null) return;

        workdayManager.OnOrderCreated += HandleOrderCreated;
        workdayManager.OnOrderServed += HandleOrderServed;
        workdayManager.OnOrderExpired += HandleOrderExpired;
        workdayManager.OnDayEnded += HandleDayEnded;
    }

    private void UnregisterCallbacks()
    {
        if (workdayManager == null) return;

        workdayManager.OnOrderCreated -= HandleOrderCreated;
        workdayManager.OnOrderServed -= HandleOrderServed;
        workdayManager.OnOrderExpired -= HandleOrderExpired;
        workdayManager.OnDayEnded -= HandleDayEnded;
    }

    // --- EVENT HANDLERS ---

    private void HandleOrderCreated(GuestOrder order)
    {
        SpawnTicketForOrder(order);
    }

    private void HandleOrderServed(GuestOrder order, GuestRatingResult _)
    {
        RemoveTicket(order.orderId);
    }

    private void HandleOrderExpired(GuestOrder order, GuestRatingResult _)
    {
        RemoveTicket(order.orderId);
    }

    private void HandleDayEnded(WorkdaySummary _)
    {
        if (clearOnDayEnded)
        {
            ClearAllTickets();
        }
    }

    // --- TICKET LOGIC ---

    private void SyncFromWorkdayState()
    {
        if (workdayManager == null) return;

        ClearAllTickets();

        IReadOnlyList<GuestOrder> activeOrders = workdayManager.ActiveOrders;
        for (int i = 0; i < activeOrders.Count; i++)
        {
            SpawnTicketForOrder(activeOrders[i]);
        }
    }

    private void SpawnTicketForOrder(GuestOrder order)
    {
        if (order == null || ticketTemplate == null || workdayManager == null) return;

        // Don't spawn a duplicate ticket if it already exists
        if (ticketsByOrderId.ContainsKey(order.orderId)) return;

        // Instantiate the ticket prefab inside the layout container
        TicketUI ticketInstance = Instantiate(ticketTemplate, ticketParent);

        // Setup the ticket visually
        ticketInstance.SetupTicket(order);

        // Add it to our tracking dictionary
        ticketsByOrderId[order.orderId] = ticketInstance;
    }

    private void RemoveTicket(int orderId)
    {
        if (!ticketsByOrderId.TryGetValue(orderId, out TicketUI ticket)) return;

        ticketsByOrderId.Remove(orderId);
        
        if (ticket != null)
        {
            // The Layout Group on the ticketParent will automatically re-space the remaining tickets!
            Destroy(ticket.gameObject); 
        }
    }

    private void ClearAllTickets()
    {
        foreach (KeyValuePair<int, TicketUI> pair in ticketsByOrderId)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }
        ticketsByOrderId.Clear();
    }
}