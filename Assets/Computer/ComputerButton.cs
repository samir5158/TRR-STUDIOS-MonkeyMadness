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
    public AudioSource clickSound; // Ziehe hier deine AudioSource rein

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("HandTag"))
        {
            if (Time.time >= lastPressedTime + pressCooldown)
            {
                lastPressedTime = Time.time;
                
                // --- VIBRATION ---
                TriggerHapticFeedback(other);
                
                // --- SOUND ---
                if (clickSound != null)
                {
                    clickSound.Play();
                }
                
                ExecuteButtonAction();
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

    private void ExecuteButtonAction()
    {
        string val = buttonValue.ToUpper();

        if (colorManager != null)
        {
            if (val == "SWITCH") colorManager.SwitchColorMode();
            else if (val == "ENTER") colorManager.SaveColor();
            else colorManager.SetColorValue(val);
        }
        else if (nameManager != null)
        {
            if (val == "SWITCH") nameManager.SwitchMode();
            else if (val == "LEAVE") PhotonNetwork.LeaveRoom();
            else if (val == "PUBLIC") PhotonNetwork.JoinRandomOrCreateRoom();
            else nameManager.OnKeyPressed(val);
        }
    }
}