using UnityEngine;
using System.Collections.Generic;

public class AsteroidBelt : MonoBehaviour
{
    public GameObject asteroidPrefab;
    public float aoiRadius = 100f;
    public float aoiBuffer = 5f;
    public int asteroidCount = 100;
    public float orbitalSpeed = 10f;
    public float minAsteroidSpacing = 2f;

    private List<GameObject> asteroids = new List<GameObject>();
    private Transform playerTransform;

    void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        SpawnInitialAsteroids();
    }

    void SpawnInitialAsteroids()
    {
        for (int i = 0; i < asteroidCount; i++)
        {
            RespawnAsteroid(true); // Initial spawn uses different logic.
        }
    }

    void Update()
    {
        for (int i = asteroids.Count - 1; i >= 0; i--)
        {
            GameObject asteroid = asteroids[i];
            Vector3 relativePosition = asteroid.transform.position - playerTransform.position;
            if (relativePosition.magnitude > aoiRadius + aoiBuffer)
            {
                asteroids.RemoveAt(i);
                Destroy(asteroid);
                RespawnAsteroid(false); // Normal respawn.
            }
            else
            {
                Vector3 orbitalAxis = Vector3.up;
                Vector3 orbitalVelocity = Vector3.Cross(asteroid.transform.position, orbitalAxis).normalized * orbitalSpeed;
                asteroid.transform.Translate(orbitalVelocity * Time.deltaTime, Space.World);
            }
        }
    }

    void RespawnAsteroid(bool initialSpawn)
    {
        Vector3 finalSpawnPosition = Vector3.zero;
        bool positionValid = false;
        int maxAttempts = 100;

        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            if (initialSpawn)
            {
                // Initial spawn within the AOI + Buffer
                finalSpawnPosition = playerTransform.position + Random.insideUnitSphere * (aoiRadius + aoiBuffer);
            }
            else
            {
                // Respawn on the border
                Vector3 randomDirection = Random.onUnitSphere;
                Vector3 spawnPosition = playerTransform.position + randomDirection * aoiRadius;

                Vector3 orbitalAxis = Vector3.up;
                Vector3 orbitalVelocity = Vector3.Cross(spawnPosition, orbitalAxis).normalized;
                Vector3 oppositeDirection = -orbitalVelocity;

                finalSpawnPosition = playerTransform.position + oppositeDirection.normalized * (aoiRadius + aoiBuffer);
            }

            Collider[] colliders = Physics.OverlapSphere(finalSpawnPosition, minAsteroidSpacing);
            if (colliders.Length == 0)
            {
                positionValid = true;
                break;
            }
            if (!initialSpawn)
            {
                Vector3 randomDirection = Random.onUnitSphere;
                Vector3 spawnPosition = playerTransform.position + randomDirection * aoiRadius;

                Vector3 orbitalAxis = Vector3.up;
                Vector3 orbitalVelocity = Vector3.Cross(spawnPosition, orbitalAxis).normalized;
                Vector3 oppositeDirection = -orbitalVelocity;

                oppositeDirection = Quaternion.Euler(0, Random.Range(-60, 60), 0) * oppositeDirection;
            }
        }

        if (positionValid)
        {
            GameObject asteroid = Instantiate(asteroidPrefab, finalSpawnPosition, Random.rotation);
            asteroids.Add(asteroid);
        }
        else
        {
            Debug.LogWarning("Failed to find a valid position for asteroid after " + maxAttempts + " attempts.");
        }
    }
}