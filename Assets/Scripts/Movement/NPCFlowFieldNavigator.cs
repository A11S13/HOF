using UnityEngine;

/// <summary>
/// Manages local flow field for a single NPC ship
/// </summary>
public class NPCFlowFieldNavigator : MonoBehaviour
{
    [Header("Grid Configuration")]
    [Tooltip("Radius of navigation grid around ship (300-500 units)")]
    public float navigationRadius = 400f;

    [Tooltip("Size of each grid cell (10-15 units recommended)")]
    public float cellSize = 12f;

    [Header("Update Settings")]
    [Tooltip("How often to rebuild flow field (seconds)")]
    public float updateInterval = 0.5f;

    [Tooltip("Minimum distance ship must move to force update")]
    public float updateDistanceThreshold = 30f;

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayer;

    [Tooltip("Radius to check for obstacles in each cell")]
    public float obstacleSampleRadius = 8f;

    [Tooltip("How far around obstacles to create cost gradient (multiplier of sample radius)")]
    [Range(1f, 5f)]
    public float gradientRangeMultiplier = 2f;

    [Tooltip("Maximum cost for gradient cells (1-254)")]
    [Range(1, 254)]
    public byte maxGradientCost = 80;

    [Tooltip("Predict asteroid movement this far ahead")]
    public float asteroidPredictionTime = 2f;

    [Header("Navigation")]
    [Tooltip("Blend flow field with direct path (0=pure flow, 1=direct)")]
    [Range(0f, 1f)]
    public float directPathBlend = 0.3f;

    [Tooltip("Stop using flow field when this close to goal")]
    public float directApproachDistance = 40f;

    [Header("Debug")]
    public bool showDebugGrid = false;
    public bool showFlowVectors = false;

    // Internal state
    private LocalFlowFieldGrid flowField;
    private Vector3 currentGoal;
    private bool hasGoal = false;
    private float lastUpdateTime;
    private Vector3 lastUpdatePosition;

    void Start()
    {
        // Calculate grid size from radius
        int gridSize = Mathf.CeilToInt((navigationRadius * 2f) / cellSize);

        // Ensure odd number for centered grid
        if (gridSize % 2 == 0) gridSize++;

        flowField = new LocalFlowFieldGrid(gridSize, gridSize, cellSize);

        lastUpdatePosition = transform.position;

        Debug.Log($"[{gameObject.name}] Flow field initialized: {gridSize}x{gridSize} cells, {cellSize}m cell size");
    }

    void Update()
    {
        if (!hasGoal) return;

        // Check if update needed
        bool timeExpired = Time.time - lastUpdateTime >= updateInterval;
        bool movedFar = Vector3.Distance(transform.position, lastUpdatePosition) > updateDistanceThreshold;

        if (timeExpired || movedFar)
        {
            UpdateFlowField();
        }
    }

    /// <summary>
    /// Set navigation goal
    /// </summary>
    public void SetGoal(Vector3 goalPosition)
    {
        bool goalChanged = !hasGoal || Vector3.Distance(currentGoal, goalPosition) > 20f;

        currentGoal = goalPosition;
        hasGoal = true;

        if (goalChanged)
        {
            UpdateFlowField();
        }
    }

    /// <summary>
    /// Clear current goal
    /// </summary>
    public void ClearGoal()
    {
        hasGoal = false;
    }

    void UpdateFlowField()
    {
        if (!hasGoal) return;

        flowField.Generate(
            transform.position,
            currentGoal,
            obstacleLayer,
            obstacleSampleRadius,
            asteroidPredictionTime,
            gradientRangeMultiplier,
            maxGradientCost
        );

        lastUpdateTime = Time.time;
        lastUpdatePosition = transform.position;
    }

    /// <summary>
    /// Get the desired movement direction (call this from NPCShipMovement)
    /// </summary>
    public Vector3 GetNavigationDirection(Vector3 currentPosition)
    {
        if (!hasGoal) return Vector3.zero;

        float distanceToGoal = Vector3.Distance(currentPosition, currentGoal);
        if (distanceToGoal < directApproachDistance) return (currentGoal - currentPosition).normalized;

        Vector3 flowDirection = flowField.GetFlowDirection(currentPosition);

        // If something is wrong with the grid, fallback
        if (flowDirection == Vector3.zero) return (currentGoal - currentPosition).normalized;

        // --- THE FIX ---
        // If we are in a dangerous area, DO NOT BLEND. 
        // Blending pulls the ship towards the goal, which might be through a wall.
        byte cost = flowField.GetCost(currentPosition);
        if (cost > 20) // Any cost higher than base
        {
            return flowDirection.normalized; // Pure avoidance
        }

        // Safe space? Blend smoothly.
        Vector3 directDirection = (currentGoal - currentPosition).normalized;
        return Vector3.Lerp(flowDirection, directDirection, directPathBlend).normalized;
    }



    /// <summary>
    /// Check if position is in high-cost area (for decision making)
    /// </summary>
    public bool IsInDangerZone(Vector3 position)
    {
        byte cost = flowField.GetCost(position);
        return cost > 30; // Adjust threshold as needed
    }

    /// <summary>
    /// Get raw cost at position
    /// </summary>
    public byte GetNavigationCost(Vector3 position)
    {
        return flowField.GetCost(position);
    }

    void OnDrawGizmos()
    {
        if (flowField == null || !hasGoal) return;

        flowField.DrawDebug(showDebugGrid, showFlowVectors);

        // Draw goal
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(currentGoal, 10f);

        // Draw navigation radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, navigationRadius);
    }
}
