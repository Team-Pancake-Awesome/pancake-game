using UnityEngine;

public class BurnerZoneTrigger : MonoBehaviour
{
    public FlamePot flamePot;

    void OnTriggerEnter(Collider other)
    {
        BurnerZoneController controller = other.GetComponentInParent<BurnerZoneController>();
        if (controller != null && flamePot != null)
        {
            Debug.Log($"Entered burner zone: {flamePot.name}");
            controller.SetActiveBurner(flamePot);
        }
    }

    void OnTriggerExit(Collider other)
    {
        BurnerZoneController controller = other.GetComponentInParent<BurnerZoneController>();
        if (controller != null && flamePot != null)
        {
            Debug.Log($"Exited burner zone: {flamePot.name}");
            controller.ClearActiveBurner(flamePot);
        }
    }
}