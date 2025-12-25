using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public WorldStateManager _stateManager;
    public AudioSource musicAudioSource;
    public AudioSource audioSource;

    public AudioClip enterClip;
    public AudioClip leaveClip;

    public AudioClip waveStartClip;
    public AudioClip waveEndClip;

    void OnEnable()
    {
        EventBus.Instance.Subscribe<ClusterTransitionEvent>(HandleCluster);

        EventBus.Instance.Subscribe<AnimaWaveStartedEvent>(_ =>
            audioSource.PlayOneShot(waveStartClip));

        EventBus.Instance.Subscribe<AnimaWaveClearedEvent>(_ =>
            audioSource.PlayOneShot(waveEndClip));
    }


    private void Awake()
    {
        _stateManager = GameObject.Find("OBSERVER").GetComponent<WorldStateManager>();
    }
    private void Start()
    {
        EventBus.Instance.Subscribe<ClusterTransitionEvent>(HandleCluster);
    }

    private void HandleCluster(ClusterTransitionEvent evt)
    {
        if (_stateManager.IsInMappedWorld)
        {
            audioSource.clip = enterClip;
            audioSource.PlayOneShot(enterClip);
            //Debug.Log("Playing Enter Sound");
        }
        else
        {
            audioSource.clip = leaveClip;
            audioSource.PlayOneShot(leaveClip);
            //Debug.Log("Playing Leave Sound");
        }
    }
}
