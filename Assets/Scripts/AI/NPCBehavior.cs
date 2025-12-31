using UnityEngine;

public class NPCBehavior : MonoBehaviour
{
    [Header("References")]
    private NPCShipMovement movement;
    private ShipCore shipCore;
    private ShipTargetingSystem targeting;

    [Header("Behavior Settings")]
    public AIBehaviorMode behaviorMode = AIBehaviorMode.Wander;

    [Header("Combat Tactics")]
    [Tooltip("Distance to start an attack run.")]
    public float attackInitiateRange = 150f;

    [Tooltip("Target point behind enemy. Set HIGH (120+) to ensure ship doesn't slow down at the end.")]
    public float overshootDistance = 120f;

    [Tooltip("Distance past the enemy where we actually break off.")]
    public float breakTriggerDistance = 80f;

    [Tooltip("Sideways spacing. 45 is safe for 5-unit ships.")]
    public float strafeOffsetDistance = 45f;

    [Tooltip("Time between attack runs.")]
    public float attackCooldown = 5f;

    [Tooltip("How long to stay in 'Break' mode after an attack.")]
    public float breakDuration = 2.5f;

    [Tooltip("General engagement radar range.")]
    public float engagementRange = 600f;

    [Header("Dogfight Settings (Circling)")]
    public float baseCirclingRadius = 60f;
    public float circlingSpeed = 50f;
    public float radiusOscillation = 20f;
    public float oscillationFreq = 1.5f;

    [Header("Wander Settings")]
    public float wanderRadius = 400f;
    public float minWanderDistance = 20f;
    public float maxWanderDistance = 800f;
    public float waypointReachDistance = 10f;
    public float wanderWaitTime = 0f;

    // Internal State
    private Vector3 wanderCenter;
    private Vector3 currentWanderTarget;
    private float wanderWaitTimer = 0f;
    private bool isWaiting = false;

    // Combat State
    private GameObject currentEnemy;
    private AIBehaviorMode currentState;
    private AIBehaviorMode previousState;
    private float stateEnterTime;

    // Attack Run Logic
    private float lastAttackTime;
    private bool committedToRun = false;
    private Vector3 cachedAttackDir; // Locked direction
    private Vector3 cachedStrafeOffset; // Locked offset

    // Circle Logic
    private Vector3 circlingCenter;
    private float circlingAngle;
    private float lastCircleUpdateTime;

    void Awake()
    {
        movement = GetComponent<NPCShipMovement>();
        shipCore = GetComponentInChildren<ShipCore>();
        targeting = GetComponentInChildren<ShipTargetingSystem>();

        wanderCenter = transform.position;
        currentState = behaviorMode;
        previousState = behaviorMode;
        stateEnterTime = Time.time;
        lastAttackTime = Time.time;
    }

    void Update()
    {
        if (movement == null || shipCore == null) return;
        UpdateBehavior();
    }

    void UpdateBehavior()
    {
        currentEnemy = FindNearestEnemy();
        AIBehaviorMode desiredState = DetermineTacticalState();

        if (desiredState != currentState)
        {
            // Lock AttackRun until we are actually far enough away
            // or if it's taken too long (timeout)
            bool locked = false;

            if (currentState == AIBehaviorMode.AttackRun)
            {
                // Only unlock if logic in DetermineTacticalState explicitly says to Break
                // or if we lost the enemy
                if (currentEnemy != null && desiredState == AIBehaviorMode.AttackRun)
                    locked = true;
            }
            else if (currentState == AIBehaviorMode.EvasiveBreak)
            {
                if (Time.time - stateEnterTime < breakDuration) locked = true;
            }

            if (currentEnemy == null) locked = false;

            if (!locked)
            {
                ChangeState(desiredState);
            }
        }

        switch (currentState)
        {
            case AIBehaviorMode.Wander: DoWander(); break;
            case AIBehaviorMode.Approach: DoApproach(); break;
            case AIBehaviorMode.Dogfight: DoDogfight(); break;
            case AIBehaviorMode.AttackRun: DoAttackRun(); break;
            case AIBehaviorMode.EvasiveBreak: DoEvasiveBreak(); break;
            case AIBehaviorMode.Retreat: DoRetreat(); break;
            case AIBehaviorMode.Hold: DoHold(); break;
        }
    }

