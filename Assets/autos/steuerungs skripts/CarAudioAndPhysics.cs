using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
// Alias-Definitionen um Verwechslungen zwischen UnityEngine.InputSystem und UnityEngine.XR zu verhindern
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class CarAudioAndPhysics : MonoBehaviour
{
    [Header("Referenz zum Auto-Skript")]
    public PCCubeCar carController;

    [Header("Audio Clips")]
    [Tooltip("Sound beim Einsteigen (Motor startet)")]
    public AudioClip engineStartClip;
    [Tooltip("Looping Sound, wenn das Auto fährt (Motorbrummen)")]
    public AudioClip engineDriveClip;
    [Tooltip("Quietschender Reifen-Sound beim Bremsen")]
    public AudioClip brakeClip;

    [Header("Audio Einstellungen")]
    [Range(0f, 1f)] public float engineVolume = 0.8f;
    [Tooltip("Wie hoch der Motorsound beim Schnellfahren maximal wird")]
    [Range(1f, 3f)] public float maxPitch = 2.0f;

    [Header("Fake Physik & Neigung")]
    [Tooltip("Drückt das Auto auf den Boden, damit es nicht hüpft")]
    public float downforce = 50f;
    [Tooltip("Wie stark sich das Auto in der Kurve zur Seite neigt")]
    public float maxTiltAngle = 8f;
    public float tiltSmoothing = 5f;

    private Rigidbody rb;
    private AudioSource audioSource;
    private AudioSource brakeAudioSource;

    private bool engineIsRunning = false;
    private bool isBraking = false;

    // Eigene Geschwindigkeitsmessung
    private Vector3 lastPosition;
    private float currentSpeed;
    private Vector3 moveDirection;

    private float currentTilt = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        audioSource.spatialBlend = 1.0f;
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        brakeAudioSource = gameObject.AddComponent<AudioSource>();
        brakeAudioSource.spatialBlend = 1.0f;
        brakeAudioSource.playOnAwake = false;

        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (carController == null)
        {
            carController = GetComponent<PCCubeCar>();
        }

        lastPosition = transform.position;
    }

    void Update()
    {
        if (carController == null) return;

        // 1. ECHTE GESCHWINDIGKEIT BERECHNEN
        if (Time.deltaTime > 0f)
        {
            currentSpeed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
            moveDirection = (transform.position - lastPosition).normalized;
        }
        lastPosition = transform.position;

        // Wenn niemand im Auto sitzt -> Alles aus!
        if (!carController.isDriving)
        {
            StopAllAudio();
            engineIsRunning = false;
            return;
        }

        // Wenn man einsteigt, Motor starten
        if (!engineIsRunning)
        {
            StartCoroutine(StartEngineRoutine());
        }

        HandleAudio();
    }

    void HandleAudio()
    {
        bool gasPressed = false;
        bool reversePressed = false;

        if (carController.useVRControllers)
        {
            XRInputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            XRInputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            rightController.TryGetFeatureValue(XRCommonUsages.trigger, out float rightTrigger);
            leftController.TryGetFeatureValue(XRCommonUsages.trigger, out float leftTrigger);

            gasPressed = rightTrigger > carController.triggerThreshold;
            reversePressed = leftTrigger > carController.triggerThreshold;
        }
        else
        {
            if (Keyboard.current != null)
            {
                gasPressed = Keyboard.current.wKey.isPressed;
                reversePressed = Keyboard.current.sKey.isPressed;
            }
        }

        // 2. MOTORSOUND PITCH (Dynamisch nach Geschwindigkeit)
        if (engineIsRunning && audioSource.clip == engineDriveClip && audioSource.isPlaying)
        {
            float speedFactor = Mathf.Clamp01(currentSpeed / carController.speed);
            float targetPitch = Mathf.Lerp(0.8f, maxPitch, speedFactor);

            if (gasPressed) targetPitch += 0.2f;

            audioSource.pitch = Mathf.Lerp(audioSource.pitch, targetPitch, Time.deltaTime * 5f);
            audioSource.volume = Mathf.Lerp(audioSource.volume, engineVolume, Time.deltaTime * 5f);
        }

        // 3. BREMS-SOUND
        bool movingForward = Vector3.Dot(transform.forward, moveDirection) > 0.5f;

        if (reversePressed && movingForward && currentSpeed > 3f)
        {
            if (!isBraking && brakeClip != null)
            {
                brakeAudioSource.PlayOneShot(brakeClip, 0.7f);
                isBraking = true;
            }
        }
        else if (!reversePressed || currentSpeed < 1f)
        {
            isBraking = false;
        }
    }

    IEnumerator StartEngineRoutine()
    {
        engineIsRunning = true;

        if (engineStartClip != null)
        {
            audioSource.clip = engineStartClip;
            audioSource.loop = false;
            audioSource.pitch = 1f;
            audioSource.volume = engineVolume;
            audioSource.Play();

            yield return new WaitForSeconds(engineStartClip.length * 0.9f);
        }

        if (engineDriveClip != null)
        {
            audioSource.clip = engineDriveClip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    // Die Physik und das Neigen MÜSSEN in FixedUpdate passieren, damit Trigger nicht kaputtgehen!
    void FixedUpdate()
    {
        if (carController == null) return;

        if (carController.isDriving)
        {
            // Auto auf den Boden drücken
            rb.AddForce(-Vector3.up * downforce * rb.mass);

            // Lenkung auslesen
            float turnInput = 0f;
            if (carController.useVRControllers)
            {
                XRInputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                leftController.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 leftThumbstick);
                turnInput = leftThumbstick.x;
            }
            else
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.isPressed) turnInput = -1f;
                    if (Keyboard.current.dKey.isPressed) turnInput = 1f;
                }
            }

            // Neigung berechnen (nur wenn man auch Geschwindigkeit hat)
            float speedFactor = Mathf.Clamp01(currentSpeed / 5f);
            float targetTilt = -turnInput * maxTiltAngle * speedFactor;

            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.fixedDeltaTime * tiltSmoothing);

            // GANZ WICHTIG: rb.MoveRotation nutzen! Das hält die Unity-Physik sauber.
            Quaternion newRotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, currentTilt);
            rb.MoveRotation(newRotation);
        }
        else
        {
            // Wenn man aussteigt: Auto sanft wieder gerade richten, bis es bei 0 ist
            if (Mathf.Abs(currentTilt) > 0.01f)
            {
                currentTilt = Mathf.Lerp(currentTilt, 0f, Time.fixedDeltaTime * tiltSmoothing);
                Quaternion resetRotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, currentTilt);
                rb.MoveRotation(resetRotation);
            }
            else
            {
                currentTilt = 0f;
            }
        }
    }

    void StopAllAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        if (brakeAudioSource != null && brakeAudioSource.isPlaying)
        {
            brakeAudioSource.Stop();
        }
    }
}