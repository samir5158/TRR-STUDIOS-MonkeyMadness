using UnityEngine;
using TMPro;
using Photon.Pun;
using System.Collections;

public class ColorComputer : MonoBehaviourPunCallbacks
{
    [Header("Anzeige")]
    public TextMeshPro colorScreenText;
    
    [Header("Werte (0-9)")]
    public int rVal = 0;
    public int gVal = 0;
    public int bVal = 0;

    // 0 = Red, 1 = Green, 2 = Blue
    private int colorMode = 0; 

    // HEX-Farben für die Anzeige
    private string redHex = "#FF0000";    // Knallrot
    private string greenHex = "#00FF00";  // Knallgrün
    private string blueHex = "#0000FF";   // Knallblau
    private string inactiveColor = "#333333"; // Dunkelgrau für inaktive Zeilen

    void Start()
    {
        // Gespeicherte Farben laden
        rVal = PlayerPrefs.GetInt("SavedR", 0);
        gVal = PlayerPrefs.GetInt("SavedG", 0);
        bVal = PlayerPrefs.GetInt("SavedB", 0);

        UpdateColorDisplay();
        
        if (PhotonNetwork.InRoom)
        {
            SyncColorToNetwork();
        }
    }

    public override void OnJoinedRoom()
    {
        SyncColorToNetwork();
    }

    public void SetColorValue(string val)
    {
        if (int.TryParse(val, out int digit))
        {
            if (colorMode == 0) rVal = digit;
            else if (colorMode == 1) gVal = digit;
            else if (colorMode == 2) bVal = digit;

            UpdateColorDisplay();
            SyncColorToNetwork();
        }
    }

    public void SwitchColorMode()
    {
        colorMode = (colorMode + 1) % 3;
        UpdateColorDisplay();
    }

    public void SaveColor()
    {
        PlayerPrefs.SetInt("SavedR", rVal);
        PlayerPrefs.SetInt("SavedG", gVal);
        PlayerPrefs.SetInt("SavedB", bVal);
        PlayerPrefs.Save();
        
        SyncColorToNetwork();
        StartCoroutine(FlashColorText("COLOR SAVED!"));
    }

    void UpdateColorDisplay()
    {
        if (colorScreenText == null) return;

        // Bestimmt die Farbe der Zeile: Wenn ausgewählt -> Farbe, wenn nicht -> Grau
        string rCol = (colorMode == 0) ? redHex : inactiveColor;
        string gCol = (colorMode == 1) ? greenHex : inactiveColor;
        string bCol = (colorMode == 2) ? blueHex : inactiveColor;

        colorScreenText.text = "<size=120%>COLOR SETTINGS</size>\n\n" +
                               $"<color={rCol}>RED:   {rVal}</color>\n" +
                               $"<color={gCol}>GREEN: {gVal}</color>\n" +
                               $"<color={bCol}>BLUE:  {bVal}</color>";
    }

    public void SyncColorToNetwork()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;

        float r = rVal / 9f;
        float g = gVal / 9f;
        float b = bVal / 9f;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("R", r);
        props.Add("G", g);
        props.Add("B", b);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    IEnumerator FlashColorText(string msg)
    {
        colorScreenText.text = $"<color=#FFFFFF>{msg}</color>"; // Weißer Flash beim Speichern
        yield return new WaitForSeconds(1.0f);
        UpdateColorDisplay();
    }
}