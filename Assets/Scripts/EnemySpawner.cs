using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float initialSpawnRadius = 15f;
    public float minSpawnRadius = 5f;
    public float initialSpawnInterval = 3f;
    public float minSpawnInterval = 0.001f; // Adjusted minimum spawn interval
    public float decayRate = 0.02f; // Rate of exponential decay
    public int maxEnemyCount = 2000; // Maximum allowed enemy count

    private Transform playerTransform;
    private int currentEnemyCount = 0;

    void Start()
    {
        // Find the player (Make sure there's a "Player" tag in your scene)
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;

        // Start spawning enemies
        SpawnEnemy();
    }

    void SpawnEnemy()
    {
        // Ensure the playerTransform is not null
        if (playerTransform != null)
        {
            // Calculate a random position within the spawn radius around the player
            Vector2 randomPosition = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, initialSpawnRadius);
            Vector3 spawnPosition = new Vector3(randomPosition.x, 0f, randomPosition.y) + playerTransform.position;

            // Instantiate the enemy at the calculated position
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            // Optionally, you can set the new enemy as a child of the spawner for organizational purposes
            newEnemy.transform.parent = transform;

            // Increase the current enemy count
            currentEnemyCount++;

            // Calculate the decayed spawn interval using a logarithmic function
            initialSpawnInterval = Mathf.Max(minSpawnInterval, initialSpawnInterval * Mathf.Exp(-decayRate * initialSpawnInterval));

            // Ensure we don't exceed the maximum enemy count
            if (currentEnemyCount < maxEnemyCount)
            {
                // Schedule the next spawn
                Invoke("SpawnEnemy", initialSpawnInterval);
            }
        }
    }
}
