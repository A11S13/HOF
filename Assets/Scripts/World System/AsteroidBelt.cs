using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class AsteroidBelt : MonoBehaviour
{
    [Header("Spawning Settings")]
    public GameObject[] largeAsteroidPrefabs;
    public GameObject[] mediumAsteroidPrefabs;
    public GameObject[] smallAsteroidPrefabs;
    public float innerRadius = 20f;
    public float outerRadius = 50f;
    public float spawnRate = 20f;
    public float cleanupMargin = 1.2f;

    [Header("Asteroid Properties")]
    public float minLargeAsteroidSize = 2f;
    public float maxLargeAsteroidSize = 4f;
    public float minMediumAsteroidSize = 1f;
    public float maxMediumAsteroidSize = 2f;
    public float minSmallAsteroidSize = 0.5f;
    public float maxSmallAsteroidSize = 1f;
    public float densityThreshold = 0.3f;
    public float spacing = 2f;

    [Header("Spawning Probability")]
    [Range(0, 1)] public float largeAsteroidProbability = 0.1f;
    [Range(0, 1)] public float mediumAsteroidProbability = 0.3f;
    // Small asteroid probability is calculated as (1 - large - medium)

    public LayerMask collisionMask;

    [Header("Orbital Texture")]
    public GameObject orbitalPlane;

    [Header("Destruction Settings")]
    public int mediumFragmentsFromLarge = 3;
    public int smallFragmentsFromMedium = 4;
    public int smallFragmentsFromLarge = 2;
    public float fragmentOffset = 55f;
    public float explosionForce = 10f;
    public float fragmentCooldown = 0.1f; // Time between fragment spawns

    private Texture2D orbitalTexture;
    private Transform playerTransform;
    private Material orbitalMaterial;
    private Vector3 planeSize;
    private float cleanupRadiusSquared;

    void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        InitializeOrbitalTexture();
        StartCoroutine(SpawnManagement());
        cleanupRadiusSquared = Mathf.Pow(outerRadius * cleanupMargin, 2);
    }

    void InitializeOrbitalTexture()
    {
        if (orbitalPlane != null)
        {
            Renderer renderer = orbitalPlane.GetComponent<Renderer>();
            orbitalMaterial = renderer.material;
            orbitalTexture = (Texture2D)orbitalMaterial.mainTexture;

            if (!orbitalTexture.isReadable)
                Debug.LogError("Enable Read/Write in texture import settings!");

            planeSize = renderer.bounds.size;
        }
    }

    IEnumerator SpawnManagement()
    {
        while (true)
        {
            if (orbitalTexture != null)
            {
                Vector3 spawnPoint = GetRingSpawnPoint();
                
                // Determine which size asteroid to spawn based on probabilities
                float randomValue = Random.value;
                AsteroidSize sizeToSpawn;
                
                if (randomValue < largeAsteroidProbability)
                {
                    sizeToSpawn = AsteroidSize.Large;
                }
                else if (randomValue < largeAsteroidProbability + mediumAsteroidProbability)
                {
                    sizeToSpawn = AsteroidSize.Medium;
                }
                else
                {
                    sizeToSpawn = AsteroidSize.Small;
                }
                
                CheckAndSpawnAsteroid(spawnPoint, sizeToSpawn);
                yield return new WaitForSeconds(1f / spawnRate);
            }
            else
            {
                yield return null;
            }
        }
    }

    Vector3 GetRingSpawnPoint()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomRadius = Mathf.Sqrt(
            Random.Range(innerRadius * innerRadius, outerRadius * outerRadius)
        );

        return playerTransform.position +
               new Vector3(randomDirection.x, 0, randomDirection.y) * randomRadius;
    }

    void CheckAndSpawnAsteroid(Vector3 worldPosition, AsteroidSize size)
    {
        Vector2 uv = WorldToTextureUV(worldPosition);
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return;

        Color pixel = orbitalTexture.GetPixelBilinear(uv.x, uv.y);
        float density = pixel.grayscale;

        float spawnProbability = Mathf.Clamp01((density - densityThreshold) / (1 - densityThreshold));
        if (Random.value > spawnProbability) return;

        float asteroidSize = 0f;
        switch (size)
        {
            case AsteroidSize.Large:
                asteroidSize = Random.Range(minLargeAsteroidSize, maxLargeAsteroidSize);
                break;
            case AsteroidSize.Medium:
                asteroidSize = Random.Range(minMediumAsteroidSize, maxMediumAsteroidSize);
                break;
            case AsteroidSize.Small:
                asteroidSize = Random.Range(minSmallAsteroidSize, maxSmallAsteroidSize);
                break;
        }

        if (!Physics.CheckSphere(worldPosition, spacing + asteroidSize / 2, collisionMask))
        {
            SpawnAsteroid(worldPosition, asteroidSize, size);
        }
    }

    Vector2 WorldToTextureUV(Vector3 worldPosition)
    {
        if (orbitalPlane == null) return Vector2.zero;

        Vector3 localPos = orbitalPlane.transform.InverseTransformPoint(worldPosition);
        return new Vector2(
            (localPos.x / planeSize.x) + 0.5f,
            (localPos.z / planeSize.z) + 0.5f
        );
    }

    public void SpawnAsteroid(Vector3 position, float size, AsteroidSize asteroidSize)
    {
        GameObject[] prefabsToUse = null;

        switch (asteroidSize)
        {
            case AsteroidSize.Large:
                prefabsToUse = largeAsteroidPrefabs;
                break;
            case AsteroidSize.Medium:
                prefabsToUse = mediumAsteroidPrefabs;
                break;
            case AsteroidSize.Small:
                prefabsToUse = smallAsteroidPrefabs;
                break;
        }

        if (prefabsToUse == null || prefabsToUse.Length == 0)
        {
            Debug.LogError("No prefabs assigned for asteroid size: " + asteroidSize);
            return;
        }

        GameObject asteroidObject = Instantiate(
            prefabsToUse[Random.Range(0, prefabsToUse.Length)],
            position,
            Random.rotation
        );

        // Clamp Y after instantiation
        asteroidObject.transform.position = new Vector3(
            asteroidObject.transform.position.x,
            0f,
            asteroidObject.transform.position.z
        );

        asteroidObject.transform.localScale = Vector3.one * size;

        Asteroid asteroidScript = asteroidObject.GetComponent<Asteroid>();
        if (asteroidScript != null)
        {
            asteroidScript.Initialize(this, asteroidSize, playerTransform, cleanupRadiusSquared);
        }
    }

    public void CreateAsteroidFragments(Vector3 position, AsteroidSize originalSize)
    {
        switch (originalSize)
        {
            case AsteroidSize.Large:
                for (int i = 0; i < mediumFragmentsFromLarge; i++)
                {
                    Vector2 randomCircle = Random.insideUnitCircle * fragmentOffset;
                    Vector3 spawnPos = position + new Vector3(randomCircle.x, 0, randomCircle.y);

                    // Force Y=0
                    spawnPos.y = 0f;

                    SpawnAsteroid(spawnPos, Random.Range(minMediumAsteroidSize, maxMediumAsteroidSize), AsteroidSize.Medium);
                }

                for (int i = 0; i < smallFragmentsFromLarge; i++)
                {
                    Vector2 randomCircle = Random.insideUnitCircle * fragmentOffset;
                    Vector3 spawnPos = position + new Vector3(randomCircle.x, 0, randomCircle.y);

                    // Force Y=0
                    spawnPos.y = 0f;

                    SpawnAsteroid(spawnPos, Random.Range(minSmallAsteroidSize, maxSmallAsteroidSize), AsteroidSize.Small);
                }
                break;

            case AsteroidSize.Medium:
                for (int i = 0; i < smallFragmentsFromMedium; i++)
                {
                    Vector2 randomCircle = Random.insideUnitCircle * fragmentOffset * 0.5f;
                    Vector3 spawnPos = position + new Vector3(randomCircle.x, 0, randomCircle.y);

                    // Force Y=0
                    spawnPos.y = 0f;

                    SpawnAsteroid(spawnPos, Random.Range(minSmallAsteroidSize, maxSmallAsteroidSize), AsteroidSize.Small);
                }
                break;

            case AsteroidSize.Small:
                // Just disappears
                break;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerTransform.position, innerRadius);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerTransform.position, outerRadius);
        }
    }
}

public enum AsteroidSize
{
    Large,
    Medium,
    Small
}