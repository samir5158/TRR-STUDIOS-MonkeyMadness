using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using GorillaLocomotion;

public class PCCubeCar : MonoBehaviour
{
    [Header("Steuerungs-Modus")]
    [Tooltip("Haken AN: Steuerung über VR-Controller. Haken AUS: Steuerung über PC (Tastatur).")]
    public bool useVRControllers = false;

    [Header("VR Controller Schwellenwerte")]
    [Range(0.1f, 0.9f)] public float triggerThreshold = 0.1f;
    [Range(0.1f, 0.9f)] public float gripThreshold = 0.5f;

    [Header("Auto Einstellungen")]
    public float speed = 15f;          // Vorwärts-Geschwindigkeit
    public float reverseSpeed = 8f;   // Rückwärts-Geschwindigkeit
    public float turnSpeed = 80f;     // Lenkgeschwindigkeit

    [Header("Spieler Referenz")]
    [Tooltip("Ziehe hier das oberste HAUPTOBJEKT deines Gorilla Players rein (z.B. Gorilla Rig)")]
    public GameObject playerGameObject;

    [Header("Kamera Einstellungen")]
    [Tooltip("Ziehe hier die 3rd-Person Kamera vom Auto rein (GTA-Perspektive von außen)")]
    public GameObject carCamera;

    [Tooltip("Ziehe hier die separate 1st-Person Kamera vom Auto rein (Sicht aus dem Auto heraus)")]
    public GameObject car1stPersonCamera;

    [Header("Sitz & Sichtbarkeits-Einstellungen")]
    [Tooltip("Haken AN: Player wird fest auf den Sitz platziert und bleibt dort verankert.")]
    public bool teleportPlayerToSeat = true;

    [Tooltip("Sitz-Position für den Player (z. B. SEAT Objekt im Auto)")]
    public Transform seatPosition;

    [Header("Audio & Voice (Mikrofon) Einstellungen")]
    [Tooltip("Ziehe hier das AudioListener-Objekt vom Auto rein (z.B. die CarCamera)")]
    public AudioListener carAudioListener;

    [Tooltip("Ziehe hier das Objekt mit deinem Photon Voice / Recorder rein")]
    public Transform voiceRecorderObject;

    [Tooltip("Ein Empty GameObject im Auto (z.B. SEAT), an dem die Stimme FEST fixiert werden soll")]
    public Transform carVoicePosition;

    [Header("Aussteig-Einstellungen")]
    [Tooltip("Ziehe hier ein Empty GameObject rein, das links neben der Fahrertür platziert ist (z.B. EXIT)")]
    public Transform exitPosition;

    [Tooltip("Abstand links vom Auto beim Aussteigen in Metern (falls exitPosition nicht zugewiesen ist)")]
    public float exitOffsetLeft = 0.8f;

    [Header("Status")]
    public bool isDriving = false;

    private bool isThirdPersonCamera = true;

    private Player gorillaMovement;
    private Rigidbody playerRigidbody;
    private AudioListener playerAudioListener;

    private Transform originalPlayerParent;
    private Transform originalVoiceParent;
    private Vector3 originalVoiceLocalPos;
    private Quaternion originalVoiceLocalRot;

    private bool lastGripState = false;
    private bool lastAButtonState = false;

    private float lastToggleTime = 0f;

