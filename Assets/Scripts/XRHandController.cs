using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;
using Photon.Realtime;

public enum HandType
{
    Left,
    Right
}

public class XRHandController : MonoBehaviour
{
    public HandType handType;
    public float thumbMoveSpeed = 0.1f;

    private Animator animator;
    private InputDevice inputDevice;
    private bool isDeviceValid = false; // Prüft, ob der Controller erfolgreich verbunden ist

    private float pose1Value;
    private float pose2Value;
    private float pose3Value;

    public PhotonView view;

    void Start()
    {
        animator = GetComponent<Animator>();
        TryInitializeController(); // Erster Versuch beim Start
    }

    void Update()
    {
        // Falls wir nicht der Besitzer sind, machen wir gar nichts
        if (!view.IsMine) return;

        // Wenn der Controller noch nicht gefunden wurde oder die Verbindung verloren hat, suchen wir ihn neu
        if (!isDeviceValid || !inputDevice.isValid)
        {
            TryInitializeController();
            return; // Frame abbrechen, da wir noch keine Daten zum Animieren haben
        }

        // Wenn alles bereit ist, animieren!
        AnimateHand();
    }

    void TryInitializeController()
    {
        InputDeviceCharacteristics controllerCharacteristic = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller;

        if (handType == HandType.Left)
        {
            controllerCharacteristic |= InputDeviceCharacteristics.Left;
        }
        else
        {
            controllerCharacteristic |= InputDeviceCharacteristics.Right;
        }

        List<InputDevice> inputDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristic, inputDevices);

        // Die wichtigste Sicherheitsabfrage überhaupt:
        if (inputDevices.Count > 0)
        {
            inputDevice = inputDevices[0];
            isDeviceValid = true;
            Debug.Log($"[XR Hand] {handType} Controller erfolgreich initialisiert!");
        }
        else
        {
            isDeviceValid = false; // Liste ist leer, wir warten auf das nächste Frame im Update
        }
    }

    void AnimateHand()
    {
        inputDevice.TryGetFeatureValue(CommonUsages.trigger, out pose1Value);
        inputDevice.TryGetFeatureValue(CommonUsages.grip, out pose2Value);

        inputDevice.TryGetFeatureValue(CommonUsages.primaryTouch, out bool primaryTouched);
        inputDevice.TryGetFeatureValue(CommonUsages.secondaryTouch, out bool secondaryTouched);

        if (primaryTouched || secondaryTouched)
        {
            pose3Value += thumbMoveSpeed;
        }
        else
        {
            pose3Value -= thumbMoveSpeed;
        }

        pose3Value = Mathf.Clamp(pose3Value, 0, 1);

        animator.SetFloat("pose1", pose1Value);
        animator.SetFloat("pose2", pose2Value);
        animator.SetFloat("pose3", pose3Value);
    }
}