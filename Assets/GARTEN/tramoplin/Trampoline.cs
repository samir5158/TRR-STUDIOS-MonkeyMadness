using UnityEngine;

public class Trampoline : MonoBehaviour
{
    public float bounceForce = 15f;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            // Setzt Y-Geschwindigkeit und gibt einen Schub nach oben
            Vector3 vel = rb.linearVelocity;
            vel.y = bounceForce;
            rb.linearVelocity = vel;
        }
    }
}