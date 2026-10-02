using UnityEngine;

/// <summary>
/// Macht das Terrain rutschig und verhindert das Hochklettern mit Händen und Körper bei steilen Hängen.
/// </summary>
[RequireComponent(typeof(TerrainCollider))]
public class TerrainSlippery : MonoBehaviour
{
    [Header("================ SLIPPERY SETTINGS ================")]
    [Tooltip("Maximale Steigung in Grad, die man hochklettern darf (z. B. 40°).")]
    public float maxClimbableAngle = 40f;

    [Tooltip("Stärke des Rutsch-Impulses nach unten.")]
    public float slideForce = 15f;

    [Tooltip("Impuls, der die Hände bei zu steiler Wand weggestoßen lässt.")]
    public float handPushbackForce = 10f;

    [Header("================ LAYER FILTERING ================")]
    [Tooltip("Wähle hier die Layer 'LeftHand' und 'RightHand' aus.")]
    public LayerMask handLayers;

    private PhysicsMaterial slipperyMaterial;

    private void Awake()
    {
        // Dynamisches Physikalisches Material ohne Reibung erstellen
        slipperyMaterial = new PhysicsMaterial("GorillaSlipperyTerrain")
        {
            dynamicFriction = 0.0f,
            staticFriction = 0.0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        TerrainCollider terrainCollider = GetComponent<TerrainCollider>();
        if (terrainCollider != null)
        {
            terrainCollider.material = slipperyMaterial;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Prüfen, ob eine Hand das Terrain berührt
        bool isHand = ((1 << collision.gameObject.layer) & handLayers) != 0;

        foreach (ContactPoint contact in collision.contacts)
        {
            // Winkel der Oberfläche berechnen
            float slopeAngle = Vector3.Angle(contact.normal, Vector3.up);

            if (slopeAngle > maxClimbableAngle)
            {
                // 1. Wenn die Hand versucht festzuhalten -> Hand mit Kraft abstoßen
                if (isHand)
                {
                    Rigidbody handRb = collision.rigidbody;
                    if (handRb != null)
                    {
                        // Stößt die Hand von der Wand nach außen/unten weg
                        Vector3 pushDirection = (contact.normal + Vector3.down).normalized;
                        handRb.AddForce(pushDirection * handPushbackForce, ForceMode.Impulse);
                    }
                }

                // 2. Den Spieler-Körper am Hang nach unten rutschen lassen
                Rigidbody playerRb = collision.rigidbody;
                if (playerRb != null)
                {
                    Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, contact.normal).normalized;
                    playerRb.AddForce(slideDirection * slideForce, ForceMode.Acceleration);
                }
            }
        }
    }
}