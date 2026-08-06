using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Photon.Pun;

public class XRGrabNetworkInteractable : XRGrabInteractable
{
    private PhotonView photonView;
    private IXRSelectInteractor currentInteractor;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        var interactor = args.interactorObject;

        if (currentInteractor != null && currentInteractor != interactor)
        {
            return;
        }

        photonView.RequestOwnership();
        currentInteractor = interactor;

        base.OnSelectEntered(args);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        if (!args.isCanceled)
        {
            if (currentInteractor == args.interactorObject)
            {
                currentInteractor = null;
            }
        }

        base.OnSelectExited(args);
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        if (currentInteractor != null && !photonView.IsMine)
        {
            interactionManager.SelectExit(
                currentInteractor,
                this
            );

            currentInteractor = null;
        }

        base.ProcessInteractable(updatePhase);
    }
}
