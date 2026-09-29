using UnityEngine;
using Photon.Pun;

public class CarHitbox : MonoBehaviour
{
    [Header("Schlag-Stärke")]
    [Tooltip("Kraft nach oben (in die Luft)")]
    public float upwardForce = 18f;

    [Tooltip("Kraft nach vorne/zur Seite (wegschmeißen)")]
    public float pushForce = 22f;

    [Header("Sound (Optional)")]
    public AudioSource crashSound;

    private void OnTriggerEnter(Collider other)
    {
        // Prüfen, ob das getroffene Objekt zu UNSEREM lokalen Spieler gehört
        PhotonView playerPV = other.GetComponentInParent<PhotonView>();

        if (playerPV != null && playerPV.IsMine)
        {
            Rigidbody playerRb = other.GetComponentInParent<Rigidbody>();

            if (playerRb != null)
            {
                // 1. Aktuelle Bewegungsgeschwindigkeit zurücksetzen für maximalen Impact
                Vector3 currentVel = playerRb.linearVelocity;
                currentVel.y = 0f;
                playerRb.linearVelocity = currentVel;

                // 2. Richtung berechnen: Vom Auto weg + nach oben
                Vector3 pushDirection = (playerRb.transform.position - transform.position).normalized;
                pushDirection.y = 0f; // Y-Komponente separat steuern

                // Kombinierte Kraft berechnen
                Vector3 finalForce = (pushDirection * pushForce) + (Vector3.up * upwardForce);

                // 3. Den Spieler mit Impuls wegschleudern (VelocityChange ignoriert Masse)
                playerRb.AddForce(finalForce, ForceMode.VelocityChange);

                // 4. Crash-Sound abspielen
                if (crashSound != null)
                {
                    crashSound.Play();
                }
            }
        }
    }
}