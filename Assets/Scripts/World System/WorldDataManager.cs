using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldDataManager : MonoBehaviour
{
    private WorldStateManager _stateManager;
    private WorldSpatialData[,] SpatialData;
    private HashSet<Vector2Int> _activeQuadrants = new HashSet<Vector2Int>();
    public static bool TexturesLoaded;

    [Header("World Data")]
    public Texture2D worldTexture;
    public GameObject quadrantMarkerPrefab;
    private Color32[,] _texturePixels;
    private int _textureSize;
    private float _pixelToQuadrantRatio;

    [Header("Prefab References")]
    [SerializeField] private GameObject[] enemyPrefabs;      // Red channel spawns
    [SerializeField] private GameObject[] artifactPrefabs;   // Green channel spawns
    [SerializeField] private GameObject[] remnantPrefabs;    // Blue channel spawns

    void Awake()
    {
        _stateManager = GetComponent<WorldStateManager>();
    }
    void Start()
    {
        LoadWorldTexture();
        InitializeSpatialData();
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
        }

        _activeQuadrants.Add(quadrantPos);
        //Debug.Log("SPAWNED!");
    }

    private Dictionary<Vector2Int, WorldSpatialData> _persistentSpatialData = new();

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
            SpawnFromChannel(pixel.r, enemyPrefabs, quadrantCenter, objectsData);
            SpawnFromChannel(pixel.g, artifactPrefabs, quadrantCenter, objectsData);
            SpawnFromChannel(pixel.b, remnantPrefabs, quadrantCenter, objectsData);
        }

        // Store in persistent storage
        _persistentSpatialData[quadrantPos] = new WorldSpatialData
        {
            Objects = objectsData,
            IsExplored = true
        };

        // Spawn the objects
        //Debug.Log("generated");
        SpawnExistingQuadrantContent(quadrantPos);
        //DebugLogQuadrantInfo(quadrantPos, quadrantCenter, texturePos);
        //Debug.Log("Spawned");
    }

    private void SpawnFromChannel(byte channelValue, GameObject[] prefabs, Vector3 quadrantCenter, List<GameObjectData> data)
    {
        if (channelValue == 0 || prefabs.Length == 0) return;

        int count = Mathf.CeilToInt(channelValue / 255f * 30); // Max 10 per channel
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

    private Dictionary<Vector2Int, List<GameObject>> _activeObjects = new();

    private void SpawnGameObject(GameObjectData objData, Vector2Int quadrantPos)
    {
        GameObject prefab = GetPrefabById(objData.PrefabID);
        if (prefab == null) return;

        var instance = Instantiate(prefab, objData.Position, objData.Rotation);

        if (!_activeObjects.ContainsKey(quadrantPos))
            _activeObjects[quadrantPos] = new List<GameObject>();

        _activeObjects[quadrantPos].Add(instance);
    }

    private void DespawnContent(Vector2Int quadrantPos)
    {
        if (_activeObjects.TryGetValue(quadrantPos, out var objects))
        {
            foreach (var obj in objects)
                Destroy(obj);

            _activeObjects.Remove(quadrantPos);
        }

        _activeQuadrants.Remove(quadrantPos);
    }

    // Returns a prefab by matching its name with the given prefabId.
    private GameObject GetPrefabById(string prefabId)
    {
        foreach (var prefab in enemyPrefabs)
            if (prefab.name == prefabId)
                return prefab;

        foreach (var prefab in artifactPrefabs)
            if (prefab.name == prefabId)
                return prefab;

        foreach (var prefab in remnantPrefabs)
            if (prefab.name == prefabId)
                return prefab;

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
}