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
    public AudioSource clickSound;

    [Header("Visuelles Feedback (Farbe)")]
    public Color flashColor = Color.red;
    public float flashDuration = 0.15f;
    private Renderer buttonRenderer;
    private Color originalColor;
    private Coroutine flashCoroutine;

    private void Awake()
    {
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
            // --- ISMINE CHECK ---
            // Reagiert NUR auf die lokale Hand des Spielers!
            PhotonView handPV = other.GetComponentInParent<PhotonView>();
            if (handPV != null && !handPV.IsMine)
            {
                return; // Hand von anderen Spielern ignorieren
            }

            if (Time.time >= lastPressedTime + pressCooldown)
            {
                lastPressedTime = Time.time;

                TriggerColorFlash();
                TriggerHapticFeedback(other);

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
        buttonRenderer.material.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
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

        if (nameManager != null)
        {
            if (val == "SWITCH") nameManager.SwitchMode();
            else if (val == "LEAVE") PhotonNetwork.LeaveRoom();
            else if (val == "PUBLIC") PhotonNetwork.JoinRandomOrCreateRoom();
            else nameManager.OnKeyPressed(val);
        }

        if (colorManager != null)
        {
            if (val == "SWITCH") colorManager.SwitchColorMode();
            else if (val == "ENTER" || val == "SAVE") colorManager.SaveColor();
            else colorManager.SetColorValue(val);
        }
    }
}