using System.Collections;
using UnityEngine;
using UnityEngine.XR; // WICHTIG für Vibration

public class ComputerSwitch : MonoBehaviour
{
    [Header("Ziel-Computer")]
    [Tooltip("Zieh hier den ComputerManager für Namen ODER den ColorComputer für Farben rein!")]
    public ComputerManager nameManager;
    public ColorComputer colorManager;

    [Header("Einstellungen")]
    public float cooldown = 0.5f;
    private float lastPressed;

    [Header("Vibration")]
    public float hapticIntensity = 0.5f;
    public float hapticDuration = 0.1f;

    [Header("Sound")]
    public AudioSource clickSound; // Hier wieder deine AudioSource reinziehen

    [Header("Visuelles Feedback (Farbe)")]
    public Color flashColor = Color.red; // Farbe beim Drauftippen
    public float flashDuration = 0.15f; // Dauer in Sekunden
    private Renderer buttonRenderer;
    private Color originalColor;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        // Holt sich den Renderer und speichert die normale Tastenfarbe
        buttonRenderer = GetComponent<Renderer>();
        if (buttonRenderer != null && buttonRenderer.material != null)
        {
            originalColor = buttonRenderer.material.color;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Prüfen, ob die Hand den Button berührt
        if (other.CompareTag("HandTag") && Time.time > lastPressed + cooldown)
        {
            lastPressed = Time.time;

            // --- VISUELLES FEEDBACK (Taste wird rot) ---
            TriggerColorFlash();

            // --- VIBRATION ---
            TriggerHapticFeedback(other);

            // --- SOUND ---
            if (clickSound != null)
            {
                clickSound.Play();
            }

            // A: Wenn der Farb-Manager zugewiesen ist
            if (colorManager != null)
            {
                colorManager.SwitchColorMode();
                Debug.Log("Color Mode gewechselt!");
            }

            // B: Wenn der Namens-Manager zugewiesen ist
            if (nameManager != null)
            {
                nameManager.SwitchMode();
                Debug.Log("Name/Room Mode gewechselt!");
            }
        }
    }

    private void TriggerColorFlash()
    {
        if (buttonRenderer == null) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Auf Rot (oder flashColor) wechseln
        buttonRenderer.material.color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        // Zurück zur Ursprungsfarbe
        buttonRenderer.material.color = originalColor;
        flashCoroutine = null;
    }

    private void TriggerHapticFeedback(Collider handCollider)
    {
        // Check ob links oder rechts (prüft Namen des Objekts und übergeordneter Rigs)
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