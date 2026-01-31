using UnityEngine;

public class Artifica : MonoBehaviour
{
    [Header("Ship Identity")]
    public string unitTech;
    public string unitType;
    public string unitClass;
    public string unitRole;
    public string unitName;

    [Header("Destruction Assets")]
    public GameObject explosionPrefab;
    public GameObject explosionSoundHolderPrefab;

    private WorldDataManager _dataManager;
    private string _objectId;
    private bool _isDestroyed;

    void Start()
    {
        // Safe ID assignment: Use instance ID if name is empty to prevent keys being null
        _objectId = string.IsNullOrEmpty(gameObject.name) ? gameObject.GetInstanceID().ToString() : gameObject.name;
        _dataManager = Object.FindFirstObjectByType<WorldDataManager>();
    }

    void OnEnable()
    {
        _isDestroyed = false;

        // Lazy load resources if not assigned in Inspector
        if (explosionPrefab == null)
            explosionPrefab = Resources.Load<GameObject>("Explode");
        if (explosionSoundHolderPrefab == null)
            explosionSoundHolderPrefab = Resources.Load<GameObject>("ExplosionSoundHolder");
    }

    /// <summary>
    /// Triggered by Health.cs via SendMessage("OnDeath")
    /// </summary>
    public void OnDeath()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;

        // 1. VISUALS: Shatter the mesh into debris first!
        // This ensures debris appears even if the hit was an "overkill" (one-shot).
        if (TryGetComponent<Damageable>(out var dmg))
        {
            dmg.ShatterAllRemaining();
        }

        // 2. FX: Spawn the explosion prefab
        HandleShipExplosion();

        // 3. LOGIC: Remove from World/Pool
        RemoveFromWorldData();
    }

    private void HandleShipExplosion()
    {
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 3f);
        }

        if (explosionSoundHolderPrefab != null)
        {
            GameObject sound = Instantiate(explosionSoundHolderPrefab, transform.position, Quaternion.identity);
            AudioSource audio = sound.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.PlayScheduled(AudioSettings.dspTime + 0.1);
            }
            Destroy(sound, 1f);
        }
    }

    private void RemoveFromWorldData()
    {
        // If no manager exists (e.g. test scene), just destroy
        if (_dataManager == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector2Int currentQuad = GetCurrentQuadrant();

        // 1. Remove from Persistent Save Data
        // This is safe to call even if key doesn't exist
        _dataManager.RemoveObjectFromPersistentData(currentQuad, _objectId);
        _dataManager.RemoveObjectFromQuadrant(currentQuad, gameObject);

        // 2. Try to Return to Pool
        try
        {
            // If this is a Manual Ship (dragged into scene), it won't be in the pool dictionary.
            // This Try-Catch prevents the "KeyNotFoundException" crash.
            _dataManager.ReturnToPool(gameObject);
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            // Not in pool? Just destroy it manually.
            Debug.Log($"[Artifica] '{gameObject.name}' was not in the Pool. Destroying manually.");
            Destroy(gameObject);
        }
    }

    private Vector2Int GetCurrentQuadrant()
    {
        var stateManager = _dataManager?.GetComponent<WorldStateManager>();
        if (stateManager == null) return Vector2Int.zero;

        float qSize = stateManager.quadrantSize;
        Vector3 offsetPos = transform.position + new Vector3(stateManager.clusterBounds.x / 2f, 0f, stateManager.clusterBounds.y / 2f);

        int x = Mathf.Clamp(Mathf.FloorToInt(offsetPos.x / qSize), 0, Mathf.FloorToInt(stateManager.clusterBounds.x / qSize) - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(offsetPos.z / qSize), 0, Mathf.FloorToInt(stateManager.clusterBounds.y / qSize) - 1);

        return new Vector2Int(x, y);
    }
}