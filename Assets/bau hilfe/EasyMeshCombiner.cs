using UnityEngine;
using UnityEditor;

public class EasyMeshCombiner : MonoBehaviour
{
    // Erstellt einen Menüeintrag oben unter "Tools -> Objekte zusammenfuegen" (Tastenkürzel: Strg + G)
    [MenuItem("Tools/Objekte zusammenfuegen %g")]
    public static void CombineSelectedObjects()
    {
        // Alle im Editor ausgewählten Objekte holen
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length < 2)
        {
            Debug.LogWarning("[Combiner] Bitte wähle mindestens 2 Objekte aus!");
            return;
        }

        // Neues Haupt-Objekt erstellen
        GameObject combinedParent = new GameObject("Kombiniertes_Objekt");
        Undo.RegisterCreatedObjectUndo(combinedParent, "Kombiniere Objekte");

        // Komponenten für das neue Gesamt-Mesh hinzufügen
        MeshFilter parentFilter = combinedParent.AddComponent<MeshFilter>();
        MeshRenderer parentRenderer = combinedParent.AddComponent<MeshRenderer>();

        // Material vom ersten gültigen Renderer übernehmen
        foreach (GameObject obj in selectedObjects)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                parentRenderer.sharedMaterial = mr.sharedMaterial;
                break;
            }
        }

        CombineInstance[] combine = new CombineInstance[selectedObjects.Length];
        int validMeshes = 0;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            MeshFilter mf = selectedObjects[i].GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                combine[validMeshes].mesh = mf.sharedMesh;
                // Verwendet die exakte Welt-Transformationsmatrix
                combine[validMeshes].transform = mf.transform.localToWorldMatrix;
                validMeshes++;
            }
        }

        if (validMeshes == 0)
        {
            Debug.LogError("[Combiner] Keine gültigen MeshFilter auf den ausgewählten Objekten gefunden!");
            DestroyImmediate(combinedParent);
            return;
        }

        // Neues Mesh erstellen und kombinieren
        Mesh finalMesh = new Mesh();
        finalMesh.name = "Combined_Mesh";
        finalMesh.CombineMeshes(combine, true, true);

        // WICHTIG: Licht, Schattierung und Kantenkorrektur neu berechnen
        finalMesh.RecalculateNormals();
        finalMesh.RecalculateBounds();
        finalMesh.RecalculateTangents();

        parentFilter.sharedMesh = finalMesh;

        // MeshCollider hinzufügen, damit Gorillas/Hände solide abprallen
        MeshCollider collider = combinedParent.AddComponent<MeshCollider>();
        collider.sharedMesh = finalMesh;

        // Alte Objekte deaktivieren
        foreach (GameObject obj in selectedObjects)
        {
            Undo.RecordObject(obj, "Objekt deaktivieren");
            obj.SetActive(false);
        }

        // Das neue Objekt direkt in Unity auswählen
        Selection.activeGameObject = combinedParent;
        Debug.Log($"<color=#00FF66>[Combiner] Erfolgreich {validMeshes} Objekte verschmolzen & Schattierungen gefixt!</color>");
    }
}