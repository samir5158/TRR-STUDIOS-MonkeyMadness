using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
// Alias-Definitionen um Verwechslungen zu verhindern
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public class WheelRotator : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("1. Referenz")]
    public PCCubeCar carScript;

    [Header("2. Roll-Einstellungen (Vorwärts / Rückwärts)")]
    public Axis rollAxis = Axis.X;       // Wähle hier X, Y oder Z
    public bool invertRoll = false;      // Häkchen setzen, wenn es falsch herum rollt
    public float rollSpeed = 600f;       // Roll-Geschwindigkeit

    [Header("3. Lenk-Einstellungen (Nur Vorderräder)")]
    public bool isFrontWheel = false;    // Häkchen für Vorderräder
    public Axis steerAxis = Axis.Y;      // Wähle hier die Achse zum Lenken (meistens Y)
    public bool invertSteer = false;     // Häkchen setzen, wenn die Lenkung falsch herum ist
    public float maxSteerAngle = 30f;    // Wie weit das Rad einlenken darf

    private float currentSteerAngle = 0f;
    private Quaternion startLocalRotation;

    void Start()
    {
        if (carScript == null)
        {
            carScript = GetComponentInParent<PCCubeCar>();
        }

        startLocalRotation = transform.localRotation;
    }

    void Update()
    {
        if (carScript == null || !carScript.isDriving) return;

        float moveInput = 0f;
        float steerInput = 0f;

        // --- INPUT ABFRAGE (VR CONTROLLER ODERN TASTAUR) ---
        if (carScript.useVRControllers)
        {
            XRInputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            XRInputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            // Trigger für Vorwärts / Rückwärts rollen
            rightController.TryGetFeatureValue(XRCommonUsages.trigger, out float rightTrigger);
            leftController.TryGetFeatureValue(XRCommonUsages.trigger, out float leftTrigger);

            if (rightTrigger > carScript.triggerThreshold) moveInput = rightTrigger;
            else if (leftTrigger > carScript.triggerThreshold) moveInput = -leftTrigger;

            // Linker Stick für Lenkung
            leftController.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 leftThumbstick);
            steerInput = leftThumbstick.x;
        }
        else
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) moveInput = 1f;
                else if (Keyboard.current.sKey.isPressed) moveInput = -1f;

                if (Keyboard.current.aKey.isPressed) steerInput = -1f;
                else if (Keyboard.current.dKey.isPressed) steerInput = 1f;
            }
        }

        // --- LENKUNG (VR Stick / A & D) ---
        if (isFrontWheel)
        {
            if (invertSteer) steerInput *= -1f;

            float targetAngle = steerInput * maxSteerAngle;
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.deltaTime * 10f);

            Vector3 steerVector = GetAxisVector(steerAxis) * currentSteerAngle;
            transform.localRotation = startLocalRotation * Quaternion.Euler(steerVector);
        }

        // --- ROLLEN (VR Trigger / W & S) ---
        if (moveInput != 0f)
        {
            if (invertRoll) moveInput *= -1f;

            Vector3 spinVector = GetAxisVector(rollAxis);
            transform.Rotate(spinVector, moveInput * rollSpeed * Time.deltaTime, Space.Self);
        }
    }

    // Hilfsfunktion zur Auswahl der Achse
    private Vector3 GetAxisVector(Axis axis)
    {
        switch (axis)
        {
            case Axis.X: return Vector3.right;
            case Axis.Y: return Vector3.up;
            case Axis.Z: return Vector3.forward;
            default: return Vector3.right;
        }
    }
}