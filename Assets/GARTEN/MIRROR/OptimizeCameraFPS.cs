using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OptimizeCameraFPS : MonoBehaviour
{
    [Tooltip("Ziel-FPS für diese Kamera (z. B. 30 FPS für flüssige Spiegel ohne Lag)")]
    public int targetFPS = 30;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false; // Automatischen Render-Loop von Unity ausschalten
    }

    private void OnEnable()
    {
        StartCoroutine(RenderLoop());
    }

    private IEnumerator RenderLoop()
    {
        while (true)
        {
            cam.Render(); // Kamera nur manuell im eigenen Rhythmus rendern
            yield return new WaitForSeconds(1f / targetFPS);
        }
    }
}