    void ChangeState(AIBehaviorMode newState)
    {
        previousState = currentState;
        currentState = newState;
        stateEnterTime = Time.time;

        movement.ClearWaypoint(WaypointPriority.Critical);

        switch (currentState)
        {
            case AIBehaviorMode.AttackRun:
                committedToRun = false;
                break;
            case AIBehaviorMode.Dogfight:
                InitializeCircling();
                break;
            case AIBehaviorMode.Wander:
                currentWanderTarget = Vector3.zero;
                break;
        }
    }

    AIBehaviorMode DetermineTacticalState()
    {
        if (currentEnemy == null) return behaviorMode;

        float dist = Vector3.Distance(transform.position, currentEnemy.transform.position);

        if (dist > engagementRange) return behaviorMode;

        switch (currentState)
        {
            case AIBehaviorMode.Approach:
                if (dist < attackInitiateRange)
                {
                    if (Time.time - lastAttackTime > attackCooldown)
                        return AIBehaviorMode.AttackRun;
                    else
                        return AIBehaviorMode.Dogfight;
                }
                return AIBehaviorMode.Approach;

            case AIBehaviorMode.Dogfight:
                if (dist > attackInitiateRange * 1.5f) return AIBehaviorMode.Approach;

                if (Time.time - lastAttackTime > attackCooldown && dist < attackInitiateRange)
                    return AIBehaviorMode.AttackRun;

                return AIBehaviorMode.Dogfight;

            case AIBehaviorMode.AttackRun:
                Vector3 toEnemy = currentEnemy.transform.position - transform.position;
                float dot = Vector3.Dot(transform.forward, toEnemy.normalized);

                // CRITICAL FIX:
                // Only break if enemy is BEHIND us (dot < 0) 
                // AND we are more than 'breakTriggerDistance' (80 units) away
                if (dot < 0 && dist > breakTriggerDistance)
                    return AIBehaviorMode.EvasiveBreak;

                // Timeout safety (5 seconds max run)
                if (Time.time - stateEnterTime > 5f)
                    return AIBehaviorMode.EvasiveBreak;

                return AIBehaviorMode.AttackRun;

            case AIBehaviorMode.EvasiveBreak:
                if (Time.time - stateEnterTime > breakDuration)
                {
                    lastAttackTime = Time.time;
                    return AIBehaviorMode.Dogfight;
                }
                return AIBehaviorMode.EvasiveBreak;

            default:
                return AIBehaviorMode.Approach;
        }
    }

    // --- MANEUVERS ---

    void DoApproach()
    {
        if (currentEnemy == null) return;
        Vector3 target = currentEnemy.transform.position;
        // Aim slightly short so we don't ram before the state change
        movement.SetWaypoint(target - (target - transform.position).normalized * 20f, WaypointPriority.High);
    }

    void DoAttackRun()
    {
        if (currentEnemy == null) return;

        Vector3 currentTargetPos = GetPredictedEnemyPosition();

        if (!committedToRun)
        {
            // LOCK THE VECTOR:
            // We calculate the direction once. We do NOT update it during the run.
            // This prevents the ship from spinning around when it passes the enemy.
            cachedAttackDir = (currentTargetPos - transform.position).normalized;

            // Calculate Strafe Offset (Left or Right)
            Vector3 strafeDir = Vector3.Cross(cachedAttackDir, Vector3.up).normalized;
            float sideSign = (Random.value > 0.5f) ? 1f : -1f;

            cachedStrafeOffset = strafeDir * sideSign * strafeOffsetDistance;

            committedToRun = true;
        }

        // Apply Logic:
        // Waypoint = Moving Enemy Pos + (Locked Direction * 120) + (Locked Offset)
        // This keeps the waypoint parallel to enemy movement but fixed in orientation.
        Vector3 flyThroughPoint = currentTargetPos + (cachedAttackDir * overshootDistance) + cachedStrafeOffset;

        // Flatten Y
        flyThroughPoint.y = currentTargetPos.y;

        movement.SetWaypoint(flyThroughPoint, WaypointPriority.Critical);
    }

