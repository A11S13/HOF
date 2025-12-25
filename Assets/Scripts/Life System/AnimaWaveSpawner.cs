using System.Collections;
using UnityEngine;
using UnityEngine.LightTransport;

public class AnimaWaveSpawner : MonoBehaviour
{
    [SerializeField] WorldDataManager world;

    void Awake()
    {
        if (!world)
            world = FindFirstObjectByType<WorldDataManager>();
    }

    [SerializeField] GameObject animaPrefab;
    [SerializeField] int animasPerWave = 5;
    [SerializeField] float waveInterval = 2f;
    [SerializeField] float spawnRadius = 20f;

    void OnEnable()
    {
        EventBus.Instance.Subscribe<StartAnimaWaveEvent>(StartWave);
    }

    void StartWave(StartAnimaWaveEvent e)
    {
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
        }
    }
}
