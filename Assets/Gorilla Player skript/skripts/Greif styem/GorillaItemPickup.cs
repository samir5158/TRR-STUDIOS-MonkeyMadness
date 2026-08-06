using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))] 
public class GorillaPerfectPickup : MonoBehaviourPun
{
    // Statische Liste aller aktiven Blöcke im Spiel (Verhindert das Wegbuggen im Code)
    private static List<GorillaPerfectPickup> allActiveBlocks = new List<GorillaPerfectPickup>();

    private Rigidbody rb;
    private bool isHeld = false;
    private Transform currentHand = null;
    private UnityEngine.XR.XRNode activeNode;
    public Collider solidCollider; // Auf public gesetzt für den internen Abgleich
    private AudioSource audioSource; 

    private bool wasGripPressed = false;
    private int originalLayer;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 handVelocity;
    private Vector3 handAngularVelocity;

    [Header("🎵 Sounds (Nutzt die AudioSource des Blocks)")]
    [Tooltip("Zieh hier deinen Sound für das Aufheben rein!")]
    public AudioClip pickupSound;
    [Tooltip("Zieh hier deinen Sound für das Ablegen/Werfen rein!")]
    public AudioClip dropSound;

    [Header("💅 Position & Drehung in der Hand")]
    public Vector3 handOffset = Vector3.zero;
    public Vector3 handRotationOffset = Vector3.zero;

    [Header("🚀 Wurf-Stärke")]
    [Tooltip("Stell diesen Wert höher, wenn der Würfel noch weiter fliegen soll!")]
    public float throwForceMultiplier = 1.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        solidCollider = GetComponent<Collider>(); 
        audioSource = GetComponent<AudioSource>(); 
        
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        originalLayer = gameObject.layer;

        // Block in der globalen Liste registrieren
        allActiveBlocks.Add(this);

