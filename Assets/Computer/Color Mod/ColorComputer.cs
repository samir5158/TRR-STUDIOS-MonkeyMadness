using UnityEngine;
using TMPro;
using Photon.Pun;
using System.Collections;
using System.Text;

public class ColorComputer : MonoBehaviourPunCallbacks
{
    [Header("Anzeige & Setup")]
    public TextMeshPro colorScreenText;

    [Header("Game Version")]
    public string gameVersion = "v1.0.0";

    [Header("Werte (0-9)")]
    public int rVal = 0;
    public int gVal = 0;
    public int bVal = 0;

    // 0 = Red, 1 = Green, 2 = Blue
    private int colorMode = 0;
    private bool isDisplayingMessage = false;

    // One UI & High-Tech Neon Farbpalette
    private string redHex = "#FF3366";
    private string greenHex = "#00FF66";
    private string blueHex = "#00F0FF";
    private string inactiveColor = "#2A2E3D";
    private string textMainColor = "#FFFFFF";
    private string textMutedColor = "#7E8B9B";
    private string accentColor = "#6C5CE7";

    // Cursor Blink-Effekt
    private float cursorTimer;
    private bool showCursor = true;

    void Start()
    {
        if (colorScreenText == null) return;

        rVal = PlayerPrefs.GetInt("SavedR", 0);
        gVal = PlayerPrefs.GetInt("SavedG", 0);
        bVal = PlayerPrefs.GetInt("SavedB", 0);

        UpdateColorDisplay();
        ApplyColorToLocalPlayer();

        if (PhotonNetwork.InRoom)
        {
            SyncColorToNetwork();
        }
    }

    void Update()
    {
        cursorTimer += Time.deltaTime;
        if (cursorTimer >= 0.4f)
        {
            cursorTimer = 0f;
            showCursor = !showCursor;
            if (!isDisplayingMessage) UpdateColorDisplay();
        }
    }

    public override void OnJoinedRoom()
    {
        SyncColorToNetwork();
    }

    public void SetColorValue(string val)
    {
        if (isDisplayingMessage) return;

        if (int.TryParse(val, out int digit))
        {
            if (colorMode == 0) rVal = Mathf.Clamp(digit, 0, 9);
            else if (colorMode == 1) gVal = Mathf.Clamp(digit, 0, 9);
            else if (colorMode == 2) bVal = Mathf.Clamp(digit, 0, 9);

            UpdateColorDisplay();
            ApplyColorToLocalPlayer();
            SyncColorToNetwork();
        }
    }

    public void SwitchColorMode()
    {
        if (isDisplayingMessage) return;

        colorMode = (colorMode + 1) % 3;
        UpdateColorDisplay();
    }

    public void SaveColor()
    {
        if (isDisplayingMessage) return;

        PlayerPrefs.SetInt("SavedR", rVal);
        PlayerPrefs.SetInt("SavedG", gVal);
        PlayerPrefs.SetInt("SavedB", bVal);
        PlayerPrefs.Save();

        ApplyColorToLocalPlayer();
        SyncColorToNetwork();
        StartCoroutine(FlashColorText("✓ COLOR CONFIG SAVED"));
    }

    void UpdateColorDisplay()
    {
        if (colorScreenText == null) return;

        string rCursor = (colorMode == 0 && showCursor) ? "❚" : "";
        string gCursor = (colorMode == 1 && showCursor) ? "❚" : "";
        string bCursor = (colorMode == 2 && showCursor) ? "❚" : "";

        int rPct = Mathf.RoundToInt((rVal / 9f) * 100);
        int gPct = Mathf.RoundToInt((gVal / 9f) * 100);
        int bPct = Mathf.RoundToInt((bVal / 9f) * 100);

        string rLine = (colorMode == 0)
            ? $"<color={redHex}><b>> RED:   [{rVal}/9]  ({rPct}%){rCursor}</b></color>"
            : $"<color={textMutedColor}>  RED:   [{rVal}/9]  ({rPct}%)</color>";

        string gLine = (colorMode == 1)
            ? $"<color={greenHex}><b>> GREEN: [{gVal}/9]  ({gPct}%){gCursor}</b></color>"
            : $"<color={textMutedColor}>  GREEN: [{gVal}/9]  ({gPct}%)</color>";

        string bLine = (colorMode == 2)
            ? $"<color={blueHex}><b>> BLUE:  [{bVal}/9]  ({bPct}%){bCursor}</b></color>"
            : $"<color={textMutedColor}>  BLUE:  [{bVal}/9]  ({bPct}%)</color>";

        Color currentGorillaColor = new Color(rVal / 9f, gVal / 9f, bVal / 9f);
        string currentPreviewHex = ColorUtility.ToHtmlStringRGB(currentGorillaColor);

        string timeStr = System.DateTime.Now.ToString("HH:mm");

        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"<size=65%><color={accentColor}>OneUI 1.0 OS</color>  |  {timeStr}  |  <color={textMutedColor}>{gameVersion}</color></size>");
        sb.AppendLine($"<color=#1F2330>────────────────────────────────</color>");

        sb.AppendLine($"<size=90%><color={textMainColor}><b>CHROMATIC MATRIX</b></color></size>");
        sb.AppendLine($"PREVIEW: <color=#{currentPreviewHex}><b>█████████████</b></color>\n");

        sb.AppendLine(rLine);
        sb.AppendLine(gLine);
        sb.AppendLine(bLine);

        sb.AppendLine($"\n<color=#1F2330>────────────────────────────────</color>");
        sb.AppendLine($"<size=60%><color={accentColor}>[SWITCH]</color> <color={textMainColor}>Select Axis</color>  |  <color={accentColor}>[ENTER/SAVE]</color> <color={textMainColor}>Sync Avatar</color></size>");

        colorScreenText.text = sb.ToString();
    }

    private void ApplyColorToLocalPlayer()
    {
        Color gorillaColor = new Color(rVal / 9f, gVal / 9f, bVal / 9f);

        PlayerPrefs.SetFloat("Red", gorillaColor.r);
        PlayerPrefs.SetFloat("Green", gorillaColor.g);
        PlayerPrefs.SetFloat("Blue", gorillaColor.b);
        PlayerPrefs.Save();
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
        isDisplayingMessage = true;
        colorScreenText.text = $"\n\n<align=center><size=110%><color={greenHex}><b>{msg}</b></color></size></align>\n\n";
        yield return new WaitForSeconds(1.2f);
        isDisplayingMessage = false;
        UpdateColorDisplay();
    }
}