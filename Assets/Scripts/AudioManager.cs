using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioSource musicAudioSource;
    public AudioSource audioSource;

    public AudioClip enterClip;
    public AudioClip leaveClip;

    public void PlayEnterSound()
    {
        audioSource.clip = enterClip;
        audioSource.Play();
        Debug.Log("Enter sound played");
    }

    public void PlayLeaveSound()
    {
        audioSource.clip = leaveClip;
        audioSource.Play();
        Debug.Log("Leave sound played");
    }
}
