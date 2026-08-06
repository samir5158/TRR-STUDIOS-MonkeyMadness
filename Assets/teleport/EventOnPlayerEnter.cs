using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EventOnPlayerEnter : MonoBehaviour
{
    public enum s
    {
        Player,
        Cam
    }

    public s tag;
    public UnityEvent events;
    void OnTriggerEnter(Collider col)
    {
        if (tag == s.Player)
        {
            if (col.gameObject.tag == "Player") {
                events.Invoke();
            }
        }
        if (tag == s.Cam)
        {
            if (col.gameObject.tag == "MainCamera") {
                events.Invoke();
            }
        }
    }

    public void Event()
    {
       events.Invoke();   
    }
}