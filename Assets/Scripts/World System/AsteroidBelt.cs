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

    public LayerMask collisionMask;

    [Header("Orbital Texture")]
    public GameObject orbitalPlane;

    private Texture2D orbitalTexture;
    private Transform playerTransform;
    private Material orbitalMaterial;
    private Vector3 planeSize;

    private float cleanupRadiusSquared;
    private List<GameObject> activeAsteroids = new List<GameObject>();

    void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        InitializeOrbitalTexture();

        StartCoroutine(SpawnManagement());
        StartCoroutine(CleanupRoutine());

        cleanupRadiusSquared = Mathf.Pow(outerRadius * cleanupMargin, 2);
    }

    private IEnumerator CleanupRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);

        while (true)
        {
            yield return wait;

            if (playerTransform == null) continue;

            // Remove destroyed/null objects quickly
            activeAsteroids.RemoveAll(item => item == null);

            for (int i = activeAsteroids.Count - 1; i >= 0; i--)
            {
                GameObject ast = activeAsteroids[i];
                float sqrDist = (ast.transform.position - playerTransform.position).sqrMagnitude;

                if (sqrDist > cleanupRadiusSquared)
                {
                    Destroy(ast);
                    activeAsteroids.RemoveAt(i);
                }
            }
        }
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
        // CACHE THIS! This prevents massive garbage collection spikes
        WaitForSeconds spawnWait = new WaitForSeconds(1f / spawnRate);

        while (true)
        {
            if (orbitalTexture != null)
            {
                Vector3 spawnPoint = GetRingSpawnPoint();

                float randomValue = Random.value;
                AsteroidSize sizeToSpawn;

                if (randomValue < largeAsteroidProbability)
                    sizeToSpawn = AsteroidSize.Large;
                else if (randomValue < largeAsteroidProbability + mediumAsteroidProbability)
                    sizeToSpawn = AsteroidSize.Medium;
                else
                    sizeToSpawn = AsteroidSize.Small;

                CheckAndSpawnAsteroid(spawnPoint, sizeToSpawn);
                yield return spawnWait; // Use cached wait
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
            case AsteroidSize.Large: asteroidSize = Random.Range(minLargeAsteroidSize, maxLargeAsteroidSize); break;
            case AsteroidSize.Medium: asteroidSize = Random.Range(minMediumAsteroidSize, maxMediumAsteroidSize); break;
            case AsteroidSize.Small: asteroidSize = Random.Range(minSmallAsteroidSize, maxSmallAsteroidSize); break;
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
            case AsteroidSize.Large: prefabsToUse = largeAsteroidPrefabs; break;
            case AsteroidSize.Medium: prefabsToUse = mediumAsteroidPrefabs; break;
            case AsteroidSize.Small: prefabsToUse = smallAsteroidPrefabs; break;
        }

        if (prefabsToUse == null || prefabsToUse.Length == 0) return;

        GameObject asteroidObject = Instantiate(
            prefabsToUse[Random.Range(0, prefabsToUse.Length)],
            position,
            Random.rotation
        );

        asteroidObject.transform.position = new Vector3(asteroidObject.transform.position.x, 0f, asteroidObject.transform.position.z);
        asteroidObject.transform.localScale = Vector3.one * size;

        Asteroid asteroidScript = asteroidObject.GetComponent<Asteroid>();
        if (asteroidScript != null)
        {
            int tier = asteroidSize == AsteroidSize.Large ? 1 : (asteroidSize == AsteroidSize.Medium ? 2 : 3);
            asteroidScript.SetupStats(tier);
            activeAsteroids.Add(asteroidObject);
        }
    }
}

public enum AsteroidSize { Large, Medium, Small }