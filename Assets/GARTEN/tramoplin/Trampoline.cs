using UnityEngine;

public class Trampoline : MonoBehaviour
{
    [Header("Trampolin Einstellungen")]
    public float bounceForce = 18f;

    [Header("Sound (Optional)")]
    public AudioSource bounceSound;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Prüfen, ob eine Hand oder der Spieler-Collider das Trampolin berührt
        if (other.CompareTag("HandTag") || other.CompareTag("Player") || other.GetComponentInParent<Rigidbody>() != null)
        {
            Rigidbody rb = other.GetComponentInParent<Rigidbody>();

            if (rb != null)
            {
                // 2. Aktuelle Y-Geschwindigkeit zurücksetzen, damit der Sprung immer gleich hoch ist
                Vector3 currentVel = rb.linearVelocity;
                currentVel.y = 0f;
                rb.linearVelocity = currentVel;

                // 3. Schub nach oben geben (VelocityChange ignoriert Masse)
                rb.AddForce(Vector3.up * bounceForce, ForceMode.VelocityChange);

                // 4. Sound abspielen
                if (bounceSound != null)
                {
                    bounceSound.Play();
                }
            }
        }
    }
}