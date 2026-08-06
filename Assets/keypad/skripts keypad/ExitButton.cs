using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;

public class ExitButton : MonoBehaviour
{
    [Header("Ziel-Keypad")]
    public KeypadSystem master; // Hier wieder das Keypad-Hauptobjekt reinziehen

    [Header("VR-Einstellungen")]
    public float pressCooldown = 0.5f; 
    private float lastPressedTime;

    [Header("Vibration")]
    public float hapticIntensity = 0.5f; 
    public float hapticDuration = 0.1f;  

    private void OnTriggerEnter(Collider other)
    {
        // Check für Gorilla-Hände
        if (other.CompareTag("HandTag"))
        {
            if (Time.time >= lastPressedTime + pressCooldown)
            {
                lastPressedTime = Time.time;
                
                TriggerHapticFeedback(other);
                
                if (master != null)
                {
                    // Wir rufen direkt den Erfolg beim Master auf!
                    // Da wir über Photon spielen, nutzen wir den RPC
                    if (PhotonNetwork.InRoom)
                    {
                        master.photonView.RPC("RPC_Success", RpcTarget.AllBuffered);
                    }
                    else
                    {
                        master.RPC_Success();
                    }
                    
                    Debug.Log("Exit-Button gedrückt: Tür öffnet sich.");
                }
                else
                {
                    Debug.LogError("Master fehlt auf dem Exit-Button!");
                }
            }
        }
    }

    private void TriggerHapticFeedback(Collider handCollider)
    {
        XRNode handNode = XRNode.RightHand; 
        if (handCollider.gameObject.name.ToLower().Contains("left"))
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