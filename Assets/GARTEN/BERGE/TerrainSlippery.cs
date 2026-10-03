using UnityEngine;

/// <summary>
/// Macht das Terrain rutschig und verhindert das Hochklettern mit Händen, Sphere-Collidern (Bällen) 
/// und Körper bei steilen Hängen.
/// </summary>
[RequireComponent(typeof(TerrainCollider))]
public class TerrainSlippery : MonoBehaviour
{
    [Header("================ SLIPPERY SETTINGS ================")]
    [Tooltip("Maximale Steigung in Grad, die man hochklettern darf (z. B. 40°).")]
    public float maxClimbableAngle = 40f;

    [Tooltip("Stärke des Rutsch-Impulses nach unten.")]
    public float slideForce = 15f;

    [Tooltip("Impuls, der die Hände/Bälle bei zu steiler Wand weggestoßen lässt.")]
    public float handPushbackForce = 8f;

    [Header("================ HAND ASSIGNMENT ================")]
    [Tooltip("Ziehe hier deine linke Hand (oder das Kugel-Objekt der linken Hand) hinein.")]
    public GameObject leftHandObject;

    [Tooltip("Ziehe hier deine rechte Hand (oder das Kugel-Objekt der rechten Hand) hinein.")]
    public GameObject rightHandObject;

    [Header("================ LAYER FILTERING ================")]
    [Tooltip("Wähle hier die Layer für Hand / Hand-Spheres / Player aus.")]
    public LayerMask handLayers;

    private PhysicsMaterial slipperyMaterial;

    private void Awake()
    {
        // Dynamisches Physikalisches Material ohne Reibung erstellen
        slipperyMaterial = new PhysicsMaterial("GorillaSlipperyTerrain")
        {
            dynamicFriction = 0.0f,
            staticFriction = 0.0f,
            bounciness = 0.0f,
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
        // Prüfen, ob das getroffene Objekt oder dessen Parent mit der linken/rechten Hand übereinstimmt
        bool isHand = IsHandOrChild(collision.gameObject);

        // Falls die Zuordnung über Layer genutzt wird
        if (!isHand && handLayers != 0)
        {
            isHand = ((1 << collision.gameObject.layer) & handLayers) != 0;
        }

        // Durch alle Kontaktpunkte iterieren
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            // Winkel der Oberfläche berechnen (0° = flach, 90° = steil)
            float slopeAngle = Vector3.Angle(contact.normal, Vector3.up);

            if (slopeAngle > maxClimbableAngle)
            {
                // 1. Wenn eine Hand oder eine Hand-Kugel die steile Wand berührt -> Wegstoßen
                if (isHand)
                {
                    Rigidbody handRb = collision.rigidbody != null ? collision.rigidbody : collision.gameObject.GetComponentInParent<Rigidbody>();
                    if (handRb != null)
                    {
                        Vector3 pushDirection = (contact.normal + Vector3.down).normalized;
                        handRb.AddForce(pushDirection * handPushbackForce, ForceMode.Impulse);
                    }
                }

                // 2. Den Spieler-Körper am Hang nach unten rutschen lassen
                Rigidbody playerRb = collision.rigidbody != null ? collision.rigidbody : collision.gameObject.GetComponentInParent<Rigidbody>();
                if (playerRb != null)
                {
                    Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, contact.normal).normalized;
                    playerRb.AddForce(slideDirection * slideForce, ForceMode.Acceleration);
                }

                break;
            }
        }
    }

    /// <summary>
    /// Prüft, ob das Objekt selbst oder ein übergeordnetes Objekt die zugewiesene Hand ist.
    /// </summary>
    private bool IsHandOrChild(GameObject obj)
    {
        if (leftHandObject != null && (obj == leftHandObject || obj.transform.IsChildOf(leftHandObject.transform)))
        {
            return true;
        }

        if (rightHandObject != null && (obj == rightHandObject || obj.transform.IsChildOf(rightHandObject.transform)))
        {
            return true;
        }

        return false;
    }
}