using UnityEngine;
using TMPro;
using System.Collections;

public class LiveRatingUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject ratingPanel;
    public TextMeshProUGUI ratingText;

    [Header("Settings")]
    public float displayDuration = 3f;

    private Coroutine hideCoroutine;

    private void Start()
    {
        if (ratingPanel != null) ratingPanel.SetActive(false);

        if (WorkdayManager.Instance != null)
        {
            WorkdayManager.Instance.OnOrderServed += ShowRecentRating;
        }
    }

    private void OnDestroy()
    {
        if (WorkdayManager.Instance != null)
        {
            WorkdayManager.Instance.OnOrderServed -= ShowRecentRating;
        }
    }

    private void ShowRecentRating(GuestOrder order, GuestRatingResult result)
    {
        if (ratingPanel == null || ratingText == null) return;

        ratingPanel.SetActive(true);
        ratingText.text = $"{order.guestName} gave you \n{result.stars:F1} Stars!";

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        if (ratingPanel != null) ratingPanel.SetActive(false);
    }
}