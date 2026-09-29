using UnityEngine;
using UnityEngine.XR;

public class GorillaWheelGrabber : MonoBehaviour
{
    public enum XRHand
    {
        LeftHand,
        RightHand
    }

    [Header("✋ Hand-Einstellungen")]
    [SerializeField] private XRHand handType = XRHand.RightHand;
    [SerializeField] private float gripThreshold = 0.5f;

    private VRSteeringWheel currentWheel;
    private bool isGrabbing = false;
    private InputDevice targetDevice;

    private void Start()
    {
        InitializeInputDevice();
    }

    private void InitializeInputDevice()
    {
        XRNode node = (handType == XRHand.LeftHand) ? XRNode.LeftHand : XRNode.RightHand;
        targetDevice = InputDevices.GetDeviceAtXRNode(node);
    }

    private void Update()
    {
        if (!targetDevice.isValid)
        {
            InitializeInputDevice();
            return;
        }

        // Controller Grip-Taste abfragen
        targetDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue);
        bool wantsToGrab = gripValue >= gripThreshold;

        // Greifen starten
        if (wantsToGrab && !isGrabbing && currentWheel != null)
        {
            isGrabbing = true;
            currentWheel.OnGrab(transform);
        }
        // Greifen beenden / Loslassen
        else if (!wantsToGrab && isGrabbing)
        {
            isGrabbing = false;
            if (currentWheel != null)
            {
                currentWheel.OnRelease();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Prüfen, ob der Trigger das Lenkrad trifft
        VRSteeringWheel wheel = other.GetComponentInParent<VRSteeringWheel>();
        if (wheel != null)
        {
            currentWheel = wheel;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        VRSteeringWheel wheel = other.GetComponentInParent<VRSteeringWheel>();
        if (wheel != null && wheel == currentWheel)
        {
            if (isGrabbing)
            {
                isGrabbing = false;
                currentWheel.OnRelease();
            }
            currentWheel = null;
        }
    }
}