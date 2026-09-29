using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SnapCarToGround : MonoBehaviour
{
    [Header("🎯 Boden-Platzierung")]
    [Tooltip("Welche Layer als Boden erkannt werden sollen (z.B. Default, Terrain, Ground).")]
    [SerializeField] private LayerMask groundLayer = ~0; // Standard: Alle Layer

    [Tooltip("Maximale Distanz nach unten, in der nach Boden gesucht wird.")]
    [SerializeField] private float maxRaycastDistance = 50f;

    [Tooltip("Zusätzlicher Höhen-Offset (falls das Auto etwas zu tief oder hoch sitzt).")]
    [SerializeField] private float heightOffset = 0.05f;

    [Header("🔍 Diagnose")]
    [SerializeField] private bool snapOnStart = true;

    private void Start()
    {
        if (snapOnStart)
        {
            SnapToGround();
        }
    }

    [ContextMenu("Auto jetzt auf Boden setzen")]
    public void SnapToGround()
    {
        // Raycast von der Mitte des Autos nach unten schießen
        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f; // Startet 1m über dem Auto-Pivot

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxRaycastDistance, groundLayer))
        {
            // Berechne die neue Position direkt auf der Trefferfläche
            Vector3 newPosition = transform.position;
            newPosition.y = hit.point.y + heightOffset;

            transform.position = newPosition;

            // Rigidbody-Geschwindigkeit zurücksetzen, damit das Auto ruhig aufsitzt
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Debug.Log($"<color=#00FF00>[SnapToGround] 🎯 SUCCESS: '{gameObject.name}' wurde erfolgreich auf Boden '{hit.collider.gameObject.name}' (Höhe: {hit.point.y:F2}m) platziert!</color>");
        }
        else
        {
            Debug.LogError($"<color=#FF0000>[SnapToGround] ❌ FEHLER: Kein Boden-Collider unter '{gameObject.name}' innerhalb von {maxRaycastDistance}m gefunden! Prüfe, ob der Boden einen Collider hat.</color>");
        }
    }
}