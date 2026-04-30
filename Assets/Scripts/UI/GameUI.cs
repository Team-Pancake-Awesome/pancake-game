using UnityEngine;
using TMPro;
using System.Collections;
using OpenCover.Framework.Model;

public class GameUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject ratingPanel;
    public TextMeshProUGUI ratingText;
    public TextMeshProUGUI timeLeft;

    [Header("Settings")]
    public float displayDuration = 3f;

    private Coroutine hideCoroutine;

    private void Start()
    {
        if (ratingPanel != null) ratingPanel.SetActive(false);

        if (WorkdayManager.Instance != null)
        {
            WorkdayManager.Instance.OnOrderServed += ShowGameUI;
        }
    }

    private void OnDestroy()
    {
        if (WorkdayManager.Instance != null)
        {
            WorkdayManager.Instance.OnOrderServed -= ShowGameUI;
        }
    }

    private void Update()
    {
        if (WorkdayManager.Instance != null && timeLeft != null)
        {
            float timeInSeconds = WorkdayManager.Instance.RemainingSeconds;

            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

            timeLeft.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    private void ShowGameUI(GuestOrder order, GuestRatingResult result)
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