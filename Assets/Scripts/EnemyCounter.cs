using UnityEngine;

public class EnemyCounter : MonoBehaviour
{
    GlobalEvents _gE;
    int enemyCount; // Initialize appropriately if needed
    public int waves;
    public GameObject _wave1;
    public GameObject _wave2;
    public GameObject _wave3;
    public GameObject _wave4;

    private GameObject wave1;
    private GameObject wave2;
    private GameObject wave3;
    private GameObject wave4;
    public bool waveDestroyed;
    public bool wave1Destroyed;
    public bool wave2Destroyed;
    public bool wave3Destroyed;
    public bool wave4Destroyed;

    void Start()
    {
        _gE = GameObject.Find("GlobalEvents").GetComponent<GlobalEvents>();
    }

    void Update()
    {
        CountEnemies();

        if (enemyCount == 0 && _gE.waveAssigned)
        {
            IsSwarmDestroyed();
        }
    }

    void IsSwarmDestroyed()
    {
        if (wave1 != null)
            CheckWaveDestroyed(wave1, ref wave1Destroyed);
        if (wave2 != null)
            CheckWaveDestroyed(wave2, ref wave2Destroyed);
        if (wave3 != null)
            CheckWaveDestroyed(wave3, ref wave3Destroyed);
        if (wave4 != null)
            CheckWaveDestroyed(wave4, ref wave4Destroyed);
    }

    void CheckWaveDestroyed(GameObject wave, ref bool currentWaveDestroyed)
    {
        EnemySpawner enemySpawner = wave.GetComponent<EnemySpawner>();

        if (enemySpawner.currentEnemyCount == enemySpawner.maxEnemyCount && enemyCount == 0)
        {
            Destroy(wave);
            currentWaveDestroyed = true;
            waveDestroyed = currentWaveDestroyed;
        }
    }

    void CountEnemies()
    {
        // Find all game objects with the "Enemy" tag
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        enemyCount = enemies.Length; // Update the enemy count
        //Debug.Log("Number of Enemies: " + enemyCount);
    }

    public void SpawnWave(int wave)
    {
        waves++;
        switch (wave)
        {
            case 1:
                wave1 = Instantiate(_wave1, Vector3.zero, Quaternion.identity);
                break;
            case 2:
                wave2 = Instantiate(_wave2, Vector3.zero, Quaternion.identity);
                break;
            case 3:
                wave3 = Instantiate(_wave3, Vector3.zero, Quaternion.identity);
                break;
            case 4:
                wave4 = Instantiate(_wave4, Vector3.zero, Quaternion.identity);
                break;
            default:
                Debug.LogWarning("Invalid wave number");
                break;
        }
    }
}
