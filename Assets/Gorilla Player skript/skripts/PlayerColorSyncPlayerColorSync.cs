using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerColorSync : MonoBehaviourPunCallbacks
{
    [Header("Zuweisung")]
    [Tooltip("Zieh hier deinen Gorilla oder die Hände rein (Mesh oder SkinnedMesh)")]
    public Renderer[] allRenderers; // Geändert auf 'Renderer', damit es alles frisst!

    void Start()
    {
        if (photonView.IsMine)
        {
            LoadAndApplySavedColor();
        }
        else
        {
            ApplyRemoteColor();
        }
    }

    void LoadAndApplySavedColor()
    {
        int rVal = PlayerPrefs.GetInt("SavedR", 0);
        int gVal = PlayerPrefs.GetInt("SavedG", 0);
        int bVal = PlayerPrefs.GetInt("SavedB", 0);

        Color newCol = new Color(rVal / 9f, gVal / 9f, bVal / 9f);
        
        ApplyColorToAllParts(newCol);

        if (PhotonNetwork.IsConnectedAndReady)
        {
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Add("R", newCol.r);
            props.Add("G", newCol.g);
            props.Add("B", newCol.b);
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (targetPlayer == photonView.Owner)
        {
            ApplyRemoteColor();
        }
    }

    void ApplyRemoteColor()
    {
        if (photonView.Owner != null && photonView.Owner.CustomProperties.ContainsKey("R"))
        {
            float r = (float)photonView.Owner.CustomProperties["R"];
            float g = (float)photonView.Owner.CustomProperties["G"];
            float b = (float)photonView.Owner.CustomProperties["B"];
            
            ApplyColorToAllParts(new Color(r, g, b));
        }
    }

    void ApplyColorToAllParts(Color col)
    {
        if (allRenderers == null || allRenderers.Length == 0) return;

        foreach (Renderer rend in allRenderers)
        {
            if (rend != null)
            {
                // Das hier färbt jetzt JEDES Material an dem Objekt (auch wenn es 5 sind)
                Material[] mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i].color = col;
                }
                rend.materials = mats;
            }
        }
    }
}