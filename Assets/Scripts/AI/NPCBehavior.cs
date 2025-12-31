using UnityEngine;

/// <summary>
/// NPC ship AI behavior controller with smooth state transitions and waypoint priority
/// </summary>
public class NPCBehavior : MonoBehaviour
{
    [Header("References")]
    private NPCShipMovement movement;
    private ShipCore shipCore;
    private ShipTargetingSystem targeting;

    [Header("Behavior Settings")]
    public AIBehaviorMode behaviorMode = AIBehaviorMode.Wander;

    [Header("Combat Range Settings")]
    [Tooltip("Long-range enemy detection for AI behavior (NOT weapon range)")]
    public float engagementRange = 500f;

    [Tooltip("Ideal combat distance - ship will circle here")]
    public float optimalRange = 50f;

    [Tooltip("If closer than this, ship will retreat")]
    public float retreatRange = 20f;

    [Tooltip("Hysteresis buffer to prevent state flickering")]
    public float rangeHysteresis = 10f;

    [Tooltip("Minimum time to stay in combat state before switching")]
    public float stateCommitmentTime = 2f;

    [Header("Wander Settings")]
    public float wanderRadius = 400f;
    public float minWanderDistance = 20f;
    public float maxWanderDistance = 800f;
    public float waypointReachDistance = 10f;
    public float wanderWaitTime = 0f;
    private Vector3 wanderCenter;
    private Vector3 currentWanderTarget;
    private float wanderWaitTimer = 0f;
    private bool isWaiting = false;

    [Header("Combat Settings")]
    [Tooltip("How fast to orbit around target (degrees per second)")]
    public float circlingSpeed = 45f;

    [Tooltip("Distance to maintain while circling")]
    public float circlingRadius = 40f;

    [Tooltip("How often to update circle waypoint (seconds)")]
    public float circleUpdateInterval = 0.5f;

    [Tooltip("Predict target movement for smoother circling")]
    public bool predictTargetMovement = true;

    private Vector3 circlingCenter;
    private float circlingAngle;
    private float lastCircleUpdateTime;

    private GameObject currentEnemy;
    private AIBehaviorMode currentState;
    private AIBehaviorMode previousState;
    private float stateEnterTime;

    void Awake()
    {
        movement = GetComponent<NPCShipMovement>();
        shipCore = GetComponentInChildren<ShipCore>();
        targeting = GetComponentInChildren<ShipTargetingSystem>();

        wanderCenter = transform.position;
        currentState = behaviorMode;
        previousState = behaviorMode;
        stateEnterTime = Time.time;
    }

    void Update()
    {
        if (movement == null || shipCore == null) return;

        UpdateBehavior();
    }

    void UpdateBehavior()
    {
        currentEnemy = FindNearestEnemy();

        AIBehaviorMode desiredState = DetermineDesiredState();

        // Only switch states if committed enough time OR critical change
        bool canSwitchState = Time.time - stateEnterTime >= stateCommitmentTime;
        bool criticalChange = (currentState == AIBehaviorMode.Wander && desiredState != AIBehaviorMode.Wander) ||
                              (currentState != AIBehaviorMode.Wander && desiredState == AIBehaviorMode.Wander);

        if (canSwitchState || criticalChange)
        {
            if (desiredState != currentState)
            {
                previousState = currentState;
                currentState = desiredState;
                stateEnterTime = Time.time;
                OnStateChanged();
            }
        }

        // Execute current behavior
        switch (currentState)
        {
            case AIBehaviorMode.Wander:
                DoWander();
                break;
            case AIBehaviorMode.Approach:
                DoApproach();
                break;
            case AIBehaviorMode.Circle:
                DoCircle();
                break;
            case AIBehaviorMode.Retreat:
                DoRetreat();
                break;
            case AIBehaviorMode.Hold:
                DoHold();
                break;
        }
    }

    /// <summary>
    /// Determine desired state with hysteresis to prevent flickering
    /// </summary>
    AIBehaviorMode DetermineDesiredState()
    {
        if (currentEnemy == null)
            return behaviorMode;

        float distance = Vector3.Distance(transform.position, currentEnemy.transform.position);

        // Not in engagement range
        if (distance > engagementRange + rangeHysteresis)
            return behaviorMode;

        // Apply hysteresis based on current state
        switch (currentState)
        {
            case AIBehaviorMode.Approach:
                // Stay approaching until we're comfortably in optimal range
                if (distance > optimalRange + rangeHysteresis)
                    return AIBehaviorMode.Approach;
                else if (distance < retreatRange)
                    return AIBehaviorMode.Retreat;
                else
                    return AIBehaviorMode.Circle;

            case AIBehaviorMode.Circle:
                // Stay circling unless we drift too far from optimal
                if (distance > optimalRange + rangeHysteresis * 2f)
                    return AIBehaviorMode.Approach;
                else if (distance < retreatRange - rangeHysteresis)
                    return AIBehaviorMode.Retreat;
                else
                    return AIBehaviorMode.Circle;

            case AIBehaviorMode.Retreat:
                // Stay retreating until we're safely back to optimal
                if (distance < retreatRange - rangeHysteresis)
                    return AIBehaviorMode.Retreat;
                else if (distance > optimalRange + rangeHysteresis)
                    return AIBehaviorMode.Approach;
                else
                    return AIBehaviorMode.Circle;

            default:
                // First entry into combat
                if (distance > optimalRange)
                    return AIBehaviorMode.Approach;
                else if (distance < retreatRange)
                    return AIBehaviorMode.Retreat;
                else
                    return AIBehaviorMode.Circle;
        }
    }

