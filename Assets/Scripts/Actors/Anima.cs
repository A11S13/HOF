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

    // Cached alignment
    [HideInInspector] public bool isFriendly;
    private Alignment _alignmentCache;

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

        // Ensure the object ID is applied to the name so WriteQuadrantData works correctly
        gameObject.name = _objectId;

        if (_playerTransform == null)
        {
            var stateManager = dataManager?.GetComponent<WorldStateManager>();
            _playerTransform = stateManager?.Player;
        }

        if (_alignmentCache == null)
            _alignmentCache = GetComponent<Alignment>();
    }

    public void RefreshAlignment()
    {
        if (_alignmentCache == null)
            _alignmentCache = GetComponent<Alignment>();

        isFriendly = _alignmentCache != null && _alignmentCache.isFriendly;
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

        RefreshAlignment();

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
        bool wasFriendly = isFriendly;

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

        GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        GameObject soundHolder = Instantiate(explosionSoundHolderPrefab, this.transform.position, Quaternion.identity);

        AudioSource soundHolderAudioSource = soundHolder.GetComponent<AudioSource>();
        if (soundHolderAudioSource != null)
        {
            double playTime = AudioSettings.dspTime + 0.1;
            soundHolderAudioSource.PlayScheduled(playTime);
        }
        Destroy(explosion, 3f);
        Destroy(soundHolder, 1f);
    }

    public bool CheckDespawn(WorldDataManager dataManager, WorldProcessor worldProcessor)
    {
        if (_playerTransform == null && dataManager != null)
        {
            var stateManager = dataManager.GetComponent<WorldStateManager>();
            _playerTransform = stateManager?.Player;
        }

        if (_playerTransform == null || dataManager == null || worldProcessor == null)
        {
            return false;
        }

        Vector2Int currentObjectQuadrant = GetCurrentQuadrant();
        bool withinActiveQuadrant = worldProcessor._currentActiveQuadrants.Contains(currentObjectQuadrant);

        if (!withinActiveQuadrant)
        {
            // FIX: Ensure name is correct before writing data (crucial for ID mapping)
            if (string.IsNullOrEmpty(gameObject.name) || gameObject.name != _objectId)
                gameObject.name = _objectId;

            // 1. Remove from OLD persistence location
            // If we moved quadrants, we remove from the old one.
            // If we are in the same quadrant, we overwrite anyway, so explicit removal helps cleanup duplicates.
            dataManager.RemoveObjectFromPersistentData(_currentQuadrant, _objectId);

            // 2. Write to NEW (or Current) persistence location
            // This saves the current position/rotation before the object vanishes
            dataManager.WriteQuadrantData(currentObjectQuadrant, gameObject);

            // 3. Remove from Active Lists
            dataManager.RemoveObjectFromQuadrant(_currentQuadrant, gameObject);
            if (currentObjectQuadrant != _currentQuadrant)
            {
                dataManager.RemoveObjectFromQuadrant(currentObjectQuadrant, gameObject);
            }

            // Update internal state
            _currentQuadrant = currentObjectQuadrant;

            // 4. Despawn
            dataManager.ReturnToPool(gameObject);
            return true;
        }

        return false;
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

        // Safety fallback
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
