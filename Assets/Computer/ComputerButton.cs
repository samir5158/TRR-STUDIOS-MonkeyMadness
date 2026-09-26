using System.Collections;
using UnityEngine;
using Photon.Pun;
using UnityEngine.XR;

public class ComputerButton : MonoBehaviour
{
    [Header("Eingabe")]
    public string buttonValue;

    [Header("Ziel-Computer")]
    public ComputerManager nameManager;
    public ColorComputer colorManager;

    [Header("VR-Einstellungen")]
    public float pressCooldown = 0.3f;
    private float lastPressedTime;

    [Header("Vibration")]
    public float hapticIntensity = 0.5f;
    public float hapticDuration = 0.1f;

    [Header("Sound")]
    public AudioSource clickSound; // Ziehe hier deine AudioSource rein

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
        if (other.CompareTag("HandTag"))
        {
            if (Time.time >= lastPressedTime + pressCooldown)
            {
                lastPressedTime = Time.time;

                // --- VISUELLES FEEDBACK (Taste wird rot) ---
                TriggerColorFlash();

                // --- VIBRATION ---
                TriggerHapticFeedback(other);

                // --- SOUND ---
                if (clickSound != null)
                {
                    clickSound.Play();
                }

                ExecuteButtonAction();
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

    private void ExecuteButtonAction()
    {
        string val = buttonValue.ToUpper();

        // Sendet Befehle an den NameManager (falls zugewiesen)
        if (nameManager != null)
        {
            if (val == "SWITCH") nameManager.SwitchMode();
            else if (val == "LEAVE") PhotonNetwork.LeaveRoom();
            else if (val == "PUBLIC") PhotonNetwork.JoinRandomOrCreateRoom();
            else nameManager.OnKeyPressed(val);
        }

        // Sendet Befehle an den ColorManager (falls zugewiesen)
        if (colorManager != null)
        {
            if (val == "SWITCH") colorManager.SwitchColorMode();
            else if (val == "ENTER" || val == "SAVE") colorManager.SaveColor();
            else colorManager.SetColorValue(val);
        }
    }
}