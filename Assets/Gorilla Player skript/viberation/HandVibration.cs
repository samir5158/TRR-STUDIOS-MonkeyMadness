using UnityEngine;
using UnityEngine.XR;

public class HandVibration : MonoBehaviour
{
    [Header("Hand Einstellung")]
    public bool isLeftHand; 

    [Header("Vibrations-Einstellungen")]
    public float intensity = 0.3f;
    public float duration = 0.05f;

    private void OnTriggerEnter(Collider other)
    {
        // Vibriert bei Boden ODER Wand
        if (other.CompareTag("Floor") || other.CompareTag("Wall"))
        {
            TriggerVibration();
        }

        // Spezieller Check: Wenn es eine Wand ist, stoppen wir das Rutschen
        if (other.CompareTag("Wall"))
        {
            StopSliding();
        }
    }

    private void TriggerVibration()
    {
        XRNode node = isLeftHand ? XRNode.LeftHand : XRNode.RightHand;
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        
        if (device.isValid)
        {
            device.SendHapticImpulse(0u, intensity, duration);
        }
    }

    private void StopSliding()
    {
        // Sucht den Rigidbody deines Gorilla-Rigs
        Rigidbody rb = GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            // Setzt die Fallgeschwindigkeit kurz auf 0, damit man "klebt"
            Vector3 v = rb.linearVelocity;
            v.y = 0; 
            rb.linearVelocity = v;
        }
    }
}