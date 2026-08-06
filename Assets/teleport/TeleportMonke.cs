using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GorillaLocomotion;

public class TeleportMonke : MonoBehaviour
{
    [Header("THIS NEEDS TO BE CALLED BY A SCRIPT OR AN EVENT")]
    public Transform newPos;
    public Transform gorillaPos;
    private Player gorillaMovement;
    void Start()
    {
        gorillaPos.gameObject.TryGetComponent<Player>(out gorillaMovement);
    }
    public void Teleport ()  {
        if (gorillaMovement != null)
        {
            gorillaMovement.locomotionEnabledLayers = 0;

            gorillaPos.position = newPos.position;

            StartCoroutine(Delay());
        }
    }

    IEnumerator Delay()
    {
        yield return new WaitForSeconds(1);
        
        gorillaMovement.locomotionEnabledLayers = 1;
    }
}