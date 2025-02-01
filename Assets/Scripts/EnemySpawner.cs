using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public float initialSpawnRadius = 15f;
    public float minSpawnRadius = 115f;
    public float initialSpawnInterval = 3f;
    public float spawnInterval;
    public float minSpawnInterval = 0.001f;
    public float decayRate = 0.02f;
    public int maxEnemyCount = 1000;
    public int currentEnemyCount = 0;

    [Header("Swarm Settings")]
    public int rayCount = 3;
    public float arcAngle = 90f;
    public float fRadius = 40f;
    public float cRadius = 8f;
    public float wPlayer = 0.2f;
    public float wCrowd = 0.1f;
    public float wAlign = 1f;
    public float wCohesion = 1f;
    public float wNoise = 0.15f;
    public bool swarm = true;
    public bool tPlayer = true;
    public bool align = true;
    public bool cohesion = true;
    public bool noise = true;
    public bool crowd = true;

    Transform playerTransform;

    void Start()
    {
        // Find the player (ensure your player GameObject is tagged "Player")
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        spawnInterval = initialSpawnInterval;
        // Pass global settings to EnemyManager
        EnemyManager.Instance.GlobalSettings = new EnemyManager.Settings
        {
            rayCount = rayCount,
            arcAngle = arcAngle,
            fRadius = fRadius,
            cRadius = cRadius,
            wPlayer = wPlayer,
            wCrowd = wCrowd,
            wAlign = wAlign,
            wCohesion = wCohesion,
            wNoise = wNoise,
            swarm = swarm,
            tPlayer = tPlayer,
            align = align,
            cohesion = cohesion,
            crowd = crowd,
            noise = noise,
            enemySpeed = Enemy.enemySpeed,
            playerPosition = playerTransform != null ? playerTransform.position : Vector3.zero
        };

        SpawnEnemy();
    }

    void SpawnEnemy()
    {
        if (playerTransform == null) return;

        // Choose a random direction around the player.
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float radius = Random.Range(minSpawnRadius, initialSpawnRadius);
        Vector3 spawnPos = new Vector3(randomDir.x, 0f, randomDir.y) * radius + playerTransform.position;

        // Instantiate and parent under spawner for organization.
        GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        newEnemy.transform.parent = transform;

        currentEnemyCount++;

        // Exponentially decrease spawn interval.
        spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval * Mathf.Exp(-decayRate * initialSpawnInterval));

        if (currentEnemyCount < maxEnemyCount)
            Invoke(nameof(SpawnEnemy), spawnInterval);
    }
}
