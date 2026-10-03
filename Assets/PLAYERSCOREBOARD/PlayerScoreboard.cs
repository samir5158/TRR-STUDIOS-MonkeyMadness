using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Text;

/// <summary>
/// Performance-Scoreboard mit Neon-Hellblau für den eigenen Spieler.
/// </summary>
public class PlayerScoreboard : MonoBehaviourPunCallbacks
{
    [Header("================ UI SETTINGS ================")]
    [Tooltip("TextMeshPro Komponente auf dem Scoreboard-Board.")]
    public TMP_Text boardText;

    private StringBuilder sb = new StringBuilder();

    private void Start()
    {
        UpdateScoreboard();
    }

    /// <summary>
    /// Baut die Spielerliste auf.
    /// </summary>
    public void UpdateScoreboard()
    {
        if (boardText == null) return;

        sb.Clear();

        // 1. Wenn der Spieler in einem Raum ist
        if (PhotonNetwork.InRoom)
        {
            Room currentRoom = PhotonNetwork.CurrentRoom;

            // HEADER (Dunkelrot)
            sb.AppendLine("<color=#8B0000><b>════════════════════════════</b></color>");
            sb.AppendLine($"<color=#8B0000><b>RAUM:</b></color> <color=#FFFFFF>{currentRoom.Name.ToUpper()}</color>");
            sb.AppendLine($"<color=#8B0000><b>SPIELER:</b></color> <color=#FFFFFF>{currentRoom.PlayerCount} / {currentRoom.MaxPlayers}</color>");
            sb.AppendLine("<color=#8B0000><b>════════════════════════════</b></color>");
            sb.AppendLine();

            // LISTE ALLER SPIELER
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                string name = string.IsNullOrEmpty(p.NickName) ? $"AFFE #{p.ActorNumber}" : p.NickName.ToUpper();

                // Host-Markierung in Orange
                string hostTag = p.IsMasterClient ? " <color=#FF8C00>[HOST]</color>" : "";

                if (p == PhotonNetwork.LocalPlayer)
                {
                    // Du selbst (Leuchtendes Neon-Hellblau)
                    sb.AppendLine($"<color=#00E5FF>► <b>{name}</b> (DU)</color>{hostTag}");
                }
                else
                {
                    // Andere normale Spieler (Sauberes Weiß)
                    sb.AppendLine($"  <color=#FFFFFF>{name}</color>{hostTag}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("<color=#8B0000><b>════════════════════════════</b></color>");
        }
        else
        {
            // 2. Nicht im Raum
            sb.AppendLine("<color=#8B0000><b>════════════════════════════</b></color>");
            sb.AppendLine("<color=#8B0000><b>STATUS: NICHT VERBUNDEN</b></color>");
            sb.AppendLine("<color=#8B0000><b>════════════════════════════</b></color>");
            sb.AppendLine();
            sb.AppendLine($"<color=#8B0000>STATUS:</color> <color=#FFFFFF>{PhotonNetwork.NetworkClientState}</color>");
        }

        boardText.text = sb.ToString();
    }

    // ================= PHOTON CALLBACKS =================

    public override void OnJoinedRoom() { UpdateScoreboard(); }
    public override void OnLeftRoom() { UpdateScoreboard(); }
    public override void OnPlayerEnteredRoom(Player newPlayer) { UpdateScoreboard(); }
    public override void OnPlayerLeftRoom(Player otherPlayer) { UpdateScoreboard(); }
    public override void OnMasterClientSwitched(Player newMasterClient) { UpdateScoreboard(); }
    public override void OnConnectedToMaster() { UpdateScoreboard(); }
    public override void OnDisconnected(DisconnectCause cause) { UpdateScoreboard(); }
}