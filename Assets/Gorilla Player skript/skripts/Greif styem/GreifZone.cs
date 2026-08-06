using UnityEngine;

public class ZoneBridge : MonoBehaviour
{
    private GorillaPerfectPickup mainPickup;

    void Start()
    {
        // Holt das Haupt-Skript vom Würfel (Eltern-Objekt)
        mainPickup = GetComponentInParent<GorillaPerfectPickup>();
    }

    // Nutzen wir OnTriggerStay direkt auf der Zone
    private void OnTriggerStay(Collider other)
    {
        if (mainPickup != null)
        {
            // Leitet die Hand direkt an das Hauptskript weiter
            mainPickup.TriggerStayFromChild(other);
        }
    }
}