    void DoEvasiveBreak()
    {
        if (currentEnemy == null) return;

        // Break OUTWARD relative to where we are now
        Vector3 toEnemy = (currentEnemy.transform.position - transform.position).normalized;
        Vector3 right = Vector3.Cross(toEnemy, Vector3.up);

        // Detect which side we are already on to continue momentum
        // If we are to the 'right' of enemy, break right.
        Vector3 myRelPos = transform.position - currentEnemy.transform.position;
        float sideCheck = Vector3.Dot(myRelPos, right);
        float sideSign = (sideCheck > 0) ? 1f : -1f;

        Vector3 breakDir = (right * sideSign) + (transform.forward * 0.5f);
        breakDir.y = 0f;
        breakDir.Normalize();

        Vector3 breakPoint = transform.position + (breakDir * 100f);
        movement.SetWaypoint(breakPoint, WaypointPriority.Critical);
    }

    void InitializeCircling()
    {
        if (currentEnemy == null) return;
        circlingCenter = currentEnemy.transform.position;
        Vector3 toShip = transform.position - circlingCenter;
        circlingAngle = Mathf.Atan2(toShip.z, toShip.x) * Mathf.Rad2Deg;
        lastCircleUpdateTime = 0f; // Force immediate update
    }

    void DoDogfight()
    {
        if (currentEnemy == null) return;

        circlingAngle += circlingSpeed * Time.deltaTime;

        if (Time.time - lastCircleUpdateTime >= 0.2f)
        {
            lastCircleUpdateTime = Time.time;
            Vector3 targetPos = GetPredictedEnemyPosition();

            float dynamicRadius = baseCirclingRadius + Mathf.Sin(Time.time * oscillationFreq) * radiusOscillation;
            float angleRad = circlingAngle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(angleRad) * dynamicRadius,
                0f,
                Mathf.Sin(angleRad) * dynamicRadius
            );

            movement.SetWaypoint(targetPos + offset, WaypointPriority.High);
        }
    }

    void DoWander()
    {
        if (isWaiting)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0f) { isWaiting = false; currentWanderTarget = Vector3.zero; }
            else { movement.ClearWaypoint(WaypointPriority.Low); return; }
        }

        if (currentWanderTarget == Vector3.zero || Vector3.Distance(transform.position, currentWanderTarget) < waypointReachDistance)
        {
            if (wanderWaitTime > 0f && currentWanderTarget != Vector3.zero)
            {
                isWaiting = true; wanderWaitTimer = wanderWaitTime; movement.ClearWaypoint(WaypointPriority.Low); return;
            }
            currentWanderTarget = GetRandomWanderPoint();
        }
        movement.SetWaypoint(currentWanderTarget, WaypointPriority.Low);
    }

    Vector3 GetRandomWanderPoint()
    {
        float distance = Random.Range(minWanderDistance, maxWanderDistance);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 newPoint = wanderCenter + offset;
        if ((newPoint - wanderCenter).magnitude > wanderRadius) newPoint = wanderCenter + (newPoint - wanderCenter).normalized * wanderRadius;
        return newPoint;
    }

    void DoRetreat()
    {
        if (currentEnemy == null) return;
        Vector3 away = (transform.position - currentEnemy.transform.position).normalized;
        movement.SetWaypoint(transform.position + away * 60f, WaypointPriority.Critical);
    }

    void DoHold() { movement.ClearWaypoint(WaypointPriority.Medium); }

    Vector3 GetPredictedEnemyPosition()
    {
        if (currentEnemy == null) return transform.position;
        Rigidbody rb = currentEnemy.GetComponent<Rigidbody>();
        return rb != null ? currentEnemy.transform.position + rb.linearVelocity * 0.75f : currentEnemy.transform.position;
    }

    GameObject FindNearestEnemy()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, engagementRange);
        GameObject best = null;
        float minDist = float.MaxValue;

        var myAlign = GetComponent<Alignment>();
        bool iHaveAlign = myAlign != null;

        foreach (var col in colliders)
        {
            if (col.transform.root == transform.root) continue;
            var targetAlign = col.GetComponent<Alignment>();
            if (targetAlign == null) continue;
            if (iHaveAlign && targetAlign.isFriendly == myAlign.isFriendly) continue;

            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < minDist) { minDist = d; best = col.gameObject; }
        }
        return best;
    }
}

public enum AIBehaviorMode { Wander, Approach, Dogfight, AttackRun, EvasiveBreak, Retreat, Hold }
public enum WaypointPriority { None = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }
