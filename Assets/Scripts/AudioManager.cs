using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioSource musicAudioSource;
    public AudioSource audioSource;

    public AudioClip enterClip;
    public AudioClip leaveClip;

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<ClusterEnterEvent>(PlayEnterSound);
        EventBus.Instance.Subscribe<ClusterExitEvent>(PlayLeaveSound);
    }

    public void PlayEnterSound(ClusterEnterEvent @event)
    {
        audioSource.clip = enterClip;
        audioSource.PlayOneShot(enterClip);
    }

    public void PlayLeaveSound(ClusterExitEvent @event)
    {
        audioSource.clip = leaveClip;
        audioSource.PlayOneShot(leaveClip);
    }
}
