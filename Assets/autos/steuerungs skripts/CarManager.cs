using UnityEngine;
using GorillaLocomotion;

public class CarManager : MonoBehaviour
{
    [Header("Spieler Zuweisung")]
    [Tooltip("Ziehe hier deinen Gorilla Player rein (optional, wird sonst automatisch gesucht)")]
    public GameObject playerGameObject;

    [Header("Referenzen (Werden automatisch gesucht, falls leer)")]
    public PCCubeCar cubeCar;
    public CarEntryTrigger entryTrigger;

    [Header("Überwachungs-Intervall")]
    [Tooltip("Wie oft pro Sekunde Referenzen geprüft werden sollen")]
    public float checkInterval = 0.5f;

    private float timer = 0f;

    void Start()
    {
        FindAndFixEverything();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            FindAndFixEverything();
        }

        // SICHERHEIT: Solange der Spieler im Auto fährt, erzwinge dass er fest am Sitz verankert ist!
        if (cubeCar != null && cubeCar.isDriving && cubeCar.playerGameObject != null && cubeCar.seatPosition != null)
        {
            if (cubeCar.playerGameObject.transform.parent != cubeCar.seatPosition)
            {
                cubeCar.playerGameObject.transform.SetParent(cubeCar.seatPosition);
                cubeCar.playerGameObject.transform.localPosition = Vector3.zero;
                cubeCar.playerGameObject.transform.localRotation = Quaternion.identity;
            }
        }
    }

    void FindAndFixEverything()
    {
        // 1. Auto-Skript sichern
        if (cubeCar == null)
        {
            cubeCar = GetComponent<PCCubeCar>();
        }

        // 2. Trigger-Skript sichern
        if (entryTrigger == null)
        {
            entryTrigger = GetComponent<CarEntryTrigger>();
        }

        if (entryTrigger != null && cubeCar != null)
        {
            if (entryTrigger.carScript == null)
            {
                entryTrigger.carScript = cubeCar;
            }
        }

        // 3. Spieler-Referenz abgleichen
        if (cubeCar != null)
        {
            if (playerGameObject != null)
            {
                cubeCar.playerGameObject = playerGameObject;
            }
            else if (cubeCar.playerGameObject == null)
            {
                Player gorillaPlayer = FindObjectOfType<Player>();
                if (gorillaPlayer != null)
                {
                    cubeCar.playerGameObject = gorillaPlayer.gameObject;
                    playerGameObject = gorillaPlayer.gameObject;
                }
                else
                {
                    GameObject taggedPlayer = GameObject.FindWithTag("Player");
                    if (taggedPlayer != null)
                    {
                        cubeCar.playerGameObject = taggedPlayer;
                        playerGameObject = taggedPlayer;
                    }
                }
            }
            else
            {
                playerGameObject = cubeCar.playerGameObject;
            }
        }
    }
}