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
    public class WheelSlot
    {
        public string name = "Rad Slot";
        [Tooltip("Das 3D-Reifenmesh aus der Hierarchy hier reinziehen")]
        public Transform visualMesh;
        [Tooltip("Der dazugehörige Collider aus der Hierarchy hier reinziehen")]
        public Collider wheelCollider;
        public bool isMotorWheel = true;
        public bool isSteerWheel = false;
    }

    [Header("🚗 Fahrzeug-Typ & Profil")]
    [SerializeField] private VehiclePreset vehicleType = VehiclePreset.TwoSeater;

    [Header("⚡ Performance & Physik")]
    [SerializeField] private float accelerationPower = 2500f;
    [SerializeField] private float brakePower = 4000f;
    [SerializeField] private float maxSteerAngle = 35f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.8f, 0f);

    [Header("🪑 Sitze & Türen")]
    [SerializeField] private List<Transform> seats = new List<Transform>();
    [SerializeField] private List<TriggerZone> entryDoors = new List<TriggerZone>();
    [SerializeField] private VRSteeringWheel steeringWheel;

    [Header("🛞 RÄDER IM INSPECTOR MANUELL ZUWEISEN")]
    [Tooltip("Trage hier deine Räder ein und ziehe Mesh + Collider rein")]
    [SerializeField] private List<WheelSlot> wheels = new List<WheelSlot>();

    // Zustände & Referenzen
    private bool isDriving = false;
    private bool isPassenger = false;
    private GameObject playerRig;
    private Rigidbody carRb;
    private Transform currentSeat;

    // Synchronisation
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkSteerAngle;

    private void Awake()
    {
        carRb = GetComponent<Rigidbody>();

        carRb.mass = 2000f;
        carRb.interpolation = RigidbodyInterpolation.Interpolate;
        carRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        ConfigureVehiclePreset();
        InitializeManualWheels();
    }

    private void Start()
    {
        if (carRb != null)
        {
            carRb.centerOfMass = centerOfMassOffset;
        }

        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    private void Update()
    {
        HandleCarEntryExit();

        if (photonView.IsMine && isDriving)
        {
            Drive();
        }
        else if (!photonView.IsMine)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
        }
    }

    private void LateUpdate()
    {
        if (isDriving || isPassenger)
        {
            KeepPlayerInSeat();
        }
    }

    /// <summary>
    /// Richtet die manuell im Inspector reingezogenen Collider an den Mesh-Positionen aus.
    /// </summary>
    private void InitializeManualWheels()
    {
        foreach (var wheel in wheels)
        {
            if (wheel.wheelCollider != null && wheel.visualMesh != null)
            {
                AdjustColliderToMesh(wheel.wheelCollider, wheel.visualMesh);
            }
        }

        Debug.Log("<color=#00FF00> BESTÄTIGUNG: Manuell reingezogene Räder wurden eingerichtet!</color>");
    }

    private void AdjustColliderToMesh(Collider col, Transform targetMesh)
    {
        // Position und Rotation exakt an den Reifen angleichen
        col.transform.position = targetMesh.position;
        col.transform.rotation = targetMesh.rotation;

        // Falls es ein SphereCollider ist, den Radius anhand des Meshes automatisch skalieren
        if (col is SphereCollider sphereCol)
        {
            Renderer meshRenderer = targetMesh.GetComponent<Renderer>();
            if (meshRenderer != null)
            {
                sphereCol.radius = meshRenderer.bounds.extents.y / targetMesh.lossyScale.y;
            }
        }

        // Physik-Material NUR zuweisen, wenn es KEIN WheelCollider ist
        if (!(col is WheelCollider))
        {
            PhysicsMaterial mat = new PhysicsMaterial("WheelGrip")
            {
                dynamicFriction = 1.2f,
                staticFriction = 1.2f,
                bounciness = 0.0f,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };
            col.material = mat;
        }
    }

    private void Drive()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stickInput);

        float forwardInput = stickInput.y;

        if (Mathf.Abs(forwardInput) > 0.1f)
        {
            Vector3 forceDirection = transform.forward * forwardInput * accelerationPower;
            carRb.AddForce(forceDirection, ForceMode.Acceleration);
        }

        float steer = 0f;
        if (steeringWheel != null)
        {
            float normalizedAngle = steeringWheel.currentSteerAngle / steeringWheel.maxSteerAngle;
            steer = normalizedAngle * maxSteerAngle;
        }

        networkSteerAngle = steer;

        if (Mathf.Abs(steer) > 0.05f && carRb.linearVelocity.magnitude > 0.5f)
        {
            transform.Rotate(Vector3.up, steer * Time.deltaTime * 1.5f);
        }

        // Vorderreifen optisch einlenken
        foreach (WheelSlot wheel in wheels)
        {
            if (wheel.isSteerWheel && wheel.visualMesh != null)
            {
                wheel.visualMesh.localRotation = Quaternion.Euler(wheel.visualMesh.localRotation.eulerAngles.x, steer, wheel.visualMesh.localRotation.eulerAngles.z);
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

        if (PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
        {
            bool asDriver = (seatIndex == 0);

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
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(carRb.position);
            stream.SendNext(carRb.rotation);
            stream.SendNext(networkSteerAngle);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkSteerAngle = (float)stream.ReceiveNext();
        }
    }

    private void ConfigureVehiclePreset()
    {
        if (vehicleType == VehiclePreset.Custom) return;

        switch (vehicleType)
        {
            case VehiclePreset.TwoSeater:
                accelerationPower = 2500f;
                brakePower = 4000f;
                centerOfMassOffset = new Vector3(0f, -0.8f, 0f);
                break;
            case VehiclePreset.FourSeater:
                accelerationPower = 3000f;
                brakePower = 4500f;
                centerOfMassOffset = new Vector3(0f, -0.7f, 0f);
                break;
            case VehiclePreset.Truck:
                accelerationPower = 4500f;
                brakePower = 6000f;
                centerOfMassOffset = new Vector3(0f, -0.6f, 0f);
                break;
            case VehiclePreset.Bus:
                accelerationPower = 6000f;
                brakePower = 8000f;
                centerOfMassOffset = new Vector3(0f, -0.5f, 0f);
                break;
        }

        if (carRb != null)
        {
            carRb.centerOfMass = centerOfMassOffset;
        }
    }
}