using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;

public class ComputerManager : MonoBehaviourPunCallbacks
{
    [Header("Setup")]
    public TextMeshPro screenText;
    
    [Header("Einstellungen")]
    private string nameInput = "GORILLA"; 
    private string roomInput = "";
    
    // Modi: 0 = Name, 1 = Room
    private int currentMode = 0; 
    private bool isDisplayingMessage = false;

    private string activeColor = "#00FF00";    // Grün (ausgewählt)
    private string inactiveColor = "#FFFFFF";  // Weiß (nicht ausgewählt)

    void Start() 
    {
        if (screenText == null) return;

        // Gespeicherten Namen laden
        if (PlayerPrefs.HasKey("SavedPlayerName")) 
            nameInput = PlayerPrefs.GetString("SavedPlayerName");

        PhotonNetwork.NickName = nameInput;
        UpdateScreen();
        
        if (!PhotonNetwork.IsConnected) 
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void SwitchMode()
    {
        if (isDisplayingMessage) return;
        
        // Wechselt nur noch zwischen Name (0) und Room (1)
        currentMode = (currentMode + 1) % 2;
        UpdateScreen();
    }

    public void OnKeyPressed(string value)
    {
        if (string.IsNullOrEmpty(value) || isDisplayingMessage) return;

        string val = value.ToUpper();

        switch (val)
        {
            case "ENTER":
                if (currentMode == 0) SubmitName();
                else if (currentMode == 1) JoinRoom();
                break;

            case "BACKSPACE":
                if (currentMode == 0 && nameInput.Length > 0) 
                    nameInput = nameInput.Substring(0, nameInput.Length - 1);
                else if (currentMode == 1 && roomInput.Length > 0) 
                    roomInput = roomInput.Substring(0, roomInput.Length - 1);
                break;

            default:
                // Normales Tippen für Name und Room
                if (val.Length == 1)
                {
                    if (currentMode == 0 && nameInput.Length < 12) nameInput += val;
                    else if (currentMode == 1 && roomInput.Length < 12) roomInput += val;
                }
                break;
        }
        
        if (!isDisplayingMessage) UpdateScreen();
    }

    void UpdateScreen()
    {
        if (screenText == null) return;

        string nCol = (currentMode == 0) ? activeColor : inactiveColor;
        string rmCol = (currentMode == 1) ? activeColor : inactiveColor;

        screenText.text = "SYSTEM SETTINGS\n\n" +
                          $"<color={nCol}>NAME: {nameInput}</color>\n" +
                          $"<color={rmCol}>ROOM: {roomInput}</color>\n\n" +
                          "<color=#aaaaaa>PRESS SWITCH TO CHANGE MODE</color>";
    }

    void SubmitName()
    {
        if (!string.IsNullOrEmpty(nameInput))
        {
            PhotonNetwork.NickName = nameInput;
            PlayerPrefs.SetString("SavedPlayerName", nameInput);
            PlayerPrefs.Save();
            StartCoroutine(FlashText("NAME UPDATED!"));
        }
    }

    void JoinRoom()
    {
        if (!string.IsNullOrEmpty(roomInput))
        {
            RoomOptions ro = new RoomOptions { MaxPlayers = 10, IsVisible = true, IsOpen = true };
            PhotonNetwork.JoinOrCreateRoom(roomInput, ro, TypedLobby.Default);
            StartCoroutine(FlashText("JOINING ROOM..."));
        }
    }

    IEnumerator FlashText(string msg)
    {
        isDisplayingMessage = true;
        screenText.text = $"<color=#00FF00>{msg}</color>";
        yield return new WaitForSeconds(1.5f);
        isDisplayingMessage = false;
        UpdateScreen();
    }
}