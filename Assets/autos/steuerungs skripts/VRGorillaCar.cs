using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class VRGorillaCar : MonoBehaviourPunCallbacks, IPunObservable
{
    public enum VehiclePreset
    {
        Custom,
        TwoSeater,
        FourSeater,
        Truck,
        Bus
    }

    [System.Serializable]
    public class WheelPair
    {
        public string name = "Wheel Pair";
        [Tooltip("Ziehe hier dein 3D-Reifen Modell rein.")]
        public Transform visualMesh;
        public bool isMotorWheel = false;
        public bool isSteerWheel = false;

        [HideInInspector] public WheelCollider generatedCollider;
    }

    [Header("🚗 Fahrzeug-Typ & Profil")]
    [SerializeField] private VehiclePreset vehicleType = VehiclePreset.TwoSeater;

    [Header("⚡ Performance & Physik")]
    [SerializeField] private float motorTorque = 1800f;
    [SerializeField] private float brakeTorque = 3000f;
    [SerializeField] private float maxWheelSteerAngle = 35f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    [Header("🪑 Sitze & Türen")]
    [Tooltip("Sitz an Index 0 ist der Fahrersitz.")]
    [SerializeField] private List<Transform> seats = new List<Transform>();
    [SerializeField] private List<TriggerZone> entryDoors = new List<TriggerZone>();
    [SerializeField] private VRSteeringWheel steeringWheel;

    [Header("🛞 Räder-Konfiguration")]
    [SerializeField] private List<WheelPair> wheels = new List<WheelPair>();

    // Zustände & Referenzen
    private bool isDriving = false;
    private bool isPassenger = false;
    private GameObject playerRig;
    private Rigidbody carRb;
    private Transform currentSeat;
    private int currentSeatIndex = -1;

    // Synchronisations-Variablen für Remote Clients
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkSteerAngle;

    private void Awake()
    {
        carRb = GetComponent<Rigidbody>();

        // Rigidbody-Physik absichern
        carRb.mass = 1200f;
        carRb.interpolation = RigidbodyInterpolation.Interpolate;
        carRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        ConfigureVehiclePreset();
        SetupWheelCollidersAutomatically();
        RunSystemCheck();
    }

    private void Start()
    {
        if (carRb != null)
        {
            carRb.centerOfMass = centerOfMassOffset;
        }

        networkPosition = transform.position;
        networkRotation = transform.rotation;

        StartCoroutine(RoutineWheelStatusCheck());
    }

    private void Update()
    {
        // Lokale Eingaben für Ein-/Ausstieg verarbeiten
        HandleCarEntryExit();

        // Wenn dieser Client der Besitzer/Fahrer ist, Physik & Steuerung berechnen
        if (photonView.IsMine && isDriving)
        {
            Drive();
        }
        else if (!photonView.IsMine)
        {
            // Netzwerk-Interpolation für Nicht-Besitzer
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);

            // Rad-Visuals auf Remote-Clients mit synchronisiertem Lenkwinkel aktualisieren
            ApplyRemoteWheelVisuals();
        }
    }

    private void LateUpdate()
    {
        // Sitzposition lokal synchron halten
        if (isDriving || isPassenger)
        {
            KeepPlayerInSeat();
        }
    }

    private void OnValidate()
    {
        ConfigureVehiclePreset();
    }

    #region Fahrzeug-Mechanik & Setup

    private void SetupWheelCollidersAutomatically()
    {
        Transform oldHolder = transform.Find("Generated_WheelColliders");
        if (oldHolder != null)
        {
            if (Application.isPlaying) Destroy(oldHolder.gameObject);
            else DestroyImmediate(oldHolder.gameObject);
        }

        GameObject colliderHolder = new GameObject("Generated_WheelColliders");
        colliderHolder.transform.SetParent(this.transform, false);

        foreach (WheelPair wheel in wheels)
        {
            if (wheel.visualMesh == null)
            {
                Debug.LogError($"[VRGorillaCar] ❌ Reifen-Mesh bei '{wheel.name}' fehlt!");
                continue;
            }

            Collider meshCol = wheel.visualMesh.GetComponent<Collider>();
            if (meshCol != null)
            {
                if (Application.isPlaying) Destroy(meshCol);
                else DestroyImmediate(meshCol);
            }

            GameObject colGo = new GameObject($"Col_{wheel.visualMesh.name}");
            colGo.transform.SetParent(colliderHolder.transform, false);
            colGo.transform.position = wheel.visualMesh.position;
            colGo.transform.rotation = this.transform.rotation;

            WheelCollider wheelCol = colGo.AddComponent<WheelCollider>();

            float finalRadius = 0.35f;
            Renderer meshRenderer = wheel.visualMesh.GetComponent<Renderer>();
            if (meshRenderer != null && meshRenderer.bounds.extents.y > 0.05f)
            {
                finalRadius = meshRenderer.bounds.extents.y;
            }

            wheelCol.radius = finalRadius;
            wheelCol.suspensionDistance = 0.15f;
            wheelCol.mass = 20f;
            wheelCol.center = Vector3.zero;

            JointSpring spring = wheelCol.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4500f;
            spring.targetPosition = 0.5f;
            wheelCol.suspensionSpring = spring;

            WheelFrictionCurve fFriction = wheelCol.forwardFriction;
            fFriction.stiffness = 2f;
            wheelCol.forwardFriction = fFriction;

            WheelFrictionCurve sFriction = wheelCol.sidewaysFriction;
            sFriction.stiffness = 2f;
            wheelCol.sidewaysFriction = sFriction;

            wheel.generatedCollider = wheelCol;
        }
    }

    private void Drive()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stickInput);

        float forwardInput = stickInput.y;
        float appliedTorque = forwardInput * motorTorque;
        float appliedBrake = (Mathf.Abs(forwardInput) < 0.05f) ? brakeTorque : 0f;

        float steerAngle = 0f;
        if (steeringWheel != null)
        {
            float normalizedAngle = steeringWheel.currentSteerAngle / steeringWheel.maxSteerAngle;
            steerAngle = normalizedAngle * maxWheelSteerAngle;
        }

        networkSteerAngle = steerAngle;

        foreach (WheelPair wheel in wheels)
        {
            if (wheel.generatedCollider == null) continue;

            if (wheel.isMotorWheel)
            {
                wheel.generatedCollider.motorTorque = appliedTorque;
                wheel.generatedCollider.brakeTorque = appliedBrake;
            }

            if (wheel.isSteerWheel)
            {
                wheel.generatedCollider.steerAngle = steerAngle;
            }

            UpdateWheelMesh(wheel.generatedCollider, wheel.visualMesh);
        }
    }

    private void UpdateWheelMesh(WheelCollider collider, Transform mesh)
    {
        if (mesh == null || collider == null) return;
        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        mesh.position = position;
        mesh.rotation = rotation;
    }

    private void ApplyRemoteWheelVisuals()
    {
        foreach (WheelPair wheel in wheels)
        {
            if (wheel.visualMesh == null) continue;
            if (wheel.isSteerWheel)
            {
                wheel.visualMesh.localRotation = Quaternion.Euler(wheel.visualMesh.localRotation.eulerAngles.x, networkSteerAngle, wheel.visualMesh.localRotation.eulerAngles.z);
            }
        }
    }

    private void KeepPlayerInSeat()
    {
        if (playerRig != null && currentSeat != null)
        {
            playerRig.transform.position = currentSeat.position;
            playerRig.transform.rotation = currentSeat.rotation;
        }
    }

    #endregion

    #region Photon PUN 2 Multiplayer Steuerung

    private void HandleCarEntryExit()
    {
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (!isDriving && !isPassenger)
        {
            if (rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed)
            {
                for (int i = 0; i < entryDoors.Count; i++)
                {
                    if (entryDoors[i] != null && entryDoors[i].isPlayerInside)
                    {
                        int targetSeatIdx = (i < seats.Count) ? i : 0;
                        photonView.RPC(nameof(RPC_RequestSeatEntry), RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber, targetSeatIdx);
                        break;
                    }
                }
            }
        }
        else
        {
            if (rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool exitPressed) && exitPressed)
            {
                photonView.RPC(nameof(RPC_RequestSeatExit), RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }
    }

    [PunRPC]
    private void RPC_RequestSeatEntry(int actorNumber, int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= seats.Count) return;

        // Wenn der lokale Spieler der Aufrufer ist
        if (PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
        {
            bool asDriver = (seatIndex == 0);

            // Fahrerrechte anfordern (Photon Ownership)
            if (asDriver)
            {
                photonView.RequestOwnership();
                carRb.isKinematic = false;
            }

            playerRig = GameObject.FindWithTag("Player");
            if (playerRig != null)
            {
                Rigidbody playerRb = playerRig.GetComponentInChildren<Rigidbody>();
                if (playerRb != null) playerRb.isKinematic = true;

                currentSeat = seats[seatIndex];
                currentSeatIndex = seatIndex;
                isDriving = asDriver;
                isPassenger = !asDriver;

                playerRig.transform.position = currentSeat.position;
                playerRig.transform.rotation = currentSeat.rotation;
                playerRig.transform.SetParent(currentSeat);
            }
        }
    }

    [PunRPC]
    private void RPC_RequestSeatExit(int actorNumber)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
        {
            if (playerRig != null)
            {
                playerRig.transform.SetParent(null);
                Rigidbody playerRb = playerRig.GetComponentInChildren<Rigidbody>();
                if (playerRb != null) playerRb.isKinematic = false;

                if (currentSeat != null)
                {
                    playerRig.transform.position = currentSeat.position + currentSeat.right * 1.5f;
                }
            }

            isDriving = false;
            isPassenger = false;
            playerRig = null;
            currentSeat = null;
            currentSeatIndex = -1;
        }
    }

    // IPunObservable Implementierung zur Positions- und Datenübertragung
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Besitzer sendet Position, Rotation und Lenkwinkel
            stream.SendNext(carRb.position);
            stream.SendNext(carRb.rotation);
            stream.SendNext(networkSteerAngle);
        }
        else
        {
            // Andere Spieler empfangen die Daten
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkSteerAngle = (float)stream.ReceiveNext();
        }
    }

    #endregion

    #region Diagnostics & Presets

    private void ConfigureVehiclePreset()
    {
        if (vehicleType == VehiclePreset.Custom) return;

        switch (vehicleType)
        {
            case VehiclePreset.TwoSeater:
                motorTorque = 1800f;
                brakeTorque = 3000f;
                centerOfMassOffset = new Vector3(0f, -0.4f, 0f);
                break;
            case VehiclePreset.FourSeater:
                motorTorque = 2200f;
                brakeTorque = 3500f;
                centerOfMassOffset = new Vector3(0f, -0.35f, 0f);
                break;
            case VehiclePreset.Truck:
                motorTorque = 3500f;
                brakeTorque = 5000f;
                centerOfMassOffset = new Vector3(0f, -0.25f, 0f);
                break;
            case VehiclePreset.Bus:
                motorTorque = 4500f;
                brakeTorque = 6500f;
                centerOfMassOffset = new Vector3(0f, -0.15f, 0f);
                break;
        }

        if (carRb != null)
        {
            carRb.centerOfMass = centerOfMassOffset;
        }
    }

    private IEnumerator RoutineWheelStatusCheck()
    {
        WaitForSeconds waitTwoSeconds = new WaitForSeconds(2.0f);

        while (true)
        {
            yield return waitTwoSeconds;

            if (photonView.IsMine)
            {
                for (int i = 0; i < wheels.Count; i++)
                {
                    WheelPair wheel = wheels[i];
                    if (wheel.generatedCollider == null) continue;

                    WheelCollider col = wheel.generatedCollider;
                    Vector3 wheelDown = -col.transform.up;
                    float downDot = Vector3.Dot(wheelDown, Vector3.down);

                    if (downDot < 0.7f)
                    {
                        Debug.LogError($"<color=#FF0000>[PUN VRCar] Reifen [{i}] '{wheel.name}': Ausrichtung prüfen!</color>");
                    }
                }
            }
        }
    }

    private void RunSystemCheck()
    {
        Debug.Log($"[PUN VRCar] Initialisiert für GameObject: {gameObject.name} | ViewID: {photonView.ViewID}");
    }

    #endregion
}