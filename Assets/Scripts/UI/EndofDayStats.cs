using UnityEngine;
using TMPro;
using System.Text;
using System.Runtime.Remoting.Messaging;

public class EndofDayStats : MonoBehaviour
{
    [Header("End of Day Elements")]
    public TextMeshProUGUI averageStarRatingText;
    public TextMeshProUGUI totalOrdersText;

    public void Start()
    {
        if (WorkdayManager.Instance != null)
        {
            WorkdayManager.Instance.OnDayEnded += DisplayStats;
        }
    }

    private void OnDestroy()
    {
        WorkdayManager.Instance.OnDayEnded -= DisplayStats;
    }

    private void DisplayStats(WorkdaySummary summary)
    {
        if (averageStarRatingText != null) 
            averageStarRatingText.text = $"Average Rating: {summary.averageStars:F1} Stars";
            
        if (totalOrdersText != null) 
            totalOrdersText.text = $"Total Orders: {summary.totalOrders}";
    }
}