    /// <summary>
    /// Called when state changes
    /// </summary>
    void OnStateChanged()
    {
        switch (currentState)
        {
            case AIBehaviorMode.Circle:
                // Initialize circling position
                InitializeCircling();
                break;

            case AIBehaviorMode.Wander:
                // Reset wander target when returning to wander
                currentWanderTarget = Vector3.zero;
                break;
        }

        Debug.Log($"[{gameObject.name}] State: {previousState} → {currentState}");
    }

    /// <summary>
    /// ✅ UPDATED: Wander behavior with LOW priority
    /// </summary>
    void DoWander()
    {
        if (isWaiting)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0f)
            {
                isWaiting = false;
                currentWanderTarget = Vector3.zero;
            }
            else
            {
                movement.ClearWaypoint(WaypointPriority.Low);
                return;
            }
        }

        if (currentWanderTarget == Vector3.zero ||
            Vector3.Distance(transform.position, currentWanderTarget) < waypointReachDistance)
        {
            if (wanderWaitTime > 0f && currentWanderTarget != Vector3.zero)
            {
                isWaiting = true;
                wanderWaitTimer = wanderWaitTime;
                movement.ClearWaypoint(WaypointPriority.Low);
                return;
            }

            currentWanderTarget = GetRandomWanderPoint();
        }

        // ✅ LOW PRIORITY - Combat will override this
        movement.SetWaypoint(currentWanderTarget, WaypointPriority.Low);
    }

    Vector3 GetRandomWanderPoint()
    {
        float distance = Random.Range(minWanderDistance, maxWanderDistance);
        float angle = Random.Range(0f, Mathf.PI * 2f);

        Vector3 offset = new Vector3(
            Mathf.Cos(angle) * distance,
            0f,
            Mathf.Sin(angle) * distance
        );

        Vector3 newPoint = wanderCenter + offset;

        Vector3 fromCenter = newPoint - wanderCenter;
        if (fromCenter.magnitude > wanderRadius)
        {
            fromCenter = fromCenter.normalized * wanderRadius;
            newPoint = wanderCenter + fromCenter;
        }

        return newPoint;
    }

    /// <summary>
    /// ✅ UPDATED: Approach enemy with HIGH priority
    /// </summary>
    void DoApproach()
    {
        if (currentEnemy == null) return;

        Vector3 targetPos = GetPredictedEnemyPosition();
        Vector3 direction = (targetPos - transform.position).normalized;
        Vector3 approachPos = targetPos - direction * optimalRange;

        // ✅ HIGH PRIORITY - Overrides wander
        movement.SetWaypoint(approachPos, WaypointPriority.High);
    }

    /// <summary>
    /// Initialize circling only once when entering state
    /// </summary>
    void InitializeCircling()
    {
        if (currentEnemy == null) return;

        circlingCenter = currentEnemy.transform.position;
        Vector3 toShip = transform.position - circlingCenter;
        circlingAngle = Mathf.Atan2(toShip.z, toShip.x) * Mathf.Rad2Deg;
        lastCircleUpdateTime = Time.time;
    }

    /// <summary>
    /// ✅ UPDATED: Circle enemy with HIGH priority
    /// </summary>
    void DoCircle()
    {
        if (currentEnemy == null) return;

        // Update circling angle
        circlingAngle += circlingSpeed * Time.deltaTime;

        // Only update waypoint at intervals (reduces jitter)
        if (Time.time - lastCircleUpdateTime >= circleUpdateInterval)
        {
            lastCircleUpdateTime = Time.time;

            // Use predicted position for smoother circling
            Vector3 targetPos = GetPredictedEnemyPosition();

            // Calculate circle position
            float angleRad = circlingAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angleRad) * circlingRadius,
                0f,
                Mathf.Sin(angleRad) * circlingRadius
            );

            Vector3 circlePos = targetPos + offset;

            // ✅ HIGH PRIORITY - Overrides wander
            movement.SetWaypoint(circlePos, WaypointPriority.High);
        }
    }

    /// <summary>
    /// ✅ UPDATED: Retreat from enemy with CRITICAL priority
    /// </summary>
    void DoRetreat()
    {
        if (currentEnemy == null) return;

        Vector3 awayDirection = (transform.position - currentEnemy.transform.position).normalized;
        Vector3 retreatPos = transform.position + awayDirection * 30f;

        // ✅ CRITICAL PRIORITY - Highest priority
        movement.SetWaypoint(retreatPos, WaypointPriority.Critical);
    }

    /// <summary>
    /// ✅ UPDATED: Hold position with MEDIUM priority
    /// </summary>
    void DoHold()
    {
        movement.ClearWaypoint(WaypointPriority.Medium);
    }

    /// <summary>
    /// Predict where enemy will be for smoother combat
    /// </summary>
    Vector3 GetPredictedEnemyPosition()
    {
        if (currentEnemy == null || !predictTargetMovement)
            return currentEnemy.transform.position;

        Rigidbody enemyRb = currentEnemy.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            // Predict 0.5 seconds ahead
            return currentEnemy.transform.position + enemyRb.linearVelocity * 0.5f;
        }

        return currentEnemy.transform.position;
    }

    /// <summary>
    /// Independent long-range detection for AI behavior
    /// Uses engagementRange instead of sonar radius
    /// </summary>
    /// <summary>
    /// ✅ FIXED: Prioritize ships over asteroids for enemy detection
    /// Ensures NPCs always engage hostile ships first, even when stuck in asteroids
    /// </summary>
    /// <summary>
    /// ✅ FIXED: ONLY detect ships with Alignment, NEVER asteroids
    /// Asteroids are handled purely by weapon targeting system when stuck
    /// </summary>
    GameObject FindNearestEnemy()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, engagementRange);

        GameObject nearestShip = null;
        float minShipDist = float.MaxValue;

        foreach (var col in colliders)
        {
            // Check if it's a valid targetable
            var targetable = col.GetComponent<Targetable>();
            if (targetable == null || !targetable.IsAlive)
                continue;

            // Skip self and children
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform))
                continue;

            // ✅ CRITICAL: Only consider objects with Alignment (ships, not asteroids!)
            var targetAlignment = col.GetComponent<Alignment>();
            if (targetAlignment == null)
            {
                // No Alignment = asteroid/debris, IGNORE for AI behavior
                continue;
            }

            // Check alignment filtering
            var myAlignment = GetComponent<Alignment>();

            if (myAlignment != null)
            {
                // Skip friendly targets
                if (targetAlignment.isFriendly && myAlignment.isFriendly)
                    continue;
            }

            // Track nearest hostile ship
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < minShipDist)
            {
                minShipDist = dist;
                nearestShip = col.gameObject;
            }
        }

        // ✅ ONLY return ships, NEVER asteroids
        return nearestShip;
    }



    public void SetWanderCenter(Vector3 newCenter)
    {
        wanderCenter = newCenter;
        currentWanderTarget = Vector3.zero;
    }

    /// <summary>
    /// Enhanced debug visualization showing both detection and targeting ranges
    /// </summary>
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // AI Detection Range (yellow wireframe)
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, engagementRange);

        // Weapon Sonar Range (cyan filled)
        if (targeting != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawSphere(transform.position, targeting.sonarRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, targeting.sonarRadius);
        }

        // Optimal range (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, optimalRange);

        // Retreat range (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatRange);

        // Enemy line and info
        if (currentEnemy != null)
        {
            float dist = Vector3.Distance(transform.position, currentEnemy.transform.position);

            // Color line based on if weapons can fire
            bool canFire = targeting != null && dist <= targeting.sonarRadius;
            Gizmos.color = canFire ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, currentEnemy.transform.position);

            // ✅ Show current waypoint priority color
            if (movement != null && movement.HasWaypoint())
            {
                WaypointPriority priority = movement.GetCurrentPriority();
                Gizmos.color = GetPriorityColor(priority);
                Gizmos.DrawLine(transform.position, movement.GetCurrentWaypoint());
            }

            // Label
#if UNITY_EDITOR
            string priorityText = movement != null ? $"\nPriority: {movement.GetCurrentPriority()}" : "";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 10f,
                $"State: {currentState}\nDist: {dist:F1}\nCan Fire: {canFire}{priorityText}"
            );
#endif
        }
    }

    /// <summary>
    /// ✅ NEW: Get debug color based on waypoint priority
    /// </summary>
    private Color GetPriorityColor(WaypointPriority priority)
    {
        switch (priority)
        {
            case WaypointPriority.Critical: return Color.red;
            case WaypointPriority.High: return new Color(1f, 0.5f, 0f); // Orange
            case WaypointPriority.Medium: return Color.yellow;
            case WaypointPriority.Low: return Color.blue;
            default: return Color.gray;
        }
    }
}

public enum AIBehaviorMode
{
    Wander,
    Approach,
    Circle,
    Retreat,
    Hold
}
