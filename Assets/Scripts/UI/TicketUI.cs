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

    private GuestOrder currentOrder;

    public void SetupTicket(GuestOrder order)
    {
        currentOrder = order;

        orderNumberText.text = $"Order #{order.orderId}";
        guestNameText.text = order.guestName;
        donenessText.text = $"Cook: {order.requiredDoneness}";

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

    public void CompleteOrder()
    {
        Destroy(gameObject);
    }
}