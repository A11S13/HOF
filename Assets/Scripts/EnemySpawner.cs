using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public int rayCount = 3;
    public float arcAngle = 90f;
    public float swarmDelay;

    [Range(0, 100)]
    public float fRadius;

    [Range(0, 1)]
    public float wPlayer;

    [Range(0, 10)]
    public float cRadius;

    [Range(0, 1)]
    public float wCrowd;

    [Range(0, 1)]
    public float wAlign;

    [Range(0, 1)]
    public float wCohesion;

    [Range(0, 1)]
    public float wNoise;

    public bool swarm;
    public bool tPlayer;
    public bool align;
    public bool cohesion;
    public bool noise;
    public bool crowd;
    public GameObject enemyPrefab;
    public float initialSpawnRadius = 15f;
    public float minSpawnRadius = 5f;
    public float initialSpawnInterval = 3f;
    public float minSpawnInterval = 0.001f; // Adjusted minimum spawn interval
    public float decayRate = 0.02f; // Rate of exponential decay
    public int maxEnemyCount = 1000; // Maximum allowed enemy count

    private Transform playerTransform;
    public int currentEnemyCount = 0;

    void Start()
    {
        swarm = true;
        tPlayer = true;
        align = true;
        cohesion = true;
        noise = true;
        crowd = true;

        fRadius = 40;
        wPlayer = 0.01f;
        cRadius = 8f;
        wCrowd = 0.1f;
        wAlign = 1f;
        wCohesion = 1f;
        wNoise = 0.15f;

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
            Vector2 randomPosition =
                Random.insideUnitCircle.normalized
                * Random.Range(minSpawnRadius, initialSpawnRadius);
            Vector3 spawnPosition =
                new Vector3(randomPosition.x, 0f, randomPosition.y) + playerTransform.position;

            // Instantiate the enemy at the calculated position
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            // Optionally, you can set the new enemy as a child of the spawner for organizational purposes
            newEnemy.transform.parent = transform;

            // Increase the current enemy count
            currentEnemyCount++;

            // Calculate the decayed spawn interval using a logarithmic function
            initialSpawnInterval = Mathf.Max(
                minSpawnInterval,
                initialSpawnInterval * Mathf.Exp(-decayRate * initialSpawnInterval)
            );

            // Ensure we don't exceed the maximum enemy count
            if (currentEnemyCount < maxEnemyCount)
            {
                // Schedule the next spawn
                Invoke("SpawnEnemy", initialSpawnInterval);
            }
        }
    }
}
