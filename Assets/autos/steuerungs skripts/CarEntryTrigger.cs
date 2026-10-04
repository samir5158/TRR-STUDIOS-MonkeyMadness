using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using GorillaLocomotion;

public class CarEntryTrigger : MonoBehaviour
{
    [Header("Haupt-Autoskript Referenz")]
    [Tooltip("Ziehe hier das Hauptskript (PCCubeCar) rein")]
    public PCCubeCar carScript;

    [Header("Sensor Einstellungen (Unsichtbare Kugel)")]
    [Tooltip("Der Mittelpunkt der unsichtbaren Einstiegs-Kugel (z.B. an der Tür)")]
    public Transform sensorPoint;
    [Tooltip("Wie groß ist der Radius der unsichtbaren Kugel (in Metern)?")]
    public float entryDistance = 1.2f;

    [Header("Grip Schwellenwert (Seitentaste)")]
    [Range(0.1f, 0.9f)] public float gripThreshold = 0.5f;

    [Header("Debug Status (Live)")]
    public bool isPlayerInside = false;
    public float currentDistance = 0f;

    private bool lastInteractState = false;

    void Start()
    {
        if (sensorPoint == null)
        {
            sensorPoint = transform;
        }
    }

    void Update()
    {
        if (carScript == null) return;

        // Wenn wir fahren, ist der Sensor aus
        if (carScript.isDriving)
        {
            isPlayerInside = false;
            lastInteractState = false;
            return;
        }

        // Spieler-Objekt ermitteln
        GameObject playerObj = GetPlayerObject();

        if (playerObj != null && sensorPoint != null)
        {
            // Unsichtbarer Kugel-Check: Misst den Abstand zum Mittelpunkt
            currentDistance = Vector3.Distance(playerObj.transform.position, sensorPoint.position);
            isPlayerInside = (currentDistance <= entryDistance);
        }
        else
        {
            isPlayerInside = false;
        }

        // Wenn du nicht in der unsichtbaren Kugel bist, machen wir nichts
        if (!isPlayerInside)
        {
            lastInteractState = false;
            return;
        }

        // Tasten-Abfrage (VR Grip oder PC 'E')
        bool isInteractPressed = false;

        if (carScript.useVRControllers)
        {
            UnityEngine.XR.InputDevice rightController = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            UnityEngine.XR.InputDevice leftController = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);

            leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float leftGrip);
            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float rightGrip);

            isInteractPressed = (leftGrip > gripThreshold) || (rightGrip > gripThreshold);
        }
        else
        {
            if (Keyboard.current != null)
            {
                isInteractPressed = Keyboard.current.eKey.isPressed;
            }
        }

        // Einsteigen auslösen bei Tastendruck
        if (isInteractPressed && !lastInteractState)
        {
            carScript.ToggleDriving();
        }

        lastInteractState = isInteractPressed;
    }

    GameObject GetPlayerObject()
    {
        if (carScript != null && carScript.playerGameObject != null)
        {
            return carScript.playerGameObject;
        }

        if (Player.Instance != null)
        {
            return Player.Instance.gameObject;
        }

        GameObject taggedPlayer = GameObject.FindWithTag("Player");
        if (taggedPlayer != null) return taggedPlayer;

        return null;
    }

    // Zeigt dir im Unity-Editor (Scene-Ansicht) einen roten Kreis zur Kontrolle, 
    // damit du siehst, wie groß die unsichtbare Kugel ist. Im Spiel selbst sieht man sie nicht!
    void OnDrawGizmosSelected()
    {
        Transform point = sensorPoint != null ? sensorPoint : transform;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(point.position, entryDistance);
    }
}