        // Schaltet sofort die Kollision mit bereits existierenden Blöcken aus
        IgnoreOtherBlocks();
    }

    void OnDestroy()
    {
        // Löscht den Block aus der Liste, wenn er zerstört wird
        if (allActiveBlocks.Contains(this))
        {
            allActiveBlocks.Remove(this);
        }
    }

    void FixedUpdate()
    {
        if (isHeld && currentHand != null)
        {
            handVelocity = (currentHand.position - lastPosition) / Time.fixedDeltaTime;
            
            Quaternion deltaRotation = currentHand.rotation * Quaternion.Inverse(lastRotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
            handAngularVelocity = (axis * (angle * Mathf.Deg2Rad)) / Time.fixedDeltaTime;

            lastPosition = currentHand.position;
            lastRotation = currentHand.rotation;

            // FIX: Werte werden nur zurückgesetzt, falls der Rigidbody NICHT kinematisch ist.
            // Da er beim Halten kinematisch ist, bewegen wir ihn rein über die Transform-Komponente darunter.
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            transform.position = currentHand.position + currentHand.TransformDirection(handOffset);
            transform.rotation = currentHand.rotation * Quaternion.Euler(handRotationOffset);
        }
    }

    void Update()
    {
        if (isHeld && currentHand != null)
        {
            if (CheckGripDown(activeNode))
            {
                ExecuteDrop();
            }
        }
    }

    public void TriggerStayFromChild(Collider other)
    {
        if (isHeld) return;

        if (other.CompareTag("HandTag"))
        {
            bool isLeft = other.gameObject.name.ToLower().Contains("left");
            UnityEngine.XR.XRNode node = isLeft ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand;

            if (CheckGripDown(node))
            {
                Transform targetSphere = other.transform;
                ExecutePickUp(other, node, targetSphere);
            }
        }
    }

    private bool CheckGripDown(UnityEngine.XR.XRNode node)
    {
        var devices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(node, devices);
        
        if (devices.Count > 0)
        {
            var device = devices[0];
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool isGripCurrentlyPressed))
            {
                if (isGripCurrentlyPressed && !wasGripPressed)
                {
                    wasGripPressed = true; 
                    return true;
                }
                if (!isGripCurrentlyPressed)
                {
                    wasGripPressed = false; 
                }
            }
        }
        return false;
    }

    private void ExecutePickUp(Collider handCollider, UnityEngine.XR.XRNode node, Transform targetSphere)
    {
        currentHand = targetSphere;
        activeNode = node;

        if (solidCollider != null && handCollider != null)
        {
            Physics.IgnoreCollision(solidCollider, handCollider, true);
        }

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RequestOwnership();
            bool isLeft = (node == UnityEngine.XR.XRNode.LeftHand);
            photonView.RPC("NetworkPickUp", RpcTarget.All, isLeft);
        }
        else
        {
            LocalPickUpSetup(targetSphere);
        }
    }

    private void ExecuteDrop()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("NetworkDrop", RpcTarget.All, handVelocity, handAngularVelocity);
        }
        else
        {
            LocalDropSetup(handVelocity, handAngularVelocity);
        }
    }

    private void LocalPickUpSetup(Transform targetSphere)
    {
        isHeld = true;

        rb.isKinematic = true; 
        rb.useGravity = false; 
        rb.detectCollisions = false; 

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        transform.SetParent(targetSphere);
        transform.localPosition = handOffset;
        transform.localEulerAngles = handRotationOffset;

        lastPosition = targetSphere.position;
        lastRotation = targetSphere.rotation;

        if (audioSource != null && pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }
    }

    private void LocalDropSetup(Vector3 throwVel, Vector3 throwAngVel)
    {
        isHeld = false;

        transform.SetParent(null); 
        
        rb.isKinematic = false; 
        rb.useGravity = true;
        rb.detectCollisions = true; 

        // Da isKinematic jetzt FALSE ist, können wir die Wurfgeschwindigkeit fehlerfrei übergeben!
        rb.linearVelocity = throwVel * throwForceMultiplier;
        rb.angularVelocity = throwAngVel;

        if (audioSource != null && dropSound != null)
        {
            audioSource.PlayOneShot(dropSound);
        }

        currentHand = null;
        gameObject.layer = originalLayer;

        GameObject leftHand = GameObject.Find("LeftHand Controller");
        GameObject rightHand = GameObject.Find("RightHand Controller");
        
        if (leftHand != null && leftHand.GetComponent<Collider>() != null && solidCollider != null) 
            Physics.IgnoreCollision(solidCollider, leftHand.GetComponent<Collider>(), false);
        if (rightHand != null && rightHand.GetComponent<Collider>() != null && solidCollider != null) 
            Physics.IgnoreCollision(solidCollider, rightHand.GetComponent<Collider>(), false);

        // Stellt sicher, dass er sich auch nach dem Werfen nicht mit neu dazugekommenen Blöcken beißt
        IgnoreOtherBlocks();
    }

    private void IgnoreOtherBlocks()
    {
        for (int i = 0; i < allActiveBlocks.Count; i++)
        {
            GorillaPerfectPickup block = allActiveBlocks[i];
            if (block != null && block != this && block.solidCollider != null && this.solidCollider != null)
            {
                Physics.IgnoreCollision(this.solidCollider, block.solidCollider, true);
            }
        }
    }

    [PunRPC] 
    void NetworkPickUp(bool isLeft)
    {
        string targetHandName = isLeft ? "LeftHand" : "RightHand";
        GameObject handObj = GameObject.Find(targetHandName);
        if (handObj == null) handObj = GameObject.Find(isLeft ? "LeftHand Controller" : "RightHand Controller");

        if (handObj != null)
        {
            Collider handCollider = handObj.GetComponent<Collider>();
            if (solidCollider != null && handCollider != null)
            {
                Physics.IgnoreCollision(solidCollider, handCollider, true);
            }
            LocalPickUpSetup(handObj.transform);
        }
    }

    [PunRPC]
    void NetworkDrop(Vector3 throwVel, Vector3 throwAngVel)
    {
        LocalDropSetup(throwVel, throwAngVel);
    }
}