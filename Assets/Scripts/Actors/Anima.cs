using UnityEngine;
using System.Collections; // Make sure to include this for Coroutines

public class Anima : MonoBehaviour
{
    public bool isTargeted;
    public bool isDestroyed;
    public float health = 100f;
    public float animaSpeedMultiplierMin = 1f; // Minimum speed multiplier
    public float animaSpeedMultiplierMax = 1.5f; // Maximum speed multiplier
    [HideInInspector] public float animaSpeedMultiplier; // Individual speed multiplier
    public static GameObject explosionPrefab;
    public static GameObject explosionSoundHolderPrefab;
    private WorldDataManager _dataManager;
    private Vector2Int _currentQuadrant;
    private Transform _playerTransform;
    private string _objectId;

    [Header("Despawn Optimization")]
    public int minDespawnCheckInterval = 3;
    public int maxDespawnCheckInterval = 10;
    private int _frameCounter = 0;
    private int _currentCheckInterval;

    // Data updated by the job.
    [System.Serializable]
    public struct AnimaData
    {
        public int rotationCounter;
        public int rotationFrequency;
        public Quaternion targetRotation;
    }
    public AnimaData data;

    public void Initialize(Vector2Int startQuadrant, WorldDataManager dataManager, string objectId)
    {
        _currentQuadrant = startQuadrant;
        _dataManager = dataManager;
        _playerTransform = dataManager.GetComponent<WorldStateManager>()?.Player.transform;
        _objectId = objectId;

        if (_playerTransform != null)
        {
            SelfDespawnCheck();
        }
    }

    private void Update()
    {
        _frameCounter++;
        if (_frameCounter >= _currentCheckInterval)
        {
            _frameCounter = 0;
            _currentCheckInterval = Random.Range(minDespawnCheckInterval, maxDespawnCheckInterval + 1);
            SelfDespawnCheck();
        }
    }

    void OnEnable()
    {
        // Reset state when the object is enabled (reused from pool)
        health = 100f;
        isDestroyed = false;
        isTargeted = false;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.enabled = true;

        // Generate a random speed multiplier with a small variance (1 to 1.5)
        animaSpeedMultiplier = Random.Range(animaSpeedMultiplierMin, animaSpeedMultiplierMax);
        // You can optionally log this value for debugging:
        // Debug.Log($"Anima {gameObject.GetInstanceID()} speed multiplier: {animaSpeedMultiplier}");

        // Existing OnEnable code...
        if (explosionPrefab == null)
            explosionPrefab = Resources.Load<GameObject>("Explode");
        if (explosionSoundHolderPrefab == null)
            explosionSoundHolderPrefab = Resources.Load<GameObject>("ExplosionSoundHolder");

        data.rotationFrequency = Random.Range(1, 300);
        data.rotationCounter = 0;
        data.targetRotation = transform.rotation;

        AnimaManager.Instance.RegisterAnima(this);
    }

    void OnDestroy()
    {
        // Unregister if this anima is destroyed.
        if (AnimaManager.Instance != null)
            AnimaManager.Instance.UnregisterAnima(this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            //Debug.Log("Player collided with an anima!");
            TakeDamage(100f);
        }
        else if (other.CompareTag("Projectile"))
        {
            // Assume your projectile has a GetDamage() method.
            float damage = other.GetComponent<Projectile>()?.GetDamage() ?? 0;
            TakeDamage(damage);
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0)
        {
            // Disable collider before despawning
            GetComponent<Collider>().enabled = false;
            isDestroyed = true;
            Explode();
        }
    }

    void Explode()
    {
        // Calculate the current quadrant before returning to pool
        Vector2Int currentObjectQuadrant = GetCurrentQuadrant();

        // Remove from the active objects of the current quadrant upon destruction
        if (_dataManager != null)
        {
            _dataManager.RemoveObjectFromQuadrant(currentObjectQuadrant, gameObject);
            _dataManager.RemoveObjectFromQuadrant(_currentQuadrant, gameObject);
            SelfDespawn();
        }

        GameObject explosion = Instantiate(
            explosionPrefab,
            transform.position,
            Quaternion.identity
        );
        // Instantiate soundHolder at the player's position
        GameObject soundHolder = Instantiate(
            explosionSoundHolderPrefab,
            this.transform.position,
            Quaternion.identity
        );
        AudioSource soundHolderAudioSource = soundHolder.GetComponent<AudioSource>();
        if (soundHolderAudioSource != null)
        {
            double playTime = AudioSettings.dspTime + 0.1;
            soundHolderAudioSource.PlayScheduled(playTime);
        }
        Destroy(explosion, 3f);
        Destroy(soundHolder, 1f);
    }

