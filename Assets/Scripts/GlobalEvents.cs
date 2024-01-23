using System.Collections;
using UnityEngine;

public class GlobalEvents : MonoBehaviour
{
    ChipSynth _cS;
    Music _music;
    EnemyCounter _enemyCounter;
    private AudioSource audioSource;
    public AudioClip approaching;
    public AudioClip destroyed;
    public bool gameStart;

    bool isWaiting = false;

    void Start()
    {
        _cS = GetComponent<ChipSynth>();
        audioSource = GetComponent<AudioSource>();
        _music = GameObject.Find("Music").GetComponent<Music>();
        _enemyCounter = GameObject.Find("EnemyCounter").GetComponent<EnemyCounter>();
        StartCoroutine(WaitAndStart(5)); // Wait for 5 seconds and then start the global event sequence
    }

    void Update()
    {
        // Add any relevant logic in Update if needed
    }

    IEnumerator GlobalEventSequence()
    {
        // Event 1
        audioSource.clip = approaching;
        audioSource.Play();
        PerformEvent1();

        // Check if Event 1 occurred
        yield return StartCoroutine(WaitForWaveDestroyed(_enemyCounter.wave1Destroyed));

        // Event 2
        audioSource.clip = destroyed;
        audioSource.Play();
        yield return StartCoroutine(Wait(5.0f));
        audioSource.clip = approaching;
        audioSource.Play();
        PerformEvent2();

        // Continue with the rest of your events...
        // You can add more events if needed using the same pattern
    }

    void PerformEvent1()
    {
        Debug.Log("Event 1");
        _music.PlayTrack("track1");
        _enemyCounter.wave1.SetActive(true);
    }

    void PerformEvent2()
    {
        Debug.Log("Event 2");
        _music.PlayTrack("track1");
        _enemyCounter.wave2.SetActive(true);
    }

    void PerformEvent3()
    {
        Debug.Log("Event 3");
        _music.PlayTrack("track1");
        _enemyCounter.wave3.SetActive(true);
    }

    void PerformEvent4()
    {
        Debug.Log("Event 4");
        _music.PlayTrack("track1");
        _enemyCounter.wave4.SetActive(true);
    }

    IEnumerator WaitForWaveDestroyed(bool waveDestroyed)
    {
        yield return new WaitUntil(() => waveDestroyed);
    }

    IEnumerator Wait(float delay)
    {
        isWaiting = true;
        yield return new WaitForSeconds(delay);
        isWaiting = false;
    }

    IEnumerator WaitAndStart(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameStart = true;
        StartCoroutine(GlobalEventSequence());
    }
}
