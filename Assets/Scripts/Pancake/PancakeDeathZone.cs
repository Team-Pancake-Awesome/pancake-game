using UnityEngine;

public class PancakeDeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Pancake"))
        {
            if (other.TryGetComponent<PancakeController>(out var pancake))
            {
                pancake.ResetPancake();
            }
        }
    }
}