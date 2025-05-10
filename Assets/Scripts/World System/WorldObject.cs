using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for List

public class WorldObject : MonoBehaviour
{
    private Vector2Int _currentQuadrant;
    private WorldDataManager _dataManager;
    private Transform _playerTransform;
    public float disableDistance = 2000f; // Adjust this multiplier to set the disable distance
    private bool _canDespawn = false;
    private bool _canDisable = false;

    public void Initialize(Vector2Int startQuadrant, WorldDataManager dataManager)
    {
        _currentQuadrant = startQuadrant;
        _dataManager = dataManager;
        _playerTransform = dataManager.GetComponent<WorldStateManager>()?.Player.transform;
        float areaOfInterestRadius = dataManager.GetComponent<WorldProcessor>()?.areaOfInterestRadius ?? 0f;

        if (_playerTransform != null)
        {
            _canDespawn = true;
            StartCoroutine(SelfDespawnCheck());
            _canDisable = true;
            StartCoroutine(SelfDisableCheck());
        }
    }

    private IEnumerator SelfDespawnCheck()
    {
        while (_canDespawn)
        {
            int waitFrames = Random.Range(0, 10);
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            if (_playerTransform != null)
            {
                Vector2Int currentObjectQuadrant = GetCurrentQuadrant();
                bool withinActiveQuadrant = IsWithinActiveQuadrants(currentObjectQuadrant);

                Rigidbody rb = GetComponent<Rigidbody>();
                Vector3 currentVelocity = Vector3.zero;
                Vector3 currentAngularVelocity = Vector3.zero;

                if (!withinActiveQuadrant)
                {
                    if (currentObjectQuadrant != _currentQuadrant)
                    {
                        currentVelocity = rb.linearVelocity;
                        currentAngularVelocity = rb.angularVelocity;

                        // Remove from old quadrant's persistent data
                        _dataManager.RemoveObjectFromPersistentData(_currentQuadrant, gameObject.name);
                        // Write to new quadrant
                        _dataManager.WriteQuadrantData(currentObjectQuadrant, gameObject, currentVelocity, currentAngularVelocity);
                        // Remove from old quadrant's active objects
                        _dataManager.RemoveObjectFromQuadrant(_currentQuadrant, gameObject);

                        _currentQuadrant = currentObjectQuadrant;
                    }

                    // Despawn the object
                    _dataManager.ReturnToPool(gameObject);
                    _canDespawn = false;
                }
            }
        }
    }

    private IEnumerator SelfDisableCheck()
    {
        while (_canDisable)
        {
            int waitFrames = Random.Range(0, 10);
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            if (_playerTransform != null)
            {
                float distanceSquared = (_playerTransform.position - transform.position).sqrMagnitude;
                // Objects should be active if the distance is LESS THAN or EQUAL to the disableDistance.
                bool withinEnableDistance = distanceSquared <= disableDistance * disableDistance; // Use squared distance for comparison

                // Handle Biota object
                Transform biotaParent = transform.Find("Biota");
                if (biotaParent != null)
                {
                    biotaParent.gameObject.SetActive(withinEnableDistance);
                }

                // Handle Gas object
                Transform gasParent = transform.Find("Gas");
                if (gasParent != null)
                {
                    gasParent.gameObject.SetActive(withinEnableDistance);
                }

                // Handle Inerta children
                Transform inertaParent = transform.Find("Inerta");
                if (inertaParent != null)
                {
                    foreach (Transform child in inertaParent)
                    {
                        if (child != null)
                        {
                            child.gameObject.SetActive(withinEnableDistance);
                        }
                    }
                }
            }
        }
    }

    private bool IsWithinActiveQuadrants(Vector2Int objectQuadrant)
    {
        if (_dataManager != null && _dataManager.GetComponent<WorldProcessor>() != null)
        {
            var activeQuadrants = _dataManager.GetComponent<WorldProcessor>()._currentActiveQuadrants;
            return activeQuadrants.Contains(objectQuadrant);
        }
        return false;
    }

    public void SelfDespawn()
    {
        if (_dataManager != null)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            Vector3 currentVelocity = Vector3.zero;
            Vector3 currentAngularVelocity = Vector3.zero;

            if (rb != null)
            {
                currentVelocity = rb.linearVelocity;
                currentAngularVelocity = rb.angularVelocity;
            }

            // Calculate which quadrant the object is now in
            Vector2Int currentObjectQuadrant = GetCurrentQuadrant();

            // Write data, including velocity and angular velocity
            _dataManager.WriteQuadrantData(currentObjectQuadrant, gameObject, currentVelocity, currentAngularVelocity);

            // Remove from the old quadrant in the data manager
            _dataManager.RemoveObjectFromQuadrant(currentObjectQuadrant, gameObject);

            _dataManager.ReturnToPool(gameObject);
        }
        _canDespawn = false; // Ensure the coroutine stops
        _canDisable = false; // Ensure the disable coroutine stops as well
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