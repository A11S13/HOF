using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldDataManager : MonoBehaviour
{
    private WorldStateManager _stateManager;
    private WorldSpatialData[,] SpatialData; // Array of lists of structures
    private Dictionary<Vector2Int, WorldSpatialData> _persistentSpatialData = new(); // Spatial Data
    private HashSet<Vector2Int> _activeQuadrants = new HashSet<Vector2Int>(); // Quadrants Online
    private Dictionary<Vector2Int, List<GameObject>> _activeObjects = new();

    // Object pooling dictionaries
    private Dictionary<string, Queue<GameObject>> _objectPools = new();
    private Dictionary<string, GameObject> _prefabLookup = new();

    [Header("Object Pooling")]
    [SerializeField] private int initialPoolSize = 20; // Initial number of objects to instantiate per prefab
    [SerializeField] private bool expandPoolWhenNeeded = true;

    public static bool TexturesLoaded;

    [Header("World Data")]
    public Texture2D worldTexture;
    private Color32[,] _texturePixels;
    private int _textureSize;
    private float _pixelToQuadrantRatio;

    [Header("Prefab References")]
    [SerializeField] private GameObject[] azothPrefab;    // Blue channel spawns
    [SerializeField] private GameObject[] animaPrefab;      // Red channel spawns
    [SerializeField] private GameObject[] artificaPrefab;   // Green channel spawns

    void Awake()
    {
        _stateManager = GetComponent<WorldStateManager>();
    }

    void Start()
    {
        LoadWorldTexture();
        InitializeSpatialData();
        InitializeObjectPools();
    }

    private void LoadWorldTexture()
    {
        if (worldTexture == null) return;

        _textureSize = worldTexture.width;
        _texturePixels = new Color32[_textureSize, _textureSize];

        for (int x = 0; x < _textureSize; x++)
            for (int y = 0; y < _textureSize; y++)
                _texturePixels[x, y] = worldTexture.GetPixel(x, y);

        _pixelToQuadrantRatio = _stateManager.clusterBounds.x / _textureSize;
        TexturesLoaded = true;
        //Debug.Log($"WORLD TEXTURE: {_textureSize}: px {_pixelToQuadrantRatio}: r");
    }

    private void InitializeSpatialData()
    {
        int quadrantsX = Mathf.CeilToInt(_stateManager.clusterBounds.x / _stateManager.quadrantSize);
        int quadrantsY = Mathf.CeilToInt(_stateManager.clusterBounds.y / _stateManager.quadrantSize);

        SpatialData = new WorldSpatialData[quadrantsX, quadrantsY];

        // Initialize all elements
        for (int x = 0; x < quadrantsX; x++)
            for (int y = 0; y < quadrantsY; y++)
                SpatialData[x, y] = new WorldSpatialData();

        //Debug.Log($"SPATIAL DATA INITIALIZED: {quadrantsX} x {quadrantsY}");
    }

    private void InitializeObjectPools()
    {
        // Add all prefabs to the lookup dictionary
        foreach (var prefab in animaPrefab)
            _prefabLookup[prefab.name] = prefab;

        foreach (var prefab in artificaPrefab)
            _prefabLookup[prefab.name] = prefab;

        foreach (var prefab in azothPrefab)
            _prefabLookup[prefab.name] = prefab;

        // Create initial pools for each prefab
        foreach (var kvp in _prefabLookup)
        {
            string prefabId = kvp.Key;
            GameObject prefab = kvp.Value;

            _objectPools[prefabId] = new Queue<GameObject>();

            // Pre-instantiate objects
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject obj = InstantiateObject(prefab);
                obj.SetActive(false);
                _objectPools[prefabId].Enqueue(obj);
            }
        }

        Debug.Log($"Initialized object pools for {_objectPools.Count} prefab types");
    }

    private GameObject InstantiateObject(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);
        obj.name = prefab.name; // Remove the "(Clone)" suffix
        return obj;
    }

    private GameObject GetPooledObject(string prefabId)
    {
        // Create pool if it doesn't exist (shouldn't happen if InitializeObjectPools is called)
        if (!_objectPools.ContainsKey(prefabId))
        {
            Debug.LogWarning($"Creating pool for {prefabId} on demand");
            GameObject prefab = GetPrefabById(prefabId);
            if (prefab == null) return null;

            _objectPools[prefabId] = new Queue<GameObject>();
        }

        GameObject obj = null;

        // Try to get an object from the pool
        if (_objectPools[prefabId].Count > 0)
        {
            obj = _objectPools[prefabId].Dequeue();
        }
        // If the pool is empty and we're allowed to expand it, create a new object
        else if (expandPoolWhenNeeded)
        {
            GameObject prefab = GetPrefabById(prefabId);
            if (prefab != null)
            {
                obj = InstantiateObject(prefab);
                Debug.LogWarning($"Expanding pool for {prefabId}");
            }
        }

        if (obj != null)
        {
            obj.SetActive(true);
        }

        return obj;
    }

    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        string prefabId = obj.name.Replace("(Clone)", "").Trim();

        // Reset the object state
        obj.SetActive(false);

        // Add back to the pool
        if (!_objectPools.ContainsKey(prefabId))
            _objectPools[prefabId] = new Queue<GameObject>();

        _objectPools[prefabId].Enqueue(obj);
    }

    public void ProcessQuadrants(List<QuadrantProcessInfo> quadrantsToProcess)
    {
        Debug.Log($"PROCESSING {quadrantsToProcess.Count} QUADRANTS...");
        foreach (var info in quadrantsToProcess)
        {
            if (info.ShouldLoad)
                SpawnAllContent(info.Position);
            else
                DespawnContent(info.Position);
        }
    }

    private void SpawnAllContent(Vector2Int quadrantPos)
    {
        //Debug.Log("Spawning ALL the SHIT!");
        if (_persistentSpatialData.ContainsKey(quadrantPos))
        {
            // Use existing data for explored quadrants
            SpawnExistingQuadrantContent(quadrantPos);
        }
        else
        {
            // Generate new content for unexplored quadrants
            GenerateNewQuadrantContent(quadrantPos);
            SpawnExistingQuadrantContent(quadrantPos);
        }

        _activeQuadrants.Add(quadrantPos);
        //Debug.Log("SPAWNED!");
    }

    private void GenerateNewQuadrantContent(Vector2Int quadrantPos)
    {
        //Debug.Log("GENERATING SHIT...");
        Vector3 quadrantCenter = GetQuadrantCenter(quadrantPos);
        var texturePos = WorldToTexturePosition(quadrantCenter);

        if (!TexturesLoaded)
        {
            Debug.LogError("Texture not loaded!");
            return;
        }

        Color32 pixel = _texturePixels[texturePos.x, texturePos.y];
        List<GameObjectData> objectsData = new List<GameObjectData>();

        if (pixel.a > 127)
        {
            SpawnFromChannel(pixel.r, azothPrefab, quadrantCenter, objectsData);
            SpawnFromChannel(pixel.g, animaPrefab, quadrantCenter, objectsData);
            SpawnFromChannel(pixel.b, artificaPrefab, quadrantCenter, objectsData);
        }

        // Store in persistent storage
        _persistentSpatialData[quadrantPos] = new WorldSpatialData
        {
            Objects = objectsData,
            IsExplored = true
        };
    }

    private void SpawnExistingQuadrantContent(Vector2Int quadrantPos)
    {
        if (!_persistentSpatialData.ContainsKey(quadrantPos)) return;
        var objectsData = _persistentSpatialData[quadrantPos].Objects;
        foreach (var objData in objectsData)
        {
            SpawnGameObject(objData, quadrantPos);
        }
        //Debug.Log($"Spawned: {quadrantPos}");
    }

    private void WriteQuadrant(Vector2Int quadrantPos)
    {
        List<GameObjectData> objectsData = new List<GameObjectData>();
        Vector3 quadrantCenter = GetQuadrantCenter(quadrantPos);
        float halfQuadrantSize = _stateManager.quadrantSize / 2f;

        // Calculate quadrant boundaries
        float minX = quadrantCenter.x - halfQuadrantSize;
        float maxX = quadrantCenter.x + halfQuadrantSize;
        float minZ = quadrantCenter.z - halfQuadrantSize;
        float maxZ = quadrantCenter.z + halfQuadrantSize;

        Debug.Log($"Writing {quadrantPos} quadrant: bounds ({minX},{minZ}) to ({maxX},{maxZ})");

        // Check all active objects across all quadrants
        foreach (var kvp in _activeObjects)
        {
            foreach (var obj in kvp.Value)
            {
                if (obj != null)
                {
                    Vector3 pos = obj.transform.position;

                    // Check if object position falls within this quadrant's boundaries
                    if (pos.x >= minX && pos.x < maxX &&
                        pos.z >= minZ && pos.z < maxZ)
                    {
                        objectsData.Add(new GameObjectData
                        {
                            Position = pos,
                            Rotation = obj.transform.rotation,
                            PrefabID = obj.name.Replace("(Clone)", "").Trim()
                        });
                        //Debug.Log($"Added {obj.name.Replace("(Clone)", "").Trim()} at {pos} to quadrant {quadrantPos}");
                    }
                }
            }
        }

        // Update persistent storage with current state
        _persistentSpatialData[quadrantPos] = new WorldSpatialData
        {
            Objects = objectsData,
            IsExplored = true
        };
    }

    private void SpawnGameObject(GameObjectData objData, Vector2Int quadrantPos)
    {
        // Get object from pool instead of instantiating
        GameObject instance = GetPooledObject(objData.PrefabID);
        if (instance == null) return;

        // Set position and rotation
        instance.transform.position = objData.Position;
        instance.transform.rotation = objData.Rotation;

        if (!_activeObjects.ContainsKey(quadrantPos))
            _activeObjects[quadrantPos] = new List<GameObject>();

        _activeObjects[quadrantPos].Add(instance);
    }

    private void SpawnFromChannel(byte channelValue, GameObject[] prefabs, Vector3 quadrantCenter, List<GameObjectData> data)
    {
        if (channelValue == 0 || prefabs.Length == 0) return;

        int count = Mathf.CeilToInt(channelValue / 255f * 30); // Max count per channel
        float halfQuadrant = _stateManager.quadrantSize / 2f;

        for (int i = 0; i < count; i++)
        {
            // Calculate local offset within quadrant
            Vector3 localOffset = new Vector3(
                Random.Range(-halfQuadrant, halfQuadrant),
                0,
                Random.Range(-halfQuadrant, halfQuadrant)
            );

            // Since quadrantCenter is already in correct world space, just add offset
            Vector3 worldPosition = quadrantCenter + localOffset;

            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            data.Add(new GameObjectData
            {
                Position = worldPosition,
                Rotation = Quaternion.Euler(0, Random.Range(0, 360), 0),
                PrefabID = prefab.name
            });
        }
    }

    private void DespawnContent(Vector2Int quadrantPos)
    {
        WriteQuadrant(quadrantPos);

        if (_activeObjects.TryGetValue(quadrantPos, out var objects))
        {
            foreach (var obj in objects)
                ReturnToPool(obj); // Return to pool instead of destroying

            _activeObjects.Remove(quadrantPos);
        }

        _activeQuadrants.Remove(quadrantPos);
    }

    private Vector2Int WorldToTexturePosition(Vector3 worldPosition)
    {
        // Convert world coordinates to normalized cluster space [0,1]
        float normalizedX = Mathf.InverseLerp(
            -_stateManager.clusterBounds.x / 2,
            _stateManager.clusterBounds.x / 2,
            worldPosition.x
        );

        float normalizedY = Mathf.InverseLerp(
            -_stateManager.clusterBounds.y / 2,
            _stateManager.clusterBounds.y / 2,
            worldPosition.z
        );

        // Convert to texture coordinates
        int texX = Mathf.FloorToInt(normalizedX * (_textureSize - 1));
        int texY = Mathf.FloorToInt(normalizedY * (_textureSize - 1));

        return new Vector2Int(
            Mathf.Clamp(texX, 0, _textureSize - 1),
            Mathf.Clamp(texY, 0, _textureSize - 1)
        );
    }

    private Vector3 GetQuadrantCenter(Vector2Int quadrantPos)
    {
        // Convert quadrant grid position to world space with cluster offset
        return new Vector3(
            (quadrantPos.x * _stateManager.quadrantSize) - (_stateManager.clusterBounds.x / 2) + (_stateManager.quadrantSize / 2f),
            0,
            (quadrantPos.y * _stateManager.quadrantSize) - (_stateManager.clusterBounds.y / 2) + (_stateManager.quadrantSize / 2f)
        );
    }

    // Returns a prefab by matching its name with the given prefabId.
    private GameObject GetPrefabById(string prefabId)
    {
        // Use prefab lookup dictionary
        if (_prefabLookup.TryGetValue(prefabId, out GameObject prefab))
            return prefab;

        // Fallback to original method if not found in dictionary
        foreach (var p in animaPrefab)
            if (p.name == prefabId)
                return p;

        foreach (var p in artificaPrefab)
            if (p.name == prefabId)
                return p;

        foreach (var p in azothPrefab)
            if (p.name == prefabId)
                return p;

        return null;
    }

    public void SaveClusterData(ClusterTransitionEvent clusterExitEvent)
    {
        if (!_stateManager.IsInMappedWorld && _stateManager.WasInMappedWorld)
        {
            // Only cleanup objects, spatial data remains in _persistentSpatialData
            var cleanupQuadrants = _activeQuadrants
            .Select(q => new QuadrantProcessInfo(q, false))
            .ToList();

            ProcessQuadrants(cleanupQuadrants);
        }
    }

    public void LoadClusterData(ClusterTransitionEvent clusterEnterEvent)
    {
        if (_stateManager.IsInMappedWorld && !_stateManager.WasInMappedWorld)
        {
            Debug.Log("doing shit");
            // TODO: Actual spatial data implementation
        }
    }

    private void DebugLogQuadrantInfo(Vector2Int quadrantPos, Vector3 worldPos, Vector2Int texturePos)
    {
        Debug.Log($"Quadrant: {quadrantPos}, " +
                  $"World Pos: {worldPos.ToString("F2")}, " +
                  $"Texture Pos: {texturePos}, " +
                  $"Pixel Color: {_texturePixels[texturePos.x, texturePos.y]}");
    }

    // Optional: Clear all pools on destroy
    private void OnDestroy()
    {
        foreach (var pool in _objectPools.Values)
        {
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                    Destroy(obj);
            }
        }
        _objectPools.Clear();
    }
}