using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // Falls du das XR Interaction Toolkit nutzt
using UnityEngine.XR;

public static class HapticFeedback
{
    public static void TriggerVibration(GameObject hand, float duration, float amplitude)
    {
        // Wir suchen den Controller an der Hand, die den Trigger ausgelöst hat
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(hand.name.Contains("Left") ? XRNode.LeftHand : XRNode.RightHand);
        
        if (device.isValid)
        {
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }
}