using UnityEngine;

public class Plate : MonoBehaviour
{
    private WorkdayManager workdayManager;
    private int orderId = -1;
    private bool isConsumed;

    public int OrderId => orderId;

    public void Initialize(WorkdayManager manager, int targetOrderId)
    {
        workdayManager = manager;
        orderId = targetOrderId;
        isConsumed = false;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isConsumed || workdayManager == null)
        {
            return;
        }

        if (!other.CompareTag("Pancake"))
        {
            return;
        }
        if (!other.TryGetComponent(out PancakeController pancakeController))
        {
            return;
        }
        if (pancakeController.IsScooped)
        {
            return;
        }

        bool served = workdayManager.ServeOrderById(orderId);
        if (!served)
        {
            return;
        }

        pancakeController.ResetPancake();

        isConsumed = true;
        if (TryGetComponent(out Collider plateCollider))
        {
            plateCollider.enabled = false;
        }
    }
}