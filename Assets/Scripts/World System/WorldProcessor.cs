using System.Buffers;
using System.Collections;
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
        //Debug.Log($"Area of Interest Radius is: {areaOfInterestRadius}");
    }

    void Start()
    {
        EventBus.Instance.Subscribe<QuadrantTransitionEvent>(HandleQuadrantChange);
        EventBus.Instance.Subscribe<ClusterTransitionEvent>(HandleClusterExit);
        //Debug.Log("PROCESSING AREA...");
        if (_stateManager.IsInMappedWorld) StartCoroutine(AreaProcessor(_stateManager.CurrentQuadrant.Value));
    }

    private void HandleQuadrantChange(QuadrantTransitionEvent evt)
    {
        Debug.Log($"Processing Area of Quadrant: {evt.NewLocation}");
        StartCoroutine(AreaProcessor(evt.NewLocation));
    }

    private IEnumerator AreaProcessor(Vector2Int newQuadrant)
    {
        //Debug.Log($"WAITING FOR TEXTURE LOAD!");
        yield return new WaitUntil(() => WorldDataManager.TexturesLoaded);
        var quadrantsToProcess = ValidatingQuadrants(newQuadrant);
        _dataManager.ProcessQuadrants(quadrantsToProcess);
    }

    private List<QuadrantProcessInfo> ValidatingQuadrants(Vector2Int newQ)
    {
        //Debug.Log($"Validating Quadrants for {newQ}");
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
                        //Debug.Log($"New Active Quadrant: {quadrantPos}");
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
                //Debug.Log($"Quadrant to be loaded: {quadrant}");
            }
        }

        // Add quadrants that need to be unloaded (in current but not in new)
        if (!_stateManager.Awakened) // INITIAL LOADING
        {
            foreach (var quadrant in _currentActiveQuadrants)
            {
                if (!newActiveQuadrants.Contains(quadrant))
                {
                    quadrantsToProcess.Add(new QuadrantProcessInfo(quadrant, false));
                    //Debug.Log($"Quadrant to be unloaded: {quadrant}");
                }
            }
        }
        // Update current active quadrants
        _currentActiveQuadrants = newActiveQuadrants;
        //Debug.Log($"Quadrants to process: {quadrantsToProcess.Count} Active Quadrants: {_currentActiveQuadrants.Count}");

        return quadrantsToProcess;
    }

    private void HandleClusterExit(ClusterTransitionEvent evt)
    {
        if (!_stateManager.Awakened && !_stateManager.IsInMappedWorld)
        {
            List<QuadrantProcessInfo> cleanupQuadrants = _currentActiveQuadrants
                .Select(q => new QuadrantProcessInfo(q, false))
                .ToList();

            _dataManager.ProcessQuadrants(cleanupQuadrants);
            _currentActiveQuadrants.Clear();
            //Debug.Log("REMOVING ALL THE SHIT!");
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || _stateManager == null) return;

        // Draw Area of Interest circle
        Gizmos.color = new Color(1, 1, 0, 0.2f); // Semi-transparent yellow
        Vector3 playerPos = _stateManager.Player.transform.position;
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