    void Start()
    {
        // WICHTIG: Beim Spielstart ist das Auto immer aus, damit man draußen bleibt
        isDriving = false;

        if (carCamera != null) carCamera.SetActive(false);
        if (car1stPersonCamera != null) car1stPersonCamera.SetActive(false);
        if (carAudioListener != null) carAudioListener.enabled = false;

        if (playerGameObject != null)
        {
            gorillaMovement = playerGameObject.GetComponent<Player>();
            if (gorillaMovement == null)
            {
                gorillaMovement = playerGameObject.GetComponentInChildren<Player>();
            }

            playerRigidbody = playerGameObject.GetComponent<Rigidbody>();
            if (playerRigidbody == null)
            {
                playerRigidbody = playerGameObject.GetComponentInChildren<Rigidbody>();
            }

            playerAudioListener = playerGameObject.GetComponentInChildren<AudioListener>();
            originalPlayerParent = playerGameObject.transform.parent;
        }

        if (voiceRecorderObject != null)
        {
            originalVoiceParent = voiceRecorderObject.parent;
            originalVoiceLocalPos = voiceRecorderObject.localPosition;
            originalVoiceLocalRot = voiceRecorderObject.localRotation;
        }
    }

    void Update()
    {
        bool switchCameraPressed = false;
        bool exitDrivingPressed = false;

        if (useVRControllers)
        {
            UnityEngine.XR.InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            UnityEngine.XR.InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            // 1. Nur fürs Aussteigen wenn man BEREITS fährt (Grip-Taste)
            if (isDriving)
            {
                leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float leftGrip);
                rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float rightGrip);
                bool currentGripState = (leftGrip > gripThreshold) || (rightGrip > gripThreshold);

                if (currentGripState && !lastGripState)
                {
                    exitDrivingPressed = true;
                }
                lastGripState = currentGripState;
            }
            else
            {
                // Wenn man NICHT fährt, soll der Grip-Zustand hier nicht greifen
                lastGripState = false;
            }

