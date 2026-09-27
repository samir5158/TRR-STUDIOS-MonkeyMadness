using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR;

public class VideoButtonToggle : MonoBehaviour
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

        // Startfarbe auf Rot setzen
        if (buttonRenderer != null && buttonRenderer.material != null)
        {
            buttonRenderer.material.color = offColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Prüfen ob Hand berührt und Cooldown abgelaufen ist
        if (other.CompareTag("HandTag") && Time.time > lastPressed + pressCooldown)
        {
            lastPressed = Time.time;

            // Sound & Haptik
            if (clickSound != null) clickSound.Play();
            TriggerHapticFeedback(other);

            // Toggle Logik (An / Aus)
            if (videoPlayer != null)
            {
                if (videoPlayer.isPlaying)
                {
                    // Video STOPPEN -> Knopf wird ROT
                    videoPlayer.Pause();
                    SetButtonColor(offColor);
                    Debug.Log("Video gestoppt!");
                }
                else
                {
                    // Video STARTEN -> Knopf wird GRÜN
                    videoPlayer.Play();
                    SetButtonColor(onColor);
                    Debug.Log("Video gestartet!");
                }
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