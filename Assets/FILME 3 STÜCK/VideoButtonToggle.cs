using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR;
using Photon.Pun; // Photon eingebunden, um den lokalen Spieler zu prüfen

public class LocalVideoButtonToggle : MonoBehaviour
{
    [Header("Ziel-Video Player")]
    public VideoPlayer videoPlayer;

    [Header("Farben")]
    public Color offColor = Color.red;   // Wenn Video aus ist
    public Color onColor = Color.green; // Wenn Video läuft

    [Header("Einstellungen")]
    public float pressCooldown = 0.4f;
    private float lastPressed;

    [Header("Feedback (Optional)")]
    public AudioSource clickSound;
    public float hapticIntensity = 0.5f;
    public float hapticDuration = 0.1f;

    private Renderer buttonRenderer;

    private void Awake()
    {
        buttonRenderer = GetComponent<Renderer>();

        // Startfarbe setzen
        SetButtonColor(offColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Cooldown prüfen
        if (Time.time < lastPressed + pressCooldown) return;

        // 2. Prüfen, ob es sich um eine Hand handelt
        if (!other.CompareTag("HandTag")) return;

        // 3. WICHTIG: Prüfen, ob es die eigene Hand (der lokale VR-Spieler) ist!
        PhotonView handPhotonView = other.GetComponentInParent<PhotonView>();
        if (handPhotonView != null && !handPhotonView.IsMine)
        {
            // Wenn es die Hand eines ANDEREN Spielers ist -> Abbrechen!
            return;
        }

        // --- Ab hier führt NUR der Spieler den Code aus, der geklickt hat ---
        lastPressed = Time.time;

        // Sound & Haptik nur für den lokalen Spieler auslösen
        if (clickSound != null) clickSound.Play();
        TriggerHapticFeedback(other);

        // Video lokal Umschalten (Play / Pause)
        if (videoPlayer != null)
        {
            if (videoPlayer.isPlaying)
            {
                // Video STOPPEN -> Knopf wird ROT
                videoPlayer.Pause();
                SetButtonColor(offColor);
                Debug.Log("Lokal: Video gestoppt!");
            }
            else
            {
                // Video STARTEN -> Knopf wird GRÜN
                videoPlayer.Play();
                SetButtonColor(onColor);
                Debug.Log("Lokal: Video gestartet!");
            }
        }
    }

    private void SetButtonColor(Color color)
    {
        if (buttonRenderer != null && buttonRenderer.material != null)
        {
            buttonRenderer.material.color = color;
        }
    }

    private void TriggerHapticFeedback(Collider handCollider)
    {
        XRNode handNode = XRNode.RightHand;
        if (handCollider.gameObject.name.ToLower().Contains("left") || handCollider.transform.root.name.ToLower().Contains("left"))
        {
            handNode = XRNode.LeftHand;
        }

        InputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
        if (device.isValid)
        {
            device.SendHapticImpulse(0u, hapticIntensity, hapticDuration);
        }
    }
}