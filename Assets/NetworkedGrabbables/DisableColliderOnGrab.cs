using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DisableColliderOnGrab : MonoBehaviour
{
    public Collider colliderToDisable;
    public XRGrabInteractable grabInteractable;

    private void Start()
    {
        // Ensure both the collider and grabInteractable are assigned
        if (colliderToDisable == null || grabInteractable == null)
        {
            Debug.LogError("Collider or XRGrabInteractable not assigned in DisableColliderOnGrab script.");
            return;
        }

        // Subscribe to the OnFirstHoverEntered event
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events to avoid memory leaks
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // Disable the collider when the object is grabbed
        colliderToDisable.enabled = false;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // Enable the collider when the object is released
        if (!args.isCanceled)
        {
            colliderToDisable.enabled = true;
        }
    }
}
