using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioSource audioSource;
    public AudioClip moveSound;
    public AudioClip eatSound;

    void Awake()
    {
        Instance = this;
    }

    public void PlayMoveSound()
    {
        if (moveSound != null && audioSource != null)
            audioSource.PlayOneShot(moveSound);
    }

    public void PlayEatSound()
    {
        if (eatSound != null && audioSource != null)
            audioSource.PlayOneShot(eatSound);
    }
}