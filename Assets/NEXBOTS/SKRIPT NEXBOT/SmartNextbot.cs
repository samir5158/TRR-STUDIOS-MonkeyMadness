using UnityEngine;
using Photon.Pun;

public class SmartNextbotMultiplayer : MonoBehaviourPun
{
    [Header("Aktivierung & Sichtweite")]
    public float sichtWeite = 30.0f; 
    public bool jagtGerade = false;

    [Header("Geschwindigkeit & Balance")]
    public float geschwindigkeitsFaktor = 1.3f; 
    public float mindestGeschwindigkeit = 5.0f; 
    public float maximaleGeschwindigkeit = 18.0f;
    
    [Header("Nacken-Klebe-Effekt")]
    public float nackenAbstand = 1.5f; 

    [Header("Sofort-Angriff bei Stillstand")]
    [Tooltip("Wie schnell muss der Spieler mindestens sein, damit der Bot abbremst?")]
    public float stillstandsSchwelle = 0.5f;
    [Tooltip("Die extreme Geschwindigkeit, mit der er dich rammt, wenn du stehen bleibst.")]
    public float angriffsGeschwindigkeit = 22.0f;
    
    [Header("Multiplayer Ziel")]
    public Transform target; 
    private Rigidbody playerRb;
    
    [Header("Multiplayer Scan-Rate")]
    [Tooltip("Wie oft sucht der Bot nach dem nächsten Spieler? (0.5 bedeutet alle halbe Sekunde)")]
    public float playerCheckRate = 0.5f;
    private float nextPlayerCheckTime;

    private Rigidbody rb;

    [Header("Ansicht-Korrektur")]
    public bool bildUmdrehen = true;  
    public bool aufDemKopf = false;

    [Header("Effekte (Zittern)")]
    public bool zappeln = true; 
    public float zappelStaerke = 0.07f;

    [Header("Catch & Jumpscare")]
    public string playerTag = "Player"; 
    public AudioSource jumpscareAudio; 
    public Transform teleportZiel; 

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Erster Scan direkt beim Start
        if (PhotonNetwork.IsMasterClient)
        {
            FindClosestPlayer();
        }
    }

    void FixedUpdate()
    {
        // MULTIPLAYER-SCHUTZ: Nur der Master-Client steuert die Bewegung des Bots im Netzwerk!
        if (!PhotonNetwork.IsMasterClient) return;

        // Regelmäßig prüfen, wer der nächste Spieler ist
        if (Time.time >= nextPlayerCheckTime)
        {
            nextPlayerCheckTime = Time.time + playerCheckRate;
            FindClosestPlayer();
        }

        if (target == null)
        {
            jagtGerade = false;
            rb.linearVelocity = Vector3.zero;
            return;
        }

        float distanz = Vector3.Distance(transform.position, target.position);

        if (distanz <= sichtWeite)
        {
            jagtGerade = true;
            
            // Aktuelle Geschwindigkeit des anvisierten Spielers holen
            float playerSpeed = (playerRb != null) ? playerRb.linearVelocity.magnitude : 0f;
            float aktuelleSpeed = Mathf.Clamp(playerSpeed * geschwindigkeitsFaktor, mindestGeschwindigkeit, maximaleGeschwindigkeit);

            // PRÜFUNG: Steht der gejagte Spieler still?
            if (playerSpeed < stillstandsSchwelle)
            {
                aktuelleSpeed = angriffsGeschwindigkeit;
            }
            else
            {
                // Wenn er im Nacken sitzt, leicht abbremsen
                if (distanz < nackenAbstand)
                {
                    aktuelleSpeed = playerSpeed * 0.96f; 
                }
            }

            Vector3 direction = (target.position - transform.position).normalized;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, direction * aktuelleSpeed, 0.2f);

            // Blickrichtung zum aktuellen Opfer
            Vector3 lookTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.LookAt(lookTarget);
            transform.Rotate(90, 0, 0); 
            if (bildUmdrehen) transform.Rotate(0, 180, 0);
            if (aufDemKopf) transform.Rotate(0, 0, 180);

            if (zappeln)
            {
                transform.position += new Vector3(
                    Random.Range(-zappelStaerke, zappelStaerke), 
                    Random.Range(-zappelStaerke, zappelStaerke), 
                    Random.Range(-zappelStaerke, zappelStaerke)
                );
            }
        }
        else
        {
            jagtGerade = false;
            rb.linearVelocity = Vector3.zero;
        }
    }

    void FindClosestPlayer()
    {
        // Sucht ALLE Spieler im Raum über das "MainCamera"-Tag
        GameObject[] players = GameObject.FindGameObjectsWithTag("MainCamera");
        
        GameObject closestPlayer = null;
        float shortestDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;

        foreach (GameObject p in players)
        {
            if (p == null) continue;

            float distanceToPlayer = Vector3.Distance(currentPosition, p.transform.position);
            
            // Wenn der Spieler näher dran ist als der vorherige, wird er das neue Ziel
            if (distanceToPlayer < shortestDistance)
            {
                shortestDistance = distanceToPlayer;
                closestPlayer = p;
            }
        }

        // Wenn ein neuer nähesten Spieler gefunden wurde, das Ziel updaten
        if (closestPlayer != null)
        {
            if (target == null || target.gameObject != closestPlayer)
            {
                target = closestPlayer.transform;
                playerRb = target.GetComponentInParent<Rigidbody>();
            }
        }
    }

    private void OnCollisionEnter(Collision collision) { CheckForPlayer(collision.gameObject); }
    private void OnTriggerEnter(Collider other) { CheckForPlayer(other.gameObject); }

    private void CheckForPlayer(GameObject hitObject)
    {
        // Hit-Check funktioniert über PhotonView
        PhotonView pv = hitObject.transform.root.GetComponent<PhotonView>();

        if (hitObject.CompareTag(playerTag) || hitObject.transform.root.CompareTag(playerTag))
        {
            // RPC oder lokaler Check: Nur der getroffene Spieler führt den Jumpscare lokal aus!
            if (pv != null && pv.IsMine)
            {
                ExecuteJumpscare(hitObject.transform.root.gameObject);
            }
        }
    }

    void ExecuteJumpscare(GameObject player)
    {
        if (jumpscareAudio != null) jumpscareAudio.Play();

        if (teleportZiel != null)
        {
            Rigidbody[] allRbs = player.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody playerRbInComponents in allRbs)
            {
                playerRbInComponents.linearVelocity = Vector3.zero;
                playerRbInComponents.angularVelocity = Vector3.zero;
            }

            player.transform.position = teleportZiel.position;
            player.transform.rotation = teleportZiel.rotation;
            
            Physics.SyncTransforms();

            foreach (Rigidbody playerRbInComponents in allRbs)
            {
                playerRbInComponents.linearVelocity = Vector3.zero;
                playerRbInComponents.angularVelocity = Vector3.zero;
            }

            Debug.Log("Du wurdest vom Nextbot im Multiplayer erwischt!");
        }
    }
}