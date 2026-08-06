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

    private void OnTriggerEnter(Collider other)
    {
        // Prüfen, ob die Hand den Button berührt
        if (other.CompareTag("HandTag") && Time.time > lastPressed + cooldown)
        {
            lastPressed = Time.time;

            // --- VIBRATION ---
            TriggerHapticFeedback(other);

            // --- SOUND ---
            if (clickSound != null)
            {
                clickSound.Play();
            }

            // A: Wenn es der Farb-Switch ist
            if (colorManager != null)
            {
                colorManager.SwitchColorMode();
                Debug.Log("Color Mode gewechselt!");
            }
            // B: Wenn es der Namens-Switch ist
            else if (nameManager != null)
            {
                nameManager.SwitchMode();
                Debug.Log("Name/Room Mode gewechselt!");
            }
        }
    }

    private void TriggerHapticFeedback(Collider handCollider)
    {
        // Check ob links oder rechts
        XRNode handNode = XRNode.RightHand;
        if (handCollider.gameObject.name.ToLower().Contains("left"))
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