            // 2. Kamera umschalten mit Taste 'A' (Rechter Controller) - Nur wenn man fährt
            if (isDriving)
            {
                rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aButtonPressed);
                if (aButtonPressed && !lastAButtonState)
                {
                    switchCameraPressed = true;
                }
                lastAButtonState = aButtonPressed;
            }
        }
        else
        {
            if (Keyboard.current != null)
            {
                if (isDriving && Keyboard.current.eKey.wasPressedThisFrame) exitDrivingPressed = true;
                if (isDriving && Keyboard.current.cKey.wasPressedThisFrame) switchCameraPressed = true;
            }
        }

        // Wenn man fährt und Aussteigen gedrückt wird -> raus aus dem Auto
        if (isDriving && exitDrivingPressed)
        {
            ToggleDriving();
        }

        if (isDriving)
        {
            if (switchCameraPressed)
            {
                SwitchCameraMode();
            }

            HandleMovement();
        }
    }

    void LateUpdate()
    {
        if (isDriving)
        {
            // Voice Recorder Fixieren
            if (voiceRecorderObject != null)
            {
                Transform targetVoiceParent = (carVoicePosition != null) ? carVoicePosition : transform;
                voiceRecorderObject.position = targetVoiceParent.position;
                voiceRecorderObject.rotation = targetVoiceParent.rotation;
            }

            // Player exakt an Sitz halten
            if (teleportPlayerToSeat && playerGameObject != null && seatPosition != null)
            {
                playerGameObject.transform.position = seatPosition.position;

                if (!useVRControllers)
                {
                    playerGameObject.transform.rotation = seatPosition.rotation;
                }
            }
        }
    }

    public void ToggleDriving()
    {
        if (Time.time - lastToggleTime < 0.5f) return;
        lastToggleTime = Time.time;

        isDriving = !isDriving;

        if (isDriving)
        {
            if (gorillaMovement != null) gorillaMovement.locomotionEnabledLayers = 0;
            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
                playerRigidbody.isKinematic = true;
            }

            if (teleportPlayerToSeat && playerGameObject != null && seatPosition != null)
            {
                playerGameObject.transform.SetParent(seatPosition);
                playerGameObject.transform.localPosition = Vector3.zero;
                playerGameObject.transform.localRotation = Quaternion.identity;
            }

            if (playerAudioListener != null) playerAudioListener.enabled = false;

            SetPlayerVisibility(false);
            UpdateActiveCamera();

            if (carAudioListener != null) carAudioListener.enabled = true;

            if (voiceRecorderObject != null)
            {
                Transform targetVoiceParent = (carVoicePosition != null) ? carVoicePosition : transform;
                voiceRecorderObject.SetParent(targetVoiceParent);
                voiceRecorderObject.localPosition = Vector3.zero;
                voiceRecorderObject.localRotation = Quaternion.identity;
            }
        }
        else
        {
            if (carCamera != null) carCamera.SetActive(false);
            if (car1stPersonCamera != null) car1stPersonCamera.SetActive(false);
            if (carAudioListener != null) carAudioListener.enabled = false;

            if (voiceRecorderObject != null)
            {
                voiceRecorderObject.SetParent(originalVoiceParent);
                voiceRecorderObject.localPosition = originalVoiceLocalPos;
                voiceRecorderObject.localRotation = originalVoiceLocalRot;
            }

            if (playerGameObject != null)
            {
                playerGameObject.transform.SetParent(originalPlayerParent);

                if (exitPosition != null)
                {
                    playerGameObject.transform.position = exitPosition.position;
                    playerGameObject.transform.rotation = exitPosition.rotation;
                }
                else
                {
                    playerGameObject.transform.position = transform.position + (-transform.right * exitOffsetLeft);
                    playerGameObject.transform.rotation = transform.rotation;
                }

                if (playerRigidbody != null)
                {
                    playerRigidbody.isKinematic = false;
                }

                SetPlayerVisibility(true);
            }

            if (playerAudioListener != null) playerAudioListener.enabled = true;

            StartCoroutine(EnableGorillaLocomotionWithDelay());
        }
    }

    void SwitchCameraMode()
    {
        isThirdPersonCamera = !isThirdPersonCamera;
        UpdateActiveCamera();
    }

    void UpdateActiveCamera()
    {
        if (car1stPersonCamera == null)
        {
            if (carCamera != null) carCamera.SetActive(true);
            return;
        }

        if (carCamera != null) carCamera.SetActive(isThirdPersonCamera);
        if (car1stPersonCamera != null) car1stPersonCamera.SetActive(!isThirdPersonCamera);

        if (!isThirdPersonCamera && useVRControllers && car1stPersonCamera != null)
        {
            if (seatPosition != null)
            {
                car1stPersonCamera.transform.position = seatPosition.position;
            }
        }
    }

    IEnumerator EnableGorillaLocomotionWithDelay()
    {
        yield return new WaitForSeconds(0.1f);
        if (gorillaMovement != null) gorillaMovement.locomotionEnabledLayers = 1;
    }

    void SetPlayerVisibility(bool visible)
    {
        if (playerGameObject == null) return;
        Renderer[] renderers = playerGameObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            rend.enabled = visible;
        }
    }

    void HandleMovement()
    {
        float gasInput = 0f;
        float reverseInput = 0f;
        float turnInput = 0f;

        if (useVRControllers)
        {
            UnityEngine.XR.InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            UnityEngine.XR.InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out gasInput);
            leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out reverseInput);

            leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 leftThumbstick);
            turnInput = leftThumbstick.x;
        }
        else
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) gasInput = 1f;
                if (Keyboard.current.sKey.isPressed) reverseInput = 1f;
                if (Keyboard.current.aKey.isPressed) turnInput = -1f;
                if (Keyboard.current.dKey.isPressed) turnInput = 1f;
            }
        }

        if (gasInput > triggerThreshold)
        {
            transform.Translate(Vector3.forward * (speed * gasInput) * Time.deltaTime);
            transform.Rotate(Vector3.up * turnInput * turnSpeed * Time.deltaTime);
        }
        else if (reverseInput > triggerThreshold)
        {
            transform.Translate(-Vector3.forward * (reverseSpeed * reverseInput) * Time.deltaTime);
            transform.Rotate(Vector3.up * (-turnInput) * turnSpeed * Time.deltaTime);
        }
    }
}