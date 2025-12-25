using UnityEngine;

public class Anima : MonoBehaviour
{
    public bool isTargeted;
    public bool isDestroyed;
    public float animaSpeedMultiplierMin = 1f;
    public float animaSpeedMultiplierMax = 1.5f;
    [HideInInspector] public float animaSpeedMultiplier;
    public static GameObject explosionPrefab;
    public static GameObject explosionSoundHolderPrefab;
    private WorldDataManager _dataManager;
    private Vector2Int _currentQuadrant;
    private Transform _playerTransform;
    private string _objectId;

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
        _objectId = objectId;

        // Ensure player transform is always set
        if (_playerTransform == null)
        {
            var stateManager = dataManager?.GetComponent<WorldStateManager>();
            _playerTransform = stateManager?.Player;
        }
    }

    void OnEnable()
    {
        isDestroyed = false;
        isTargeted = false;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.enabled = true;

        animaSpeedMultiplier = Random.Range(animaSpeedMultiplierMin, animaSpeedMultiplierMax);

        if (explosionPrefab == null)
            explosionPrefab = Resources.Load<GameObject>("Explode");
        if (explosionSoundHolderPrefab == null)
            explosionSoundHolderPrefab = Resources.Load<GameObject>("ExplosionSoundHolder");

        data.rotationFrequency = Random.Range(1, 300);
        data.rotationCounter = 0;
        data.targetRotation = transform.rotation;

        AnimaManager.Instance?.RegisterAnima(this);
    }

    void OnDestroy()
    {
        if (AnimaManager.Instance != null)
            AnimaManager.Instance.UnregisterAnima(this);
    }

    void OnDeath()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        Explode();
    }

    void Explode()
    {
        var alignment = GetComponent<Alignment>();
        bool wasFriendly = alignment && alignment.isFriendly;

        EventBus.Instance.Publish(
            new AnimaKilledEvent(transform.position, wasFriendly)
        );

        Vector2Int currentObjectQuadrant = GetCurrentQuadrant();

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

    /// <summary>
    /// Called by AnimaManager to check if this anima should despawn
    /// </summary>
    public bool CheckDespawn(WorldDataManager dataManager, WorldProcessor worldProcessor)
    {
        // Ensure we have player reference
        if (_playerTransform == null && dataManager != null)
        {
            var stateManager = dataManager.GetComponent<WorldStateManager>();
            _playerTransform = stateManager?.Player;
        }

        if (_playerTransform == null || dataManager == null || worldProcessor == null)
        {
            return false; // Can't check, don't despawn
        }

        Vector2Int currentObjectQuadrant = GetCurrentQuadrant();
        bool withinActiveQuadrant = worldProcessor._currentActiveQuadrants.Contains(currentObjectQuadrant);

        if (!withinActiveQuadrant)
        {
            // Update persistent data if quadrant changed
            if (currentObjectQuadrant != _currentQuadrant)
            {
                dataManager.RemoveObjectFromPersistentData(_currentQuadrant, _objectId);
                dataManager.WriteQuadrantData(currentObjectQuadrant, gameObject);
                dataManager.RemoveObjectFromQuadrant(_currentQuadrant, gameObject);
                _currentQuadrant = currentObjectQuadrant;
            }

            // Despawn the object
            dataManager.ReturnToPool(gameObject);
            return true; // Was despawned
        }

        return false; // Still active
    }

    public void SelfDespawn()
    {
        if (_dataManager != null)
        {
            Vector2Int currentObjectQuadrant = GetCurrentQuadrant();
            _dataManager.RemoveObjectFromPersistentData(currentObjectQuadrant, _objectId);
            _dataManager.RemoveObjectFromQuadrant(currentObjectQuadrant, gameObject);
            _dataManager.ReturnToPool(gameObject);
        }
    }

    public Vector2Int GetCurrentQuadrant()
    {
        if (_dataManager == null)
        {
            _dataManager = FindFirstObjectByType<WorldDataManager>();
        }

        if (_dataManager == null || _dataManager.GetComponent<WorldStateManager>() == null)
        {
            return _currentQuadrant;
        }

        var stateManager = _dataManager.GetComponent<WorldStateManager>();
        float quadrantSize = stateManager.quadrantSize;
        Vector3 positionInCluster = transform.position + new Vector3(stateManager.clusterBounds.x / 2f, 0f, stateManager.clusterBounds.y / 2f);

        int x = Mathf.FloorToInt(positionInCluster.x / quadrantSize);
        int y = Mathf.FloorToInt(positionInCluster.z / quadrantSize);

        int maxX = Mathf.FloorToInt(stateManager.clusterBounds.x / quadrantSize);
        int maxY = Mathf.FloorToInt(stateManager.clusterBounds.y / quadrantSize);

        x = Mathf.Clamp(x, 0, maxX - 1);
        y = Mathf.Clamp(y, 0, maxY - 1);

        return new Vector2Int(x, y);
    }
}
