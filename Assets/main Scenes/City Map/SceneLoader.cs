using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class SceneLoader : MonoBehaviour
{
    [Header("Einstellungen")]
    public string sceneName = "City";

    private static SceneLoader instance;
    private bool isLoading = false;

    private void Awake()
    {
        // Verhindert doppelte Loader-Objekte beim Szenenwechsel
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isLoading) return;

        // Prüft, ob der Collider zum Spieler gehört
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            isLoading = true;

            // Für Multiplayer: Lädt die Szene synchron über Photon
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LoadLevel(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == sceneName)
        {
            // 1. Suche nach dem SpawnPoint
            GameObject spawnPoint = GameObject.FindWithTag("Respawn");
            if (spawnPoint == null)
            {
                spawnPoint = GameObject.Find("CitySpawnPoint");
            }

            // 2. Suche nach dem Gorilla Rig
            GameObject gorillaRig = GameObject.FindWithTag("Player");

            if (spawnPoint != null && gorillaRig != null)
            {
                // Position und Rotation exakt anpassen
                gorillaRig.transform.position = spawnPoint.transform.position;
                gorillaRig.transform.rotation = spawnPoint.transform.rotation;

                // 3. Normale Physik wiederherstellen (Schwung & Rest-Geschwindigkeit stoppen)
                Rigidbody rb = gorillaRig.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                // Physik auf allen Child-Rigidbodies zurücksetzen (z.B. Hände/Arme)
                Rigidbody[] childRbs = gorillaRig.GetComponentsInChildren<Rigidbody>();
                foreach (Rigidbody childRb in childRbs)
                {
                    childRb.linearVelocity = Vector3.zero;
                    childRb.angularVelocity = Vector3.zero;
                }
            }

            isLoading = false;
        }
    }
}