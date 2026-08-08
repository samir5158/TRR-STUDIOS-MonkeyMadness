using UnityEngine;

[DisallowMultipleComponent] // Verhindert aus Versehen doppeltes Hinzufügen auf demselben Objekt
public class DontDestroyRig : MonoBehaviour
{
    public static DontDestroyRig Instance { get; private set; }

    [Header("Debugging & Logger")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        // 1. Singleton-Schutz gegen doppelte Spieler-Rigs
        if (Instance != null && Instance != this)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[DontDestroyRig] Doppeltes Gorilla Rig auf '{gameObject.name}' erkannt und gelöscht, um Netzwerkkonflikte zu vermeiden.", gameObject);
            }
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 2. Sicherheits-Checks für Tag und Setup
        ValidateRigSetup();

        if (showDebugLogs)
        {
            Debug.Log($"<color=#00FF00>[DontDestroyRig] SUCCESS:</color> '{gameObject.name}' wurde erfolgreich über Szenenwechsel hinweg geschützt!", gameObject);
        }
    }

    /// <summary>
    /// Überprüft, ob das Rig alle nötigen Tags und Komponenten für Photon/VR besitzt.
    /// </summary>
    private void ValidateRigSetup()
    {
        // Tag-Überprüfung
        if (!gameObject.CompareTag("Player"))
        {
            Debug.LogWarning($"[DontDestroyRig] WARNING: Das Objekt '{gameObject.name}' hat nicht den Tag 'Player'! Teleportation und Spawner könnten fehlschlagen.", gameObject);
        }

        // Rigidbody-Überprüfung (sucht auf diesem Objekt UND in Unterobjekten wie GorillaPlayer)
        Rigidbody rb = GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            if (rb.isKinematic)
            {
                Debug.LogWarning($"[DontDestroyRig] NOTICE: Rigidbody auf '{rb.gameObject.name}' ist Kinematic. Gorilla Movement benötigt meist isKinematic = false.", rb.gameObject);
            }
        }
        else
        {
            Debug.LogError($"[DontDestroyRig] ERROR: Kein Rigidbody auf '{gameObject.name}' oder seinen Unterobjekten gefunden! Gorilla Movement benötigt eine Rigidbody-Komponente.", gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}