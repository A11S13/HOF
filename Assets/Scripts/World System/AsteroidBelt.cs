using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class AsteroidBelt : MonoBehaviour
{
    [Header("Spawning Settings")]
    public GameObject asteroidPrefab;
    public float innerRadius = 20f;
    public float outerRadius = 50f;
    public float spawnRate = 20f;
    public float cleanupMargin = 1.2f;

    [Header("Asteroid Properties")]
    public float minAsteroidSize = 0.5f;
    public float maxAsteroidSize = 3f;
    public float densityThreshold = 0.3f;
    public float spacing = 2f;

    public LayerMask collisionMask;

    [Header("Orbital Texture")]
    public GameObject orbitalPlane;

    private Texture2D orbitalTexture;
    private Transform playerTransform;
    private Material orbitalMaterial;
    private Vector3 planeSize;
    private float cleanupRadiusSquared; // Store squared radius for efficiency

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
                CheckAndSpawnAsteroid(spawnPoint);
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
        // Get random direction in XZ plane
        Vector2 randomDirection = Random.insideUnitCircle.normalized;

        // Calculate random radius with uniform distribution
        float randomRadius = Mathf.Sqrt(
            Random.Range(innerRadius * innerRadius, outerRadius * outerRadius)
        );

        return playerTransform.position +
               new Vector3(randomDirection.x, 0, randomDirection.y) * randomRadius;
    }

    void CheckAndSpawnAsteroid(Vector3 worldPosition)
    {
        Vector2 uv = WorldToTextureUV(worldPosition);
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return;

        Color pixel = orbitalTexture.GetPixelBilinear(uv.x, uv.y);
        float density = pixel.grayscale;

        // Density-based spawn probability
        float spawnProbability = Mathf.Clamp01((density - densityThreshold) / (1 - densityThreshold));
        if (Random.value > spawnProbability) return;

        // Calculate size with density weighting
        float size = Mathf.Lerp(minAsteroidSize, maxAsteroidSize, density);

        // Check collisions with size consideration
        if (!Physics.CheckSphere(worldPosition, spacing + size / 2, collisionMask))
        {
            SpawnAsteroid(worldPosition, size);
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

    void SpawnAsteroid(Vector3 position, float size)
    {
        GameObject asteroidObject = Instantiate(asteroidPrefab, position, Random.rotation);
        asteroidObject.transform.localScale = Vector3.one * size;

        // Tell the asteroid to start managing its despawn
        Asteroid asteroidScript = asteroidObject.GetComponent<Asteroid>();
        if (asteroidScript != null)
        {
            asteroidScript.InitializeDespawn(playerTransform, cleanupRadiusSquared);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            // Draw inner radius (safe zone)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerTransform.position, innerRadius);

            // Draw outer radius
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerTransform.position, outerRadius);
        }
    }
}