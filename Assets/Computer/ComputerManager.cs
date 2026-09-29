using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Text;

public class ComputerManager : MonoBehaviourPunCallbacks
{
    [Header("Setup")]
    public TextMeshPro screenText;

    [Header("Game Version")]
    public string gameVersion = "v1.0.0";

    [Header("Standardwerte")]
    private string nameInput;
    private string roomInput = "ROOM1";

    // Modi: 0 = Name, 1 = Room
    private int currentMode = 0;
    private bool isDisplayingMessage = false;

    // Design & Farbpalette (One UI OS Theme)
    private string activeCardColor = "#00F0FF";  // High-Tech Cyan (Aktive Karte)
    private string inactiveCardColor = "#2A2E3D";// Dunkles Slate-Grau (Inaktive Karte)
    private string textMainColor = "#FFFFFF";    // Reinweiß für Haupttext
    private string textMutedColor = "#7E8B9B";   // Mattes Grau für Untertexte
    private string accentColor = "#6C5CE7";      // Modernes Violett/Blau für OS-Akzente

    // Statusfarben für Ping (Signal-System)
    private string pingGoodColor = "#00FF66";    // Grün (< 80ms)
    private string pingOkColor = "#FFCC00";      // Gelb (80 - 160ms)
    private string pingBadColor = "#FF3333";     // Rot (> 160ms)

    // Cursor Blink-Effekt
    private float cursorTimer;
    private bool showCursor = true;

    void Start()
    {
        if (screenText == null) return;

        // Standard-Name NUR setzen, wenn noch NIE etwas gespeichert wurde
        if (PlayerPrefs.HasKey("SavedPlayerName"))
        {
            nameInput = PlayerPrefs.GetString("SavedPlayerName");
        }
        else
        {
            nameInput = "GORILLA_1";
            PlayerPrefs.SetString("SavedPlayerName", nameInput);
            PlayerPrefs.Save();
        }

        PhotonNetwork.NickName = nameInput;

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        UpdateScreen();
    }

    void Update()
    {
        // Smooth Cursor Blink & Uhren-Update
        cursorTimer += Time.deltaTime;
        if (cursorTimer >= 0.4f)
        {
            cursorTimer = 0f;
            showCursor = !showCursor;
            if (!isDisplayingMessage) UpdateScreen();
        }
    }

    public void SwitchMode()
    {
        if (isDisplayingMessage) return;

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

            // Flexiblere Prüfung für Lösch-Tasten (Backspace, <-, delete, etc.)
            case "BACKSPACE":
            case "<------":
            case "DELETE":
            case "<-":
            case "<":
                if (currentMode == 0 && nameInput.Length > 0)
                    nameInput = nameInput.Substring(0, nameInput.Length - 1);
                else if (currentMode == 1 && roomInput.Length > 0)
                    roomInput = roomInput.Substring(0, roomInput.Length - 1);
                break;

            default:
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

        // Dynamic Cursors
        string nameCursor = (currentMode == 0 && showCursor) ? "❚" : "";
        string roomCursor = (currentMode == 1 && showCursor) ? "❚" : "";

        // Header-Daten auslesen (Uhrzeit, Ping, Spieler & Raum)
        string timeStr = System.DateTime.Now.ToString("HH:mm");
        int ping = PhotonNetwork.IsConnected ? PhotonNetwork.GetPing() : 0;
        int onlinePlayers = PhotonNetwork.IsConnected ? PhotonNetwork.CountOfPlayers : 0;
        string currentRoomName = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "NONE";

        // Dynamische Farbwahl basierend auf der Latenz (Ping)
        string pingColor = pingBadColor;
        if (ping <= 80 && PhotonNetwork.IsConnected) pingColor = pingGoodColor;
        else if (ping <= 160 && PhotonNetwork.IsConnected) pingColor = pingOkColor;

        string pingDisplay = PhotonNetwork.IsConnected
            ? $"<color={pingColor}>{ping}ms</color>"
            : $"<color={pingBadColor}>OFFLINE</color>";

        // One UI Style Cards
        string nameCard = (currentMode == 0)
            ? $"<color={activeCardColor}>◈ USER IDENTITY</color>\n<color={textMainColor}><b>{nameInput}{nameCursor}</b></color>"
            : $"<color={textMutedColor}>◈ USER IDENTITY</color>\n<color={textMutedColor}>{nameInput}</color>";

        string roomCard = (currentMode == 1)
            ? $"<color={activeCardColor}>❖ TARGET ROOM</color>\n<color={textMainColor}><b>{roomInput}{roomCursor}</b></color>"
            : $"<color={textMutedColor}>❖ TARGET ROOM</color>\n<color={textMutedColor}>{roomInput}</color>";

        // UI-Generierung
        StringBuilder sb = new StringBuilder();

        // Oberste OS Header-Zeile (OS Version, Uhrzeit, Game Version)
        sb.AppendLine($"<size=65%><color={accentColor}>OneUI 1.0 OS</color>  |  {timeStr}  |  <color={textMutedColor}>{gameVersion}</color></size>");

        // Zweite System-Zeile (Raum, Ping in Farbe, Online-Spieler)
        sb.AppendLine($"<size=60%><color={textMutedColor}>ROOM:</color> {currentRoomName}  |  <color={textMutedColor}>PING:</color> {pingDisplay}  |  <color={textMutedColor}>ONLINE:</color> {onlinePlayers}</size>");
        sb.AppendLine($"<color=#1F2330>────────────────────────────────</color>");

        // Haupt-Karten
        sb.AppendLine($"\n{nameCard}\n");
        sb.AppendLine($"{roomCard}\n");

        // Footer & Status-Informationen
        sb.AppendLine($"<color=#1F2330>────────────────────────────────</color>");
        sb.AppendLine($"<size=60%><color={pingColor}>⚠ SYSTEM STATUS:</color> <color={textMutedColor}>All systems operational.</color></size>");
        sb.AppendLine($"<size=60%><color={accentColor}>[SWITCH]</color> <color={textMainColor}>Select Card</color>  |  <color={accentColor}>[ENTER]</color> <color={textMainColor}>Confirm</color></size>");

        screenText.text = sb.ToString();
    }

    void SubmitName()
    {
        if (!string.IsNullOrEmpty(nameInput))
        {
            PhotonNetwork.NickName = nameInput;
            PlayerPrefs.SetString("SavedPlayerName", nameInput);
            PlayerPrefs.Save();
            StartCoroutine(FlashText("✓ PROFILE UPDATED"));
        }
    }

    void JoinRoom()
    {
        if (!string.IsNullOrEmpty(roomInput))
        {
            RoomOptions ro = new RoomOptions { MaxPlayers = 10, IsVisible = true, IsOpen = true };
            PhotonNetwork.JoinOrCreateRoom(roomInput, ro, TypedLobby.Default);
            StartCoroutine(FlashText("CONNECTING TO ROOM..."));
        }
    }

    public override void OnJoinedRoom()
    {
        StartCoroutine(FlashText($"✓ LINKED TO: {PhotonNetwork.CurrentRoom.Name}"));
    }

    IEnumerator FlashText(string msg)
    {
        isDisplayingMessage = true;
        screenText.text = $"\n\n<align=center><size=110%><color={activeCardColor}><b>{msg}</b></color></size></align>\n\n";
        yield return new WaitForSeconds(1.2f);
        isDisplayingMessage = false;
        UpdateScreen();
    }
}