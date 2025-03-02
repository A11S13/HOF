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

    [SerializeField] private int _activeAzothCount = 0;
    [SerializeField] private int _activeAnimaCount = 0;
    [SerializeField] private int _activeArtificaCount = 0;

    public static bool TexturesLoaded;

    [Header("World Data")]
    public Texture2D worldTexture;
    private Color32[,] _texturePixels;
    private int _textureSize;
    private float _pixelToQuadrantRatio;

    [Header("Prefab References")]
    [SerializeField] private GameObject azothPrefab;    // Blue channel spawns
    [SerializeField] private GameObject animaPrefab;      // Red channel spawns
    [SerializeField] private GameObject artificaPrefab;   // Green channel spawns

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
        Debug.Log($"WORLD TEXTURE: {_textureSize}: px {_pixelToQuadrantRatio}: r");
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

        Debug.Log($"SPATIAL DATA INITIALIZED: {quadrantsX} x {quadrantsY}");
    }

    private void InitializeObjectPools()
    {
        _prefabLookup[animaPrefab.name] = animaPrefab;
        _prefabLookup[artificaPrefab.name] = artificaPrefab;
        _prefabLookup[azothPrefab.name] = azothPrefab;

        // Create a parent objects for all pools
        GameObject poolsParent = new GameObject("POOLS");
        GameObject azothPool = new GameObject("AZOTH");
        GameObject animaPool = new GameObject("ANIMA");
        GameObject artificaPool = new GameObject("ARTIFICA");

        // Set parent relationships
        azothPool.transform.SetParent(poolsParent.transform);
        animaPool.transform.SetParent(poolsParent.transform);
        artificaPool.transform.SetParent(poolsParent.transform);

        // Create initial pools for each prefab
        foreach (var kvp in _prefabLookup)
        {
            string prefabId = kvp.Key;
            GameObject prefab = kvp.Value;

            _objectPools[prefabId] = new Queue<GameObject>();

            // Determine the parent pool
            Transform parentPoolTransform = GetParentPoolTransform(prefab, azothPool.transform, animaPool.transform, artificaPool.transform);

            // Pre-instantiate objects
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject obj = InstantiateObject(prefab, parentPoolTransform); // Pass parent transform
                obj.SetActive(false);
                _objectPools[prefabId].Enqueue(obj);
            }
        }
    }

    private GameObject InstantiateObject(GameObject prefab, Transform parent)
    {
        GameObject obj = Instantiate(prefab, parent);
        return obj;
    }

    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        string prefabId = obj.name;
        int underscoreIndex = obj.name.IndexOf('_');
        prefabId = obj.name.Substring(0, underscoreIndex);
        obj.name = prefabId;
        _objectPools[prefabId].Enqueue(obj);

        // Reset the object state
        obj.SetActive(false);

        switch (prefabId)
        {
            case "Azoth":
                _activeAzothCount--;
                break;
            case "Anima":
                _activeAnimaCount--;
                break;
            case "Artifica":
                _activeArtificaCount--;
                break;
        }
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
    }

    private void GenerateNewQuadrantContent(Vector2Int quadrantPos)
    {
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

    private void SpawnFromChannel(byte channelValue, GameObject prefab, Vector3 quadrantCenter, List<GameObjectData> data)
    {
        if (channelValue == 0) return;

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

            string prefabId = prefab.name;

            data.Add(new GameObjectData
            {
                Position = worldPosition,
                Rotation = Quaternion.Euler(0, Random.Range(0, 360), 0),
                PrefabID = prefabId,
                ObjectID = GenerateUniqueName(prefabId, worldPosition)
            });
        }
    }

    private void SpawnExistingQuadrantContent(Vector2Int quadrantPos)
    {
        if (!_persistentSpatialData.ContainsKey(quadrantPos)) return;
        var objectsData = _persistentSpatialData[quadrantPos].Objects;
        _persistentSpatialData[quadrantPos] = new(); // Clearing Quadrant's Spatial  Data
        foreach (var objData in objectsData)
        {
            SpawnGameObject(objData, quadrantPos);
        }
    }

    private GameObject GetPooledObject(string prefabId)
    {
        GameObject obj = null;

        // Try to get an object from the pool
        if (_objectPools[prefabId].Count > 0)
        {
            obj = _objectPools[prefabId].Dequeue();
        }
        // If the pool is empty and we're allowed to expand it, create a new object
        else if (expandPoolWhenNeeded)
        {
            if (obj != null)
            {
                // Get the parent pool transform
                Transform parentPoolTransform = GetParentPoolTransform(obj,
                    GameObject.Find("AZOTH").transform,
                    GameObject.Find("ANIMA").transform,
                    GameObject.Find("ARTIFICA").transform);

                obj = InstantiateObject(obj, parentPoolTransform); // Pass the parent
                Debug.LogWarning($"Expanding pool for {prefabId}");
            }
        }

        obj.SetActive(true);

        return obj;
    }

    private void SpawnGameObject(GameObjectData objData, Vector2Int quadrantPos)
    {
        // Get object from pool instead of instantiating
        GameObject instance = GetPooledObject(objData.PrefabID);
        if (instance == null) return;

        // Set position and rotation
        instance.transform.position = objData.Position;
        instance.transform.rotation = objData.Rotation;
        instance.name = objData.ObjectID;

        if (!_activeObjects.ContainsKey(quadrantPos))
            _activeObjects[quadrantPos] = new List<GameObject>();

        _activeObjects[quadrantPos].Add(instance);

        string prefabId = objData.PrefabID;

        switch (prefabId)
        {
            case "Azoth":
                _activeAzothCount++;
                break;
            case "Anima":
                _activeAnimaCount++;
                break;
            case "Artifica":
                _activeArtificaCount++;
                break;
        }
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

        // Check all active objects across all quadrants
        foreach (var kvp in _activeObjects)
        {
            foreach (var obj in kvp.Value)
            {
                if (obj != null)
                {
                    Vector3 pos = obj.transform.position;

                    int underscoreIndex = obj.name.IndexOf('_');
                    string prefabId = obj.name.Substring(0, underscoreIndex);

                    // Check if object position falls within this quadrant's boundaries
                    if (pos.x >= minX && pos.x < maxX &&
                        pos.z >= minZ && pos.z < maxZ)
                    {
                        objectsData.Add(new GameObjectData
                        {
                            Position = pos,
                            Rotation = obj.transform.rotation,
                            PrefabID = prefabId,
                            ObjectID = obj.name
                        });
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

    private Transform GetParentPoolTransform(GameObject prefab, Transform azothParent, Transform animaParent, Transform artificaParent)
    {
        if (azothPrefab = prefab)
        {
            return azothParent;
        }
        else if (animaPrefab = prefab)
        {
            return animaParent;
        }
        else if (artificaPrefab = prefab)
        {
            return artificaParent;
        }
        return null;
    }

    private string GenerateUniqueName(string baseName, Vector3 position)
    {
        // Generate deterministic hex suffix based on position
        int hashCode = position.GetHashCode();
        string hexSuffix = hashCode.ToString("X8");

        // Ensure name format like "Azoth_U203kLM3"
        return $"{baseName}_{hexSuffix}";
    }

    // Clear all pools on destroy
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