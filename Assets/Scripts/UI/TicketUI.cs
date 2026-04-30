using UnityEngine;
using TMPro;
using System.Text;

public class TicketUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI orderNumberText;
    public TextMeshProUGUI guestNameText;
    public TextMeshProUGUI donenessText;
    public TextMeshProUGUI toppingsText;
    public TextMeshProUGUI timeLeftText;

    private GuestOrder currentOrder;

    void Update()
    {
        if (currentOrder != null)
        {
            RefreshTicketText();
        }
    }

    public void SetupTicket(GuestOrder order)
    {
        currentOrder = order;
        orderNumberText.text = $"Order #{order.orderId}";
        guestNameText.text = order.guestName;
        donenessText.text = $"Cook: {order.requiredDoneness}";
        timeLeftText.text = $"Time Left: {order.RemainingTime(Time.time):F1}s";

        if (order.requiredToppings == null || order.requiredToppings.Count == 0)
        {
            toppingsText.text = "Plain (No Toppings)";
        } else
        {
            StringBuilder toppingsBuilder = new StringBuilder();

            for (int i = 0; i < order.requiredToppings.Count; i++)
            {
                toppingsBuilder.AppendLine($"- {order.requiredToppings[i]}");
            }
            toppingsText.text = toppingsBuilder.ToString();
        }
    }

    private void RefreshTicketText()
	{
		timeLeftText.text = $"Time Left: {currentOrder.RemainingTime(Time.time):F1}s";
	}

    public void CompleteOrder()
    {
        Destroy(gameObject);
    }
}