using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class PlayerScoreboard : MonoBehaviourPunCallbacks
{
    public TMP_Text boardText; 

    void Update()
    {
        if (boardText == null) return;

        // Wenn wir im Raum sind -> Spielerliste zeigen
        if (PhotonNetwork.InRoom)
        {
            string liste = "<color=yellow>RAUM: " + PhotonNetwork.CurrentRoom.Name + "</color>\n";
            liste += "SPIELER: " + PhotonNetwork.CurrentRoom.PlayerCount + " / 10\n";
            liste += "--------------------------\n";

            foreach (Player p in PhotonNetwork.PlayerList)
            {
                string n = string.IsNullOrEmpty(p.NickName) ? "Affe " + p.ActorNumber : p.NickName;
                if (p == PhotonNetwork.LocalPlayer)
                    liste += "<color=green> > " + n + " (DU)</color>\n";
                else
                    liste += "   " + n + "\n";
            }
            boardText.text = liste;
        }
        else
        {
            // Wenn wir noch nicht im Raum sind -> Status zeigen
            boardText.text = "<color=red>VERBINDUNG...</color>\nStatus: " + PhotonNetwork.NetworkClientState.ToString();
        }
    }

    // Diese Funktionen sorgen dafür, dass das Board sofort reagiert, wenn jemand kommt/geht
    public override void OnJoinedRoom() { Update(); }
    public override void OnPlayerEnteredRoom(Player newPlayer) { Update(); }
    public override void OnPlayerLeftRoom(Player otherPlayer) { Update(); }
}