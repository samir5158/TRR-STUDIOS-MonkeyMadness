using UnityEngine;
using UnityEngine.XR;

public class KeypadButton : MonoBehaviour
{
    [Header("Eingabe")]
    public string buttonValue; // Im Inspector "1", "2", "ENTER" oder "CLEAR" eingeben

    [Header("Ziel-Keypad")]
    public KeypadSystem master;

    [Header("VR-Einstellungen")]
    public float pressCooldown = 0.3f; // Verhindert Doppelklicks
    private float lastPressedTime;

    [Header("Vibration")]
    public float hapticIntensity = 0.5f; 
    public float hapticDuration = 0.1f;  

    private void OnTriggerEnter(Collider other)
    {
        // Wir nutzen "HandTag", weil das bei deinem Computer-Skript funktioniert!
        if (other.CompareTag("HandTag"))
        {
            // Cooldown-Check
            if (Time.time >= lastPressedTime + pressCooldown)
            {
                lastPressedTime = Time.time;
                
                // Vibration auslösen
                TriggerHapticFeedback(other);
                
                // Befehl an das Keypad-System senden
                if (master != null)
                {
                    master.PressButton(buttonValue);
                }
                else
                {
                    Debug.LogError("Master fehlt auf Button: " + gameObject.name);
                }
            }
        }
    }

    private void TriggerHapticFeedback(Collider handCollider)
    {
        // Findet heraus, ob die linke oder rechte Hand gedrückt hat
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