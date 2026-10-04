using UnityEngine;

public class MirrorOptimizer : MonoBehaviour
{
    [Header("Referenzen")]
    [Tooltip("Die Kamera, die das Spiegelbild rendert")]
    public Camera mirrorCamera;

    [Tooltip("Das Renderer-Komponent des Spiegels (wo das Spiegel-Material drauf ist)")]
    public Renderer mirrorRenderer;

    [Tooltip("Der Kopf / die VR-Kamera des Spielers")]
    public Transform playerHead;

    [Header("Distanz-Einstellungen")]
    [Tooltip("Ab dieser Distanz fängt der Spiegel an abzudunkeln")]
    public float startFadeDistance = 3.0f;

    [Tooltip("Ab dieser Distanz ist der Spiegel komplett schwarz und schaltet sich ab")]
    public float maxDistance = 7.0f;

    [Header("Farbe / Abdunkelung")]
    [Tooltip("Die Standardfarbe des Spiegels (normalerweise Weiß/Helligkeit 1)")]
    public Color normalColor = Color.white;

    private Material mirrorMaterial;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Für Standard/URP Unlit Shader (oder "_BaseColor")

    void Start()
    {
        if (mirrorRenderer != null)
        {
            mirrorMaterial = mirrorRenderer.material;
        }

        // Falls playerHead nicht zugewiesen ist, automatisch die Main Camera suchen
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }
    }

    void Update()
    {
        if (playerHead == null || mirrorCamera == null || mirrorMaterial == null) return;

        // Distanz zwischen Spieler-Kopf und Spiegel berechnen
        float distance = Vector3.Distance(playerHead.position, transform.position);

        if (distance > maxDistance)
        {
            // Spieler ist zu weit weg: Kamera komplett AUS + Spiegel schwarz
            if (mirrorCamera.enabled)
            {
                mirrorCamera.enabled = false;
            }
            mirrorMaterial.SetColor(ColorProperty, Color.black);
        }
        else
        {
            // Spieler ist nahe genug: Kamera EIN
            if (!mirrorCamera.enabled)
            {
                mirrorCamera.enabled = true;
            }

            if (distance > startFadeDistance)
            {
                // Smooth abdunkeln zwischen startFadeDistance und maxDistance
                float t = (distance - startFadeDistance) / (maxDistance - startFadeDistance);
                Color dimmedColor = Color.Lerp(normalColor, Color.black, t);
                mirrorMaterial.SetColor(ColorProperty, dimmedColor);
            }
            else
            {
                // Nahe genug: volle Helligkeit
                mirrorMaterial.SetColor(ColorProperty, normalColor);
            }
        }
    }
}