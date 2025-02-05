using UnityEngine;

public class GlobalEvents : MonoBehaviour
{
    ChipSynth _cS;
    Music _music;
    EnemyCounter _eC;
    private AudioSource audioSource;
    public AudioClip approaching;
    public AudioClip destroyed;
    public bool gameStart;
    public bool waveAssigned;
    public bool waveDestroyed;

    void Start()
    {
        gameStart = true;
        _cS = GetComponent<ChipSynth>();
        audioSource = GetComponent<AudioSource>();
        _music = GameObject.Find("Music").GetComponent<Music>();
        _eC = GameObject.Find("EnemyCounter").GetComponent<EnemyCounter>();
    }

    void Update()
    {
        if (_eC.waveDestroyed)
        {
            Debug.Log("WAVE DESTROYED");
            waveAssigned = false;
            Destroyed();
        }
    }

    void Approaching()
    {
        audioSource.clip = approaching;
        audioSource.Play();
    }

    void Destroyed()
    {
        _eC.waveDestroyed = false;
        audioSource.clip = destroyed;
        audioSource.Play();
        _music.PlayTrack("track2");
    }

    public void PerformEvent1()
    {
        Debug.Log("Event 1");
        _music.PlayTrack("track1");
        waveAssigned = true;
        _eC.SpawnWave(1);
        Approaching();
    }

    public void PerformEvent2(bool? ikill)
    {
        Debug.Log("Event 2");
        _music.PlayTrack("track1");
        _eC.SpawnWave(2);
    }

    public void PerformEvent3()
    {
        Debug.Log("Event 3");
        _music.PlayTrack("track1");
        _eC.SpawnWave(3);
    }

    public void PerformEvent4(bool? ikill)
    {
        Debug.Log("Event 4");
        _music.PlayTrack("track1");
        _eC.SpawnWave(4);
    }
}
