using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldDataManager : MonoBehaviour
{
    private WorldStateManager _stateManager;
    private Dictionary<Vector2Int, WorldSpatialData> _persistentSpatialData = new(); // Spatial Data
    private HashSet<Vector2Int> _activeQuadrants = new HashSet<Vector2Int>(); // Quadrants Online
    public Dictionary<Vector2Int, List<GameObject>> _activeObjects = new();
    private Dictionary<Vector2Int, List<GameObject>> _quadrantObjects = new Dictionary<Vector2Int, List<GameObject>>();

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
    [SerializeField] private GameObject azothPrefab;     // Blue channel spawns
    [SerializeField] private GameObject animaPrefab;      // Red channel spawns
    [SerializeField] private GameObject artificaPrefab;   // Green channel spawns

    void Awake()
    {
        _stateManager = GetComponent<WorldStateManager>();
    }

    void Start()
    {
        LoadWorldTexture();
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

    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        string prefabId = obj.name.Split('_')[0]; // Get prefab ID from name
        obj.name = prefabId;
        _objectPools[prefabId].Enqueue(obj);
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
            //else
               // DespawnContent(info.Position);
        }
    }

    private void SpawnAllContent(Vector2Int quadrantPos)
    {
        bool isExplored = _persistentSpatialData.ContainsKey(quadrantPos) && _persistentSpatialData[quadrantPos].IsExplored;

        // Spawn persistent data if it exists and has objects
        if (isExplored && _persistentSpatialData[quadrantPos].Objects != null)
        {
            SpawnExistingQuadrantContent(quadrantPos);
        }

        // Generate new content if the quadrant hasn't been explored yet
        if (!isExplored)
        {
            GenerateNewQuadrantContent(quadrantPos);
            // Immediately spawn the newly generated content
            if (_persistentSpatialData.ContainsKey(quadrantPos) && _persistentSpatialData[quadrantPos].Objects != null)
            {
                SpawnExistingQuadrantContent(quadrantPos);
            }
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

        // Store the initial generated data in persistent storage
        _persistentSpatialData[quadrantPos] = new WorldSpatialData
        {
            Objects = objectsData,
            IsExplored = true // Mark as explored upon initial generation
        };
    }

    private void SpawnFromChannel(byte channelValue, GameObject prefab, Vector3 quadrantCenter, List<GameObjectData> data)
    {
        if (channelValue == 0) return;

        // pick your per‐prefab maximums in one line
        int maxCount = prefab == azothPrefab ? 30   // red channel
                     : prefab == animaPrefab ? 120   // green channel
                     : prefab == artificaPrefab ? 15   // blue channel
                     : 30;

        // scale from 0–255 into 0–maxCount
        int count = Mathf.CeilToInt((channelValue / 255f) * maxCount);

        float halfQ = _stateManager.quadrantSize * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var localOffset = new Vector3(
                Random.Range(-halfQ, halfQ),
                0,
                Random.Range(-halfQ, halfQ)
            );
            var worldPos = quadrantCenter + localOffset;
            var id = GenerateUniqueName(prefab.name, worldPos);

            data.Add(new GameObjectData
            {
                Position = worldPos,
                Rotation = Quaternion.Euler(0, Random.Range(0, 360), 0),
                PrefabID = prefab.name,
                ObjectID = id
            });
        }
    }

    private void SpawnExistingQuadrantContent(Vector2Int quadrantPos)
    {
        if (!_persistentSpatialData.ContainsKey(quadrantPos) || _persistentSpatialData[quadrantPos].Objects == null) return;

        var objectsData = _persistentSpatialData[quadrantPos].Objects;
        if (!_activeObjects.ContainsKey(quadrantPos))
        {
            _activeObjects[quadrantPos] = new List<GameObject>();
        }

        foreach (var objData in objectsData)
        {
            SpawnGameObject(objData, quadrantPos);
        }
    }

    private GameObject GetPooledObject(string prefabId)
    {
        if (!_objectPools.ContainsKey(prefabId))
        {
            Debug.LogError($"No pool found for prefab: {prefabId}");
            return null;
        }

        GameObject obj = null;

        // Try to get an object from the pool
        if (_objectPools[prefabId].Count > 0)
        {
            obj = _objectPools[prefabId].Dequeue();
        }
        // If the pool is empty and we're allowed to expand it, create a new object
        else if (expandPoolWhenNeeded && _prefabLookup.ContainsKey(prefabId))
        {
            GameObject prefab = _prefabLookup[prefabId];
            Transform parentPoolTransform = GetParentPoolTransform(prefab,
                GameObject.Find("POOLS/AZOTH")?.transform,
                GameObject.Find("POOLS/ANIMA")?.transform,
                GameObject.Find("POOLS/ARTIFICA")?.transform);

            if (parentPoolTransform != null)
            {
                obj = InstantiateObject(prefab, parentPoolTransform);
                Debug.LogWarning($"Expanding pool for {prefabId}");
            }
        }

        if (obj != null)
        {
            obj.SetActive(true);
        }

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
    instance.name = objData.ObjectID; // Ensure the GameObject's name is the ObjectID

    // Apply velocity and angular velocity if the object has a Rigidbody
    Rigidbody rb = instance.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.linearVelocity = objData.Velocity;  // Corrected: linearVelocity
        rb.angularVelocity = objData.AngularVelocity;
    }

    // Initialize the WorldObject method on the spawned object
    // Initialize Azoth objects
    if (objData.PrefabID == "Azoth")
    {
        Azoth azothScript = instance.GetComponent<Azoth>();
        if (azothScript != null)
        {
            azothScript.Initialize(quadrantPos, this, objData.ObjectID); // Pass ObjectID
        }
    }
    // Initialize Anima objects
    else if (objData.PrefabID == "Anima")
    {
        Anima animaScript = instance.GetComponent<Anima>();
        if (animaScript != null)
        {
            animaScript.Initialize(quadrantPos, this, objData.ObjectID); // Pass ObjectID
        }
    }

    if (!_activeObjects.ContainsKey(quadrantPos))
    {
        _activeObjects[quadrantPos] = new List<GameObject>();
    }
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

    public void WriteQuadrantData(Vector2Int quadrantPos, GameObject obj, Vector3 velocity, Vector3 angularVelocity)
    {
        if (!_persistentSpatialData.ContainsKey(quadrantPos))
        {
            _persistentSpatialData[quadrantPos] = new WorldSpatialData();
        }
        var data = _persistentSpatialData[quadrantPos].Objects;
        if (data == null)
        {
            data = new List<GameObjectData>();
            _persistentSpatialData[quadrantPos].Objects = data;
        }

        string objectId = obj.name; // Unique identifier for the object
                                    // Remove any existing entries with the same ObjectID to prevent duplicates
        data.RemoveAll(d => d.ObjectID == objectId);

        string prefabId = obj.name.Split('_')[0];
        data.Add(new GameObjectData
        {
            Position = obj.transform.position,
            Rotation = obj.transform.rotation,
            PrefabID = prefabId,
            ObjectID = objectId,
            Velocity = velocity,
            AngularVelocity = angularVelocity
        });
    }

    public void RemoveObjectFromPersistentData(Vector2Int quadrant, string objectId)
    {
        if (_persistentSpatialData.TryGetValue(quadrant, out var data))
        {
            if (data.Objects != null)
            {
                data.Objects.RemoveAll(d => d.ObjectID == objectId);
            }
        }
    }
    public void RemoveObjectFromQuadrant(Vector2Int quadrant, GameObject obj)
    {
        if (_activeObjects.ContainsKey(quadrant))
        {
            _activeObjects[quadrant].Remove(obj);
        }
    }

    // Overload the method for cases where velocity and angular velocity aren't immediately available
    public void WriteQuadrantData(Vector2Int quadrantPos, GameObject obj)
    {
        WriteQuadrantData(quadrantPos, obj, Vector3.zero, Vector3.zero); // Default to zero velocity and angular velocity
    }

    // Overload for just velocity (in case you call it from older parts of your code)
    public void WriteQuadrantData(Vector2Int quadrantPos, GameObject obj, Vector3 velocity)
    {
        WriteQuadrantData(quadrantPos, obj, velocity, Vector3.zero);
    }

    private void DespawnContent(Vector2Int quadrantPos)
    {
        // Writing is now handled by individual WorldObject scripts
        if (_activeObjects.TryGetValue(quadrantPos, out var objects))
        {
            foreach (var obj in objects)
            {
                var worldObject = obj.GetComponent<WorldObject>();
                if (worldObject != null)
                {
                    worldObject.SelfDespawn(); // Tell the object to despawn itself
                }
                else
                {
                    ReturnToPool(obj); // Fallback if WorldObject is missing
                }
            }
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
        if (prefab == azothPrefab)
        {
            return azothParent;
        }
        else if (prefab == animaPrefab)
        {
            return animaParent;
        }
        else if (prefab == artificaPrefab)
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