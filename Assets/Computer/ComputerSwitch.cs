using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;

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
            // Reagiert NUR auf die eigene VR-Hand!
            PhotonView handPV = other.GetComponentInParent<PhotonView>();
            if (handPV != null && !handPV.IsMine)
            {
                return; // Fremde Hände ablocken
            }

            if (Time.time > lastPressed + cooldown)
            {
                lastPressed = Time.time;

                TriggerColorFlash();
                TriggerHapticFeedback(other);

                if (clickSound != null)
                {
                    clickSound.Play();
                }

                if (colorManager != null)
                {
                    colorManager.SwitchColorMode();
                }

                if (nameManager != null)
                {
                    nameManager.SwitchMode();
                }
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
}