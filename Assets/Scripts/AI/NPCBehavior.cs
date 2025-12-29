using UnityEngine;

/// <summary>
/// NPC ship AI behavior controller with smooth state transitions
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
    [Tooltip("Start engaging enemies within this range")]
    public float engagementRange = 100f;

    [Tooltip("Ideal combat distance - ship will circle here")]
    public float optimalRange = 50f;

    [Tooltip("If closer than this, ship will retreat")]
    public float retreatRange = 20f;

    [Tooltip("Hysteresis buffer to prevent state flickering")]
    public float rangeHysteresis = 10f; // ← NEW

    [Tooltip("Minimum time to stay in combat state before switching")]
    public float stateCommitmentTime = 2f; // ← NEW

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
    public float circlingSpeed = 45f; // ← Changed to degrees/sec

    [Tooltip("Distance to maintain while circling")]
    public float circlingRadius = 40f;

    [Tooltip("How often to update circle waypoint (seconds)")]
    public float circleUpdateInterval = 0.5f; // ← NEW

    [Tooltip("Predict target movement for smoother circling")]
    public bool predictTargetMovement = true; // ← NEW

    private Vector3 circlingCenter;
    private float circlingAngle;
    private float lastCircleUpdateTime;

    private GameObject currentEnemy;
    private AIBehaviorMode currentState;
    private AIBehaviorMode previousState;
    private float stateEnterTime; // ← NEW

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
    /// ✅ NEW: Determine desired state with hysteresis to prevent flickering
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
    /// ✅ NEW: Called when state changes
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
                movement.ClearWaypoint();
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
                movement.ClearWaypoint();
                return;
            }

            currentWanderTarget = GetRandomWanderPoint();
        }

        movement.SetWaypoint(currentWanderTarget);
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

    void DoApproach()
    {
        if (currentEnemy == null) return;

        Vector3 targetPos = GetPredictedEnemyPosition();
        Vector3 direction = (targetPos - transform.position).normalized;
        Vector3 approachPos = targetPos - direction * optimalRange;

        movement.SetWaypoint(approachPos);
    }

    /// <summary>
    /// ✅ FIXED: Initialize circling only once when entering state
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
    /// ✅ FIXED: Update waypoint at intervals, not every frame
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
            movement.SetWaypoint(circlePos);
        }
    }

    void DoRetreat()
    {
        if (currentEnemy == null) return;

        Vector3 awayDirection = (transform.position - currentEnemy.transform.position).normalized;
        Vector3 retreatPos = transform.position + awayDirection * 30f;

        movement.SetWaypoint(retreatPos);
    }

    void DoHold()
    {
        movement.ClearWaypoint();
    }

    /// <summary>
    /// ✅ NEW: Predict where enemy will be for smoother combat
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

    GameObject FindNearestEnemy()
    {
        if (targeting == null) return null;

        var targets = targeting.GetTargetDataArray();
        if (targets.Length == 0) return null;

        GameObject nearest = null;
        float minDist = float.MaxValue;

        foreach (var target in targets)
        {
            if (target.TargetObject == null) continue;

            float dist = Vector3.Distance(transform.position, target.TargetObject.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = target.TargetObject;
            }
        }

        return nearest;
    }

    public void SetWanderCenter(Vector3 newCenter)
    {
        wanderCenter = newCenter;
        currentWanderTarget = Vector3.zero;
    }

    /// <summary>
    /// ✅ NEW: Debug visualization
    /// </summary>
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw engagement range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, engagementRange);

        // Draw optimal range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, optimalRange);

        // Draw retreat range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatRange);

        // Draw current enemy line
        if (currentEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentEnemy.transform.position);
        }

        // Draw current state
        UnityEditor.Handles.Label(transform.position + Vector3.up * 10f, $"State: {currentState}");
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
