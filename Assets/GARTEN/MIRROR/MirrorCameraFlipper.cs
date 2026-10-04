using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MirrorCameraFlipper : MonoBehaviour
{
    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void OnPreCull()
    {
        // Projektionsmatrix zurücksetzen und horizontal spiegeln
        cam.ResetProjectionMatrix();
        Matrix4x4 mat = cam.projectionMatrix;
        mat *= Matrix4x4.Scale(new Vector3(-1, 1, 1));
        cam.projectionMatrix = mat;

        // Winding Order umkehren, damit Flächen nicht falsch herum gerendert werden
        GL.invertCulling = true;
    }

    void OnPostRender()
    {
        // Culling für den Rest des Frames wieder auf normal zurücksetzen
        GL.invertCulling = false;
    }
}