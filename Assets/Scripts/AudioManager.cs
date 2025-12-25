using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("References")]
    public WorldStateManager _stateManager;
    public AudioSource musicAudioSource;
    public AudioSource audioSource;

    [Header("Sound Effects")]
    public AudioClip enterClip;
    public AudioClip leaveClip;
    public AudioClip waveStartClip;
    public AudioClip waveEndClip;

    [Header("Music Tracks")]
    public AudioClip calmMusic;
    public AudioClip threateningMusic;
    public AudioClip aftermathMusic;

    [Header("Transition Settings")]
    [Range(0.1f, 3f)]
    public float musicFadeDuration = 1f;

    [Header("Aftermath Timing")]
    [Tooltip("Base delay before aftermath music (seconds)")]
    public float baseAftermathDelay = 2f;
    [Tooltip("Additional delay per aggression point (seconds)")]
    public float delayPerAggression = 0.1f;
    [Tooltip("Maximum aftermath delay (seconds)")]
    public float maxAftermathDelay = 10f;

    private MusicState currentMusicState = MusicState.Calm;
    private Coroutine fadeCoroutine;
    private Coroutine aftermathDelayCoroutine;
    private PlayerAggressionTracker aggressionTracker;

    void Awake()
    {
        _stateManager = GameObject.Find("OBSERVER").GetComponent<WorldStateManager>();
        aggressionTracker = FindFirstObjectByType<PlayerAggressionTracker>();

        if (musicAudioSource != null)
        {
            musicAudioSource.loop = true;
            musicAudioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        if (calmMusic != null && musicAudioSource != null)
        {
            currentMusicState = MusicState.Calm;
            musicAudioSource.clip = calmMusic;
            musicAudioSource.volume = 1f;
            musicAudioSource.Play();
        }
        else
        {
            Debug.LogError("AudioManager: Missing calm music clip or music audio source!");
        }
    }

    void OnEnable()
    {
        EventBus.Instance.Subscribe<ClusterTransitionEvent>(HandleCluster);
        EventBus.Instance.Subscribe<AnimaWaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Subscribe<AnimaWaveClearedEvent>(OnWaveCleared);
        EventBus.Instance.Subscribe<AggressionDecayedEvent>(OnAggressionDecayed);
    }

    private void HandleCluster(ClusterTransitionEvent evt)
    {
        if (_stateManager.IsInMappedWorld)
        {
            audioSource.PlayOneShot(enterClip);
        }
        else
        {
            audioSource.PlayOneShot(leaveClip);
        }
    }

    private void OnWaveStarted(AnimaWaveStartedEvent evt)
    {
        audioSource.PlayOneShot(waveStartClip);

        if (aftermathDelayCoroutine != null)
        {
            StopCoroutine(aftermathDelayCoroutine);
            aftermathDelayCoroutine = null;
        }

        SwitchMusic(MusicState.Threatening, true);
    }

    private void OnWaveCleared(AnimaWaveClearedEvent evt)
    {
        audioSource.PlayOneShot(waveEndClip);

        float delay = CalculateAftermathDelay();

        if (aftermathDelayCoroutine != null)
            StopCoroutine(aftermathDelayCoroutine);

        aftermathDelayCoroutine = StartCoroutine(DelayedAftermathTransition(delay));
    }

    private void OnAggressionDecayed(AggressionDecayedEvent evt)
    {
        SwitchMusic(MusicState.Calm);
    }

    private float CalculateAftermathDelay()
    {
        if (aggressionTracker == null)
            return baseAftermathDelay;

        float aggressionDelay = baseAftermathDelay + (aggressionTracker.CurrentAggression * delayPerAggression);
        return Mathf.Min(aggressionDelay, maxAftermathDelay);
    }

    private IEnumerator DelayedAftermathTransition(float delay)
    {
        yield return new WaitForSeconds(delay);
        SwitchMusic(MusicState.Aftermath);
        aftermathDelayCoroutine = null;
    }

    public void SwitchMusic(MusicState newState, bool immediate = false)
    {
        if (currentMusicState == newState)
            return;

        currentMusicState = newState;

        AudioClip targetClip = newState switch
        {
            MusicState.Calm => calmMusic,
            MusicState.Threatening => threateningMusic,
            MusicState.Aftermath => aftermathMusic,
            _ => calmMusic
        };

        if (targetClip == null)
        {
            Debug.LogWarning($"AudioManager: No music clip assigned for state {newState}");
            return;
        }

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (immediate)
        {
            musicAudioSource.clip = targetClip;
            musicAudioSource.volume = 1f;
            if (!musicAudioSource.isPlaying)
                musicAudioSource.Play();
        }
        else
        {
            fadeCoroutine = StartCoroutine(CrossfadeMusic(targetClip));
        }
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        float elapsed = 0f;
        float startVolume = musicAudioSource.volume;

        while (elapsed < musicFadeDuration / 2)
        {
            elapsed += Time.deltaTime;
            musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration / 2));
            yield return null;
        }

        musicAudioSource.clip = newClip;
        musicAudioSource.Play();

        elapsed = 0f;
        while (elapsed < musicFadeDuration / 2)
        {
            elapsed += Time.deltaTime;
            musicAudioSource.volume = Mathf.Lerp(0f, 1f, elapsed / (musicFadeDuration / 2));
            yield return null;
        }

        musicAudioSource.volume = 1f;
    }
}
