using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldProcessor : MonoBehaviour
{
    private WorldStateManager _stateManager;
    private WorldDataManager _dataManager;
    private HashSet<Vector2Int> _currentActiveQuadrants = new HashSet<Vector2Int>();

    public float areaOfInterestRadius;
    public GameObject quadrantMarkerPrefab;

    private void Awake()
    {
        _stateManager = GetComponent<WorldStateManager>();
        _dataManager = GetComponent<WorldDataManager>();
        areaOfInterestRadius = _stateManager.quadrantSize * 3;
        Debug.Log($"Area of Interest Radius is: {areaOfInterestRadius}");
    }

    void Start()
    {
        EventBus.Instance.Subscribe<QuadrantTransitionEvent>(HandleQuadrantChange);
        EventBus.Instance.Subscribe<ClusterExitEvent>(HandleClusterExit);
    }

    private void HandleQuadrantChange(QuadrantTransitionEvent evt)
    {
        AreaProcessor(evt.NewLocation, evt.OldLocation);
    }

    private void AreaProcessor(Vector2Int newQuadrant, Vector2Int? oldQuadrant)
    {
        var quadrantsToProcess = ValidatingQuadrants(newQuadrant, oldQuadrant);
        _dataManager.ProcessQuadrants(quadrantsToProcess);
    }

    private List<QuadrantProcessInfo> ValidatingQuadrants(Vector2Int newQ, Vector2Int? oldQ)
    {
        // Add cluster bounds check
        int maxQuadrantX = Mathf.FloorToInt(_stateManager.clusterBounds.x / _stateManager.quadrantSize);
        int maxQuadrantY = Mathf.FloorToInt(_stateManager.clusterBounds.y / _stateManager.quadrantSize);
        // Calculate the radius in terms of quadrants
        int quadrantRadius = Mathf.CeilToInt(areaOfInterestRadius / _stateManager.quadrantSize);

        // Get new quadrants that should be active
        HashSet<Vector2Int> newActiveQuadrants = new HashSet<Vector2Int>();
        for (int x = -quadrantRadius; x <= quadrantRadius; x++)
        {
            for (int y = -quadrantRadius; y <= quadrantRadius; y++)
            {
                Vector2Int offset = new Vector2Int(x, y);
                Vector2Int quadrantPos = newQ + offset;

                // Check if this quadrant is within the circular AOI
                float distance = Vector2.Distance(
                    new Vector2(quadrantPos.x * _stateManager.quadrantSize, quadrantPos.y * _stateManager.quadrantSize),
                    new Vector2(newQ.x * _stateManager.quadrantSize, newQ.y * _stateManager.quadrantSize)
                );

                if (distance <= areaOfInterestRadius)
                {
                    // Add bounds check
                    if (quadrantPos.x >= 0 && quadrantPos.x < maxQuadrantX &&
                       quadrantPos.y >= 0 && quadrantPos.y < maxQuadrantY)
                    {
                        newActiveQuadrants.Add(quadrantPos);
                    }
                }
            }
        }

        // Create list of quadrants to process
        List<QuadrantProcessInfo> quadrantsToProcess = new List<QuadrantProcessInfo>();

        // Add quadrants that need to be loaded (in new but not in current)
        foreach (var quadrant in newActiveQuadrants)
        {
            if (!_currentActiveQuadrants.Contains(quadrant))
            {
                quadrantsToProcess.Add(new QuadrantProcessInfo(quadrant, true));
            }
        }

        // Add quadrants that need to be unloaded (in current but not in new)
        foreach (var quadrant in _currentActiveQuadrants)
        {
            if (!newActiveQuadrants.Contains(quadrant))
            {
                quadrantsToProcess.Add(new QuadrantProcessInfo(quadrant, false));
            }
        }

        // Update current active quadrants
        _currentActiveQuadrants = newActiveQuadrants;

        /*foreach (var q in quadrantsToProcess)
        {
            Vector3 center = new Vector3(q.Position.x * _stateManager.quadrantSize, 0, q.Position.y * _stateManager.quadrantSize);

            // Adjust center position to account for the cluster offset
            center.x += -_stateManager.clusterBounds.x / 2;
            center.z += -_stateManager.clusterBounds.y / 2;
            Instantiate(quadrantMarkerPrefab, center, Quaternion.identity);
            Debug.Log($"Active Quadrant: {q}");
        }*/

        return quadrantsToProcess;
    }

    private void HandleClusterExit(ClusterExitEvent evt)
    {
        // Create unload instructions for all active quadrants
        List<QuadrantProcessInfo> cleanupQuadrants = _currentActiveQuadrants
            .Select(q => new QuadrantProcessInfo(q, false))
            .ToList();

        // Process the cleanup
        _dataManager.ProcessQuadrants(cleanupQuadrants);

        // Clear the active quadrants set
        _currentActiveQuadrants.Clear();
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || _stateManager == null) return;

        // Draw Area of Interest circle
        Gizmos.color = new Color(1, 1, 0, 0.2f); // Semi-transparent yellow
        Vector3 playerPos = _stateManager._player.transform.position;
        Gizmos.DrawWireSphere(new Vector3(playerPos.x, 0, playerPos.z), areaOfInterestRadius);

        // Draw active quadrants
        Gizmos.color = Color.cyan;
        foreach (var q in _currentActiveQuadrants)
        {
            Vector3 center = new Vector3(
                q.x * _stateManager.quadrantSize,
                0,
                q.y * _stateManager.quadrantSize
            );

            // Adjust center position to account for the cluster offset
            center.x += -_stateManager.clusterBounds.x / 2;
            center.z += -_stateManager.clusterBounds.y / 2;

            Gizmos.DrawWireCube(center, new Vector3(_stateManager.quadrantSize, 1, _stateManager.quadrantSize));
        }
    }
    void DrawQuadrant(Vector2Int quadrantPos)
    {
        Instantiate(quadrantMarkerPrefab, GetQuadrantCenter(quadrantPos), Quaternion.identity);
    }

    private Vector3 GetQuadrantCenter(Vector2Int quadrantPos)
    {
        return new Vector3(
            quadrantPos.x + _stateManager.quadrantSize / 2f,
            0,
            quadrantPos.y + _stateManager.quadrantSize / 2f
        );
    }
}