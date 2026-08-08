using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class VRSteeringWheel : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("🎡 Lenkrad Einstellungen")]
    [Tooltip("Maximale Drehung in Grad nach links und rechts (z. B. 360° oder 450°).")]
    public float maxSteerAngle = 360f;

    [Tooltip("Geschwindigkeit, mit der das Lenkrad in die Ausgangsposition zurückkehrt.")]
    [SerializeField] private float returnSpeed = 5f;

    [Tooltip("Soll sich das Lenkrad automatisch zentrieren, wenn es nicht gegriffen wird?")]
    [SerializeField] private bool autoCenter = true;

    [Header("🔗 Sensor & Interaktion")]
    [SerializeField] private Transform handGrabPoint;

    [HideInInspector] public float currentSteerAngle = 0f;

    private bool isGrabbed = false;
    private Transform grabbingHand;
    private Vector3 initialHandDirection;
    private Quaternion initialWheelRotation;

    // Synchronisations-Variablen für das Netzwerk
    private float networkSteerAngle = 0f;

    private void Start()
    {
        initialWheelRotation = transform.localRotation;
    }

    private void Update()
    {
        if (isGrabbed && grabbingHand != null)
        {
            // Eigener Client: Berechne Lenkwinkel basierend auf Handposition
            CalculateSteeringAngle();
        }
        else if (autoCenter && photonView.IsMine)
        {
            // Eigener Client: Zentriere das Lenkrad, wenn nicht gegriffen
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, 0f, Time.deltaTime * returnSpeed * 100f);
            ApplyRotationToMesh(currentSteerAngle);
        }
        else if (!photonView.IsMine)
        {
            // Remote-Clients: Smooth nachführen
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, networkSteerAngle, Time.deltaTime * 15f);
            ApplyRotationToMesh(currentSteerAngle);
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn der VR-Spieler das Lenkrad greift.
    /// </summary>
    public void OnGrab(Transform handTransform)
    {
        grabbingHand = handTransform;
        isGrabbed = true;

        // Wechsel den Ownership im Netzwerk zum aktuellen Spieler
        if (!photonView.IsMine)
        {
            photonView.RequestOwnership();
        }

        Vector3 handDirection = handTransform.position - transform.position;
        initialHandDirection = transform.InverseTransformDirection(handDirection);
    }

    /// <summary>
    /// Wird aufgerufen, wenn der VR-Spieler das Lenkrad loslässt.
    /// </summary>
    public void OnRelease()
    {
        isGrabbed = false;
        grabbingHand = null;
    }

    private void CalculateSteeringAngle()
    {
        Vector3 currentHandVector = grabbingHand.position - transform.position;
        Vector3 localHandVector = transform.InverseTransformDirection(currentHandVector);

        // Winkel im 2D-Raum der Lenkrad-Ebene berechnen (Z-Achse als Rotationsachse)
        float angleDifference = Vector2.SignedAngle(
            new Vector2(initialHandDirection.x, initialHandDirection.y),
            new Vector2(localHandVector.x, localHandVector.y)
        );

        currentSteerAngle = Mathf.Clamp(currentSteerAngle + angleDifference, -maxSteerAngle, maxSteerAngle);
        initialHandDirection = localHandVector;

        ApplyRotationToMesh(currentSteerAngle);
    }

    private void ApplyRotationToMesh(float angle)
    {
        // Dreht das 3D-Modell um die Z-Achse (entsprechend der lokalen Ausrichtung anpassen falls nötig)
        transform.localRotation = initialWheelRotation * Quaternion.Euler(0f, 0f, -angle);
    }

    #region Photon PUN 2 Synchronisation

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Der Besitzer des Lenkrads sendet den aktuellen Winkel
            stream.SendNext(currentSteerAngle);
        }
        else
        {
            // Alle anderen Empfangen den Winkel über das Netzwerk
            networkSteerAngle = (float)stream.ReceiveNext();
        }
    }

    #endregion
}