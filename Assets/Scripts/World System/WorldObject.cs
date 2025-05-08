using UnityEngine;
using System.Collections;

public class WorldObject : MonoBehaviour
{
    private Vector2Int _currentQuadrant;
    private WorldDataManager _dataManager;
    private Transform _playerTransform;
    private float _despawnDistanceSquared;
    private bool _canDespawn = false;

    public void Initialize(Vector2Int startQuadrant, WorldDataManager dataManager)
    {
        _currentQuadrant = startQuadrant;
        _dataManager = dataManager;
        _playerTransform = dataManager.GetComponent<WorldStateManager>()?.Player.transform;
        float areaOfInterestRadius = dataManager.GetComponent<WorldProcessor>()?.areaOfInterestRadius ?? 0f;
        _despawnDistanceSquared = areaOfInterestRadius * areaOfInterestRadius * 1.25f * 1.25f; // Similar cleanup margin

        if (_playerTransform != null)
        {
            _canDespawn = true;
            StartCoroutine(SelfDespawnCheck());
        }
    }

    private IEnumerator SelfDespawnCheck()
    {
        while (_canDespawn)
        {
            int waitFrames = Random.Range(5, 15); // Adjust range as needed
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            if (_playerTransform != null && Vector3.SqrMagnitude(transform.position - _playerTransform.position) > _despawnDistanceSquared)
            {
                // Calculate which quadrant the object is now in
                Vector2Int newQuadrant = GetCurrentQuadrant();

                // If the object has moved to a new quadrant, write its data to that quadrant
                if (newQuadrant != _currentQuadrant)
                {
                    _dataManager.WriteQuadrantData(newQuadrant, gameObject);
                }
                else
                {
                    //_dataManager.WriteQuadrantData(newQuadrant, gameObject);
                    _dataManager._activeObjects.Remove(newQuadrant);
                }

                    // Despawn the object (return to pool)
                    _dataManager.ReturnToPool(gameObject);
                _canDespawn = false; // Stop the coroutine
            }
        }
    }

    public void SelfDespawn()
    {
        if (_dataManager != null)
        {
            _dataManager.ReturnToPool(gameObject);
            
        }
        _canDespawn = false; // Ensure the coroutine stops
    }

    private Vector2Int GetCurrentQuadrant()
    {
        if (_dataManager == null || _dataManager.GetComponent<WorldStateManager>() == null)
        {
            Debug.LogError("WorldDataManager or WorldStateManager is null!");
            return _currentQuadrant; // Return the last known quadrant as a fallback
        }

        var stateManager = _dataManager.GetComponent<WorldStateManager>();
        float quadrantSize = stateManager.quadrantSize;
        Vector3 positionInCluster = transform.position + new Vector3(stateManager.clusterBounds.x / 2f, 0f, stateManager.clusterBounds.y / 2f); // Adjust for cluster origin

        int x = Mathf.FloorToInt(positionInCluster.x / quadrantSize);
        int y = Mathf.FloorToInt(positionInCluster.z / quadrantSize);

        // Clamp values to ensure they are within the valid quadrant range
        int maxX = Mathf.FloorToInt(stateManager.clusterBounds.x / quadrantSize);
        int maxY = Mathf.FloorToInt(stateManager.clusterBounds.y / quadrantSize);

        x = Mathf.Clamp(x, 0, maxX - 1);
        y = Mathf.Clamp(y, 0, maxY - 1);

        return new Vector2Int(x, y);
    }
}