    private void SelfDespawnCheck()
    {
            if (_playerTransform != null && _dataManager != null)
            {
                Vector2Int currentObjectQuadrant = GetCurrentQuadrant();
                bool withinActiveQuadrant = IsWithinActiveQuadrants(currentObjectQuadrant);

                Rigidbody rb = GetComponent<Rigidbody>();
                Vector3 currentVelocity = Vector3.zero;
                Vector3 currentAngularVelocity = Vector3.zero;

                if (!withinActiveQuadrant)
                {
                    if (currentObjectQuadrant != _currentQuadrant)
                    {
                        // Remove from old quadrant's persistent data
                        _dataManager.RemoveObjectFromPersistentData(_currentQuadrant, _objectId);
                        // Write to new quadrant
                        _dataManager.WriteQuadrantData(currentObjectQuadrant, gameObject);
                        // Remove from old quadrant's active objects
                        _dataManager.RemoveObjectFromQuadrant(_currentQuadrant, gameObject);

                        _currentQuadrant = currentObjectQuadrant;
                    }

                    // Despawn the object
                    _dataManager.ReturnToPool(gameObject);
                }
            }
    }

    private bool IsWithinActiveQuadrants(Vector2Int objectQuadrant)
    {
        if (_dataManager != null && _dataManager.GetComponent<WorldProcessor>() != null)
        {
            var activeQuadrants = _dataManager.GetComponent<WorldProcessor>()._currentActiveQuadrants;
            return activeQuadrants.Contains(objectQuadrant);
        }
        return false;
    }

    public void SelfDespawn()
    {
        if (_dataManager != null)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            Vector3 currentVelocity = Vector3.zero;
            Vector3 currentAngularVelocity = Vector3.zero;

            if (rb != null)
            {
                currentVelocity = rb.linearVelocity;
                currentAngularVelocity = rb.angularVelocity;
            }

            // Calculate which quadrant the object is now in
            Vector2Int currentObjectQuadrant = GetCurrentQuadrant();

            // Remove from the persistent data of the current quadrant BEFORE returning to pool
            _dataManager.RemoveObjectFromPersistentData(currentObjectQuadrant, _objectId);

            // DO NOT remove from active objects here. This is handled in Explode().
            _dataManager.RemoveObjectFromQuadrant(currentObjectQuadrant, gameObject);

            _dataManager.ReturnToPool(gameObject);
        }
    }

    private Vector2Int GetCurrentQuadrant()
    {
        if (_dataManager == null || _dataManager.GetComponent<WorldStateManager>() == null)
        {
            Debug.LogError("WorldDataManager or WorldStateManager is null!");
            return _currentQuadrant; // Return the last known quadrant as a fallback
        }

        var stateManager = _dataManager.GetComponent<WorldStateManager>();
        float quadrantSize = stateManager.quadrantSize;
        Vector3 positionInCluster = transform.position + new Vector3(stateManager.clusterBounds.x / 2f, 0f, stateManager.clusterBounds.y / 2f); // Adjust for cluster origin

        int x = Mathf.FloorToInt(positionInCluster.x / quadrantSize);
        int y = Mathf.FloorToInt(positionInCluster.z / quadrantSize);

        // Clamp values to ensure they are within the valid quadrant range
        int maxX = Mathf.FloorToInt(stateManager.clusterBounds.x / quadrantSize);
        int maxY = Mathf.FloorToInt(stateManager.clusterBounds.y / quadrantSize);

        x = Mathf.Clamp(x, 0, maxX - 1);
        y = Mathf.Clamp(y, 0, maxY - 1);

        return new Vector2Int(x, y);
    }
}