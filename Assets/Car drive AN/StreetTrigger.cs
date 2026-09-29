using System.Collections;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class StreetTriggerNetwork : MonoBehaviourPunCallbacks
{
    [Header("Die beiden Autos")]
    public GameObject car1;
    public GameObject car2;

    [Header("Einstellungen")]
    [Tooltip("Dauer der Fahrt in Sekunden, bevor die Autos an den Start zurückgesetzt werden")]
    public float driveDuration = 3f;

    [Header("Sound (Optional)")]
    public AudioSource hupenOderMotorSound;

    private bool isDriving = false;

    private void Start()
    {
        // Deaktiviert die Animationen zu Beginn und setzt Autos an den Start
        StopAndResetCar(car1);
        StopAndResetCar(car2);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Nur reagieren, wenn die Autos nicht bereits fahren
        if (isDriving) return;

        // Versuchen, die PhotonView des berührenden Spielers zu finden
        PhotonView playerPV = other.GetComponentInParent<PhotonView>();

        // Prüfen, ob die Berührung von UNSEREM lokalen Spieler stammt
        bool isLocalPlayer = false;

        if (playerPV != null)
        {
            isLocalPlayer = playerPV.IsMine;
        }
        else
        {
            // Fallback: Falls die Hand/der Tag als lokaler Player klassifiziert ist
            if (other.CompareTag("HandTag") || other.CompareTag("Player"))
            {
                isLocalPlayer = true;
            }
        }

        // Nur wenn es der lokale Spieler war, senden wir den RPC an den Server
        if (isLocalPlayer)
        {
            photonView.RPC(nameof(RPC_TriggerCars), RpcTarget.AllViaServer);
        }
    }

    [PunRPC]
    private void RPC_TriggerCars()
    {
        if (!isDriving)
        {
            StartCoroutine(StartCarRoutine());
        }
    }

    private IEnumerator StartCarRoutine()
    {
        isDriving = true;

        // Autos an den Anfang setzen und Animation starten
        PlayCarAnimation(car1);
        PlayCarAnimation(car2);

        if (hupenOderMotorSound != null)
        {
            hupenOderMotorSound.Play();
        }

        // Wartet, bis die Autos am Ziel angekommen sind
        yield return new WaitForSeconds(driveDuration);

        // Autos zurück an den Startpunkt setzen und stoppen
        StopAndResetCar(car1);
        StopAndResetCar(car2);

        // Bereit für das nächste Betreten der Straße!
        isDriving = false;
    }

    private void PlayCarAnimation(GameObject car)
    {
        if (car != null)
        {
            Animator anim = car.GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                anim.Play(0, -1, 0f); // Spielt die Animation ab Sekunde 0 neu ab
            }
        }
    }

    private void StopAndResetCar(GameObject car)
    {
        if (car != null)
        {
            Animator anim = car.GetComponent<Animator>();
            if (anim != null)
            {
                anim.Play(0, -1, 0f); // Auf Sekunde 0 setzen
                anim.Update(0f);      // Visuell sofort auf den Startpunkt aktualisieren
                anim.enabled = false;  // Stoppen
            }
        }
    }
}