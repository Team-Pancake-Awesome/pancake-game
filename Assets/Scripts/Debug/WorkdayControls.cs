using UnityEngine;

public class WorkdayControls : MonoBehaviour
{
	[Header("References")]
	public WorkdayManager workdayManager;

	[Header("Controls")]
	public KeyCode serveCurrentOrderKey = KeyCode.Return;
	public KeyCode selectPreviousOrderKey = KeyCode.UpArrow;
	public KeyCode selectNextOrderKey = KeyCode.DownArrow;

	private void Awake()
	{
		if (workdayManager == null)
		{
			workdayManager = GetComponent<WorkdayManager>();
		}
        if (workdayManager == null)
        {
            Debug.LogError("WorkdayControls: No WorkdayManager reference found on " + name);
        }
	}

	private void Update()
	{
		if (workdayManager == null || !workdayManager.IsRunning)
		{
			return;
		}

		if (Input.GetKeyDown(selectPreviousOrderKey))
		{
			workdayManager.SelectPreviousOrder();
		}

		if (Input.GetKeyDown(selectNextOrderKey))
		{
			workdayManager.SelectNextOrder();
		}

		if (Input.GetKeyDown(serveCurrentOrderKey))
		{
			workdayManager.ServeSelectedOrder();
		}
	}
}