using UnityEngine;

public class TriggerZone : MonoBehaviour
{
    public bool isPlayerInside { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            isPlayerInside = false;
        }
    }
}