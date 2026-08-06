using UnityEngine;
using TMPro;
using System.Collections;
using Photon.Pun;

public class KeypadSystem : MonoBehaviourPun
{
    [Header("Einstellungen")]
    public string correctCode = "1209";
    public string welcomeMessage = "WILLKOMMEN!";
    public int maxCodeLength = 4;
    public float autoCloseDelay = 3f; // Zeit in Sekunden, bis die Tür wieder zugeht

    [Header("Referenzen")]
    public TextMeshPro display;
    public GameObject door; 
    public MeshRenderer statusBlockRenderer; 
    public AudioSource audioSource;

    [Header("Farben")]
    public Color normalColor = Color.white;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;

    [Header("Sounds")]
    public AudioClip clickSound;
    public AudioClip winSound;
    public AudioClip failSound;

    private string currentInput = "";
    private bool isUnlocked = false;

    void Start()
    {
        ResetDisplay();
        if (statusBlockRenderer) statusBlockRenderer.material.color = normalColor;
        
        if (GetComponent<PhotonView>() == null)
        {
            Debug.LogError("FEHLER: PhotonView fehlt auf diesem Objekt!");
        }
    }

    public void PressButton(string value)
    {
        if (isUnlocked) return; // Während die Tür offen ist, keine Eingabe möglich

        if (audioSource && clickSound) audioSource.PlayOneShot(clickSound);

        if (value == "ENTER")
        {
            CheckCode();
        }
        else if (value == "CLEAR")
        {
            currentInput = "";
            UpdateText();
        }
        else
        {
            if (currentInput.Length < maxCodeLength)
            {
                currentInput += value;
                UpdateText();
            }
        }
    }

    void CheckCode()
    {
        if (currentInput.Trim() == correctCode.Trim())
        {
            if (PhotonNetwork.InRoom && photonView != null)
            {
                photonView.RPC("RPC_Success", RpcTarget.AllBuffered);
            }
            else
            {
                RPC_Success();
            }
        }
        else
        {
            StartCoroutine(FlashError());
        }
    }

    [PunRPC]
    public void RPC_Success()
    {
        isUnlocked = true; // Sperrt Eingabe
        display.text = $"<color=green>{welcomeMessage}</color>";
        
        if (statusBlockRenderer) statusBlockRenderer.material.color = correctColor;
        if (audioSource && winSound) audioSource.PlayOneShot(winSound);
        
        if (door != null)
        {
            door.SetActive(false); // Tür auf
            StartCoroutine(CloseDoorAfterDelay()); // Timer starten
        }
    }

    // --- NEU: DER TIMER ZUM SCHLIESSEN ---
    IEnumerator CloseDoorAfterDelay()
    {
        yield return new WaitForSeconds(autoCloseDelay);

        if (door != null) door.SetActive(true); // Tür wieder zu

        // Keypad resetten
        isUnlocked = false;
        if (statusBlockRenderer) statusBlockRenderer.material.color = normalColor;
        ResetDisplay();
    }

    IEnumerator FlashError()
    {
        display.text = "<color=red>FALSCH!</color>";
        if (statusBlockRenderer) statusBlockRenderer.material.color = wrongColor;
        if (audioSource && failSound) audioSource.PlayOneShot(failSound);
        
        yield return new WaitForSeconds(1.5f);
        
        if (!isUnlocked)
        {
            if (statusBlockRenderer) statusBlockRenderer.material.color = normalColor;
            currentInput = "";
            UpdateText();
        }
    }

    void UpdateText()
    {
        if (!isUnlocked) display.text = currentInput;
    }

    void ResetDisplay()
    {
        display.text = "CODE...";
        currentInput = "";
    }
}