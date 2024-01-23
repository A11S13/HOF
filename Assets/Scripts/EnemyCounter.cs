using UnityEngine;

public class EnemyCounter : MonoBehaviour
{
    int enemyCount; // Initialize appropriately if needed
    public GameObject wave1;
    public GameObject wave2;
    public GameObject wave3;
    public GameObject wave4;
    public bool wave1Destroyed;
    public bool wave2Destroyed;
    public bool wave3Destroyed;
    public bool wave4Destroyed;

    void Start()
    {
        // Perform any necessary initialization
    }

    void Update()
    {
        CountEnemies();
        IsSwarmDestroyed();
    }

    void IsSwarmDestroyed()
    {
        CheckWaveDestroyed(wave1, ref wave1Destroyed);
        CheckWaveDestroyed(wave2, ref wave2Destroyed);
        CheckWaveDestroyed(wave3, ref wave3Destroyed);
        CheckWaveDestroyed(wave4, ref wave4Destroyed);
    }

    void CheckWaveDestroyed(GameObject wave, ref bool waveDestroyed)
    {
        EnemySpawner enemySpawner = wave.GetComponent<EnemySpawner>();
        
        if (enemySpawner != null)
        {
            if (enemySpawner.currentEnemyCount == enemySpawner.maxEnemyCount && enemyCount == 0)
            {
                waveDestroyed = true;
                Debug.Log("Destroyed is: " + wave1Destroyed);
            }
        }
        else
        {
            Debug.LogError("EnemySpawner component not found on " + wave.name);
        }
    }

    void CountEnemies()
    {
        // Find all game objects with the "Enemy" tag
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        enemyCount = enemies.Length; // Update the enemy count
        Debug.Log("Number of Enemies: " + enemyCount);
    }

    void SpawnWaves(int wave)
    {
        switch (wave)
        {
            case 1:
                wave1.SetActive(true);
                break;
            case 2:
                wave2.SetActive(true);
                break;
            case 3:
                wave3.SetActive(true);
                break;
            case 4:
                wave4.SetActive(true);
                break;
            default:
                Debug.LogWarning("Invalid wave number");
                break;
        }
    }
}
