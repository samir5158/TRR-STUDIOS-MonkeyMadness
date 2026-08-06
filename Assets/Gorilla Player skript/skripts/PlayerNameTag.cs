using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class PlayerNameTag : MonoBehaviourPunCallbacks
{
    [Header("Zuweisung")]
    public TextMeshPro nameTagText;

    [Header("Bewegung & Glitch-Schutz")]
    public float rotationSpeed = 12f; 

    void Start()
    {
        // 1. Wenn ich der Besitzer dieses Spielers bin, lade meinen gespeicherten Namen
        if (photonView.IsMine)
        {
            // Schaut in den PlayerPrefs nach "SavedPlayerName". Wenn leer, dann "GORILLA"
            string savedName = PlayerPrefs.GetString("SavedPlayerName", "GORILLA");
            
            // Setzt den Namen global für Photon
            PhotonNetwork.NickName = savedName;
        }

        // 2. Anzeige für alle aktualisieren
        UpdateNameDisplay();
    }

    void Update()
    {
        // Billboarding: Name schaut zur Kamera
        if (Camera.main != null)
        {
            Vector3 direction = Camera.main.transform.position - transform.position;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }

    // Wird aufgerufen, wenn du am Computer den Namen änderst (Enter drückst)
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (targetPlayer == photonView.Owner)
        {
            // Wenn wir der Besitzer sind, speichern wir den neuen Namen permanent ab
            if (targetPlayer.IsLocal)
            {
                PlayerPrefs.SetString("SavedPlayerName", targetPlayer.NickName);
                PlayerPrefs.Save(); // Schreibt es sicher auf die Festplatte
            }

            UpdateNameDisplay();
        }
    }

    void UpdateNameDisplay()
    {
        if (nameTagText != null && photonView.Owner != null)
        {
            string currentName = photonView.Owner.NickName;
            nameTagText.text = string.IsNullOrEmpty(currentName) ? "GORILLA" : currentName;
        }
    }
}