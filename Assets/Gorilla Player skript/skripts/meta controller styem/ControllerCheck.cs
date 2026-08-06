using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class ControllerCheck : MonoBehaviour
{
    [Header("PC Test Modus (Einfach hier ankreuzen)")]
    public bool pcTestModus = false;

    [Header("Die Hand-Objekte aus der Hierarchy")]
    public GameObject leftHandObject;
    public GameObject rightHandObject;

    [Header("Die VR Brille (Main Camera)")]
    public Transform vrHead;

    [Header("PC-EINSTELLUNGEN LINKS (Inspector-Steuerung)")]
    public Vector3 linkeHandPosition = new Vector3(-0.15f, -0.3f, 0.4f);
    public Vector3 linkeHandRotation = new Vector3(0f, 0f, 0f);

    [Header("PC-EINSTELLUNGEN RECHTS (Inspector-Steuerung)")]
    public Vector3 rechteHandPosition = new Vector3(0.15f, -0.3f, 0.4f);
    public Vector3 rechteHandRotation = new Vector3(0f, 0f, 0f);

    private bool moveLeftToHead = false;
    private bool moveRightToHead = false;

    void Start()
    {
        if (pcTestModus)
        {
            Debug.Log("[PC-TEST] Modus aktiv! Hände werden manuell gesteuert.");
            moveLeftToHead = true;
            moveRightToHead = true;
            DisableHandColliders();
        }
        else
        {
            StartCoroutine(CheckControllersAfterDelay(3f));
        }
    }

    IEnumerator CheckControllersAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        List<InputDevice> leftControllers = new List<InputDevice>();
        List<InputDevice> rightControllers = new List<InputDevice>();

        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left, leftControllers);
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right, rightControllers);

        if (leftControllers.Count == 0)
        {
            moveLeftToHead = true;
            DisableCollider(leftHandObject);
        }

        if (rightControllers.Count == 0)
        {
            moveRightToHead = true;
            DisableCollider(rightHandObject);
        }
    }

    void DisableHandColliders()
    {
        if (leftHandObject != null) DisableCollider(leftHandObject);
        if (rightHandObject != null) DisableCollider(rightHandObject);
    }

    void DisableCollider(GameObject hand)
    {
        Collider[] colliders = hand.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    void Update()
    {
        // Linke Hand Steuerung
        if (moveLeftToHead && leftHandObject != null && vrHead != null)
        {
            leftHandObject.transform.position = vrHead.TransformPoint(linkeHandPosition);
            leftHandObject.transform.rotation = vrHead.rotation * Quaternion.Euler(linkeHandRotation);
        }

        // Rechte Hand Steuerung
        if (moveRightToHead && rightHandObject != null && vrHead != null)
        {
            rightHandObject.transform.position = vrHead.TransformPoint(rechteHandPosition);
            rightHandObject.transform.rotation = vrHead.rotation * Quaternion.Euler(rechteHandRotation);
        }
    }
}