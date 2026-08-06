using UnityEngine;
using System.Collections.Generic;

public class PickupManager : MonoBehaviour
{
    public static PickupManager Instance;

    [Header("🎯 Globale Zuweisung der Hand-Kugeln")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("⚙️ Collider-Einstellungen beim Tragen")]
    [Tooltip("Wähle hier einen Layer, der KEINE Kollision mit Wänden/Boden hat (z.B. Ignore Raycast oder ein eigener Hand-Layer)")]
    public string transparentLayerName = "Ignore Raycast";
    
    private int originalLayer;
    private int transparentLayer;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        transparentLayer = LayerMask.NameToLayer(transparentLayerName);
    }

    // Setzt das Objekt sicher an die Hand, ohne die Trigger-Zone zu zerstören
    public void SetObjectToHand(Transform objTransform, Collider solidCollider, bool isLeft, Vector3 offset, Vector3 rotationOffset, Transform fallbackHand)
    {
        Transform target = isLeft ? leftHandTarget : rightHandTarget;
        if (target == null) target = fallbackHand; // Falls im Manager nichts zugewiesen ist

        if (target != null)
        {
            objTransform.SetParent(target);
            objTransform.localPosition = offset;
            objTransform.localEulerAngles = rotationOffset;

            // Ändert nur den Physik-Layer, damit man beim Laufen nicht hängen bleibt!
            if (solidCollider != null)
            {
                originalLayer = solidCollider.gameObject.layer;
                solidCollider.gameObject.layer = transparentLayer;
            }
        }
    }

    // Setzt das Objekt wieder zurück in die Welt
    public void ReleaseObject(Transform objTransform, Collider solidCollider)
    {
        objTransform.SetParent(null);
        
        if (solidCollider != null)
        {
            solidCollider.gameObject.layer = originalLayer; // Alten Layer für normale Physik wiederherstellen
        }
    }
}