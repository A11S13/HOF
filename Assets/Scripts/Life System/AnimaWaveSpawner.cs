using System.Collections;
using UnityEngine;

public class AnimaWaveSpawner : MonoBehaviour
{
    [SerializeField] WorldDataManager world;
    [SerializeField] int animasPerWave = 5;
    [SerializeField] float waveInterval = 2f;
    [SerializeField] float spawnRadius = 20f;

    int aliveHostiles;
    bool isWaveSequenceActive; // Track if wave sequence is running
    int remainingWaves; // Track remaining waves

    void Awake()
    {
        if (!world)
            world = FindFirstObjectByType<WorldDataManager>();
    }

    void OnEnable()
    {
        EventBus.Instance.Subscribe<StartAnimaWaveEvent>(StartWave);
        EventBus.Instance.Subscribe<AnimaSpawnedEvent>(OnAnimaSpawned);
        EventBus.Instance.Subscribe<AnimaKilledEvent>(OnAnimaKilled);
    }

    void StartWave(StartAnimaWaveEvent e)
    {
        aliveHostiles = 0;
        isWaveSequenceActive = true; // Mark sequence as active
        remainingWaves = e.Waves; // Track total waves

        EventBus.Instance.Publish(new AnimaWaveStartedEvent());
        StartCoroutine(SpawnWaves(e.Location, e.Waves));
    }

    IEnumerator SpawnWaves(Vector3 center, int waves)
    {
        for (int i = 0; i < waves; i++)
        {
            SpawnWave(center);
            yield return new WaitForSeconds(waveInterval);
        }
    }

    void SpawnWave(Vector3 center)
    {
        for (int i = 0; i < animasPerWave; i++)
        {
            Vector3 p = center + Random.insideUnitSphere * spawnRadius;
            p.y = 0f;

            world.SpawnAnimaAt(p, false);

            EventBus.Instance.Publish(
                new AnimaSpawnedEvent(p, false)
            );
        }
    }

    void OnAnimaSpawned(AnimaSpawnedEvent e)
    {
        if (!e.IsFriendly)
            aliveHostiles++;
    }

    void OnAnimaKilled(AnimaKilledEvent e)
    {
        if (e.WasFriendly)
            return;

        if (--aliveHostiles > 0)
            return;

        // Sub-wave cleared, decrement remaining waves
        remainingWaves--;

        // Only publish clear event when ALL waves are done
        if (remainingWaves <= 0 && isWaveSequenceActive)
        {
            isWaveSequenceActive = false;
            EventBus.Instance.Publish(new AnimaWaveClearedEvent());
        }
    }
}
