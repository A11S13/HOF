using UnityEngine;

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

    AIBehaviorMode DetermineDesiredState()
    {
        if (currentEnemy == null)
            return behaviorMode;

        float distance = Vector3.Distance(transform.position, currentEnemy.transform.position);

        if (distance > engagementRange + rangeHysteresis)
            return behaviorMode;

        switch (currentState)
        {
            case AIBehaviorMode.Approach:
                if (distance > optimalRange + rangeHysteresis)
                    return AIBehaviorMode.Approach;
                else if (distance < retreatRange)
                    return AIBehaviorMode.Retreat;
                else
                    return AIBehaviorMode.Circle;

            case AIBehaviorMode.Circle:
                if (distance > optimalRange + rangeHysteresis * 2f)
                    return AIBehaviorMode.Approach;
                else if (distance < retreatRange - rangeHysteresis)
                    return AIBehaviorMode.Retreat;
                else
                    return AIBehaviorMode.Circle;

            case AIBehaviorMode.Retreat:
                if (distance < retreatRange - rangeHysteresis)
                    return AIBehaviorMode.Retreat;
                else if (distance > optimalRange + rangeHysteresis)
                    return AIBehaviorMode.Approach;
                else
                    return AIBehaviorMode.Circle;

            default:
                if (distance > optimalRange)
                    return AIBehaviorMode.Approach;
                else if (distance < retreatRange)
                    return AIBehaviorMode.Retreat;
                else
                    return AIBehaviorMode.Circle;
        }
    }

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

    void DoApproach()
    {
        if (currentEnemy == null) return;

        Vector3 targetPos = GetPredictedEnemyPosition();
        Vector3 direction = (targetPos - transform.position).normalized;
        Vector3 approachPos = targetPos - direction * optimalRange;

        movement.SetWaypoint(approachPos, WaypointPriority.High);
    }

    void InitializeCircling()
    {
        if (currentEnemy == null) return;

        circlingCenter = currentEnemy.transform.position;
        Vector3 toShip = transform.position - circlingCenter;
        circlingAngle = Mathf.Atan2(toShip.z, toShip.x) * Mathf.Rad2Deg;
        lastCircleUpdateTime = Time.time;
    }

    void DoCircle()
    {
        if (currentEnemy == null) return;

        // Update circling angle
        circlingAngle += circlingSpeed * Time.deltaTime;

        if (Time.time - lastCircleUpdateTime >= circleUpdateInterval)
        {
            lastCircleUpdateTime = Time.time;

            Vector3 targetPos = GetPredictedEnemyPosition();

            float angleRad = circlingAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angleRad) * circlingRadius,
                0f,
                Mathf.Sin(angleRad) * circlingRadius
            );

            Vector3 circlePos = targetPos + offset;

            movement.SetWaypoint(circlePos, WaypointPriority.High);
        }
    }

    void DoRetreat()
    {
        if (currentEnemy == null) return;

        Vector3 awayDirection = (transform.position - currentEnemy.transform.position).normalized;
        Vector3 retreatPos = transform.position + awayDirection * 30f;

        movement.SetWaypoint(retreatPos, WaypointPriority.Critical);
    }

    void DoHold()
    {
        movement.ClearWaypoint(WaypointPriority.Medium);
    }

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

        return nearestShip;
    }

    public void SetWanderCenter(Vector3 newCenter)
    {
        wanderCenter = newCenter;
        currentWanderTarget = Vector3.zero;
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

public enum WaypointPriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
