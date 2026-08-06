using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayButtonSound : MonoBehaviour
{
    [Header("Audio Einstellungen")]
    public AudioSource audioSource; // Die AudioSource, die den Ton abspielt
    public AudioClip buttonSound;   // Der Soundeffekt selbst

    public void PlaySound()
    {
        // Prüft, ob eine AudioSource und ein Sound zugewiesen sind
        if (audioSource != null && buttonSound != null)
        {
            // Spielt den Sound einmal sauber ab
            audioSource.PlayOneShot(buttonSound);
        }
    }
}