using UnityEngine;

public class BurnerZoneController : MonoBehaviour
{
    public SpatulaController spatulaController;

    [Header("Tuning")]
    public float knobSensitivity = 1.2f;
    public float potDeltaDeadzone = 0.005f;

    private FlamePot activeBurner;
    private float lastPotValue;
    private bool initialized;

    void Update()
    {
        if (spatulaController == null)
            return;

        float currentPot = Mathf.Clamp01(spatulaController.PotValue);

        if (!initialized)
        {
            lastPotValue = currentPot;
            initialized = true;
            return;
        }

        float potDelta = currentPot - lastPotValue;

        if (activeBurner != null && Mathf.Abs(potDelta) >= potDeltaDeadzone)
        {
            Debug.Log($"APPLY {potDelta:0.000} to {activeBurner.name}");
            activeBurner.AddHeatDelta(potDelta * knobSensitivity);
        }

        lastPotValue = currentPot;
    }

    public void SetActiveBurner(FlamePot burner)
    {
        activeBurner = burner;
        if (spatulaController != null)
            lastPotValue = Mathf.Clamp01(spatulaController.PotValue);

        Debug.Log($"ACTIVE burner = {(burner != null ? burner.name : "null")}");
    }

    public void ClearActiveBurner(FlamePot burner)
    {
        if (activeBurner == burner)
        {
            activeBurner = null;
            Debug.Log("ACTIVE burner cleared");
        }
        
    }
}