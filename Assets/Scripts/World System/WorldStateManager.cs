// DON'T MODIFY THIS FILE, NIGGER!
using UnityEngine;

public class WorldStateManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _quadrantSize = 200f;
    [SerializeField] private Vector2 _clusterBounds = new Vector2(200000f, 200000f);

    public bool IsInMappedWorld { get; private set; }
    public Vector2Int? CurrentQuadrant { get; private set; }
    public WorldSpatialData[,] SpatialData { get; private set; }

    [SerializeField] private Transform _player;

    void Start()
    {
        InitializeSpatialData();
        IsInMappedWorld = IsPlayerInClusterBounds();
        if (IsInMappedWorld)
        {
            LoadCluster();
            CurrentQuadrant = CalculateCurrentQuadrant();
            SpawnQuadrantContent();
        }
    }

    void Update()
    {
        CheckWorldTransition();
        CheckQuadrantTransition();
    }

    private void InitializeSpatialData()
    {
        int quadrantsX = Mathf.CeilToInt(_clusterBounds.x / _quadrantSize);
        int quadrantsY = Mathf.CeilToInt(_clusterBounds.y / _quadrantSize);
        SpatialData = new WorldSpatialData[quadrantsX, quadrantsY];
    }

    private bool IsPlayerInClusterBounds()
    {
        // AABB check
        return Mathf.Abs(_player.position.x) < _clusterBounds.x / 2 &&
               Mathf.Abs(_player.position.z) < _clusterBounds.y / 2;
    }

    private void CheckWorldTransition()
    {
        bool wasInMappedWorld = IsInMappedWorld;
        IsInMappedWorld = IsPlayerInClusterBounds();

        if (IsInMappedWorld != wasInMappedWorld) // If someone or both lied
        {
            if (IsInMappedWorld)
            {
                LoadCluster();
                EventBus.Instance.Publish(new ClusterEnterEvent());
                Debug.Log("ENTERING HINESSA CLUSTER");
            }
            else if (wasInMappedWorld)
            {
                SaveCluster();
                Debug.Log($"Leaving quadrant: {CurrentQuadrant}");
                DespawnQuadrantContent();
                CurrentQuadrant = null; // Reset quadrant when leaving cluster
                EventBus.Instance.Publish(new ClusterExitEvent());
                Debug.Log("EXITING HINESSA CLUSTER");
            }
        }
    }

    private void CheckQuadrantTransition()
    {
        if (!IsInMappedWorld) return;

        Vector2Int newQuadrant = CalculateCurrentQuadrant();

        if (newQuadrant != CurrentQuadrant)
        {
            Debug.Log($"Quadrant transition: {CurrentQuadrant} -> {newQuadrant}");

            if (CurrentQuadrant != null) DespawnQuadrantContent();

            if (CurrentQuadrant.HasValue) //Check if CurrentQuadrant has a value before publishing event, because of niggers
            {
                EventBus.Instance.Publish(new ClusterExitEvent());
            }

            CurrentQuadrant = newQuadrant;

            EventBus.Instance.Publish(new ClusterEnterEvent());

            HandleQuadrantChange();
        }
    }

    private Vector2Int CalculateCurrentQuadrant()
    {
        return new Vector2Int(
            Mathf.FloorToInt((_player.position.x + _clusterBounds.x / 2) / _quadrantSize),
            Mathf.FloorToInt((_player.position.z + _clusterBounds.y / 2) / _quadrantSize)
        );
    }

    private void LoadCluster()
    {
        // TODO: Implement World Texture loading
        Debug.Log($"Loading Cluster Texture");
    }

    private void SaveCluster()
    {
        // TODO: Implement World Texture saving
        Debug.Log("Saving spatial data to Cluster Texture");
    }

    private void HandleQuadrantChange()
    {
        if (CurrentQuadrant.HasValue) // Check if CurrentQuadrant has a value
        {
            if (SpatialData[CurrentQuadrant.Value.x, CurrentQuadrant.Value.y] == null) // Use .Value to access x and y
            {
                SpatialData[CurrentQuadrant.Value.x, CurrentQuadrant.Value.y] = new WorldSpatialData();
                Debug.Log($"New quadrant discovered: {CurrentQuadrant.Value}");
            }

            SpawnQuadrantContent();
        }
    }

    private void SpawnQuadrantContent()
    {
        // TODO: Implement object spawning
        Debug.Log($"Spawning content for quadrant {CurrentQuadrant}");
    }

    private void DespawnQuadrantContent()
    {
        // TODO: Implement object despawning
        Debug.Log($"Removing shit from quadrant {CurrentQuadrant}");
    }
}